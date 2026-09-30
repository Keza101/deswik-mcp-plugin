# Phase 8 — Underground roadmap and final pilot

## Gate

Phase 8 — yes to the full underground roadmap as the selected scope; no to
end-to-end completion in this run. The user chose to finish the roadmap before
the final three-node pilot, approved the tracked synthetic donor, and chose a
synthetic editable profile as the first milestone on 2026-09-29.

## Status

in progress — the synthetic profile editor and read-only selected-design
snapshot each passed four live Deswik.CAD checks reported by the user on
2026-09-29. Temporary operator labels and opt-in polyface metrics also passed
four live CAD checks reported by the user. The bounded raw polyface-geometry
read passed its original four live checks. The user reported the expanded
polyface regression and bounded polyline launcher suite passed on 2026-09-29.
The mixed simple-figure suite is accepted, with native Line explicitly recorded
as an automated-only coverage gap because the installed CAD UI cannot create
one normally. Bounded native Points collection reads are now built and await
their broad live-CAD suite. Verified semantic roles, remaining geometry types,
the wider roadmap, and the final pilot remain open.

## Changed files

- `deswik-mcp/src/Deswik.Ug.Design/` — adds versioned synthetic profile schema, validation, and starter JSON.
- `deswik-mcp/src/Deswik.Ug.Design/SelectionContext.cs` — types selected figures, source handles, and unverified design metadata.
- `deswik-mcp/src/Deswik.Ug.Design/GeometryPages.cs` — validates bounded page requests and types raw polyface/polyline vertices and faces.
- `deswik-mcp/src/Deswik.Ug.Design/FigureGeometry.cs` — types strict raw geometry snapshots for six simple figure types.
- `deswik-mcp/src/Deswik.Addin/ProfileEditorForm.cs` — adds dock-panel profile editing and save-as controls.
- `deswik-mcp/src/Deswik.Addin/ProfileFiles.cs` — writes new profile JSON without overwriting the source.
- `deswik-mcp/src/Deswik.Addin/McpStatusControl.cs` and `Deswik.Addin.csproj` — expose the editor from the existing dock panel.
- `deswik-mcp/src/Deswik.Bridge.Tests/` — checks profile validation, JSON round-trip, and file save-as.
- `deswik-mcp/src/Deswik.Addin/CadReader.cs`, `BridgeClient.cs`, and `DeswikMcpAddin.cs` — capture selection context on the CAD UI thread and expose a read-only action.
- `deswik-mcp/src/Deswik.Bridge.Standalone/BridgeJobs.cs` and `deswik-mcp/src/Deswik.Mcp.Server/McpToolCatalog.cs` — permit the context read through jobs and MCP.
- `.gitignore` — tracks the new shared design project.
- `docs/ug-mining-functionality-roadmap.md` — records the full delivery order and current status.
- `docs/workflow-bridge-status.html` — marks the profile milestone in progress.
- `docs/bridge-phases/phase-8-underground-roadmap.md` — records the milestone and acceptance gate.
- `README.md` and `docs/Wire-Protocol.md` — document the profile editor and context action.
- `Run-Deswik-Tests.cmd` and `deswik-mcp/tools/*Deswik*Test*.ps1` — provide the single reusable root acceptance launcher and guided live suite.

## Behaviour

Before, the repository had no profile model or editor. Now the dock panel has
**UG profiles**, which opens a property editor for the embedded synthetic
starter or a previously saved profile. Users can change ring and deviation
parameters, then **Save as new** into their own
`Documents\DeswikMcp\Profiles` folder. Each save gets a new ID and revision 1;
the starter and existing files are not overwritten. Invalid or non-finite
values, inconsistent spacing, and claims of an approved basis are refused.
The profile model contains no CAD API or I/O; local file storage stays in the
add-in. The values are test-only and cannot yet drive a production write.

The next read-only action, `get_ug_selection_context`, captures the current
drawing path, dirty flag, exact selected handles, CAD figure types, layers,
and available bounds in one UI-thread read. Its schema version is 1. It keeps
mining roles `unclassified`, coordinate system and units `unknown`, and
`readyForDesign: false`. An empty selection or unsaved drawing is reported as
a warning. No CAD writes or **Inspect stope** button are added.

The current follow-up accepts a `roles` map of selected handles to temporary
operator labels and an `includePolyfaceMetrics` switch. The map accepts
decimal handles or `0x`-prefixed hex handles, refuses invalid, duplicate, or
unselected handles, and disappears after the request. Opt-in polyface metrics
use the already compiled `CenterOfGravity`, `Volume`, and `VertexCount` CAD
properties. Unreadable metrics are reported as warnings. Operator labels do
not verify mining roles or change `readyForDesign: false`.

`get_cad_polyface_geometry` reads up to 500 vertices and raw face-index tuples
per request from one polyface. `start` and `nextStart` permit reconstruction
across pages. This uses the `VertexList`, `FaceCount`, and `GetFaceIndexes`
members verified in the installed Deswik 2025.2 assembly. A synthetic triangle
probe confirmed that the API returns four raw face-index values; their sign
and edge-visibility conventions are deliberately not normalized. The response
keeps units and coordinate system unknown and is not design-ready.

