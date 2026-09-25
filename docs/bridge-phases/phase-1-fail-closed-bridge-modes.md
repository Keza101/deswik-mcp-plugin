## Gate

Phase 1 — yes. The fail-closed response contract was built and proven with the resolved Deswik 2025.2 installation, the local .NET 8.0.425 SDK, deterministic bridge tests, and the required Python regression tests. The user confirmed all four human acceptance checks passed on 2026-09-22.

## Status

complete

## Changed files

`W:\deswik-mcp-plugin\deswik-mcp\src\Deswik.Bridge\models\McpCommand.cs` — adds the request-mode selector and mandatory response mode.

`W:\deswik-mcp-plugin\deswik-mcp\src\Deswik.Bridge.Standalone\TcpBridge.cs` — removes implicit demo fallback and separates live, demo, unsupported, and disconnected routing.

`W:\deswik-mcp-plugin\deswik-mcp\src\Deswik.Bridge.Tests\Deswik.Bridge.Tests.csproj` — adds the offline net8.0 bridge test project.

`W:\deswik-mcp-plugin\deswik-mcp\src\Deswik.Bridge.Tests\Program.cs` — tests every phase-1 routing state and refusal case.

`W:\deswik-mcp-plugin\deswik-mcp\tools\deswik.ps1` — exposes explicit live/demo request selection in the supported client.

`W:\deswik-mcp-plugin\docs\Wire-Protocol.md` — documents request and response mode semantics.

`W:\deswik-mcp-plugin\README.md` — documents fail-closed responses and the test command while preserving the existing local Deswik path edit.

`W:\deswik-mcp-plugin\docs\bridge-phases\phase-1-fail-closed-bridge-modes.md` — records phase evidence and human acceptance steps (relocated after acceptance).

## Behaviour

Live request without an add-in: plausible demo response -> `mode: "disconnected"`, `success: false`, `errorCode: "ADDIN_DISCONNECTED"`, and no data.

Live request with a connected add-in that did not register the action: plausible demo response -> `mode: "unsupported"`, `success: false`, `errorCode: "UNSUPPORTED_ACTION"`, and no data.

Explicit request with top-level `mode: "demo"`: implicit demo selection -> synthetic response with top-level `mode: "demo"`.

Connected add-in response or refusal: response without source identity -> top-level `mode: "live"` with success independent of mode and the add-in error code preserved when supplied.

Invalid request mode: accepted by fallback routing -> `mode: "live"`, `success: false`, `errorCode: "INVALID_MODE"`.

Every response created through `McpResponse.Ok` or `McpResponse.Fail`: omitted `mode` -> mandatory top-level `mode`, defaulting to `live`.

Bridge startup: ambiguous `Demo services initialized.` message -> `Explicit demo handler ready (used only when request mode is 'demo').`

Browser request to raw TCP port: repeated JSON parse errors for every HTTP header -> one explanatory `HTTP/1.1 400 Bad Request`, connection close, and one concise log entry.

## Commands run

```powershell
python W:\AI_Deswik\tests\test_roundtrip.py
```

```text
PASS tag round-trip: 1 nodes, 3 commands, 0 mismatches
PASS node model: <Node "Create layer 'SDK TEST'\nand draw a Deswik Logo" type=RoundedRectangle cmds=3>, MessageBox snippet='Drill-and-blast design layout drawn on t'
PASS edit/save: modified map reloads, 1 nodes intact -> C:\Users\kevst\AppData\Local\Temp\tmprpox7ych\edited.ddf

ALL TESTS PASSED
```

```powershell
python W:\AI_Deswik\tests\test_commands.py
```

```text
PASS SelectEntities/AttributesValidate round-trip
registry: 74 commands, 26 verified

ALL TESTS PASSED
```

```powershell
& 'C:\Users\kevst\.dotnet-sdk-8\dotnet.exe' build 'W:\deswik-mcp-plugin\deswik-mcp\src\Deswik.Addin\Deswik.Addin.csproj' -c Release -p:DeswikDir='C:\Program Files\Deswik\Deswik.Suite 2025.2' -clp:'ErrorsOnly;Summary'
```

```text
Build succeeded.
    11 Warning(s)
    0 Error(s)

Time Elapsed 00:00:03.95
```

```powershell
& 'C:\Users\kevst\.dotnet-sdk-8\dotnet.exe' build 'W:\deswik-mcp-plugin\deswik-mcp\src\Deswik.Bridge.Standalone' -c Release -p:DeswikDir='C:\Program Files\Deswik\Deswik.Suite 2025.2' -clp:'ErrorsOnly;Summary'
```

