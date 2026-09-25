## Gate

Phase 3 — yes. The user confirmed all four Phase 2 acceptance checks passed on 2026-09-22, and the workflow package Definition of Done is provable on this machine without operating Deswik.CAD.

## Status

complete

## Changed files

Historical paths below use `<PLUGIN_ROOT>` and `<SDK_ROOT>` in place of machine-specific drive paths; replace them with the corresponding folders before rerunning an old command.

`<SDK_ROOT>\deswik_pm\package.py` — builds and verifies portable workflow packages with derived versions, registry data, and file hashes.

`<SDK_ROOT>\deswik_pm\__main__.py` — adds `package build` and `package verify` CLI commands.

`<SDK_ROOT>\tests\test_roundtrip.py` — proves altered maps and hand-edited verified-command claims are rejected.

`<SDK_ROOT>\README.md` — documents package build and verification commands.

`<SDK_ROOT>\KNOWLEDGE.md` — records the package workflow and human acceptance boundary.

`<PLUGIN_ROOT>\docs\bridge-phases\phase-2-process-map-sidecar-actions.md` — records the user's four passed Phase 2 checks (relocated after acceptance).

`<PLUGIN_ROOT>\docs\bridge-phases\phase-3-workflow-package-format.md` — records Phase 3 evidence and acceptance steps (relocated after acceptance).

## Behaviour

Before, a generated `.ddf` had no portable unit tying it to its source spec, software versions, verified command registry, hashes, or acceptance record. After, `deswik_pm package build` creates one directory containing the map, source JSON, `manifest.json`, and an empty `acceptance.json`; it refuses an existing package directory.

The manifest derives `sdkVersion` from `deswik_pm.__version__`, `addinVersion` and `bridgeVersion` from the binaries' Windows `FileVersion`, `deswikBuild` from the resolved `Deswik.Graphics.dll`, `deswikBuildSource` from its absolute path, and `verifiedCommands` from the runtime registry. It hashes the packaged map and source spec. The acceptance fields are explicitly marked human-supplied and remain separately editable.

`package verify` recomputes every hash in the manifest, requires exactly one map and one source-spec role, rejects path escape, rejects a changed SDK registry claim, and rejects missing version fields. When `Deswik.Graphics.dll` is unavailable, build records `deswikBuild: null` and `deswikBuildSource: "unavailable"`; verification then refuses an empty or failed acceptance record.

The ignored package built for acceptance is `<SDK_ROOT>\workflow-packages\phase-3\_TEST_Phase 3 Package`. Its derived versions are SDK `0.1.0`, add-in `2025.2.3203.55048`, bridge `1.0.0.0`, and Deswik `2025.2.3940.40169`. Initial verification passed with two matching hashes and `acceptanceComplete: false`.

## Commands run

`python <SDK_ROOT>\tests\test_roundtrip.py`

```text
PASS package verify rejects altered maps and hand-edited command claims

ALL TESTS PASSED
```

`python <SDK_ROOT>\tests\test_commands.py`

```text
registry: 74 commands, 26 verified

ALL TESTS PASSED
```

`python -m deswik_pm package build --map "<SDK_ROOT>\workflow-packages\phase-2\_TEST_Phase 2 Sidecar.ddf" --spec "<SDK_ROOT>\workflow-packages\phase-3\source-spec.json" --out "<SDK_ROOT>\workflow-packages\phase-3\_TEST_Phase 3 Package" --addin "<PLUGIN_ROOT>\deswik-mcp\src\Deswik.Addin\bin\Release\net8.0-windows\Deswik.Addin.dll" --bridge "<PLUGIN_ROOT>\deswik-mcp\src\Deswik.Bridge.Standalone\bin\Release\net8.0\Deswik.Bridge.Standalone.exe" --deswik-dir "C:\Program Files\Deswik\Deswik.Suite 2025.2" --json`

```text
"sdkVersion": "0.1.0"
"addinVersion": "2025.2.3203.55048"
"bridgeVersion": "1.0.0.0"
"deswikBuild": "2025.2.3940.40169"
```

`python -m deswik_pm package verify "<SDK_ROOT>\workflow-packages\phase-3\_TEST_Phase 3 Package" --json`

```text
"valid": true
"acceptanceComplete": false
```

`C:\Users\kevst\.dotnet-sdk-8\dotnet.exe build <PLUGIN_ROOT>\deswik-mcp\src\Deswik.Addin\Deswik.Addin.csproj -c Release -p:DeswikDir="C:\Program Files\Deswik\Deswik.Suite 2025.2"`

```text
    10 Warning(s)
    0 Error(s)

Time Elapsed 00:00:02.52
```

`C:\Users\kevst\.dotnet-sdk-8\dotnet.exe build <PLUGIN_ROOT>\deswik-mcp\src\Deswik.Bridge.Standalone\Deswik.Bridge.Standalone.csproj -c Release -p:DeswikDir="C:\Program Files\Deswik\Deswik.Suite 2025.2"`

