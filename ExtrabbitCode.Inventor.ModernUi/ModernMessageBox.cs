using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ExtrabbitCode.Inventor.ModernUi;

/// <summary>The button sets a <see cref="ModernMessageBox"/> can show.</summary>
public enum ModernDialogButtons
{
    /// <summary>A single OK button.</summary>
    Ok,

    /// <summary>OK and Cancel.</summary>
    OkCancel,

    /// <summary>Yes and No.</summary>
    YesNo,

    /// <summary>Yes, No and Cancel.</summary>
    YesNoCancel,
}

/// <summary>The outcome of a <see cref="ModernMessageBox"/>.</summary>
public enum ModernDialogResult
{
    /// <summary>Dismissed without a choice.</summary>
    None,

    /// <summary>OK.</summary>
    Ok,

    /// <summary>Cancel (also the result when closed via the title bar).</summary>
    Cancel,

    /// <summary>Yes.</summary>
    Yes,

    /// <summary>No.</summary>
    No,
}

/// <summary>The leading glyph shown next to the message.</summary>
public enum ModernDialogIcon
{
    /// <summary>No icon.</summary>
    None,

    /// <summary>Informational (accent).</summary>
    Info,

    /// <summary>Warning (amber).</summary>
    Warning,

    /// <summary>Error (error color).</summary>
    Error,

    /// <summary>Question (accent).</summary>
    Question,
}

/// <summary>
/// One button in a <see cref="ModernMessageBox"/> built with a custom button set. Lets a caller use
/// its own captions (e.g. "Enable (recommended)" / "Disable") and map each to a result.
/// </summary>
/// <param name="Label">The button caption.</param>
/// <param name="Result">The result returned when this button is clicked.</param>
/// <param name="IsDefault">True if this is the default button (activated by Enter).</param>
/// <param name="IsCancel">True if this is the cancel button (activated by Esc); also the result used
/// when the dialog is closed via the title bar.</param>
/// <param name="Accent">True to render this button with the accent (primary) style.</param>
public readonly record struct ModernDialogButton(
    string Label,
    ModernDialogResult Result,
    bool IsDefault = false,
    bool IsCancel = false,
    bool Accent = false);

/// <summary>
/// A small themed replacement for <see cref="MessageBox"/>, built on <see cref="ModernWindow"/> so it
/// matches the theme and stays conflict-free (no custom controls, no dependency-property registration).
/// </summary>
public static class ModernMessageBox
{
    /// <summary>Shows a modal themed message box and returns the user's choice.</summary>
    /// <param name="owner">Owner window (centers on it and stays on top); may be null.</param>
    /// <param name="theme">Light or Dark.</param>
    /// <param name="message">The message text.</param>
    /// <param name="title">Title-bar caption.</param>
    /// <param name="buttons">Which buttons to show.</param>
    /// <param name="icon">Optional leading glyph.</param>
    /// <param name="palette">Optional color override.</param>
    /// <param name="font">Optional font (e.g. <c>FontOptions.FromInventor(...)</c>).</param>
    public static ModernDialogResult Show(
        Window? owner,
        Theme theme,
        string message,
        string title = "",
        ModernDialogButtons buttons = ModernDialogButtons.Ok,
        ModernDialogIcon icon = ModernDialogIcon.None,
        ThemePalette? palette = null,
        FontOptions? font = null)
    {
        return Show(owner, theme, message, title, ToButtonList(buttons), icon, palette, font);
    }

