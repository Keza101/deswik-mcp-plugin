# Phase 5 — Controlled in-process map-to-bridge action

## Gate

Phase 4 is complete, including the 2026-09-25 new-machine stale-source
regression. Phase 5 passed its three live CAD acceptance checks on 2026-09-25
after the first two macro attempts exposed syntax errors that were corrected.

## Status

complete — Release artifacts, generated test map, automated tests, host
preflight, and the three live CAD acceptance checks passed.

## Design decision

Two rejected designs are retained as evidence:

1. `ExecuteFile` cannot satisfy the original hand-edited-map abuse test. A
   modified `.ddf` can replace the helper path with `cmd.exe` and bypass every
   check in the helper or bridge. Preventing that requires Deswik product
   enforcement or Windows application control.
2. Deswik's `Plugin` Process Map command is not a command dispatcher. Its
   payload is `AssemblyName{StartupClass}`, and the live test proved that
   running it rewrites the plugin's registered startup class. The failed test
   map is archived with a non-`.ddf` extension and cannot be selected normally.

The accepted implementation uses a verified `EmbeddedMacro` command. The
macro starts no process and loads no plugin. It finds the already-loaded
`Deswik.Addin` assembly in the current AppDomain and invokes the single public
entry point `Deswik.Addin.ProcessMapActions.Run`. That entry point maps an
exact, case-sensitive command ID through a compile-time allowlist and makes one
loopback request to the bridge. Unknown IDs are refused before a request.

The normal Plugin Manager startup class remains `DeswikMcpAddin` throughout.

The generated map is controlled by the workflow builder, but a hostile
hand-authored `.ddf` remains executable content because Deswik itself supports
`EmbeddedMacro` and `ExecuteFile`. Phase 5 does not claim to sandbox arbitrary
third-party maps.

## Changed files

- `deswik-mcp/src/Deswik.Bridge/models/ProcessMapActionPolicy.cs`
- `deswik-mcp/src/Deswik.Addin/LoopbackBridgeRequester.cs`
- `deswik-mcp/src/Deswik.Addin/ProcessMapActions.cs`
- `deswik-mcp/src/Deswik.Bridge.Tests/Program.cs`
- `deswik-mcp/tools/Test-AddinPreflight.ps1`
- `deswik_pm/__main__.py`

Generated local acceptance map:
`C:\ProgramData\Deswik\Workflows\_TEST_Phase 5 In-Process Bridge.ddf`.

Archived failed map:
`C:\ProgramData\Deswik\Workflows\_TEST_Phase 5 Plugin Launcher.ddf.failed-plugin-design`.

Archived first macro attempt:
`C:\ProgramData\Deswik\Workflows\_TEST_Phase 5 In-Process Bridge.ddf.failed-macro-header`.

Archived second macro attempt:
`C:\ProgramData\Deswik\Workflows\_TEST_Phase 5 In-Process Bridge.ddf.failed-msgbox-style`.

## Automated evidence

- Both Python suites print `ALL TESTS PASSED`.
- All 17 bridge tests pass, including exact-match rejection of suffixes, case
  changes, and executable text.
- Add-in Release build succeeds with 0 errors.
- Add-in preflight passes 18/18. It verifies the public static action entry
  point, confirms the obsolete `MCP_READ_DOCUMENT` startup type is absent, and
  confirms the registered startup class is exactly `DeswikMcpAddin`.
- The generated two-node map contains two verified `EmbeddedMacro` commands,
  no `Plugin` or `ExecuteFile` command, no warnings, and has an identical
  binary-container round trip.
- The first live attempt reported `Line 1: Expecting 'Const | Data | If |
  ElseIf | Else | End | Region'`. The generator had emitted a bare
  `#Language "WWB.NET"`; verified Deswik examples use the commented directive
  `'#Language "WWB.NET"`. Both generated nodes now use the verified header.
- The second live attempt reported `Line 13: Expecting a valid data type
  (eg. Integer)` on a three-argument `MsgBox` with `MsgBoxStyle.Exclamation`.
  Both messages now use the verified one-argument form. The reflection call
  also uses the sole public `Run` method without a `BindingFlags` enum.
- Corrected generated map SHA256:
  `EA1B91A94C01AE558288681D7C6ED5C76AD4FC974DE7F74994F298EB4285F72D`.

## Human acceptance

The user reported on 2026-09-25 that all three checks worked in Deswik.CAD
with the corrected generated map:

1. [x] **Read current drawing through MCP bridge** — an `MCP Bridge` dialog
   says `Allowlisted bridge action completed` and contains live document JSON
   for the current drawing. No geometry or layer changes.
2. [x] **Refusal test: unknown MCP command** — an `MCP Bridge` dialog says
   `Bridge action refused (map_action_refused)`. No geometry, layer,
   executable, console, or external process is created.
3. [x] Open Plugin Manager and confirm the `Deswik.Addin` startup class is
   still exactly `DeswikMcpAddin`.

Phase 5 passed on the user's report of all three observations.

## Repeat generation

```powershell
python -m deswik_pm plugin-test `
  --donor 'tests\archive_ddf\SDK - Stope Design Layout.ddf' `
  --out 'C:\ProgramData\Deswik\Workflows\_TEST_Phase 5 In-Process Bridge.ddf'
```

The generator refuses to overwrite an existing file unless `--force` is
explicitly supplied.