`get_cad_polyline_geometry` uses the same bounded page request for one exact
polyline handle. It returns vertex coordinates and the native `Closed` flag.
The existing layer-wide polyline read proves the installed API exposes these
fields, but only live CAD acceptance can verify behavior on real drawings.
No role, units, coordinate system, or design-ready status is inferred.

`get_cad_figure_geometry` now accepts one exact decimal or `0x`-prefixed
handle and returns a schema-versioned snapshot for Line, Circle, Arc, Point,
Text, or MText. The snapshot contains the verified native fields available for
that type plus user attributes read from the figure's layer definitions. It
rejects unknown parameters and unsupported figure types, preserves unknown
coordinate/distance/angle conventions, and can never claim design readiness.

`get_cad_points_geometry` uses the existing strict bounded page request for one
native `Points` collection. It returns up to 500 raw insertion points plus the
native point-cloud flag, point style, size type, align-to-view state and size,
and extrusion vector. It refuses a single `Point`, a `Polyline`, malformed
requests, and unknown handles; units and coordinate system remain unknown and
the response is never design-ready.

The final pilot remains deferred. Current `ProcessMapActionPolicy` permits
only `MCP_READ_DOCUMENT`, and `LoopbackBridgeRequester` sends empty
parameters. The existing `preview_ugdrillholes` action opens the approval
modal during preview, so wiring it to node 2 would not make node 3 the
required human gate. The roadmap must address that boundary later.

## Commands run

From the plugin repository root:

```powershell
git status --branch --short
python -m deswik_pm inspect 'tests\archive_ddf\SDK - Stope Design Layout.ddf' --json
python -m deswik_pm validate 'tests\archive_ddf\SDK - Stope Design Layout.ddf' --json
python tests/test_roundtrip.py
python tests/test_commands.py
$dotnetExe = Join-Path $env:USERPROFILE '.dotnet-sdk-8\dotnet.exe'
$buildOut = Join-Path (Get-Location).Path '.phase8-build\out\'
$env:DESWIK_DIR = 'C:\Program Files\Deswik\Deswik.Suite 2025.2'
$env:DESWIK_MCP_PYTHON = (Get-Command python).Source
& $dotnetExe build 'deswik-mcp\src\Deswik.Ug.Design\Deswik.Ug.Design.csproj' -c Release
& $dotnetExe build 'deswik-mcp\src\Deswik.Addin\Deswik.Addin.csproj' -c Release -p:DeswikDir="$env:DESWIK_DIR" -p:BaseOutputPath="$buildOut"
& $dotnetExe build 'deswik-mcp\src\Deswik.Bridge.Standalone\Deswik.Bridge.Standalone.csproj' -c Release -p:DeswikDir="$env:DESWIK_DIR" -p:BaseOutputPath="$buildOut"
& $dotnetExe build 'deswik-mcp\src\Deswik.Mcp.Server\Deswik.Mcp.Server.csproj' -c Release -p:DeswikDir="$env:DESWIK_DIR" -p:BaseOutputPath="$buildOut"
& $dotnetExe build 'deswik-mcp\src\Deswik.Bridge.Tests\Deswik.Bridge.Tests.csproj' -c Release -p:DeswikDir="$env:DESWIK_DIR" -p:BaseOutputPath="$buildOut"
& '.\.phase8-build\out\Release\net8.0\Deswik.Bridge.Tests.exe'
```

The tracked donor is 27,573 bytes, SHA256
`E703203914900A4997D738F4F2BC8DE14621B94B335A6C8D794E63868429233E`.
It contains one SDK demo node with three verified commands. Its parsed
container has no opaque tail, and the scan found no path or email markers.
Validation returned no warnings; both Python suites printed
`ALL TESTS PASSED`. The design library built with 0 errors; the Addin built
with 0 errors and existing Deswik reference warnings; the C# harness passed
all 35 checks, including profile save-as, selection-context invariants,
operator-role validation, polyface paging, and polyline paging. The editor
form constructor also loaded from the built Addin assembly. These checks do
not replace live Deswik.CAD acceptance.

An isolated bridge process on a temporary loopback port also passed
registration-and-routing checks. A fake add-in received forwarded
`get_ug_selection_context`, `get_cad_polyface_geometry`, and
`get_cad_polyline_geometry` requests; callers
received the add-in responses with `mode: live`. These checks cover transport
routing, not real CAD selection or geometry capture.

For the mixed simple-figure milestone, the two Python regression suites again
printed `ALL TESTS PASSED`. Isolated Release builds of the Addin and bridge-test
harness completed with 0 errors (the existing dependency-version warnings
remain), and the executable harness passed all 35 checks, including strict
simple-figure request parsing, all six type discriminators, finite-value
validation, raw/not-design-ready enforcement, MCP catalogue/routing, and async
job allowlisting. PowerShell parsed the reusable setup, bridge, guided-live-test,
and preflight scripts without errors. A normal output build was deliberately
not forced while the user's accepted CAD session held the loaded DLL; the root
launcher requires CAD to be closed before rebuilding.

