"""Low-level reader/writer for Deswik *.ddf process map files.

File layout (reverse-engineered from Deswik.CAD output, Suite 2025.x):

    byte 0      0x01            format marker
    byte 1      0x32 (50)       format version
    section     varint length + UTF-8 bytes   main diagram XML (<doc>)
    section     varint length + UTF-8 bytes   <DiagramAppearance> XML
    tail        opaque bytes to EOF           .NET BinaryFormatter stream(s)

The varint is the .NET BinaryWriter 7-bit encoded string length prefix.
Both XML sections declare encoding="utf-16" (an artifact of .NET
StringWriter) but are stored as UTF-8 byte strings.
"""
from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path

HEADER = bytes([0x01, 0x32])


def read_varint(data: bytes, pos: int) -> tuple[int, int]:
    """Read a 7-bit encoded int. Returns (value, next_pos)."""
    value, shift = 0, 0
    while True:
        b = data[pos]
        value |= (b & 0x7F) << shift
        pos += 1
        if not b & 0x80:
            return value, pos
        shift += 7


def write_varint(value: int) -> bytes:
    """Encode an int as a .NET 7-bit varint."""
    out = bytearray()
    while True:
        b = value & 0x7F
        value >>= 7
        if value:
            out.append(b | 0x80)
        else:
            out.append(b)
            return bytes(out)


@dataclass
class DdfFile:
    """A parsed .ddf file. Sections are kept as raw strings/bytes so an
    unmodified file round-trips byte-identically."""

    main_xml: str = ""
    appearance_xml: str = ""
    tail: bytes = b""
    header: bytes = HEADER

    @classmethod
    def load(cls, path: str | Path) -> "DdfFile":
        data = Path(path).read_bytes()
        # byte 0 = 0x01 marker, byte 1 = format version (0x32 in 2024.1-era
        # files, 0x33 in 2024.2+). The version byte is preserved on save.
        if data[0] != 0x01 or not (0x30 <= data[1] <= 0x3F):
            raise ValueError(
                f"Unexpected .ddf header {data[:2].hex()} (expected 01 3x)"
            )
        pos = 2
        length, pos = read_varint(data, pos)
        main_xml = data[pos : pos + length].decode("utf-8")
        pos += length
        length, pos = read_varint(data, pos)
        appearance_xml = data[pos : pos + length].decode("utf-8")
        pos += length
        return cls(
            main_xml=main_xml,
            appearance_xml=appearance_xml,
            tail=data[pos:],
            header=data[:2],
        )

    def to_bytes(self) -> bytes:
        out = bytearray(self.header)
        for xml in (self.main_xml, self.appearance_xml):
            raw = xml.encode("utf-8")
            out += write_varint(len(raw))
            out += raw
        out += self.tail
        return bytes(out)

    def save(self, path: str | Path) -> None:
        Path(path).write_bytes(self.to_bytes())
