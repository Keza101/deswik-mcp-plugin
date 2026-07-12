using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Deswik.Bridge.Models;
using Deswik.Bridge.Services;

namespace Deswik.Bridge.Standalone;

/// <summary>
/// Simple TCP-based bridge for testing without Named Pipe security issues.
/// Uses TCP port 9595 instead of Named Pipes.
///
/// When the Deswik plugin connects and sends register_addin, commands whose
/// action is in the addin's capability list are forwarded to the live Deswik
/// instance; everything else (or everything, when no addin is connected) is
/// answered with demo data.
/// </summary>
class TcpBridge
{
    private const int Port = 9595;
    private static readonly TimeSpan ForwardTimeout = TimeSpan.FromSeconds(15);
    private static readonly string LogFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Deswik", "Logs", "Deswik.MCP.TcpBridge.log");

    private static DemoSchedulerService? _schedulerService;
    private static DemoCommandHandler? _commandHandler;
    private static bool _isRunning = true;

    // Connected Deswik addins (CAD, Sched, ...) routed by capability:
    // capability/action -> the addin connection that registered it.
    private static readonly Dictionary<string, ClientConnection> CapabilityMap =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly object AddinLock = new();

    // Requests forwarded to an addin, keyed by command id:
    // id -> (requester, addin the request went to).
    private static readonly ConcurrentDictionary<string, (ClientConnection Requester, ClientConnection Addin)> Pending = new();

    /// <summary>A connected TCP client with serialized writes.</summary>
    private class ClientConnection
    {
        public required StreamWriter Writer { get; init; }
        public SemaphoreSlim WriteLock { get; } = new(1, 1);
        public bool IsAddin { get; set; }
        public string AddinName { get; set; } = "";

        public async Task SendLineAsync(string line)
        {
            await WriteLock.WaitAsync();
            try
            {
                await Writer.WriteLineAsync(line);
            }
            finally
            {
                WriteLock.Release();
            }
        }
    }

