## Gate

Phase 2 — yes. Phase 1 was accepted by the user on 2026-09-22, and the Process Map sidecar Definition of Done was proven on this machine. The user confirmed all four Phase 2 acceptance checks passed on 2026-09-22.

## Status

complete

## Changed files

Historical paths below use `<PLUGIN_ROOT>` and `<SDK_ROOT>` in place of machine-specific drive paths; replace them with the corresponding folders before rerunning an old command.

`<SDK_ROOT>\.gitignore` — excludes `.ddf`, captured samples, workflow packages, and inventory output.

`<SDK_ROOT>\deswik_pm\__main__.py` — adds fail-closed `install` and `inventory` CLI verbs.

`<SDK_ROOT>\deswik_pm\sidecar.py` — exposes the five approved `map.*` actions and file SHA256 provenance.

`<SDK_ROOT>\tests\test_roundtrip.py` — proves sidecar provenance plus traversal and overwrite refusal.

`<SDK_ROOT>\README.md` — documents the new CLI and bridge actions.

`<PLUGIN_ROOT>\.gitignore` — makes the bridge test project trackable.

`<PLUGIN_ROOT>\deswik-mcp\src\Deswik.Bridge.Standalone\ProcessMapSidecar.cs` — launches the absolute pinned Python entry point with a fixed argument vector and exact action allowlist.

`<PLUGIN_ROOT>\deswik-mcp\src\Deswik.Bridge.Standalone\TcpBridge.cs` — routes approved `map.*` requests to the local sidecar before add-in capability routing.

`<PLUGIN_ROOT>\deswik-mcp\src\Deswik.Bridge.Tests\Program.cs` — verifies the allowlist, integrity pin, and an end-to-end inspect response.

`<PLUGIN_ROOT>\deswik-mcp\tools\deswik.ps1` — adds a Process Map example.

`<PLUGIN_ROOT>\docs\Wire-Protocol.md` — documents action schemas, provenance, and install refusal rules.

`<PLUGIN_ROOT>\README.md` — documents sidecar ownership and integrity.

## Behaviour

Before, the bridge treated every non-demo `map.*` action as an add-in capability and the SDK had no `install` or `inventory` verb. After, `map.inspect`, `map.validate`, `map.generate`, `map.install`, and `map.inventory` execute through `C:\Python314\python.exe -I <SDK_ROOT>\deswik_pm\sidecar.py`; successful data preserves the CLI JSON shape and adds `fileHashes` entries for every read or written file.

The bridge accepts only exact, case-sensitive sidecar action names. It verifies sidecar SHA256 `B334E3A6B71D265385807F2CBCB3003226BB732789AB811E125ABF8428CA9960`, uses no shell, accepts request data only on standard input, and returns live failures for missing, altered, invalid, or timed-out sidecar execution.

`map.install` resolves into `C:\ProgramData\Deswik\Workflows`, accepts only `^_TEST_[A-Za-z0-9 _-]+\.ddf$`, rejects path syntax and traversal, creates the destination exclusively, never overwrites, and logs source, destination, and source SHA256 to `%LOCALAPPDATA%\Deswik\Logs\Deswik.ProcessMap.Install.log`.

The ignored acceptance map is `<SDK_ROOT>\workflow-packages\phase-2\_TEST_Phase 2 Sidecar.ddf`. It was generated from the repository's sanitized portable donor with verified `CreateLayers`, `EmbeddedMacro`, and `MessageBox` commands, then validated as safe. No live Workflows file was installed automatically.

## Commands run

`python <SDK_ROOT>\tests\test_roundtrip.py`

```text
PASS sidecar inspect returns the CLI JSON shape plus SHA256 provenance
PASS install rejects traversal and refuses an existing destination

ALL TESTS PASSED
```

`python <SDK_ROOT>\tests\test_commands.py`

```text
registry: 74 commands, 26 verified

ALL TESTS PASSED
```

`C:\Users\kevst\.dotnet-sdk-8\dotnet.exe build <PLUGIN_ROOT>\deswik-mcp\src\Deswik.Addin\Deswik.Addin.csproj -c Release -p:DeswikDir="C:\Program Files\Deswik\Deswik.Suite 2025.2"`

```text
    10 Warning(s)
    0 Error(s)

Time Elapsed 00:00:02.49
```

`C:\Users\kevst\.dotnet-sdk-8\dotnet.exe build <PLUGIN_ROOT>\deswik-mcp\src\Deswik.Bridge.Standalone\Deswik.Bridge.Standalone.csproj -c Release -p:DeswikDir="C:\Program Files\Deswik\Deswik.Suite 2025.2"`

