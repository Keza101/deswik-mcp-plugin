# Wire Protocol

TCP `127.0.0.1:9595`, one JSON object per line, UTF-8 **without BOM**.

## Frames

Request (client → bridge):

```json
{ "id": "unique-id-1", "action": "get_cad_layers", "params": {} }
```

Response (bridge → client):

```json
{ "id": "unique-id-1", "success": true, "data": { ... } }
{ "id": "unique-id-1", "success": false, "error": "message" }
```

## Rules

- **`id` must be unique per request.** The bridge keeps a 15 s timeout timer
  per id; reusing an id lets a stale timer kill the newer request.
- Requests time out after **15 s** at the bridge. Long-running work should be
  started asynchronously by the add-in with a separate status-poll action.
- Writers must use `new UTF8Encoding(false)` — a BOM on the first line breaks
  the bridge's JSON parser.

## Capability routing

Add-ins connect to the same port and register:

```json
{ "id": "…", "action": "register_addin",
  "params": { "name": "Deswik.MCP", "version": "1.0.0",
              "capabilities": ["get_cad_layers", "…"] } }
```

The bridge routes each incoming action to the add-in that registered it.
**Last registration wins** — if two add-ins claim the same action, the later
one silently takes over, so keep capability sets disjoint.

## PowerShell client

```powershell
. deswik-mcp/tools/deswik.ps1
dsw get_cad_selection
dsw draw_cad_text @{ layer="NOTES"; text="hello"; x=0; y=0; z=0; height=5 }
```
