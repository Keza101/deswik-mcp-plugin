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

    public event Action<string>? OnLog;
    /// <summary>Raised for each command from the bridge: (requestId, action, params).</summary>
    public event Action<string, string, JsonElement>? OnCommand;
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

            _cts = new CancellationTokenSource();
            _ = Task.Run(() => ReceiveLoop(_cts.Token));

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
                        "draw_cad_polylines",
                        "get_cad_polyface_info",
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

    private async Task ReceiveLoop(CancellationToken ct)
    {
        var reader = new StreamReader(_stream!, Encoding.UTF8);
        var wasConnected = false;

        while (!ct.IsCancellationRequested && _isConnected)
        {
            wasConnected = true;
            try
            {
                var line = await reader.ReadLineAsync(ct);
                if (line == null) break;

                Log($"Received: {line}");

                var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                // Ignore bridge acknowledgements of our own messages
                // (e.g. the register_addin response has "success" but no "action").
                if (!root.TryGetProperty("action", out var actionProp)) continue;

                var action = actionProp.GetString() ?? "";
                var id = root.TryGetProperty("id", out var i) ? i.GetString() ?? "" : "";
                var parameters = root.TryGetProperty("params", out var p) ? p : default;

                OnCommand?.Invoke(id, action, parameters);
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

        var lost = _isConnected && wasConnected && !ct.IsCancellationRequested;
        _isConnected = false;
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
