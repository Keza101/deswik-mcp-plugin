using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Deswik.Bridge.Services;

namespace Deswik.Addin;

/// <summary>
/// Deswik.CAD plugin that bridges the running Deswik instance to the external
/// MCP server over TCP (127.0.0.1:9595).
///
/// Deswik's Plugin Manager loads plugins by reflection (duck typing), NOT via
/// an interface. The contract (verified against Deswik.Common.Plugins.Host
/// 2025.2 string heap and Deswik.ASD's AutoStopeDesigner class) is:
///   - ctor() and/or ctor(Deswik.Graphics.Application)
///   - property: Deswik.Graphics.Application Application { get; set; }
///   - void Load(IWin32Window) or void Load(IWin32Window, string[])
///   - void Unload()
///   - optional: bool Unloading(), string ProductName, string ProductVersion,
///     void GetCommands(ref string[], ref string[]), void ExecuteByID(string)
///
/// Register in Deswik.CAD: Interface | Add-ons | Plugins, add Deswik.Addin.dll
/// (use "Specify specific path to plugin DLL"), Startup class: DeswikMcpAddin.
/// </summary>
public class DeswikMcpAddin
{
    private BridgeClient? _bridgeClient;
    private SchedulerService? _schedulerService;
    private CadService? _cadService;
    private CadReader? _cadReader;
    private Deswik.Graphics.Application? _cadApplication;
    private GuardedWriteCoordinator? _guardedWrites;
    private CancellationTokenSource? _cts;
    private SynchronizationContext? _uiContext;
    private McpStatusControl? _statusControl;
    private int _commandCount;
    private string _lastCommand = "-";
    private bool _isLoaded;

    private static readonly string LogFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeswikMcp", "plugin.log");

    public DeswikMcpAddin()
    {
    }

    public DeswikMcpAddin(Deswik.Graphics.Application application)
    {
        Application = application;
    }

    /// <summary>Set by the plugin host (or via the ctor) to the live CAD application.</summary>
    public Deswik.Graphics.Application? Application { get; set; }

    public string ProductName => "Deswik MCP Bridge";

    /// <summary>
    /// The host compares this against the application version and warns on
    /// mismatch, so report the version of the Deswik assemblies we run against.
    /// </summary>
    public string ProductVersion
    {
        get
        {
            try
            {
                var loc = typeof(Deswik.Graphics.Application).Assembly.Location;
                return FileVersionInfo.GetVersionInfo(loc).FileVersion ?? "1.0.0";
            }
            catch
            {
                return "1.0.0";
            }
        }
    }

    public void Load(IWin32Window owner) => Load(owner, Array.Empty<string>());

    public void Load(IWin32Window owner, string[] args)
    {
        if (_isLoaded) return;

        Log("Loading MCP Bridge plugin...");

        // Load runs on CAD's UI thread; keep its context so command handlers
        // (which run on the bridge receive thread) can touch CAD objects safely.
        _uiContext = SynchronizationContext.Current;

        _schedulerService = new SchedulerService();
        _cadService = new CadService();
        _cadService.Initialize();

        _bridgeClient = new BridgeClient("127.0.0.1", 9595);
        _bridgeClient.OnLog += m => Log($"[Bridge] {m}");
        _bridgeClient.OnCommand += OnBridgeCommand;
        _bridgeClient.OnDisconnected += OnBridgeDisconnected;

        _cts = new CancellationTokenSource();
        _isLoaded = true;

        // Connect in the background with retry so CAD's UI thread is never
        // blocked and the plugin survives the MCP server starting later.
        _ = Task.Run(() => ConnectLoop(_cts.Token));

        Log("MCP Bridge plugin loaded");
    }

    public bool Unloading() => true;

    public void Unload()
    {
        Log("Unloading MCP Bridge plugin...");
        _isLoaded = false;
        _cts?.Cancel();
        _bridgeClient?.Disconnect();
        _bridgeClient?.Dispose();
        _bridgeClient = null;
        _schedulerService = null;
        _cadService = null;
        _cadReader = null;
        _cadApplication = null;
        _guardedWrites = null;
        Log("MCP Bridge plugin unloaded");
    }

    #region Dock panel (plugin host UI)

