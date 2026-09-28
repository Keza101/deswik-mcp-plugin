# Phase 6 — Async jobs

## Gate

Phase 5 passed its three live CAD checks on 2026-09-25. The operator accepted
Phase 6 on 2026-09-25 after the live CAD run.

## Status

accepted — bridge and add-in code, normal Release builds, automated tests,
TCP smoke test, and operator live acceptance are complete. The operator
reported acceptance; detailed final terminal output was not archived here.

## Changed files

- `deswik-mcp/src/Deswik.Bridge.Standalone/BridgeJobs.cs` — process-local job
  records, deadlines, progress, cancellation, and exact action allowlist.
- `deswik-mcp/src/Deswik.Bridge.Standalone/TcpBridge.cs` — submit, poll, cancel,
  progress relay, job result routing, and a disposable-port test setting.
- `deswik-mcp/src/Deswik.Bridge.Standalone/GuardedWritePolicy.cs` — refuses the
  internal cancellation signal from external callers.
- `deswik-mcp/src/Deswik.Addin/BridgeClient.cs` — keeps reading the bridge
  connection while ordered CAD commands run and dispatches cancel signals
  promptly.
- `deswik-mcp/src/Deswik.Addin/DeswikMcpAddin.cs` — checks cancellation before
  production writes and sends per-hole progress.
- `deswik-mcp/src/Deswik.Addin/CadReader.cs` — reports each whole guarded hole.
- `deswik-mcp/src/Deswik.Addin/GuardedWriteCoordinator.cs` — records surviving
  handles if compensation after a failed commit is incomplete.
- `deswik-mcp/src/Deswik.Bridge.Tests/Program.cs` — job state and abuse tests.
- `tests/test_phase6_tcp.py` — disposable-port TCP test with a fake read-only
  add-in and a reply delayed beyond 15 seconds.
- `deswik-mcp/tools/deswik.ps1`, `docs/Wire-Protocol.md`, `README.md`, and
  `.gitignore` — client response option, operator protocol, overview, and test
  tracking.

## Behaviour

Direct forwarded requests retain their 15-second timeout. `job.submit`
accepts only the exact action set in `BridgeJobPolicy` and returns a job ID
before the add-in result. `job.get` exposes `queued`, `running`, `completed`,
`cancelled`, `failed`, or `partial`, progress counts, and recorded handles.
`job.cancel` is idempotent and accepts only a job ID. Unknown actions fail
before a job is created. Unknown IDs, including IDs from a previous bridge
process, fail with `job_unknown` and `mode: live`.
Raw request frames are no longer written to bridge or add-in logs, since a
guarded job submission may contain a single-use write token.

Guarded commit and rollback jobs consume the same human-minted single-use
tokens as direct calls. The bridge checks cancellation immediately before
dispatch; the add-in checks again on the CAD UI thread before a write begins.
After a write begins, cancel is recorded but the whole write finishes. If a
failed commit leaves handles after its exact-handle compensating delete, the
result is `partial` with those surviving handles and a commit ID. A deadline
during an in-progress write is temporarily `partial` with
`job_deadline_write_unknown` until the add-in reports its final result; it is
never called `cancelled` while geometry may remain. A CAD hang may therefore
leave the drawing outcome unknown to the bridge and requires inspection.

The default job ceiling is 30 minutes. A bridge host may set a shorter
per-action ceiling through `DESWIK_JOB_TIMEOUT_SECONDS_<ACTION>` (1–1800).
Job state and tokens remain in the bridge process only. The add-in abandons
queued writes when its bridge connection drops; a write already inside a
single commit finishes to its boundary.

## Commands run

Initial Release builds were directed to `.phase6-build/out/` because the
running bridge locked its normal Release executable:

```powershell
dotnet build deswik-mcp/src/Deswik.Addin/Deswik.Addin.csproj -c Release -p:DeswikDir='C:\Program Files\Deswik\Deswik.Suite 2025.2' -p:BaseOutputPath='H:\Apps_Tools\Deswik-Tools\Deswik-MCP-Plugin\.phase6-build\out\' -v:q -clp:ErrorsOnly
dotnet build deswik-mcp/src/Deswik.Bridge.Standalone/Deswik.Bridge.Standalone.csproj -c Release -p:DeswikDir='C:\Program Files\Deswik\Deswik.Suite 2025.2' -p:BaseOutputPath='H:\Apps_Tools\Deswik-Tools\Deswik-MCP-Plugin\.phase6-build\out\' -v:q -clp:ErrorsOnly
```

Both builds ended with `Build succeeded` and `0 Error(s)`; installed Deswik
assembly references still produce their existing version warnings.

