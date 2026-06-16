using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
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
        }

        Shutdown();
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
