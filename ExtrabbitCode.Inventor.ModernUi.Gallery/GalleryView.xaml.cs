using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ExtrabbitCode.Inventor.ModernUi.Demo;

/// <summary>
/// Paged control showcase: a left nav switches between pages; each item shows the live styled
/// control above the exact XAML that produces it. Most live controls are built by parsing the same
/// snippet that is displayed (one source of truth); the validation item is built in code because its
/// rule needs a type reference. Demo-only — it uses just standard WPF controls + the library styles.
/// </summary>
public partial class GalleryView : UserControl
{
    // Segoe Fluent Icons glyphs: Brightness (sun, U+E706) and QuietHours (crescent moon, U+EC46).
    private static readonly string SunGlyph = char.ConvertFromUtf32(0xE706);
    private static readonly string MoonGlyph = char.ConvertFromUtf32(0xEC46);

    private readonly List<DemoPage> _pages;
    private Theme _theme;
    private string _detail = string.Empty;
    private Color? _accent;
    private DemoItem? _pendingScrollTo;
    private readonly Stack<DemoPage> _history = new();
    private bool _navigatingBack;

    private const string OverviewPageName = "Overview";

    private sealed record AccentOption(string Name, Color? Color);

    // null = keep the palette's default accent.
    private static readonly AccentOption[] Accents =
    [
        new("Inventor Blue", null),
        new("Orange", Color.FromRgb(0xF2, 0x8C, 0x28)),
        new("Green", Color.FromRgb(0x3F, 0xB9, 0x50)),
        new("Purple", Color.FromRgb(0x89, 0x57, 0xE5)),
        new("Teal", Color.FromRgb(0x1F, 0xA8, 0xA8)),
        new("Pink", Color.FromRgb(0xE5, 0x48, 0x8D)),
    ];

    public GalleryView()
    {
        InitializeComponent();
        _pages = BuildPages();
        NavList.ItemsSource = _pages;
        NavList.SelectedIndex = 0;

        AccentPicker.ItemsSource = Accents;
        AccentPicker.SelectedIndex = 0;

        ImageSource? branding = LoadBranding();
        LogoImage.Source = branding;
        Loaded += (_, _) =>
        {
            Window? window = Window.GetWindow(this);
            if (window is not null && window.Icon is null)
            {
                window.Icon = branding;
            }
        };
    }