The operator's first normal bridge build failed with `MSB3027`/`MSB3021`
because the old bridge process (PID 10832) locked its executable. Launching a
second instance then failed with socket error 10048 because that process still
owned port 9595. After verifying that the port owner was this repository's
bridge and CAD was closed, I stopped it and rebuilt both normal Release
projects. Both builds succeeded with zero errors; the rebuilt bridge was then
started and verified listening on `127.0.0.1:9595`.

```powershell
$env:DESWIK_MCP_PYTHON = (Get-Command python).Source
dotnet test deswik-mcp/src/Deswik.Bridge.Tests/Deswik.Bridge.Tests.csproj -c Release -p:DeswikDir='C:\Program Files\Deswik\Deswik.Suite 2025.2' -p:BaseOutputPath='H:\Apps_Tools\Deswik-Tools\Deswik-MCP-Plugin\.phase6-build\out\' -v:q -clp:ErrorsOnly
```

Exit code 0 and `ALL TESTS PASSED` (23 checks). The executable harness includes cancellation before dispatch,
cancel during a simulated 40-hole commit, per-hole progress, partial handles,
expiry, restart forgetting, and exact action refusal.

The disposable TCP test used port 9596 and a fake add-in. It returned the job
ID immediately, completed a 16-second read without the direct timeout, and
refused an unfenced writer and unknown job ID:

```powershell
python tests/test_phase6_tcp.py --port 9596 --delay 16
```

```text
PASS Phase 6 TCP submit returned immediately and completed after the 15 s direct ceiling
PASS unfenced job action refused and unknown job ID failed closed
```

```powershell
python tests/test_roundtrip.py
python tests/test_commands.py
```

Both ended with `ALL TESTS PASSED`.

```powershell
git check-ignore -v docs/bridge-phases/phase-6-async-jobs.md
```

Exit code 1 with no output: the phase record is not ignored and can be tracked.

## Constraint check

- The TCP listener remains on `127.0.0.1`; the optional port override is for
  a separate local test process.
- No new Process Map command, donor map, client data, external endpoint, shell
  launch, or production drawing was added or changed.
- CAD object model reads and writes remain on the Deswik UI thread.
- Legacy writers remain fenced, and job submission cannot route an internal
  write or mint a token.
- Compensating deletion uses only handles created by the current guarded
  commit; any survivors are reported as `partial`.
- No push or publication was performed.

## Assumptions

- The installed `C:\Program Files\Deswik\Deswik.Suite 2025.2` was the target
  build for the accepted live run.
- The CAD UI thread eventually returns from an individual commit. If it hangs,
  the bridge reports an unknown write outcome at the configured deadline and
  the operator must inspect the drawing.

## Human acceptance

Work only in a **saved disposable Deswik drawing**. An earlier local test used
`docs\Test File.duf`; use your own disposable drawing if it is absent. Close
Deswik.CAD and the existing bridge before rebuilding the normal Release paths,
because Windows locks their loaded binaries. Open PowerShell in
`H:\Apps_Tools\Deswik-Tools\Deswik-MCP-Plugin`. Copy only the lines inside each
code block, never the Markdown fence markers or raw JSON.

To repeat the acceptance run, rebuild and start the bridge in PowerShell
window 1. Leave this window open:

```powershell
$pluginRoot = (Get-Location).Path
$deswikDir = 'C:\Program Files\Deswik\Deswik.Suite 2025.2'
$env:DESWIK_DIR = $deswikDir
$env:DESWIK_MCP_PYTHON = (Get-Command python).Source
$bridgeExe = (Resolve-Path 'deswik-mcp\src\Deswik.Bridge.Standalone\bin\Release\net8.0\Deswik.Bridge.Standalone.exe').Path
Get-CimInstance Win32_Process -Filter "Name='Deswik.Bridge.Standalone.exe'" |
  Where-Object { $_.ExecutablePath -eq $bridgeExe } |
  ForEach-Object { Stop-Process -Id $_.ProcessId -ErrorAction Stop }
if (Get-NetTCPConnection -LocalPort 9595 -State Listen -ErrorAction SilentlyContinue) { throw 'Port 9595 is still in use.' }
dotnet build 'deswik-mcp\src\Deswik.Addin\Deswik.Addin.csproj' -c Release -p:DeswikDir="$deswikDir" -v:q -clp:ErrorsOnly
if ($LASTEXITCODE -ne 0) { throw 'Add-in build failed.' }
dotnet build 'deswik-mcp\src\Deswik.Bridge.Standalone' -c Release -p:DeswikDir="$deswikDir" -v:q -clp:ErrorsOnly
if ($LASTEXITCODE -ne 0) { throw 'Bridge build failed.' }
& (Join-Path $pluginRoot 'deswik-mcp\src\Deswik.Bridge.Standalone\bin\Release\net8.0\Deswik.Bridge.Standalone.exe')
```

