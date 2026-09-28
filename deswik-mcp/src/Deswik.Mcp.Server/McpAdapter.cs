using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Deswik.Mcp.Server;

public interface IBridgeClient
{
    Task<JsonElement> InvokeAsync(string action, JsonElement arguments, CancellationToken cancellationToken);
}

public sealed class LoopbackBridgeClient : IBridgeClient
{
    private readonly int _port;

    public LoopbackBridgeClient(int? port = null)
    {
        var configured = Environment.GetEnvironmentVariable("DESWIK_BRIDGE_PORT");
        _port = port ?? (int.TryParse(configured, out var value) && value is >= 1 and <= 65535 ? value : 9595);
    }

    public async Task<JsonElement> InvokeAsync(string action, JsonElement arguments, CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", _port, cancellationToken);
        await using var stream = client.GetStream();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(stream, new UTF8Encoding(false), false, leaveOpen: true);
        var request = JsonSerializer.Serialize(new
        {
            id = Guid.NewGuid().ToString("D"), action, @params = arguments, mode = "live",
        });
        await writer.WriteLineAsync(request.AsMemory(), cancellationToken);
        var line = await reader.ReadLineAsync(cancellationToken);
        if (line == null) throw new IOException("The Deswik bridge closed without a response");
        using var document = JsonDocument.Parse(line);
        return document.RootElement.Clone();
    }
}

public sealed class McpAdapter
{
    public const string ProtocolVersion = "2025-11-25";
    private const int MaxMessageCharacters = 4 * 1024 * 1024;
    private static readonly JsonElement EmptyArguments = JsonDocument.Parse("{}").RootElement.Clone();
    private readonly IBridgeClient _bridge;

    public McpAdapter(IBridgeClient bridge) => _bridge = bridge;

    public async Task<string?> ProcessLineAsync(string line, CancellationToken cancellationToken = default)
    {
        if (line.Length > MaxMessageCharacters)
            return Serialize(Error(null, -32600, "Request exceeds the 4 MiB stdio limit"));
        JsonDocument document;
        try { document = JsonDocument.Parse(line); }
        catch (JsonException ex) { return Serialize(Error(null, -32700, $"Parse error: {ex.Message}")); }
        using (document)
        {
            var request = document.RootElement;
            if (request.ValueKind != JsonValueKind.Object)
                return Serialize(Error(null, -32600, "Invalid JSON-RPC request"));
            var hasId = request.TryGetProperty("id", out var id);
            if (!request.TryGetProperty("jsonrpc", out var versionElement) ||
                versionElement.ValueKind != JsonValueKind.String || versionElement.GetString() != "2.0")
                return hasId ? Serialize(Error(id, -32600, "jsonrpc must be '2.0'"))
                    : Serialize(Error(null, -32600, "jsonrpc must be '2.0'"));
            if (!request.TryGetProperty("method", out var methodElement) || methodElement.ValueKind != JsonValueKind.String)
                return hasId ? Serialize(Error(id, -32600, "Invalid JSON-RPC request")) : null;
            var method = methodElement.GetString();
            if (!hasId) return null;
            return method switch
            {
                "initialize" => Serialize(Success(id, InitializeResult(request))),
                "ping" => Serialize(Success(id, new { })),
                "tools/list" => Serialize(Success(id, new { tools = McpToolCatalog.Tools })),
                "tools/call" => Serialize(await CallToolAsync(id, request, cancellationToken)),
                _ => Serialize(Error(id, -32601, $"Method not found: {method}")),
            };
        }
    }

    private async Task<object> CallToolAsync(JsonElement id, JsonElement request, CancellationToken cancellationToken)
    {
        if (!request.TryGetProperty("params", out var parameters) || parameters.ValueKind != JsonValueKind.Object ||
            !parameters.TryGetProperty("name", out var nameElement) || nameElement.ValueKind != JsonValueKind.String)
            return Error(id, -32602, "tools/call requires a tool name");
        var name = nameElement.GetString()!;
        if (!McpToolCatalog.TryGet(name, out var tool))
            return Error(id, -32602, $"Tool is not approved: {name}");
        var arguments = parameters.TryGetProperty("arguments", out var supplied) ? supplied : EmptyArguments;
        var refusal = McpToolCatalog.ValidateCall(tool, arguments);
        if (refusal != null)
        {
            var code = refusal.Contains("token", StringComparison.OrdinalIgnoreCase)
                ? "human_token_required" : "invalid_arguments";
            return Success(id, ToolError(code, refusal));
        }
        try
        {
            var bridgeResponse = await _bridge.InvokeAsync(name, arguments, cancellationToken);
            var success = bridgeResponse.TryGetProperty("success", out var successElement) && successElement.ValueKind == JsonValueKind.True;
            var text = JsonSerializer.Serialize(bridgeResponse, new JsonSerializerOptions { WriteIndented = true });
            return Success(id, new
            {
                content = new[] { new { type = "text", text } },
                structuredContent = bridgeResponse,
                isError = !success,
            });
        }
        catch (Exception ex) when (ex is SocketException or IOException or OperationCanceledException)
        {
            return Success(id, ToolError("bridge_unavailable", ex.Message));
        }
    }

    private static object InitializeResult(JsonElement request)
    {
        var requested = request.TryGetProperty("params", out var parameters) &&
                        parameters.TryGetProperty("protocolVersion", out var version) && version.ValueKind == JsonValueKind.String
            ? version.GetString() : null;
        var selected = requested is "2025-03-26" or "2025-06-18" or ProtocolVersion ? requested : ProtocolVersion;
        return new
        {
            protocolVersion = selected,
            capabilities = new { tools = new { listChanged = false } },
            serverInfo = new { name = "deswik-workflow-bridge", version = "1.0.0" },
            instructions = "Live Deswik tools fail closed. Production CAD writes require a token minted after in-CAD approval.",
        };
    }

    private static object ToolError(string code, string message)
    {
        var envelope = new { mode = "live", success = false, error = message, errorCode = code };
        return new
        {
            content = new[] { new { type = "text", text = JsonSerializer.Serialize(envelope) } },
            structuredContent = envelope, isError = true,
        };
    }

    private static object Success(JsonElement id, object result) => new { jsonrpc = "2.0", id = id.Clone(), result };
    private static object Error(JsonElement? id, int code, string message) => new
    {
        jsonrpc = "2.0", id = id?.Clone(), error = new { code, message },
    };
    private static string Serialize(object value) => JsonSerializer.Serialize(value);
}
