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
  The SDK may also exist in a separate `AI_Deswik` project; keep shared files
  in sync when changing them.
- `docs/bridge-phases` — bridge gate reports and operator acceptance steps.
- `docs/workflow-bridge-status.html` — interactive phase and decision board.

## Build

```powershell
# Open PowerShell in this repository folder. Set your actual Deswik install path.
$deswikDir = '<your Deswik.Suite installation folder>'
$env:DESWIK_DIR = $deswikDir
$env:DESWIK_MCP_PYTHON = (Get-Command python).Source
dotnet build deswik-mcp/src/Deswik.Addin/Deswik.Addin.csproj -c Release
dotnet build deswik-mcp/src/Deswik.Bridge.Standalone -c Release
dotnet test deswik-mcp/src/Deswik.Bridge.Tests -c Release
```

Process Map actions are `map.inspect`, `map.validate`, `map.generate`,
`map.install`, and `map.inventory`. They use the pinned
`deswik_pm/sidecar.py` entry point and return SHA256 provenance
for every file read or written.

The bridge's pinned sidecar SHA256 in `ProcessMapSidecar.cs` must match this
local copy after any SDK update. Local acceptance packages stay in the ignored
`workflow-packages` folder; the portable test fixture is in `tests/archive_ddf`.
The bridge locates the sidecar by walking up from its executable to this
repository folder. Set `DESWIK_MCP_PYTHON` to the full path of `python.exe` in
the PowerShell window **before** starting the bridge; the bridge refuses a
missing or relative interpreter path.

CAD production writes use the guarded flow documented in
`docs/CAD-Actions.md`: preview on `_MCP_PREVIEW`, a default-No approval modal
inside Deswik, then a single-use token-bound commit. The six older direct
writer actions fail with `forbidden_unfenced`.

## Run

1. Open PowerShell in this repository folder and start the built bridge:

   ```powershell
   $env:DESWIK_MCP_PYTHON = (Get-Command python).Source
   & '.\deswik-mcp\src\Deswik.Bridge.Standalone\bin\Release\net8.0\Deswik.Bridge.Standalone.exe'
   ```

   Keep that window open.
2. In Deswik.CAD: Tools → Plugin Manager → Add → select the built
   `Deswik.Addin.dll` → Load. A dock panel shows the bridge connection state.
3. Talk to it:

```powershell
. deswik-mcp/tools/deswik.ps1
dsw get_cad_document
dsw get_cad_layers
dsw get_cad_selection
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
