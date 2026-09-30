# Next-agent handover

## Start here

1. Read `C:\Users\kevst\.codex\RTK.md`. Its `rtk` wrapper was not available
   in the Phase 7 shell, so confirm availability before relying on it.
2. Read `docs/workflow-bridge-build-prompt.md`, especially the trust boundary,
   one-phase termination rule, and Phase 8.
3. Read `docs/bridge-phases/phase-8-underground-roadmap.md` and
   `docs/ug-mining-functionality-roadmap.md`.
4. Check `git status` before changing anything. The intended branch is `main`
   and the intended remote is `origin`.

## Current gate

Phase 7 is human-accepted. Phase 8 is the full underground roadmap before the
final three-node pilot, as explicitly selected by the user. The synthetic
profile editor passed its four live CAD checks on 2026-09-29. The next
read-only milestone, `get_ug_selection_context`, passed its four live checks
on 2026-09-29. It is a PowerShell/MCP action, not an **Inspect stope** button.
The opt-in polyface metrics and temporary operator-supplied role labels also
passed four live checks on 2026-09-29. Full geometry and verified role mapping
are still outstanding. The `get_cad_polyface_geometry` action reads bounded
pages of raw vertices and face indexes; its original four live checks passed.
Expanded polyface regression checks and the new paged polyline read passed the
reusable launcher suite on 2026-09-29. Raw Line, Circle, Arc, Point, Text, and
MText geometry is the current pending live gate. Always update the single root
`Run-Deswik-Tests.cmd`; never add another root launcher. Archive test intent and
human results in `phase-8-underground-roadmap.md`, and maximize meaningful
valid, boundary, refusal, stress, and no-mutation coverage rather than targeting
four checks.
The user also approved the tracked synthetic donor for the eventual pilot.

## Verified automated state

- `C:\Program Files\Deswik\Deswik.Suite 2025.2` is the resolved Deswik install.
- `python` is available on `PATH` in the current shell; recheck it before
  starting the bridge on another machine.
- `tests/test_roundtrip.py`: `ALL TESTS PASSED`.
- `tests/test_commands.py`: `ALL TESTS PASSED`.
- The optional `tests/test_phase6_tcp.py` smoke test passed against an isolated
  bridge on its disposable port.
- Isolated Release builds for the updated Addin and bridge-test harness
  succeeded with zero errors while the accepted CAD session held the normal
  output DLL.
- The executable C# harness passed all 36 checks, including a real MCP stdio
  subprocess connected to a fake loopback TCP bridge, geometry-page invariants,
  strict six-type simple-figure geometry contracts, and bounded native Points
  collection paging with raw display metadata.
- Isolated live bridge processes passed fake-addin registration and
  `get_ug_selection_context`, `get_cad_polyface_geometry`, and
  `get_cad_polyline_geometry` routing checks on temporary loopback ports. The
  new Points route is covered by the MCP adapter harness and awaits live CAD.
- `git diff --check` passed.

The current Phase 8 geometry files are uncommitted. `main` and
`origin/main` were both at `d678579` before these changes. Earlier profile,
selection, role/metric, polyface/polyline, and mixed simple-figure gates are
accepted, with native Line recorded as an automated-only gap. The new Points
collection live gate, full CTX-01, and the pilot are not. Do not push without a
new request.

## Phase 8 boundary

The final Phase 8 pilot will use three Process Map nodes:
`Inspect stope`, `Preview rings`, and `Approve write`. These controls do not
exist in the current add-in or Process Map. Node 3 must be a real
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