    /// <summary>Loads the embedded "ModernUi-64.png" branding icon from the hosting assembly (app or add-in).</summary>
    public static ImageSource? LoadBranding()
    {
        try
        {
            using Stream? stream = typeof(GalleryView).Assembly.GetManifestResourceStream("ModernUi-64.png");
            if (stream is null)
            {
                return null;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Sets the initial theme and the caption detail (e.g. the resolved font).</summary>
    public void Initialize(Theme theme, string detail)
    {
        _theme = theme;
        _detail = detail;
        UpdateInfo();
        UpdateToggleIcon();
    }

    private void OnNavChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavList.SelectedItem is not DemoPage page)
        {
            return;
        }

        // Track history so the back button can retrace the path (unless this IS a back navigation).
        if (!_navigatingBack && e.RemovedItems.Count > 0 && e.RemovedItems[0] is DemoPage previous && previous != page)
        {
            _history.Push(previous);
        }
        BackButton.Visibility = _history.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        PageTitle.Text = page.Name;
        ContentHost.Children.Clear();

        if (page.Name == OverviewPageName)
        {
            ContentHost.Children.Add(BuildOverview());
            return;
        }

        foreach (DemoItem item in page.Items)
        {
            ContentHost.Children.Add(BuildBlock(item));
        }

        // If we navigated here from an overview tile, scroll that control into view once laid out.
        if (_pendingScrollTo is not null)
        {
            DemoItem target = _pendingScrollTo;
            _pendingScrollTo = null;
            Dispatcher.BeginInvoke(
                () =>
                {
                    foreach (FrameworkElement child in ContentHost.Children)
                    {
                        if (ReferenceEquals(child.Tag, target))
                        {
                            child.BringIntoView();
                            break;
                        }
                    }
                },
                DispatcherPriority.Loaded);
        }
    }

    private void OnBack(object sender, RoutedEventArgs e)
    {
        if (_history.Count == 0)
        {
            return;
        }

        DemoPage target = _history.Pop();
        _navigatingBack = true;
        NavList.SelectedItem = target; // fires OnNavChanged synchronously
        _navigatingBack = false;
        BackButton.Visibility = _history.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Navigates to the nav page with the given name (no-op if it does not exist). Used by
    /// the headless "--shoot" capture to render a specific page.</summary>
    public void SelectPage(string name)
    {
        foreach (DemoPage page in _pages)
        {
            if (page.Name == name)
            {
                NavList.SelectedItem = page;
                return;
            }
        }
    }

    // --- overview / home page ----------------------------------------------

    private FrameworkElement BuildOverview()
    {
        var root = new StackPanel();
        foreach (DemoPage page in _pages)
        {
            if (page.Name == OverviewPageName || page.Items.Count == 0)
            {
                continue;
            }

            var header = new TextBlock { Text = page.Name, Margin = new Thickness(2, 10, 0, 8) };
            header.SetResourceReference(StyleProperty, "CaptionTextStyle");
            root.Children.Add(header);

            var wrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
            foreach (DemoItem item in page.Items)
            {
                wrap.Children.Add(BuildOverviewTile(page, item));
            }
            root.Children.Add(wrap);
        }
        return root;
    }

    private FrameworkElement BuildOverviewTile(DemoPage page, DemoItem item)
    {
        var label = new TextBlock
        {
            Text = item.Title,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        label.SetResourceReference(ForegroundProperty, "Brush.Foreground");
        label.SetResourceReference(FontFamilyProperty, "Font.Family");
        label.SetResourceReference(FontSizeProperty, "Font.Size.Normal");

        var tile = new Border
        {
            Width = 200,
            Height = 64,
            Margin = new Thickness(0, 0, 10, 10),
            Cursor = System.Windows.Input.Cursors.Hand,
            Child = label,
        };
        tile.SetResourceReference(StyleProperty, "Card");
        tile.MouseEnter += (_, _) => tile.SetResourceReference(Border.BorderBrushProperty, "Brush.Accent");
        tile.MouseLeave += (_, _) => tile.SetResourceReference(Border.BorderBrushProperty, "Brush.Border");
        tile.MouseLeftButtonUp += (_, _) =>
        {
            _pendingScrollTo = item;
            NavList.SelectedItem = page;
        };
        return tile;
    }

    private void OnToggleTheme(object sender, RoutedEventArgs e)
    {
        _theme = _theme == Theme.Light ? Theme.Dark : Theme.Light;
        ApplyCurrentTheme();
        UpdateInfo();
        UpdateToggleIcon();
    }

    private void OnAccentChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AccentPicker.SelectedItem is AccentOption option)
        {
            _accent = option.Color;
            ApplyCurrentTheme();
        }
    }

    /// <summary>Re-applies the current theme + chosen accent to the host window (re-colors live).</summary>
    private void ApplyCurrentTheme()
    {
        Window? window = Window.GetWindow(this);
        if (window is null)
        {
            return;
        }

        ThemePalette palette = ThemePalette.For(_theme);
        if (_accent is Color accent)
        {
            palette = palette with { Accent = accent, AccentMuted = Muted(accent) };
        }

        ModernUi.SetTheme(window, _theme, palette);
    }

    /// <summary>Derives a calmer companion to an accent by blending it toward the panel color.</summary>
    private static Color Muted(Color c)
    {
        static byte Mix(byte x, byte n) => (byte)((x * 0.6) + (n * 0.4));
        return Color.FromRgb(Mix(c.R, 0x4B), Mix(c.G, 0x54), Mix(c.B, 0x63));
    }

    private void UpdateInfo() => InfoText.Text = $"{_theme}  ·  {_detail}";

    private void UpdateToggleIcon()
    {
        bool dark = _theme == Theme.Dark;
        ThemeToggleIcon.Text = dark ? SunGlyph : MoonGlyph;
        ThemeToggleButton.ToolTip = dark ? "Switch to light theme" : "Switch to dark theme";
    }

    // --- block construction -------------------------------------------------

    private FrameworkElement BuildBlock(DemoItem item)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 24), Tag = item };

        var title = new TextBlock { Text = item.Title, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) };
        title.SetResourceReference(ForegroundProperty, "Brush.Foreground");
        title.SetResourceReference(FontFamilyProperty, "Font.Family");
        title.SetResourceReference(FontSizeProperty, "Font.Size.Normal");
        stack.Children.Add(title);

        // Live control inside a card.
        var card = new Border { Margin = new Thickness(0, 0, 0, 8) };
        card.SetResourceReference(StyleProperty, "Card");
        card.Child = item.Build is not null ? item.Build() : ParseSnippet(item.Xaml);
        stack.Children.Add(card);

        // The XAML lives in a collapsed "XAML" expander so it doesn't crowd the live controls.
        var xaml = new Expander
        {
            Header = "XAML",
            IsExpanded = false,
            Margin = new Thickness(0, 4, 0, 0),
            Content = BuildCodeBox(item.Xaml),
        };
        stack.Children.Add(xaml);
        return stack;
    }

    private static FrameworkElement ParseSnippet(string innerXaml)
    {
        const string ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        const string nsX = "http://schemas.microsoft.com/winfx/2006/xaml";
        string xaml = $"<StackPanel xmlns=\"{ns}\" xmlns:x=\"{nsX}\">{innerXaml}</StackPanel>";
        try
        {
            return (FrameworkElement)XamlReader.Parse(xaml);
        }
        catch (System.Exception ex)
        {
            return new TextBlock { Text = "XAML error: " + ex.Message, Foreground = Brushes.OrangeRed, TextWrapping = TextWrapping.Wrap };
        }
    }

    // --- XAML syntax highlighting (VS-dark palette on a fixed dark code surface) ----------------

    private static readonly Regex XamlToken = new(
        @"(?<comment><!--.*?-->)" +
        @"|(?<tag></?[A-Za-z_][\w\.\-:]*)" +
        @"|(?<close>/?>)" +
        @"|(?<attr>[A-Za-z_][\w\.\-:]*)(?=\s*=)" +
        @"|(?<str>""[^""]*"")" +
        @"|(?<entity>&#?\w+;)",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static SolidColorBrush Rgb(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private static readonly Brush CodeDefault = Rgb(0xD4, 0xD4, 0xD4);
    private static readonly Brush CodeComment = Rgb(0x6A, 0x99, 0x55);
    private static readonly Brush CodeTag = Rgb(0x56, 0x9C, 0xD6);
    private static readonly Brush CodeAttr = Rgb(0x9C, 0xDC, 0xFE);
    private static readonly Brush CodeString = Rgb(0xCE, 0x91, 0x78);
    private static readonly Brush CodeEntity = Rgb(0xD7, 0xBA, 0x7D);
    private static readonly Brush CodePunct = Rgb(0x80, 0x80, 0x80);

    private static Brush ColorFor(Match m) =>
          m.Groups["comment"].Success ? CodeComment
        : m.Groups["tag"].Success ? CodeTag
        : m.Groups["close"].Success ? CodePunct
        : m.Groups["attr"].Success ? CodeAttr
        : m.Groups["str"].Success ? CodeString
        : m.Groups["entity"].Success ? CodeEntity
        : CodeDefault;

    private static void AppendHighlighted(InlineCollection inlines, string code)
    {
        int pos = 0;
        foreach (Match m in XamlToken.Matches(code))
        {
            if (m.Index > pos)
            {
                inlines.Add(new Run(code.Substring(pos, m.Index - pos)) { Foreground = CodeDefault });
            }
            inlines.Add(new Run(m.Value) { Foreground = ColorFor(m) });
            pos = m.Index + m.Length;
        }
        if (pos < code.Length)
        {
            inlines.Add(new Run(code.Substring(pos)) { Foreground = CodeDefault });
        }
    }

    private static FrameworkElement BuildCodeBox(string code)
    {
        var paragraph = new Paragraph { Margin = new Thickness(0) };
        AppendHighlighted(paragraph.Inlines, code);

        var doc = new FlowDocument(paragraph)
        {
            PagePadding = new Thickness(0),
            FontFamily = new FontFamily("Cascadia Code, Consolas, Courier New"),
            FontSize = 12,
            Foreground = CodeDefault,
        };

        var box = new RichTextBox
        {
            Document = doc,
            IsReadOnly = true,
            IsDocumentEnabled = true,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = CodeDefault,
            Padding = new Thickness(0),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };

        // Fixed dark code surface so the syntax colors read in both light and dark themes.
        return new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12),
            Background = Rgb(0x25, 0x2B, 0x34),
            Child = box,
        };
    }

    private FrameworkElement BuildMessageBoxDemo(string buttonText, string title, string message,
        ModernDialogButtons buttons, ModernDialogIcon icon, bool reportResult)
    {
        var trigger = new Button { Content = buttonText, HorizontalAlignment = HorizontalAlignment.Left };
        trigger.Click += (_, _) =>
        {
            Window? owner = Window.GetWindow(this);
            ModernDialogResult result = ModernMessageBox.Show(owner, _theme, message, title, buttons, icon);
            if (reportResult)
            {
                ModernMessageBox.Show(owner, _theme, $"You chose: {result}", "Result",
                    ModernDialogButtons.Ok, ModernDialogIcon.Info);
            }
        };
        return trigger;
    }

    private FrameworkElement BuildCustomButtonsDemo()
    {
        var trigger = new Button { Content = "Consent prompt", HorizontalAlignment = HorizontalAlignment.Left };
        trigger.Click += (_, _) =>
        {
            Window? owner = Window.GetWindow(this);
            var buttons = new[]
            {
                new ModernDialogButton("Enable (recommended)", ModernDialogResult.Yes, IsDefault: true, Accent: true),
                new ModernDialogButton("Disable", ModernDialogResult.No, IsCancel: true),
            };
            ModernDialogResult result = ModernMessageBox.Show(owner, _theme,
                "Share anonymous usage data to help improve the add-in?",
                "Anonymous Usage Data", buttons, ModernDialogIcon.Question);
            ModernMessageBox.Show(owner, _theme, $"You chose: {result}", "Result",
                ModernDialogButtons.Ok, ModernDialogIcon.Info);
        };
        return trigger;
    }

    private FrameworkElement BuildRichContentDemo()
    {
        var trigger = new Button { Content = "Show details", HorizontalAlignment = HorizontalAlignment.Left };
        trigger.Click += (_, _) =>
        {
            Window? owner = Window.GetWindow(this);

            var text = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxWidth = 400 };
            text.Inlines.Add(new Run("This dialog can host arbitrary content, including a "));
            var link = new Hyperlink(new Run("hyperlink")) { NavigateUri = new Uri("https://learn.microsoft.com") };
            link.RequestNavigate += (_, e) => System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            text.Inlines.Add(link);
            text.Inlines.Add(new Run(" that opens in the browser."));

            var buttons = new[]
            {
                new ModernDialogButton("OK", ModernDialogResult.Ok, IsDefault: true, IsCancel: true, Accent: true),
            };
            ModernMessageBox.Show(owner, _theme, text, "Details", buttons, ModernDialogIcon.Info);
        };
        return trigger;
    }

    /// <summary>Loads an embedded image (by logical name) from the hosting assembly, or null.</summary>
    private static ImageSource? LoadIcon(string name)
    {
        try
        {
            using Stream? stream = typeof(GalleryView).Assembly.GetManifestResourceStream(name);
            if (stream is null)
            {
                return null;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>A tree node whose header is an icon + label, with optional child nodes.</summary>
    private static TreeViewItem IconNode(ImageSource? icon, string text, bool expanded, params TreeViewItem[] children)
    {
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        if (icon is not null)
        {
            header.Children.Add(new Image
            {
                Source = icon,
                Width = 16,
                Height = 16,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });
        }
        header.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });

        var node = new TreeViewItem { Header = header, IsExpanded = expanded };
        foreach (TreeViewItem child in children)
        {
            node.Items.Add(child);
        }
        return node;
    }

    /// <summary>An assembly model tree using the Inventor file-type icons from resources.</summary>
    private static FrameworkElement BuildAssemblyTree()
    {
        ImageSource? iam = LoadIcon("assembly.png");
        ImageSource? ipt = LoadIcon("part.png");
        ImageSource? idw = LoadIcon("drawing.png");
        ImageSource? ipn = LoadIcon("presentation.png");
        ImageSource? other = LoadIcon("other.png");

        var tree = new TreeView { Width = 300, Height = 250, HorizontalAlignment = HorizontalAlignment.Left };
        tree.Items.Add(
            IconNode(iam, "Bracket.iam", true,
                IconNode(ipt, "Base.ipt", false),
                IconNode(ipt, "Cover.ipt", false),
                IconNode(iam, "Hinge.iam", true,
                    IconNode(ipt, "Pin.ipt", false),
                    IconNode(ipt, "Bushing.ipt", false)),
                IconNode(idw, "Bracket.idw", false),
                IconNode(ipn, "Bracket.ipn", false),
                IconNode(other, "Notes.txt", false)));
        return tree;
    }

    /// <summary>
    /// A checkbox tree with tri-state parent behaviour: checking the parent checks all children;
    /// checking some children puts the parent in the indeterminate (third) state.
    /// </summary>
    private static FrameworkElement BuildCheckboxTree()
    {
        var children = new[]
        {
            new CheckBox { Content = "Dimensions", IsChecked = true },
            new CheckBox { Content = "Sketches", IsChecked = false },
            new CheckBox { Content = "Work features", IsChecked = true },
        };
        // IsThreeState=false so a user click only toggles on/off; the indeterminate state is set
        // programmatically from the children.
        var parent = new CheckBox { Content = "All layers", IsThreeState = false };

        bool updating = false;

        // Parent -> children: clicking the parent forces every child to the same state.
        parent.Click += (_, _) =>
        {
            bool target = parent.IsChecked == true;
            updating = true;
            foreach (CheckBox child in children)
            {
                child.IsChecked = target;
            }
            updating = false;
        };

        // Children -> parent: all checked = checked, none = unchecked, mixed = indeterminate.
        void SyncParent()
        {
            if (updating)
            {
                return;
            }

            int checkedCount = 0;
            foreach (CheckBox child in children)
            {
                if (child.IsChecked == true)
                {
                    checkedCount++;
                }
            }

            updating = true;
            parent.IsChecked = checkedCount == 0 ? false
                : checkedCount == children.Length ? true
                : null;
            updating = false;
        }

        foreach (CheckBox child in children)
        {
            child.Checked += (_, _) => SyncParent();
            child.Unchecked += (_, _) => SyncParent();
        }

        SyncParent(); // initial 2-of-3 -> parent shows the indeterminate dash

        var root = new TreeViewItem { Header = parent, IsExpanded = true };
        foreach (CheckBox child in children)
        {
            root.Items.Add(new TreeViewItem { Header = child });
        }

        var tree = new TreeView { Width = 280, Height = 180, HorizontalAlignment = HorizontalAlignment.Left };
        tree.Items.Add(root);
        return tree;
    }

    /// <summary>A spinner driven by a controllable clock, with a slider that changes its speed live.</summary>
    private static FrameworkElement BuildSpeedSpinner()
    {
        var ring = new System.Windows.Shapes.Ellipse
        {
            Width = 45,
            Height = 45,
            StrokeThickness = 4,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ring.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "Brush.Border");

        var arc = new System.Windows.Shapes.Path
        {
            StrokeThickness = 4,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Data = Geometry.Parse("M 25,2.5 A 22.5,22.5 0 1 1 2.5,25"),
        };
        arc.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "Brush.Accent");

        var rotate = new RotateTransform();
        var face = new Grid { Width = 50, Height = 50, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = rotate };
        face.Children.Add(ring);
        face.Children.Add(arc);

        var spinner = new Viewbox { Width = 40, Height = 40, Child = face, HorizontalAlignment = HorizontalAlignment.Left };

        var anim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(0.9)) { RepeatBehavior = RepeatBehavior.Forever };
        AnimationClock clock = anim.CreateClock();
        rotate.ApplyAnimationClock(RotateTransform.AngleProperty, clock);

        var label = new TextBlock { Text = "Speed: 1.0x", Margin = new Thickness(0, 12, 0, 4) };
        var slider = new Slider
        {
            Minimum = 0.25,
            Maximum = 3,
            Value = 1,
            Width = 220,
            HorizontalAlignment = HorizontalAlignment.Left,
            TickFrequency = 0.25,
            IsSnapToTickEnabled = true,
        };
        slider.ValueChanged += (_, e) =>
        {
            if (clock.Controller is not null)
            {
                clock.Controller.SpeedRatio = e.NewValue;
            }
            label.Text = $"Speed: {e.NewValue:0.0}x";
        };

        var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(spinner);
        panel.Children.Add(label);
        panel.Children.Add(slider);
        return panel;
    }

    /// <summary>An indeterminate bar driven by controllable clocks, with a live speed slider.</summary>
    private static FrameworkElement BuildSpeedBar()
    {
        var s1 = new GradientStop(Colors.Transparent, -0.4);
        var s2 = new GradientStop(Color.FromRgb(0x06, 0x96, 0xD7), -0.2);
        var s3 = new GradientStop(Colors.Transparent, 0.0);
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        brush.GradientStops.Add(s1);
        brush.GradientStops.Add(s2);
        brush.GradientStops.Add(s3);

        var band = new Border { CornerRadius = new CornerRadius(4), Background = brush };
        var track = new Border
        {
            Height = 8,
            Width = 260,
            HorizontalAlignment = HorizontalAlignment.Left,
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Child = band,
        };
        track.SetResourceReference(Border.BackgroundProperty, "Brush.Control");
        track.SetResourceReference(Border.BorderBrushProperty, "Brush.Border");
        // Follow the theme accent (GradientStop can't take a DynamicResource in code).
        track.Loaded += (_, _) =>
        {
            if (track.TryFindResource("Color.Accent") is Color accent)
            {
                s2.Color = accent;
            }
        };

        var dur = TimeSpan.FromSeconds(1.1);
        var clocks = new[]
        {
            new DoubleAnimation(-0.4, 1.0, dur) { RepeatBehavior = RepeatBehavior.Forever }.CreateClock(),
            new DoubleAnimation(-0.2, 1.2, dur) { RepeatBehavior = RepeatBehavior.Forever }.CreateClock(),
            new DoubleAnimation(0.0, 1.4, dur) { RepeatBehavior = RepeatBehavior.Forever }.CreateClock(),
        };
        s1.ApplyAnimationClock(GradientStop.OffsetProperty, clocks[0]);
        s2.ApplyAnimationClock(GradientStop.OffsetProperty, clocks[1]);
        s3.ApplyAnimationClock(GradientStop.OffsetProperty, clocks[2]);

        var label = new TextBlock { Text = "Speed: 1.0x", Margin = new Thickness(0, 12, 0, 4) };
        var slider = new Slider
        {
            Minimum = 0.25,
            Maximum = 3,
            Value = 1,
            Width = 260,
            HorizontalAlignment = HorizontalAlignment.Left,
            TickFrequency = 0.25,
            IsSnapToTickEnabled = true,
        };
        slider.ValueChanged += (_, e) =>
        {
            foreach (AnimationClock clock in clocks)
            {
                if (clock.Controller is not null)
                {
                    clock.Controller.SpeedRatio = e.NewValue;
                }
            }
            label.Text = $"Speed: {e.NewValue:0.0}x";
        };

        var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(track);
        panel.Children.Add(label);
        panel.Children.Add(slider);
        return panel;
    }

    private static FrameworkElement BuildDataGrid()
    {
        var items = new ObservableCollection<DemoParameter>
        {
            new() { Name = "Length", Value = 120.0, Unit = "mm" },
            new() { Name = "Width", Value = 60.0, Unit = "mm" },
            new() { Name = "Height", Value = 25.5, Unit = "mm" },
            new() { Name = "Holes", Value = 4, Unit = string.Empty },
        };

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            IsReadOnly = true,
            Height = 200,
            ItemsSource = items,
        };
        grid.Columns.Add(new DataGridTextColumn { Header = "Parameter", Binding = new Binding("Name"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Value", Binding = new Binding("Value"), Width = new DataGridLength(80) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Unit", Binding = new Binding("Unit"), Width = new DataGridLength(70) });
        grid.Columns.Add(new DataGridTemplateColumn { Header = string.Empty, Width = new DataGridLength(64), CellTemplate = BuildDeleteCellTemplate(items) });

        // Toggle read-only / editable.
        var editToggle = new Button { Content = "Switch to edit" };
        editToggle.Click += (_, _) =>
        {
            grid.IsReadOnly = !grid.IsReadOnly;
            editToggle.Content = grid.IsReadOnly ? "Switch to edit" : "Switch to read-only";
        };

        // Add a new row.
        var addRow = new Button { Content = "Add row", Margin = new Thickness(8, 0, 0, 0) };
        addRow.Click += (_, _) => items.Add(new DemoParameter { Name = "New parameter", Value = 0, Unit = "mm" });

        var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        bar.Children.Add(editToggle);
        bar.Children.Add(addRow);

        var panel = new StackPanel();
        panel.Children.Add(bar);
        panel.Children.Add(grid);
        return panel;
    }

    /// <summary>A per-row trash-icon button that removes its row's item from the collection.</summary>
    private static DataTemplate BuildDeleteCellTemplate(ObservableCollection<DemoParameter> items)
    {
        var icon = new FrameworkElementFactory(typeof(TextBlock));
        icon.SetValue(TextBlock.TextProperty, ""); // Segoe Fluent Icons "Delete" (trash)
        icon.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"));
        icon.SetValue(TextBlock.FontSizeProperty, 14.0);

        var button = new FrameworkElementFactory(typeof(Button));
        button.AppendChild(icon);
        button.SetResourceReference(FrameworkElement.StyleProperty, "IconButton");
        button.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        button.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        button.SetValue(FrameworkElement.ToolTipProperty, "Delete row");
        button.AddHandler(Button.ClickEvent, new RoutedEventHandler((sender, _) =>
        {
            if (sender is FrameworkElement { DataContext: DemoParameter row })
            {
                items.Remove(row);
            }
        }));

        return new DataTemplate { VisualTree = button };
    }

    private FrameworkElement BuildToastControls()
    {
        var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 260 };

        // Max visible.
        var maxLabel = new TextBlock { Text = $"Max visible: {ModernToast.MaxVisible}", Margin = new Thickness(0, 0, 0, 4) };
        var maxSlider = new Slider
        {
            Minimum = 1, Maximum = 6, Value = ModernToast.MaxVisible,
            TickFrequency = 1, IsSnapToTickEnabled = true, TickPlacement = System.Windows.Controls.Primitives.TickPlacement.BottomRight,
            Width = 220, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 14),
        };
        maxSlider.ValueChanged += (_, e) =>
        {
            ModernToast.MaxVisible = (int)e.NewValue;
            maxLabel.Text = $"Max visible: {ModernToast.MaxVisible}";
        };

        // Default duration (seconds).
        var durLabel = new TextBlock { Text = $"Duration: {ModernToast.DefaultDuration.TotalSeconds:0} s", Margin = new Thickness(0, 0, 0, 4) };
        var durSlider = new Slider
        {
            Minimum = 1, Maximum = 10, Value = ModernToast.DefaultDuration.TotalSeconds,
            TickFrequency = 1, IsSnapToTickEnabled = true, TickPlacement = System.Windows.Controls.Primitives.TickPlacement.BottomRight,
            Width = 220, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 14),
        };
        durSlider.ValueChanged += (_, e) =>
        {
            ModernToast.DefaultDuration = TimeSpan.FromSeconds(e.NewValue);
            durLabel.Text = $"Duration: {e.NewValue:0} s";
        };

        // Burst button to show the cap in action.
        var burst = new Button { Content = "Show 6 toasts in a row", HorizontalAlignment = HorizontalAlignment.Left };
        var types = new[] { ToastType.Info, ToastType.Success, ToastType.Warning, ToastType.Error };
        burst.Click += (_, _) =>
        {
            Window? owner = Window.GetWindow(this);
            if (owner is null)
            {
                return;
            }
            for (int i = 1; i <= 6; i++)
            {
                ModernToast.Show(owner, $"Notification #{i}", types[(i - 1) % types.Length]);
            }
        };

        panel.Children.Add(maxLabel);
        panel.Children.Add(maxSlider);
        panel.Children.Add(durLabel);
        panel.Children.Add(durSlider);
        panel.Children.Add(burst);
        return panel;
    }

    private FrameworkElement BuildToastDemo(string label, ToastType type, string title, string message)
    {
        var trigger = new Button { Content = label, HorizontalAlignment = HorizontalAlignment.Left };
        trigger.Click += (_, _) =>
        {
            Window? owner = Window.GetWindow(this);
            if (owner is not null)
            {
                ModernToast.Show(owner, message, type, title);
            }
        };
        return trigger;
    }

    private static FrameworkElement BuildValidationField()
    {
        var box = new TextBox { Width = 240, HorizontalAlignment = HorizontalAlignment.Left };
        var binding = new Binding("Text")
        {
            Source = new DemoValue(),
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        };
        binding.ValidationRules.Add(new NumberValidationRule { ValidatesOnTargetUpdated = true });
        box.SetBinding(TextBox.TextProperty, binding);
        return box;
    }

    // --- demo content -------------------------------------------------------

    private List<DemoPage> BuildPages() =>
    [
        new DemoPage(OverviewPageName, []),

        new DemoPage("Buttons",
        [
            new DemoItem("Default button", """
                <Button Content="Default" Width="120" HorizontalAlignment="Left" />
                """),
            new DemoItem("Accent button", """
                <Button Content="Primary" Width="120" HorizontalAlignment="Left"
                        Style="{DynamicResource AccentButton}" />
                """),
            new DemoItem("Danger button", """
                <Button Content="Delete" Width="120" HorizontalAlignment="Left"
                        Style="{DynamicResource DangerButton}" />
                """),
            new DemoItem("Disabled button", """
                <Button Content="Disabled" Width="120" HorizontalAlignment="Left" IsEnabled="False" />
                """),
            new DemoItem("Icon buttons", """
                <StackPanel Orientation="Horizontal">
                    <Button Style="{DynamicResource IconButton}" ToolTip="Settings">
                        <TextBlock FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets" Text="&#xE713;" />
                    </Button>
                    <Button Style="{DynamicResource IconButton}" ToolTip="Refresh" Margin="6,0,0,0">
                        <TextBlock FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets" Text="&#xE72C;" />
                    </Button>
                    <Button Style="{DynamicResource IconButton}" ToolTip="Delete" Margin="6,0,0,0">
                        <TextBlock FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets" Text="&#xE74D;" />
                    </Button>
                </StackPanel>
                """),
        ]),

        new DemoPage("Text input",
        [
            new DemoItem("Text box", """
                <TextBox Width="240" HorizontalAlignment="Left" Text="Editable text" />
                """),
            new DemoItem("Search box (placeholder + icon)", """
                <!-- Placeholder text comes from Tag; a magnifier glyph leads the field. -->
                <StackPanel HorizontalAlignment="Left">
                    <TextBox Width="240" Style="{DynamicResource SearchBox}" Tag="Search ..." />
                    <TextBox Width="240" Margin="0,8,0,0" Style="{DynamicResource SearchBox}"
                             Tag="Search ..." Text="bracket" />
                </StackPanel>
                """),
            new DemoItem("Disabled text box", """
                <TextBox Width="240" HorizontalAlignment="Left" Text="Disabled" IsEnabled="False" />
                """),
            new DemoItem("Combo box", """
                <ComboBox Width="240" HorizontalAlignment="Left" SelectedIndex="0">
                    <ComboBoxItem Content="First option" />
                    <ComboBoxItem Content="Second option" />
                    <ComboBoxItem Content="Third option" />
                </ComboBox>
                """),
        ]),

        new DemoPage("Selection",
        [
            new DemoItem("Check box", """
                <CheckBox Content="Checked option" IsChecked="True" />
                """),
            new DemoItem("Toggle switch", """
                <CheckBox Content="Enabled" IsChecked="True" Style="{DynamicResource ToggleSwitch}" />
                """),
            new DemoItem("Radio buttons", """
                <StackPanel>
                    <RadioButton Content="Option one" IsChecked="True" GroupName="demo" />
                    <RadioButton Content="Option two" GroupName="demo" />
                </StackPanel>
                """),
            new DemoItem("List box", """
                <ListBox Width="240" Height="120" HorizontalAlignment="Left" SelectedIndex="0">
                    <ListBoxItem Content="Solid1" />
                    <ListBoxItem Content="Solid2" />
                    <ListBoxItem Content="Surface1" />
                    <ListBoxItem Content="Work Plane 1" />
                    <ListBoxItem Content="Sketch 1" />
                </ListBox>
                """),
            new DemoItem("Slider", """
                <Slider Width="280" HorizontalAlignment="Left"
                        Minimum="0" Maximum="100" Value="40" />
                """),
            new DemoItem("Slider with live value", """
                <DockPanel Width="280" HorizontalAlignment="Left">
                    <TextBlock DockPanel.Dock="Right" Width="32" TextAlignment="Right"
                               Text="{Binding Value, ElementName=mySlider, StringFormat={}{0:0}}" />
                    <Slider x:Name="mySlider" VerticalAlignment="Center"
                            Minimum="0" Maximum="100" Value="40" />
                </DockPanel>
                """),
            new DemoItem("Slider with ticks + snapping", """
                <Slider Width="280" HorizontalAlignment="Left"
                        Minimum="0" Maximum="100" Value="40"
                        TickPlacement="BottomRight" TickFrequency="10"
                        IsSnapToTickEnabled="True" />
                """),
            new DemoItem("Slider with min / max labels", """
                <DockPanel Width="300" HorizontalAlignment="Left">
                    <TextBlock DockPanel.Dock="Left" Text="0" Margin="0,0,8,0" VerticalAlignment="Center"
                               Foreground="{DynamicResource Brush.ForegroundMuted}" />
                    <TextBlock DockPanel.Dock="Right" Text="100" Margin="8,0,0,0" VerticalAlignment="Center"
                               Foreground="{DynamicResource Brush.ForegroundMuted}" />
                    <Slider VerticalAlignment="Center" Minimum="0" Maximum="100" Value="40"
                            AutoToolTipPlacement="TopLeft" AutoToolTipPrecision="0" />
                </DockPanel>
                """),
        ]),

        new DemoPage("Validation",
        [
            new DemoItem("Number field", """
                <TextBox Width="240" HorizontalAlignment="Left">
                    <TextBox.Text>
                        <Binding Path="Value" UpdateSourceTrigger="PropertyChanged">
                            <Binding.ValidationRules>
                                <local:NumberValidationRule ValidatesOnTargetUpdated="True" />
                            </Binding.ValidationRules>
                        </Binding>
                    </TextBox.Text>
                </TextBox>
                """) { Build = BuildValidationField },
        ]),

        new DemoPage("Typography",
        [
            new DemoItem("Title", """
                <TextBlock Text="Title text" Style="{DynamicResource TitleTextStyle}" />
                """),
            new DemoItem("Body", """
                <TextBlock TextWrapping="Wrap" Style="{DynamicResource BodyTextStyle}"
                           Text="Body text uses the theme foreground color and the Inventor font." />
                """),
            new DemoItem("Caption", """
                <TextBlock Text="Caption text is muted and smaller." Style="{DynamicResource CaptionTextStyle}" />
                """),
        ]),

        new DemoPage("Icons",
        [
            new DemoItem("ModernGlyphs catalog", """
                // Named glyphs the library ships; render one with the helper:
                TextBlock icon = ModernGlyphs.Icon(ModernGlyphs.Settings, 24);
                // or use the constant directly: ModernGlyphs.Add, .Delete, .Purge, .Book, ...
                """) { Build = GlyphCatalogBuilder.Build },
            new DemoItem("Single glyph", """
                <TextBlock FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets"
                           FontSize="24" Text="&#xE706;" />
                """),
            new DemoItem("Icon + text button", """
                <Button HorizontalAlignment="Left">
                    <StackPanel Orientation="Horizontal">
                        <TextBlock FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets"
                                   FontSize="16" VerticalAlignment="Center" Text="&#xE713;" />
                        <TextBlock Text="Settings" Margin="8,0,0,0" VerticalAlignment="Center" />
                    </StackPanel>
                </Button>
                """),
            new DemoItem("Common glyphs", """
                <StackPanel Orientation="Horizontal">
                    <TextBlock FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets" FontSize="20"
                               Margin="0,0,18,0" Text="&#xE74E;" ToolTip="Save (E74E)" />
                    <TextBlock FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets" FontSize="20"
                               Margin="0,0,18,0" Text="&#xE74D;" ToolTip="Delete (E74D)" />
                    <TextBlock FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets" FontSize="20"
                               Margin="0,0,18,0" Text="&#xE713;" ToolTip="Settings (E713)" />
                    <TextBlock FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets" FontSize="20"
                               Margin="0,0,18,0" Text="&#xE72C;" ToolTip="Refresh (E72C)" />
                    <TextBlock FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets" FontSize="20"
                               Text="&#xE8FB;" ToolTip="Accept (E8FB)" />
                </StackPanel>
                """),
        ]),

        new DemoPage("Message boxes",
        [
            new DemoItem("Information", """
                ModernMessageBox.Show(owner, theme,
                    "The export finished successfully.",
                    "Information",
                    ModernDialogButtons.Ok,
                    ModernDialogIcon.Info);
                """) { Build = () => BuildMessageBoxDemo("Show info", "Information",
                    "The export finished successfully.", ModernDialogButtons.Ok, ModernDialogIcon.Info, false) },

            new DemoItem("Confirm (Yes / No)", """
                var result = ModernMessageBox.Show(owner, theme,
                    "Delete the selected items?",
                    "Confirm",
                    ModernDialogButtons.YesNo,
                    ModernDialogIcon.Question);
                """) { Build = () => BuildMessageBoxDemo("Confirm", "Confirm",
                    "Delete the selected items?", ModernDialogButtons.YesNo, ModernDialogIcon.Question, true) },

            new DemoItem("Error (OK / Cancel)", """
                ModernMessageBox.Show(owner, theme,
                    "The file could not be opened.",
                    "Error",
                    ModernDialogButtons.OkCancel,
                    ModernDialogIcon.Error);
                """) { Build = () => BuildMessageBoxDemo("Show error", "Error",
                    "The file could not be opened.", ModernDialogButtons.OkCancel, ModernDialogIcon.Error, false) },

            new DemoItem("Custom button labels", """
                var buttons = new[]
                {
                    new ModernDialogButton("Enable (recommended)", ModernDialogResult.Yes,
                        IsDefault: true, Accent: true),
                    new ModernDialogButton("Disable", ModernDialogResult.No, IsCancel: true),
                };
                ModernMessageBox.Show(owner, theme,
                    "Share anonymous usage data to help improve the add-in?",
                    "Anonymous Usage Data", buttons, ModernDialogIcon.Question);
                """) { Build = BuildCustomButtonsDemo },

            new DemoItem("Rich content (hyperlink)", """
                var text = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxWidth = 400 };
                text.Inlines.Add(new Run("This dialog can host arbitrary content, including a "));
                text.Inlines.Add(new Hyperlink(new Run("link")) { NavigateUri = uri });
                text.Inlines.Add(new Run("."));
                ModernMessageBox.Show(owner, theme, text, "Details",
                    new[] { new ModernDialogButton("OK", ModernDialogResult.Ok,
                            IsDefault: true, IsCancel: true, Accent: true) },
                    ModernDialogIcon.Info);
                """) { Build = BuildRichContentDemo },
        ]),

        new DemoPage("Display",
        [
            new DemoItem("Progress bar", """
                <ProgressBar Value="60" Maximum="100" />
                """),
            new DemoItem("Indeterminate bar", """
                <ProgressBar Style="{DynamicResource IndeterminateBar}" />
                """),
            new DemoItem("Spinner", """
                <StackPanel Orientation="Horizontal">
                    <ProgressBar Style="{DynamicResource Spinner}" />
                    <ProgressBar Style="{DynamicResource Spinner}" Width="40" Height="40" Margin="20,0,0,0" />
                </StackPanel>
                """),
            new DemoItem("Spinner with label", """
                <StackPanel Orientation="Horizontal">
                    <ProgressBar Style="{DynamicResource Spinner}" Width="20" Height="20" />
                    <TextBlock Text="Loading…" VerticalAlignment="Center" Margin="10,0,0,0" />
                </StackPanel>
                """),
            new DemoItem("Dots loader", """
                <ProgressBar Style="{DynamicResource DotsLoader}" HorizontalAlignment="Left" />
                """),
            new DemoItem("Spinner with speed control", """
                // Animation timing can't be data-bound in XAML, so drive it from a controllable
                // clock and change SpeedRatio at runtime:
                var anim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(0.9))
                    { RepeatBehavior = RepeatBehavior.Forever };
                AnimationClock clock = anim.CreateClock();
                rotate.ApplyAnimationClock(RotateTransform.AngleProperty, clock);

                clock.Controller.SpeedRatio = 2.0;   // 2x faster, live
                """) { Build = BuildSpeedSpinner },
            new DemoItem("Indeterminate bar with speed control", """
                // Same controllable-clock pattern, applied to the gradient offsets:
                foreach (var (stop, from, to) in stops)
                {
                    var clock = new DoubleAnimation(from, to, TimeSpan.FromSeconds(1.1))
                        { RepeatBehavior = RepeatBehavior.Forever }.CreateClock();
                    stop.ApplyAnimationClock(GradientStop.OffsetProperty, clock);
                    clocks.Add(clock);
                }
                // slider: foreach (clock) clock.Controller.SpeedRatio = value;
                """) { Build = BuildSpeedBar },
            new DemoItem("Card", """
                <Border Style="{DynamicResource Card}">
                    <TextBlock Text="Card surface" />
                </Border>
                """),
            new DemoItem("Expander", """
                <Expander Header="Output" IsExpanded="True">
                    <StackPanel>
                        <TextBlock Text="Body Name" Margin="0,0,0,4" />
                        <TextBox Text="Solid1" Width="200" HorizontalAlignment="Left" />
                    </StackPanel>
                </Expander>
                """),
        ]),

        new DemoPage("Badges",
        [
            new DemoItem("Status badges", """
                <StackPanel Orientation="Horizontal">
                    <ContentControl Style="{DynamicResource Badge}" Content="Draft" />
                    <ContentControl Style="{DynamicResource BadgeAccent}" Content="New" Margin="8,0,0,0" />
                    <ContentControl Style="{DynamicResource BadgeError}" Content="Error" Margin="8,0,0,0" />
                </StackPanel>
                """),
            new DemoItem("Counter on a button", """
                <Grid HorizontalAlignment="Left">
                    <Button Content="Inbox" />
                    <ContentControl Style="{DynamicResource CounterBadge}" Content="3"
                                    HorizontalAlignment="Right" VerticalAlignment="Top"
                                    Margin="0,-8,-8,0" />
                </Grid>
                """),
            new DemoItem("Shield (two-tone)", """
                <StackPanel Orientation="Horizontal" HorizontalAlignment="Left">
                    <ContentControl Style="{DynamicResource ShieldLabel}" Content="build" />
                    <ContentControl Style="{DynamicResource ShieldValue}" Content="passing" />
                </StackPanel>
                """),
        ]),

        new DemoPage("Toasts",
        [
            new DemoItem("Settings (max visible / duration)", """
                // Both are static knobs on ModernToast, applied to every toast.
                ModernToast.MaxVisible = 3;                              // cap shown at once
                ModernToast.DefaultDuration = TimeSpan.FromSeconds(4);   // auto-dismiss after

                // Per-call duration still overrides the default:
                ModernToast.Show(owner, "Saved", ToastType.Success,
                    duration: TimeSpan.FromSeconds(2));
                """) { Build = BuildToastControls },
            new DemoItem("Info toast", """
                ModernToast.Show(owner, "Your settings were loaded.",
                    ToastType.Info, title: "Heads up");
                """) { Build = () => BuildToastDemo("Show info", ToastType.Info, "Heads up", "Your settings were loaded.") },
            new DemoItem("Success toast", """
                ModernToast.Show(owner, "Export completed.",
                    ToastType.Success, title: "Done");
                """) { Build = () => BuildToastDemo("Show success", ToastType.Success, "Done", "Export completed.") },
            new DemoItem("Warning toast", """
                ModernToast.Show(owner, "Some parameters were skipped.",
                    ToastType.Warning, title: "Check input");
                """) { Build = () => BuildToastDemo("Show warning", ToastType.Warning, "Check input", "Some parameters were skipped.") },
            new DemoItem("Error toast", """
                ModernToast.Show(owner, "The file could not be opened.",
                    ToastType.Error, title: "Failed");
                """) { Build = () => BuildToastDemo("Show error", ToastType.Error, "Failed", "The file could not be opened.") },
        ]),

        new DemoPage("Layout",
        [
            new DemoItem("Group box", """
                <GroupBox Header="Output" Width="260" HorizontalAlignment="Left">
                    <StackPanel>
                        <TextBlock Text="Body Name" Margin="0,0,0,4" />
                        <TextBox Text="Solid1" />
                    </StackPanel>
                </GroupBox>
                """),
            new DemoItem("Separator", """
                <StackPanel Width="260" HorizontalAlignment="Left">
                    <TextBlock Text="Section one" />
                    <Separator />
                    <TextBlock Text="Section two" />
                </StackPanel>
                """),
            new DemoItem("Tooltip", """
                <Button Content="Hover for a themed tooltip"
                        HorizontalAlignment="Left"
                        ToolTip="Themed tooltip — panel surface, theme border and font." />
                """),
            new DemoItem("Scroll bar", """
                <ScrollViewer Height="96" Width="260" HorizontalAlignment="Left"
                              VerticalScrollBarVisibility="Visible">
                    <StackPanel>
                        <TextBlock Text="Item 1" /> <TextBlock Text="Item 2" />
                        <TextBlock Text="Item 3" /> <TextBlock Text="Item 4" />
                        <TextBlock Text="Item 5" /> <TextBlock Text="Item 6" />
                        <TextBlock Text="Item 7" /> <TextBlock Text="Item 8" />
                        <TextBlock Text="Item 9" /> <TextBlock Text="Item 10" />
                    </StackPanel>
                </ScrollViewer>
                """),
            new DemoItem("Tab control", """
                <TabControl Width="300" Height="120" HorizontalAlignment="Left">
                    <TabItem Header="General">
                        <TextBlock Text="General settings" />
                    </TabItem>
                    <TabItem Header="Advanced">
                        <TextBlock Text="Advanced settings" />
                    </TabItem>
                    <TabItem Header="About">
                        <TextBlock Text="About this add-in" />
                    </TabItem>
                </TabControl>
                """),
        ]),

        new DemoPage("Menus",
        [
            new DemoItem("Menu bar", """
                <Menu HorizontalAlignment="Left">
                    <MenuItem Header="File">
                        <MenuItem Header="New" InputGestureText="Ctrl+N" />
                        <MenuItem Header="Open" InputGestureText="Ctrl+O" />
                        <Separator />
                        <MenuItem Header="Exit" />
                    </MenuItem>
                    <MenuItem Header="Edit">
                        <MenuItem Header="Undo" InputGestureText="Ctrl+Z" />
                        <MenuItem Header="Redo" InputGestureText="Ctrl+Y" />
                        <Separator />
                        <MenuItem Header="Snap to grid" IsCheckable="True" IsChecked="True" />
                    </MenuItem>
                </Menu>
                """),
            new DemoItem("Context menu (right-click)", """
                <Border Background="{DynamicResource Brush.Control}" CornerRadius="4"
                        Padding="24" HorizontalAlignment="Left">
                    <TextBlock Text="Right-click here" />
                    <Border.ContextMenu>
                        <ContextMenu>
                            <MenuItem Header="Cut" InputGestureText="Ctrl+X" />
                            <MenuItem Header="Copy" InputGestureText="Ctrl+C" />
                            <MenuItem Header="Paste" InputGestureText="Ctrl+V" />
                            <Separator />
                            <MenuItem Header="More">
                                <MenuItem Header="Rename" />
                                <MenuItem Header="Delete" />
                            </MenuItem>
                        </ContextMenu>
                    </Border.ContextMenu>
                </Border>
                """),
        ]),

        new DemoPage("Data",
        [
            new DemoItem("Tree view", """
                <TreeView Width="260" Height="180" HorizontalAlignment="Left">
                    <TreeViewItem Header="Assembly1" IsExpanded="True">
                        <TreeViewItem Header="Part1" />
                        <TreeViewItem Header="Part2" />
                        <TreeViewItem Header="Sub-assembly" IsExpanded="True">
                            <TreeViewItem Header="Part3" />
                            <TreeViewItem Header="Part4" />
                        </TreeViewItem>
                    </TreeViewItem>
                </TreeView>
                """),
            new DemoItem("Tree view with icons", """
                <TreeView Width="280" Height="210" HorizontalAlignment="Left">
                    <TreeViewItem IsExpanded="True">
                        <TreeViewItem.Header>
                            <StackPanel Orientation="Horizontal">
                                <TextBlock FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets"
                                           Text="&#xE8B7;" Margin="0,0,8,0" VerticalAlignment="Center" />
                                <TextBlock Text="Assembly1" VerticalAlignment="Center" />
                            </StackPanel>
                        </TreeViewItem.Header>
                        <TreeViewItem>
                            <TreeViewItem.Header>
                                <StackPanel Orientation="Horizontal">
                                    <TextBlock FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets"
                                               Text="&#xE7C3;" Margin="0,0,8,0" VerticalAlignment="Center" />
                                    <TextBlock Text="Part1" VerticalAlignment="Center" />
                                </StackPanel>
                            </TreeViewItem.Header>
                        </TreeViewItem>
                        <TreeViewItem IsExpanded="True">
                            <TreeViewItem.Header>
                                <StackPanel Orientation="Horizontal">
                                    <TextBlock FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets"
                                               Text="&#xE8B7;" Margin="0,0,8,0" VerticalAlignment="Center" />
                                    <TextBlock Text="Sub-assembly" VerticalAlignment="Center" />
                                </StackPanel>
                            </TreeViewItem.Header>
                            <TreeViewItem>
                                <TreeViewItem.Header>
                                    <StackPanel Orientation="Horizontal">
                                        <TextBlock FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets"
                                                   Text="&#xE7C3;" Margin="0,0,8,0" VerticalAlignment="Center" />
                                        <TextBlock Text="Part2" VerticalAlignment="Center" />
                                    </StackPanel>
                                </TreeViewItem.Header>
                            </TreeViewItem>
                            <TreeViewItem>
                                <TreeViewItem.Header>
                                    <StackPanel Orientation="Horizontal">
                                        <TextBlock FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets"
                                                   Text="&#xE70F;" Margin="0,0,8,0" VerticalAlignment="Center" />
                                        <TextBlock Text="Sketch1" VerticalAlignment="Center" />
                                    </StackPanel>
                                </TreeViewItem.Header>
                            </TreeViewItem>
                        </TreeViewItem>
                    </TreeViewItem>
                </TreeView>
                """),
            new DemoItem("Tree view with checkboxes (tri-state)", """
                <!-- Tri-state is wired in code: the parent toggles every child; the children set the
                     parent to checked / unchecked / indeterminate (null) based on how many are on. -->
                <TreeView Width="280" Height="180" HorizontalAlignment="Left">
                    <TreeViewItem IsExpanded="True">
                        <TreeViewItem.Header>
                            <CheckBox Content="All layers" Click="OnParentClick" />
                        </TreeViewItem.Header>
                        <TreeViewItem>
                            <TreeViewItem.Header>
                                <CheckBox Content="Dimensions" Checked="OnChildChanged" Unchecked="OnChildChanged" />
                            </TreeViewItem.Header>
                        </TreeViewItem>
                        <TreeViewItem>
                            <TreeViewItem.Header>
                                <CheckBox Content="Sketches" Checked="OnChildChanged" Unchecked="OnChildChanged" />
                            </TreeViewItem.Header>
                        </TreeViewItem>
                        <TreeViewItem>
                            <TreeViewItem.Header>
                                <CheckBox Content="Work features" Checked="OnChildChanged" Unchecked="OnChildChanged" />
                            </TreeViewItem.Header>
                        </TreeViewItem>
                    </TreeViewItem>
                </TreeView>
                """) { Build = BuildCheckboxTree },
            new DemoItem("Assembly tree (file-type icons)", """
                <TreeView Width="300" Height="250" HorizontalAlignment="Left">
                    <TreeViewItem IsExpanded="True">
                        <TreeViewItem.Header>
                            <StackPanel Orientation="Horizontal">
                                <Image Source="/resources/assembly.png" Width="16" Height="16"
                                       Margin="0,0,8,0" VerticalAlignment="Center" />
                                <TextBlock Text="Bracket.iam" VerticalAlignment="Center" />
                            </StackPanel>
                        </TreeViewItem.Header>
                        <!-- child nodes use ipt / idw / ipn / generic icons the same way -->
                        <TreeViewItem Header="Base.ipt" />
                        <TreeViewItem Header="Cover.ipt" />
                    </TreeViewItem>
                </TreeView>
                """) { Build = BuildAssemblyTree },
            new DemoItem("Data grid", """
                <StackPanel>
                    <StackPanel Orientation="Horizontal" Margin="0,0,0,8">
                        <Button Content="Switch to edit" Click="OnToggleReadOnly" />
                        <Button Content="Add row" Click="OnAddRow" Margin="8,0,0,0" />
                    </StackPanel>
                    <DataGrid ItemsSource="{Binding Parameters}" IsReadOnly="True"
                              AutoGenerateColumns="False" CanUserAddRows="False">
                        <DataGrid.Columns>
                            <DataGridTextColumn Header="Parameter" Binding="{Binding Name}" Width="*" />
                            <DataGridTextColumn Header="Value" Binding="{Binding Value}" />
                            <DataGridTextColumn Header="Unit" Binding="{Binding Unit}" />
                            <DataGridTemplateColumn Width="64">
                                <DataGridTemplateColumn.CellTemplate>
                                    <DataTemplate>
                                        <Button Click="OnDeleteRow" ToolTip="Delete row"
                                                Style="{DynamicResource IconButton}">
                                            <TextBlock FontFamily="Segoe Fluent Icons" Text="&#xE74D;" />
                                        </Button>
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellTemplate>
                            </DataGridTemplateColumn>
                        </DataGrid.Columns>
                    </DataGrid>
                </StackPanel>
                """) { Build = BuildDataGrid },
        ]),
    ];
}

