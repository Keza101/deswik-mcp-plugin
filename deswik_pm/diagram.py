"""Object model over the main diagram XML of a .ddf file.

The main XML is a <doc> element containing canvas settings, <Layers64>,
<nodes> (one <node> per shape) and <links>. Each <node> carries its shape
type in <__type>, its on-screen text in <_text>, and the command
configuration in <Tag> (see deswik_pm.tag).
"""
from __future__ import annotations

import re
import uuid
import xml.etree.ElementTree as ET
from dataclasses import dataclass

from .ddf import DdfFile
from .tag import NodeTag

XML_DECL = '<?xml version="1.0" encoding="utf-16"?>'


@dataclass
class Node:
    """Wrapper around a <node> element."""

    element: ET.Element

    @property
    def type(self) -> str:
        return self.element.findtext("__type", "")

    @property
    def guid(self) -> str:
        return self.element.findtext("DiagramGuid", "")

    @property
    def name(self) -> str:
        return self.element.findtext("Name", "")

    @name.setter
    def name(self, value: str) -> None:
        self._set("Name", value)

    @property
    def text(self) -> str:
        return self.element.findtext("_text", "")

    @text.setter
    def text(self, value: str) -> None:
        self._set("_text", value)

    @property
    def tag(self) -> NodeTag:
        return NodeTag.parse(self.element.findtext("Tag", ""))

    @tag.setter
    def tag(self, value: NodeTag) -> None:
        self._set("Tag", value.serialize())

    @property
    def raw_tag(self) -> str:
        return self.element.findtext("Tag", "")

    @property
    def position(self) -> tuple[float, float]:
        el = self.element.find("_translation")
        if el is None:
            return (0.0, 0.0)
        return (float(el.findtext("X", "0")), float(el.findtext("Y", "0")))

    @property
    def size(self) -> tuple[float, float]:
        el = self.element.find("_scale")
        if el is None:
            return (0.0, 0.0)
        return (float(el.findtext("X", "0")), float(el.findtext("Y", "0")))

    def _set(self, tag_name: str, value: str) -> None:
        el = self.element.find(tag_name)
        if el is None:
            el = ET.SubElement(self.element, tag_name)
        el.text = value

    def __repr__(self) -> str:
        return f"<Node {self.text!r} type={self.type.rsplit('.', 1)[-1]} cmds={len(self.tag.commands)}>"


class Diagram:
    """Parsed process map diagram. Construct via Diagram.load(path)."""

    def __init__(self, ddf: DdfFile):
        self.ddf = ddf
        body = re.sub(r"^<\?xml[^>]*\?>\s*", "", ddf.main_xml)
        self.root = ET.fromstring(body)

    @classmethod
    def load(cls, path) -> "Diagram":
        return cls(DdfFile.load(path))

    @property
    def nodes(self) -> list[Node]:
        parent = self.root.find("nodes")
        if parent is None:
            return []
        return [Node(el) for el in parent.findall("node")]

    def find(self, text: str) -> Node | None:
        """Find first node whose display text contains `text`."""
        for node in self.nodes:
            if text in node.text:
                return node
        return None

    def find_by_name(self, name: str) -> Node | None:
        for node in self.nodes:
            if node.name == name:
                return node
        return None

    def serialize_main_xml(self) -> str:
        body = ET.tostring(self.root, encoding="unicode")
        return XML_DECL + "\r\n" + body

    def save(self, path) -> None:
        """Write changes back into the ddf container and save."""
        self.ddf.main_xml = self.serialize_main_xml()
        self.ddf.save(path)
