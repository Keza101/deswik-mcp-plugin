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
  routes requests to whichever add-in registered the capability.
- `deswik-mcp/tools/deswik.ps1` — PowerShell client
  (`. deswik.ps1; dsw get_layers`).

## Build

```powershell
# point DeswikDir at your install if it differs from the csproj default
dotnet build deswik-mcp/src/Deswik.Addin/Deswik.Addin.csproj -c Release `
  -p:DeswikDir="D:\Program Files\Deswik\Deswik.Suite 2025.2"
dotnet build deswik-mcp/src/Deswik.Bridge.Standalone -c Release
```

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

## Wire protocol

One JSON object per line over TCP (UTF-8, **no BOM**):
request `{ "id": "...", "action": "...", "params": { } }` →
response `{ "id": "...", "success": true, "data": { } }`.
Use a fresh unique `id` per request — duplicate ids confuse the 15 s timeout
bookkeeping.

## Notes

- The plugin DLL is locked while Deswik.CAD runs; close CAD before rebuilding.
- Set `AssemblyVersion` to match your Deswik build to silence the version
  warning in the plugin log.
