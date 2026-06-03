using System.IO;
using System.Windows;
using System.Windows.Controls;
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

        new LauncherWindow().Show();
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
                    string path = Path.Combine(outputDir, $"gallery-{theme}.png".ToLowerInvariant());
                    SaveWindowPng(window, path);
                    window.Close();
                    ShootNext();
                });
            };

            window.Show();
        }

        ShootNext();
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

    /// <summary>
    /// Small dark launcher: two buttons that open the gallery in light and dark. Built in code so
    /// the library's only custom type (ModernWindow) never enters this app's XAML/BAML.
    /// </summary>
    private sealed class LauncherWindow : ModernWindow
    {
        public LauncherWindow() : base(Theme.Dark)
        {
            Title = "ExtrabbitCode Modern UI — Gallery";
            Icon = GalleryView.LoadBranding();
            Width = 380;
            Height = 240;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var dark = new Button { Content = "Open gallery — Dark", Margin = new Thickness(0, 0, 0, 10) };
            dark.SetResourceReference(StyleProperty, "AccentButton");
            dark.Click += (_, _) => OpenGallery(Theme.Dark);

            var light = new Button { Content = "Open gallery — Light" };
            light.Click += (_, _) => OpenGallery(Theme.Light);

            Content = new StackPanel
            {
                Margin = new Thickness(24),
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new TextBlock { Text = "Pick a theme to preview the styled controls." },
                    new StackPanel { Height = 12 },
                    dark,
                    light,
                },
            };
        }

        private void OpenGallery(Theme theme)
        {
            var gallery = new GalleryView();
            gallery.Initialize(
                theme,
                $"Font: {FontOptions.Default.Family.Source} {FontOptions.Default.NormalSize:0.#}px");

            new ModernWindow(theme)
            {
                Title = $"Control gallery — {theme}",
                Owner = this,
                Content = gallery,
            }.Show();
        }
    }
}
