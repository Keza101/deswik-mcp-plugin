"""Command-line entry point: python -m deswik_pm ...

Subcommands
-----------
  inspect <file.ddf>
        List nodes and their command grids.

  draw <shape> [options]
        Generate a process map that creates a layer and draws a shape.
        shapes: circle | square | triangle | polygon | text
        options: --out PATH --layer NAME --radius R --size S --sides N
                 --text STR --color R,G,B --donor PATH

Examples
--------
  python -m deswik_pm inspect "C:\\ProgramData\\Deswik\\Workflows\\My Map.ddf"
  python -m deswik_pm draw circle --radius 75 --color 0,128,255 \\
        --out "C:\\ProgramData\\Deswik\\Workflows\\Circle.ddf"
  python -m deswik_pm draw polygon --sides 6 --layer HEX
"""
from __future__ import annotations

import argparse
import hashlib
import json
import logging
import re
import shutil
import sys
from pathlib import Path

from . import macros
from .builder import ProcessMapBuilder, DEFAULT_DONOR
from .commands import MessageBox
from .commands import resolve
from .commands.library import COMMAND_CLASSES, VERIFIED_COMMANDS, CreateLayers, EmbeddedMacro
from .diagram import Diagram
from .package import build_package, verify_package

DEFAULT_OUT_DIR = Path(r"C:\ProgramData\Deswik\Workflows")
INSTALL_NAME = re.compile(r"^_TEST_[A-Za-z0-9 _-]+\.ddf$")


def _color(s: str | None):
    if not s:
        return None
    rgb = tuple(int(x) for x in s.split(","))
    if len(rgb) != 3 or any(x < 0 or x > 255 for x in rgb):
        raise argparse.ArgumentTypeError("color must be R,G,B values from 0 to 255")
    return rgb


def _print(data, as_json: bool) -> None:
    if as_json:
        print(json.dumps(data, indent=2))
    else:
        print(data)


def _node_summary(node):
    commands = []
    for entry in node.tag.commands:
        commands.append(
            {
                "name": entry.name,
                "verified": entry.name in VERIFIED_COMMANDS,
                "enabled": entry.use,
                "description": entry.description,
            }
        )
    return {
        "name": node.name,
        "text": node.text,
        "type": node.type.rsplit(".", 1)[-1],
        "position": {"x": node.position[0], "y": node.position[1]},
        "size": {"x": node.size[0], "y": node.size[1]},
        "commands": commands,
    }


def _diagram_summary(path: str | Path):
    path = Path(path)
    d = Diagram.load(path)
    warnings = []
    for node in d.nodes:
        for entry in node.tag.commands:
            if entry.name not in VERIFIED_COMMANDS:
                warnings.append(f"{node.name or node.text}: command {entry.name} is unverified")
    return {
        "file": str(path),
        "header": d.ddf.header.hex(),
        "version": f"0x{d.ddf.header[1]:02x}",
        "node_count": len(d.nodes),
        "nodes": [_node_summary(node) for node in d.nodes],
        "warnings": warnings,
    }


def _check_output_path(path: str | Path, force: bool) -> Path:
    out = Path(path)
    if out.exists() and not force:
        raise FileExistsError(f"{out} already exists; pass --force to overwrite")
    out.parent.mkdir(parents=True, exist_ok=True)
    return out


def _load_builder(donor: str | None):
    donor_path = Path(donor) if donor else DEFAULT_DONOR
    if not donor_path.exists():
        raise FileNotFoundError(
            f"donor file not found: {donor_path}. Pass --donor PATH for a local .ddf fixture."
        )
    return ProcessMapBuilder(donor=donor_path)


def cmd_inspect(args):
    summary = _diagram_summary(args.file)
    if args.json:
        _print(summary, True)
        return 0
    print(f"{summary['file']}: {summary['node_count']} node(s), version {summary['version']}")
    for n in summary["nodes"]:
        print(f"\n  [{n['name']}] {n['text']!r}")
        for c in n["commands"]:
            flag = "" if c["enabled"] else "  (disabled)"
            verified = "verified" if c["verified"] else "unverified"
            print(f"    - {c['name']} ({verified}){flag}")
    if summary["warnings"]:
        print("\nWarnings:")
        for warning in summary["warnings"]:
            print(f"  - {warning}")
    return 0


