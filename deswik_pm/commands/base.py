"""Base classes for process map command payloads.

Two payload styles exist in the wild:

* Delimited — positional args joined by ``~~~^^^~~~`` (MessageBox, ...)
* DataSet — an embedded XML <NewDataSet> document whose flattened column
  names encode a settings grid (BulkExport, LayerPreset, ...). Array-style
  settings use names like ``_dw_DeleteLayers__x005B_0_x005D__LayerName``
  (``[0]`` XML-encoded).
"""
from __future__ import annotations

import re
import xml.etree.ElementTree as ET

from ..tag import ARG_SEP, CommandEntry

DATASET_DECL = '<?xml version="1.0" encoding="utf-16"?>'


class GenericCommand:
    """Fallback wrapper: holds any command by name with a raw payload."""

    name: str = ""

    def __init__(self, name: str = "", payload: str = ""):
        if name:
            self.name = name
        self.payload = payload

    def to_entry(self, use: bool = True, description: str = "") -> CommandEntry:
        return CommandEntry(
            self.name,
            self.payload,
            props=[("Use", "True" if use else "False"), ("Description", description)],
        )

    @classmethod
    def from_entry(cls, entry: CommandEntry) -> "GenericCommand":
        return cls(entry.name, entry.payload)


class DelimitedCommand(GenericCommand):
    """Command whose payload is positional args joined by ARG_SEP.

    Subclasses define `fields`: ordered arg names.
    """

    fields: list[str] = []

    def __init__(self, **kwargs):
        self.values = {f: kwargs.get(f, "") for f in self.fields}
        self.extra_args: list[str] = []  # args beyond the known fields

    @property
    def payload(self) -> str:
        args = [str(self.values[f]) for f in self.fields] + self.extra_args
        return ARG_SEP.join(args)

    @payload.setter
    def payload(self, raw: str) -> None:
        args = raw.split(ARG_SEP)
        self.values = {f: (args[i] if i < len(args) else "") for i, f in enumerate(self.fields)}
        self.extra_args = args[len(self.fields):]

    @classmethod
    def from_entry(cls, entry: CommandEntry):
        obj = cls()
        obj.payload = entry.payload
        return obj


SECTION_SEP = "***|||***"


class DataSetCommand(GenericCommand):
    """Command whose payload is an embedded <NewDataSet> XML document,
    optionally followed by extra ``***|||***``-separated sections.

    Settings are exposed as a flat dict of column name -> value under the
    Deswik_OPIS_Settings row. If the payload is not parseable XML it is kept
    verbatim (settings stays empty) so nothing is ever lost.
    """

    table: str = "Deswik_OPIS_Settings"

    def __init__(self, settings: dict[str, str] | None = None):
        self.settings: dict[str, str] = dict(settings or {})
        self.extra_sections: list[str] = []
        self._raw: str | None = None  # set when payload could not be parsed
        self._raw_xml: str | None = None  # original XML text, kept verbatim

    @property
    def payload(self) -> str:
        if self._raw is not None:
            return self._raw
        if self._raw_xml is not None:
            xml = self._raw_xml
        else:
            ds = ET.Element("NewDataSet")
            row = ET.SubElement(ds, self.table)
            for key, value in self.settings.items():
                ET.SubElement(row, key).text = str(value)
            xml = DATASET_DECL + "\n\n" + ET.tostring(ds, encoding="unicode")
        return SECTION_SEP.join([xml] + self.extra_sections)

    @payload.setter
    def payload(self, raw: str) -> None:
        self._raw = None
        self._raw_xml = None
        self.settings = {}
        self.extra_sections = []
        parts = raw.split(SECTION_SEP)
        body = re.sub(r"^<\?xml[^>]*\?>\s*", "", parts[0])
        try:
            ds = ET.fromstring(body)
        except ET.ParseError:
            self._raw = raw
            return
        self._raw_xml = parts[0]
        self.extra_sections = parts[1:]
        row = ds.find(self.table)
        if row is not None:
            for el in row:
                self.settings[el.tag] = el.text or ""

    def set(self, key: str, value: str) -> None:
        """Change a setting and mark the XML for regeneration."""
        self.settings[key] = value
        self._raw_xml = None
        self._raw = None

    @classmethod
    def from_entry(cls, entry: CommandEntry):
        obj = cls()
        obj.payload = entry.payload
        return obj

    @staticmethod
    def array_key(prefix: str, index: int, column: str) -> str:
        """Build an array-style column name, e.g.
        array_key('_dw_DeleteLayers', 0, 'LayerName') ->
        '_dw_DeleteLayers__x005B_0_x005D__LayerName'."""
        return f"{prefix}__x005B_{index}_x005D__{column}"