For the Points collection milestone, reflection against the installed 2025.2
`Deswik.Graphics.dll` verified the native `Points.InsertionPoints`,
`VertexCount`, point-cloud, style, size, alignment, and extrusion members before
implementation. Isolated Release builds of the Addin and bridge-test harness
completed with 0 errors; the existing dependency-version warnings remain. The
expanded executable harness passed all 36 checks. `test_roundtrip.py` and
`test_commands.py` printed `ALL TESTS PASSED`. The optional Phase 6 TCP smoke
test passed against an isolated bridge after the bridge was started on its
disposable port. All four launcher PowerShell scripts parse without errors.
The launcher now runs both Python suites automatically before the C# harness
and add-in preflight.

## Constraint check

- Use only captured `EmbeddedMacro` and other verified Process Map commands;
  do not generate a `Plugin` or `ExecuteFile` node.
- Preserve the approved donor's appearance XML and binary tail verbatim when
  the final `.ddf` is generated. No `.ddf` was generated in this milestone.
- Keep calculations in a deterministic library outside CAD, with explicit
  input units and a versioned calculation basis.
- Keep all CAD object-model calls on the add-in UI thread and all bridge
  traffic on `127.0.0.1`.
- Do not weaken the preview ownership, modal approval, token, commit, or
  exact-handle rollback rules from Phases 4–7.
- Keep any generated map, package, geometry, and run record in ignored local
  output; do not commit client data or publish without a separate go-ahead.

## Assumptions

- Phase 7 was accepted by the user on 2026-09-29.
- The user approved the tracked SDK fixture as the eventual pilot donor on
  2026-09-29.
- The starter values are synthetic examples, not mine standards; new copies
  retain the same test-only basis.
- The existing Phase 4 guarded-write implementation is the safety base;
  Phase 8 must be tested against a disposable saved drawing before acceptance.

## Current acceptance — synthetic profile editor

The user confirmed checks 1–4 passed on 2026-09-29. The instructions remain
here for repeat acceptance on another installation. Use a disposable
saved drawing; the editor itself does not modify drawing geometry. Close
Deswik.CAD before rebuilding the add-in because its DLL may be locked. Open
PowerShell in the plugin folder and build the Release add-in with the installed
Deswik directory. Copy only the commands inside this block, not the Markdown
fences. If the build fails, stop; the DLL path below will not be a valid result
of this build.

```powershell
$dotnetExe = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnetExe -or -not (& $dotnetExe --list-sdks)) {
  $dotnetExe = Join-Path $env:USERPROFILE '.dotnet-sdk-8\dotnet.exe'
}
if (-not (Test-Path $dotnetExe) -or -not (& $dotnetExe --list-sdks)) { throw 'Install .NET 8 SDK and set $dotnetExe to its dotnet.exe.' }
$deswikDir = Read-Host 'Full path to your Deswik.Suite installation folder'
if (-not (Test-Path (Join-Path $deswikDir 'Deswik.Graphics.dll'))) { throw 'Deswik.Graphics.dll was not found.' }
& $dotnetExe build 'deswik-mcp\src\Deswik.Addin\Deswik.Addin.csproj' -c Release -p:DeswikDir="$deswikDir"
if ($LASTEXITCODE -ne 0) { throw 'Add-in build failed.' }
(Resolve-Path '.\deswik-mcp\src\Deswik.Addin\bin\Release\net8.0-windows\Deswik.Addin.dll').Path
```

Then open Deswik.CAD, load that exact DLL in Plugin Manager with startup class
`DeswikMcpAddin`, and open its dock panel. **UG profiles is the only Phase 8
button in the current build.** The four checks below require human
observation; none is accepted by an SDK or C# pass alone.

1. [x] Click **UG profiles**. Confirm the window says **synthetic only** and
   shows starter ring and deviation values. A missing button, empty editor, or
   production-approved label fails.
2. [x] Change **Name** and one ring parameter (for example `BurdenM` from
   `1.8` to `2.1`), then click **Save as new**. Confirm a new JSON file appears
   under the current user's `Documents\DeswikMcp\Profiles`; opening it shows
   the changed value, `schemaVersion: 1`, `revision: 1`, and
   `basis: synthetic-test-only`. An overwritten starter or approved basis fails.
3. [x] Use **Open saved** to reload that profile. Then click **Load starter**
   and confirm the original value remains `1.8`. A changed starter fails.
4. [x] Try an invalid value such as `MinHoleSeparationM` greater than
   `ToeSpacingM`, then click **Save as new**. Confirm an error appears and no
   new JSON file is created. A saved invalid profile fails.

## Accepted — selected-design snapshot (4/4)

The user reported all four checks passed on 2026-09-29. Deswik's Properties
Report showed selected handles `670` and `67A` in hexadecimal, matching the
snapshot's decimal `1648` and `1658`. The instructions below remain for repeat
acceptance on another installation.

This is a PowerShell/MCP read, **not** an Inspect stope button. Use a saved
disposable drawing. Close Deswik.CAD and stop the old bridge with `Ctrl+C`
before rebuilding; then open PowerShell in the plugin repository folder and
run the commands inside this block:

