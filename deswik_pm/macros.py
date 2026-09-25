"""Generators for verified Deswik.CAD embedded-macro source (WWB.NET).

These produce the macro text you feed to commands.EmbeddedMacro. Every
pattern here was confirmed to run in Deswik.CAD Suite 2024.2; see
docs/MACRO-RECIPE.md for the hard-won environment facts behind them.

Key rules baked in:
  * Only Deswik.Graphics.* types are nameable (VectorDraw is unavailable).
  * `Point` is ambiguous -> always fully-qualified Geometry.Point.
  * Geometry is added via CurrentDoc.ActiveLayOut.Entities.AddItem(fig).
  * Create the layer with the CreateLayers command (not the macro), then
    look it up here with CurrentDoc.Layers.FindName(name).
"""
from __future__ import annotations

HEADER = "'#Language \"WWB.NET\"\n\nImports Deswik.Graphics\nImports Deswik.Graphics.Figures\n"


def _pt(x, y, z=0):
    return f"New Deswik.Graphics.Geometry.Point({x}, {y}, {z})"


def _color(var: str, rgb: tuple[int, int, int] | None) -> str:
    if rgb is None:
        return ""
    r, g, b = rgb
    return f"    {var}.PenColor.SetRGB({r}, {g}, {b})\n"


def _wrap(body: str, layer: str) -> str:
    return (
        f"{HEADER}\nSub Main\n\n"
        f"    ' Layer \"{layer}\" is created by a CreateLayers command on the same node.\n"
        f"    Dim lay As Object = CurrentDoc.Layers.FindName(\"{layer}\")\n\n"
        f"{body}\n"
        f"    CurrentDoc.Regen(True)\n\n"
        f"End Sub\n"
    )


def polyline(layer: str, points, closed: bool = True,
             rgb: tuple[int, int, int] | None = None) -> str:
    """Macro drawing a polyline through `points` ([(x,y[,z]), ...])."""
    lines = ["    Dim pts As New System.Collections.Generic.List(Of Deswik.Graphics.Geometry.Point)"]
    for p in points:
        lines.append(f"    pts.Add({_pt(*p)})")
    lines += [
        "    Dim pl As New Polyline(pts)",
        f"    pl.Closed = {'True' if closed else 'False'}",
        "    pl.Layer = lay",
    ]
    body = "\n".join(lines) + "\n" + _color("pl", rgb) + \
        "    CurrentDoc.ActiveLayOut.Entities.AddItem(pl)\n"
    return _wrap(body, layer)


def circle(layer: str, center=(0, 0, 0), radius: float = 50,
           rgb: tuple[int, int, int] | None = None) -> str:
    """Macro drawing a circle."""
    body = (
        "    Dim c As New Circle\n"
        f"    c.CenterPoint = {_pt(*center)}\n"
        f"    c.Radius = {radius}\n"
        "    c.Layer = lay\n"
        + _color("c", rgb) +
        "    CurrentDoc.ActiveLayOut.Entities.AddItem(c)\n"
    )
    return _wrap(body, layer)


def text(layer: str, string: str, at=(0, 0, 0), height: float = 30,
         rgb: tuple[int, int, int] | None = None) -> str:
    """Macro placing an MText label."""
    body = (
        "    Dim tx As New MText\n"
        f"    tx.TextString = \"{string}\"\n"
        f"    tx.InsertionPoint = {_pt(*at)}\n"
        f"    tx.Height = {height}\n"
        "    tx.Layer = lay\n"
        + _color("tx", rgb) +
        "    CurrentDoc.ActiveLayOut.Entities.AddItem(tx)\n"
    )
    return _wrap(body, layer)


def regular_polygon(layer: str, sides: int, center=(0, 0), radius: float = 50,
                    rgb: tuple[int, int, int] | None = None) -> str:
    """Macro drawing a regular n-gon as a closed polyline."""
    import math
    cx, cy = center
    pts = [
        (round(cx + radius * math.cos(2 * math.pi * i / sides), 4),
         round(cy + radius * math.sin(2 * math.pi * i / sides), 4))
        for i in range(sides)
    ]
    return polyline(layer, pts, closed=True, rgb=rgb)
