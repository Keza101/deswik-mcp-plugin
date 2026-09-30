using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Deswik.Ug.Design;

public static class HandleValue
{
    public static ulong Parse(string raw)
    {
        var hex = raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
        var digits = hex ? raw[2..] : raw;
        var style = hex ? NumberStyles.AllowHexSpecifier : NumberStyles.None;
        if (!ulong.TryParse(digits, style, CultureInfo.InvariantCulture, out var handle) || handle == 0)
            throw new ArgumentException($"Invalid handle: {raw}");
        return handle;
    }
}

public sealed record GeometryPageRequest(ulong Handle, int Start, int Limit)
{
    public static GeometryPageRequest Parse(JsonElement parameters)
    {
        if (parameters.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Geometry parameters must be an object");
        ulong handle = 0;
        var start = 0;
        var limit = 250;
        foreach (var parameter in parameters.EnumerateObject())
        {
            switch (parameter.Name)
            {
                case "handle":
                    handle = parameter.Value.ValueKind switch
                    {
                        JsonValueKind.Number when parameter.Value.TryGetUInt64(out var numeric) && numeric > 0 => numeric,
                        JsonValueKind.String => HandleValue.Parse(parameter.Value.GetString()!),
                        _ => throw new ArgumentException("handle must be a positive decimal number or 0x-prefixed hex string"),
                    };
                    break;
                case "start":
                    if (parameter.Value.ValueKind != JsonValueKind.Number ||
                        !parameter.Value.TryGetInt32(out start) || start < 0)
                        throw new ArgumentException("start must be a non-negative integer");
                    break;
                case "limit":
                    if (parameter.Value.ValueKind != JsonValueKind.Number ||
                        !parameter.Value.TryGetInt32(out limit) || limit is < 1 or > 500)
                        throw new ArgumentException("limit must be an integer from 1 to 500");
                    break;
                default:
                    throw new ArgumentException($"Unsupported geometry parameter: {parameter.Name}");
            }
        }
        if (handle == 0) throw new ArgumentException("handle is required");
        return new GeometryPageRequest(handle, start, limit);
    }

    public int CountFor(int total) => Start >= total ? 0 : Math.Min(Limit, total - Start);

    public int? NextStart(int vertexCount, int faceCount) =>
        (long)Start + Limit < Math.Max(vertexCount, faceCount) ? Start + Limit : null;

    public int? NextStart(int vertexCount) =>
        (long)Start + Limit < vertexCount ? Start + Limit : null;
}

public sealed record FaceIndices(
    [property: JsonPropertyName("a")] int A,
    [property: JsonPropertyName("b")] int B,
    [property: JsonPropertyName("c")] int C,
    [property: JsonPropertyName("d")] int D);

public sealed record PolyfaceGeometryPage(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("handle")] ulong Handle,
    [property: JsonPropertyName("start")] int Start,
    [property: JsonPropertyName("limit")] int Limit,
    [property: JsonPropertyName("vertexCount")] int VertexCount,
    [property: JsonPropertyName("faceCount")] int FaceCount,
    [property: JsonPropertyName("vertices")] IReadOnlyList<Point3> Vertices,
    [property: JsonPropertyName("faces")] IReadOnlyList<FaceIndices> Faces,
    [property: JsonPropertyName("nextStart")] int? NextStart,
    [property: JsonPropertyName("faceIndexSource")] string FaceIndexSource,
    [property: JsonPropertyName("coordinateSystem")] string CoordinateSystem,
    [property: JsonPropertyName("units")] string Units,
    [property: JsonPropertyName("readyForDesign")] bool ReadyForDesign);

public sealed record PolylineGeometryPage(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("handle")] ulong Handle,
    [property: JsonPropertyName("start")] int Start,
    [property: JsonPropertyName("limit")] int Limit,
    [property: JsonPropertyName("vertexCount")] int VertexCount,
    [property: JsonPropertyName("vertices")] IReadOnlyList<Point3> Vertices,
    [property: JsonPropertyName("closed")] bool Closed,
    [property: JsonPropertyName("nextStart")] int? NextStart,
    [property: JsonPropertyName("coordinateSystem")] string CoordinateSystem,
    [property: JsonPropertyName("units")] string Units,
    [property: JsonPropertyName("readyForDesign")] bool ReadyForDesign);

public sealed record PointsGeometryPage(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("handle")] ulong Handle,
    [property: JsonPropertyName("start")] int Start,
    [property: JsonPropertyName("limit")] int Limit,
    [property: JsonPropertyName("pointCount")] int PointCount,
    [property: JsonPropertyName("points")] IReadOnlyList<Point3> Points,
    [property: JsonPropertyName("nextStart")] int? NextStart,
    [property: JsonPropertyName("isPointCloud")] bool IsPointCloud,
    [property: JsonPropertyName("pointStyle")] string? PointStyle,
    [property: JsonPropertyName("sizeType")] string? SizeType,
    [property: JsonPropertyName("alignToView")] bool AlignToView,
    [property: JsonPropertyName("alignToViewSize")] double AlignToViewSize,
    [property: JsonPropertyName("extrusionVector")] Point3 ExtrusionVector,
    [property: JsonPropertyName("coordinateSystem")] string CoordinateSystem,
    [property: JsonPropertyName("units")] string Units,
    [property: JsonPropertyName("readyForDesign")] bool ReadyForDesign);
