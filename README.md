# Deswik MCP Plugin (CAD add-in)

A Deswik.CAD add-in that exposes live CAD data (layers, entities, selection,
geometry, drill-hole drawing) over a local TCP bridge, so external tools —
MCP servers, Claude, PowerShell, Python — can read and drive a running
Deswik.CAD session.

> **Disclaimer**: not affiliated with or endorsed by Deswik. No Deswik
> binaries or documentation are included; you need your own licensed
> Deswik.Suite installation to build and run this.

## Architecture

```
Deswik.CAD ── Deswik.Addin (this plugin, loaded by Plugin Manager)
                  │  TCP 127.0.0.1:9595, newline-delimited JSON
              Deswik.Bridge.Standalone (router process)
                  │
              your client (PowerShell, Python, MCP server, …)
```

- `deswik-mcp/src/Deswik.Addin` — the CAD plugin. Duck-typed plugin contract
  (`Load(IWin32Window)`, `Unload()`, `Application` property, and a
  `ctor(Deswik.Graphics.Application)` — all four are required by the loader).
  Registers a dock panel and connects to the bridge as a capability provider.
- `deswik-mcp/src/Deswik.Bridge` — shared wire-protocol library.
- `deswik-mcp/src/Deswik.Bridge.Standalone` — standalone TCP bridge that
  routes requests to whichever add-in registered the capability and owns the
  pinned local Process Map sidecar actions.
- `deswik-mcp/tools/deswik.ps1` — PowerShell client
  (`. deswik.ps1; dsw get_layers`).
- `deswik_pm` — local copy of the Process Map SDK used by the bridge sidecar.
  The SDK also remains in `W:\AI_Deswik`; keep both copies in sync when changing it.
- `docs/bridge-phases` — bridge gate reports and operator acceptance steps.
- `docs/workflow-bridge-status.html` — interactive phase and decision board.

## Build

```powershell
# The csproj default is D:\Program Files\Deswik\Deswik.Suite 2025.2.
# Override it with -p:DeswikDir if your install is elsewhere, e.g. on C:.
dotnet build deswik-mcp/src/Deswik.Addin/Deswik.Addin.csproj -c Release `
  -p:DeswikDir="C:\Program Files\Deswik\Deswik.Suite 2025.2"
dotnet build deswik-mcp/src/Deswik.Bridge.Standalone -c Release
dotnet test deswik-mcp/src/Deswik.Bridge.Tests -c Release `
  -p:DeswikDir="C:\Program Files\Deswik\Deswik.Suite 2025.2"
```

Process Map actions are `map.inspect`, `map.validate`, `map.generate`,
`map.install`, and `map.inventory`. They use the pinned
`W:\deswik-mcp-plugin\deswik_pm\sidecar.py` entry point and return SHA256 provenance
for every file read or written.

The bridge's pinned sidecar SHA256 in `ProcessMapSidecar.cs` must match this
local copy after any SDK update. Local acceptance packages stay in the ignored
`workflow-packages` folder; the portable test fixture is in `tests/archive_ddf`.

CAD production writes use the guarded flow documented in
`docs/CAD-Actions.md`: preview on `_MCP_PREVIEW`, a default-No approval modal
inside Deswik, then a single-use token-bound commit. The six older direct
writer actions fail with `forbidden_unfenced`.

## Run

1. Start the bridge: `dotnet run --project deswik-mcp/src/Deswik.Bridge.Standalone`.
2. In Deswik.CAD: Tools → Plugin Manager → Add → select the built
   `Deswik.Addin.dll` → Load. A dock panel shows the bridge connection state.
3. Talk to it:

```powershell
. deswik-mcp/tools/deswik.ps1
dsw hello
dsw get_layers
dsw get_selection
```

Port `9595` is a raw JSON-over-TCP endpoint, not a website. Do not open it in
a browser. Use the PowerShell client above; add `-Mode demo` only when
synthetic demo data is explicitly required.

## Wire protocol

One JSON object per line over TCP (UTF-8, **no BOM**):
request `{ "id": "...", "action": "...", "params": { } }` →
response `{ "id": "...", "mode": "live", "success": true, "data": { } }`.
Use a fresh unique `id` per request — duplicate ids confuse the 15 s timeout
bookkeeping.

Responses always include a top-level `mode`: `live`, `demo`, `unsupported`,
or `disconnected`. Requests default to live and fail closed when the add-in or
capability is unavailable. Send top-level `"mode": "demo"` only when synthetic
demo data is explicitly wanted.

## Notes

- The plugin DLL is locked while Deswik.CAD runs; close CAD before rebuilding.
- Set `AssemblyVersion` to match your Deswik build to silence the version
  warning in the plugin log.