**Stop** if either build fails. In Deswik.CAD, load the updated Release
`Deswik.Addin.dll` through Plugin Manager and open the saved disposable
drawing. Confirm the dock panel says connected. In PowerShell window 2, from
the same repository folder, load the client:

```powershell
$pluginRoot = (Get-Location).Path
. (Join-Path $pluginRoot 'deswik-mcp\tools\deswik.ps1')
Invoke-Deswik get_cad_document | Format-List
```

**Stop** if this does not name the active disposable drawing. A failed command
does not populate its result variable; do not use an empty or stale `$preview`,
`$approval`, `$submitted`, or `$final` value.

1. [x] **Async read and exact refusal.** Run each command in window 2:

   ```powershell
   $read = Invoke-Deswik job.submit @{ action='get_cad_elements'; args=@{ limit=100 } }
   $read | Format-List
   Invoke-Deswik job.get @{ jobId=$read.JobId } -RawResponse | Format-List
   Invoke-Deswik job.submit @{ action='draw_cad_ugdrillholes'; args=@{} } -RawResponse | Format-List
   ```

   The first call returns a job ID promptly. Poll again if it says `running`;
   the final state is `completed` with live drawing data. The direct writer
   attempt fails with `job_action_refused` and creates no geometry.

2. [x] **Cancel a 40-hole guarded write.** In window 2, construct 40 holes in
   the disposable drawing's empty test area, preview them, inspect the manifest
   in CAD, and choose **Yes** only when it names that drawing and exactly 40
   holes. First inspect the preview layer:

   ```powershell
   Invoke-Deswik get_cad_elements @{ layer='_MCP_PREVIEW'; limit=100 } |
     Format-Table handle,type,layer,label
   ```

   If it contains entities from an earlier session, inspect them in CAD and
   manually remove only the confirmed disposable preview entities, or use a
   clean saved disposable drawing. Do not clear the layer blindly: the add-in
   intentionally refuses `preview_layer_dirty` when it cannot prove ownership.
   Then run each block after observing the preceding CAD dialog:

   ```powershell
   $holes = @(1..40 | ForEach-Object { $x = [double]$_; @{ pivot=@($x,0.0,0.0); collar=@($x,0.0,1.0); toe=@($x,0.0,10.0); pivotId='P1'; holeId="H$_"; diameter=0.089 } })
   $preview = Invoke-Deswik preview_ugdrillholes @{ holes=$holes }
   $preview | Format-List
   ```

   ```powershell
   $approval = Invoke-Deswik get_write_approval @{ approvalId=$preview.approvalId }
   $submitted = Invoke-Deswik job.submit @{ action='commit_ugdrillholes'; args=@{ token=$approval.token } }
   $submitted | Format-List
   ```

   ```powershell
   $cancel = Invoke-Deswik job.cancel @{ jobId=$submitted.JobId } -RawResponse
   $cancel | Format-List
   $final = Invoke-Deswik job.get @{ jobId=$submitted.JobId } -RawResponse
   $final | Format-List
   ```

   Repeat the final poll until it leaves `queued` or `running`. If it ends
   `cancelled`, there must be **zero** new production holes. If it ends
   `completed`, all 40 whole holes and all 40 handles must be in the job record
   and visible in the drawing. `partial` or `failed` is an unexpected result:
   stop, record every reported handle, and inspect the drawing before cleanup.
   Cancellation must never say `cancelled` while production geometry remains.

3. [x] **Restart semantics.** Save `$read.JobId`, then stop this repository's
   bridge from window 2 (the current bridge may have no visible window):

   ```powershell
   $bridgeExe = (Resolve-Path 'deswik-mcp\src\Deswik.Bridge.Standalone\bin\Release\net8.0\Deswik.Bridge.Standalone.exe').Path
   Get-CimInstance Win32_Process -Filter "Name='Deswik.Bridge.Standalone.exe'" |
     Where-Object { $_.ExecutablePath -eq $bridgeExe } |
     ForEach-Object { Stop-Process -Id $_.ProcessId -ErrorAction Stop }
   ```

   Restart the bridge command in window 1. When it is listening, run in
   window 2:

   ```powershell
   Invoke-Deswik job.get @{ jobId=$read.JobId } -RawResponse | Format-List
   ```

   Expect `success: false`, `mode: live`, and `errorCode: job_unknown`, with no
   fabricated running state or result. The add-in should reconnect. Restart
   does not alter the drawing.

4. [x] **Drawing/record reconciliation.** Compare the final 40-hole job's
   reported handles and state with the disposable drawing. If it completed,
   use Phase 4's separately approved rollback for its `CommitId` to clean up.
   If it cancelled, verify no production holes were added. Preserve any
   unexpected partial result for inspection rather than deleting by layer.

## Missing inputs

None. The operator accepted the live CAD run on 2026-09-25.

## Open questions

None for Phase 6.
