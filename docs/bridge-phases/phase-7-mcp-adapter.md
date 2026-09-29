# Phase 7 — MCP adapter

## Gate

Phase 7 — yes. Phases 1–6 were accepted, and the MCP adapter Definition of
Done was proven on this machine. The user reported Phase 7 passed on
2026-09-29, covering the four live Deswik/MCP checks below.

## Status

accepted — typed stdio server, exact tool catalogue, Release builds, real
stdio/TCP round-trip, regression tests, abuse tests, documentation, and the
human acceptance walkthrough are complete. The user reported all four live
acceptance checks passed on 2026-09-29; the CAD run was not independently
observed in this session.

## Changed files

Paths below are relative to the folder containing this file's `README.md`.

- `deswik-mcp/src/Deswik.Mcp.Server/Deswik.Mcp.Server.csproj` — adds the dependency-free .NET 8 stdio executable.
- `deswik-mcp/src/Deswik.Mcp.Server/McpToolCatalog.cs` — defines the exact 20-tool catalogue and token gate.
- `deswik-mcp/src/Deswik.Mcp.Server/McpAdapter.cs` — implements JSON-RPC lifecycle, tool calls, refusals, and loopback forwarding.
- `deswik-mcp/src/Deswik.Mcp.Server/Program.cs` — hosts newline-delimited MCP over stdin/stdout.
- `deswik-mcp/src/Deswik.Bridge.Tests/Deswik.Bridge.Tests.csproj` — builds the MCP project into the existing offline test harness.
- `deswik-mcp/src/Deswik.Bridge.Tests/Program.cs` — tests the catalogue, refusals, direct adapter, and real subprocess stdio/TCP path.
- `.gitignore` — tracks the new MCP source project.
- `README.md` — documents build, architecture, client configuration, and safety boundary.
- `docs/Wire-Protocol.md` — documents MCP versions, tools, result shape, and excluded actions.
- `docs/workflow-bridge-status.html` — records Phase 7 acceptance and Phase 8 as next.
- `docs/bridge-phases/phase-7-mcp-adapter.md` — records this gate and its operator checklist.

## Behaviour

Before, port 9595 was only a custom newline-delimited TCP protocol and there
was no MCP process. After, `Deswik.Mcp.Server.exe` is a local stdio JSON-RPC
server with `initialize`, `ping`, `tools/list`, and `tools/call`. It negotiates
the project-required `2025-11-25` lifecycle and the compatible `2025-06-18`
and `2025-03-26` versions.

`tools/list` is deterministic and contains exactly 20 typed tools: nine CAD
reads, five guarded-write workflow tools, three bridge job tools, and three
read-only Process Map tools. It excludes demo mode, internal actions, all six
legacy writers, and the file-producing `map.generate` and `map.install`
actions.

Every tool call uses `mode: live` over `127.0.0.1`. An unknown tool is refused
before a TCP connection is opened. Direct commit/rollback and nested async
commit/rollback calls are refused before the bridge unless a non-empty token
is supplied. The adapter cannot mint a token; only the existing add-in modal
approval path can cause the bridge to mint one. Bridge envelopes are preserved
in `structuredContent`; bridge failures become `isError: true` without losing
their `mode` or `errorCode`.

The root `.phase6-build` directory was not moved into `docs/bridge-phases`.
It contains about 2 MB of generated binaries, NuGet intermediates, and logs,
not a phase record. It remains ignored build output; the Phase 6 record is
already in `docs/bridge-phases/phase-6-async-jobs.md`.

## Commands run

The required `rtk` wrapper was not installed or on `PATH`, so ordinary
PowerShell commands were used.

```powershell
$pythonExe='C:\Program Files\LibreOffice\program\python.exe'
& $pythonExe 'tests\test_roundtrip.py'
& $pythonExe 'tests\test_commands.py'
```

Both ended with:

```text
ALL TESTS PASSED
```

The first sandboxed attempt was correctly stopped by `PermissionError` when
the existing install-refusal regression tried to create its normal audit log
under `C:\Users\kevst\AppData\Local\Deswik\Logs`. The same commands were
rerun with approved filesystem access and passed. No workflow was installed.

