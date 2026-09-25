"""Single-request JSON sidecar for approved Process Map bridge actions."""
from __future__ import annotations

import contextlib
import hashlib
import io
import json
import sys
from pathlib import Path

if __package__ in (None, ""):
    sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from deswik_pm.__main__ import main as cli_main

ACTION_ARGS = {
    "map.inspect": lambda p: ["inspect", _required(p, "path"), "--json"],
    "map.validate": lambda p: ["validate", _required(p, "path"), "--json"],
    "map.generate": lambda p: _generate_args(p),
    "map.install": lambda p: ["install", _required(p, "source"), _required(p, "filename"), "--json"],
    "map.inventory": lambda p: ["inventory", "--json"],
}


def _required(params: dict, name: str) -> str:
    value = params.get(name)
    if not isinstance(value, str) or not value:
        raise ValueError(f"{name} is required")
    return value


def _generate_args(params: dict) -> list[str]:
    args = [
        "draw", _required(params, "shape"),
        "--out", _required(params, "out"),
        "--donor", _required(params, "donor"),
        "--json",
    ]
    for name in ("layer", "text", "color"):
        value = params.get(name)
        if value is not None:
            args.extend([f"--{name}", str(value)])
    for name in ("radius", "size", "sides"):
        value = params.get(name)
        if value is not None:
            args.extend([f"--{name}", str(value)])
    return args


def _hash(path: str, access: str) -> dict:
    resolved = Path(path).resolve(strict=True)
    digest = hashlib.sha256(resolved.read_bytes()).hexdigest()
    return {"path": str(resolved), "sha256": digest, "access": access}


def _file_hashes(action: str, params: dict, data: dict) -> list[dict]:
    if action in ("map.inspect", "map.validate"):
        return [_hash(_required(params, "path"), "read")]
    if action == "map.generate":
        return [_hash(_required(params, "donor"), "read"), _hash(data["output_path"], "write")]
    if action == "map.install":
        return [_hash(data["source"], "read"), _hash(data["destination"], "write")]
    if action == "map.inventory":
        return [_hash(item["file"], "read") for item in data["maps"]]
    return []


def handle(request: dict) -> dict:
    action = request.get("action")
    params = request.get("params") or {}
    if action not in ACTION_ARGS:
        raise ValueError(f"unsupported sidecar action: {action}")
    if not isinstance(params, dict):
        raise ValueError("params must be an object")

    output, errors = io.StringIO(), io.StringIO()
    with contextlib.redirect_stdout(output), contextlib.redirect_stderr(errors):
        code = cli_main(ACTION_ARGS[action](params))
    if code:
        raise RuntimeError(errors.getvalue().strip() or f"command exited {code}")
    data = json.loads(output.getvalue())
    data["fileHashes"] = _file_hashes(action, params, data)
    return data


def main() -> int:
    try:
        request = json.loads(sys.stdin.readline())
        print(json.dumps({"success": True, "data": handle(request)}))
        return 0
    except Exception as exc:
        print(json.dumps({"success": False, "error": str(exc), "errorCode": "MAP_SIDECAR_ERROR"}))
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
