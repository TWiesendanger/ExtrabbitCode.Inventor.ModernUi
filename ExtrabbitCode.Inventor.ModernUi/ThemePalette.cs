using System.Windows.Media;

namespace ExtrabbitCode.Inventor.ModernUi;

/// <summary>
/// The single source of truth for the library's colors. Each member maps to a window-scoped
/// <c>Brush.*</c> resource key that the control styles reference via <c>DynamicResource</c>.
/// <para>
/// Re-skin without forking the library: copy a default and override only what you need, e.g.
/// <code>ThemePalette.Dark with { Accent = (Color)ColorConverter.ConvertFromString("#FF8A00") }</code>
/// or build a complete custom palette and pass it to <see cref="ModernUi.Apply"/>.
/// </para>
/// </summary>
public sealed record ThemePalette
{
    /// <summary>Window base background. Key: <c>Brush.Background</c>.</summary>
    public required Color Background { get; init; }

    /// <summary>Surface / card / panel background. Key: <c>Brush.Panel</c>.</summary>
    public required Color Panel { get; init; }

    /// <summary>Input / sunken control background. Key: <c>Brush.Control</c>.</summary>
    public required Color Control { get; init; }

    /// <summary>Primary text. Key: <c>Brush.Foreground</c>.</summary>
    public required Color Foreground { get; init; }

    /// <summary>Secondary / muted text. Key: <c>Brush.ForegroundMuted</c>.</summary>
    public required Color ForegroundMuted { get; init; }

    /// <summary>Borders / dividers. Key: <c>Brush.Border</c>.</summary>
    public required Color Border { get; init; }

    /// <summary>Accent: primary buttons, focus / selection border. Key: <c>Brush.Accent</c>.</summary>
    public required Color Accent { get; init; }

    /// <summary>Muted accent: hover / selection backgrounds. Key: <c>Brush.AccentMuted</c>.</summary>
    public required Color AccentMuted { get; init; }

    /// <summary>Error / destructive. Key: <c>Brush.Error</c>.</summary>
    public required Color Error { get; init; }

    /// <summary>Default dark palette, aligned to Inventor's dark theme.</summary>
    public static ThemePalette Dark { get; } = new()
    {
        Background = Hex("#3b4453"),
        Panel = Hex("#4b5463"),
        Control = Hex("#2c3340"),
        Foreground = Hex("#f5f5f5"),
        ForegroundMuted = Hex("#c8c8c8"),
        Border = Hex("#2c3340"),
        Accent = Hex("#0696d7"),
        AccentMuted = Hex("#3a7292"),
        Error = Hex("#ec4a41"),
    };

    /// <summary>Default light palette, aligned to Inventor's light theme.</summary>
    public static ThemePalette Light { get; } = new()
    {
        Background = Hex("#f5f5f5"),
        Panel = Hex("#d9d9d9"),
        Control = Hex("#ffffff"),
        Foreground = Hex("#1e1e1e"),
        ForegroundMuted = Hex("#6a6a6a"),
        Border = Hex("#c0c0c0"),
        Accent = Hex("#0696d7"),
        AccentMuted = Hex("#3a7292"),
        Error = Hex("#ec4a41"),
    };

    /// <summary>Returns the built-in default palette for the given <paramref name="theme"/>.</summary>
    public static ThemePalette For(Theme theme) => theme == Theme.Light ? Light : Dark;

    private static Color Hex(string hex) => (Color)ColorConverter.ConvertFromString(hex)!;
}