```powershell
$dotnetExe = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnetExe -or -not (& $dotnetExe --list-sdks)) {
  $dotnetExe = Join-Path $env:USERPROFILE '.dotnet-sdk-8\dotnet.exe'
}
if (-not (Test-Path $dotnetExe) -or -not (& $dotnetExe --list-sdks)) { throw 'Install .NET 8 SDK and set $dotnetExe to its dotnet.exe.' }
$deswikDir = Read-Host 'Full path to your Deswik.Suite installation folder'
if (-not (Test-Path (Join-Path $deswikDir 'Deswik.Graphics.dll'))) { throw 'Deswik.Graphics.dll was not found.' }
& $dotnetExe build 'deswik-mcp\src\Deswik.Addin\Deswik.Addin.csproj' -c Release -p:DeswikDir="$deswikDir"
if ($LASTEXITCODE -ne 0) { throw 'Add-in build failed.' }
& $dotnetExe build 'deswik-mcp\src\Deswik.Bridge.Standalone\Deswik.Bridge.Standalone.csproj' -c Release -p:DeswikDir="$deswikDir"
if ($LASTEXITCODE -ne 0) { throw 'Bridge build failed.' }
```

In that PowerShell window, start the bridge with the command below and leave
it running. In Deswik.CAD Plugin Manager load the newly built
`deswik-mcp\src\Deswik.Addin\bin\Release\net8.0-windows\Deswik.Addin.dll`
with startup class `DeswikMcpAddin`. Open the disposable drawing.

```powershell
$env:DESWIK_MCP_PYTHON = (Get-Command python).Source
& '.\deswik-mcp\src\Deswik.Bridge.Standalone\bin\Release\net8.0\Deswik.Bridge.Standalone.exe'
```

Open **another** PowerShell window in the same plugin folder. Copy this
single line to load the client, then run the checks below:

```powershell
. .\deswik-mcp\tools\deswik.ps1
```

1. [x] With nothing selected in CAD, run
   `dsw get_ug_selection_context | ConvertTo-Json -Depth 8`. Confirm
   `sourceHandles` is empty, `readyForDesign` is `false`, and a warning says
   no CAD figures are selected. Synthetic data or a design-ready result fails.
2. [x] Select one or more figures in CAD and run the commands below. Confirm
   the `sourceHandles` list exactly matches the current selection handles;
   `Compare-Object` must print nothing. The Deswik Properties Report displays
   handles in hexadecimal while the bridge prints decimal: `670` in Deswik is
   `1648` in the bridge, and `67A` is `1658`. The last command prints the
   bridge handles in Deswik's hexadecimal format. A missing or extra handle
   fails.

   ```powershell
   $context = dsw get_ug_selection_context
   $selection = dsw get_cad_selection
   $expected = @($selection.figures | ForEach-Object { [uint64]$_.handle } | Sort-Object -Unique)
   $actual = @($context.sourceHandles | ForEach-Object { [uint64]$_ })
   Compare-Object $expected $actual
   $actual | ForEach-Object { '{0:X}' -f $_ }
   ```

3. [x] Inspect `$context | ConvertTo-Json -Depth 8`. Confirm
   `schemaVersion` is `1`, `drawingPath` names the saved drawing, each figure
   has `role: unclassified`, `coordinateSystem` and `units` are `unknown`,
   and `readyForDesign` is `false`. An inferred stope role or design-ready
   result fails.
4. [x] Compare layer entity counts before and after one more context read.
   The final command must print `True`; changed geometry fails.

   ```powershell
   $before = dsw get_cad_layers | Sort-Object name | Select-Object name,entityCount | ConvertTo-Json -Compress
   $null = dsw get_ug_selection_context
   $after = dsw get_cad_layers | Sort-Object name | Select-Object name,entityCount | ConvertTo-Json -Compress
   $before -eq $after
   ```

## Accepted — temporary roles and polyface metrics (4/4)

The user reported all four checks passed on 2026-09-29. The steps remain for
repeat acceptance on another installation.

This extends the same **PowerShell read**, not the Process Map. Close Deswik.CAD
and stop the bridge with `Ctrl+C`. From the plugin repository folder, repeat
the .NET SDK detection, Deswik path prompt, Addin build, and bridge start in
the preceding section. Reload the newly built Addin DLL in Deswik.CAD and
open the saved disposable drawing. In a second PowerShell window, run this
line before using `dsw`:

```powershell
. .\deswik-mcp\tools\deswik.ps1
```

1. [x] Select at least two figures, including a polyface. Use the commands
   below. Confirm the first selected handle has `role: stope` and
   `roleSource: operator`; any other selected figure stays `unclassified`
   with `roleSource: none`. This is a temporary test label, not a claim that
   the figure is an approved stope. A changed or missing handle fails.

   ```powershell
   $base = dsw get_ug_selection_context
   if (@($base.sourceHandles).Count -lt 2) { throw 'Select at least two figures first.' }
   $hexHandle = '0x{0:X}' -f [uint64]$base.sourceHandles[0]
   $roles = @{}
   $roles[$hexHandle] = 'stope'
   $context = dsw get_ug_selection_context @{ roles=$roles; includePolyfaceMetrics=$true }
   $context.figures | Select-Object handle,type,role,roleSource | Format-Table
   ```

