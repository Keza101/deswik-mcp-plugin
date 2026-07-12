using System;
using System.Collections.Generic;
using System.Linq;
using DwApplication = Deswik.Graphics.Application;
using DwLayer = Deswik.Graphics.Primaries.Layer;
using DwFigure = Deswik.Graphics.Primaries.Figure;

namespace Deswik.Addin;

/// <summary>
/// Reads live data from the injected Deswik.Graphics.Application.
/// All members verified against Deswik.Graphics.dll 2025.2 — compiling
/// against the real DLL is the API check.
/// </summary>
internal class CadReader
{
    private readonly DwApplication _app;

    public CadReader(DwApplication app)
    {
        _app = app;
    }

    public object GetDocumentInfo()
    {
        return new
        {
            filename = _app.Filename,
            fileDirectory = _app.FileDirectory,
            isDirty = _app.IsDirty,
            isReadOnly = _app.IsReadOnly,
            activeLayerName = _app.ActiveLayerName,
            activeLayoutName = _app.ActiveLayoutName,
            layerCount = _app.Layers.Count
        };
    }

    public object GetLayers()
    {
        var result = new List<object>();
        foreach (var obj in _app.Layers)
        {
            if (obj is not DwLayer layer) continue;
            if (layer.Deleted) continue;

            int entityCount;
            try
            {
                entityCount = layer.Entities?.Count ?? 0;
            }
            catch
            {
                entityCount = -1; // e.g. reference-only layer not loaded
            }

            result.Add(new
            {
                name = layer.Name,
                visible = layer.Visible,
                locked = layer.Locked,
                isLoaded = layer.IsLoaded,
                entityCount,
                description = layer.Description
            });
        }
        return result;
    }

    public object GetElements(string? layerName, int limit)
    {
        var figures = new List<DwFigure>();

        if (!string.IsNullOrEmpty(layerName))
        {
            var layer = _app.Layers.FindName(layerName);
            if (layer == null)
                throw new ArgumentException($"Layer not found: {layerName}");
            CollectFigures(layer.Entities, figures, limit);
        }
        else
        {
            foreach (var obj in _app.Layers)
            {
                if (figures.Count >= limit) break;
                if (obj is not DwLayer layer || layer.Deleted || !layer.IsLoaded) continue;
                try
                {
                    CollectFigures(layer.Entities, figures, limit);
                }
                catch
                {
                    // skip layers whose entities cannot be read
                }
            }
        }

        return figures.Select(Describe).ToList();
    }

    private static void CollectFigures(System.Collections.IEnumerable? entities, List<DwFigure> into, int limit)
    {
        if (entities == null) return;
        foreach (var e in entities)
        {
            if (into.Count >= limit) return;
            if (e is DwFigure fig) into.Add(fig);
        }
    }

    private static object Describe(DwFigure fig)
    {
        string? layerName = null;
        try { layerName = fig.Layer?.Name; } catch { }

        return new
        {
            handle = fig.HandleID,
            guid = fig.GUID,
            type = FigureType(fig),
            layer = layerName,
            label = fig.Label,
            visible = fig.Visible
        };
    }

    private static string FigureType(DwFigure fig)
    {
        if (fig.IsPolyline) return "Polyline";
        if (fig.IsPolyface) return "Polyface";
        if (fig.IsCircle) return "Circle";
        if (fig.IsLine) return "Line";
        if (fig.IsArc) return "Arc";
        if (fig.IsPoint) return "Point";
        if (fig.IsPoints) return "Points";
        if (fig.IsText) return "Text";
        if (fig.IsMText) return "MText";
        if (fig.IsInsert) return "Insert";
        if (fig.IsImage) return "Image";
        if (fig.IsUGDrillHole) return "UGDrillHole";
        if (fig.IsBlastHole) return "BlastHole";
        return fig.GetType().Name;
    }

    /// <summary>Currently selected figures with their bounding boxes.</summary>
    public object GetSelection()
    {
        if (!_app.SelectionExists)
            return new { count = 0, figures = new List<object>() };

        var sel = _app.Selections.SelectedEntities();
        var figures = new List<object>();