    static async Task Main(string[] args)
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║         Deswik MCP Bridge - TCP Mode (Port 9595)          ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        try
        {
            InitializeLogging();
            Log("Starting Deswik MCP Bridge (TCP Mode)...");

            // Initialize demo services
            _schedulerService = new DemoSchedulerService();
            _commandHandler = new DemoCommandHandler(_schedulerService);
            Log("Demo services initialized.");

            // Start TCP server
            await StartTcpServerAsync();

            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine($"  MCP Bridge is running on TCP port {Port}");
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine();
            Console.WriteLine("Press any key to stop...");
            Console.WriteLine();

            // Wait for key press
            Console.ReadKey();
            _isRunning = false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
            Log($"Fatal error: {ex}");
            Environment.Exit(1);
        }
    }

    private static void InitializeLogging()
    {
        var logDir = Path.GetDirectoryName(LogFile);
        if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
        {
            Directory.CreateDirectory(logDir);
        }
    }

    private static void Log(string message)
    {
        var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
        Console.WriteLine(entry);
        try
        {
            File.AppendAllText(LogFile, entry + Environment.NewLine);
        }
        catch { }
    }

    private static async Task StartTcpServerAsync()
    {
        var listener = new TcpListener(IPAddress.Loopback, Port);
        listener.Start();
        Log($"TCP Server started on port {Port}");

        while (_isRunning)
        {
            try
            {
                var client = await listener.AcceptTcpClientAsync();
                Log("Client connected");
                _ = HandleClientAsync(client);
            }
            catch (Exception ex) when (_isRunning)
            {
                Log($"Accept error: {ex.Message}");
            }
        }

        listener.Stop();
    }

    private static async Task HandleClientAsync(TcpClient client)
    {
        ClientConnection? conn = null;

        using (client)
        using (var stream = client.GetStream())
        using (var reader = new StreamReader(stream, Encoding.UTF8))
        // UTF8Encoding(false): no BOM, which would corrupt the first JSON line.
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true })
        {
            conn = new ClientConnection { Writer = writer };

            try
            {
                while (_isRunning && client.Connected)
                {
                    var line = await reader.ReadLineAsync();
                    if (line == null) break;

                    Log($"Received: {line}");

                    try
                    {
                        if (conn.IsAddin)
                        {
                            await HandleAddinMessageAsync(line);
                        }
                        else
                        {
                            await HandleClientCommandAsync(conn, line);
                        }
                    }
                    catch (JsonException ex)
                    {
                        Log($"JSON parse error: {ex.Message}");
                        var errorResponse = McpResponse.Fail("unknown", $"Invalid JSON: {ex.Message}", "PARSE_ERROR");
                        await conn.SendLineAsync(JsonSerializer.Serialize(errorResponse));
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Client error: {ex.Message}");
            }
        }

        if (conn != null) OnClientGone(conn);
        Log("Client disconnected");
    }

    private static async Task HandleClientCommandAsync(ClientConnection conn, string line)
    {
        var command = JsonSerializer.Deserialize<McpCommand>(line);
        if (command == null) return;

        // A Deswik plugin announces itself; remember which actions route to it.
        if (command.Action == "register_addin")
        {
            var capabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string name = "addin";
            if (command.Params != null)
            {
                if (command.Params.TryGetValue("capabilities", out var caps) &&
                    caps is JsonElement capsEl && capsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var c in capsEl.EnumerateArray())
                    {
                        var s = c.GetString();
                        if (!string.IsNullOrEmpty(s)) capabilities.Add(s);
                    }
                }
                if (command.Params.TryGetValue("name", out var nm) &&
                    nm is JsonElement nmEl && nmEl.ValueKind == JsonValueKind.String)
                {
                    name = nmEl.GetString() ?? name;
                }
            }

            lock (AddinLock)
            {
                conn.IsAddin = true;
                conn.AddinName = name;
                foreach (var cap in capabilities)
                    CapabilityMap[cap] = conn;   // last registration wins
            }

            Log($"Addin '{name}' registered ({capabilities.Count} capabilities)");
            await conn.SendLineAsync(JsonSerializer.Serialize(
                McpResponse.Ok(command.Id, new { registered = true, name })));
            return;
        }

        // Forward to whichever live addin registered this action.
        ClientConnection? addin;
        lock (AddinLock)
        {
            CapabilityMap.TryGetValue(command.Action, out addin);
        }

        if (addin != null)
        {
            Pending[command.Id] = (conn, addin);
            await addin.SendLineAsync(line);
            Log($"Forwarded to addin '{addin.AddinName}': {command.Action} (id={command.Id})");

            // Fail the request if Deswik never answers.
            _ = Task.Run(async () =>
            {
                await Task.Delay(ForwardTimeout);
                if (Pending.TryRemove(command.Id, out var entry))
                {
                    var timeout = McpResponse.Fail(command.Id,
                        $"Deswik did not respond to '{command.Action}' within {ForwardTimeout.TotalSeconds}s",
                        "ADDIN_TIMEOUT");
                    try { await entry.Requester.SendLineAsync(JsonSerializer.Serialize(timeout)); }
                    catch { }
                }
            });
            return;
        }

        // No addin (or unsupported action): answer with demo data.
        var response = await _commandHandler!.HandleCommandAsync(command);
        var responseJson = JsonSerializer.Serialize(response);
        await conn.SendLineAsync(responseJson);
        Log($"Sent (demo): {responseJson}");
    }

    private static async Task HandleAddinMessageAsync(string line)
    {
        // Addin replies look like { id, action: "*_result", data, error? }.
        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;
        var id = root.TryGetProperty("id", out var i) ? i.GetString() : null;

        if (string.IsNullOrEmpty(id) || !Pending.TryRemove(id, out var entry))
        {
            Log($"Addin message with no pending request (id={id ?? "none"}) - ignored");
            return;
        }

        var error = root.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String
            ? e.GetString()
            : null;
        object? data = root.TryGetProperty("data", out var d) ? d.Clone() : null;

        var response = error == null
            ? McpResponse.Ok(id, data)
            : McpResponse.Fail(id, error, "ADDIN_ERROR");

        var json = JsonSerializer.Serialize(response);
        await entry.Requester.SendLineAsync(json);
        Log($"Relayed addin response for id={id}");
    }

    private static void OnClientGone(ClientConnection conn)
    {
        lock (AddinLock)
        {
            if (conn.IsAddin)
            {
                var caps = CapabilityMap.Where(kv => kv.Value == conn)
                                        .Select(kv => kv.Key).ToList();
                foreach (var cap in caps) CapabilityMap.Remove(cap);
                Log($"Addin '{conn.AddinName}' disconnected - {caps.Count} capabilities unrouted");
            }
        }

        foreach (var (id, entry) in Pending)
        {
            if (entry.Addin == conn)
            {
                // Requests waiting on this addin fail fast.
                if (Pending.TryRemove(id, out _))
                {
                    var fail = McpResponse.Fail(id, $"Deswik addin '{conn.AddinName}' disconnected",
                                                "ADDIN_DISCONNECTED");
                    _ = entry.Requester.SendLineAsync(JsonSerializer.Serialize(fail));
                }
            }
            else if (entry.Requester == conn)
            {
                // The requester itself is gone; drop its pending entries.
                Pending.TryRemove(id, out _);
            }
        }
    }
}