def cmd_validate(args):
    path = Path(args.file)
    original = path.read_bytes()
    d = Diagram.load(path)
    warnings = []
    safe = True

    if d.ddf.to_bytes() != original:
        safe = False
        warnings.append("binary container did not round-trip byte-identically")

    total_commands = 0
    for node in d.nodes:
        raw = node.raw_tag
        if not raw:
            continue
        parsed = node.tag
        total_commands += len(parsed.commands)
        if parsed.serialize() != raw:
            safe = False
            warnings.append(f"tag did not round-trip on node {node.name or node.text!r}")
        for entry in parsed.commands:
            if resolve(entry).payload != entry.payload:
                safe = False
                warnings.append(f"command payload did not round-trip: {entry.name}")

    data = _diagram_summary(path)
    data.update(
        {
            "safe": safe,
            "command_count": total_commands,
            "container_roundtrip_identical": d.ddf.to_bytes() == original,
            "manual_validation_required": "Open in Deswik.CAD and run affected nodes.",
            "warnings": data["warnings"] + warnings,
        }
    )
    if args.json:
        _print(data, True)
    else:
        verdict = "PASS" if safe else "FAIL"
        print(f"{verdict} validate {path}: {len(d.nodes)} nodes, {total_commands} commands")
        for warning in data["warnings"]:
            print(f"  - {warning}")
        print("Manual validation required: open in Deswik.CAD and run affected nodes.")
    return 0 if safe else 1


def cmd_commands(args):
    verified = sorted(VERIFIED_COMMANDS)
    unverified = sorted(name for name in COMMAND_CLASSES if name not in VERIFIED_COMMANDS)
    data = {
        "verified_count": len(verified),
        "unverified_count": len(unverified),
        "verified": verified,
        "unverified": unverified,
        "warnings": ["Unverified command names/payloads are guessed; capture a real payload before generating them."],
    }
    if args.json:
        _print(data, True)
    else:
        print(f"Verified commands ({len(verified)}):")
        for name in verified:
            print(f"  - {name}")
        print(f"\nUnverified commands ({len(unverified)}):")
        for name in unverified:
            print(f"  - {name}")
        print("\nWarning: unverified command names/payloads are guessed from docs.")
    return 0


def cmd_macro_template(args):
    layer = args.layer
    templates = {
        "shape": macros.circle(layer, radius=args.radius, rgb=_color(args.color)),
        "stope": STOPE_TEMPLATE,
        "selection-debug": SELECTION_DEBUG_TEMPLATE,
    }
    data = {
        "template": args.template,
        "source": templates[args.template],
        "warnings": [
            "Use Deswik.Graphics.* types only.",
            "Create layers with CreateLayers before running the embedded macro.",
            "Validate generated maps manually inside Deswik.CAD.",
        ],
    }
    if args.json:
        _print(data, True)
    else:
        print(data["source"])
    return 0


def build_shape_macro(shape: str, layer: str, *, radius: float = 50, size: float = 100,
                       sides: int = 6, text: str | None = None, rgb=None):
    """Shape name + params -> (macro source, human description). Shared by the
    CLI `draw` command and gui_builder.py so both call one dispatch table."""
    if shape == "circle":
        return macros.circle(layer, radius=radius, rgb=rgb), f"circle r={radius}"
    if shape == "square":
        s = size
        return macros.polyline(layer, [(0, 0), (s, 0), (s, s), (0, s)], rgb=rgb), f"{s}x{s} square"
    if shape == "triangle":
        s = size
        return macros.polyline(layer, [(0, 0), (s, 0), (s / 2, s * 0.866)], rgb=rgb), "triangle"
    if shape == "polygon":
        return macros.regular_polygon(layer, sides, radius=radius, rgb=rgb), f"{sides}-gon r={radius}"
    if shape == "text":
        return macros.text(layer, text or "Deswik", height=size, rgb=rgb), f"text {text!r}"
    raise ValueError(f"unknown shape: {shape}")


