# ExtrabbitCode Modern UI — Demo Inventor add-in

A minimal Inventor 2025+ add-in that showcases the `ExtrabbitCode.Inventor.ModernUi` library. It
reads the **active Inventor theme** (`ThemeManager.ActiveTheme`) and the **Inventor UI font**
(`GeneralOptions.TextAppearance` + `TextSize`), then opens a themed dialog with the full control
gallery. Mostly visual — there is no feature behind it.

## How loading works (no regsvr32)

Inventor activates a .NET 8 add-in as an in-process COM server. This project embeds a **native
manifest** ([Addin/…​.manifest](Addin/ExtrabbitCode.Inventor.ModernUi.Demo.AddIn.manifest)) via
`<ApplicationManifest>`; its `clrClass` entry lets Inventor activate the add-in **by CLSID with no
registration** (`regasm`/`regsvr32` are not needed). The CLSID is the same GUID in three places:
the `[Guid]` on `DemoAddInServer`, the manifest `clrClass clsid`, and the `.addin` `ClassId`.

Inventor discovers the add-in from the `.addin`
([Addin/…​.addin](Addin/ExtrabbitCode.Inventor.ModernUi.Demo.AddIn.addin)) placed in its add-ins
folder; the `.addin`'s `<Assembly>` points at the deployed DLL.

## Build & deploy

```
dotnet build ExtrabbitCode.Inventor.ModernUi.Demo.AddIn.csproj
```

The post-build step ([Addin/BuildScript.cmd](Addin/BuildScript.cmd)) automatically:

1. copies the `.addin` to `C:\ProgramData\Autodesk\Inventor Addins\`, and
2. copies the build output to
   `C:\ProgramData\ExtrabbitCode\ExtrabbitCode.Inventor.ModernUi.Demo.AddIn\` (the path the
   `.addin` references).

So a normal build *is* the install. To build without deploying (CI, or library-only checks):

```
dotnet build -p:DeployToInventor=false
```

## Run / debug

Start Inventor 2025+ (or use the `LaunchInventor_2025/2026/2027` profiles in
`Properties/launchSettings.json` to F5-debug into Inventor). On the **Tools** tab you'll find a
**Modern UI** panel with a **Modern UI Gallery** button — click it to open the themed gallery.
Switch Inventor's theme (Application Options → Colors) and reopen to see the colors and the Inventor
font flow through every control.

## Uninstall

Delete `C:\ProgramData\Autodesk\Inventor Addins\ExtrabbitCode.Inventor.ModernUi.Demo.AddIn.addin`
(and optionally the `C:\ProgramData\ExtrabbitCode\…​` folder).