/// <summary>
/// Demo scheduler service with mock data for testing.
/// </summary>
public class DemoSchedulerService
{
    public ScheduleInfo GetScheduleInfo() => new()
    {
        FileName = "Demo_Schedule.dspx",
        DirectoryName = "C:\\Deswik\\Projects\\Demo",
        TargetStart = DateTime.Today,
        TotalTasks = 150,
        TotalResources = 25,
        TotalDependencies = 200,
        Version = "2025.2 (Demo)"
    };

    public IEnumerable<TaskInfo> GetTasks(int limit = 100, int offset = 0, string? filter = null)
    {
        var tasks = new List<TaskInfo>
        {
            new() { Id = "T001", Name = "Pre-Stripping", Start = DateTime.Today, Finish = DateTime.Today.AddDays(30), Duration = TimeSpan.FromDays(30) },
            new() { Id = "T002", Name = "Mine Development", Start = DateTime.Today.AddDays(15), Finish = DateTime.Today.AddDays(60), Duration = TimeSpan.FromDays(45) },
            new() { Id = "T003", Name = "Ore Extraction Zone A", Start = DateTime.Today.AddDays(45), Finish = DateTime.Today.AddDays(120), Duration = TimeSpan.FromDays(75) },
            new() { Id = "T004", Name = "Ore Extraction Zone B", Start = DateTime.Today.AddDays(60), Finish = DateTime.Today.AddDays(150), Duration = TimeSpan.FromDays(90) },
            new() { Id = "T005", Name = "Waste Dumping North", Start = DateTime.Today.AddDays(30), Finish = DateTime.Today.AddDays(180), Duration = TimeSpan.FromDays(150) },
            new() { Id = "T006", Name = "Crushing Station Setup", Start = DateTime.Today.AddDays(90), Finish = DateTime.Today.AddDays(105), Duration = TimeSpan.FromDays(15) },
            new() { Id = "T007", Name = "Conveyor Installation", Start = DateTime.Today.AddDays(100), Finish = DateTime.Today.AddDays(140), Duration = TimeSpan.FromDays(40) },
            new() { Id = "T008", Name = "Processing Plant Commissioning", Start = DateTime.Today.AddDays(150), Finish = DateTime.Today.AddDays(170), Duration = TimeSpan.FromDays(20) },
        };

        var filtered = string.IsNullOrEmpty(filter)
            ? tasks
            : tasks.Where(t => t.Name.Contains(filter, StringComparison.OrdinalIgnoreCase));

        return filtered.Skip(offset).Take(limit);
    }