        foreach (var obj in sel)
        {
            if (obj is not DwFigure fig) continue;

            object? bbox = null;
            try
            {
                var box = fig.BoundingBox;
                bbox = new
                {
                    min = Pt(box.Min),
                    max = Pt(box.Max),
                    mid = Pt(box.Midpoint),
                    dx = box.DX,
                    dy = box.DY,
                    dz = box.DZ
                };
            }
            catch
            {
                // some figure types have no valid bounding box
            }

            string? layerName = null;
            try { layerName = fig.Layer?.Name; } catch { }

            figures.Add(new
            {
                handle = fig.HandleID,
                guid = fig.GUID,
                type = FigureType(fig),
                layer = layerName,
                boundingBox = bbox
            });
        }

        return new { count = figures.Count, figures };
    }

    // Note: Deswik.Graphics.Geometry.Point exposes lowercase x/y/z.
    private static object Pt(Deswik.Graphics.Geometry.Point p) => new { x = p.x, y = p.y, z = p.z };

    private DwFigure FindByHandle(ulong handle)
    {
        foreach (var obj in _app.Layers)
        {
            if (obj is not DwLayer layer || layer.Deleted || !layer.IsLoaded) continue;
            System.Collections.IEnumerable? entities;
            try { entities = layer.Entities; } catch { continue; }
            if (entities == null) continue;
            foreach (var e in entities)
            {
                if (e is DwFigure fig && fig.HandleID == handle) return fig;
            }
        }
        throw new ArgumentException($"No figure with handle {handle}");
    }

    private Deswik.Graphics.Figures.Polyface RequirePolyface(ulong handle)
    {
        var fig = FindByHandle(handle);
        if (!fig.IsPolyface)
            throw new ArgumentException($"Figure {handle} is not a Polyface (it is {FigureType(fig)})");
        return fig.asPolyface;
    }

    /// <summary>COG, bounding box, volume of a polyface (for design math).</summary>
    public object GetPolyfaceInfo(ulong handle)
    {
        var pf = RequirePolyface(handle);
        var cog = pf.CenterOfGravity;
        var box = pf.BoundingBox;
        return new
        {
            handle,
            cog = Pt(cog),
            boundingBox = new
            {
                min = Pt(box.Min),
                max = Pt(box.Max),
                mid = Pt(box.Midpoint),
                dx = box.DX,
                dy = box.DY,
                dz = box.DZ
            },
            volume = pf.Volume,
            vertexCount = pf.VertexCount
        };
    }

    /// <summary>
    /// Slices a polyface with the plane through origin with the given normal;
    /// returns the intersection loops as lists of [x,y,z] points.
    /// (Same API the V1.82 ring-layout macro uses: GenerateSlicePoints.)
    /// </summary>
    public object SlicePolyface(ulong handle, double[] origin, double[] normal)
    {
        var pf = RequirePolyface(handle);
        var loops = pf.GenerateSlicePoints(
            new Deswik.Graphics.Geometry.Point(origin[0], origin[1], origin[2]),
            new Deswik.Graphics.Geometry.Vector(normal[0], normal[1], normal[2]));

        var result = new List<List<double[]>>();
        if (loops != null)
        {
            foreach (var loopObj in loops)
            {
                if (loopObj is not Deswik.Graphics.Geometry.Verticies vl) continue;
                var lp = new List<double[]>();
                for (int i = 0; i < vl.Count; i++)
                {
                    var p = vl[i];
                    lp.Add(new[] { p.x, p.y, p.z });
                }
                if (lp.Count > 0) result.Add(lp);
            }
        }
        return new { handle, loopCount = result.Count, loops = result };
    }

    #region Write operations

    /// <summary>Creates the layer if it does not exist; returns its info.</summary>
    public object CreateLayer(string name)
    {
        var existing = _app.Layers.FindName(name);
        if (existing == null)
        {
            _app.Layers.Add(name);
            existing = _app.Layers.FindName(name)
                ?? throw new InvalidOperationException($"Layer '{name}' could not be created");
        }

        return new
        {
            name = existing.Name,
            created = true,
            visible = existing.Visible,
            entityCount = existing.Entities?.Count ?? 0
        };
    }

    /// <summary>Draws an MText on the given layer (creating the layer if needed).</summary>
    public object DrawText(string layerName, string text, double x, double y, double z, double height)
    {
        var layer = _app.Layers.FindName(layerName);
        if (layer == null)
        {
            _app.Layers.Add(layerName);
            layer = _app.Layers.FindName(layerName)
                ?? throw new InvalidOperationException($"Layer '{layerName}' could not be created");
        }

