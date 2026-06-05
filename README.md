# ExtrabbitCode.Inventor.ModernUi

<img src="resources/ModernUi-1024.png" alt="ExtrabbitCode Modern UI" width="120" align="right" />

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
| `ExtrabbitCode.Inventor.ModernUi.ConflictTest` | Console app | Headless acceptance test (the fast CI check): builds **two different versions** of the library (V1 + V2, each with a version-only method), loads each into its own isolated `AddinLoadContext`, and in each calls its version-only method **and** themes a window. Prints `RESULT: PASS`. |
| `ExtrabbitCode.Inventor.ModernUi.Coexistence.A` / `.B` | Inventor 2025+ add-ins (COM class libraries) | The in-Inventor coexistence proof: two *separate* add-ins, **A ships ModernUi V1, B ships V2**, each loading its own copy in an isolated `AddinLoadContext` (`IsolatedApplicationAddInServer`). Each opens a themed dialog showing which version it loaded and the result of its version-only method. Load both in one Inventor session to confirm zero conflicts live. |

Dependency flow: everything references the **library**; the **add-in** and **Gallery** share `GalleryView`; the **ConflictTest** loads the library by reflection (to get two isolated copies). The library, Gallery and ConflictTest build and run anywhere; the **Demo.AddIn** and **Coexistence.A/B** need Inventor installed to load.

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

## Proving coexistence — and why it matters

The library exists because **multiple Inventor add-ins run in one `Inventor.exe`**, and each may ship
its *own* copy/version of a UI library. WPF keeps process-global state — the `DependencyProperty`
registry, `Application.Current.Resources`, and the XAML `xmlns→assembly` cache — in framework
assemblies shared across every `AssemblyLoadContext`. A library that registers DPs against framework
types, writes to `Application.Resources`, or relies on the global xmlns cache will, when two versions
load, crash with one of three failures: `DependencyProperty already registered`, an
`Application.Resources` value clash, or a cross-version `InvalidCastException` (version B's XAML
building version A's controls). This library is built to make all three impossible.

Two layers prove it:

1. **`ConflictTest` (headless, runs in CI).** Builds the library as V1 and V2, loads each in its own
   `AddinLoadContext`, calls a method that exists *only* in that version, and themes a window. Fast,
   deterministic, no Inventor required.

2. **`Coexistence.A` + `Coexistence.B` (the ultimate, in-Inventor test).** Two real add-ins, each
   isolated, **A on ModernUi V1 and B on V2**, loaded into one running Inventor. Why this is the
   strongest possible test:
   - **Different versions, not two copies of one.** The worst real-world crash is *cross-version*
     (the `InvalidCastException` above); two copies of the same version can't surface it. This is the
     realistic case — e.g. one add-in on 1.2, another on 1.5.
   - **Isolation is required, so it's exercised.** The runtime won't load two same-named assemblies
     with different versions into one context, so each add-in *must* isolate its copy. That makes the
     `IsolatedApplicationAddInServer` / `AddinLoadContext` path part of the test.
   - **A version-only method proves no unification.** A calls `GetV1()`, B calls `GetV2()`; each
     lacks the other's method. If both succeed, two genuinely distinct assemblies are running — not
     silently merged into one.
   - **Both dialogs render = no global clash.** Both themed windows opening together, with no
     exception, is the live confirmation that the three failure modes don't happen.

   To run it: build the two add-ins (deploys each with its own library version + `.addin`), start
   Inventor 2025+, and open both add-ins' **Modern UI Coexistence** buttons. Two themed dialogs, one
   reading "ModernUi V1 … GetV1()", the other "ModernUi V2 … GetV2()", with no errors = pass.

## Build & verify

```
dotnet build
dotnet run --project ExtrabbitCode.Inventor.ModernUi.ConflictTest   # expect RESULT: PASS
dotnet run --project ExtrabbitCode.Inventor.ModernUi.Gallery         # interactive preview
```

`-p:DeployToInventor=false` skips the add-in deploy steps (CI/library-only builds);
`-p:BuildCoexistenceVersions=false` skips building the two test versions.

See the add-in's own `README.md` for installing it into Inventor.
