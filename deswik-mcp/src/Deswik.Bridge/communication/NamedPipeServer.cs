using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Deswik.Bridge.Models;

namespace Deswik.Bridge.Communication;

/// <summary>
/// Named Pipe server for communication between MCP Server (TypeScript) and Deswik Bridge (C#).
/// </summary>
public class NamedPipeServer : IDisposable
{
    private const string PipeName = "deswik-mcp-pipe";
    private NamedPipeServerStream? _server;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;
    private bool _disposed;

    /// <summary>
    /// Event raised when a command is received.
    /// </summary>
    public event Func<McpCommand, Task<McpResponse>>? OnCommandReceived;

    /// <summary>
    /// Indicates whether the server is currently running.
    /// </summary>
    public bool IsRunning => _server != null && _server.IsConnected;

    /// <summary>
    /// Starts the Named Pipe server asynchronously.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(NamedPipeServer));

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                _server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                System.Diagnostics.Debug.WriteLine($"[{nameof(NamedPipeServer)}] Waiting for client connection...");
                await _server.WaitForConnectionAsync(_cts.Token);
                System.Diagnostics.Debug.WriteLine($"[{nameof(NamedPipeServer)}] Client connected.");

                await HandleClientAsync(_server, _cts.Token);
            }
            catch (OperationCanceledException) when (_cts.Token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{nameof(NamedPipeServer)}] Error: {ex.Message}");
                await Task.Delay(1000, _cts.Token); // Wait before retrying
            }
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream server, CancellationToken ct)
    {
        using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
        using var writer = new StreamWriter(server, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

        try
        {
            while (!ct.IsCancellationRequested && server.IsConnected)
            {
                var line = await reader.ReadLineAsync(ct);

                if (line == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[{nameof(NamedPipeServer)}] Client disconnected.");
                    break;
                }

                System.Diagnostics.Debug.WriteLine($"[{nameof(NamedPipeServer)}] Received: {line}");

                try
                {
                    var command = JsonSerializer.Deserialize<McpCommand>(line);

                    if (command == null)
                    {
                        var errorResponse = McpResponse.Fail("unknown", "Failed to parse command");
                        await writer.WriteLineAsync(JsonSerializer.Serialize(errorResponse));
                        continue;
                    }

                    McpResponse response;

                    if (OnCommandReceived != null)
                    {
                        response = await OnCommandReceived(command);
                    }
                    else
                    {
                        response = McpResponse.Fail(command.Id, "No command handler registered", "NO_HANDLER");
                    }

                    var responseJson = JsonSerializer.Serialize(response);
                    await writer.WriteLineAsync(responseJson);
                    System.Diagnostics.Debug.WriteLine($"[{nameof(NamedPipeServer)}] Sent: {responseJson}");
                }
                catch (JsonException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[{nameof(NamedPipeServer)}] JSON parse error: {ex.Message}");
                    var errorResponse = McpResponse.Fail("unknown", $"Invalid JSON: {ex.Message}", "PARSE_ERROR");
                    await writer.WriteLineAsync(JsonSerializer.Serialize(errorResponse));
                }
            }
        }
        catch (IOException ex) when (ex.HResult == -2146232800) // Broken pipe
        {
            System.Diagnostics.Debug.WriteLine($"[{nameof(NamedPipeServer)}] Pipe broken: {ex.Message}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[{nameof(NamedPipeServer)}] Client handler error: {ex.Message}");
        }
    }

    /// <summary>
    /// Stops the server.
    /// </summary>
    public void Stop()
    {
        _cts?.Cancel();
        _server?.Dispose();
        _server = null;
    }

    public void Dispose()
    {
        if (_disposed) return;

        Stop();
        _cts?.Dispose();
        _disposed = true;

        GC.SuppressFinalize(this);
    }
}
