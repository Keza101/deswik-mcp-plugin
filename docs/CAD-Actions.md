# CAD Actions

CAD actions registered by `Deswik.Addin`. Parameters marked `?` are
optional (default in parentheses). Geometry is in world coordinates; entity
`handle`s come from `get_cad_selection` / `get_cad_elements`.

## Reading

| Action | Params | Returns |
|---|---|---|
| `send_hello` | — | echo/health check |
| `get_cad_document` | — | active document info |
| `get_cad_layers` | — | full layer tree |
| `get_cad_elements` | `layer?`, `limit?` (500) | entities on a layer |
| `get_cad_layer_attributes` | `layer`, `limit?` (10000) | entities incl. attribute data |
| `get_cad_selection` | — | currently selected entities with geometry |
| `get_cad_polyface_info` | `handle` | polyface mesh stats/bounds |
| `get_cad_polyface_geometry` | `handle`, `start?`, `limit?` | bounded raw vertex/face page |
| `get_cad_polyline_geometry` | `handle`, `start?`, `limit?` | bounded vertex page and native closed flag |
| `get_cad_points_geometry` | `handle`, `start?`, `limit?` | bounded native Points collection page and raw display metadata |
| `get_cad_figure_geometry` | `handle` | raw Line/Circle/Arc/Point/Text/MText geometry and user attributes |
| `get_cad_polylines_under` | `layerPrefix` | all polylines under a layer subtree |
| `get_cad_blasthole_details` | `handle` | BlastHole entity fields |
| `get_cad_ugdrillhole_details` | `handle` | UGDrillHole entity fields |

## Guarded writing

| Action | Params | Behaviour |
|---|---|---|
| `preview_ugdrillholes` | `holes: [{ pivot, collar, toe, pivotId?, holeId?, diameter? }]` | Draws owned polylines on `_MCP_PREVIEW`, returns a manifest and opens the default-No approval modal. Never returns a token. |
| `get_write_approval` | `approvalId` | Returns a bridge-process token only after the Deswik modal was accepted. |
| `commit_ugdrillholes` | `token` | Consumes the token before the attempt and creates native `UGDrillHole` entities on `RINGDESIGN\_MCP_APPROVED\HOLES`. |
| `prepare_rollback_ugdrillholes` | `commitId` | Shows a default-No rollback manifest and returns its approval ID. |
| `rollback_ugdrillholes` | `token` | Consumes a separate rollback token and deletes only handles in that commit record. |

The six former direct writers remain recognizable for compatibility but fail
with `forbidden_unfenced`: `create_cad_layer`, `draw_cad_text`,
`draw_cad_polylines`, `slice_cad_polyface`, `draw_cad_blastholes`, and
`draw_cad_ugdrillholes`. The only exception is `create_cad_layer` when `name`
is exactly `_MCP_PREVIEW`.

Long reads and token-bound commit/rollback can also be submitted through the
bridge's `job.submit` action, then polled or cancelled by job ID. The same
guarded-write token and CAD UI-thread rules apply; see `docs/Wire-Protocol.md`.

See `CadReader.cs` for the exact hole-spec fields — the two drill-hole actions
create native Deswik.UGDB-compatible entities, which need `Hole` (ID) and
`OriginalLength` populated or UGDB shows blank IDs / zero lengths.

## Notes

- Live acceptance uses the single reusable root `Run-Deswik-Tests.cmd`. Update
  it in place for each milestone, maximize valid/boundary/refusal/stress and
  no-mutation coverage, and archive intent/results in the phase write-up.
- Preview, commit, and rollback run on the CAD UI thread.
- Preview refuses `preview_layer_dirty` when `_MCP_PREVIEW` contains a handle
  outside the current in-process preview record.
- Tokens expire after 10 minutes, are single-use, and die with the bridge.
- Dip sign: `UGDrillHole.Dip` is **positive-up** from horizontal;
  `BlastHole` uses positive-down. Negate when converting between them.
