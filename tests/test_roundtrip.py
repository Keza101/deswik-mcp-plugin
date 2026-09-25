"""Round-trip validation against repo-local or user-provided sample files."""
import sys
import tempfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent))

from deswik_pm import DdfFile, Diagram, NodeTag
from deswik_pm.__main__ import _install_map, _inventory
from deswik_pm.sidecar import handle as sidecar_handle
from deswik_pm.package import build_package, verify_package

ROOT = Path(__file__).parent.parent
ARCHIVE = ROOT / "tests" / "archive_ddf"
SAMPLE = Path(
    __import__("os").environ.get(
        "DESWIK_PM_SAMPLE",
        ARCHIVE / "SDK - Stope Design Layout.ddf",
    )
)


def _sample() -> Path:
    if SAMPLE.exists():
        return SAMPLE
    candidates = sorted(ARCHIVE.glob("*.ddf"))
    if candidates:
        return candidates[0]
    raise RuntimeError(
        "No .ddf sample available. Add fixtures under tests/archive_ddf or set DESWIK_PM_SAMPLE."
    )


def _first_node_with_command(diagram: Diagram, command_name: str):
    for node in diagram.nodes:
        for entry in node.tag.commands:
            if entry.name == command_name:
                return node
    return None


def test_file_roundtrip_byte_identical():
    sample = _sample()
    original = sample.read_bytes()
    ddf = DdfFile.load(sample)
    assert ddf.to_bytes() == original
    print(f"PASS file round-trip: {len(original):,} bytes identical")


def test_tag_roundtrip_all_nodes():
    diagram = Diagram.load(_sample())
    nodes = diagram.nodes
    failures = 0
    total_cmds = 0
    for node in nodes:
        raw = node.raw_tag
        if not raw:
            continue
        parsed = NodeTag.parse(raw)
        total_cmds += len(parsed.commands)
        if parsed.serialize() != raw:
            failures += 1
            print(f"FAIL tag round-trip on node {node.text!r}")
    assert failures == 0
    print(f"PASS tag round-trip: {len(nodes)} nodes, {total_cmds} commands, 0 mismatches")


def test_node_model():
    diagram = Diagram.load(_sample())
    node = _first_node_with_command(diagram, "MessageBox")
    assert node is not None
    tag = node.tag
    assert any(entry.name == "CreateLayers" for entry in tag.commands)
    mb_entry = next(entry for entry in tag.commands if entry.name == "MessageBox")
    from deswik_pm.commands import MessageBox
    mb = MessageBox.from_entry(mb_entry)
    assert mb.values["message"]
    print(f"PASS node model: {node!r}, MessageBox snippet={mb.values['message'][:40]!r}")


def test_edit_and_save(tmp_path=None):
    if tmp_path is None:
        tmp_ctx = tempfile.TemporaryDirectory()
        tmp_path = Path(tmp_ctx.name)
    else:
        tmp_ctx = None
    diagram = Diagram.load(_sample())
    node = _first_node_with_command(diagram, "MessageBox")
    assert node is not None
    tag = node.tag
    from deswik_pm.commands import MessageBox
    mb_index = next(i for i, entry in enumerate(tag.commands) if entry.name == "MessageBox")
    mb = MessageBox.from_entry(tag.commands[mb_index])
    mb.values["message"] = "SDK was here"
    tag.commands[mb_index] = mb.to_entry(
        use=tag.commands[mb_index].use,
        description=tag.commands[mb_index].description,
    )
    node.tag = tag
    out = tmp_path / "edited.ddf"
    diagram.save(out)
    # reload and verify
    d2 = Diagram.load(out)
    n2 = d2.find_by_name(node.name) or d2.find(node.text.splitlines()[0])
    assert "SDK was here" in n2.raw_tag
    assert len(d2.nodes) == len(diagram.nodes)
    print(f"PASS edit/save: modified map reloads, {len(d2.nodes)} nodes intact -> {out}")
    if tmp_ctx is not None:
        tmp_ctx.cleanup()


def test_sidecar_inspect_hashes():
    sample = _sample()
    result = sidecar_handle({"action": "map.inspect", "params": {"path": str(sample)}})
    assert result["node_count"] >= 1
    assert result["fileHashes"] == [{
        "path": str(sample.resolve()),
        "sha256": __import__("hashlib").sha256(sample.read_bytes()).hexdigest(),
        "access": "read",
    }]
    print("PASS sidecar inspect returns the CLI JSON shape plus SHA256 provenance")


def test_install_refuses_traversal_and_existing_destination():
    sample = _sample()
    with tempfile.TemporaryDirectory() as tmp:
        root = Path(tmp)
        for invalid in ("../escape.ddf", "_TEST_..\\escape.ddf", "C:\\escape.ddf", "plain.ddf"):
            try:
                _install_map(sample, invalid, root)
            except ValueError:
                pass
            else:
                raise AssertionError(f"unsafe install name accepted: {invalid}")

        installed = _install_map(sample, "_TEST_Phase 2.ddf", root)
        assert Path(installed["destination"]).read_bytes() == sample.read_bytes()
        try:
            _install_map(sample, "_TEST_Phase 2.ddf", root)
        except FileExistsError:
            pass
        else:
            raise AssertionError("existing destination was overwritten")
        assert _inventory(root)["map_count"] == 1
    print("PASS install rejects traversal and refuses an existing destination")


def test_package_hash_and_registry_tamper_refusal():
    with tempfile.TemporaryDirectory() as tmp:
        root = Path(tmp)
        spec = root / "source.json"
        spec.write_text('{"shape":"circle","radius":25}', encoding="utf-8")
        addin = root / "addin.dll"
        bridge = root / "bridge.exe"
        addin.write_bytes(b"addin")
        bridge.write_bytes(b"bridge")
        version = lambda path: {"addin.dll": "1.2.3.4", "bridge.exe": "5.6.7.8"}[Path(path).name]

        package = root / "package"
        build_package(_sample(), spec, package, addin, bridge, root / "missing-deswik", version_reader=version)
        acceptance = package / "acceptance.json"
        acceptance.write_text(
            '{"tester":"Engineer","testedAt":"2026-09-22","result":"passed","notes":""}\n',
            encoding="utf-8",
        )
        assert verify_package(package)["valid"]

        packaged_map = package / _sample().name
        original = packaged_map.read_bytes()
        packaged_map.write_bytes(original + b"tamper")
        try:
            verify_package(package)
        except ValueError as exc:
            assert "SHA256 mismatch" in str(exc)
        else:
            raise AssertionError("altered .ddf passed package verification")
        packaged_map.write_bytes(original)

        manifest_path = package / "manifest.json"
        manifest = __import__("json").loads(manifest_path.read_text("utf-8"))
        manifest["verifiedCommands"].append("UnverifiedInjectedCommand")
        manifest_path.write_text(__import__("json").dumps(manifest, indent=2), encoding="utf-8")
        try:
            verify_package(package)
        except ValueError as exc:
            assert "verifiedCommands" in str(exc)
        else:
            raise AssertionError("hand-edited verifiedCommands passed verification")
    print("PASS package verify rejects altered maps and hand-edited command claims")


if __name__ == "__main__":
    test_file_roundtrip_byte_identical()
    test_tag_roundtrip_all_nodes()
    test_node_model()
    test_edit_and_save()
    test_sidecar_inspect_hashes()
    test_install_refuses_traversal_and_existing_destination()
    test_package_hash_and_registry_tamper_refusal()
    print("\nALL TESTS PASSED")
