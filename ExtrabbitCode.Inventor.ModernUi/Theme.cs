namespace ExtrabbitCode.Inventor.ModernUi;

/// <summary>
/// The two visual themes this library supports. The library is Inventor-agnostic: the host
/// add-in decides which theme to use (typically from <c>Inventor.ThemeManager.ActiveTheme.Name</c>)
/// and passes it to <see cref="ModernUi.Apply"/> or <see cref="ModernWindow"/>.
/// </summary>
public enum Theme
{
    /// <summary>Light theme.</summary>
    Light,

    /// <summary>Dark theme.</summary>
    Dark,
}
