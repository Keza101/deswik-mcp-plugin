using System.Text.Json.Serialization;

namespace Deswik.Bridge.Models;

/// <summary>
/// Represents a command sent from MCP Server to Deswik Bridge via Named Pipe.
/// </summary>
public class McpCommand
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("params")]
    public Dictionary<string, object>? Params { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Standard response format for MCP commands.
/// </summary>
public class McpResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public object? Data { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public static McpResponse Ok(string id, object? data = null) => new()
    {
        Id = id,
        Success = true,
        Data = data
    };

    public static McpResponse Fail(string id, string error, string? errorCode = null) => new()
    {
        Id = id,
        Success = false,
        Error = error,
        ErrorCode = errorCode
    };
}