        var mtext = new Deswik.Graphics.Figures.MText
        {
            TextString = text,
            InsertionPoint = new Deswik.Graphics.Geometry.Point(x, y, z),
            Height = height,
            Layer = layer
        };
        mtext.PenColor.SetRGB(41, 143, 207);

        _app.ActiveLayout.Entities.AddItem(mtext);
        _app.Regen(true);

        return new
        {
            handle = mtext.HandleID,
            guid = mtext.GUID,
            layer = layer.Name,
            text,
            position = new { x, y, z },
            height
        };
    }

    /// <summary>
    /// Draws a batch of polylines (plus optional MText labels) on one layer
    /// (created if needed) with a single Regen at the end.
    /// </summary>
    public object DrawPolylines(string layerName, IReadOnlyList<PolylineSpec> specs, IReadOnlyList<TextSpec>? texts = null)
    {
        var layer = _app.Layers.FindName(layerName);
        if (layer == null)
        {
            _app.Layers.Add(layerName);
            layer = _app.Layers.FindName(layerName)
                ?? throw new InvalidOperationException($"Layer '{layerName}' could not be created");
        }

        var handles = new List<ulong>();
        foreach (var spec in specs)
        {
            var pts = spec.Points
                .Select(p => new Deswik.Graphics.Geometry.Point(p[0], p[1], p[2]))
                .ToList();
            if (pts.Count < 2) continue;

            var pl = new Deswik.Graphics.Figures.Polyline(pts)
            {
                Closed = spec.Closed,
                Layer = layer
            };
            if (!string.IsNullOrEmpty(spec.Label)) pl.Label = spec.Label;
            pl.PenColor.SetRGB((byte)spec.R, (byte)spec.G, (byte)spec.B);
            _app.ActiveLayout.Entities.AddItem(pl);
            handles.Add(pl.HandleID);
        }

        int textCount = 0;
        if (texts != null)
        {
            foreach (var t in texts)
            {
                var mt = new Deswik.Graphics.Figures.MText
                {
                    TextString = t.Text,
                    InsertionPoint = new Deswik.Graphics.Geometry.Point(t.X, t.Y, t.Z),
                    Height = t.Height,
                    AlignToView = true,
                    Layer = layer
                };
                mt.PenColor.SetRGB((byte)t.R, (byte)t.G, (byte)t.B);
                _app.ActiveLayout.Entities.AddItem(mt);
                textCount++;
            }
        }

        _app.Regen(true);

        return new
        {
            layer = layer.Name,
            drawn = handles.Count,
            textsDrawn = textCount,
            handles
        };
    }

    /// <summary>
    /// Full parameter dump of an existing BlastHole (for diffing our holes
    /// against UGDB-created ones).
    /// </summary>
    public object GetBlastHoleDetails(ulong handle)
    {
        var fig = FindByHandle(handle);
        if (!fig.IsBlastHole)
            throw new ArgumentException($"Figure {handle} is not a BlastHole (it is {FigureType(fig)})");
        var bh = fig.asBlastHole;

        object? Try(Func<object?> f)
        {
            try { return f(); } catch (Exception ex) { return $"<err: {ex.Message}>"; }
        }

        return new
        {
            handle,
            layer = Try(() => fig.Layer?.Name),
            label = Try(() => fig.Label),
            holeId = Try(() => bh.HoleID),
            diameter = Try(() => bh.Diameter),
            azimuth = Try(() => bh.HoleAzimuth),
            dip = Try(() => bh.HoleDip),
            length = Try(() => bh.HoleLength),
            burden = Try(() => bh.Burden),
            spacing = Try(() => bh.Spacing),
            origin = Try(() => Pt(bh.Origin)),
            collar = Try(() => Pt(bh.CollarPoint)),
            toe = Try(() => Pt(bh.ToePoint)),
            holeOption = Try(() => bh.HoleOption),
            projectionPlane = Try(() => bh.ProjectionPlane),
            orientation = Try(() => bh.Orientation),
            status = Try(() => bh.Status.ToString()),
            patternConfiguration = Try(() => bh.PatternConfiguration?.ToString()),
            collarOverride = Try(() => bh.CollarOverride),
            subDrill = Try(() => bh.SubDrill),
            bearing = Try(() => bh.Bearing),
            angle = Try(() => bh.Angle),
            holeColumn = Try(() => bh.HoleColumn),
            gridRow = Try(() => bh.GridRow),
            chargePlanId = Try(() => bh.ChargePlanID),
            chargeState = Try(() => bh.ChargeState.ToString()),
            isValid = Try(() => bh.IsValid)
        };
    }