```powershell
$env:DESWIK_MCP_PYTHON='C:\Program Files\LibreOffice\program\python.exe'
$deswikDir='C:\Program Files\Deswik\Deswik.Suite 2025.2'
dotnet build 'deswik-mcp\src\Deswik.Addin\Deswik.Addin.csproj' -c Release -p:DeswikDir="$deswikDir" -v:q -clp:ErrorsOnly
dotnet build 'deswik-mcp\src\Deswik.Bridge.Standalone\Deswik.Bridge.Standalone.csproj' -c Release -p:DeswikDir="$deswikDir" -v:q -clp:ErrorsOnly
dotnet build 'deswik-mcp\src\Deswik.Mcp.Server\Deswik.Mcp.Server.csproj' -c Release -v:q -clp:ErrorsOnly
dotnet test 'deswik-mcp\src\Deswik.Bridge.Tests\Deswik.Bridge.Tests.csproj' -c Release -p:DeswikDir="$deswikDir" -v:q -clp:ErrorsOnly
```

The three builds ended with `Build succeeded` and `0 Error(s)`. The add-in and
bridge retained their existing Deswik reference-version warnings. The MCP
server produced zero warnings. `dotnet test` exited 0.

```powershell
$env:DESWIK_MCP_PYTHON='C:\Program Files\LibreOffice\program\python.exe'
& '.\deswik-mcp\src\Deswik.Bridge.Tests\bin\Release\net8.0\Deswik.Bridge.Tests.exe'
```

```text
PASS response envelope always includes mode
PASS disconnected live request fails without data
PASS connected unregistered action is unsupported
PASS demo requires an explicit request
PASS addin refusal remains a live failure
PASS invalid mode is rejected
PASS HTTP clients receive a clear protocol rejection
PASS Process Map action allowlist is exact
PASS Process Map sidecar integrity pin is enforced
PASS Process Map inspect returns file provenance
PASS all legacy CAD writers are fenced
PASS preview layer ownership detects foreign handles
PASS guarded writes reject source handle additions and removals
PASS Process Map actions use an exact allowlist
PASS write tokens are opaque bound and single use
PASS write tokens expire
PASS release bridge has one token mint call site
PASS job actions use an exact allowlist
PASS cancel before commit dispatch writes nothing
PASS cancel during a 40-hole commit preserves its complete result
PASS partial writes report every surviving handle
PASS expired jobs cancel and restarted registries know no prior IDs
PASS expired in-flight write never reports cancelled
PASS MCP tool catalogue is exact and typed
PASS MCP refuses unknown and tokenless write tools
PASS MCP rejects malformed JSON-RPC without exiting
PASS MCP read tool round-trips the live bridge envelope
PASS MCP stdio server completes initialize list call and refusals
ALL TESTS PASSED
```

The last test launches the built MCP executable, speaks through its actual
stdin/stdout, and connects it to a fake read-only loopback bridge on a
disposable port. Trace mode printed the raw frames. The full `tools/list`
response contained the 20 schemas; its exact ordered names were
`get_cad_document`, `get_cad_layers`, `get_cad_layer_attributes`,
`get_cad_elements`, `get_cad_selection`, `get_cad_polyface_info`,
`get_cad_polylines_under`, `get_cad_blasthole_details`,
`get_cad_ugdrillhole_details`, `preview_ugdrillholes`,
`get_write_approval`, `commit_ugdrillholes`,
`prepare_rollback_ugdrillholes`, `rollback_ugdrillholes`, `job.submit`,
`job.get`, `job.cancel`, `map.inspect`, `map.validate`, and `map.inventory`.
The non-list frames were:

```text
MCP> {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"stdio-test","version":"1"}}}
MCP< {"jsonrpc":"2.0","id":1,"result":{"protocolVersion":"2025-11-25","capabilities":{"tools":{"listChanged":false}},"serverInfo":{"name":"deswik-workflow-bridge","version":"1.0.0"},"instructions":"Live Deswik tools fail closed. Production CAD writes require a token minted after in-CAD approval."}}
MCP> {"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"draw_cad_ugdrillholes","arguments":{}}}
MCP< {"jsonrpc":"2.0","id":3,"error":{"code":-32602,"message":"Tool is not approved: draw_cad_ugdrillholes"}}
MCP> {"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"commit_ugdrillholes","arguments":{}}}
MCP< {"jsonrpc":"2.0","id":4,"result":{"content":[{"type":"text","text":"{\u0022mode\u0022:\u0022live\u0022,\u0022success\u0022:false,\u0022error\u0022:\u0022A non-empty human-minted token is required\u0022,\u0022errorCode\u0022:\u0022human_token_required\u0022}"}],"structuredContent":{"mode":"live","success":false,"error":"A non-empty human-minted token is required","errorCode":"human_token_required"},"isError":true}}
MCP> {"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"get_cad_document","arguments":{}}}
MCP< {"jsonrpc":"2.0","id":5,"result":{"content":[{"type":"text","text":"{\r\n  \u0022id\u0022: \u0022<generated-request-id>\u0022,\r\n  \u0022mode\u0022: \u0022live\u0022,\r\n  \u0022success\u0022: true,\r\n  \u0022data\u0022: {\r\n    \u0022DocumentName\u0022: \u0022Disposable Test Drawing\u0022\r\n  }\r\n}"}],"structuredContent":{"id":"<generated-request-id>","mode":"live","success":true,"data":{"DocumentName":"Disposable Test Drawing"}},"isError":false}}
```