    public IEnumerable<ResourceInfo> GetResources()
    {
        return new List<ResourceInfo>
        {
            new() { Id = "R001", Name = "Haul Truck Fleet", IsPool = true, MaxUnits = 10, Rate = "250/hr" },
            new() { Id = "R002", Name = "Excavator 1", IsPool = false, MaxUnits = 1, Rate = "500/hr" },
            new() { Id = "R003", Name = "Excavator 2", IsPool = false, MaxUnits = 1, Rate = "500/hr" },
            new() { Id = "R004", Name = "Dozer Fleet", IsPool = true, MaxUnits = 4, Rate = "180/hr" },
            new() { Id = "R005", Name = "Drill Rig", IsPool = false, MaxUnits = 2, Rate = "350/hr" },
        };
    }

    /// <summary>
    /// Returns all available field names in the schedule (demo data).
    /// In production, this reflects the live schedule's ProductionFields and CustomFields.
    /// </summary>
    public Dictionary<string, List<string>> GetScheduleFields()
    {
        return new Dictionary<string, List<string>>
        {
            ["ProductionFields"] = new List<string>
            {
                "Total Material",
                "Total Ore Tonnes",
                "Total Waste Tonnes",
                "Aug 2025 total ore tonnes",
                "Sep 2025 total ore tonnes",
                "Oct 2025 total ore tonnes",
                "Total ore %",
                "Stripping Ratio",
                "Limestone (t)",
                "Clay (t)",
                "Tonnes Per Day"
            },
            ["CustomFields"] = new List<string>
            {
                "Block ID",
                "Phase",
                "Zone",
                "Bench",
                "Material Type",
                "Pit Design",
                "Priority",
                "Status"
            },
            ["StandardFields"] = new List<string>
            {
                "Name",
                "Start",
                "Finish",
                "Duration",
                "Calendar",
                "WBS",
                "UniqueID"
            }
        };
    }

    /// <summary>
    /// Returns field values for a specific task (demo data).
    /// In production, this reflects the live schedule's actual field values.
    /// </summary>
    public Dictionary<string, object?> GetTaskFields(string taskId, List<string>? fields = null)
    {
        // Default demo data - returned when taskId not found or in demo mode
        var defaultData = new Dictionary<string, object?>
        {
            ["Name"] = $"Task {taskId}",
            ["Start"] = DateTime.Today,
            ["Finish"] = DateTime.Today.AddDays(30),
            ["Duration"] = 30.0,
            ["Total Material"] = 100000.0,
            ["Total Ore Tonnes"] = 30000.0,
            ["Total Waste Tonnes"] = 70000.0,
            ["Total ore %"] = 30.0,
            ["Stripping Ratio"] = 2.33,
            ["Block ID"] = taskId,
            ["Phase"] = "Phase 1",
            ["Zone"] = "North",
            ["Material Type"] = "Ore",
            ["Status"] = "Planned"
        };

        var taskData = new Dictionary<string, Dictionary<string, object?>>
        {
            ["T001"] = new Dictionary<string, object?>
            {
                ["Name"] = "Pre-Stripping",
                ["Start"] = DateTime.Today,
                ["Finish"] = DateTime.Today.AddDays(30),
                ["Duration"] = 30.0,
                ["Total Material"] = 1250000.0,
                ["Total Ore Tonnes"] = 350000.0,
                ["Total Waste Tonnes"] = 900000.0,
                ["Aug 2025 total ore tonnes"] = 175000.0,
                ["Total ore %"] = 28.0,
                ["Stripping Ratio"] = 2.57,
                ["Block ID"] = "PS-001",
                ["Phase"] = "Phase 1",
                ["Zone"] = "North",
                ["Bench"] = "1200-1180",
                ["Material Type"] = "Mixed",
                ["Status"] = "Active"
            },
            ["T002"] = new Dictionary<string, object?>
            {
                ["Name"] = "Mine Development",
                ["Start"] = DateTime.Today.AddDays(15),
                ["Finish"] = DateTime.Today.AddDays(60),
                ["Duration"] = 45.0,
                ["Total Material"] = 2100000.0,
                ["Total Ore Tonnes"] = 580000.0,
                ["Total Waste Tonnes"] = 1520000.0,
                ["Aug 2025 total ore tonnes"] = 290000.0,
                ["Sep 2025 total ore tonnes"] = 290000.0,
                ["Total ore %"] = 27.6,
                ["Stripping Ratio"] = 2.62,
                ["Block ID"] = "MD-001",
                ["Phase"] = "Phase 1",
                ["Zone"] = "Central",
                ["Bench"] = "1180-1160",
                ["Material Type"] = "Ore",
                ["Priority"] = "High",
                ["Status"] = "Planned"
            }
        };

        // Use known data if available, otherwise use defaults
        Dictionary<string, object?> selectedData = defaultData;

        if (taskData.TryGetValue(taskId, out var knownData) && knownData != null)
        {
            selectedData = knownData;
        }

        // If no specific fields requested, return all
        if (fields == null || fields.Count == 0)
        {
            return selectedData;
        }

        // Filter to requested fields
        var result = new Dictionary<string, object?>();
        foreach (var field in fields)
        {
            if (selectedData.TryGetValue(field, out var value))
            {
                result[field] = value;
            }
        }
        return result;
    }
}

