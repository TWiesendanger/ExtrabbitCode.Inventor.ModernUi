using System.Windows;

namespace ExtrabbitCode.Inventor.ModernUi;

/// <summary>
/// Convenience <see cref="Window"/> base class that applies the Modern UI theme window-scoped in
/// its constructor. This is the only custom type in the library; it registers no dependency
/// properties against framework types and is never cast across library copies, so it is safe in a
/// multi-add-in process. Prefer composing content (e.g. set the window's <c>Content</c>)
/// rather than subclassing further.
/// </summary>
public class ModernWindow : Window
{
    /// <summary>Creates a dark-themed window (for designer/XAML use).</summary>
    public ModernWindow() : this(Theme.Dark)
    {
    }

    /// <summary>Creates a themed window.</summary>
    /// <param name="theme">Light or Dark.</param>
    /// <param name="palette">Optional color override.</param>
    /// <param name="font">Optional font (e.g. <c>FontOptions.FromInventor(...)</c>).</param>
    public ModernWindow(Theme theme, ThemePalette? palette = null, FontOptions? font = null)
    {
        Width = 760;
        Height = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ModernUi.Apply(this, theme, palette, font);
    }
}