The generated request ID is shown as a placeholder in the write-up to avoid
pretending it is stable; the terminal trace contained its actual UUID.

Windows PowerShell 5.1 compatibility was rechecked on 2026-09-29. Its
`ConvertFrom-Json` has no `-Depth` parameter, and its redirected stdin writes
a UTF-8 BOM before the first frame. The client block below now uses plain
`ConvertFrom-Json`; the MCP host removes a BOM only at the start of its first
input line. The executable C# harness passed all 28 checks with a BOM-bearing
stdio client, and a Windows PowerShell 5.1 run returned protocol `2025-11-25`,
exactly 20 tools, and `inputSchema` on every tool. The normal Release MCP
server was rebuilt with zero warnings and zero errors. A server process started
before this correction must be closed and restarted before continuing.

```powershell
git check-ignore -v 'docs\bridge-phases\phase-7-mcp-adapter.md'
```

Exit code 1 with no output is expected: the phase record is not ignored.

## Constraint check

- No Process Map command, donor, appearance XML, macro, or opaque payload was created or changed.
- No client map, drawing, geometry, coordinates, inventory output, Deswik binary, or external content was committed.
- The adapter listens on no socket and makes no outbound request; it connects only to `127.0.0.1`.
- All CAD calls still execute in the add-in on the Deswik UI thread.
- The MCP tool list is an exact allowlist and exposes no internal action, legacy writer, demo route, `map.generate`, or `map.install`.
- Production CAD writes still require the Phase 4 human-minted, bound, expiring, single-use token.
- The adapter has no reference to `WriteTokenVault` and no token-mint call site.
- No push, publication, production drawing, Scheduler provider, or LHS provider was added or used.

## Assumptions

- `C:\Program Files\Deswik\Deswik.Suite 2025.2` remains the target Deswik installation.
- The accepted Phase 4 guarded-write path and Phase 6 job path were assumed Current for the live check; automated tests revalidated their safety policies.
- The project's required initialized stdio lifecycle targets MCP `2025-11-25`; compatible older initialized versions are also accepted.
- A disposable saved drawing with a clean `_MCP_PREVIEW` layer is available for the human write check.

## Human acceptance

The user reported “phase 7 passes” on 2026-09-29 after the Windows PowerShell
5.1 client and UTF-8 BOM fixes. This is a human-reported pass for checks 1–4;
the detailed live transcript was not supplied. The steps remain below for
repeat runs.

Use only a **saved disposable Deswik drawing**. Close Deswik.CAD before any
rebuild because it locks the add-in DLL. Open PowerShell in the plugin folder
containing `README.md`. Copy only the lines inside
each code block, never the Markdown fences or raw JSON examples. Stop if a
command fails: it does not populate its result variable, so do not reuse an
empty or stale approval, token, or commit value.

In PowerShell window 1, build and run the bridge. Leave this window open:

```powershell
$dotnetExe = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnetExe -or -not (& $dotnetExe --list-sdks)) {
  $dotnetExe = Join-Path $env:USERPROFILE '.dotnet-sdk-8\dotnet.exe'
}
if (-not (Test-Path $dotnetExe) -or -not (& $dotnetExe --list-sdks)) { throw 'Install .NET 8 SDK and set $dotnetExe to its dotnet.exe.' }
$deswikDir = Read-Host 'Full path to your Deswik.Suite installation folder'
if (-not (Test-Path (Join-Path $deswikDir 'Deswik.Graphics.dll'))) { throw 'Deswik.Graphics.dll was not found in that folder.' }
$env:DESWIK_DIR = $deswikDir
$pythonExe = (Get-Command python -ErrorAction SilentlyContinue).Source
if (-not $pythonExe) { $pythonExe = Read-Host 'Full path to python.exe' }
if (-not [IO.Path]::IsPathFullyQualified($pythonExe) -or -not (Test-Path $pythonExe)) { throw 'A full path to python.exe is required.' }
$env:DESWIK_MCP_PYTHON = $pythonExe
& $dotnetExe build 'deswik-mcp\src\Deswik.Addin\Deswik.Addin.csproj' -c Release -p:DeswikDir="$deswikDir" -v:q -clp:ErrorsOnly
if ($LASTEXITCODE -ne 0) { throw 'Add-in build failed.' }
& $dotnetExe build 'deswik-mcp\src\Deswik.Bridge.Standalone' -c Release -p:DeswikDir="$deswikDir" -v:q -clp:ErrorsOnly
if ($LASTEXITCODE -ne 0) { throw 'Bridge build failed.' }
& $dotnetExe build 'deswik-mcp\src\Deswik.Mcp.Server' -c Release -v:q -clp:ErrorsOnly
if ($LASTEXITCODE -ne 0) { throw 'MCP server build failed.' }
& '.\deswik-mcp\src\Deswik.Bridge.Standalone\bin\Release\net8.0\Deswik.Bridge.Standalone.exe'
```

In Deswik.CAD, load the rebuilt Release `Deswik.Addin.dll`, open the saved
disposable drawing, and confirm the dock panel says connected. Open PowerShell
window 2 in the same plugin folder, then start a real MCP stdio process and
define a small client:

```powershell
$mcpExe = (Resolve-Path '.\deswik-mcp\src\Deswik.Mcp.Server\bin\Release\net8.0\Deswik.Mcp.Server.exe').Path
$start = [System.Diagnostics.ProcessStartInfo]::new()
$start.FileName = $mcpExe
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.RedirectStandardInput = $true
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
$mcp = [System.Diagnostics.Process]::new()
$mcp.StartInfo = $start
if (-not $mcp.Start()) { throw 'MCP server did not start.' }
$script:mcpId = 0
function Invoke-Mcp([string]$Method, [hashtable]$Params) {
  $script:mcpId++
  $request = [ordered]@{ jsonrpc='2.0'; id=$script:mcpId; method=$Method; params=$Params }
  $mcp.StandardInput.WriteLine(($request | ConvertTo-Json -Depth 20 -Compress))
  $mcp.StandardInput.Flush()
  $line = $mcp.StandardOutput.ReadLine()
  if ([string]::IsNullOrWhiteSpace($line)) { throw 'MCP server returned no response.' }
  $line | ConvertFrom-Json
}
$initialized = Invoke-Mcp 'initialize' @{ protocolVersion='2025-11-25'; capabilities=@{}; clientInfo=@{ name='phase-7-acceptance'; version='1' } }
$mcp.StandardInput.WriteLine('{"jsonrpc":"2.0","method":"notifications/initialized"}')
$mcp.StandardInput.Flush()
$initialized | ConvertTo-Json -Depth 10
```

**Stop** unless the response names `deswik-workflow-bridge`, returns protocol
`2025-11-25`, and advertises the `tools` capability.

1. [x] **Exact typed catalogue.** Run:

   ```powershell
   $listed = Invoke-Mcp 'tools/list' @{}
   $listed.result.tools | Select-Object name,description | Format-Table -AutoSize
   ($listed.result.tools).Count
   ```

   Pass: count is exactly 20, every row has an `inputSchema`, and the names
   match the list under Behaviour. Disprove: any internal action, legacy
   writer, demo route, `map.generate`, or `map.install` appears; stop and save
   the response.

2. [x] **Live read round-trip.** Run:

   ```powershell
   $document = Invoke-Mcp 'tools/call' @{ name='get_cad_document'; arguments=@{} }
   $document.result.structuredContent | Format-List
   ```

   Pass: `isError` is false, `mode` is `live`, and the document name/path
   identifies the active disposable drawing. Disprove: demo data, another
   drawing, a missing mode, or a disconnected/unsupported result.

