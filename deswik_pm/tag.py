"""Parser/serializer for the node <Tag> payload inside a .ddf diagram.

Grammar (segments joined by ``**||**``):

    command*  node_prop*

    command   = "Name:payload" cmd_prop*
    cmd_prop  = "LayerSource=..." | "Use=bool" | "Description=text"
                (in that order; Description terminates the command's props)
    node_prop = "Key=Value"

Node-level property keys observed: ToolTip, LastTab, Hidden, AllowViewing,
SplitterPosition, LayerSource, DisabledOnLayers.

Command payloads are either an embedded XML <NewDataSet> document (grid
based commands) or positional arguments joined by ``~~~^^^~~~`` (simple
commands such as MessageBox). Payloads may also contain further delimiter
levels (``***|||***``, ``##||##``); they are kept verbatim here and
interpreted by the command classes in deswik_pm.commands.
"""
from __future__ import annotations

import re
from dataclasses import dataclass, field

SEG_SEP = "**||**"
ARG_SEP = "~~~^^^~~~"

_PROP_RE = re.compile(r"^([A-Za-z_]\w*)=")
_CMD_RE = re.compile(r"^([A-Za-z_]\w*):")


def _is_prop(seg: str) -> bool:
    pm = _PROP_RE.match(seg)
    if not pm:
        return False
    if ":" in seg and seg.index(":") < seg.index("="):
        return False
    return True


@dataclass
class CommandEntry:
    """One command on a node: name, raw payload, and its grid options.

    `props` preserves the original key order (e.g. LayerSource before Use).
    """

    name: str
    payload: str = ""
    props: list[tuple[str, str]] = field(default_factory=lambda: [("Use", "True"), ("Description", "")])

    def _get(self, key: str, default: str = "") -> str:
        for k, v in self.props:
            if k == key:
                return v
        return default

    def _set(self, key: str, value: str) -> None:
        for i, (k, _) in enumerate(self.props):
            if k == key:
                self.props[i] = (key, value)
                return
        # insert before Description if present, else append
        for i, (k, _) in enumerate(self.props):
            if k == "Description":
                self.props.insert(i, (key, value))
                return
        self.props.append((key, value))

    @property
    def use(self) -> bool:
        return self._get("Use", "True") == "True"

    @use.setter
    def use(self, value: bool) -> None:
        self._set("Use", "True" if value else "False")

    @property
    def description(self) -> str:
        return self._get("Description")

    @description.setter
    def description(self, value: str) -> None:
        self._set("Description", value)

    def to_segments(self) -> list[str]:
        return [f"{self.name}:{self.payload}"] + [f"{k}={v}" for k, v in self.props]

    @property
    def args(self) -> list[str]:
        """Positional args for delimiter-style payloads."""
        return self.payload.split(ARG_SEP)


@dataclass
class NodeTag:
    """Parsed contents of a node's <Tag> element."""

    commands: list[CommandEntry] = field(default_factory=list)
    props: dict[str, str] = field(default_factory=dict)

    @classmethod
    def parse(cls, tag: str) -> "NodeTag":
        node = cls()
        if not tag:
            return node
        segments = tag.split(SEG_SEP)
        i = 0
        while i < len(segments):
            seg = segments[i]
            if _is_prop(seg):
                key, _, value = seg.partition("=")
                node.props[key] = value
                i += 1
                continue
            cm = _CMD_RE.match(seg)
            if not cm:
                raise ValueError(f"Unrecognized tag segment: {seg[:80]!r}")
            entry = CommandEntry(name=cm.group(1), payload=seg[cm.end():], props=[])
            i += 1
            # consume command props; Description= terminates the block
            while i < len(segments) and _is_prop(segments[i]):
                key, _, value = segments[i].partition("=")
                entry.props.append((key, value))
                i += 1
                if key == "Description":
                    break
            node.commands.append(entry)
        return node

    def serialize(self) -> str:
        segments: list[str] = []
        for cmd in self.commands:
            segments.extend(cmd.to_segments())
        for key, value in self.props.items():
            segments.append(f"{key}={value}")
        return SEG_SEP.join(segments)