```text
Build succeeded.
    8 Warning(s)
    0 Error(s)

Time Elapsed 00:00:02.52
```

```powershell
& 'C:\Users\kevst\.dotnet-sdk-8\dotnet.exe' test 'W:\deswik-mcp-plugin\deswik-mcp\src\Deswik.Bridge.Tests\Deswik.Bridge.Tests.csproj' -c Release --no-restore -p:DeswikDir='C:\Program Files\Deswik\Deswik.Suite 2025.2' -v:minimal
```

```text
Deswik.Bridge.Tests -> W:\deswik-mcp-plugin\deswik-mcp\src\Deswik.Bridge.Tests\bin\Release\net8.0\Deswik.Bridge.Tests.dll
PASS response envelope always includes mode
PASS disconnected live request fails without data
PASS connected unregistered action is unsupported
PASS demo requires an explicit request
PASS addin refusal remains a live failure
PASS invalid mode is rejected
PASS HTTP clients receive a clear protocol rejection
ALL TESTS PASSED
```

```powershell
git -C W:\AI_Deswik check-ignore -v docs/bridge-phases/phase-1-fail-closed-bridge-modes.md
```

```text
(no output; exit 1 — expected because the phase write-up is trackable)
```

## Constraint check

Verified Process Map commands only — no Process Map command was generated or changed.

Approved sanitized donor — no production map was generated; existing repository fixtures were read only for the required regression tests and temporary edits stayed under the Windows temporary directory.

XML byte preservation — the byte-identical round-trip regression passed.

Embedded macro API boundary — no macro was added or changed.

DLL API verification — no Deswik API call was added; both C# projects compiled against `C:\Program Files\Deswik\Deswik.Suite 2025.2`.

CAD UI thread — no CAD object-model path was changed.

Loopback only — the listener remains `IPAddress.Loopback`; no outbound call or network listener was added.

Client-data exclusions — no `.ddf`, package, inventory result, client path, or client geometry was written to either repository; the phase did not reach the pre-generation `.gitignore` gate.

No GitHub push — no commit or push was performed.

## Assumptions

`C:\Program Files\Deswik\Deswik.Suite 2025.2` is the resolved build source on this machine; `Deswik.Graphics.dll` reports file version `2025.2.3940.40169`.

The local SDK at `C:\Users\kevst\.dotnet-sdk-8` is .NET SDK `8.0.425` and is used explicitly because the system `DOTNET_ROOT` points at Deswik's runtime-only installation.

The existing Deswik dependency-version warnings are unchanged and remain compatibility work for the later platform-hardening scope; all required builds completed with zero errors.

## Human acceptance

1. [x] Map file: none for Phase 1; node: none; open a non-production blank drawing in Deswik.CAD with no solid or entity selected, start `Deswik.Bridge.Standalone.exe`, leave the add-in unloaded, send a live `get_cad_layers` request, and confirm the response is `mode: "disconnected"`, `success: false`, with no `data`; any coordinates, handles, entities, or demo layer data disproves acceptance. User confirmed passed 2026-09-22.
2. [x] In the same blank drawing, click **Tools -> Plugin Manager**, load `Deswik.Addin.dll`, wait for the bridge panel to show connected, send an action the add-in did not register, and confirm `mode: "unsupported"`, `success: false`, with no `data`; `disconnected`, `demo`, or a success response disproves acceptance. User confirmed passed 2026-09-22.
3. [x] With the add-in still connected, send a valid live read such as `get_cad_layers` and confirm `mode: "live"`; then trigger or observe a rejected/failed live action and confirm it remains `mode: "live"`, `success: false`, with a machine-readable `errorCode`; any source-mode change caused only by failure disproves acceptance. User confirmed passed 2026-09-22.
4. [x] Unload or disconnect the add-in, send `{"id":"phase1-demo","action":"ping","mode":"demo","params":{}}`, and confirm synthetic data is returned with top-level `mode: "demo"`; repeat without `mode: "demo"` and confirm it fails as `disconnected`; demo data from the second request disproves acceptance. Confirmed 2026-09-22: the explicit demo request returned `success: true`, `mode: "demo"`, and `pong`; the request without `mode` returned `success: false`, `mode: "disconnected"`, and `ADDIN_DISCONNECTED`.

## Missing inputs

None.

## Open questions
