# Phase 4 — Guarded CAD writes

## Gate

Phase 4 accepted by the user: all four human checks passed on 2026-09-24.

## Status

Complete. The user reported all four live Deswik.CAD acceptance checks passed on 2026-09-24. This is human-reported acceptance, not an independently observed CAD run in this session.

## Changed files

Historical file paths below use `<PLUGIN_ROOT>` in place of the earlier machine's drive path. The repeat-run commands under **Human acceptance** resolve from the current plugin folder.

- `<PLUGIN_ROOT>\deswik-mcp\src\Deswik.Bridge.Standalone\GuardedWritePolicy.cs`
- `<PLUGIN_ROOT>\deswik-mcp\src\Deswik.Bridge.Standalone\AssemblyInfo.cs`
- `<PLUGIN_ROOT>\deswik-mcp\src\Deswik.Bridge.Standalone\TcpBridge.cs`
- `<PLUGIN_ROOT>\deswik-mcp\src\Deswik.Bridge.Tests\Program.cs`
- `<PLUGIN_ROOT>\deswik-mcp\src\Deswik.Addin\BridgeClient.cs`
- `<PLUGIN_ROOT>\deswik-mcp\src\Deswik.Addin\CadReader.cs`
- `<PLUGIN_ROOT>\deswik-mcp\src\Deswik.Addin\Deswik.Addin.csproj`
- `<PLUGIN_ROOT>\deswik-mcp\src\Deswik.Addin\DeswikMcpAddin.cs`
- `<PLUGIN_ROOT>\deswik-mcp\src\Deswik.Addin\GuardedWriteCoordinator.cs`
- `<PLUGIN_ROOT>\deswik-mcp\tools\Test-AddinPreflight.ps1`
- `<PLUGIN_ROOT>\docs\CAD-Actions.md`
- `<PLUGIN_ROOT>\docs\Wire-Protocol.md`
- `<PLUGIN_ROOT>\README.md`

## Behaviour

- `preview_ugdrillholes` draws only owned polylines on `_MCP_PREVIEW`. It refuses `preview_layer_dirty` if the layer contains a handle outside the live preview record. It returns a manifest and approval ID, never a token.
- Deswik shows the manifest in a `Yes/No` modal with `No` selected by default. The display uses live document data and sanitized identifiers; newline and field-separator injection is removed.
- The bridge accepts an approval event only from the add-in connection that produced the matching preview result. It verifies the manifest SHA256 before recording the pending approval. The mint function has one release call site.
- Tokens contain 256 bits of CSPRNG entropy, expire after 10 minutes, live only in the bridge process, and are consumed before the first commit or rollback attempt.
- Commit re-fingerprints the loaded production drawing before creating native `UGDrillHole` entities on `RINGDESIGN\_MCP_APPROVED\HOLES`. A mismatch returns `stale_preview`, creates nothing, consumes the token, and leaves the preview.
- Rollback requires a separate default-No modal and token. It deletes only handles stored in that commit record.
- Unsaved drawings fail with `drawing_unsaved` because a write token cannot be bound to an empty drawing path.
- The six prior writers are fenced in the bridge and add-in: `draw_cad_text`, `draw_cad_polylines`, `slice_cad_polyface`, `draw_cad_blastholes`, and `draw_cad_ugdrillholes` return `forbidden_unfenced`; `create_cad_layer` does too unless the name is exactly `_MCP_PREVIEW`.

## Commands run

```powershell
python tests/test_roundtrip.py
python tests/test_commands.py

dotnet build `
  'deswik-mcp\src\Deswik.Addin\Deswik.Addin.csproj' -c Release `
  -p:DeswikDir="$env:DESWIK_DIR"

dotnet build `
  'deswik-mcp\src\Deswik.Bridge.Standalone\Deswik.Bridge.Standalone.csproj' -c Release `
  -p:DeswikDir="$env:DESWIK_DIR"

dotnet test `
  'deswik-mcp\src\Deswik.Bridge.Tests\Deswik.Bridge.Tests.csproj' -c Release `
  -p:DeswikDir="$env:DESWIK_DIR"
```

