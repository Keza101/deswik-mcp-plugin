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

- **`id` must be unique per request.** Direct forwarded requests keep a 15 s
  timeout timer per id; reusing an id can let a stale timer kill a newer request.
- Live requests fail closed. The bridge never substitutes demo data when a
  live provider is missing.
- Direct forwarded requests time out after **15 s**. Approved long-running
  actions use bridge-owned jobs, described below.
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

## Async jobs

Submit an approved long read or guarded write with `job.submit`; it returns a
job ID without waiting for the CAD result:

```json
{ "id": "submit-1", "action": "job.submit",
  "params": { "action": "get_cad_elements", "args": { "limit": 100 } } }
{ "id": "submit-1", "mode": "live", "success": true,
  "data": { "JobId": "...", "State": "queued", "DeadlineAt": "..." } }
```

Poll with `job.get` and cancel with `job.cancel`, each using
`{ "jobId": "..." }`. States are `queued`, `running`, `completed`,
`cancelled`, `failed`, and `partial`. A poll of a failed or partial job has
`success: false`, `mode: live`, and a job snapshot in `data`. A bridge restart
forgets all IDs; polling an old ID fails with `job_unknown`.

The exact `job.submit` set is `get_cad_document`, `get_cad_layers`,
`get_cad_layer_attributes`, `get_cad_elements`, `get_cad_selection`,
`get_ug_selection_context`, `get_cad_polyface_info`,
`get_cad_polyface_geometry`, `get_cad_polyline_geometry`,
`get_cad_polylines_under`,
`get_cad_blasthole_details`, `get_cad_ugdrillhole_details`,
`commit_ugdrillholes`, and `rollback_ugdrillholes`. A commit or rollback
still requires its own human-minted token in `args`. Unknown actions and legacy
writers fail with `job_action_refused` before a job is created. Cancelling
before write dispatch leaves no production geometry. Once a whole commit has
started, cancellation waits for the add-in's result; a completed commit is
reported `completed` with its handles, and any surviving handles after a
failed commit are reported as `partial`.

Each job has a default 30-minute ceiling. The bridge host can set a shorter
per-action ceiling with `DESWIK_JOB_TIMEOUT_SECONDS_<ACTION>`, where dots become
underscores and the value is 1–1800 seconds. A deadline during an active
commit reports `partial` with `job_deadline_write_unknown` until the add-in
reports the outcome. Inspect the drawing before another write if the add-in
never reports a result.

The PowerShell client returns only successful `data` by default. Use
`-RawResponse` when polling so failure snapshots and surviving handles remain
visible.

## MCP stdio adapter

`deswik-mcp/src/Deswik.Mcp.Server` is the MCP boundary. It uses newline-
delimited JSON-RPC 2.0 on stdin/stdout and supports the `2025-11-25`,
`2025-06-18`, and `2025-03-26` initialization versions for compatible local
clients. The server advertises only the `tools` capability; logs and bridge
traffic never use stdout.

`tools/list` returns an exact 23-tool catalogue:

- CAD reads: `get_cad_document`, `get_cad_layers`,
  `get_cad_layer_attributes`, `get_cad_elements`, `get_cad_selection`,
  `get_ug_selection_context`, `get_cad_polyface_info`,
  `get_cad_polyface_geometry`, `get_cad_polyline_geometry`,
  `get_cad_polylines_under`,
  `get_cad_blasthole_details`, and `get_cad_ugdrillhole_details`.
- Guarded workflow: `preview_ugdrillholes`, `get_write_approval`,
  `commit_ugdrillholes`, `prepare_rollback_ugdrillholes`, and
  `rollback_ugdrillholes`.
- Jobs: `job.submit`, `job.get`, and `job.cancel`.
- Read-only Process Map operations: `map.inspect`, `map.validate`, and
  `map.inventory`.

Tool calls always send `mode: live` to the loopback bridge. An unknown tool is
refused before any TCP connection. Direct commit and rollback tools require a
non-empty human-minted token, as do commit and rollback actions nested inside
`job.submit`; missing tokens return `isError: true` with
`human_token_required`. File-producing `map.generate` and `map.install`, the
six legacy CAD writers, internal actions, and demo mode are not MCP tools.

Successful and failed bridge envelopes are returned in `structuredContent`
and mirrored as JSON text content. A bridge failure sets `isError: true`
without changing its `mode` or `errorCode`.

## PowerShell client

```powershell
. deswik-mcp/tools/deswik.ps1
dsw get_cad_selection
dsw get_ug_selection_context
dsw get_ug_selection_context @{ roles=@{ '0x670'='stope' }; includePolyfaceMetrics=$true }
dsw get_cad_polyface_geometry @{ handle='0x670'; start=0; limit=25 }
dsw get_cad_polyline_geometry @{ handle=42; start=0; limit=25 }
dsw preview_ugdrillholes @{ holes=@(@{ pivot=@(0,0,0); collar=@(0,0,1); toe=@(0,0,10); holeId="H1" }) }
dsw map.inspect @{ path=(Resolve-Path 'tests\archive_ddf\SDK - Stope Design Layout.ddf').Path }
```

`get_ug_selection_context` accepts optional `roles` and
`includePolyfaceMetrics` parameters. Each role key must be a selected decimal
handle or an explicitly `0x`-prefixed hexadecimal handle. Supported labels
are `stope`, `drive`, `brow`, `void`, `ring`, `hole`, `survey`, and
`unclassified`. Labels are operator-supplied for this response only; unknown,
duplicate, or unselected handles fail with `invalid_context_request`.
Metrics are opt-in because polyface volume and center-of-gravity reads can be
costly. Unreadable metrics are reported as warnings, and `readyForDesign`
remains `false` even when every selected figure has a role.

`get_cad_polyface_geometry` requires one polyface handle (decimal or
`0x`-prefixed hex) and accepts `start` (default 0) and `limit` (default 250,
maximum 500). It returns a matching page of vertices and raw four-value
`GetFaceIndexes` results, with total `vertexCount`, `faceCount`, and
`nextStart`. Face indexes are preserved exactly as Deswik returns them;
negative values and edge visibility have not yet been interpreted. The
response declares unknown units and coordinate system and
`readyForDesign: false`. An invalid request fails with
`invalid_geometry_request`; unreadable geometry fails with
`geometry_unavailable`.

`get_cad_polyline_geometry` uses the same `handle`, `start`, and `limit`
request bounds for one polyline. It returns `vertexCount`, a bounded
`vertices` page, the native `closed` flag, and `nextStart`. It also declares
unknown units and coordinate system and `readyForDesign: false`.