/// <summary>Renders every named glyph in <see cref="ModernGlyphs"/> as a labelled tile, so the icon
/// set (including the substitute glyphs like Purge / DeleteAll / Book) can be eyeballed live.</summary>
file static class GlyphCatalogBuilder
{
    public static FrameworkElement Build()
    {
        WrapPanel wrap = new() { Orientation = Orientation.Horizontal };

        foreach (FieldInfo field in typeof(ModernGlyphs).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            // The glyph fields are static-readonly strings; skip the const FontFamily (IsLiteral) and
            // anything that is not a string.
            if (field.FieldType != typeof(string) || field.IsLiteral)
            {
                continue;
            }

            string glyph = (string)field.GetValue(null)!;
            int codepoint = glyph.Length > 0 ? char.ConvertToUtf32(glyph, 0) : 0;

            StackPanel tile = new()
            {
                Width = 120,
                Margin = new Thickness(0, 0, 12, 14),
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            TextBlock icon = ModernGlyphs.Icon(glyph, 30);
            icon.Margin = new Thickness(0, 6, 0, 8);
            icon.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Foreground");
            tile.Children.Add(icon);

            TextBlock name = new()
            {
                Text = field.Name,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                FontWeight = FontWeights.SemiBold,
            };
            name.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Foreground");
            tile.Children.Add(name);

            TextBlock code = new()
            {
                Text = $"U+{codepoint:X4}",
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            code.SetResourceReference(TextBlock.ForegroundProperty, "Brush.ForegroundMuted");
            tile.Children.Add(code);

            wrap.Children.Add(tile);
        }

        return wrap;
    }
}

/// <summary>One showcase entry: a title, the XAML shown as code, and (usually) parsed into the live control.</summary>
public sealed class DemoItem
{
    public DemoItem(string title, string xaml)
    {
        Title = title;
        Xaml = xaml;
    }

    public string Title { get; }

    public string Xaml { get; }

    /// <summary>Optional live-control builder; when null the live control is parsed from <see cref="Xaml"/>.</summary>
    public System.Func<FrameworkElement>? Build { get; init; }
}

/// <summary>A nav page grouping several demo items.</summary>
public sealed class DemoPage
{
    public DemoPage(string name, IReadOnlyList<DemoItem> items)
    {
        Name = name;
        Items = items;
    }

    public string Name { get; }

    public IReadOnlyList<DemoItem> Items { get; }
}

/// <summary>Demo row for the DataGrid showcase.</summary>
public sealed class DemoParameter
{
    public string Name { get; set; } = string.Empty;

    public double Value { get; set; }

    public string Unit { get; set; } = string.Empty;
}

/// <summary>Demo binding source for the validation showcase.</summary>
public sealed class DemoValue
{
    public string Text { get; set; } = "sasd mm";
}

/// <summary>Demo rule: the value must parse as a number. Drives the built-in WPF validation visuals.</summary>
public sealed class NumberValidationRule : ValidationRule
{
    public override ValidationResult Validate(object value, CultureInfo cultureInfo)
    {
        string text = (value as string ?? string.Empty).Trim();
        return double.TryParse(text, NumberStyles.Any, cultureInfo, out _)
            ? ValidationResult.ValidResult
            : new ValidationResult(false, "Enter a valid number.");
    }
}