Results: both Python suites passed; 15 bridge tests passed; Addin Release build passed with 11 dependency-resolution warnings and 0 errors; Standalone Release build passed with 8 dependency-resolution warnings and 0 errors. A live loopback abuse check returned `forbidden_unfenced` for a legacy writer and internal commit action, and `approval_unavailable` for an unapproved ID.

Host preflight: `Test-AddinPreflight.ps1` passed 13/13 checks with CAD closed. In Deswik.CAD 2025.2.3940, Plugin Manager loaded the Release `Deswik.Addin` with startup class `DeswikMcpAddin` and showed the MCP panel. With the Release bridge connected, `get_cad_document` returned an unsaved `Document 1`; `preview_ugdrillholes` returned `drawing_unsaved`, and `get_cad_layers` showed no `_MCP_PREVIEW` layer. The former direct writer `draw_cad_ugdrillholes` and production `create_cad_layer` both returned `forbidden_unfenced`. The bridge process started for this check was stopped afterward.

Subsequent live check: `Test File.duf` was open and saved, but the add-in still reported the earlier unnamed document. Reloading the add-in made the path appear; switching back to `Document 1` left the saved path in the response. The add-in cached the document passed at load. `Deswik.Common.Utilities.DeswikApplication.CurrentOpenDoc` is the SDK-verified active-document property; `RequireCadReader()` now resolves it on each request and resets guarded-write state when the document instance changes. The changed add-in builds to `%TEMP%\DeswikMcpPhase4Patch` with 0 errors, and the 15 bridge harness tests pass. The in-use Release DLL could not be replaced while Deswik.CAD is open, so the fix is not yet host-verified.

## Constraint check

- Bound to `127.0.0.1:9595`; no external network access added.
- No shell execution or caller-controlled executable path added.
- Preview deletion and rollback use exact recorded handles through the DLL-verified `Entities.Remove(Figure)` API.
- Production creation uses native `Deswik.Graphics.Figures.UGDrillHole`.
- No official help content, production `.ddf`, or client package was added.
- No push, publish, or external write was performed.

## Assumptions

- Deswik.Graphics 2025.2 exposes drawing path but no document GUID. The add-in therefore assigns a session document GUID and replaces it whenever the live drawing path changes.
- The source fingerprint covers every loaded non-preview figure using fields already exposed by CAD actions and verified against the DLL: handle, figure GUID, type, layer, and bounding box. Polyfaces also include centre of gravity, volume, and vertex count.
- The fixed production layer is `RINGDESIGN\_MCP_APPROVED\HOLES`; request text cannot select another production layer.

## Human acceptance

For a repeat run, confirm the updated Release DLL at `deswik-mcp\src\Deswik.Addin\bin\Release\net8.0-windows\Deswik.Addin.dll` is loaded after a CAD restart. A green build or bridge connection alone is not acceptance. Work in a **saved, disposable drawing**, never a production drawing. An earlier local test used `docs\Test File.duf`, which is not part of the Git project; use your own disposable file if it is absent. A `drawing_unsaved` error means save the intended drawing in CAD before continuing.

Open two PowerShell windows **in the plugin repository folder**. In window 1, run the bridge below and leave that window open. If it already says `TCP Server started on port 9595`, leave the existing bridge running instead of starting another copy. Set `$env:DESWIK_MCP_PYTHON` to the full path of your `python.exe` before starting the bridge.

```powershell
$pluginRoot = (Get-Location).Path
$env:DESWIK_MCP_PYTHON = (Get-Command python).Source
& (Join-Path $pluginRoot 'deswik-mcp\src\Deswik.Bridge.Standalone\bin\Release\net8.0\Deswik.Bridge.Standalone.exe')
```

In window 2, load the `dsw` command:

```powershell
$pluginRoot = (Get-Location).Path
. (Join-Path $pluginRoot 'deswik-mcp\tools\deswik.ps1')
```

Copy only the lines **inside** code blocks into PowerShell; do not paste the Markdown fence lines. A red `dsw` error means that command failed. Stop that check instead of using an empty or old `$preview`, `$approval`, or `$commit` variable.

Before check 1, load the updated add-in in Deswik.CAD, open the saved test drawing, and run `dsw get_cad_document | Format-List`. Switch to another CAD tab, run it again, switch back, and run it a third time. The reported drawing must follow the active tab each time. If it does not, **stop Phase 4**.

