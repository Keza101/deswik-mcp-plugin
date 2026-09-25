"""High-level builder for process maps.

Wraps the repeated boilerplate seen across the example generators:
clone a donor .ddf for its container (appearance XML + binary tail), strip
its nodes, reset to a single process-map layer, and add nodes whose command
grids are assembled from typed command objects.

Example
-------
    from deswik_pm.builder import ProcessMapBuilder
    from deswik_pm.commands import MessageBox
    from deswik_pm.commands.library import CreateLayers, EmbeddedMacro

    b = ProcessMapBuilder()
    cl = CreateLayers(); cl.add_layer("SDK CIRCLE")
    b.add_node(
        "Draw a circle",
        commands=[cl, EmbeddedMacro(macro_src), MessageBox(message="Done")],
    )
    b.save(r"C:\\ProgramData\\Deswik\\Workflows\\My Map.ddf")
"""
from __future__ import annotations

import copy
import uuid
import xml.etree.ElementTree as ET
from pathlib import Path

from .diagram import Diagram
from .tag import NodeTag

# A donor file ships the appearance XML + binary tail we cannot yet author
# from scratch. Any real map works; this one is large but always present.
DEFAULT_DONOR = Path(r"C:\ProgramData\Deswik\Workflows\Starlight - UG Survey.ddf")


class ProcessMapBuilder:
    """Assemble a process map by cloning a donor container and adding nodes."""

    def __init__(self, donor: str | Path = DEFAULT_DONOR, layer_name: str = "Main"):
        donor = Path(donor)
        if not donor.exists():
            raise FileNotFoundError(f"donor .ddf not found: {donor}")
        self.diagram = Diagram.load(donor)
        self._template = self._extract_template()
        self._clear()
        self._reset_layers(layer_name)
        self._y = 60  # auto-stacking cursor for node placement

    # -- container preparation -------------------------------------------------

    def _extract_template(self) -> ET.Element:
        nodes_el = self.diagram.root.find("nodes")
        for el in nodes_el.findall("node"):
            if el.findtext("__type", "").endswith("RoundedRectangle"):
                return copy.deepcopy(el)
        raise ValueError("donor has no RoundedRectangle node to use as template")

    def _clear(self) -> None:
        for tag in ("nodes", "links"):
            parent = self.diagram.root.find(tag)
            if parent is not None:
                for el in list(parent):
                    parent.remove(el)

    def _reset_layers(self, layer_name: str) -> None:
        root = self.diagram.root
        layers_el = root.find("Layers64")
        for el in list(layers_el):
            layers_el.remove(el)
        layer = ET.SubElement(layers_el, "Layer")
        ET.SubElement(layer, "Name").text = layer_name
        ET.SubElement(layer, "MaskIdx").text = "0"
        self._set(root, "Tag1",
                  f"MaintainStatusInDocument=False|DefaultLayers={layer_name}|"
                  "MaintainLayerVisibilityInDocument=False")
        self._set(root, "VisibleLayerMask", "1")
        self._set(root, "VisibleLayerMaskNew", "False:1")
        self._set(root, "ActiveLayer", layer_name)

    # -- node assembly ---------------------------------------------------------

    def add_node(self, text: str, commands=None, *, name: str | None = None,
                 tooltip: str = "", pos: tuple[int, int] | None = None,
                 size: tuple[int, int] = (440, 100)):
        """Add a node. `commands` is a list of typed command objects (anything
        with .to_entry()) or CommandEntry instances. Returns the node element."""
        node_el = copy.deepcopy(self._template)
        self.diagram.root.find("nodes").append(node_el)

        self._set(node_el, "DiagramGuid", str(uuid.uuid4()))
        self._set(node_el, "Name", name or text.splitlines()[0])
        self._set(node_el, "_text", text)
        self._set(node_el, "LayersMask", "1")
        self._set(node_el, "LayersMaskNew", "False:1")
        sx, sy = size
        self._set(node_el.find("_scale"), "X", str(sx))
        self._set(node_el.find("_scale"), "Y", str(sy))
        px, py = pos if pos else (60, self._y)
        self._set(node_el.find("_translation"), "X", str(px))
        self._set(node_el.find("_translation"), "Y", str(py))
        self._y = py + sy + 40

        tag = NodeTag()
        for cmd in (commands or []):
            tag.commands.append(cmd.to_entry() if hasattr(cmd, "to_entry") else cmd)
        tag.props["ToolTip"] = tooltip
        tag.props["LastTab"] = "1"
        tag.props["Hidden"] = "False"
        tag.props["AllowViewing"] = "True"
        self._set(node_el, "Tag", tag.serialize())
        return node_el

    def save(self, path: str | Path) -> Path:
        path = Path(path)
        self.diagram.save(path)
        return path

    # -- helpers ---------------------------------------------------------------

    @staticmethod
    def _set(parent: ET.Element, tag: str, value: str) -> None:
        el = parent.find(tag)
        if el is None:
            el = ET.SubElement(parent, tag)
        el.text = value
