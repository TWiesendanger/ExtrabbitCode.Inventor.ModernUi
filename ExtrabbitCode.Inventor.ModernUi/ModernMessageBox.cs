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
        var window = new ModernWindow(theme, palette, font)
        {
            Title = title,
            Owner = owner,
            WindowStartupLocation = owner is null
                ? WindowStartupLocation.CenterScreen
                : WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
        };

        // X / Esc default to the safe outcome.
        ModernDialogResult result = buttons switch
        {
            ModernDialogButtons.Ok => ModernDialogResult.Ok,
            ModernDialogButtons.YesNo => ModernDialogResult.No,
            _ => ModernDialogResult.Cancel,
        };

        var root = new Grid { Margin = new Thickness(24, 22, 24, 18) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };
        FrameworkElement? glyph = BuildIcon(icon);
        if (glyph is not null)
        {
            header.Children.Add(glyph);
        }

        var text = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 400,
            VerticalAlignment = VerticalAlignment.Center,
        };
        header.Children.Add(text);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var buttonBar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        Grid.SetRow(buttonBar, 1);
        root.Children.Add(buttonBar);

        void Add(string content, ModernDialogResult value, bool isDefault, bool isCancel, bool accent)
        {
            var button = new Button
            {
                Content = content,
                MinWidth = 92,
                Margin = new Thickness(8, 0, 0, 0),
                IsDefault = isDefault,
                IsCancel = isCancel,
            };
            if (accent)
            {
                button.SetResourceReference(FrameworkElement.StyleProperty, "AccentButton");
            }

            button.Click += (_, _) =>
            {
                result = value;
                window.Close();
            };
            buttonBar.Children.Add(button);
        }

        switch (buttons)
        {
            case ModernDialogButtons.Ok:
                Add("OK", ModernDialogResult.Ok, isDefault: true, isCancel: true, accent: true);
                break;
            case ModernDialogButtons.OkCancel:
                Add("OK", ModernDialogResult.Ok, isDefault: true, isCancel: false, accent: true);
                Add("Cancel", ModernDialogResult.Cancel, isDefault: false, isCancel: true, accent: false);
                break;
            case ModernDialogButtons.YesNo:
                Add("Yes", ModernDialogResult.Yes, isDefault: true, isCancel: false, accent: true);
                Add("No", ModernDialogResult.No, isDefault: false, isCancel: true, accent: false);
                break;
            case ModernDialogButtons.YesNoCancel:
                Add("Yes", ModernDialogResult.Yes, isDefault: true, isCancel: false, accent: true);
                Add("No", ModernDialogResult.No, isDefault: false, isCancel: false, accent: false);
                Add("Cancel", ModernDialogResult.Cancel, isDefault: false, isCancel: true, accent: false);
                break;
        }

        window.Content = root;

        // Size the window explicitly from a measure pass. SizeToContent is unreliable with
        // WindowChrome (leaves the HWND oversized → a black region), so we avoid it: measure the
        // content (resources resolve via the window's logical tree) and add the title bar + borders.
        const double maxContentWidth = 520;
        const double captionHeight = 34;
        const double borders = 2;
        root.Measure(new Size(maxContentWidth, double.PositiveInfinity));
        Size content = root.DesiredSize;
        window.Width = Math.Max(340, content.Width + borders);
        window.Height = content.Height + captionHeight + borders;

        window.ShowDialog();
        return result;
    }

    private static FrameworkElement? BuildIcon(ModernDialogIcon icon)
    {
        if (icon == ModernDialogIcon.None)
        {
            return null;
        }

        // Segoe Fluent Icons glyphs.
        (int glyph, string? brushKey, string? hardColor) = icon switch
        {
            ModernDialogIcon.Info => (0xE946, "Brush.Accent", (string?)null),
            ModernDialogIcon.Warning => (0xE7BA, (string?)null, "#E0A52E"),
            ModernDialogIcon.Error => (0xE783, "Brush.Error", (string?)null),
            ModernDialogIcon.Question => (0xE897, "Brush.Accent", (string?)null),
            _ => (0, (string?)null, (string?)null),
        };

        var tb = new TextBlock
        {
            Text = char.ConvertFromUtf32(glyph),
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 26,
            Margin = new Thickness(0, 0, 14, 0),
            VerticalAlignment = VerticalAlignment.Top,
        };
        if (brushKey is not null)
        {
            tb.SetResourceReference(TextBlock.ForegroundProperty, brushKey);
        }
        else if (hardColor is not null)
        {
            tb.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hardColor)!);
        }

        return tb;
    }
}