```text
    8 Warning(s)
    0 Error(s)

Time Elapsed 00:00:02.59
```

`C:\Users\kevst\.dotnet-sdk-8\dotnet.exe test <PLUGIN_ROOT>\deswik-mcp\src\Deswik.Bridge.Tests\Deswik.Bridge.Tests.csproj -c Release -p:DeswikDir="C:\Program Files\Deswik\Deswik.Suite 2025.2"`

```text
PASS Process Map action allowlist is exact
PASS Process Map sidecar integrity pin is enforced
PASS Process Map inspect returns file provenance
ALL TESTS PASSED
```

The bridge executable was started for one loopback integration probe, `dsw map.inspect` returned one node and one read hash, and the process was stopped; port 9595 was confirmed free afterward.

`git check-ignore -v <SDK_ROOT>\docs\bridge-phases\phase-2-process-map-sidecar-actions.md`

```text
No output; exit 1, confirming the write-up is trackable.
```

## Constraint check

- Generated map commands are all in the verified registry; no Plugin payload was emitted.
- The generated acceptance map used the tracked portable SDK fixture, whose generic content contains no client drawing path, site, project, personal name, or production coordinate set.
- Existing round-trip tests remain byte-identical; XML handling was unchanged.
- The generated macro uses `Deswik.Graphics.*`; its layer is created by `CreateLayers`.
- No new Deswik macro or add-in API was introduced.
- No CAD object-model call was added.
- The bridge still binds only to `127.0.0.1`; the sidecar performs no network operation.
- `.ddf`, `docs/samples/`, `workflow-packages/`, and `inventory-output/` are ignored. The generated acceptance map is ignored. No inventory output or client data was saved. Existing tracked samples showed no client identity during the provenance review and were not modified.
- Nothing was pushed.
- `interactive.html` was read as raw text. SHA256 is `75983E41E59B97780712D792B258EC53DD514D115224BCCD31FFB29EA7033F38`; its hidden attributes are table-filter and responsive UI state, with no hidden instruction payload.

## Assumptions

- Assumed current, verified locally: Python interpreter `C:\Python314\python.exe`, version 3.14.6.
- Assumed current, verified locally: Deswik install `C:\Program Files\Deswik\Deswik.Suite 2025.2`.
- Assumed current, verified from tracked fixture content: `<SDK_ROOT>\tests\archive_ddf\SDK - Stope Design Layout.ddf` is the sanitized portable donor intended for SDK tests.

## Human acceptance

1. [x] With Deswik.CAD closed, start `<PLUGIN_ROOT>\deswik-mcp\src\Deswik.Bridge.Standalone\bin\Release\net8.0\Deswik.Bridge.Standalone.exe`, dot-source `<PLUGIN_ROOT>\deswik-mcp\tools\deswik.ps1`, run `dsw map.inspect @{ path='<SDK_ROOT>\workflow-packages\phase-2\_TEST_Phase 2 Sidecar.ddf' }`, and confirm `node_count` is `1` and `fileHashes` contains one `read` record with a 64-character SHA256; a disconnected, demo, missing-hash, or parse-error response disproves acceptance. User confirmed passed 2026-09-22.
2. [x] With the bridge still running, run `dsw map.install @{ source='<SDK_ROOT>\workflow-packages\phase-2\_TEST_Phase 2 Sidecar.ddf'; filename='_TEST_Phase 2 Sidecar.ddf' }`, confirm it creates `C:\ProgramData\Deswik\Workflows\_TEST_Phase 2 Sidecar.ddf`, then repeat the same command and confirm it fails because the destination exists without changing the installed file hash; any overwrite, traversal, alternate destination, or successful second install disproves acceptance. User confirmed passed 2026-09-22.
3. [x] Open a non-production blank drawing in Deswik.CAD with no solid or entity selected, open the Process Map window, load `C:\ProgramData\Deswik\Workflows\_TEST_Phase 2 Sidecar.ddf`, click the node `Create layer '_TEST_PHASE2' and draw a circle`, and confirm layer `_TEST_PHASE2` contains one blue closed circle of radius 25 and the completion message appears; missing geometry, geometry on another layer, an error dialog, or changes outside the test layer disprove acceptance. User confirmed passed 2026-09-22.
4. [x] With the bridge running, run `dsw map.inventory`, inspect the console only, and confirm `_TEST_Phase 2 Sidecar.ddf` appears with a matching `read` SHA256 in `fileHashes`; absence of the installed map, a missing hash, or writing the inventory into the repository disproves acceptance. User confirmed passed 2026-09-22.

## Missing inputs

None.

## Open questions

None.
