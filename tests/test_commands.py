"""Validate typed command classes against every real payload in the sample map."""
import os
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent))

from deswik_pm import Diagram
from deswik_pm.commands import COMMAND_CLASSES, VERIFIED_COMMANDS, GenericCommand, resolve

ROOT = Path(__file__).parent.parent
ARCHIVE = ROOT / "tests" / "archive_ddf"
SAMPLE = Path(os.environ.get("DESWIK_PM_SAMPLE", ARCHIVE / "SDK - Stope Design Layout.ddf"))


def _sample() -> Path:
    if SAMPLE.exists():
        return SAMPLE
    candidates = sorted(ARCHIVE.glob("*.ddf"))
    if candidates:
        return candidates[0]
    raise RuntimeError(
        "No .ddf sample available. Add fixtures under tests/archive_ddf or set DESWIK_PM_SAMPLE."
    )


def test_all_payloads_roundtrip():
    diagram = Diagram.load(_sample())
    counts, failures = {}, []
    for node in diagram.nodes:
        for entry in node.tag.commands:
            obj = resolve(entry)
            counts[entry.name] = counts.get(entry.name, 0) + 1
            if obj.payload != entry.payload:
                failures.append((node.text, entry.name))
    for name, cls in sorted(VERIFIED_COMMANDS.items()):
        mark = "typed" if cls is not GenericCommand else "raw"
        print(f"  {name}: {counts.get(name, 0)} payloads ({mark})")
    assert not failures, failures
    total = sum(counts.values())
    print(f"PASS payload round-trip: {total} commands across {len(counts)} types")


def test_typed_access():
    diagram = Diagram.load(_sample())
    from deswik_pm.commands import resolve
    from deswik_pm.commands.library import CreateLayers, EmbeddedMacro
    found = {"layers": False, "macro": False}
    for node in diagram.nodes:
        for entry in node.tag.commands:
            obj = resolve(entry)
            if isinstance(obj, CreateLayers) and not found["layers"]:
                found["layers"] = True
                print(f"  CreateLayers: found")
            if isinstance(obj, EmbeddedMacro) and not found["macro"]:
                assert "WWB.NET" in obj.payload or "Deswik.Graphics" in obj.payload
                found["macro"] = True
                print(f"  EmbeddedMacro: found")
    assert all(found.values())
    print("PASS typed access")


def test_select_entities_and_attributes_validate_roundtrip():
    from deswik_pm.tag import CommandEntry
    from deswik_pm.commands.library import SelectEntities, AttributesValidate

    samples = {
        "SelectEntities": (SelectEntities, ROOT / "docs" / "samples" / "SelectEntities.txt"),
        "AttributesValidate": (AttributesValidate, ROOT / "docs" / "samples" / "AttributesValidate.txt"),
    }
    for name, (cls, path) in samples.items():
        raw = path.read_text(encoding="utf-8")
        entry = CommandEntry(name=name, payload=raw)
        obj = resolve(entry)
        assert isinstance(obj, cls)
        assert obj.payload == raw
        print(f"  {name}: sample round-trips byte-identical via {cls.__name__}")
    print("PASS SelectEntities/AttributesValidate round-trip")


def test_registry_coverage():
    print(f"registry: {len(COMMAND_CLASSES)} commands, {len(VERIFIED_COMMANDS)} verified")
    assert len(COMMAND_CLASSES) >= 69


if __name__ == "__main__":
    test_all_payloads_roundtrip()
    test_typed_access()
    test_select_entities_and_attributes_validate_roundtrip()
    test_registry_coverage()
    print("\nALL TESTS PASSED")
