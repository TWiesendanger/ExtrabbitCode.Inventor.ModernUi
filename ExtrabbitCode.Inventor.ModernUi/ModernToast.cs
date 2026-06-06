using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace ExtrabbitCode.Inventor.ModernUi;

/// <summary>The semantic kind of a toast (drives its accent color and icon).</summary>
public enum ToastType
{
    /// <summary>Neutral information (accent color).</summary>
    Info,

    /// <summary>A successful action (green).</summary>
    Success,

    /// <summary>A non-blocking caution (amber).</summary>
    Warning,

    /// <summary>A failure (red).</summary>
    Error,
}

/// <summary>
/// Lightweight, auto-dismissing toast notifications shown in the bottom-right of a window.
/// <para>
/// Toasts are hosted inside the owning window's own visual tree (so they move and clip with it and
/// pick up its themed brushes). Nothing is written to <see cref="Application"/>.<c>Current.Resources</c>
/// and no dependency properties or custom controls are introduced, so this stays conflict-free across
/// library versions — exactly like <c>ModernMessageBox</c>.
/// </para>
/// </summary>
public static class ModernToast
{
    // One toast host (a bottom-right StackPanel) per window, created on first use.
    private static readonly ConditionalWeakTable<Window, Panel> Hosts = new();

    /// <summary>
    /// Maximum number of toasts shown at once per window. When a new toast would exceed this, the
    /// oldest is dropped to make room. Minimum 1. Default 3.
    /// </summary>
    public static int MaxVisible { get; set; } = 3;

    /// <summary>
    /// Display duration used when <see cref="Show"/> is called without an explicit duration.
    /// Default 4 seconds.
    /// </summary>
    public static TimeSpan DefaultDuration { get; set; } = TimeSpan.FromSeconds(4);

    // Status accent colors (match the palette's accent/error; green/amber for success/warning).
    private static readonly Color InfoColor = Color.FromRgb(0x06, 0x96, 0xD7);
    private static readonly Color SuccessColor = Color.FromRgb(0x3F, 0xB9, 0x50);
    private static readonly Color WarningColor = Color.FromRgb(0xD2, 0x99, 0x22);
    private static readonly Color ErrorColor = Color.FromRgb(0xEC, 0x4A, 0x41);

    // Segoe Fluent Icons glyphs.
    private const string InfoGlyph = "";     // Info
    private const string SuccessGlyph = "";  // Completed
    private const string WarningGlyph = "";  // Warning
    private const string ErrorGlyph = "";    // ErrorBadge
    private const string CloseGlyph = "";    // Cancel (x)

    /// <summary>Shows a toast in the bottom-right of <paramref name="owner"/>.</summary>
    /// <param name="owner">The window to host the toast (typically a themed <see cref="ModernWindow"/>).</param>
    /// <param name="message">The body text.</param>
    /// <param name="type">Semantic kind — sets the accent color and icon.</param>
    /// <param name="title">Optional bold title shown above the message.</param>
    /// <param name="duration">How long before it auto-dismisses; defaults to <see cref="DefaultDuration"/>. Click the toast to dismiss early.</param>
    public static void Show(Window owner, string message, ToastType type = ToastType.Info,
        string? title = null, TimeSpan? duration = null)
    {
        ArgumentNullException.ThrowIfNull(owner);

        Panel? host = GetOrCreateHost(owner);
        if (host is null)
        {
            return; // window has no wrappable content yet
        }

        FrameworkElement toast = BuildToast(message, title, type);

        // Enforce the visible cap: drop the oldest toasts to make room for the new one.
        int max = Math.Max(1, MaxVisible);
        while (host.Children.Count >= max)
        {
            host.Children.RemoveAt(0);
        }

        host.Children.Add(toast);
        AnimateIn(toast);

        var timer = new DispatcherTimer { Interval = duration ?? DefaultDuration };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Dismiss(host, toast);
        };
        timer.Start();

