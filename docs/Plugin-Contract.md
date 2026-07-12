# Plugin Contract

Deswik.CAD's Plugin Manager does **not** load plugins through a public
interface. `Deswik.Common.Plugins.Host.IPlugin` is internal — a DLL that
implements it gets **rejected**. Instead the loader reflects over a naming
convention. Your plugin class must provide **all four** of:

```csharp
public class MyPlugin
{
    // 1. REQUIRED: constructor taking the CAD application object.
    //    A parameterless ctor alone fails with "Constructor not found".
    public MyPlugin(Deswik.Graphics.Application app) { Application = app; }

    // 2. Application property (getter+setter), injected by the host
    public Deswik.Graphics.Application Application { get; set; }

    // 3. Load — called on the UI thread when the user clicks Load.
    //    Overloads with (IWin32Window) or (IWin32Window, string[]) both work.
    public void Load(IWin32Window owner) { }

    // 4. Unload — called when the plugin is unloaded / CAD closes
    public void Unload() { }
}
```

## Dock panel

`Application.RegisterMainControl(...)` registers a WinForms control as a
dockable panel inside CAD — useful for a status/log UI.

## UI thread

`Load` runs on the UI thread; bridge callbacks arrive on socket threads.
Capture `SynchronizationContext.Current` (or a control handle) during `Load`
and marshal all CAD API calls back onto it, or CAD will throw / corrupt state.

## Version matching

Set `AssemblyVersion`/`FileVersion` to your Deswik build number (e.g.
`2025.2.3203.55048`), otherwise the host logs
`Plugin "..." version "1.0.0.0" does not match application version …`
(warning only, but noisy).

## Target framework

Deswik.Suite 2025.2 hosts .NET 8 (`net8.0-windows`, WinForms). Reference the
Deswik DLLs from the install directory with `<Private>false</Private>` —
never copy them next to your plugin.
