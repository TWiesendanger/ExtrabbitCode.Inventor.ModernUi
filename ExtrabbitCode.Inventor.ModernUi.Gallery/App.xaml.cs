using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ExtrabbitCode.Inventor.ModernUi;
using ExtrabbitCode.Inventor.ModernUi.Demo;

namespace ExtrabbitCode.Inventor.ModernUi.Gallery;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string[] args = e.Args;

        // Headless docs mode: "--shoot-docs <dir>" renders ONE image per example and exits.
        int shootDocsIndex = Array.IndexOf(args, "--shoot-docs");
        if (shootDocsIndex >= 0 && shootDocsIndex + 1 < args.Length)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            ShootDocs(args[shootDocsIndex + 1]);
            return;
        }

        // Headless capture mode: "--shoot <dir>" renders the galleries to PNG and exits.
        int shootIndex = Array.IndexOf(args, "--shoot");
        if (shootIndex >= 0 && shootIndex + 1 < args.Length)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            ShootGalleries(args[shootIndex + 1]);
            return;
        }

        ShowGallery(Theme.Dark);
    }

    /// <summary>Opens the gallery directly in the given theme; the in-window toggle switches it live.</summary>
    private static void ShowGallery(Theme theme)
    {
        var gallery = new GalleryView();
        gallery.Initialize(
            theme,
            $"Font: {FontOptions.Default.Family.Source} {FontOptions.Default.NormalSize:0.#}px");

        string version = typeof(ModernWindow).Assembly.GetName().Version?.ToString(3) ?? "";

        new ModernWindow(theme)
        {
            Title = $"ExtrabbitCode Modern UI — Gallery  v{version}",
            Icon = GalleryView.LoadBranding(),
            Width = 1040,
            Height = 720,
            MinWidth = 820,
            MinHeight = 560,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = gallery,
        }.Show();
    }

    private void ShootGalleries(string outputDir)
    {
        Directory.CreateDirectory(outputDir);

        var queue = new Queue<Theme>(new[] { Theme.Dark, Theme.Light });

        void ShootNext()
        {
            if (queue.Count == 0)
            {
                Shutdown();
                return;
            }

            Theme theme = queue.Dequeue();
            var gallery = new GalleryView();
            gallery.Initialize(
                theme,
                $"Font: {FontOptions.Default.Family.Source} {FontOptions.Default.NormalSize:0.#}px");

            var window = new ModernWindow(theme)
            {
                Title = $"Control gallery — {theme}",
                Width = 760,
                Height = 920,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = gallery,
            };

            window.ContentRendered += (_, _) =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    string overviewPath = Path.Combine(outputDir, $"gallery-{theme}.png".ToLowerInvariant());
                    SaveWindowPng(window, overviewPath);

                    // Then walk a curated set of pages, capturing each, so individual features
                    // (glyph catalog, button variants, message boxes, ...) are visible in screenshots.
                    var pages = new Queue<string>(PagesToShoot);
                    ShootPageThenNext(window, gallery, theme, outputDir, pages, ShootNext);
                });
            };

            window.Show();
        }

        ShootNext();
    }

    /// <summary>Pages captured individually by "--shoot" (one PNG each, per theme), in addition to the overview.</summary>
    private static readonly string[] PagesToShoot = ["Buttons", "Text input", "Icons", "Message boxes"];

    /// <summary>Selects the next queued page, captures it, then either recurses or closes the window
    /// and moves on to the next theme.</summary>
    private void ShootPageThenNext(Window window, GalleryView gallery, Theme theme, string outputDir, Queue<string> pages, Action onComplete)
    {
        if (pages.Count == 0)
        {
            window.Close();
            onComplete();
            return;
        }

        string pageName = pages.Dequeue();
        gallery.SelectPage(pageName);
        Dispatcher.BeginInvoke(
            () =>
            {
                string slug = pageName.Replace(' ', '-').ToLowerInvariant();
                string path = Path.Combine(outputDir, $"{slug}-{theme}.png".ToLowerInvariant());
                SaveWindowPng(window, path);
                ShootPageThenNext(window, gallery, theme, outputDir, pages, onComplete);
            },
            System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private static void SaveWindowPng(Window window, string path) => SaveBitmap(RenderElement(window), path);

    private static void SaveBitmap(RenderTargetBitmap rtb, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using FileStream fs = File.Create(path);
        encoder.Save(fs);
    }

    // ============================================================================================
    // Documentation snapshotter: "--shoot-docs <dir>" renders ONE cropped image per example
    // (light + dark), so each control can sit next to its snippet in the docs. Inline controls are
    // rendered directly; modal dialogs and transient toasts are captured live.
    // ============================================================================================

    private void ShootDocs(string outputDir)
    {
        Directory.CreateDirectory(outputDir);

        foreach (Theme theme in new[] { Theme.Dark, Theme.Light })
        {
            var gallery = new GalleryView();
            gallery.Initialize(theme, string.Empty);

            // One reused off-screen themed host carries the window-scoped resources so each control
            // resolves its DynamicResource brushes / fonts.
            ModernWindow host = CreateOffscreenHost(theme, 1000, 800);
            host.Show();

            // 1. Inline controls — render each example cropped to its content.
            foreach (DemoPage page in gallery.Pages)
            {
                foreach (DemoItem item in page.Items)
                {
                    if (item.DocCapture != DocCapture.Inline)
                    {
                        continue;
                    }

                    FrameworkElement control;
                    try
                    {
                        control = gallery.RenderItemControl(item);
                    }
                    catch
                    {
                        continue; // a snippet that fails to build is skipped, not fatal
                    }

                    var container = new Border
                    {
                        Padding = new Thickness(16),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,
                        Child = control,
                        DataContext = gallery, // snippets that bind (e.g. DataGrid) resolve against the gallery
                    };
                    container.SetResourceReference(Border.BackgroundProperty, "Brush.Background");

                    host.Content = container;
                    host.UpdateLayout();
                    container.Measure(new Size(900, double.PositiveInfinity));
                    container.Arrange(new Rect(container.DesiredSize));
                    host.UpdateLayout();

                    string slug = $"{Slug(page.Name)}__{Slug(item.Title)}";
                    SaveBitmap(RenderElement(container), DocPath(outputDir, slug, theme));
                    host.Content = null;
                }
            }

            // 2. Modal dialogs — capture the real dialog window.
            foreach ((string slug, Action show) in DialogJobs(host, theme))
            {
                CaptureDialog(host, slug, theme, show, outputDir);
            }

            host.Close();

            // 3. Toasts — capture the real toast in a fresh host.
            foreach ((string slug, ToastType type, string title, string message) in ToastJobs())
            {
                CaptureToast(theme, slug, type, title, message, outputDir);
            }

            // 4. Interactive states (hover / focus / pressed / selected) for input-reactive controls.
            // Focus needs a real activated window; the rest are forced via read-only DP keys.
            foreach ((string slug, Func<(FrameworkElement root, Control target)> build, StateKind[] states) in StateJobs())
            {
                CaptureStates(theme, slug, build, states, outputDir);
            }
        }

        // 5. A sample themed with a fully custom palette (for the Theming docs page).
        CaptureCustomPalette(outputDir);

        Shutdown();
    }

    /// <summary>Renders a small control sample under a completely custom <see cref="ThemePalette"/>,
    /// to show on the docs that every colour can be overridden.</summary>
    private void CaptureCustomPalette(string outputDir)
    {
        // A "Tokyo Night" style palette — nothing to do with the Inventor defaults.
        ThemePalette palette = new()
        {
            Background = Hex("#1a1b26"),
            Panel = Hex("#24283b"),
            Control = Hex("#1f2335"),
            Foreground = Hex("#c0caf5"),
            ForegroundMuted = Hex("#565f89"),
            Border = Hex("#2f334d"),
            Accent = Hex("#7aa2f7"),
            AccentMuted = Hex("#3d59a1"),
            Error = Hex("#f7768e"),
        };

        ModernWindow host = CreateOffscreenHost(Theme.Dark, 1000, 800);
        ModernUi.SetTheme(host, Theme.Dark, palette); // re-colour to the custom palette
        host.Show();

        FrameworkElement sample = ParseSample("""
            <StackPanel>
                <TextBlock Text="Custom palette" Style="{DynamicResource TitleTextStyle}" Margin="0,0,0,12" />
                <StackPanel Orientation="Horizontal" Margin="0,0,0,12">
                    <Button Content="Primary" Style="{DynamicResource AccentButton}" Width="110" />
                    <Button Content="Delete" Style="{DynamicResource DangerButton}" Width="110" Margin="8,0,0,0" />
                    <Button Content="Cancel" Width="110" Margin="8,0,0,0" />
                </StackPanel>
                <TextBox Width="300" Style="{DynamicResource SearchBox}" Tag="Search ..." Margin="0,0,0,12" />
                <StackPanel Orientation="Horizontal">
                    <CheckBox Content="Enabled" IsChecked="True" Style="{DynamicResource ToggleSwitch}" />
                    <ContentControl Style="{DynamicResource BadgeAccent}" Content="New" Margin="16,0,0,0" />
                    <ContentControl Style="{DynamicResource BadgeError}" Content="Error" Margin="8,0,0,0" />
                </StackPanel>
            </StackPanel>
            """);

        var card = new Border
        {
            Padding = new Thickness(20),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            Child = sample,
        };
        card.SetResourceReference(Border.BackgroundProperty, "Brush.Panel");
        card.SetResourceReference(Border.BorderBrushProperty, "Brush.Border");

        var outer = new Border
        {
            Padding = new Thickness(24),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Child = card,
        };
        outer.SetResourceReference(Border.BackgroundProperty, "Brush.Background");

        host.Content = outer;
        host.UpdateLayout();
        outer.Measure(new Size(900, double.PositiveInfinity));
        outer.Arrange(new Rect(outer.DesiredSize));
        host.UpdateLayout();

        SaveBitmap(RenderElement(outer), Path.Combine(outputDir, "theming__custom-palette.png"));
        host.Close();
    }

    private static Color Hex(string hex) => (Color)ColorConverter.ConvertFromString(hex)!;

    private static FrameworkElement ParseSample(string innerXaml)
    {
        const string ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        const string nsX = "http://schemas.microsoft.com/winfx/2006/xaml";
        string xaml = $"<StackPanel xmlns=\"{ns}\" xmlns:x=\"{nsX}\">{innerXaml}</StackPanel>";
        return (FrameworkElement)System.Windows.Markup.XamlReader.Parse(xaml);
    }

    private static ModernWindow CreateOffscreenHost(Theme theme, double width, double height) => new(theme)
    {
        Width = width,
        Height = height,
        WindowStartupLocation = WindowStartupLocation.Manual,
        Left = -10000,
        Top = -10000,
        ShowInTaskbar = false,
        ShowActivated = false,
    };

    /// <summary>The interactive states a control can be captured in.</summary>
    private enum StateKind { Hover, Focus, Pressed, Selected }

    /// <summary>Controls whose interactive states are worth showing in the docs. Each job builds the
    /// control fresh (root to render + the target element the state applies to) and lists its states.
    /// For list items the target is the item itself, not the list.</summary>
    private static IEnumerable<(string slug, Func<(FrameworkElement root, Control target)> build, StateKind[] states)> StateJobs()
    {
        // Only controls whose hover / focus / pressed visuals are clearly visible are captured.
        // ComboBox, CheckBox and ToggleSwitch are omitted: their hover feedback is a faint grey glow
        // (or a border that doesn't surface when the state is forced) and doesn't read as a still.
        yield return ("text-input__text-box", () => Solo(new TextBox { Width = 240, Text = "Editable text" }),
            [StateKind.Hover, StateKind.Focus]);
        yield return ("text-input__search-box", () => Solo(Styled(new TextBox { Width = 240, Tag = "Search ..." }, "SearchBox")),
            [StateKind.Hover, StateKind.Focus]);

        yield return ("buttons__accent-button", () => Solo(Styled(new Button { Content = "Primary", Width = 120 }, "AccentButton")),
            [StateKind.Hover, StateKind.Pressed]);

        yield return ("selection__list-box", MakeListBox,
            [StateKind.Hover, StateKind.Selected]);
        yield return ("selection__slider", () => Solo(new Slider { Width = 240, Minimum = 0, Maximum = 100, Value = 40 }),
            [StateKind.Hover]);
    }

    private static (FrameworkElement root, Control target) Solo(Control c) => (c, c);

    private static T Styled<T>(T element, string styleKey) where T : FrameworkElement
    {
        element.SetResourceReference(FrameworkElement.StyleProperty, styleKey);
        return element;
    }

    private static (FrameworkElement root, Control target) MakeListBox()
    {
        var lb = new ListBox { Width = 240, Height = 120 };
        var first = new ListBoxItem { Content = "Solid1" };
        lb.Items.Add(first);
        lb.Items.Add(new ListBoxItem { Content = "Solid2" });
        lb.Items.Add(new ListBoxItem { Content = "Surface1" });
        return (lb, first); // the hover/selection target is the first item, not the list
    }

    /// <summary>Captures the listed interactive states of a control. Hover/pressed are forced via the
    /// controls' read-only DP keys (deterministic); focus uses real keyboard focus in an activated
    /// window; selection sets the item's IsSelected.</summary>
    private void CaptureStates(Theme theme, string slug,
        Func<(FrameworkElement root, Control target)> build, StateKind[] states, string outputDir)
    {
        var host = new ModernWindow(theme)
        {
            Width = 340,
            Height = 220,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = 80,
            Top = 80,
            ShowInTaskbar = false,
        };

        (FrameworkElement root, Control target) = build();
        var container = new Border
        {
            Padding = new Thickness(20),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Child = root,
        };
        container.SetResourceReference(Border.BackgroundProperty, "Brush.Background");

        host.Content = container;
        host.Show();
        host.Activate();
        MeasureArrange(host, container);

        foreach (StateKind state in states)
        {
            ApplyState(target, state, true);
            if (state == StateKind.Focus)
            {
                WaitMs(120);
            }
            host.UpdateLayout();
            SaveBitmap(RenderElement(container),
                DocPath(outputDir, slug + "--" + state.ToString().ToLowerInvariant(), theme));
            ApplyState(target, state, false);
        }

        host.Close();
    }

    private static void ApplyState(Control target, StateKind state, bool on)
    {
        switch (state)
        {
            case StateKind.Hover:
                // The Slider's hover visual lives on its Thumb, not the root, so target that.
                DependencyObject hoverEl = target is Slider slider
                    ? FindDescendant<Thumb>(slider) ?? (DependencyObject)target
                    : target;
                SetReadOnly(hoverEl, IsMouseOverKey, on);
                break;
            case StateKind.Pressed:
                if (target is ButtonBase)
                {
                    SetReadOnly(target, IsPressedKey, on);
                }
                break;
            case StateKind.Selected:
                if (target is ListBoxItem item)
                {
                    item.IsSelected = on;
                }
                break;
            case StateKind.Focus:
                if (on)
                {
                    target.Focus();
                    Keyboard.Focus(target);
                }
                else
                {
                    Keyboard.ClearFocus();
                }
                break;
        }
    }

    private static void MeasureArrange(Window host, FrameworkElement element)
    {
        host.UpdateLayout();
        element.Measure(new Size(300, double.PositiveInfinity));
        element.Arrange(new Rect(element.DesiredSize));
        host.UpdateLayout();
    }

    // IsMouseOver / IsPressed are read-only dependency properties; their backing keys let us force the
    // hover / pressed visuals deterministically for a screenshot (moving the real cursor doesn't
    // register reliably). Resolved once via reflection; null if a runtime ever renames them.
    private static readonly DependencyPropertyKey? IsMouseOverKey = ReadOnlyKey(typeof(UIElement), "IsMouseOverPropertyKey");
    private static readonly DependencyPropertyKey? IsPressedKey = ReadOnlyKey(typeof(ButtonBase), "IsPressedPropertyKey");

    private static DependencyPropertyKey? ReadOnlyKey(Type type, string fieldName) =>
        type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as DependencyPropertyKey;

    private static void SetReadOnly(DependencyObject element, DependencyPropertyKey? key, bool value)
    {
        if (key is not null)
        {
            element.SetValue(key, value);
        }
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                return match;
            }

            T? deeper = FindDescendant<T>(child);
            if (deeper is not null)
            {
                return deeper;
            }
        }

        return null;
    }

    private static IEnumerable<(string slug, Action show)> DialogJobs(Window host, Theme theme)
    {
        yield return ("dialogs__information", () => ModernMessageBox.Show(host, theme,
            "The export finished successfully.", "Information",
            ModernDialogButtons.Ok, ModernDialogIcon.Info));

        yield return ("dialogs__confirm", () => ModernMessageBox.Show(host, theme,
            "Delete the selected items?", "Confirm",
            ModernDialogButtons.YesNo, ModernDialogIcon.Question));

        yield return ("dialogs__error", () => ModernMessageBox.Show(host, theme,
            "The file could not be opened.", "Error",
            ModernDialogButtons.OkCancel, ModernDialogIcon.Error));

        yield return ("dialogs__custom-buttons", () =>
        {
            var buttons = new[]
            {
                new ModernDialogButton("Enable (recommended)", ModernDialogResult.Yes, IsDefault: true, Accent: true),
                new ModernDialogButton("Disable", ModernDialogResult.No, IsCancel: true),
            };
            ModernMessageBox.Show(host, theme,
                "Share anonymous usage data to help improve the add-in?",
                "Anonymous Usage Data", buttons, ModernDialogIcon.Question);
        });

        yield return ("dialogs__rich-content", () =>
        {
            var text = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxWidth = 400 };
            text.Inlines.Add(new System.Windows.Documents.Run("This dialog can host arbitrary content, including a "));
            text.Inlines.Add(new System.Windows.Documents.Hyperlink(
                new System.Windows.Documents.Run("hyperlink"))
            { NavigateUri = new Uri("https://learn.microsoft.com") });
            text.Inlines.Add(new System.Windows.Documents.Run(" that opens in the browser."));
            var buttons = new[]
            {
                new ModernDialogButton("OK", ModernDialogResult.Ok, IsDefault: true, IsCancel: true, Accent: true),
            };
            ModernMessageBox.Show(host, theme, text, "Details", buttons, ModernDialogIcon.Info);
        });
    }

    private void CaptureDialog(Window host, string slug, Theme theme, Action show, string outputDir)
    {
        // The timer ticks inside ShowDialog's nested message pump, captures the dialog, then closes
        // it (which unblocks the show() call below).
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(500) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Window? dialog = null;
            foreach (Window w in Windows)
            {
                if (!ReferenceEquals(w, host) && w.IsVisible)
                {
                    dialog = w;
                }
            }
            if (dialog is not null)
            {
                dialog.UpdateLayout();
                SaveBitmap(RenderElement(dialog), DocPath(outputDir, slug, theme));
                dialog.Close();
            }
        };
        timer.Start();
        try
        {
            show(); // blocks on ShowDialog until the timer closes the dialog
        }
        catch
        {
            timer.Stop();
        }
    }

    private static IEnumerable<(string slug, ToastType type, string title, string message)> ToastJobs()
    {
        yield return ("toasts__info", ToastType.Info, "Heads up", "Your settings were loaded.");
        yield return ("toasts__success", ToastType.Success, "Done", "Export completed.");
        yield return ("toasts__warning", ToastType.Warning, "Check input", "Some parameters were skipped.");
        yield return ("toasts__error", ToastType.Error, "Failed", "The file could not be opened.");
    }

    private void CaptureToast(Theme theme, string slug, ToastType type, string title, string message, string outputDir)
    {
        ModernWindow host = CreateOffscreenHost(theme, 380, 150);
        var surface = new Grid();
        surface.SetResourceReference(Panel.BackgroundProperty, "Brush.Background");
        host.Content = surface;
        host.Show();
        host.UpdateLayout();

        ModernToast.Show(host, message, type, title);
        WaitMs(450); // let the toast animate in
        host.UpdateLayout();
        SaveBitmap(RenderElement(host), DocPath(outputDir, slug, theme));
        host.Close();
    }

    private static string DocPath(string dir, string slug, Theme theme) =>
        Path.Combine(dir, $"{slug}-{theme}.png".ToLowerInvariant());

    private static string Slug(string s) =>
        Regex.Replace(s.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');

    private static RenderTargetBitmap RenderElement(FrameworkElement element)
    {
        int w = Math.Max(1, (int)Math.Ceiling(element.ActualWidth));
        int h = Math.Max(1, (int)Math.Ceiling(element.ActualHeight));
        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(element);
        return rtb;
    }

    /// <summary>Pumps the dispatcher for ~<paramref name="ms"/> ms (lets toasts animate in / layout
    /// settle) without blocking the message loop.</summary>
    private static void WaitMs(int ms)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

}