2. [x] Run `$context.figures | Where-Object type -eq 'Polyface' |
   Select-Object handle,polyfaceMetrics | Format-List`. Confirm each readable
   selected polyface has numeric `centerOfGravity` coordinates, finite
   `volume`, and a positive `vertexCount`. A missing value or a warning that
   metrics are unavailable for a readable polyface fails.
3. [x] Run the following refusal check. Confirm `success: False` and
   `errorCode: invalid_context_request`; an unselected handle must not be
   accepted.

   ```powershell
   dsw get_ug_selection_context @{ roles=@{ '0xFFFFFFFFFFFFFFFF'='stope' } } -RawResponse | Format-List
   ```

4. [x] Run `dsw get_ug_selection_context | ConvertTo-Json -Depth 8` again
   without options. Confirm every `role` is `unclassified`, every
   `roleSource` is `none`, `polyfaceMetrics` is null, and `readyForDesign` is
   `false`. A persisted label, inferred role, or design-ready result fails.

## Accepted — paged polyface geometry (original 4/4)

The user reported all four checks below passed on 2026-09-29. They remain as
the original smoke test. The larger regression batch below is still pending.

This is a **read-only PowerShell/MCP action**, not a Process Map button. Close
Deswik.CAD and stop the old bridge with `Ctrl+C`. In PowerShell, open the plugin
repository folder, then run only the commands inside this block. Enter your
own Deswik.Suite installation folder when prompted:

```powershell
$dotnetExe = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnetExe -or -not (& $dotnetExe --list-sdks)) {
  $dotnetExe = Join-Path $env:USERPROFILE '.dotnet-sdk-8\dotnet.exe'
}
if (-not (Test-Path $dotnetExe) -or -not (& $dotnetExe --list-sdks)) { throw 'Install .NET 8 SDK and set $dotnetExe to its dotnet.exe.' }
$deswikDir = Read-Host 'Full path to your Deswik.Suite installation folder'
if (-not (Test-Path (Join-Path $deswikDir 'Deswik.Graphics.dll'))) { throw 'Deswik.Graphics.dll was not found.' }
& $dotnetExe build 'deswik-mcp\src\Deswik.Addin\Deswik.Addin.csproj' -c Release -p:DeswikDir="$deswikDir"
if ($LASTEXITCODE -ne 0) { throw 'Add-in build failed.' }
& $dotnetExe build 'deswik-mcp\src\Deswik.Bridge.Standalone\Deswik.Bridge.Standalone.csproj' -c Release -p:DeswikDir="$deswikDir"
if ($LASTEXITCODE -ne 0) { throw 'Bridge build failed.' }
```

In that window, start the bridge and leave it running:

```powershell
$env:DESWIK_MCP_PYTHON = (Get-Command python).Source
& '.\deswik-mcp\src\Deswik.Bridge.Standalone\bin\Release\net8.0\Deswik.Bridge.Standalone.exe'
```

Open Deswik.CAD and load the newly built
`deswik-mcp\src\Deswik.Addin\bin\Release\net8.0-windows\Deswik.Addin.dll`
in Plugin Manager with startup class `DeswikMcpAddin`. Open a saved disposable
drawing that has at least one polyface. Open **another** PowerShell window in
the plugin folder and load `dsw`:

```powershell
. .\deswik-mcp\tools\deswik.ps1
```

1. [x] Select a polyface in CAD. Run the commands below. Confirm `handle`
   matches the selected polyface, `schemaVersion` is `1`, and
   `readyForDesign` is `false`. An unsupported action or a different handle
   fails.

   ```powershell
   $figure = (dsw get_ug_selection_context).figures | Where-Object type -eq 'Polyface' | Select-Object -First 1
   if (-not $figure) { throw 'Select a polyface in CAD first.' }
   $handle = [uint64]$figure.handle
   $page = dsw get_cad_polyface_geometry @{ handle=$handle; start=0; limit=25 }
   $page | Select-Object schemaVersion,handle,vertexCount,faceCount,start,limit,nextStart,readyForDesign | Format-List
   ```

2. [x] Run the checks below. Both comparisons must print `True` and sample
   vertices must have numeric `x`, `y`, `z` coordinates. Faces must contain
   four integer fields `a`, `b`, `c`, `d`; **do not interpret their signs** as
   geometry validity yet.

   ```powershell
   @($page.vertices).Count -eq [Math]::Min(25, [int]$page.vertexCount)
   @($page.faces).Count -eq [Math]::Min(25, [int]$page.faceCount)
   $page.vertices | Select-Object -First 3 | Format-List
   $page.faces | Select-Object -First 3 | Format-Table
   ```

3. [x] Fetch a one-entry page. If `nextStart` is not null, fetch the next
   page and confirm `start` advances to `1`. A repeated page or missing
   available page fails.

   ```powershell
   $first = dsw get_cad_polyface_geometry @{ handle=$handle; start=0; limit=1 }
   if ($null -ne $first.nextStart) {
     $second = dsw get_cad_polyface_geometry @{ handle=$handle; start=$first.nextStart; limit=1 }
     $second.start -eq 1
   }
   ```