1. [x] **Direct-write fence.** In window 2, run each line separately:

   ```powershell
   dsw draw_cad_ugdrillholes @{}
   dsw create_cad_layer @{ name='PRODUCTION' }
   dsw create_cad_layer @{ name='_MCP_PREVIEW' }
   ```

   The first two must fail with `forbidden_unfenced` and create nothing. The `_MCP_PREVIEW` layer request is the permitted exception; verify that this is the only new layer. Do not continue if a production write succeeds.

2. [x] **Preview, reject, and dirty-layer refusal.** First enter the sample holes, then run the preview. This is a separate command: wait for its result and CAD dialog before typing the next command.

   ```powershell
   $holes = @(
     @{ pivot=@(0,0,0); collar=@(0,0,1); toe=@(0,0,10); pivotId='P1'; holeId='H1'; diameter=0.089 },
     @{ pivot=@(0,0,0); collar=@(1,0,1); toe=@(1,0,9); pivotId='P1'; holeId='H2'; diameter=0.089 }
   )
   $preview = $null
   $preview = dsw preview_ugdrillholes @{ holes=$holes }
   ```

   Confirm only `_MCP_PREVIEW` geometry appears. The dialog must show the drawing, document ID, fixed layer, hole count, hole IDs, and total metres, with **No** selected by default. Choose **No**. Only if preview succeeded and `$preview.approvalId` exists, run:

   ```powershell
   $preview | Format-List
   dsw get_write_approval @{ approvalId=$preview.approvalId }
   ```

   Approval must fail with `approval_unavailable`. In CAD, manually add one test entity to `_MCP_PREVIEW`, then run `dsw preview_ugdrillholes @{ holes=$holes }` again. It must fail with `preview_layer_dirty` and add no geometry. Remove **only** that manually added entity before check 3.

3. [x] **Approved commit, token replay, and stale-source refusal.** Preview again using `$preview = dsw preview_ugdrillholes @{ holes=$holes }`. Inspect the manifest and choose **Yes** only if it names the saved disposable drawing and the expected holes. After a successful preview and approval, run these lines **one at a time**:

   ```powershell
   $approval = dsw get_write_approval @{ approvalId=$preview.approvalId }
   $commit = dsw commit_ugdrillholes @{ token=$approval.token }
   $commit | Format-List
   dsw commit_ugdrillholes @{ token=$approval.token }
   ```

   The first commit must return native `UGDrillHole` handles on `RINGDESIGN\_MCP_APPROVED\HOLES`; the repeated commit must fail with `token_consumed`. For the stale-source case, run a **new** preview and choose **Yes**:

   ```powershell
   $stalePreview = dsw preview_ugdrillholes @{ holes=$holes }
   ```

   After that succeeds, change one non-preview source entity in the disposable drawing. Then run these separately:

   ```powershell
   $staleApproval = dsw get_write_approval @{ approvalId=$stalePreview.approvalId }
   dsw commit_ugdrillholes @{ token=$staleApproval.token }
   ```

   Expect `stale_preview`, no new production holes, and the preview still present. Do not reuse the first token.

4. [x] **Rollback of the successful commit.** Use the successful `$commit.commitId` from check 3. Run the preparation command, inspect the default-**No** rollback dialog, and approve it only if it identifies that commit. Then fetch the separate rollback token and run rollback:

   ```powershell
   $rollbackPreview = dsw prepare_rollback_ugdrillholes @{ commitId=$commit.commitId }
   ```

   After approving the correct rollback dialog, run these **one at a time**:

   ```powershell
   $rollbackApproval = dsw get_write_approval @{ approvalId=$rollbackPreview.approvalId }
   dsw rollback_ugdrillholes @{ token=$rollbackApproval.token }
   dsw rollback_ugdrillholes @{ token=$rollbackApproval.token }
   ```

   Only that commit's recorded handles should disappear. The second rollback must fail with `token_consumed`. Record a check as passed only after observing its CAD result, not from the bridge's connected indicator alone.

## Missing inputs

- None for Phase 4 acceptance; the user reported checks 1–4 passed. The CAD observations and document-switch preflight were not captured by this session.

## Open questions

- None recorded after the user's 4/4 acceptance report.