def cmd_draw(args):
    layer = args.layer or f"SDK {args.shape.upper()}"
    rgb = _color(args.color)

    try:
        src, desc = build_shape_macro(
            args.shape, layer, radius=args.radius, size=args.size,
            sides=args.sides, text=args.text, rgb=rgb,
        )
    except ValueError as exc:
        print(exc, file=sys.stderr)
        return 2

    out = Path(args.out) if args.out else DEFAULT_OUT_DIR / f"SDK Draw {args.shape.title()}.ddf"
    out = _check_output_path(out, args.force)

    b = _load_builder(args.donor)
    cl = CreateLayers()
    cl.add_layer(layer)
    b.add_node(
        f"Create layer '{layer}'\nand draw a {args.shape}",
        commands=[
            cl,
            EmbeddedMacro(src),
            MessageBox(message=f"Drew {desc} on layer '{layer}'.", title="SDK Demo"),
        ],
        tooltip=f"Creates layer {layer} and draws a {args.shape}.",
    )
    b.save(out)
    data = {
        "file": str(out),
        "output_path": str(out),
        "shape": args.shape,
        "layer": layer,
        "warnings": ["Generated maps require manual validation inside Deswik.CAD."],
    }
    if args.json:
        _print(data, True)
    else:
        print(f"saved {out}")
        print("Manual validation required: open in Deswik.CAD and click the generated node.")
    return 0


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _install_map(source: str | Path, filename: str, workflows_dir: Path = DEFAULT_OUT_DIR):
    if not INSTALL_NAME.fullmatch(filename) or any(token in filename for token in ("\\", "/", ":", "..")):
        raise ValueError("destination must match _TEST_[A-Za-z0-9 _-]+.ddf and contain no path syntax")

    source_path = Path(source).resolve(strict=True)
    if not source_path.is_file():
        raise ValueError(f"source is not a file: {source_path}")

    root = workflows_dir.resolve()
    destination = (root / filename).resolve()
    try:
        destination.relative_to(root)
    except ValueError as exc:
        raise ValueError("destination resolves outside the workflows directory") from exc

    root.mkdir(parents=True, exist_ok=True)
    source_hash = _sha256(source_path)
    created = False
    try:
        with source_path.open("rb") as src, destination.open("xb") as dst:
            created = True
            shutil.copyfileobj(src, dst)
    except Exception:
        if created and destination.exists():
            destination.unlink()
        raise

    log_dir = Path(__import__("os").environ.get("LOCALAPPDATA", Path.home())) / "Deswik" / "Logs"
    log_dir.mkdir(parents=True, exist_ok=True)
    logging.basicConfig(
        filename=log_dir / "Deswik.ProcessMap.Install.log",
        level=logging.INFO,
        format="%(asctime)s %(message)s",
        force=True,
    )
    logging.info("source=%s destination=%s source_sha256=%s", source_path, destination, source_hash)
    return {
        "source": str(source_path),
        "destination": str(destination),
        "source_sha256": source_hash,
        "installed": True,
        "warnings": ["Open the installed map in Deswik.CAD and complete manual acceptance."],
    }


def cmd_install(args):
    _print(_install_map(args.source, args.filename), args.json)
    return 0


def _inventory(workflows_dir: Path = DEFAULT_OUT_DIR):
    root = workflows_dir.resolve()
    maps = []
    if root.exists():
        for path in sorted(root.glob("*.ddf"), key=lambda item: item.name.casefold()):
            maps.append({"name": path.name, "file": str(path.resolve()), "size": path.stat().st_size})
    return {"workflows_directory": str(root), "map_count": len(maps), "maps": maps, "warnings": []}


def cmd_inventory(args):
    _print(_inventory(), args.json)
    return 0


def cmd_package_build(args):
    data = build_package(
        args.map, args.spec, args.out, args.addin, args.bridge, args.deswik_dir,
    )
    _print(data, args.json)
    return 0


def cmd_package_verify(args):
    _print(verify_package(args.package_dir), args.json)
    return 0


def cmd_edit_message(args):
    out = _check_output_path(args.out, args.force)
    diagram = Diagram.load(args.file)
    node = diagram.find_by_name(args.node) or diagram.find(args.node)
    if node is None:
        raise ValueError(f"node not found by name or text: {args.node}")

    tag = node.tag
    edited = False
    for i, entry in enumerate(tag.commands):
        if entry.name != "MessageBox":
            continue
        mb = MessageBox.from_entry(entry)
        mb.values["message"] = args.message
        tag.commands[i] = mb.to_entry(use=entry.use, description=entry.description)
        edited = True
        break
    if not edited:
        raise ValueError(f"node {args.node!r} has no MessageBox command to edit")

    node.tag = tag
    diagram.save(out)
    data = _diagram_summary(out)
    data.update(
        {
            "output_path": str(out),
            "edited_node": node.name,
            "edited_command": "MessageBox",
            "manual_validation_required": "Open in Deswik.CAD and run the edited node.",
        }
    )
    if args.json:
        _print(data, True)
    else:
        print(f"saved {out}")
        print(f"edited MessageBox on node [{node.name}]")
        print("Manual validation required: open in Deswik.CAD and run the edited node.")
    return 0


STOPE_TEMPLATE = """'#Language "WWB.NET"

Imports Deswik.Graphics
Imports Deswik.Graphics.Figures

Sub Main
    ' Verified pattern: scan layers for the selected polyface by ShowGrips.
    Dim selectedSolid As Object = Nothing
    For Each lay As Object In CurrentDoc.Layers
        For i As Integer = 0 To lay.Entities.Count - 1
            Dim fig As Object = lay.Entities.Item(i)
            If fig.ShowGrips AndAlso fig.IsPolyface Then
                selectedSolid = fig.asPolyface
                Exit For
            End If
        Next
        If Not selectedSolid Is Nothing Then Exit For
    Next

    If selectedSolid Is Nothing Then
        MsgBox("Select one stope solid before running this node.")
        Exit Sub
    End If

    MsgBox("Selected stope solid found. Extend this macro from docs/MACRO-RECIPE.md.")
End Sub
"""