4. [x] Send an invalid page limit. Confirm `success: False` and
   `errorCode: invalid_geometry_request`. Confirm the layer entity counts are
   unchanged before and after the read; any geometry change fails.

   ```powershell
   $before = dsw get_cad_layers | Sort-Object name | Select-Object name,entityCount | ConvertTo-Json -Compress
   dsw get_cad_polyface_geometry @{ handle=$handle; limit=501 } -RawResponse | Select-Object success,errorCode
   $after = dsw get_cad_layers | Sort-Object name | Select-Object name,entityCount | ConvertTo-Json -Compress
   $before -eq $after
   ```

## Accepted — combined geometry session

The user reported on 2026-09-29 that every check printed by the launcher passed,
including the manual CAD comparison. The exact test intent and manual fallback
remain archived below. The launcher has since been renamed and advanced to the
next milestone; use `Run-Deswik-Tests.cmd` for the current suite.

This batch checks the polyface edge cases and the new polyline action in **one
CAD/bridge run**. You do not need to restart the server between checks. The
earlier four polyface checks remain accepted; this is extra coverage because
geometry paging needs more than four live checks. Use a disposable saved
drawing with a polyface containing **more than 500 vertices** and at least one
polyline. If none exists, draw one in this disposable drawing using CAD's
normal polyline tool and save the drawing before starting the read checks.
Know whether the polyline is open or closed from CAD's Properties
panel. None of these commands writes drawing geometry.

1. [x] **Start once.** Close Deswik.CAD and stop any old bridge with `Ctrl+C`.
   Double-click `Run-Deswik-Tests.cmd` in the plugin repository root.
   It runs the Release builds, deterministic harness, and add-in preflight;
   starts the bridge in its own window; then opens an interactive test window.
   Follow that window's prompts to load the newly built Addin DLL, open the
   disposable drawing, clear the selection, and then select the fixtures. The
   launcher performs checks 2–4 below and prints the final failure count.

   For a manual fallback, run the build commands at the start of the preceding
   accepted section **once**, start the bridge there, and load the newly built
   Addin DLL in Deswik.CAD. In a second PowerShell window in the repository run:

   ```powershell
   . .\deswik-mcp\tools\deswik.ps1
   ```

2. [x] **Empty selection and missing handle.** Clear the CAD selection (press
   `Esc` until no figures are highlighted). In the second PowerShell window,
   run the block below. It must print two green `PASS` lines. An empty
   selection must remain empty, and a geometry request with no handle must
   fail with `invalid_geometry_request`.

   ```powershell
   $empty = dsw get_ug_selection_context
   if (@($empty.sourceHandles).Count -eq 0) { Write-Host 'PASS empty selection' -ForegroundColor Green } else { Write-Host 'FAIL empty selection' -ForegroundColor Red }
   $missing = dsw get_cad_polyface_geometry @{} -RawResponse
   if (-not $missing.success -and $missing.errorCode -eq 'invalid_geometry_request') { Write-Host 'PASS missing handle refused' -ForegroundColor Green } else { Write-Host 'FAIL missing handle' -ForegroundColor Red }
   ```