3. [x] **Unapproved and tokenless writes fail closed.** Note the production
   hole count in the disposable test area, then run:

   ```powershell
   $unknown = Invoke-Mcp 'tools/call' @{ name='draw_cad_ugdrillholes'; arguments=@{} }
   $tokenless = Invoke-Mcp 'tools/call' @{ name='commit_ugdrillholes'; arguments=@{} }
   $unknown | ConvertTo-Json -Depth 10
   $tokenless | ConvertTo-Json -Depth 10
   ```

   Expected refusals: the first is JSON-RPC `-32602` “Tool is not approved”;
   the second has `isError: true` and `human_token_required`. Pass only if the
   drawing remains unchanged. Any new production geometry is an unexpected
   failure: stop and inspect it without deleting by layer.

4. [x] **Human-approved MCP commit and rollback.** First verify
   `_MCP_PREVIEW` is clean using the Phase 4 procedure. Choose a test point in
   an empty area of the saved disposable drawing and adjust the three points
   below if necessary. Run the preview call, then switch to Deswik and choose
   **Yes only if** the modal names the correct drawing and exactly one hole:

   ```powershell
   $preview = Invoke-Mcp 'tools/call' @{ name='preview_ugdrillholes'; arguments=@{ holes=@(@{ pivot=@(1.0,0.0,0.0); collar=@(1.0,0.0,1.0); toe=@(1.0,0.0,10.0); pivotId='P1'; holeId='MCP-P7-H1'; diameter=0.089 }) } }
   $preview.result.structuredContent | ConvertTo-Json -Depth 20
   ```

   After accepting the modal, run each block separately:

   ```powershell
   $approvalId = $preview.result.structuredContent.data.ApprovalId
   if ([string]::IsNullOrWhiteSpace($approvalId)) { throw 'Preview returned no approval ID.' }
   $approval = Invoke-Mcp 'tools/call' @{ name='get_write_approval'; arguments=@{ approvalId=$approvalId } }
   $token = $approval.result.structuredContent.data.token
   if ([string]::IsNullOrWhiteSpace($token)) { throw 'Human approval returned no token.' }
   $commit = Invoke-Mcp 'tools/call' @{ name='commit_ugdrillholes'; arguments=@{ token=$token } }
   $commit.result.structuredContent | ConvertTo-Json -Depth 20
   ```

   Pass commit: exactly one handle is returned and one whole native
   `UGDrillHole` appears on `RINGDESIGN\_MCP_APPROVED\HOLES`. Then prepare its
   rollback, inspect the Deswik modal, and choose **Yes only if** it names that
   one commit/handle:

   ```powershell
   $commitId = $commit.result.structuredContent.data.CommitId
   if ([string]::IsNullOrWhiteSpace($commitId)) { throw 'Commit returned no commit ID.' }
   $rollbackPreview = Invoke-Mcp 'tools/call' @{ name='prepare_rollback_ugdrillholes'; arguments=@{ commitId=$commitId } }
   $rollbackPreview.result.structuredContent | ConvertTo-Json -Depth 20
   ```

   ```powershell
   $rollbackApprovalId = $rollbackPreview.result.structuredContent.data.ApprovalId
   $rollbackApproval = Invoke-Mcp 'tools/call' @{ name='get_write_approval'; arguments=@{ approvalId=$rollbackApprovalId } }
   $rollbackToken = $rollbackApproval.result.structuredContent.data.token
   if ([string]::IsNullOrWhiteSpace($rollbackToken)) { throw 'Rollback approval returned no token.' }
   $rolledBack = Invoke-Mcp 'tools/call' @{ name='rollback_ugdrillholes'; arguments=@{ token=$rollbackToken } }
   $rolledBack.result.structuredContent | ConvertTo-Json -Depth 20
   ```

   Pass rollback: `Deleted` is 1 and only the Phase 7 test hole disappears.
   Disprove: a commit succeeds without the Deswik **Yes**, the handle/result
   disagrees with the drawing, or rollback removes any other entity.

When finished, close the MCP process without affecting the bridge or drawing:

```powershell
$mcp.StandardInput.Close()
$mcp.WaitForExit(5000) | Out-Null
if (-not $mcp.HasExited) { $mcp.Kill() }
$mcp.Dispose()
```

## Missing inputs

- A standalone `python.exe` was not on `PATH`. The installed LibreOffice
  Python 3.12.13 runtime was compatible and passed both dependency-free Python
  suites, so it was used by absolute path.
- The user reported all four live MCP client/Deswik checks passed on
  2026-09-29; no detailed transcript was supplied.

## Open questions

None for Phase 7.
