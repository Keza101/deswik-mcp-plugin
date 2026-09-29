# Next-agent handover

## Start here

1. Read `C:\Users\kevst\.codex\RTK.md`. Its `rtk` wrapper was not available
   in the Phase 7 shell, so confirm availability before relying on it.
2. Read `docs/workflow-bridge-build-prompt.md`, especially the trust boundary,
   one-phase termination rule, and Phase 8.
3. Read `docs/bridge-phases/phase-7-mcp-adapter.md` and
   `docs/bridge-phases/phase-6-async-jobs.md`.
4. Check `git status` before changing anything. The intended branch is `main`
   and the intended remote is `origin`.

## Current gate

Phase 7 is human-accepted. The user reported “phase 7 passes” on 2026-09-29,
covering the four checks in `phase-7-mcp-adapter.md`:

1. exactly 20 typed MCP tools;
2. live `get_cad_document` against the disposable drawing;
3. unapproved and tokenless writes refused with no geometry change;
4. one human-approved MCP commit followed by its exact-handle rollback.

The Phase 7 write-up and status board record the user's acceptance. The next
separate run may begin Phase 8 under the project's one-phase-per-run rule.

## Verified automated state

- `C:\Program Files\Deswik\Deswik.Suite 2025.2` is the resolved Deswik install.
- No standalone Python was on `PATH`; the compatible runtime used was
  `C:\Program Files\LibreOffice\program\python.exe` (Python 3.12.13).
- `tests/test_roundtrip.py`: `ALL TESTS PASSED`.
- `tests/test_commands.py`: `ALL TESTS PASSED`.
- Release builds for Addin, Bridge.Standalone, and Deswik.Mcp.Server succeeded
  with zero errors.
- The executable C# harness passed all 28 checks, including a real MCP stdio
  subprocess connected to a fake loopback TCP bridge.
- `git diff --check` passed.

The ignored `.phase6-build` directory at repository root contains only about
2 MB of generated binaries/intermediates/logs. It does not belong under
`docs/bridge-phases`; the durable Phase 6 record is already there.

## Phase 8 boundary

Phase 8 is the three-node pilot:
`Inspect stope`, `Preview rings`, and `Approve write`. Node 3 must be a real
human gate that shows the change manifest and permits token minting only after
operator action. Geometry mathematics must stay in the deterministic library
outside CAD. Every run must record source handles, parameters, warnings,
created handles, versions, and disposition.

The pilot acceptance must prove both directions:

- running all three nodes in order on a real disposable drawing records source
  handles in, created handles out, token mint time, and the operator; and
- running node 3 without node 2 creates nothing.

Do not guess Process Map payloads, use an unsanitized donor, add a remote
listener, weaken the Phase 4 token boundary, or treat any file/test result as
human approval.