SELECTION_DEBUG_TEMPLATE = """'#Language "WWB.NET"

Imports Deswik.Graphics
Imports Deswik.Graphics.Figures

Sub Main
    Dim report As String = ""
    For Each lay As Object In CurrentDoc.Layers
        For i As Integer = 0 To lay.Entities.Count - 1
            Dim fig As Object = lay.Entities.Item(i)
            If fig.ShowGrips Then
                report = report & lay.Name & ": IsPolyface=" & fig.IsPolyface & ", IsPolyline=" & fig.IsPolyline & vbCrLf
            End If
        Next
    Next
    If report = "" Then report = "No selected figures found via ShowGrips."
    MsgBox(report)
End Sub
"""


def build_parser():
    p = argparse.ArgumentParser(prog="deswik_pm", description="Deswik process map SDK")
    sub = p.add_subparsers(dest="cmd", required=True)

    pi = sub.add_parser("inspect", help="list nodes/commands in a .ddf")
    pi.add_argument("file")
    pi.add_argument("--json", action="store_true")
    pi.set_defaults(func=cmd_inspect)

    pv = sub.add_parser("validate", help="parse and verify round-trip safety for a .ddf")
    pv.add_argument("file")
    pv.add_argument("--json", action="store_true")
    pv.set_defaults(func=cmd_validate)

    pc = sub.add_parser("commands", help="list verified and unverified command classes")
    pc.add_argument("--json", action="store_true")
    pc.set_defaults(func=cmd_commands)

    pm = sub.add_parser("macro-template", help="print a known-good embedded macro template")
    pm.add_argument("template", choices=["shape", "stope", "selection-debug"])
    pm.add_argument("--layer", default="SDK TEMPLATE")
    pm.add_argument("--radius", type=float, default=50)
    pm.add_argument("--color", help="R,G,B (0-255)")
    pm.add_argument("--json", action="store_true")
    pm.set_defaults(func=cmd_macro_template)

    pe = sub.add_parser("edit-message", help="safely edit the first MessageBox on a node")
    pe.add_argument("file")
    pe.add_argument("--node", required=True, help="node Name, or substring of node text")
    pe.add_argument("--message", required=True)
    pe.add_argument("--out", required=True)
    pe.add_argument("--force", action="store_true")
    pe.add_argument("--json", action="store_true")
    pe.set_defaults(func=cmd_edit_message)

    pd = sub.add_parser("draw", help="generate a shape-drawing process map")
    pd.add_argument("shape", choices=["circle", "square", "triangle", "polygon", "text"])
    pd.add_argument("--out")
    pd.add_argument("--layer")
    pd.add_argument("--radius", type=float, default=50)
    pd.add_argument("--size", type=float, default=100)
    pd.add_argument("--sides", type=int, default=6)
    pd.add_argument("--text", dest="text")
    pd.add_argument("--color", help="R,G,B (0-255)")
    pd.add_argument("--donor", help="donor .ddf for the container")
    pd.add_argument("--force", action="store_true")
    pd.add_argument("--json", action="store_true")
    pd.set_defaults(func=cmd_draw)

    pin = sub.add_parser("install", help="install a test-prefixed map without overwriting")
    pin.add_argument("source")
    pin.add_argument("filename")
    pin.add_argument("--json", action="store_true")
    pin.set_defaults(func=cmd_install)

    p_inv = sub.add_parser("inventory", help="list maps in the live Workflows directory")
    p_inv.add_argument("--json", action="store_true")
    p_inv.set_defaults(func=cmd_inventory)

    pp = sub.add_parser("package", help="build or verify a portable workflow package")
    package_sub = pp.add_subparsers(dest="package_cmd", required=True)

    pp_build = package_sub.add_parser("build")
    pp_build.add_argument("--map", required=True)
    pp_build.add_argument("--spec", required=True)
    pp_build.add_argument("--out", required=True)
    pp_build.add_argument("--addin", required=True)
    pp_build.add_argument("--bridge", required=True)
    pp_build.add_argument("--deswik-dir", required=True)
    pp_build.add_argument("--json", action="store_true")
    pp_build.set_defaults(func=cmd_package_build)

    pp_verify = package_sub.add_parser("verify")
    pp_verify.add_argument("package_dir")
    pp_verify.add_argument("--json", action="store_true")
    pp_verify.set_defaults(func=cmd_package_verify)
    return p


def main(argv=None):
    args = build_parser().parse_args(argv)
    try:
        return args.func(args) or 0
    except Exception as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
