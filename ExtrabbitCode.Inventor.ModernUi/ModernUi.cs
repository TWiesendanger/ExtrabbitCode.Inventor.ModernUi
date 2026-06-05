using System.IO.Packaging;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ExtrabbitCode.Inventor.ModernUi;

/// <summary>
/// Applies the Modern UI theme and styles to a single <see cref="Window"/>, window-scoped.
/// <para>
/// Design guarantee: this never touches <see cref="Application"/>.<c>Current.Resources</c> and
/// registers no dependency properties against framework types, so two copies/versions of this
/// library can be loaded into one process (e.g. several Inventor add-ins) without conflicts.
/// </para>
/// </summary>
public static class ModernUi
{
    static ModernUi()
    {
        // Ensure the pack:// URI scheme is registered even when this library is hosted without a
        // WPF Application (e.g. inside Inventor.exe). Touching PackUriHelper triggers registration.
        _ = PackUriHelper.UriSchemePack;
    }

    // Relative component paths, loaded from THIS assembly. Colors and font are injected directly
    // into the window's resources (below), so these dictionaries reference only framework types
    // and string resource keys — nothing that can collide or cross-cast between two copies.
    private static readonly string[] StyleDictionaries =
    [
        "Shared.xaml",
        "Controls/Window.xaml",
        "Controls/Text.xaml",
        "Controls/Button.xaml",
        "Controls/TextBox.xaml",
        "Controls/CheckBox.xaml",
        "Controls/RadioButton.xaml",
        "Controls/ComboBox.xaml",
        "Controls/Card.xaml",
        "Controls/ProgressBar.xaml",
        "Controls/ToggleSwitch.xaml",
        "Controls/Expander.xaml",

        // Demo-only: the two library builds (V1 / V2) ship a DIFFERENT file at this same logical
        // path, to exercise version coexistence. Harmless in the product (its keys are namespaced
        // "Coexistence.*" and referenced by nothing unless a consumer opts in).
        "Controls/VersionShowcase.xaml"
    ];

    /// <summary>
    /// Applies the theme, colors, font and control styles to <paramref name="window"/>'s own
    /// resources. Safe to call once per window (typically in its constructor).
    /// </summary>
    /// <param name="window">The window to theme.</param>
    /// <param name="theme">Light or Dark. Selects the default palette when <paramref name="palette"/> is null.</param>
    /// <param name="palette">Optional color override. Defaults to <see cref="ThemePalette.For"/>.</param>
    /// <param name="font">Optional font. Defaults to <see cref="FontOptions.Default"/>.</param>
    public static void Apply(Window window, Theme theme, ThemePalette? palette = null, FontOptions? font = null)
    {
        ArgumentNullException.ThrowIfNull(window);

        ResourceDictionary res = window.Resources;

        // 1. Colors — built in code from the palette and inserted as window-scoped brushes.
        ApplyPalette(res, palette ?? ThemePalette.For(theme));

        // 2. Font tokens — derived from the (optionally Inventor-provided) base size.
        ApplyFont(res, font ?? FontOptions.Default);

        // 3. Control styles — merged into THIS window's resources only.
        foreach (string path in StyleDictionaries)
        {
            res.MergedDictionaries.Add(LoadDictionary(path));
        }

        // 4. Assign the window chrome style explicitly (a Window does not reliably pick up an
        //    implicit style from its own resources).
        window.SetResourceReference(FrameworkElement.StyleProperty, "ModernWindowStyle");

        // 5. Wire the custom title-bar caption buttons (min / max / restore / close).
        WireCaptionCommands(window);
    }

    /// <summary>
    /// Switches an already-themed window to another theme at runtime. Only the colors change; the
    /// merged control styles and the font are left untouched. All styles use <c>DynamicResource</c>,
    /// so the window re-colors live.
    /// </summary>
    /// <param name="window">A window previously passed to <see cref="Apply"/> (or a <see cref="ModernWindow"/>).</param>
    /// <param name="theme">The new theme. Selects the default palette when <paramref name="palette"/> is null.</param>
    /// <param name="palette">Optional color override.</param>
    public static void SetTheme(Window window, Theme theme, ThemePalette? palette = null)
    {
        ArgumentNullException.ThrowIfNull(window);
        ApplyPalette(window.Resources, palette ?? ThemePalette.For(theme));
    }

    /// <summary>Inserts the <c>Brush.*</c> resources from a palette into a resource dictionary.</summary>
    internal static void ApplyPalette(ResourceDictionary res, ThemePalette p)
    {
        res["Brush.Background"] = Frozen(p.Background);
        res["Brush.Panel"] = Frozen(p.Panel);
        res["Brush.Control"] = Frozen(p.Control);
        res["Brush.Foreground"] = Frozen(p.Foreground);
        res["Brush.ForegroundMuted"] = Frozen(p.ForegroundMuted);
        res["Brush.Border"] = Frozen(p.Border);
        res["Brush.Accent"] = Frozen(p.Accent);
        res["Brush.AccentMuted"] = Frozen(p.AccentMuted);
        res["Brush.Error"] = Frozen(p.Error);
    }

    /// <summary>Inserts the <c>Font.*</c> resources derived from <paramref name="font"/>.</summary>
    internal static void ApplyFont(ResourceDictionary res, FontOptions font)
    {
        res["Font.Family"] = font.Family;
        res["Font.Size.Normal"] = font.NormalSize;
        res["Font.Size.Small"] = Math.Round(font.NormalSize * 0.85);
        res["Font.Size.Title"] = font.NormalSize + 2d;
    }

    private static SolidColorBrush Frozen(Color color)
    {
        SolidColorBrush brush = new(color);
        brush.Freeze();
        return brush;
    }

    private static ResourceDictionary LoadDictionary(string relativePath)
    {
        AssemblyName name = typeof(ModernUi).Assembly.GetName();

        // Version-hardened pack URI: the ";v1.2.3.4;component" segment pins the lookup to THIS
        // assembly's exact version. When two builds of this library share one process (e.g. two
        // Inventor add-ins), each window then resolves its OWN version's dictionary instead of
        // whichever copy WPF happened to load first. If the version-qualified form can't be
        // resolved in some host, we fall back to the simple-name URI — worst case cosmetic (the
        // other version's identical-shaped, framework-typed styles), never a crash.
        Version? version = name.Version;
        if (version is not null)
        {
            try
            {
                Uri versioned = new($"/{name.Name};v{version};component/{relativePath}", UriKind.Relative);
                return new ResourceDictionary { Source = versioned };
            }
            catch
            {
                // Fall through to the unversioned URI below.
            }
        }

        Uri uri = new($"/{name.Name};component/{relativePath}", UriKind.Relative);
        return new ResourceDictionary { Source = uri };
    }

    private static void WireCaptionCommands(Window window)
    {
        AddBinding(window, SystemCommands.MinimizeWindowCommand, (_, _) => SystemCommands.MinimizeWindow(window));
        AddBinding(window, SystemCommands.MaximizeWindowCommand, (_, _) => SystemCommands.MaximizeWindow(window));
        AddBinding(window, SystemCommands.RestoreWindowCommand, (_, _) => SystemCommands.RestoreWindow(window));
        AddBinding(window, SystemCommands.CloseWindowCommand, (_, _) => SystemCommands.CloseWindow(window));
    }

    private static void AddBinding(Window window, ICommand command, ExecutedRoutedEventHandler handler)
        => window.CommandBindings.Add(new CommandBinding(command, handler));
}
