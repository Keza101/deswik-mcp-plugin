using System.Text.Json;
using System.Text.Json.Serialization;

namespace Deswik.Ug.Design;

public sealed record Point3(
    [property: JsonPropertyName("x")] double X,
    [property: JsonPropertyName("y")] double Y,
    [property: JsonPropertyName("z")] double Z);

public sealed record Bounds3(
    [property: JsonPropertyName("min")] Point3 Min,
    [property: JsonPropertyName("max")] Point3 Max);

public sealed record PolyfaceMetrics(
    [property: JsonPropertyName("centerOfGravity")] Point3 CenterOfGravity,
    [property: JsonPropertyName("volume")] double Volume,
    [property: JsonPropertyName("vertexCount")] int VertexCount);

public sealed record SelectedFigure(
    [property: JsonPropertyName("handle")] ulong Handle,
    [property: JsonPropertyName("guid")] string Guid,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("layer")] string? Layer,
    [property: JsonPropertyName("bounds")] Bounds3? Bounds,
    [property: JsonPropertyName("polyfaceMetrics")] PolyfaceMetrics? PolyfaceMetrics = null)
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = "unclassified";

    [JsonPropertyName("roleSource")]
    public string RoleSource { get; init; } = "none";
}

public sealed record SelectionContextRequest(
    IReadOnlyDictionary<ulong, string> Roles, bool IncludePolyfaceMetrics)
{
    private static readonly HashSet<string> AllowedRoles = new(StringComparer.Ordinal)
    {
        "stope", "drive", "brow", "void", "ring", "hole", "survey", "unclassified"
    };

    public static SelectionContextRequest Parse(JsonElement parameters)
    {
        var roles = new Dictionary<ulong, string>();
        var includeMetrics = false;
        if (parameters.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return new SelectionContextRequest(roles, false);
        if (parameters.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Context parameters must be an object");
        foreach (var parameter in parameters.EnumerateObject())
        {
            if (parameter.NameEquals("includePolyfaceMetrics"))
            {
                if (parameter.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    throw new ArgumentException("includePolyfaceMetrics must be true or false");
                includeMetrics = parameter.Value.GetBoolean();
                continue;
            }
            if (!parameter.NameEquals("roles") || parameter.Value.ValueKind != JsonValueKind.Object)
                throw new ArgumentException($"Unsupported context parameter: {parameter.Name}");
            foreach (var assignment in parameter.Value.EnumerateObject())
            {
                var raw = assignment.Name;
                var handle = HandleValue.Parse(raw);
                var role = assignment.Value.ValueKind == JsonValueKind.String
                    ? assignment.Value.GetString()?.ToLowerInvariant() : null;
                if (role == null || !AllowedRoles.Contains(role))
                    throw new ArgumentException($"Unsupported mining role for handle {raw}");
                if (!roles.TryAdd(handle, role))
                    throw new ArgumentException($"Duplicate role handle: {raw}");
            }
        }
        return new SelectionContextRequest(roles, includeMetrics);
    }
}

public sealed record SelectionContext(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("drawingPath")] string DrawingPath,
    [property: JsonPropertyName("isDirty")] bool IsDirty,
    [property: JsonPropertyName("coordinateSystem")] string CoordinateSystem,
    [property: JsonPropertyName("units")] string Units,
    [property: JsonPropertyName("sourceHandles")] IReadOnlyList<ulong> SourceHandles,
    [property: JsonPropertyName("figures")] IReadOnlyList<SelectedFigure> Figures,
    [property: JsonPropertyName("readyForDesign")] bool ReadyForDesign,
    [property: JsonPropertyName("warnings")] IReadOnlyList<string> Warnings)
{
    public static SelectionContext FromSelection(string drawingPath, bool isDirty,
        IEnumerable<SelectedFigure> figures, SelectionContextRequest? request = null)
    {
        var selected = figures.OrderBy(figure => figure.Handle).ToArray();
        if (selected.Any(figure => figure.Handle == 0) ||
            selected.Select(figure => figure.Handle).Distinct().Count() != selected.Length)
            throw new ArgumentException("Selected figures must have unique non-zero handles");
        if (request != null)
        {
            var selectedHandles = selected.Select(figure => figure.Handle).ToHashSet();
            foreach (var handle in request.Roles.Keys)
                if (!selectedHandles.Contains(handle))
                    throw new ArgumentException($"Role handle {handle} is not selected");
        }
        selected = selected.Select(figure =>
        {
            if (request != null && request.Roles.TryGetValue(figure.Handle, out var role))
                return figure with { Role = role, RoleSource = "operator" };
            return figure with { Role = "unclassified", RoleSource = "none" };
        }).ToArray();

        var warnings = new List<string>();
        if (selected.Length == 0) warnings.Add("No CAD figures are selected");
        if (string.IsNullOrWhiteSpace(drawingPath)) warnings.Add("Save the drawing to establish its identity");
        if (isDirty) warnings.Add("The drawing has unsaved changes");
        if (request?.IncludePolyfaceMetrics == true)
            foreach (var figure in selected.Where(figure =>
                figure.Type == "Polyface" && figure.PolyfaceMetrics == null))
                warnings.Add($"Polyface metrics unavailable for handle {figure.Handle}");
        warnings.Add("Mining roles, coordinate system, and units are unverified; design generation is unavailable");

        return new SelectionContext(1, drawingPath, isDirty, "unknown", "unknown",
            selected.Select(figure => figure.Handle).ToArray(), selected, false, warnings);
    }
}