/// <summary>
/// Demo command handler with mock data responses.
/// </summary>
public class DemoCommandHandler
{
    private readonly DemoSchedulerService _schedulerService;

    public DemoCommandHandler(DemoSchedulerService schedulerService)
    {
        _schedulerService = schedulerService;
    }

    public Task<McpResponse> HandleCommandAsync(McpCommand command)
    {
        return Task.FromResult(command.Action.ToLowerInvariant() switch
        {
            // Basic commands
            "ping" => McpResponse.Ok(command.Id, new { message = "pong", timestamp = DateTime.UtcNow, mode = "demo" }),
            "get_schedule_info" => McpResponse.Ok(command.Id, _schedulerService.GetScheduleInfo()),
            "get_tasks" => McpResponse.Ok(command.Id, _schedulerService.GetTasks()),
            "get_resources" => McpResponse.Ok(command.Id, _schedulerService.GetResources()),
            "get_available_actions" => McpResponse.Ok(command.Id, GetAvailableActions()),

            // Field query commands (demo mode)
            "get_schedule_fields" => McpResponse.Ok(command.Id, _schedulerService.GetScheduleFields()),
            "get_task_fields" => HandleGetTaskFields(command),

            // Drawing commands - simulate visual output
            "draw_circle" => HandleDrawCircle(command),
            "draw_rectangle" => HandleDrawRectangle(command),
            "draw_line" => HandleDrawLine(command),
            "clear_drawing" => McpResponse.Ok(command.Id, new { message = "Canvas cleared", shapes = 0 }),

            _ => McpResponse.Fail(command.Id, $"Unknown action: {command.Action}", "UNKNOWN_ACTION")
        });
    }

    private static McpResponse HandleGetTaskFields(McpCommand command)
    {
        var p = command.Params;
        var taskId = GetString(p, "taskId", "");
        if (string.IsNullOrEmpty(taskId))
        {
            return McpResponse.Fail(command.Id, "taskId is required", "MISSING_PARAM");
        }

        List<string>? fields = null;
        if (p != null && p.TryGetValue("fields", out var fieldsValue) && fieldsValue is JsonElement fieldsEl)
        {
            if (fieldsEl.ValueKind == JsonValueKind.Array)
            {
                fields = new List<string>();
                foreach (var f in fieldsEl.EnumerateArray())
                {
                    var s = f.GetString();
                    if (!string.IsNullOrEmpty(s)) fields.Add(s);
                }
            }
        }

        // Use the instance field _schedulerService (set in constructor)
        var demoService = new DemoSchedulerService();
        var result = demoService.GetTaskFields(taskId, fields);
        return McpResponse.Ok(command.Id, result);
    }