    /// <summary>
    /// Returns every polyline whose layer name starts with the given prefix,
    /// with its full vertex list. Used to learn design parameters from an
    /// existing hand-made drill design (persistent layers, no selection needed).
    /// </summary>
    public object GetPolylinesUnder(string layerPrefix)
    {
        var result = new List<object>();
        foreach (var obj in _app.Layers)
        {
            if (obj is not DwLayer layer || layer.Deleted) continue;
            if (!layer.Name.StartsWith(layerPrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            System.Collections.IEnumerable? entities;
            try { entities = layer.Entities; } catch { continue; }
            if (entities == null) continue;

            foreach (var e in entities)
            {
                if (e is not DwFigure fig || !fig.IsPolyline) continue;
                var pl = fig.asPolyline;
                var pts = new List<double[]>();
                try
                {
                    var vl = pl.VertexList;
                    for (int i = 0; i < vl.Count; i++)
                    {
                        var p = vl[i];
                        pts.Add(new[] { p.x, p.y, p.z });
                    }
                }
                catch { }
                if (pts.Count == 0) continue;

                result.Add(new
                {
                    handle = fig.HandleID,
                    layer = layer.Name,
                    label = fig.Label,
                    closed = pl.Closed,
                    points = pts
                });
            }
        }
        return new { layerPrefix, count = result.Count, polylines = result };
    }

    /// <summary>
    /// Reads entity ATTRIBUTES (Deswik user attributes, e.g. Deswik.IS task
    /// polygons' task/activity data) for entities on a layer. Attribute names
    /// come from the layer's attribute definitions; values via
    /// Figure.AttributeValueGet.
    /// </summary>
    public object GetLayerAttributes(string layerName, int limit)
    {
        var layer = _app.Layers.FindName(layerName)
            ?? throw new ArgumentException($"Layer not found: {layerName}");

        // attribute definition names (reflect: item type varies by version)
        var names = new List<string>();
        try
        {
            if (layer.Attributes is System.Collections.IEnumerable defs)
            {
                foreach (var a in defs)
                {
                    if (a == null) continue;
                    var nm = a.GetType().GetProperty("Name")?.GetValue(a)?.ToString()
                             ?? a.ToString();
                    if (!string.IsNullOrEmpty(nm) && !names.Contains(nm!))
                        names.Add(nm!);
                }
            }
        }
        catch { }

        var rows = new List<object>();
        System.Collections.IEnumerable? entities = null;
        try { entities = layer.Entities; } catch { }
        if (entities != null)
        {
            foreach (var e in entities)
            {
                if (rows.Count >= limit) break;
                if (e is not DwFigure fig) continue;
                var vals = new Dictionary<string, object?>();
                foreach (var n in names)
                {
                    try
                    {
                        var v = fig.AttributeValueGet(n, null);
                        if (v == null) continue;
                        vals[n] = v is string or bool or int or long or double or float
                            ? v : v.ToString();
                    }
                    catch { }
                }
                rows.Add(new { handle = fig.HandleID, attrs = vals });
            }
        }

        return new
        {
            layer = layer.Name,
            attributeNames = names,
            count = rows.Count,
            entities = rows
        };
    }

    /// <summary>
    /// Full parameter dump of an existing UGDrillHole (UGDB's native hole
    /// type) - used to learn UGDB's conventions from real samples.
    /// </summary>
    public object GetUGDrillHoleDetails(ulong handle)
    {
        var fig = FindByHandle(handle);
        if (!fig.IsUGDrillHole)
            throw new ArgumentException($"Figure {handle} is not a UGDrillHole (it is {FigureType(fig)})");
        var h = fig.asUGDrillHole;

        object? Val(Func<object?> f)
        {
            try
            {
                var v = f();
                if (v is Deswik.Graphics.Geometry.Point p) return Pt(p);
                if (v is null || v is string || v.GetType().IsPrimitive) return v;
                return v.ToString();
            }
            catch (Exception ex)
            {
                return $"<err: {ex.Message}>";
            }
        }

        List<object>? verts = null;
        try
        {
            verts = new List<object>();
            var vl = h.VertexList;
            for (int i = 0; i < vl.Count; i++) verts.Add(Pt(vl[i]));
        }
        catch { }

        return new
        {
            handle,
            layer = Val(() => fig.Layer?.Name),
            label = Val(() => fig.Label),
            ringId = Val(() => h.RingID),
            pivotId = Val(() => h.PivotID),
            pivotPoint = Val(() => h.PivotPoint),
            collarPoint = Val(() => h.CollarPoint),
            toePoint = Val(() => h.ToePoint),
            collar = Val(() => h.Collar),
            dip = Val(() => h.Dip),
            dipReal = Val(() => h.DipReal),
            direction = Val(() => h.Direction),
            holeLength = Val(() => h.HoleLength),
            length = Val(() => h.Length),
            originalLength = Val(() => h.OriginalLength),
            pivotToCollarLength = Val(() => h.PivotToCollarLength),
            upHole = Val(() => h.UpHole),
            diameterReal = Val(() => h.DiameterReal),
            diameterString = Val(() => h.DiameterString),
            subDrill = Val(() => h.SubDrill),
            breakThrough = Val(() => h.BreakThrough),
            orderIndex = Val(() => h.OrderIndex),
            isWinze = Val(() => h.IsWinze),
            showId = Val(() => h.ShowID),
            show3D = Val(() => h.Show3D),
            explosive = Val(() => h.Explosive),
            chargeCollar = Val(() => h.ChargeCollar),
            chargeLength = Val(() => h.ChargeLength),
            comments = Val(() => h.Comments),
            asBuilt = Val(() => h.AsBuilt),
            extended = Val(() => h.Extended),
            vertexList = verts
        };
    }

    /// <summary>
    /// Creates native UGDrillHole figures following UGDB's observed model:
    /// VertexList = [pivot, collar, toe], RingID/PivotID linkage, and the
    /// RINGDESIGN\project\ring\HOLES layer supplied by the caller.
    /// </summary>
    public object DrawUGDrillHoles(string layerName, IReadOnlyList<UGHoleSpec> specs)
    {
        var layer = _app.Layers.FindName(layerName);
        if (layer == null)
        {
            _app.Layers.Add(layerName);
            layer = _app.Layers.FindName(layerName)
                ?? throw new InvalidOperationException($"Layer '{layerName}' could not be created");
        }

        var handles = new List<ulong>();
        var errors = new List<string>();
        foreach (var s in specs)
        {
            try
            {
                var pivot = new Deswik.Graphics.Geometry.Point(s.Pivot[0], s.Pivot[1], s.Pivot[2]);
                var collar = new Deswik.Graphics.Geometry.Point(s.Collar[0], s.Collar[1], s.Collar[2]);
                var toe = new Deswik.Graphics.Geometry.Point(s.Toe[0], s.Toe[1], s.Toe[2]);

                double dx = s.Toe[0] - s.Collar[0];
                double dy = s.Toe[1] - s.Collar[1];
                double dz = s.Toe[2] - s.Collar[2];
                double horiz = Math.Sqrt(dx * dx + dy * dy);
                double len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                double p2c = Math.Sqrt(
                    Math.Pow(s.Collar[0] - s.Pivot[0], 2) +
                    Math.Pow(s.Collar[1] - s.Pivot[1], 2) +
                    Math.Pow(s.Collar[2] - s.Pivot[2], 2));

                var h = new Deswik.Graphics.Figures.UGDrillHole();
                h.VertexList.Add(pivot);
                h.VertexList.Add(collar);
                h.VertexList.Add(toe);
                // Hole = the grid "ID" column (H1, H2, ...); OriginalLength
                // drives the displayed Length. Both were missing before.
                h.Hole = s.HoleId;
                h.RingID = s.RingId;
                h.PivotID = s.PivotId;
                h.PivotPoint = pivot;
                h.Collar = collar;
                h.Direction = new Deswik.Graphics.Geometry.Vector(dx / len, dy / len, dz / len);
                h.OriginalLength = len;
                h.DiameterReal = s.Diameter;
                h.DiameterString = s.DiameterString;
                h.PivotToCollarLength = p2c;
                // UGDB convention (observed): Dip = angle from horizontal, positive up
                h.Dip = Math.Atan2(dz, horiz) * 180.0 / Math.PI;
                h.UpHole = false;
                h.Show3D = true;
                h.Extended = true;
                h.OrderIndex = s.OrderIndex;
                if (!string.IsNullOrEmpty(s.Explosive))
                {
                    h.Explosive = s.Explosive;
                    h.ChargeCollar = s.ChargeCollar;
                    h.ChargeLength = Math.Max(0.0, len - s.ChargeCollar);
                }
                h.Layer = layer;
                _app.ActiveLayout.Entities.AddItem(h);
                handles.Add(h.HandleID);
            }
            catch (Exception ex)
            {
                errors.Add($"pivot {s.PivotId}/order {s.OrderIndex}: {ex.Message}");
            }
        }

        _app.Regen(true);

        return new
        {
            layer = layer.Name,
            drawn = handles.Count,
            handles,
            errors
        };
    }

    /// <summary>
    /// Creates native Deswik.Graphics.Figures.BlastHole entities (the same
    /// figure type Deswik.UGDB works with) from collar/toe pairs.
    /// Azimuth/dip/length are derived from the geometry.
    /// </summary>
    public object DrawBlastHoles(string layerName, IReadOnlyList<BlastHoleSpec> specs)
    {
        var layer = _app.Layers.FindName(layerName);
        if (layer == null)
        {
            _app.Layers.Add(layerName);
            layer = _app.Layers.FindName(layerName)
                ?? throw new InvalidOperationException($"Layer '{layerName}' could not be created");
        }

        var handles = new List<ulong>();
        var errors = new List<string>();
        foreach (var s in specs)
        {
            try
            {
                double dx = s.Toe[0] - s.Collar[0];
                double dy = s.Toe[1] - s.Collar[1];
                double dz = s.Toe[2] - s.Collar[2];
                double horiz = Math.Sqrt(dx * dx + dy * dy);
                double length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                if (length < 1e-6)
                {
                    errors.Add($"{s.Id}: zero length");
                    continue;
                }
                double azimuth = (Math.Atan2(dx, dy) * 180.0 / Math.PI + 360.0) % 360.0;
                // Deswik dip convention: positive DOWN (upholes get negative dip).
                double dip = -Math.Atan2(dz, horiz) * 180.0 / Math.PI;

                var collar = new Deswik.Graphics.Geometry.Point(s.Collar[0], s.Collar[1], s.Collar[2]);
                var bh = new Deswik.Graphics.Figures.BlastHole(
                    collar, azimuth, dip, length, s.Burden, s.Spacing, s.Diameter, s.Id)
                {
                    Layer = layer
                };
                _app.ActiveLayout.Entities.AddItem(bh);
                handles.Add(bh.HandleID);
            }
            catch (Exception ex)
            {
                errors.Add($"{s.Id}: {ex.Message}");
            }
        }

        _app.Regen(true);

        return new
        {
            layer = layer.Name,
            drawn = handles.Count,
            handles,
            errors
        };
    }

    #endregion
}

/// <summary>One polyline to draw: [x,y,z] points, closed flag, RGB pen color, optional Label.</summary>
internal record PolylineSpec(List<double[]> Points, bool Closed, int R, int G, int B, string? Label = null);

/// <summary>One native blast hole: collar/toe [x,y,z], id, diameter, burden, spacing.</summary>
internal record BlastHoleSpec(double[] Collar, double[] Toe, string Id,
                              double Diameter, double Burden, double Spacing);

/// <summary>One native UG drill hole: pivot/collar/toe [x,y,z] + UGDB linkage.</summary>
internal record UGHoleSpec(double[] Pivot, double[] Collar, double[] Toe,
                           string RingId, string PivotId, string HoleId,
                           double Diameter, string DiameterString,
                           int OrderIndex, string? Explosive, double ChargeCollar);

/// <summary>One MText label to draw alongside a polyline batch.</summary>
internal record TextSpec(string Text, double X, double Y, double Z, double Height, int R, int G, int B);
