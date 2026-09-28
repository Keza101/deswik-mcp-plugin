using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Deswik.Bridge.Models;

namespace Deswik.Addin;

internal static class LoopbackBridgeRequester
{
    public static async Task<MapBridgeResult> InvokeAsync(
        string commandId,
        CancellationToken cancellationToken)
    {
        if (!ProcessMapActionPolicy.TryGetAction(commandId, out var action))
            return new(false, "map_action_refused", "The Process Map command is not allowlisted");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", 9595, timeout.Token);
            await using var stream = client.GetStream();
            var request = JsonSerializer.Serialize(new
            {
                id = Guid.NewGuid().ToString("D"),
                action,
                mode = "live",
                @params = new { }
            });
            var bytes = Encoding.UTF8.GetBytes(request + "\n");
            await stream.WriteAsync(bytes, timeout.Token);
            await stream.FlushAsync(timeout.Token);

            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            var line = await reader.ReadLineAsync(timeout.Token);
            if (string.IsNullOrWhiteSpace(line))
                return new(false, "bridge_empty_response", "The bridge returned no response");

            using var response = JsonDocument.Parse(line);
            var root = response.RootElement;
            var success = root.TryGetProperty("success", out var successElement) &&
                          successElement.ValueKind == JsonValueKind.True;
            if (!success)
            {
                var code = root.TryGetProperty("errorCode", out var codeElement)
                    ? codeElement.GetString() ?? "bridge_refused"
                    : "bridge_refused";
                var error = root.TryGetProperty("error", out var errorElement)
                    ? errorElement.GetString() ?? "The bridge refused the action"
                    : "The bridge refused the action";
                return new(false, code, error);
            }

            var data = root.TryGetProperty("data", out var dataElement)
                ? dataElement.GetRawText()
                : "{}";
            return new(true, null, data);
        }
        catch (OperationCanceledException)
        {
            return new(false, "bridge_timeout", "The bridge did not respond within 15 seconds");
        }
        catch (Exception ex)
        {
            return new(false, "bridge_unavailable", ex.Message);
        }
    }
}

internal sealed record MapBridgeResult(bool Success, string? ErrorCode, string Message);