    /// <summary>
    /// Called by the plugin host after load; the returned control is docked
    /// as this plugin's panel (same mechanism as UGDB/ASD dock windows).
    /// </summary>
    public object RegisterMainControl()
    {
        _statusControl ??= new McpStatusControl(this);
        return _statusControl;
    }

    public int MinimumStartWidth => 300;
    public int MinimumStartHeight => 220;

    internal bool IsBridgeConnected => _bridgeClient?.IsConnected ?? false;
    internal int CommandCount => _commandCount;
    internal string LastCommand => _lastCommand;
    internal string LogFilePath => LogFile;

    internal void RequestReconnect()
    {
        if (_isLoaded && _cts != null)
            _ = Task.Run(() => ConnectLoop(_cts.Token));
    }

    #endregion

    /// <summary>Commands exposed to process maps via the Plugins node command.</summary>
    public void GetCommands(ref string[] commandIds, ref string[] commandNames)
    {
        commandIds = new[] { "MCP_STATUS", "MCP_RECONNECT" };
        commandNames = new[] { "MCP Bridge Status", "MCP Bridge Reconnect" };
    }

    public void ExecuteByID(string commandId)
    {
        switch (commandId)
        {
            case "MCP_STATUS":
                MessageBox.Show(
                    $"MCP Bridge loaded: {_isLoaded}\n" +
                    $"Bridge connected: {_bridgeClient?.IsConnected ?? false}\n" +
                    $"Application set: {Application != null}\n" +
                    $"Log: {LogFile}",
                    "Deswik MCP Bridge");
                break;

            case "MCP_RECONNECT":
                if (_isLoaded && _cts != null)
                    _ = Task.Run(() => ConnectLoop(_cts.Token));
                break;

            default:
                Log($"Unknown command: {commandId}");
                break;
        }
    }

    #region Bridge connection

    private async Task ConnectLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var client = _bridgeClient;
            if (client == null) return;
            if (client.IsConnected) return;

