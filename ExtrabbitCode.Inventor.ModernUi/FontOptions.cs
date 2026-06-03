using System.Windows;
using System.Windows.Media;

namespace ExtrabbitCode.Inventor.ModernUi;

/// <summary>
/// The font applied window-scoped to all themed controls. Inventor exposes its UI font through
/// <c>Application.GeneralOptions.TextAppearance</c> (family) and <c>TextSize</c> (base size); the
/// host add-in reads those and passes them in via <see cref="FromInventor"/> so dialogs match
/// Inventor exactly. Outside Inventor, <see cref="Default"/> uses the Windows UI font.
/// </summary>
/// <remarks>Creates font options from a family and a base ("normal") size in device-independent pixels.</remarks>
public readonly struct FontOptions(FontFamily family, double normalSize)
{

    /// <summary>Font family. Mapped to the <c>Font.Family</c> resource key.</summary>
    public FontFamily Family { get; } = family ?? throw new ArgumentNullException(nameof(family));

    /// <summary>Base font size. Mapped to <c>Font.Size.Normal</c>; small/title are derived from it.</summary>
    public double NormalSize { get; } = normalSize > 0 ? normalSize : 12d;

    /// <summary>The Windows UI font (used when no Inventor-provided font is available).</summary>
    public static FontOptions Default { get; } = new(
        SystemFonts.MessageFontFamily,
        SystemFonts.MessageFontSize > 0 ? SystemFonts.MessageFontSize : 12d);

    /// <summary>
    /// Builds font options from the values Inventor reports
    /// (<c>Application.GeneralOptions.TextAppearance</c> and <c>TextSize</c>). Falls back to
    /// <see cref="Default"/> for blank/invalid values.
    /// </summary>
    /// <param name="textAppearance">Inventor's UI font family name.</param>
    /// <param name="textSizePoints">
    /// Inventor's UI font size in <b>points</b> (as reported by <c>GeneralOptions.TextSize</c>).
    /// Converted to device-independent pixels for WPF, which is why Inventor "8" renders at ~10.7px.
    /// </param>
    public static FontOptions FromInventor(string? textAppearance, double textSizePoints)
    {
        FontFamily family = string.IsNullOrWhiteSpace(textAppearance)
            ? Default.Family
            : new FontFamily(textAppearance);

        // WPF font sizes are device-independent pixels (1/96"); Inventor reports points (1/72").
        double size = textSizePoints > 0 ? textSizePoints * 96.0 / 72.0 : Default.NormalSize;
        return new FontOptions(family, size);
    }
}
