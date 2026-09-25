# Wire Protocol

TCP `127.0.0.1:9595`, one JSON object per line, UTF-8 **without BOM**.
This is raw TCP, not HTTP: opening `http://localhost:9595` in a browser returns
an explanatory `400 Bad Request`. Use `deswik-mcp/tools/deswik.ps1` or another
newline-delimited JSON client.

## Frames

Request (client → bridge):

```json
{ "id": "unique-id-1", "action": "get_cad_layers", "params": {} }
```

Requests are live by default. Synthetic demo data requires an explicit
top-level `"mode": "demo"` field. `"mode": "live"` is also accepted;
any other request mode is rejected.

Response (bridge → client):

```json
{ "id": "unique-id-1", "mode": "live", "success": true, "data": { ... } }
{ "id": "unique-id-1", "mode": "disconnected", "success": false, "error": "message", "errorCode": "ADDIN_DISCONNECTED" }
```

`mode` is mandatory on every response and identifies who answered:

| Mode | Meaning |
|---|---|
| `live` | A connected add-in or the pinned local Process Map sidecar answered, or the bridge produced a protocol/transport failure. |
| `demo` | Synthetic data answered an explicitly requested demo operation. |
| `unsupported` | An add-in is connected, but none registered the requested action. |
| `disconnected` | No add-in is connected. |

`success` is independent of `mode`: a live add-in may return a failed action.
`unsupported` and `disconnected` responses fail and contain no `data` payload.

## Rules

- **`id` must be unique per request.** The bridge keeps a 15 s timeout timer
  per id; reusing an id lets a stale timer kill the newer request.
- Live requests fail closed. The bridge never substitutes demo data when a
  live provider is missing.
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

## Process Map sidecar

The bridge owns an exact allowlist of local SDK actions and starts the pinned
Python entry point directly, without a shell or caller-controlled arguments:

| Action | Parameters |
|---|---|
| `map.inspect` | `path` |
| `map.validate` | `path` |
| `map.generate` | `shape`, `out`, `donor`; optional draw parameters |
| `map.install` | `source`, `filename` |
| `map.inventory` | none |

Every successful result includes `fileHashes`, with the absolute path, SHA256,
and `read` or `write` access for every file used. `map.install` only accepts a
filename matching `_TEST_[A-Za-z0-9 _-]+.ddf`, rejects path syntax, and fails
when the destination already exists.

## Guarded CAD writes

Production geometry uses three steps: `preview_ugdrillholes`, human approval
inside Deswik, then `commit_ugdrillholes`. Preview returns `approvalId`,
`recordId`, and a manifest but no token. After the operator accepts the
default-No modal, fetch the token with `get_write_approval` and that
`approvalId`. Commit accepts only `{ "token": "..." }`.

Tokens are opaque 256-bit values, expire after 10 minutes, and are consumed on
the first attempt whether it succeeds or fails. They are bound to the add-in
session document ID, drawing path, fixed target layer, manifest SHA256, source
fingerprint, operation, and record. A stale drawing fails with
`stale_preview`; replay fails with `token_consumed`.

Rollback is a second guarded operation:
`prepare_rollback_ugdrillholes` shows the exact commit record, and
`rollback_ugdrillholes` requires its own human-approved token. Deletion is by
the recorded handles only.

## PowerShell client

```powershell
. deswik-mcp/tools/deswik.ps1
dsw get_cad_selection
dsw preview_ugdrillholes @{ holes=@(@{ pivot=@(0,0,0); collar=@(0,0,1); toe=@(0,0,10); holeId="H1" }) }
dsw map.inspect @{ path=(Resolve-Path 'tests\archive_ddf\SDK - Stope Design Layout.ddf').Path }
```
