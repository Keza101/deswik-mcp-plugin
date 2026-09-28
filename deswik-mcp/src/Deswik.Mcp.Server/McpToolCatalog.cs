using System.Text.Json;
using System.Text.Json.Serialization;

namespace Deswik.Mcp.Server;

public sealed record McpToolDefinition(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("inputSchema")] object InputSchema,
    [property: JsonIgnore] bool RequiresHumanToken = false);

/// <summary>
/// The MCP surface is deliberately narrower than the raw bridge surface.
/// File-producing Process Map actions and legacy CAD writers are not tools.
/// Production CAD writes require a token minted by the in-CAD approval flow.
/// </summary>
public static class McpToolCatalog
{
    private static readonly object Empty = Schema();
    private static readonly object Handle = new Dictionary<string, object>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object>
        {
            ["handle"] = new Dictionary<string, object>
            {
                ["description"] = "Deswik entity handle",
                ["oneOf"] = new object[]
                {
                    new Dictionary<string, object> { ["type"] = "integer", ["minimum"] = 0 },
                    new Dictionary<string, object> { ["type"] = "string", ["minLength"] = 1 },
                },
            },
        },
        ["required"] = new[] { "handle" },
        ["additionalProperties"] = false,
    };

    public static IReadOnlyList<McpToolDefinition> Tools { get; } = new[]
    {
        new McpToolDefinition("get_cad_document", "Return the active Deswik drawing identity.", Empty),
        new McpToolDefinition("get_cad_layers", "Return the active drawing's layer tree.", Empty),
        new McpToolDefinition("get_cad_layer_attributes", "Return entities and attributes from one layer.",
            Schema(("layer", String("Exact layer path")), ("limit", Integer(1, 10000)), required: new[] { "layer" })),
        new McpToolDefinition("get_cad_elements", "Return bounded entity summaries, optionally from one layer.",
            Schema(("layer", String("Optional exact layer path")), ("limit", Integer(1, 10000)))),
        new McpToolDefinition("get_cad_selection", "Return the current Deswik selection with supported geometry.", Empty),
        new McpToolDefinition("get_cad_polyface_info", "Return mesh statistics and bounds for one polyface.", Handle),
        new McpToolDefinition("get_cad_polylines_under", "Return polylines below a layer subtree.",
            Schema(("layerPrefix", String("Layer subtree prefix")), required: new[] { "layerPrefix" })),
        new McpToolDefinition("get_cad_blasthole_details", "Return native BlastHole fields for one handle.", Handle),
        new McpToolDefinition("get_cad_ugdrillhole_details", "Return native UGDrillHole fields for one handle.", Handle),
        new McpToolDefinition("preview_ugdrillholes", "Create temporary preview geometry and open the default-No Deswik approval dialog.",
            Schema(("holes", HoleArray()), required: new[] { "holes" })),
        new McpToolDefinition("get_write_approval", "Fetch a token only after the matching in-CAD approval was accepted.",
            Schema(("approvalId", String("Approval identifier returned by preview")), required: new[] { "approvalId" })),
        new McpToolDefinition("commit_ugdrillholes", "Commit the approved preview using a single-use human-minted token.",
            TokenSchema(), RequiresHumanToken: true),
        new McpToolDefinition("prepare_rollback_ugdrillholes", "Open a default-No rollback approval dialog for one commit.",
            Schema(("commitId", String("Commit identifier")), required: new[] { "commitId" })),
        new McpToolDefinition("rollback_ugdrillholes", "Rollback one recorded commit using a separate human-minted token.",
            TokenSchema(), RequiresHumanToken: true),
        new McpToolDefinition("job.submit", "Submit an approved long CAD read or token-bound commit/rollback.", JobSubmitSchema()),
        new McpToolDefinition("job.get", "Poll bridge-process job state.", JobIdSchema()),
        new McpToolDefinition("job.cancel", "Request idempotent cancellation of one bridge-process job.", JobIdSchema()),
        new McpToolDefinition("map.inspect", "Inspect an approved local Process Map file and return SHA256 provenance.", PathSchema()),
        new McpToolDefinition("map.validate", "Validate an approved local Process Map file and return SHA256 provenance.", PathSchema()),
        new McpToolDefinition("map.inventory", "Inventory the local Deswik Workflows directory without modifying it.", Empty),
    };

    private static readonly Dictionary<string, McpToolDefinition> ByName =
        Tools.ToDictionary(tool => tool.Name, StringComparer.Ordinal);

    public static bool TryGet(string name, out McpToolDefinition definition) =>
        ByName.TryGetValue(name, out definition!);

    public static string? ValidateCall(McpToolDefinition tool, JsonElement arguments)
    {
        if (arguments.ValueKind != JsonValueKind.Object)
            return "Tool arguments must be a JSON object";
        if (tool.RequiresHumanToken && !HasToken(arguments))
            return "A non-empty human-minted token is required";
        if (tool.Name == "job.submit")
        {
            if (!arguments.TryGetProperty("action", out var actionElement) ||
                actionElement.ValueKind != JsonValueKind.String)
                return "job.submit requires an approved action";
            var action = actionElement.GetString();
            if (action is "commit_ugdrillholes" or "rollback_ugdrillholes")
            {
                if (!arguments.TryGetProperty("args", out var nested) || !HasToken(nested))
                    return "A non-empty human-minted token is required for a write job";
            }
        }
        return null;
    }

    private static bool HasToken(JsonElement value) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty("token", out var token) &&
        token.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(token.GetString());

    private static object TokenSchema() =>
        Schema(("token", String("Opaque single-use token minted after in-CAD approval")), required: new[] { "token" });
    private static object JobIdSchema() =>
        Schema(("jobId", String("Bridge-process job identifier")), required: new[] { "jobId" });
    private static object PathSchema() =>
        Schema(("path", String("Absolute local Process Map path")), required: new[] { "path" });

    private static object JobSubmitSchema() => new Dictionary<string, object>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object>
        {
            ["action"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["enum"] = new[]
                {
                    "get_cad_document", "get_cad_layers", "get_cad_layer_attributes",
                    "get_cad_elements", "get_cad_selection", "get_cad_polyface_info",
                    "get_cad_polylines_under", "get_cad_blasthole_details",
                    "get_cad_ugdrillhole_details", "commit_ugdrillholes", "rollback_ugdrillholes",
                },
            },
            ["args"] = new Dictionary<string, object>
            {
                ["type"] = "object", ["description"] = "Arguments for the selected bridge action",
            },
        },
        ["required"] = new[] { "action" },
        ["additionalProperties"] = false,
    };

    private static object HoleArray() => new Dictionary<string, object>
    {
        ["type"] = "array", ["minItems"] = 1,
        ["items"] = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>
            {
                ["pivot"] = Point(), ["collar"] = Point(), ["toe"] = Point(),
                ["pivotId"] = String("Optional pivot ID"), ["holeId"] = String("Optional hole ID"),
                ["diameter"] = new Dictionary<string, object> { ["type"] = "number", ["exclusiveMinimum"] = 0 },
            },
            ["required"] = new[] { "pivot", "collar", "toe" },
            ["additionalProperties"] = false,
        },
    };

    private static object Point() => new Dictionary<string, object>
    {
        ["type"] = "array", ["minItems"] = 3, ["maxItems"] = 3,
        ["items"] = new Dictionary<string, object> { ["type"] = "number" },
    };
    private static object String(string description) => new Dictionary<string, object>
    {
        ["type"] = "string", ["minLength"] = 1, ["description"] = description,
    };
    private static object Integer(int minimum, int maximum) => new Dictionary<string, object>
    {
        ["type"] = "integer", ["minimum"] = minimum, ["maximum"] = maximum,
    };

    private static object Schema(
        (string Name, object Definition) first = default,
        (string Name, object Definition) second = default,
        string[]? required = null)
    {
        var properties = new Dictionary<string, object>();
        if (!string.IsNullOrEmpty(first.Name)) properties[first.Name] = first.Definition;
        if (!string.IsNullOrEmpty(second.Name)) properties[second.Name] = second.Definition;
        var schema = new Dictionary<string, object>
        {
            ["type"] = "object", ["properties"] = properties, ["additionalProperties"] = false,
        };
        if (required is { Length: > 0 }) schema["required"] = required;
        return schema;
    }
}