        toast.MouseLeftButtonUp += (_, _) =>
        {
            timer.Stop();
            Dismiss(host, toast);
        };
    }

    private static Panel? GetOrCreateHost(Window owner)
    {
        if (Hosts.TryGetValue(owner, out Panel? existing))
        {
            return existing;
        }

        // Wrap the window's content once: [ original content ] + [ toast host ] in a Grid.
        if (owner.Content is not UIElement content)
        {
            return null;
        }

        var host = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(16),
        };

        owner.Content = null;
        var grid = new Grid();
        grid.Children.Add(content);
        grid.Children.Add(host);
        owner.Content = grid;

        Hosts.Add(owner, host);
        owner.Closed += (_, _) => Hosts.Remove(owner);
        return host;
    }

    private static FrameworkElement BuildToast(string message, string? title, ToastType type)
    {
        Color accent = type switch
        {
            ToastType.Success => SuccessColor,
            ToastType.Warning => WarningColor,
            ToastType.Error => ErrorColor,
            _ => InfoColor,
        };
        var accentBrush = new SolidColorBrush(accent);
        accentBrush.Freeze();

        var card = new Border
        {
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 8, 0, 0),
            MinWidth = 260,
            MaxWidth = 360,
            SnapsToDevicePixels = true,
            Cursor = System.Windows.Input.Cursors.Hand,
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 12, ShadowDepth = 2, Opacity = 0.35 },
        };
        card.SetResourceReference(Border.BackgroundProperty, "Brush.Panel");
        card.SetResourceReference(Border.BorderBrushProperty, "Brush.Border");

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var bar = new Border { Background = accentBrush, CornerRadius = new CornerRadius(6, 0, 0, 6) };
        Grid.SetColumn(bar, 0);
        grid.Children.Add(bar);

        var icon = new TextBlock
        {
            Text = GlyphFor(type),
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 16,
            Foreground = accentBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
        };
        Grid.SetColumn(icon, 1);
        grid.Children.Add(icon);

        var text = new StackPanel { Margin = new Thickness(10), VerticalAlignment = VerticalAlignment.Center };
        if (!string.IsNullOrEmpty(title))
        {
            var titleBlock = new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 2) };
            titleBlock.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Foreground");
            titleBlock.SetResourceReference(TextBlock.FontFamilyProperty, "Font.Family");
            text.Children.Add(titleBlock);
        }

        var messageBlock = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap };
        messageBlock.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Foreground");
        messageBlock.SetResourceReference(TextBlock.FontFamilyProperty, "Font.Family");
        messageBlock.SetResourceReference(TextBlock.FontSizeProperty, "Font.Size.Normal");
        text.Children.Add(messageBlock);
        Grid.SetColumn(text, 2);
        grid.Children.Add(text);

        var close = new TextBlock
        {
            Text = CloseGlyph,
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 10,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(6, 10, 10, 0),
        };
        close.SetResourceReference(TextBlock.ForegroundProperty, "Brush.ForegroundMuted");
        Grid.SetColumn(close, 3);
        grid.Children.Add(close);

        card.Child = grid;
        return card;
    }

    private static string GlyphFor(ToastType type) => type switch
    {
        ToastType.Success => SuccessGlyph,
        ToastType.Warning => WarningGlyph,
        ToastType.Error => ErrorGlyph,
        _ => InfoGlyph,
    };

    private static void AnimateIn(FrameworkElement toast)
    {
        toast.Opacity = 0;
        var shift = new TranslateTransform(0, 12);
        toast.RenderTransform = shift;

        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180));
        var rise = new DoubleAnimation(12, 0, TimeSpan.FromMilliseconds(180));
        toast.BeginAnimation(UIElement.OpacityProperty, fade);
        shift.BeginAnimation(TranslateTransform.YProperty, rise);
    }

    private static void Dismiss(Panel host, FrameworkElement toast)
    {
        if (!host.Children.Contains(toast))
        {
            return;
        }

        var fade = new DoubleAnimation(toast.Opacity, 0, TimeSpan.FromMilliseconds(180));
        fade.Completed += (_, _) => host.Children.Remove(toast);
        toast.BeginAnimation(UIElement.OpacityProperty, fade);
    }
}