3. [x] **Select the fixtures once.** In CAD select the large polyface and one
   polyline together. Do not restart either application. Run this single block
   in the second PowerShell window. It prints a coloured result for every
   check, including first/middle/final/beyond-end pages, a 500-entry mesh
   page, repeated reads, both wrong-type handles, invalid parameters, and
   unchanged layer counts. A red line fails acceptance; yellow means the
   selected polyface is too small for the large-mesh check and you must use a
   larger disposable mesh. The final line reports the number of failures.

   ```powershell
   $script:geometryFailures = 0
   function Test-Geometry([string]$Name, [bool]$Pass) {
     if ($Pass) { Write-Host "PASS $Name" -ForegroundColor Green }
     else { Write-Host "FAIL $Name" -ForegroundColor Red; $script:geometryFailures++ }
   }
   $context = dsw get_ug_selection_context
   $pf = @($context.figures | Where-Object type -eq 'Polyface')[0]
   $pl = @($context.figures | Where-Object type -eq 'Polyline')[0]
   if (-not $pf -or -not $pl) { throw 'Select one polyface and one polyline in CAD, then rerun this block.' }
   $pfHandle = [uint64]$pf.handle
   $plHandle = [uint64]$pl.handle
   $before = dsw get_cad_layers | Sort-Object name | Select-Object name,entityCount | ConvertTo-Json -Compress

   $p0 = dsw get_cad_polyface_geometry @{ handle=$pfHandle; start=0; limit=1 }
   $p1 = dsw get_cad_polyface_geometry @{ handle=$pfHandle; start=1; limit=1 }
   $pTotal = [Math]::Max([int]$p0.vertexCount,[int]$p0.faceCount)
   $pLast = dsw get_cad_polyface_geometry @{ handle=$pfHandle; start=($pTotal-1); limit=25 }
   $pBeyond = dsw get_cad_polyface_geometry @{ handle=$pfHandle; start=($pTotal+1); limit=25 }
   $pWide = dsw get_cad_polyface_geometry @{ handle=$pfHandle; start=0; limit=500 }
   $pAgain = dsw get_cad_polyface_geometry @{ handle=$pfHandle; start=0; limit=1 }
   Test-Geometry 'polyface exact handle and raw status' ($p0.handle -eq $pfHandle -and $p0.readyForDesign -eq $false -and $p0.units -eq 'unknown' -and $p0.coordinateSystem -eq 'unknown')
   Test-Geometry 'polyface first vertex and face present' (@($p0.vertices).Count -eq 1 -and @($p0.faces).Count -eq 1 -and $null -ne $p0.vertices[0].x -and $null -ne $p0.faces[0].a)
   Test-Geometry 'polyface first to middle page' ($p0.nextStart -eq 1 -and $p1.start -eq 1)
   Test-Geometry 'polyface final page' ($null -eq $pLast.nextStart)
   Test-Geometry 'polyface beyond end empty' (@($pBeyond.vertices).Count -eq 0 -and @($pBeyond.faces).Count -eq 0 -and $null -eq $pBeyond.nextStart)
   if ($p0.vertexCount -le 500) { Write-Host 'SKIP large mesh: select a polyface with more than 500 vertices' -ForegroundColor Yellow; $script:geometryFailures++ }
   else { Test-Geometry 'large mesh bounded at 500' (@($pWide.vertices).Count -eq 500 -and @($pWide.faces).Count -eq [Math]::Min(500,[int]$pWide.faceCount) -and $pWide.nextStart -eq 500) }
   Test-Geometry 'polyface repeated page stable' (($p0 | ConvertTo-Json -Depth 8 -Compress) -eq ($pAgain | ConvertTo-Json -Depth 8 -Compress))
   $badLimit = dsw get_cad_polyface_geometry @{ handle=$pfHandle; limit=501 } -RawResponse
   $badStart = dsw get_cad_polyface_geometry @{ handle=$pfHandle; start=-1 } -RawResponse
   $wrongPf = dsw get_cad_polyface_geometry @{ handle=$plHandle } -RawResponse
   Test-Geometry 'polyface invalid limit and start refused' (-not $badLimit.success -and -not $badStart.success)
   Test-Geometry 'non-polyface handle refused' (-not $wrongPf.success)

   $l0 = dsw get_cad_polyline_geometry @{ handle=$plHandle; start=0; limit=1 }
   $l1 = dsw get_cad_polyline_geometry @{ handle=$plHandle; start=1; limit=1 }
   $lLast = dsw get_cad_polyline_geometry @{ handle=$plHandle; start=([int]$l0.vertexCount-1); limit=25 }
   $lBeyond = dsw get_cad_polyline_geometry @{ handle=$plHandle; start=([int]$l0.vertexCount+1); limit=25 }
   $lAgain = dsw get_cad_polyline_geometry @{ handle=$plHandle; start=0; limit=1 }
   Test-Geometry 'polyline exact handle and raw status' ($l0.handle -eq $plHandle -and $l0.readyForDesign -eq $false -and $l0.units -eq 'unknown' -and $l0.coordinateSystem -eq 'unknown')
   Test-Geometry 'polyline first vertex present' (@($l0.vertices).Count -eq 1 -and $null -ne $l0.vertices[0].x -and $null -ne $l0.vertices[0].y -and $null -ne $l0.vertices[0].z)
   Test-Geometry 'polyline first to middle page' ($l0.nextStart -eq 1 -and $l1.start -eq 1)
   Test-Geometry 'polyline final page' ($null -eq $lLast.nextStart)
   Test-Geometry 'polyline beyond end empty' (@($lBeyond.vertices).Count -eq 0 -and $null -eq $lBeyond.nextStart)
   Test-Geometry 'polyline repeated page stable' (($l0 | ConvertTo-Json -Depth 8 -Compress) -eq ($lAgain | ConvertTo-Json -Depth 8 -Compress))
   $wrongPl = dsw get_cad_polyline_geometry @{ handle=$pfHandle } -RawResponse
   $badPl = dsw get_cad_polyline_geometry @{ handle=$plHandle; limit=501 } -RawResponse
   $missingPl = dsw get_cad_polyline_geometry @{} -RawResponse
   Test-Geometry 'non-polyline handle refused' (-not $wrongPl.success)
   Test-Geometry 'polyline invalid and missing parameters refused' (-not $badPl.success -and -not $missingPl.success)
   $after = dsw get_cad_layers | Sort-Object name | Select-Object name,entityCount | ConvertTo-Json -Compress
   Test-Geometry 'drawing entity counts unchanged' ($before -eq $after)
   Write-Host "Failures: $script:geometryFailures" -ForegroundColor $(if ($script:geometryFailures) { 'Red' } else { 'Green' })
   $l0 | Select-Object handle,vertexCount,closed,coordinateSystem,units | Format-List
   ```

4. [x] **Compare with CAD.** Confirm the printed polyline `closed` value
   matches CAD Properties and the returned first vertex coordinates match
   the selected polyline. Confirm the drawing's dirty/modified indicator did
   not change during the reads. These two visual checks cannot be proven by
   the bridge alone. Report the failure count and any red or yellow lines.

## Accepted — mixed simple-figure geometry

The user reported on 2026-09-29 that the updated suite passed with zero
failures, including the CAD Properties comparison and no-mutation checks. The
native Line case was not available from the installed Deswik UI and remains the
documented automated-only coverage gap. The launcher has now advanced to the
Points collection milestone.

