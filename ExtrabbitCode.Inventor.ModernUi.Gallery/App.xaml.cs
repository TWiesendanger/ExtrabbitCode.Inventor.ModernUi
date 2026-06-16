using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ExtrabbitCode.Inventor.ModernUi;
using ExtrabbitCode.Inventor.ModernUi.Demo;

namespace ExtrabbitCode.Inventor.ModernUi.Gallery;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Headless capture mode: "--shoot <dir>" renders the galleries to PNG and exits.
        string[] args = e.Args;
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

    private static void SaveWindowPng(Window window, string path)
    {
        int w = (int)Math.Ceiling(window.ActualWidth);
        int h = (int)Math.Ceiling(window.ActualHeight);
        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(window);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using FileStream fs = File.Create(path);
        encoder.Save(fs);
    }

}