            try
            {
                if (await client.ConnectAsync())
                {
                    Log("Connected to MCP bridge server");
                    return;
                }
            }
            catch (Exception ex)
            {
                Log($"Connect attempt failed: {ex.Message}");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private void OnBridgeDisconnected()
    {
        if (!_isLoaded || _cts == null) return;
        Log("Bridge connection lost - reconnecting...");
        var ct = _cts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
                await ConnectLoop(ct);
            }
            catch (OperationCanceledException)
            {
            }
        });
    }

    private void OnBridgeCommand(string id, string action, System.Text.Json.JsonElement parameters)
    {
        try
        {
            _commandCount++;
            _lastCommand = $"{action} @ {DateTime.Now:HH:mm:ss}";
            Log($"Command received: {action} (id={id})");

            switch (action)
            {
                case "get_schedule_info":
                    HandleGetScheduleInfo(id);
                    break;
                case "get_tasks":
                    HandleGetTasks(id, parameters);
                    break;
                case "get_task_details":
                    HandleGetTaskDetails(id, parameters);
                    break;
                case "get_resources":
                    HandleGetResources(id);
                    break;
                case "get_dependencies":
                    HandleGetDependencies(id, parameters);
                    break;
                case "update_task_fields":
                    HandleUpdateTaskFields(id, parameters);
                    break;
                case "query_block_status":
                    HandleQueryBlockStatus(id, parameters);
                    break;
                case "get_cad_document":
                    HandleGetCadDocument(id);
                    break;
                case "get_cad_layers":
                    HandleGetCadLayers(id);
                    break;
                case "get_cad_layer_attributes":
                    HandleGetCadLayerAttributes(id, parameters);
                    break;
                case "get_cad_elements":
                    HandleGetCadElements(id, parameters);
                    break;
                case "create_cad_layer":
                    HandleCreatePreviewLayer(id, parameters);
                    break;
                case "draw_cad_text":
                    SendError(id, action, "Writer requires the guarded write flow", "forbidden_unfenced");
                    break;
                case "get_cad_selection":
                    HandleGetCadSelection(id);
                    break;
                case "draw_cad_polylines":
                    SendError(id, action, "Writer requires the guarded write flow", "forbidden_unfenced");
                    break;
                case "get_cad_polyface_info":
                    HandleGetCadPolyfaceInfo(id, parameters);
                    break;
                case "slice_cad_polyface":
                    SendError(id, action, "Writer requires the guarded write flow", "forbidden_unfenced");
                    break;
                case "draw_cad_blastholes":
                    SendError(id, action, "Writer requires the guarded write flow", "forbidden_unfenced");
                    break;
                case "get_cad_blasthole_details":
                    HandleGetCadBlastHoleDetails(id, parameters);
                    break;
                case "get_cad_ugdrillhole_details":
                    HandleGetCadUGDrillHoleDetails(id, parameters);
                    break;
                case "get_cad_polylines_under":
                    HandleGetCadPolylinesUnder(id, parameters);
                    break;
                case "draw_cad_ugdrillholes":
                    SendError(id, action, "Writer requires the guarded write flow", "forbidden_unfenced");
                    break;
                case "preview_ugdrillholes":
                    HandlePreviewUGDrillHoles(id, parameters);
                    break;
                case "prepare_rollback_ugdrillholes":
                    HandlePrepareRollback(id, parameters);
                    break;
                case "_commit_ugdrillholes_authorized":
                    HandleAuthorizedCommit(id, parameters);
                    break;
                case "_rollback_ugdrillholes_authorized":
                    HandleAuthorizedRollback(id, parameters);
                    break;
                case "send_hello":
                    HandleSendHello(id);
                    break;
                default:
                    Log($"Unknown command: {action}");
                    SendError(id, action, $"Unknown command: {action}");
                    break;
            }
        }
        catch (GuardedWriteException ex)
        {
            Log($"Guarded write refused {action}: {ex.Message}");
            SendError(id, action, ex.Message, ex.ErrorCode);
        }
        catch (Exception ex)
        {
            Log($"Error handling command {action}: {ex}");
            SendError(id, action, ex.Message);
        }
    }

    private void SendError(string id, string action, string error, string errorCode = "ADDIN_ERROR")
    {
        _bridgeClient?.SendAsync(new
        {
            id,
            action = action + "_result",
            data = (object?)null,
            error,
            errorCode
        });
    }

    #endregion

    #region Command handlers

    private void HandleGetScheduleInfo(string id)
    {
        if (_schedulerService == null) return;

        var info = _schedulerService.GetScheduleInfo();
        _bridgeClient?.SendAsync(new
        {
            id,
            action = "schedule_info_result",
            data = new
            {
                fileName = info.FileName,
                directoryName = info.DirectoryName,
                targetStart = info.TargetStart,
                totalTasks = info.TotalTasks,
                totalResources = info.TotalResources,
                totalDependencies = info.TotalDependencies,
                version = info.Version
            }
        });
    }

    private void HandleGetTasks(string id, System.Text.Json.JsonElement parameters)
    {
        if (_schedulerService == null) return;

        int limit = parameters.ValueKind == System.Text.Json.JsonValueKind.Object && parameters.TryGetProperty("limit", out var l) ? l.GetInt32() : 100;
        int offset = parameters.ValueKind == System.Text.Json.JsonValueKind.Object && parameters.TryGetProperty("offset", out var o) ? o.GetInt32() : 0;
        string? filter = parameters.ValueKind == System.Text.Json.JsonValueKind.Object && parameters.TryGetProperty("filter", out var f) ? f.GetString() : null;

        var tasks = _schedulerService.GetTasks(limit, offset, filter);
        _bridgeClient?.SendAsync(new
        {
            id,
            action = "tasks_result",
            data = tasks.Select(t => new
            {
                t.Id,
                t.Name,
                t.Start,
                t.Finish,
                Duration = t.Duration.TotalHours,
                t.UniqueId,
                t.DistributionCount
            }).ToList()
        });
    }

    private void HandleGetTaskDetails(string id, System.Text.Json.JsonElement parameters)
    {
        if (_schedulerService == null) return;

        string taskId = parameters.GetProperty("taskId").GetString() ?? "";
        var task = _schedulerService.GetTaskDetails(taskId);

        _bridgeClient?.SendAsync(new
        {
            id,
            action = "task_details_result",
            data = task != null ? new
            {
                task.Id,
                task.Name,
                task.Start,
                task.Finish,
                Duration = task.Duration.TotalHours,
                task.UniqueId,
                task.DistributionCount,
                task.CustomFields
            } : null
        });
    }

    private void HandleGetResources(string id)
    {
        if (_schedulerService == null) return;

        var resources = _schedulerService.GetResources();
        _bridgeClient?.SendAsync(new
        {
            id,
            action = "resources_result",
            data = resources.Select(r => new
            {
                r.Id,
                r.Name,
                r.IsPool,
                r.MaxUnits,
                r.Rate,
                r.Description
            }).ToList()
        });
    }

    private void HandleGetDependencies(string id, System.Text.Json.JsonElement parameters)
    {
        if (_schedulerService == null) return;

        string? taskId = parameters.ValueKind == System.Text.Json.JsonValueKind.Object && parameters.TryGetProperty("taskId", out var t) ? t.GetString() : null;
        var deps = _schedulerService.GetDependencies(taskId);

        _bridgeClient?.SendAsync(new
        {
            id,
            action = "dependencies_result",
            data = deps.Select(d => new
            {
                d.Id,
                d.FromTaskId,
                d.ToTaskId,
                d.LinkType,
                d.Lag,
                d.PercentageOverlap,
                d.Layer
            }).ToList()
        });
    }

    private void HandleUpdateTaskFields(string id, System.Text.Json.JsonElement parameters)
    {
        if (_schedulerService == null) return;

        string taskId = parameters.GetProperty("taskId").GetString() ?? "";
        var fieldsJson = parameters.GetProperty("fields");
        var fields = new Dictionary<string, object>();

        foreach (var prop in fieldsJson.EnumerateObject())
        {
            fields[prop.Name] = prop.Value.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => prop.Value.GetString() ?? "",
                System.Text.Json.JsonValueKind.Number => prop.Value.GetDouble(),
                System.Text.Json.JsonValueKind.True => true,
                System.Text.Json.JsonValueKind.False => false,
                _ => prop.Value.ToString()
            };
        }

        var success = _schedulerService.UpdateTaskFields(taskId, fields);
        _bridgeClient?.SendAsync(new
        {
            id,
            action = "update_result",
            data = new { success, taskId }
        });
    }

    private void HandleQueryBlockStatus(string id, System.Text.Json.JsonElement parameters)
    {
        if (_schedulerService == null) return;

        string blockId = parameters.GetProperty("blockId").GetString() ?? "";
        var status = _schedulerService.QueryBlockStatus(blockId);

        _bridgeClient?.SendAsync(new
        {
            id,
            action = "block_status_result",
            data = status != null ? new
            {
                status.BlockId,
                status.Material,
                status.ScheduledVolume,
                status.ScheduledPeriod
            } : null
        });
    }

    /// <summary>
    /// Runs a CAD read on the UI thread (CAD objects are not thread-safe;
    /// bridge commands arrive on the receive-loop thread).
    /// </summary>
    private T OnUiThread<T>(Func<T> read)
    {
        if (_uiContext == null) return read();

        T result = default!;
        Exception? error = null;
        _uiContext.Send(_ =>
        {
            try { result = read(); }
            catch (Exception ex) { error = ex; }
        }, null);
        if (error != null) throw error;
        return result;
    }

    private CadReader RequireCadReader()
    {
        var app = Deswik.Common.Utilities.DeswikApplication.CurrentOpenDoc as Deswik.Graphics.Application
            ?? throw new InvalidOperationException("No active CAD document is attached to the plugin");
        if (!ReferenceEquals(app, _cadApplication))
        {
            _cadApplication = app;
            _cadReader = new CadReader(app);
            _guardedWrites = null;
        }
        return _cadReader!;
    }

    private GuardedWriteCoordinator RequireGuardedWrites()
    {
        var reader = RequireCadReader();
        return _guardedWrites ??= new GuardedWriteCoordinator(reader);
    }

    private void HandleGetCadDocument(string id)
    {
        var data = OnUiThread(() => RequireCadReader().GetDocumentInfo());
        _bridgeClient?.SendAsync(new
        {
            id,
            action = "cad_document_result",
            data
        });
    }

    private void HandleGetCadLayers(string id)
    {
        var data = OnUiThread(() => RequireCadReader().GetLayers());
        _bridgeClient?.SendAsync(new
        {
            id,
            action = "cad_layers_result",
            data
        });
    }

    private void HandleGetCadLayerAttributes(string id, System.Text.Json.JsonElement parameters)
    {
        string layer = parameters.GetProperty("layer").GetString()
            ?? throw new ArgumentException("'layer' is required");
        int limit = parameters.TryGetProperty("limit", out var l) ? l.GetInt32() : 10000;

        var data = OnUiThread(() => RequireCadReader().GetLayerAttributes(layer, limit));
        _bridgeClient?.SendAsync(new
        {
            id,
            action = "cad_layer_attributes_result",
            data
        });
    }

    private void HandleGetCadElements(string id, System.Text.Json.JsonElement parameters)
    {
        bool hasParams = parameters.ValueKind == System.Text.Json.JsonValueKind.Object;
        string? layer = hasParams && parameters.TryGetProperty("layer", out var l) ? l.GetString() : null;
        int limit = hasParams && parameters.TryGetProperty("limit", out var lim) ? lim.GetInt32() : 500;

        var data = OnUiThread(() => RequireCadReader().GetElements(layer, limit));
        _bridgeClient?.SendAsync(new
        {
            id,
            action = "cad_elements_result",
            data
        });
    }

    private void HandleCreatePreviewLayer(string id, System.Text.Json.JsonElement parameters)
    {
        string name = parameters.GetProperty("name").GetString()
            ?? throw new ArgumentException("'name' is required");
        if (!string.Equals(name, GuardedWriteCoordinator.PreviewLayer, StringComparison.Ordinal))
            throw new GuardedWriteException("forbidden_unfenced", "Only the exact _MCP_PREVIEW layer may be created without a token");

        var data = OnUiThread(() => RequireCadReader().CreateLayer(name));
        _bridgeClient?.SendAsync(new
        {
            id,
            action = "create_cad_layer_result",
            data
        });
    }

    private void HandlePreviewUGDrillHoles(string id, System.Text.Json.JsonElement parameters)
    {
        var specs = ParseGuardedHoles(parameters);
        var preview = OnUiThread(() => RequireGuardedWrites().Preview(specs));
        _bridgeClient?.SendAsync(new
        {
            id,
            action = "preview_ugdrillholes_result",
            data = preview
        }).GetAwaiter().GetResult();

        var accepted = OnUiThread(() => MessageBox.Show(
            RenderManifest(preview.Manifest),
            "Approve MCP UGDrillHole commit",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2) == DialogResult.Yes);
        if (!accepted) return;

        _bridgeClient?.SendAsync(new
        {
            id = preview.ApprovalId,
            action = "_human_write_approval",
            data = new { preview.ApprovalId }
        }).GetAwaiter().GetResult();
    }

    private void HandleAuthorizedCommit(string id, System.Text.Json.JsonElement parameters)
    {
        var binding = ParseBinding(parameters);
        var result = OnUiThread(() => RequireGuardedWrites().Commit(binding));
        _bridgeClient?.SendAsync(new { id, action = "commit_ugdrillholes_result", data = result });
    }

    private void HandlePrepareRollback(string id, System.Text.Json.JsonElement parameters)
    {
        var commitId = GetString(parameters, "commitId", "CommitId");
        var rollback = OnUiThread(() => RequireGuardedWrites().PrepareRollback(commitId));
        _bridgeClient?.SendAsync(new
        {
            id,
            action = "prepare_rollback_ugdrillholes_result",
            data = new { rollback.ApprovalId, rollback.CommitId, rollback.Manifest, rollback.ManifestHash, rollback.Binding }
        }).GetAwaiter().GetResult();

        var accepted = OnUiThread(() => MessageBox.Show(
            RenderManifest(rollback.Manifest),
            "Approve MCP UGDrillHole rollback",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2) == DialogResult.Yes);
        if (!accepted) return;
        _bridgeClient?.SendAsync(new
        {
            id = rollback.ApprovalId,
            action = "_human_write_approval",
            data = new { rollback.ApprovalId }
        }).GetAwaiter().GetResult();
    }

    private void HandleAuthorizedRollback(string id, System.Text.Json.JsonElement parameters)
    {
        var binding = ParseBinding(parameters);
        var result = OnUiThread(() => RequireGuardedWrites().Rollback(binding));
        _bridgeClient?.SendAsync(new { id, action = "rollback_ugdrillholes_result", data = result });
    }

    private static List<UGHoleSpec> ParseGuardedHoles(System.Text.Json.JsonElement parameters)
    {
        var specs = new List<UGHoleSpec>();
        var order = 0;
        foreach (var hole in parameters.GetProperty("holes").EnumerateArray())
        {
            double[] Point(string name)
            {
                var values = hole.GetProperty(name).EnumerateArray().Select(value => value.GetDouble()).ToArray();
                if (values.Length != 3 || values.Any(value => double.IsNaN(value) || double.IsInfinity(value)))
                    throw new ArgumentException($"'{name}' must contain three finite numbers");
                return values;
            }
            var diameter = hole.TryGetProperty("diameter", out var diameterValue)
                ? diameterValue.GetDouble() : 0.089;
            if (diameter <= 0 || double.IsNaN(diameter) || double.IsInfinity(diameter))
                throw new ArgumentException("'diameter' must be a positive finite number");
            var holeId = SafeIdentifier(hole.TryGetProperty("holeId", out var identifier)
                ? identifier.GetString() : null, $"H{order + 1}");
            var pivotId = SafeIdentifier(hole.TryGetProperty("pivotId", out var pivot)
                ? pivot.GetString() : null, "P1");
            specs.Add(new UGHoleSpec(
                Point("pivot"), Point("collar"), Point("toe"),
                "MCP_APPROVED", pivotId, holeId, diameter,
                $"{diameter * 1000:0} mm", order, null, 0));
            order++;
        }
        return specs;
    }

    private static ApprovalBinding ParseBinding(System.Text.Json.JsonElement parameters) => new(
        GetString(parameters, "approvalId", "ApprovalId"),
        GetString(parameters, "operation", "Operation"),
        GetString(parameters, "documentGuid", "DocumentGuid"),
        GetString(parameters, "drawingPath", "DrawingPath"),
        GetString(parameters, "targetLayer", "TargetLayer"),
        GetString(parameters, "manifestHash", "ManifestHash"),
        GetString(parameters, "sourceFingerprint", "SourceFingerprint"),
        GetString(parameters, "recordId", "RecordId"));

    private static string GetString(System.Text.Json.JsonElement value, string camel, string pascal)
    {
        if (value.TryGetProperty(camel, out var property) || value.TryGetProperty(pascal, out property))
            return property.GetString() ?? throw new ArgumentException($"'{camel}' is required");
        throw new ArgumentException($"'{camel}' is required");
    }

    private static string SafeIdentifier(string? value, string fallback)
    {
        var safe = new string((value ?? "").Where(character =>
            char.IsLetterOrDigit(character) || character is '-' or '_' or '.').Take(32).ToArray());
        return string.IsNullOrEmpty(safe) ? fallback : safe;
    }

    private static string RenderManifest(ChangeManifest manifest)
    {
        static string Line(string value) => new(value
            .Where(character => character is not '\r' and not '\n' and not '|').Take(240).ToArray());
        return
            $"Operation: {Line(manifest.Operation)}\n" +
            $"Document: {Line(manifest.DocumentGuid)}\n" +
            $"Drawing: {Line(manifest.DrawingPath)}\n" +
            $"Layer: {Line(manifest.TargetLayer)}\n" +
            $"Holes: {manifest.HoleCount}\n" +
            $"Hole IDs: {Line(string.Join(", ", manifest.HoleIds))}\n" +
            $"Total metres: {manifest.TotalMetres:0.###}";
    }

    private void HandleDrawCadText(string id, System.Text.Json.JsonElement parameters)
    {
        string layer = parameters.GetProperty("layer").GetString()
            ?? throw new ArgumentException("'layer' is required");
        string text = parameters.GetProperty("text").GetString()
            ?? throw new ArgumentException("'text' is required");
        double x = parameters.TryGetProperty("x", out var px) ? px.GetDouble() : 0;
        double y = parameters.TryGetProperty("y", out var py) ? py.GetDouble() : 0;
        double z = parameters.TryGetProperty("z", out var pz) ? pz.GetDouble() : 0;
        double height = parameters.TryGetProperty("height", out var ph) ? ph.GetDouble() : 5;

        var data = OnUiThread(() => RequireCadReader().DrawText(layer, text, x, y, z, height));
        _bridgeClient?.SendAsync(new
        {
            id,
            action = "draw_cad_text_result",
            data
        });
    }

    private void HandleGetCadSelection(string id)
    {
        var data = OnUiThread(() => RequireCadReader().GetSelection());
        _bridgeClient?.SendAsync(new
        {
            id,
            action = "cad_selection_result",
            data
        });
    }

    private void HandleDrawCadPolylines(string id, System.Text.Json.JsonElement parameters)
    {
        string layer = parameters.GetProperty("layer").GetString()
            ?? throw new ArgumentException("'layer' is required");

        var specs = new List<PolylineSpec>();
        foreach (var pl in parameters.GetProperty("polylines").EnumerateArray())
        {
            var points = new List<double[]>();
            foreach (var pt in pl.GetProperty("points").EnumerateArray())
            {
                var coords = pt.EnumerateArray().Select(v => v.GetDouble()).ToArray();
                if (coords.Length >= 3) points.Add(coords);
            }

            bool closed = pl.TryGetProperty("closed", out var c) && c.GetBoolean();
            int r = 255, g = 0, b = 0;
            if (pl.TryGetProperty("color", out var col) && col.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                var rgb = col.EnumerateArray().Select(v => v.GetInt32()).ToArray();
                if (rgb.Length >= 3) (r, g, b) = (rgb[0], rgb[1], rgb[2]);
            }
            string? label = pl.TryGetProperty("label", out var lb) ? lb.GetString() : null;

            specs.Add(new PolylineSpec(points, closed, r, g, b, label));
        }

        var texts = new List<TextSpec>();
        if (parameters.TryGetProperty("texts", out var textsEl) && textsEl.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var t in textsEl.EnumerateArray())
            {
                int r = 255, g = 255, b = 255;
                if (t.TryGetProperty("color", out var col) && col.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    var rgb = col.EnumerateArray().Select(v => v.GetInt32()).ToArray();
                    if (rgb.Length >= 3) (r, g, b) = (rgb[0], rgb[1], rgb[2]);
                }
                texts.Add(new TextSpec(
                    t.GetProperty("text").GetString() ?? "",
                    t.GetProperty("x").GetDouble(),
                    t.GetProperty("y").GetDouble(),
                    t.GetProperty("z").GetDouble(),
                    t.TryGetProperty("height", out var h) ? h.GetDouble() : 0.5,
                    r, g, b));
            }
        }

        var data = OnUiThread(() => RequireCadReader().DrawPolylines(layer, specs, texts));
        _bridgeClient?.SendAsync(new
        {
            id,
            action = "draw_cad_polylines_result",
            data
        });
    }

    private void HandleGetCadPolyfaceInfo(string id, System.Text.Json.JsonElement parameters)
    {
        ulong handle = parameters.GetProperty("handle").GetUInt64();
        var data = OnUiThread(() => RequireCadReader().GetPolyfaceInfo(handle));
        _bridgeClient?.SendAsync(new
        {
            id,
            action = "cad_polyface_info_result",
            data
        });
    }

    private void HandleDrawCadBlastHoles(string id, System.Text.Json.JsonElement parameters)
    {
        string layer = parameters.GetProperty("layer").GetString()
            ?? throw new ArgumentException("'layer' is required");

        var specs = new List<BlastHoleSpec>();
        foreach (var h in parameters.GetProperty("holes").EnumerateArray())
        {
            double[] Arr(string name) => h.GetProperty(name).EnumerateArray()
                .Select(v => v.GetDouble()).ToArray();
            specs.Add(new BlastHoleSpec(
                Arr("collar"),
                Arr("toe"),
                h.GetProperty("id").GetString() ?? "",
                h.TryGetProperty("diameter", out var d) ? d.GetDouble() : 0.076,
                h.TryGetProperty("burden", out var b) ? b.GetDouble() : 0.0,
                h.TryGetProperty("spacing", out var sp) ? sp.GetDouble() : 0.0));
        }

        var data = OnUiThread(() => RequireCadReader().DrawBlastHoles(layer, specs));
        _bridgeClient?.SendAsync(new
        {
            id,
            action = "draw_cad_blastholes_result",
            data
        });
    }

    private void HandleGetCadBlastHoleDetails(string id, System.Text.Json.JsonElement parameters)
    {
        ulong handle = parameters.GetProperty("handle").GetUInt64();
        var data = OnUiThread(() => RequireCadReader().GetBlastHoleDetails(handle));
        _bridgeClient?.SendAsync(new
        {
            id,
            action = "cad_blasthole_details_result",
            data
        });
    }

    private void HandleGetCadUGDrillHoleDetails(string id, System.Text.Json.JsonElement parameters)
    {
        ulong handle = parameters.GetProperty("handle").GetUInt64();
        var data = OnUiThread(() => RequireCadReader().GetUGDrillHoleDetails(handle));
        _bridgeClient?.SendAsync(new
        {
            id,
            action = "cad_ugdrillhole_details_result",
            data
        });
    }

    private void HandleGetCadPolylinesUnder(string id, System.Text.Json.JsonElement parameters)
    {
        string prefix = parameters.GetProperty("layerPrefix").GetString()
            ?? throw new ArgumentException("'layerPrefix' is required");
        var data = OnUiThread(() => RequireCadReader().GetPolylinesUnder(prefix));
        _bridgeClient?.SendAsync(new
        {
            id,
            action = "cad_polylines_under_result",
            data
        });
    }

    private void HandleDrawCadUGDrillHoles(string id, System.Text.Json.JsonElement parameters)
    {
        string layer = parameters.GetProperty("layer").GetString()
            ?? throw new ArgumentException("'layer' is required");
        string ringId = parameters.GetProperty("ringId").GetString()
            ?? throw new ArgumentException("'ringId' is required");

        var specs = new List<UGHoleSpec>();
        int order = 0;
        foreach (var h in parameters.GetProperty("holes").EnumerateArray())
        {
            double[] Arr(string name) => h.GetProperty(name).EnumerateArray()
                .Select(v => v.GetDouble()).ToArray();
            double diameter = h.TryGetProperty("diameter", out var d) ? d.GetDouble() : 0.089;
            specs.Add(new UGHoleSpec(
                Arr("pivot"), Arr("collar"), Arr("toe"),
                ringId,
                h.GetProperty("pivotId").GetString() ?? "1",
                h.TryGetProperty("holeId", out var hid) ? hid.GetString() ?? $"H{order + 1}" : $"H{order + 1}",
                diameter,
                h.TryGetProperty("diameterString", out var ds)
                    ? ds.GetString() ?? $"{diameter * 1000:0} mm"
                    : $"{diameter * 1000:0} mm",
                h.TryGetProperty("orderIndex", out var oi) ? oi.GetInt32() : order,
                h.TryGetProperty("explosive", out var ex) ? ex.GetString() : null,
                h.TryGetProperty("chargeCollar", out var cc) ? cc.GetDouble() : 1.0));
            order++;
        }

        var data = OnUiThread(() => RequireCadReader().DrawUGDrillHoles(layer, specs));
        _bridgeClient?.SendAsync(new
        {
            id,
            action = "draw_cad_ugdrillholes_result",
            data
        });
    }

    private void HandleSliceCadPolyface(string id, System.Text.Json.JsonElement parameters)
    {
        ulong handle = parameters.GetProperty("handle").GetUInt64();
        double[] origin = parameters.GetProperty("origin").EnumerateArray().Select(v => v.GetDouble()).ToArray();
        double[] normal = parameters.GetProperty("normal").EnumerateArray().Select(v => v.GetDouble()).ToArray();
        if (origin.Length < 3 || normal.Length < 3)
            throw new ArgumentException("'origin' and 'normal' must be [x,y,z]");

        var data = OnUiThread(() => RequireCadReader().SlicePolyface(handle, origin, normal));
        _bridgeClient?.SendAsync(new
        {
            id,
            action = "slice_cad_polyface_result",
            data
        });
    }

    private void HandleSendHello(string id)
    {
        _bridgeClient?.SendAsync(new
        {
            id,
            action = "hello_response",
            data = new
            {
                message = "Hello from Deswik MCP plugin!",
                timestamp = DateTime.UtcNow,
                version = ProductVersion,
                loaded = _isLoaded,
                applicationAttached = Application != null
            }
        });
    }

    #endregion

    private static readonly object LogLock = new();

    private void Log(string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [MCP] {message}";
        Debug.WriteLine(line);
        try
        {
            lock (LogLock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogFile)!);
                File.AppendAllText(LogFile, line + Environment.NewLine);
            }
        }
        catch
        {
            // Logging must never take the host down.
        }
        _statusControl?.AppendLog(message);
    }
}
