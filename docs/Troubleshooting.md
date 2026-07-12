# Troubleshooting

Every entry here was hit for real during development.

## "Constructor not found" when loading the plugin
The loader needs a `ctor(Deswik.Graphics.Application)`. A parameterless
constructor alone is not enough.

## Plugin DLL rejected on Add
Don't implement `IPlugin` — it's internal to Deswik and implementing it gets
the assembly rejected. Use the duck-typed contract ([Plugin Contract](Plugin-Contract.md)).

## MSB3027 / MSB3021: file locked by Deswik.CAD
CAD holds the plugin DLL while running. Close Deswik, rebuild, reopen,
reload the plugin. There is no hot reload.

## First request after connect fails to parse
Your writer emitted a UTF-8 BOM. Use `new UTF8Encoding(false)`.

## Requests randomly time out after working once
You reused a request `id`. The bridge's 15 s timeout timer for the old
request fires and kills the new one. Always send fresh unique ids.

## Port 9595 already in use
A bridge instance is already running — reuse it instead of starting another.

## Version warning in the plugin log
`Plugin "…" version "1.0.0.0" does not match application version …` —
harmless; silence it by setting `AssemblyVersion` to the Deswik build number.

## Scheduler actions answered by the wrong add-in
Capability routing is last-registration-wins. If two add-ins register the
same action, the later registration silently hijacks it. Keep capability
lists disjoint.

## Drawn drill holes show blank ID / zero length in UGDB
Populate `Hole` (the ID column) and `OriginalLength` on each hole —
`draw_cad_ugdrillholes` does this for you.

## Selection disappears after drawing
Deswik clears the selection after entities are added. Re-select before the
next selection-based command.
