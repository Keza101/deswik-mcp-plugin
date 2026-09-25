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

    /// <summary>
    /// Requested response source. Omitted means live. Demo data is returned
    /// only when the caller explicitly sends "demo".
    /// </summary>
    [JsonPropertyName("mode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Mode { get; set; }

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

    /// <summary>Who answered: live, demo, unsupported, or disconnected.</summary>
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = McpResponseMode.Live;

    [JsonPropertyName("data")]
    public object? Data { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public static McpResponse Ok(
        string id,
        object? data = null,
        string mode = McpResponseMode.Live) => new()
    {
        Id = id,
        Success = true,
        Mode = mode,
        Data = data
    };

    public static McpResponse Fail(
        string id,
        string error,
        string? errorCode = null,
        string mode = McpResponseMode.Live) => new()
    {
        Id = id,
        Success = false,
        Mode = mode,
        Error = error,
        ErrorCode = errorCode
    };
}

public static class McpResponseMode
{
    public const string Live = "live";
    public const string Demo = "demo";
    public const string Unsupported = "unsupported";
    public const string Disconnected = "disconnected";

    public static bool IsDemoRequest(string? mode) =>
        string.Equals(mode, Demo, StringComparison.OrdinalIgnoreCase);

    public static bool IsValidRequest(string? mode) =>
        string.IsNullOrWhiteSpace(mode) ||
        string.Equals(mode, Live, StringComparison.OrdinalIgnoreCase) ||
        IsDemoRequest(mode);
}
