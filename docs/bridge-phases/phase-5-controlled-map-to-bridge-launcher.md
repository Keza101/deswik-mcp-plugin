## Gate

Phase 5 — no. The user reported Phase 4 acceptance checks 1–4 passed on 2026-09-24. Phase 5's edited-map abuse criterion cannot be met by the specified `ExecuteFile` helper and bridge alone.

## Status

blocked-on-human — choose the launcher boundary before implementation.

## Changed files

`W:\deswik-mcp-plugin\docs\bridge-phases\phase-4-guarded-cad-writes.md` — records the user's 4/4 Phase 4 acceptance report.

`W:\deswik-mcp-plugin\docs\bridge-phases\phase-5-controlled-map-to-bridge-launcher.md` — records the Phase 5 design blocker and the decision required to proceed.

## Behaviour

The Phase 5 prompt says a Process Map `ExecuteFile` node launches the executable in its plaintext `path` field, while the bridge checks the approved helper's hash. Deswik launches the `path` before the helper can make a bridge request. A local edit replacing that path with `cmd.exe` bypasses both the helper's allowlist and all bridge checks. Therefore a bridge-side SHA256 pin cannot establish the required result that an edited `.ddf` launches nothing. This is a control-order issue, not a missing hash check.

The installed `C:\ProgramData\Deswik\Workflows` directory grants inherited Modify to `Authenticated Users`; the installed `_TEST_Phase 2 Sidecar.ddf` also grants Modify. There is no Phase 5 bridge config beside a bridge binary under `C:\Program Files\`. The available SDK fixtures and installed workflow maps contain no captured `ExecuteFile` or `Plugin` node payload. The meaning of `ExecuteFile` flags and timeout remains unverified.

## Commands run

`rg -a -l "ExecuteFile" tests/archive_ddf workflow-packages` — no captured map found.

`rg -a -l -g '*.ddf' -g '!Backup/**' "ExecuteFile" 'C:\ProgramData\Deswik\Workflows'` — no captured map found.

`icacls 'C:\ProgramData\Deswik\Workflows'` — `NT AUTHORITY\Authenticated Users:(I)(OI)(CI)(M)`.

`icacls 'C:\ProgramData\Deswik\Workflows\_TEST_Phase 2 Sidecar.ddf'` — `NT AUTHORITY\Authenticated Users:(I)(M)`.

`Test-Path 'C:\Program Files\Deswik\Deswik.MCP.Bridge'` — `False`.

No Phase 5 build or live CAD launch was run. The Phase 5 Definition of Done has not been claimed.

## Constraint check

No generated map, helper, executable, config, CAD write, or production workflow was changed. No unverified `ExecuteFile` flags or `Plugin` payload were emitted. No Phase 5 capability is represented as accepted.

## Assumptions

The installed workflow ACLs reflect the active machine configuration at the time of inspection. The statement that Deswik executes `ExecuteFile.path` comes from the requested Phase 5 design and the SDK's verified command description; its actual launch behavior in CAD has not been retested in this session.

## Human acceptance

Do not run Phase 5 acceptance yet. After the launcher boundary is selected, this section will contain a beginner walkthrough in this file: PowerShell window setup, exact commands, disposable CAD/map paths, one-at-a-time clicks, expected refusals, and the edited-map abuse check. A build or connected bridge alone will not count as acceptance.

1. [ ] An approved map node invokes only the intended allowlisted bridge action in a disposable drawing.
2. [ ] An unknown action is refused without changing the drawing.
3. [ ] A modified map attempting to launch `cmd.exe` launches no process and shows an operator-visible refusal, using an enforcement point that runs before process creation.
4. [ ] The deployed helper/config integrity check refuses altered files; optional signing is checked when a signing certificate is provided.

## Missing inputs

- A decision on the launcher boundary.
- A real captured node payload for the selected Process Map command, with observed flag/timeout behavior if `ExecuteFile` remains in scope.
- A protected deployment location and, if signing is required, an approved certificate/thumbprint.

## Open questions

- Replace `ExecuteFile` with a captured Deswik `Plugin` node calling an allowlisted add-in command, or keep `ExecuteFile` and require a Windows application-control policy that denies unapproved process launches from Deswik?