    /// <summary>
    /// Shows a modal themed message box with a custom button set (your own captions / results), with a
    /// plain-text message.
    /// </summary>
    public static ModernDialogResult Show(
        Window? owner,
        Theme theme,
        string message,
        string title,
        IReadOnlyList<ModernDialogButton> buttons,
        ModernDialogIcon icon = ModernDialogIcon.None,
        ThemePalette? palette = null,
        FontOptions? font = null)
    {
        TextBlock text = new()
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 400,
            VerticalAlignment = VerticalAlignment.Center,
        };
        return Show(owner, theme, text, title, buttons, icon, palette, font);
    }

    /// <summary>
    /// Shows a modal themed message box with arbitrary <paramref name="content"/> (e.g. a rich block
    /// with a hyperlink) and a custom button set. This is the core overload the others delegate to.
    /// </summary>
    public static ModernDialogResult Show(
        Window? owner,
        Theme theme,
        FrameworkElement content,
        string title,
        IReadOnlyList<ModernDialogButton> buttons,
        ModernDialogIcon icon = ModernDialogIcon.None,
        ThemePalette? palette = null,
        FontOptions? font = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (buttons is null || buttons.Count == 0)
        {
            throw new ArgumentException("At least one button is required.", nameof(buttons));
        }

        // Match the owner's current colors (e.g. a customized accent) unless an explicit palette is
        // given.
        ThemePalette? effective = palette ?? ThemePalette.TryInheritFrom(owner, theme);

        ModernWindow window = new(theme, effective, font)
        {
            Title = title,
            Owner = owner,
            WindowStartupLocation = owner is null
                ? WindowStartupLocation.CenterScreen
                : WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
        };

        // X / Esc default to the cancel button's outcome (or None if there is no cancel button).
        ModernDialogResult result = ModernDialogResult.None;
        foreach (ModernDialogButton b in buttons)
        {
            if (b.IsCancel)
            {
                result = b.Result;
                break;
            }
        }

        Grid root = new() { Margin = new Thickness(24, 22, 24, 18) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        StackPanel header = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };
        FrameworkElement? glyph = BuildIcon(icon);
        if (glyph is not null)
        {
            header.Children.Add(glyph);
        }

        header.Children.Add(content);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        StackPanel buttonBar = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        Grid.SetRow(buttonBar, 1);
        root.Children.Add(buttonBar);

        foreach (ModernDialogButton spec in buttons)
        {
            Button button = new()
            {
                Content = spec.Label,
                MinWidth = 92,
                Margin = new Thickness(8, 0, 0, 0),
                IsDefault = spec.IsDefault,
                IsCancel = spec.IsCancel,
            };
            if (spec.Accent)
            {
                button.SetResourceReference(FrameworkElement.StyleProperty, "AccentButton");
            }

            ModernDialogResult value = spec.Result;
            button.Click += (_, _) =>
            {
                result = value;
                window.Close();
            };
            buttonBar.Children.Add(button);
        }

        window.Content = root;

        // Size the window explicitly from a measure pass. SizeToContent is unreliable with
        // WindowChrome (leaves the HWND oversized → a black region), so we avoid it: measure the
        // content (resources resolve via the window's logical tree) and add the title bar + borders.
        const double maxContentWidth = 520;
        const double captionHeight = 34;
        const double borders = 2;
        root.Measure(new Size(maxContentWidth, double.PositiveInfinity));
        Size desired = root.DesiredSize;
        window.Width = Math.Max(340, desired.Width + borders);
        window.Height = desired.Height + captionHeight + borders;

        window.ShowDialog();
        return result;
    }

    /// <summary>Maps a built-in <see cref="ModernDialogButtons"/> set to an explicit button list,
    /// preserving the default / cancel / accent assignments.</summary>
    private static IReadOnlyList<ModernDialogButton> ToButtonList(ModernDialogButtons buttons) => buttons switch
    {
        ModernDialogButtons.Ok =>
        [
            new ModernDialogButton("OK", ModernDialogResult.Ok, IsDefault: true, IsCancel: true, Accent: true),
        ],
        ModernDialogButtons.OkCancel =>
        [
            new ModernDialogButton("OK", ModernDialogResult.Ok, IsDefault: true, Accent: true),
            new ModernDialogButton("Cancel", ModernDialogResult.Cancel, IsCancel: true),
        ],
        ModernDialogButtons.YesNo =>
        [
            new ModernDialogButton("Yes", ModernDialogResult.Yes, IsDefault: true, Accent: true),
            new ModernDialogButton("No", ModernDialogResult.No, IsCancel: true),
        ],
        ModernDialogButtons.YesNoCancel =>
        [
            new ModernDialogButton("Yes", ModernDialogResult.Yes, IsDefault: true, Accent: true),
            new ModernDialogButton("No", ModernDialogResult.No),
            new ModernDialogButton("Cancel", ModernDialogResult.Cancel, IsCancel: true),
        ],
        _ => throw new ArgumentOutOfRangeException(nameof(buttons), buttons, null),
    };

    private static FrameworkElement? BuildIcon(ModernDialogIcon icon)
    {
        if (icon == ModernDialogIcon.None)
        {
            return null;
        }

        // Segoe Fluent Icons glyphs, colored from the themed status brushes.
        (int glyph, string brushKey) = icon switch
        {
            ModernDialogIcon.Info => (0xE946, "Brush.Accent"),
            ModernDialogIcon.Warning => (0xE7BA, "Brush.Warning"),
            ModernDialogIcon.Error => (0xE783, "Brush.Error"),
            _ => (0xE897, "Brush.Accent"), // Question
        };

        TextBlock tb = new()
        {
            Text = char.ConvertFromUtf32(glyph),
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 26,
            Margin = new Thickness(0, 0, 14, 0),
            VerticalAlignment = VerticalAlignment.Top,
        };
        tb.SetResourceReference(TextBlock.ForegroundProperty, brushKey);
        return tb;
    }
}
