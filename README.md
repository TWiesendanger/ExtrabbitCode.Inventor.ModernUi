# ExtrabbitCode.Inventor.ModernUi

A small WPF styling library that gives Inventor add-in dialogs a consistent, Inventor-aligned look
(light + dark) **without** the process-global WPF state that makes UI libraries collide between
add-ins. It is **styles on standard WPF controls** — not a control framework.

The bar it clears: **two different versions of this library can be loaded into one `Inventor.exe`
(multiple add-ins) and used at once with zero conflicts** — worst case is cosmetic, never an
exception. This is verified by an automated two-`AssemblyLoadContext` test.

## Projects

| Project | Type | What it's for |
|---|---|---|
| `ExtrabbitCode.Inventor.ModernUi` | Class library (the deliverable) | The styling library. Public API (`Theme`, `ThemePalette`, `FontOptions`, `ModernWindow`, `ModernUi.Apply`/`SetTheme`) plus the control styles (`Controls/*.xaml`), design tokens (`Shared.xaml`) and the WindowChrome title bar. The only thing a real add-in references. |
| `ExtrabbitCode.Inventor.ModernUi.Gallery` | WPF desktop app (.exe) | Runs **without Inventor** for fast visual iteration: opens the paged control gallery in light/dark; `--shoot <dir>` renders it to PNG. Also the home of `GalleryView` — the showcase UserControl with every styled control. |
| `ExtrabbitCode.Inventor.ModernUi.Demo.AddIn` | Inventor 2025+ add-in (COM class library) | The in-Inventor demo. Reads the active Inventor theme + UI font and shows the gallery in a themed dialog. Embeds a native manifest for registration-free COM and auto-deploys on build. Links `GalleryView` from the Gallery project (no duplication). |
| `ExtrabbitCode.Inventor.ModernUi.ConflictTest` | Console app | The acceptance test: builds **two different versions** of the library (V1 + V2, each with a version-only method), loads each into its own isolated `AddinLoadContext`, and in each calls its version-only method **and** themes a window — proving two add-ins shipping two library versions coexist in one process. Prints `RESULT: PASS`. |

Dependency flow: everything references the **library**; the **add-in** and **Gallery** share `GalleryView`; the **ConflictTest** loads the library by reflection (to get two isolated copies). The library, Gallery and ConflictTest build and run anywhere; only the **Demo.AddIn** needs Inventor installed to load.

## Using it in an add-in

```csharp
// Read the theme + font from Inventor, then apply window-scoped.
Theme theme = app.ThemeManager.ActiveTheme.Name == "LightTheme" ? Theme.Light : Theme.Dark;
FontOptions font = FontOptions.FromInventor(app.GeneralOptions.TextAppearance, app.GeneralOptions.TextSize);

var dialog = new ModernWindow(theme, font: font) { Title = "My dialog", Content = myView };
new WindowInteropHelper(dialog) { Owner = new IntPtr(app.MainFrameHWND) };
dialog.Show();
```

Standard controls (`Button`, `TextBox`, `CheckBox`, `ComboBox`, …) inside the window are themed
automatically. Keyed variants: `AccentButton`, `Card`, `ToggleSwitch`, `TitleTextStyle`,
`BodyTextStyle`, `CaptionTextStyle`.

## Changing colors

Colors live in one place — the `ThemePalette` record (`ThemePalette.cs`). To re-skin without
forking, override at apply-time:

```csharp
ModernUi.Apply(window, Theme.Dark,
    ThemePalette.Dark with { Accent = (Color)ColorConverter.ConvertFromString("#FF8A00") });
```

Non-color tokens (corner radius, control height, paddings) live in `Shared.xaml`.

## Design rules (why it is conflict-free)

- No custom dependency properties registered against framework types.
- No custom controls except `ModernWindow : Window` (own type, never cast across copies; no
  framework-type DPs; never referenced from XAML).
- Window-scoped resources only — **never** `Application.Current.Resources`.
- Colors/font built in code and injected per window; style XAML references only framework types and
  string resource keys via `DynamicResource`.

## Build & verify

```
dotnet build
dotnet run --project ExtrabbitCode.Inventor.ModernUi.ConflictTest   # expect RESULT: PASS
dotnet run --project ExtrabbitCode.Inventor.ModernUi.Gallery         # interactive preview
```

See the add-in's own `README.md` for installing it into Inventor.
