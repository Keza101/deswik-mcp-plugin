# CAD Actions

All 16 actions registered by `Deswik.Addin`. Parameters marked `?` are
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
| `get_cad_polylines_under` | `layerPrefix` | all polylines under a layer subtree |
| `get_cad_blasthole_details` | `handle` | BlastHole entity fields |
| `get_cad_ugdrillhole_details` | `handle` | UGDrillHole entity fields |

## Writing / drawing

| Action | Params |
|---|---|
| `create_cad_layer` | `name` — backslash path (`A\B\C`) creates the nested tree |
| `draw_cad_text` | `layer`, `text`, `x?`, `y?`, `z?`, `height?` (5) |
| `draw_cad_polylines` | `layer`, `polylines: [{ points: [[x,y,z]…], closed?, color? [r,g,b], label? }]`, `texts?: [{ text, x, y, z, height? (0.5), color? }]` |
| `slice_cad_polyface` | `handle`, `origin: [x,y,z]`, `normal: [x,y,z]` — plane section of a solid |
| `draw_cad_blastholes` | `layer`, `holes: [{ id, collar/toe geometry, diameter? (0.076), burden?, spacing? }]` |
| `draw_cad_ugdrillholes` | `layer`, `ringId`, `holes: [{ pivotId, holeId?, geometry, diameter? (0.089), diameterString?, orderIndex?, explosive?, chargeCollar? (1.0) }]` |

See `CadReader.cs` for the exact hole-spec fields — the two drill-hole actions
create native Deswik.UGDB-compatible entities, which need `Hole` (ID) and
`OriginalLength` populated or UGDB shows blank IDs / zero lengths.

## Notes

- Drawing actions run on the CAD UI thread (marshalled internally).
- `draw_cad_ugdrillholes` layer convention for UGDB pickup:
  `RINGDESIGN\<project>\<ring>\HOLES`.
- Dip sign: `UGDrillHole.Dip` is **positive-up** from horizontal;
  `BlastHole` uses positive-down. Negate when converting between them.