```text
    8 Warning(s)
    0 Error(s)

Time Elapsed 00:00:03.28
```

`C:\Users\kevst\.dotnet-sdk-8\dotnet.exe test <PLUGIN_ROOT>\deswik-mcp\src\Deswik.Bridge.Tests\Deswik.Bridge.Tests.csproj -c Release -p:DeswikDir="C:\Program Files\Deswik\Deswik.Suite 2025.2"`

```text
PASS Process Map action allowlist is exact
PASS Process Map sidecar integrity pin is enforced
PASS Process Map inspect returns file provenance
ALL TESTS PASSED
```

`git check-ignore -v <SDK_ROOT>\docs\bridge-phases\phase-3-workflow-package-format.md`

```text
No output; exit 1, confirming the write-up is trackable.
```

## Constraint check

- The package contains the Phase 2 map, which uses only verified `CreateLayers`, `EmbeddedMacro`, and `MessageBox` commands; no Plugin payload was generated.
- The package map was generated from the tracked sanitized portable donor and its source spec records the donor basename and SHA256.
- Existing byte-identical container round-trip tests still pass.
- No macro content changed; the packaged macro remains within the verified `Deswik.Graphics.*` and `CreateLayers` pattern.
- No Deswik macro or add-in API was added or guessed.
- No CAD object-model call was added.
- No listener or network call was added; the bridge remains loopback-only.
- The package and its source spec are under ignored `workflow-packages/`; no package, inventory result, or client data became a fixture.
- Nothing was pushed.

## Assumptions

- Assumed current, verified locally: Python interpreter `C:\Python314\python.exe`, version 3.14.6.
- Assumed current, verified locally: Deswik install `C:\Program Files\Deswik\Deswik.Suite 2025.2` and `Deswik.Graphics.dll` FileVersion `2025.2.3940.40169`.
- Assumed current, verified from tracked fixture content: `<SDK_ROOT>\tests\archive_ddf\SDK - Stope Design Layout.ddf` is the sanitized portable donor intended for SDK tests.

## Human acceptance

1. [x] With Deswik.CAD closed, run `Set-Location '<SDK_ROOT>'`, then `python -m deswik_pm package verify "<SDK_ROOT>\workflow-packages\phase-3\_TEST_Phase 3 Package" --json` using the package directory, not its `.ddf` file. Open `<SDK_ROOT>\workflow-packages\phase-3\_TEST_Phase 3 Package\manifest.json`, and confirm the command reports `valid: true`, both file hashes match, `sdkVersion` is `0.1.0`, `addinVersion` is `2025.2.3203.55048`, `bridgeVersion` is `1.0.0.0`, and `deswikBuild` is `2025.2.3940.40169`; a mismatch, missing source path, typed-by-hand command claim, or verification failure disproves acceptance. Confirmed 2026-09-23.
2. [x] Copy `<SDK_ROOT>\workflow-packages\phase-3\_TEST_Phase 3 Package` to a temporary directory outside both repositories, append one byte to the copied `_TEST_Phase 2 Sidecar.ddf`, run `Set-Location '<SDK_ROOT>'`, then `python -m deswik_pm package verify "<temporary-package-directory>" --json`, and confirm it fails with `SHA256 mismatch`; success or a failure unrelated to the altered map disproves acceptance. Delete the temporary copy afterward. The tamper was initially applied to the original package; verification rejected it, the original was restored from the matching accepted source, and verification passed afterward.
3. [x] Open a non-production blank drawing in Deswik.CAD with no solid or entity selected, open the Process Map window, load `C:\ProgramData\Deswik\Workflows\_TEST_Phase 2 Sidecar.ddf`, click the node `Create layer '_TEST_PHASE2' and draw a circle`, confirm the blue radius-25 circle and completion message, then fill `<SDK_ROOT>\workflow-packages\phase-3\_TEST_Phase 3 Package\acceptance.json` with the tester name, ISO date, `"result": "passed"`, and concise notes; missing or incorrect geometry, an error dialog, or an acceptance record completed without this run disproves acceptance. Acceptance record completed by the user with result `passed`.
4. [x] Run `Set-Location '<SDK_ROOT>'`, then `python -m deswik_pm package verify "<SDK_ROOT>\workflow-packages\phase-3\_TEST_Phase 3 Package" --json` again and confirm `valid: true` and `acceptanceComplete: true`. Copy the whole package to an approved temporary transfer location and verify that copy by passing its package directory to the same command; a missing file, changed hash, incomplete acceptance, or verification that depends on the original directory disproves acceptance. Confirmed for the original and `C:\Users\kevst\Desktop\Work\_TEST_Phase 3 Package` on 2026-09-23.

## Missing inputs

None.

## Open questions

None.
