"""Build and verify portable Process Map workflow packages."""
from __future__ import annotations

import ctypes
import hashlib
import json
import shutil
from pathlib import Path

from . import __version__
from .commands.library import VERIFIED_COMMANDS
from .diagram import Diagram

MANIFEST = "manifest.json"
ACCEPTANCE = "acceptance.json"


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _file_version(path: str | Path) -> str:
    path = str(Path(path).resolve(strict=True))
    size = ctypes.windll.version.GetFileVersionInfoSizeW(path, None)
    if not size:
        raise OSError(f"FileVersion unavailable: {path}")
    data = ctypes.create_string_buffer(size)
    if not ctypes.windll.version.GetFileVersionInfoW(path, 0, size, data):
        raise OSError(f"FileVersion read failed: {path}")
    pointer, length = ctypes.c_void_p(), ctypes.c_uint()
    if not ctypes.windll.version.VerQueryValueW(data, "\\", ctypes.byref(pointer), ctypes.byref(length)):
        raise OSError(f"FileVersion query failed: {path}")

    class FixedFileInfo(ctypes.Structure):
        _fields_ = [(name, ctypes.c_uint32) for name in (
            "signature", "struct_version", "file_version_ms", "file_version_ls",
            "product_version_ms", "product_version_ls", "flags_mask", "flags",
            "os", "file_type", "subtype", "date_ms", "date_ls",
        )]

    info = ctypes.cast(pointer, ctypes.POINTER(FixedFileInfo)).contents
    return ".".join(str(value) for value in (
        info.file_version_ms >> 16,
        info.file_version_ms & 0xFFFF,
        info.file_version_ls >> 16,
        info.file_version_ls & 0xFFFF,
    ))


def _acceptance_complete(record: dict) -> bool:
    return (
        bool(record.get("tester"))
        and bool(record.get("testedAt"))
        and str(record.get("result", "")).lower() in {"pass", "passed", "accepted"}
    )


def build_package(
    map_file: str | Path,
    source_spec: str | Path,
    output_dir: str | Path,
    addin_binary: str | Path,
    bridge_binary: str | Path,
    deswik_dir: str | Path,
    *,
    version_reader=_file_version,
) -> dict:
    map_path = Path(map_file).resolve(strict=True)
    spec_path = Path(source_spec).resolve(strict=True)
    addin_path = Path(addin_binary).resolve(strict=True)
    bridge_path = Path(bridge_binary).resolve(strict=True)
    output = Path(output_dir).resolve()

    if map_path.suffix.lower() != ".ddf" or not map_path.is_file():
        raise ValueError("map_file must be an existing .ddf file")
    if spec_path.suffix.lower() != ".json" or not isinstance(json.loads(spec_path.read_text("utf-8")), dict):
        raise ValueError("source_spec must be a JSON object")
    Diagram.load(map_path)
    if output.exists():
        raise FileExistsError(f"package output already exists: {output}")
    if len({map_path.name.casefold(), spec_path.name.casefold(), MANIFEST, ACCEPTANCE}) != 4:
        raise ValueError("package filenames must be distinct from manifest.json and acceptance.json")

    deswik_dll = Path(deswik_dir).resolve() / "Deswik.Graphics.dll"
    deswik_build = version_reader(deswik_dll) if deswik_dll.is_file() else None
    deswik_source = str(deswik_dll) if deswik_build else "unavailable"

    output.mkdir(parents=True)
    try:
        packaged_map = output / map_path.name
        packaged_spec = output / spec_path.name
        shutil.copy2(map_path, packaged_map)
        shutil.copy2(spec_path, packaged_spec)
        acceptance = {
            "tester": "",
            "testedAt": "",
            "result": "",
            "notes": "",
        }
        (output / ACCEPTANCE).write_text(json.dumps(acceptance, indent=2) + "\n", encoding="utf-8")
        manifest = {
            "formatVersion": 1,
            "sdkVersion": __version__,
            "addinVersion": version_reader(addin_path),
            "bridgeVersion": version_reader(bridge_path),
            "deswikBuild": deswik_build,
            "deswikBuildSource": deswik_source,
            "verifiedCommands": sorted(VERIFIED_COMMANDS),
            "humanSuppliedFields": [
                "acceptance.tester", "acceptance.testedAt",
                "acceptance.result", "acceptance.notes",
            ],
            "files": [
                {"role": "map", "path": packaged_map.name, "sha256": _sha256(packaged_map)},
                {"role": "sourceSpec", "path": packaged_spec.name, "sha256": _sha256(packaged_spec)},
            ],
        }
        (output / MANIFEST).write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
        return {"package": str(output), "manifest": manifest, "acceptance": acceptance, "warnings": []}
    except Exception:
        shutil.rmtree(output, ignore_errors=True)
        raise


def verify_package(package_dir: str | Path) -> dict:
    root = Path(package_dir).resolve(strict=True)
    manifest = json.loads((root / MANIFEST).read_text("utf-8"))
    acceptance = json.loads((root / ACCEPTANCE).read_text("utf-8"))
    errors = []

    if manifest.get("sdkVersion") != __version__:
        errors.append("sdkVersion does not match this SDK")
    if manifest.get("verifiedCommands") != sorted(VERIFIED_COMMANDS):
        errors.append("verifiedCommands differs from the generated verified registry")
    if not manifest.get("addinVersion") or not manifest.get("bridgeVersion"):
        errors.append("addinVersion and bridgeVersion are required")
    if manifest.get("deswikBuild") is None and not _acceptance_complete(acceptance):
        errors.append("deswikBuild is unavailable and human acceptance is incomplete")

    checked = []
    for entry in manifest.get("files", []):
        relative = entry.get("path", "")
        candidate = (root / relative).resolve()
        try:
            candidate.relative_to(root)
        except ValueError:
            errors.append(f"manifest path escapes package: {relative}")
            continue
        if not candidate.is_file():
            errors.append(f"missing package file: {relative}")
            continue
        actual = _sha256(candidate)
        checked.append({"path": relative, "sha256": actual})
        if actual.lower() != str(entry.get("sha256", "")).lower():
            errors.append(f"SHA256 mismatch: {relative}")

    if len(manifest.get("files", [])) != 2 or {entry.get("role") for entry in manifest.get("files", [])} != {"map", "sourceSpec"}:
        errors.append("manifest must contain exactly the map and sourceSpec roles")
    if errors:
        raise ValueError("; ".join(errors))
    return {
        "package": str(root),
        "valid": True,
        "checkedFiles": checked,
        "acceptanceComplete": _acceptance_complete(acceptance),
        "warnings": [],
    }
