using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ExtrabbitCode.Inventor.ModernUi;

/// <summary>
/// Named Segoe Fluent Icons / Segoe MDL2 Assets glyphs, plus a helper to drop one into a
/// <see cref="TextBlock"/>. This is the library's icon system: there is no icon control and no icon
/// enum to register — icons are just glyph strings rendered in the icon font, exactly like the
/// glyphs already used by <see cref="ModernToast"/>, <see cref="ModernMessageBox"/> and the window
/// caption buttons. Pure strings + a framework <see cref="TextBlock"/> factory, so nothing here can
/// clash across two loaded copies of the library.
/// <para>
/// Each glyph is built from its hex codepoint via <see cref="char.ConvertFromUtf32(int)"/> so the
/// source shows the exact codepoint (no invisible literal characters). The codepoints are in the
/// Private Use Area shared by "Segoe Fluent Icons" (Windows 11) and its fallback
/// "Segoe MDL2 Assets" (Windows 10), so they render on both.
/// </para>
/// </summary>
public static class ModernGlyphs
{
    /// <summary>The icon font stack used by <see cref="Icon"/> and the rest of the library.</summary>
    public const string FontFamily = "Segoe Fluent Icons, Segoe MDL2 Assets";

    /// <summary>Add / new (plus). <c>U+E710</c>.</summary>
    public static readonly string Add = char.ConvertFromUtf32(0xE710);

    /// <summary>Delete (trash can) — e.g. delete the selected item. <c>U+E74D</c>.</summary>
    public static readonly string Delete = char.ConvertFromUtf32(0xE74D);

    /// <summary>Clear all (delete everything in scope). Substitute for WPF-UI "DeleteDismiss". <c>U+E894</c>.</summary>
    public static readonly string DeleteAll = char.ConvertFromUtf32(0xE894);

    /// <summary>Refresh / reload (circular arrow). <c>U+E72C</c>.</summary>
    public static readonly string Refresh = char.ConvertFromUtf32(0xE72C);

    /// <summary>Chevron pointing down — e.g. expand all. <c>U+E70D</c>.</summary>
    public static readonly string ChevronDown = char.ConvertFromUtf32(0xE70D);

    /// <summary>Chevron pointing up — e.g. collapse all. <c>U+E70E</c>.</summary>
    public static readonly string ChevronUp = char.ConvertFromUtf32(0xE70E);

    /// <summary>Cleanup / purge (eraser). Substitute for a "broom"; verify visually in the Gallery. <c>U+E75C</c>.</summary>
    public static readonly string Purge = char.ConvertFromUtf32(0xE75C);

    /// <summary>Search (magnifier). <c>U+E721</c>.</summary>
    public static readonly string Search = char.ConvertFromUtf32(0xE721);

    /// <summary>Settings (gear). <c>U+E713</c>.</summary>
    public static readonly string Settings = char.ConvertFromUtf32(0xE713);

    /// <summary>Copy (overlapping pages). <c>U+E8C8</c>.</summary>
    public static readonly string Copy = char.ConvertFromUtf32(0xE8C8);

    /// <summary>Documentation / library (stack of books). Substitute for WPF-UI "Book". <c>U+E736</c>.</summary>
    public static readonly string Book = char.ConvertFromUtf32(0xE736);

    /// <summary>Open in a new/external window — e.g. follow an external link. <c>U+E8A7</c>.</summary>
    public static readonly string OpenExternal = char.ConvertFromUtf32(0xE8A7);

    /// <summary>
    /// Builds a <see cref="TextBlock"/> showing <paramref name="glyph"/> in the icon font. Returns a
    /// plain framework element (no custom type), so it is safe to use across library copies. The
    /// foreground is left unset so it inherits the surrounding context; set it on the result if needed.
    /// </summary>
    /// <param name="glyph">One of the glyph fields on this class (or any icon-font glyph).</param>
    /// <param name="size">Font size in device-independent pixels. Defaults to 16.</param>
    public static TextBlock Icon(string glyph, double size = 16d)
    {
        return new TextBlock
        {
            Text = glyph,
            FontFamily = new FontFamily(FontFamily),
            FontSize = size,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
        };
    }
}
