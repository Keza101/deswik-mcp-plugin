# Deswik MCP Plugin

A Deswik.CAD add-in that exposes a running CAD session over a local TCP
bridge, so external tools (MCP servers, Claude, PowerShell, Python) can read
layers, entities, selection and geometry, and draw polylines, text and drill
holes programmatically.

**Not affiliated with or endorsed by Deswik.** No Deswik binaries or
documentation are distributed; a licensed Deswik.Suite installation is
required.

## Pages

- [Plugin Contract](Plugin-Contract.md) — how Deswik.CAD loads external plugins (the
  reflection-based contract this add-in implements)
- [Wire Protocol](Wire-Protocol.md) — the TCP/JSON protocol and capability routing
- [CAD Actions](CAD-Actions.md) — every action the add-in exposes, with parameters
- [Troubleshooting](Troubleshooting.md) — the gotchas, each one earned the hard way

## Architecture

```
Deswik.CAD
  └─ Deswik.Addin  (plugin, loaded via Plugin Manager)
        │  registers capabilities
        ▼
  Deswik.Bridge.Standalone  (TCP router, 127.0.0.1:9595)
        ▲
        │  newline-delimited JSON
  your client (PowerShell dsw, Python socket, MCP server…)
```

The bridge is multi-add-in: each add-in registers a capability list and the
router forwards each request to whichever add-in claimed that action.

## Quick start

```powershell
dotnet run --project deswik-mcp/src/Deswik.Bridge.Standalone   # 1. bridge
# 2. Deswik.CAD → Tools → Plugin Manager → Add → Deswik.Addin.dll → Load
. deswik-mcp/tools/deswik.ps1                                  # 3. client
dsw get_cad_layers
```