This milestone adds `get_cad_figure_geometry`, a strict read-only action for
`Line`, `Circle`, `Arc`, `Point`, `Text`, and `MText`. It returns native geometry
fields and user attributes with `coordinateSystem`, `units`, and `angleUnits`
left `unknown`, and `readyForDesign: false`. It deliberately refuses other
figure types, missing/zero/unknown handles, and extra parameters.

Close Deswik.CAD and any old bridge, then double-click the same reusable root
launcher:

```powershell
.\Run-Deswik-Tests.cmd
```

Use a saved disposable drawing containing one known Circle, Arc, Point, Text,
MText, and Polyline. A native Line is optional because the user's installed
Deswik UI does not expose a normal Line-creation tool; do not substitute a
two-vertex Polyline. The guided console waits until all six required fixture
types are selected together and also tests a native Line if one is available.
It checks strict invalid-input refusals; every available supported
discriminator; handle, GUID, and layer identity; required native fields; finite
values; decimal/hex equivalence; user-attribute parity; wrong-reader and
wrong-type refusals; 25 stable repeated reads per simple fixture; unchanged
selection, entity counts, and dirty state; and a visual comparison against CAD
Properties. This produces dozens of named assertions plus at least 125 stress
reads before the manual comparison. The Line parser and geometry contract are
still covered by the deterministic C# harness; live Line coverage remains an
explicit evidence gap until a representative native entity is available.

1. [x] Setup reports all deterministic and add-in preflight checks passed and
   opens the bridge and guided test windows.
2. [x] Empty-selection and all strict invalid-input refusal checks print green
   `PASS` lines with no unexpected error.
3. [x] Every assertion for Circle, Arc, Point, Text, and MText prints green,
   including 25 stable repeated reads per fixture and attribute parity. If a
   native Line is selected, its equivalent assertions also pass; otherwise the
   console prints the documented yellow coverage note.
4. [x] Wrong-type readers refuse every fixture, the selected Polyline is
   refused by the simple-figure reader, and no refusal returns geometry data.
5. [x] Selection handles, layer entity counts, and dirty state remain unchanged.
6. [x] CAD Properties agree with every fixture's native values and the final
   summary reports zero failures. Any mismatch fails acceptance.

## Current acceptance — native Points collection paging (pending)

The same root launcher now tests `get_cad_points_geometry`. Prepare a saved
disposable drawing with one native `Points` collection containing 3–20 known
points, one separate single `Point`, and one `Polyline`. The collection must be
one entity; several separate Point entities are not equivalent.

Double-click `Run-Deswik-Tests.cmd`. The launcher rebuilds and runs all
deterministic checks, starts the bridge, and opens the guided live-test window.
The suite performs strict malformed/missing/unknown-handle refusals; first,
second, final, beyond-end, and maximum page checks; decimal/hex equivalence;
finite coordinate and metadata checks; cross-reader and wrong-type refusals;
50 stable full-page rereads; unchanged selection, layer counts, and dirty state;
and a manual comparison of count, coordinates, and display metadata.

1. [ ] Setup and all deterministic/preflight checks pass, then both live windows
   open from the single root launcher.
2. [ ] Empty selection remains empty and all eight invalid-request cases print
   green `PASS` lines.
3. [ ] Select exactly one 3–20-entry `Points` collection, one `Point`, and one
   `Polyline`; the console identifies all three native types.
4. [ ] Every page boundary, raw-status, finite-value, metadata, and decimal/hex
   equivalence check passes.
5. [ ] The full page remains byte-for-byte stable across 50 repeated reads.
6. [ ] Single Point and Polyline are refused by the Points reader; the Points
   collection is refused by the Polyline and simple-figure readers.
7. [ ] Selection handles, layer entity counts, and drawing dirty state remain
   unchanged.
8. [ ] CAD Properties agree with point count, coordinates, point style, size
   type, and align-to-view values; final failure count is zero.

## Future pilot specification — not runnable yet

**Inspect stope**, **Preview rings**, and **Approve write** are planned Process
Map nodes, not buttons in the current dock panel or current acceptance tests.
Do not look for them or mark their checks as failed in this build. The Phase 2
sidecar map shown in Deswik.CAD is an older test map, not the Phase 8 pilot.
After the full underground roadmap is built, the generated pilot map will need
a separate release and acceptance guide. Its checks will cover exact selected
source handles and drawing identity, owned temporary preview geometry with
calculation provenance, default-No operator approval and exact created handles,
and refusal to approve without a matching preview.

The pilot is governed by gates `G7`–`G9` in
`docs/ug-mining-functionality-roadmap.md`. Passing it produces an evidence pack,
not production authorization. Production use remains blocked until controlled
readiness (`G10`), limited rollout (`G11`), and scope-specific expansion/change
control (`G12`) are separately completed. Any pilot defect is returned to the
gate that owns the failed contract rather than waived at pilot closeout.

## Missing inputs

No required source file was absent. No approved production mine-standard
profile has been supplied; the current milestone deliberately uses only a
synthetic test profile.

## Open questions

No decision remains for the first profile milestone. Later production design
work requires a reviewed site profile and real provider contracts; synthetic
values must not be promoted to mine standards by inference.