    private static McpResponse HandleDrawCircle(McpCommand command)
    {
        var p = command.Params;
        var x = GetInt(p, "x", 100);
        var y = GetInt(p, "y", 100);
        var radius = GetInt(p, "radius", 50);
        var color = GetString(p, "color", "blue");
        var label = GetString(p, "label", "");

        var shape = new
        {
            type = "circle",
            x, y, radius, color,
            label,
            svg = $"<circle cx=\"{x}\" cy=\"{y}\" r=\"{radius}\" stroke=\"{color}\" stroke-width=\"2\" fill=\"none\"/>",
            ascii = $"  ,---.\r\n /     \\\r\n|   +   | ({x},{y}) r={radius}\r\n \\     /\r\n  `---'"
        };

        return McpResponse.Ok(command.Id, new { shape, message = $"Circle drawn at ({x},{y}) with radius {radius}" });
    }

    private static McpResponse HandleDrawRectangle(McpCommand command)
    {
        var p = command.Params;
        var x = GetInt(p, "x", 50);
        var y = GetInt(p, "y", 50);
        var width = GetInt(p, "width", 100);
        var height = GetInt(p, "height", 60);
        var color = GetString(p, "color", "red");

        var shape = new
        {
            type = "rectangle",
            x, y, width, height, color,
            svg = $"<rect x=\"{x}\" y=\"{y}\" width=\"{width}\" height=\"{height}\" stroke=\"{color}\" stroke-width=\"2\" fill=\"none\"/>",
            ascii = $"┌{new string('─', Math.Max(0, width - 2))}┐\r\n" +
                    $"|{new string(' ', Math.Max(0, width - 2))}| ({x},{y}) {width}x{height}\r\n" +
                    $"└{new string('─', Math.Max(0, width - 2))}┘"
        };

        return McpResponse.Ok(command.Id, new { shape, message = $"Rectangle drawn at ({x},{y}) with size {width}x{height}" });
    }

    private static McpResponse HandleDrawLine(McpCommand command)
    {
        var p = command.Params;
        var x1 = GetInt(p, "x1", 0);
        var y1 = GetInt(p, "y1", 0);
        var x2 = GetInt(p, "x2", 100);
        var y2 = GetInt(p, "y2", 100);
        var color = GetString(p, "color", "black");
        var thickness = GetInt(p, "thickness", 2);

        var dx = x2 - x1;
        var dy = y2 - y1;
        var length = Math.Sqrt(dx * dx + dy * dy);

        var shape = new
        {
            type = "line",
            x1, y1, x2, y2, color, thickness,
            length = Math.Round(length, 2),
            svg = $"<line x1=\"{x1}\" y1=\"{y1}\" x2=\"{x2}\" y2=\"{y2}\" stroke=\"{color}\" stroke-width=\"{thickness}\"/>",
            ascii = $"({x1},{y1}) ────→ ({x2},{y2}) length={Math.Round(length, 2)}"
        };

        return McpResponse.Ok(command.Id, new { shape, message = $"Line drawn from ({x1},{y1}) to ({x2},{y2})" });
    }

    private static int GetInt(Dictionary<string, object>? dict, string key, int defaultValue)
    {
        if (dict != null && dict.TryGetValue(key, out var value))
        {
            return value switch
            {
                JsonElement je => je.GetInt32(),
                int i => i,
                double d => (int)d,
                string s when int.TryParse(s, out var i) => i,
                _ => defaultValue
            };
        }
        return defaultValue;
    }

    private static string GetString(Dictionary<string, object>? dict, string key, string defaultValue)
    {
        if (dict != null && dict.TryGetValue(key, out var value))
        {
            return value switch
            {
                JsonElement je => je.GetString() ?? defaultValue,
                string s => s,
                _ => defaultValue
            };
        }
        return defaultValue;
    }

    private static List<string> GetAvailableActions()
    {
        return new List<string>
        {
            // Basic commands
            "ping",
            "get_schedule_info",
            "get_tasks",
            "get_task_fields",
            "get_resources",
            "get_schedule_fields",
            "get_available_actions",
            // Drawing commands
            "draw_circle",
            "draw_rectangle",
            "draw_line",
            "clear_drawing"
        };
    }
}
