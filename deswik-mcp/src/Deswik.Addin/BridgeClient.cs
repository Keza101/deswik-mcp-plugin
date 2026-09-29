using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Deswik.Addin;

/// <summary>
/// Bridge communication service for the Deswik plugin.
/// This runs inside Deswik and communicates with the external Bridge/MCP Server.
/// </summary>
internal class BridgeClient : IDisposable
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;
    private readonly string _bridgeHost;
    private readonly int _bridgePort;
    private bool _isConnected;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly object _dispatchLock = new();
    private Task _dispatchTail = Task.CompletedTask;

    public event Action<string>? OnLog;
    /// <summary>Raised for each command with the lifetime of its bridge connection.</summary>
    public event Action<string, string, JsonElement, CancellationToken>? OnCommand;
    /// <summary>Raised when an established connection is lost.</summary>
    public event Action? OnDisconnected;

    public bool IsConnected => _isConnected;

    public BridgeClient(string bridgeHost = "127.0.0.1", int bridgePort = 9595)
    {
        _bridgeHost = bridgeHost;
        _bridgePort = bridgePort;
    }

    /// <summary>
    /// Connects to the external Bridge server.
    /// </summary>
    public async Task<bool> ConnectAsync()
    {
        try
        {
            _cts?.Cancel();
            _stream?.Dispose();
            _client?.Dispose();

            _client = new TcpClient();
            await _client.ConnectAsync(_bridgeHost, _bridgePort);
            _stream = _client.GetStream();
            _isConnected = true;

            Log($"Connected to Bridge at {_bridgeHost}:{_bridgePort}");

            var connection = new CancellationTokenSource();
            _cts = connection;
            lock (_dispatchLock) _dispatchTail = Task.CompletedTask;
            var connectedStream = _stream;
            _ = Task.Run(() => ReceiveLoop(connection, connectedStream));

            // Send registration message
            await SendAsync(new
            {
                id = Guid.NewGuid().ToString(),
                action = "register_addin",
                @params = new
                {
                    name = "Deswik.MCP",
                    version = "1.0.0",
                    // CAD-only capabilities: scheduler actions belong to the
                    // Sched addin (capability routing: last registration wins,
                    // so claiming them here would hijack live schedule calls).
                    capabilities = new[]
                    {
                        "get_cad_document",
                        "get_cad_layers",
                        "get_cad_elements",
                        "get_cad_layer_attributes",
                        "create_cad_layer",
                        "draw_cad_text",
                        "get_cad_selection",
                        "get_ug_selection_context",
                        "draw_cad_polylines",
                        "get_cad_polyface_info",
                        "get_cad_polyface_geometry",
                        "get_cad_polyline_geometry",
                        "slice_cad_polyface",
                        "draw_cad_blastholes",
                        "get_cad_blasthole_details",
                        "get_cad_ugdrillhole_details",
                        "get_cad_polylines_under",
                        "draw_cad_ugdrillholes",
                        "preview_ugdrillholes",
                        "prepare_rollback_ugdrillholes",
                        "send_hello"
                    }
                }
            });

            return true;
        }
        catch (Exception ex)
        {
            Log($"Failed to connect to Bridge: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Disconnects from the Bridge.
    /// </summary>
    public void Disconnect()
    {
        _isConnected = false;
        _cts?.Cancel();
        _stream?.Close();
        _client?.Close();
        Log("Disconnected from Bridge");
    }

    /// <summary>
    /// Sends a message to the Bridge.
    /// </summary>
    public async Task SendAsync(object message)
    {
        if (!_isConnected || _stream == null) return;

        try
        {
            var json = JsonSerializer.Serialize(message);
            var data = Encoding.UTF8.GetBytes(json + "\n");
            await _writeLock.WaitAsync();
            try
            {
                await _stream.WriteAsync(data);
                await _stream.FlushAsync();
            }
            finally
            {
                _writeLock.Release();
            }
        }
        catch (Exception ex)
        {
            Log($"Send error: {ex.Message}");
        }
    }

    private async Task ReceiveLoop(CancellationTokenSource connection, NetworkStream stream)
    {
        var ct = connection.Token;
        var reader = new StreamReader(stream, Encoding.UTF8);
        var wasConnected = false;

        while (!ct.IsCancellationRequested && _isConnected)
        {
            wasConnected = true;
            try
            {
                var line = await reader.ReadLineAsync(ct);
                if (line == null) break;

                Log($"Received bridge frame ({Encoding.UTF8.GetByteCount(line)} bytes)");

                var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                // Ignore bridge acknowledgements of our own messages
                // (e.g. the register_addin response has "success" but no "action").
                if (!root.TryGetProperty("action", out var actionProp)) continue;

                var action = actionProp.GetString() ?? "";
                var id = root.TryGetProperty("id", out var i) ? i.GetString() ?? "" : "";
                var parameters = root.TryGetProperty("params", out var p) ? p.Clone() : default;

                // Keep reading the socket while a CAD action waits for the
                // UI thread. This lets a lost bridge cancel queued writes.
                if (action == "_job_cancel")
                {
                    _ = Task.Run(() =>
                    {
                        if (!ct.IsCancellationRequested)
                            OnCommand?.Invoke(id, action, parameters, ct);
                    });
                    continue;
                }
                lock (_dispatchLock)
                {
                    var previous = _dispatchTail;
                    _dispatchTail = Task.Run(async () =>
                    {
                        try { await previous; } catch { }
                        if (!ct.IsCancellationRequested) OnCommand?.Invoke(id, action, parameters, ct);
                    });
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log($"Receive error: {ex.Message}");
                break;
            }
        }

        var current = ReferenceEquals(_cts, connection);
        var lost = current && _isConnected && wasConnected && !ct.IsCancellationRequested;
        connection.Cancel();
        if (current) _isConnected = false;
        Log("Receive loop ended");
        if (lost) OnDisconnected?.Invoke();
    }

    private void Log(string message)
    {
        System.Diagnostics.Debug.WriteLine($"[BridgeClient] {message}");
        OnLog?.Invoke(message);
    }

    public void Dispose()
    {
        Disconnect();
        _cts?.Dispose();
    }
}
