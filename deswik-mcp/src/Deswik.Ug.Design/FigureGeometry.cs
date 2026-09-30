using System.Text.Json;
using System.Text.Json.Serialization;

namespace Deswik.Ug.Design;

public sealed record FigureGeometryRequest(ulong Handle)
{
    public static FigureGeometryRequest Parse(JsonElement parameters)
    {
        if (parameters.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Figure geometry parameters must be an object");
        ulong handle = 0;
        foreach (var parameter in parameters.EnumerateObject())
        {
            if (parameter.Name != "handle")
                throw new ArgumentException($"Unsupported figure geometry parameter: {parameter.Name}");
            handle = parameter.Value.ValueKind switch
            {
                JsonValueKind.Number when parameter.Value.TryGetUInt64(out var numeric) && numeric > 0 => numeric,
                JsonValueKind.String => HandleValue.Parse(parameter.Value.GetString()!),
                _ => throw new ArgumentException("handle must be a positive decimal number or 0x-prefixed hex string"),
            };
        }
        if (handle == 0) throw new ArgumentException("handle is required");
        return new FigureGeometryRequest(handle);
    }
}

public sealed record FigureGeometrySnapshot
{
    public static readonly IReadOnlySet<string> SupportedTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "Line", "Circle", "Arc", "Point", "Text", "MText",
    };

    [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; init; } = 1;
    [JsonPropertyName("handle")] public required ulong Handle { get; init; }
    [JsonPropertyName("guid")] public required string Guid { get; init; }
    [JsonPropertyName("type")] public required string Type { get; init; }
    [JsonPropertyName("layer")] public string? Layer { get; init; }
    [JsonPropertyName("label")] public string? Label { get; init; }
    [JsonPropertyName("startPoint")] public Point3? StartPoint { get; init; }
    [JsonPropertyName("endPoint")] public Point3? EndPoint { get; init; }
    [JsonPropertyName("centerPoint")] public Point3? CenterPoint { get; init; }
    [JsonPropertyName("insertionPoint")] public Point3? InsertionPoint { get; init; }
    [JsonPropertyName("alignmentPoint")] public Point3? AlignmentPoint { get; init; }
    [JsonPropertyName("extrusionVector")] public Point3? ExtrusionVector { get; init; }
    [JsonPropertyName("radius")] public double? Radius { get; init; }
    [JsonPropertyName("startAngle")] public double? StartAngle { get; init; }
    [JsonPropertyName("endAngle")] public double? EndAngle { get; init; }
    [JsonPropertyName("length")] public double? Length { get; init; }
    [JsonPropertyName("area")] public double? Area { get; init; }
    [JsonPropertyName("height")] public double? Height { get; init; }
    [JsonPropertyName("rotation")] public double? Rotation { get; init; }
    [JsonPropertyName("thickness")] public double? Thickness { get; init; }
    [JsonPropertyName("text")] public string? Text { get; init; }
    [JsonPropertyName("alignToView")] public bool? AlignToView { get; init; }
    [JsonPropertyName("attributes")] public IReadOnlyDictionary<string, object?> Attributes { get; init; } =
        new Dictionary<string, object?>();
    [JsonPropertyName("coordinateSystem")] public string CoordinateSystem { get; init; } = "unknown";
    [JsonPropertyName("units")] public string Units { get; init; } = "unknown";
    [JsonPropertyName("angleUnits")] public string AngleUnits { get; init; } = "unknown";
    [JsonPropertyName("readyForDesign")] public bool ReadyForDesign { get; init; }

    public FigureGeometrySnapshot Validate()
    {
        if (SchemaVersion != 1) throw new ArgumentException("Unsupported figure geometry schema version");
        if (Handle == 0) throw new ArgumentException("Figure handle must be positive");
        if (!SupportedTypes.Contains(Type)) throw new ArgumentException($"Unsupported figure geometry type: {Type}");
        if (ReadyForDesign) throw new ArgumentException("Raw figure geometry cannot be marked design-ready");
        foreach (var value in new[] { Radius, StartAngle, EndAngle, Length, Area, Height, Rotation, Thickness })
            if (value.HasValue && !double.IsFinite(value.Value))
                throw new ArgumentException("Figure geometry contains a non-finite numeric value");
        foreach (var point in new[] { StartPoint, EndPoint, CenterPoint, InsertionPoint, AlignmentPoint, ExtrusionVector })
            if (point != null && new[] { point.X, point.Y, point.Z }.Any(value => !double.IsFinite(value)))
                throw new ArgumentException("Figure geometry contains a non-finite point");
        return this;
    }
}
