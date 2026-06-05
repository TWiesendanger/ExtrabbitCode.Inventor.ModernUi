using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Inventor;

namespace ExtrabbitCode.Inventor.ModernUi.Coexistence;

/// <summary>
/// Shared behavior for the two coexistence test add-ins. Each derives from this, supplies its own
/// identity, and ships its own (different) build of the ModernUi library. On the ribbon button it
/// opens a themed <see cref="ModernWindow"/> showing which library version it loaded and the result
/// of a version-only method (reflected, since the method differs between V1 and V2). Runs inside the
/// add-in's isolated <see cref="AddinLoadContext"/> (see <see cref="IsolatedApplicationAddInServer"/>).
/// </summary>
public abstract class CoexistenceAddInBase : IsolatedApplicationAddInServer
{
    private global::Inventor.Application? _app;
    private ButtonDefinition? _button;

    /// <summary>This add-in's GUID (must match its [Guid] and .addin/.manifest).</summary>
    protected abstract string AddInGuid { get; }

    /// <summary>Display name shown on the ribbon and the dialog title.</summary>
    protected abstract string DisplayName { get; }

    /// <summary>The version-only method to call on CoexistenceMarker ("GetV1" or "GetV2").</summary>
    protected abstract string VersionMethod { get; }

    protected override void OnActivate(ApplicationAddInSite site, bool firstTime)
    {
        _app = site.Application;

        Assembly self = typeof(CoexistenceAddInBase).Assembly;
        object smallIcon = PictureDispConverter.FromResource(self, "ModernUi-16.png") ?? (object)Type.Missing;
        object largeIcon = PictureDispConverter.FromResource(self, "ModernUi-32.png") ?? (object)Type.Missing;

        ControlDefinitions defs = _app.CommandManager.ControlDefinitions;
        _button = defs.AddButtonDefinition(
            DisplayName,
            "ExtrabbitCode_Coexistence_" + VersionMethod,
            CommandTypesEnum.kShapeEditCmdType,
            "{" + AddInGuid + "}",
            DisplayName,
            DisplayName,
            smallIcon,
            largeIcon);

        _button.OnExecute += OnExecute;
        AddButtonToRibbons();
    }

    protected override void OnDeactivate()
    {
        if (_button is not null)
        {
            _button.OnExecute -= OnExecute;
        }

        _button = null;
        _app = null;
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    private static System.Windows.Media.ImageSource? TryLoadWindowIcon()
    {
        try
        {
            using System.IO.Stream? stream = typeof(CoexistenceAddInBase).Assembly.GetManifestResourceStream("ModernUi-64.png");
            if (stream is null)
            {
                return null;
            }

            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
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

    private void OnExecute(NameValueMap context)
    {
        if (_app is null)
        {
            return;
        }

        Theme theme = ReadTheme(_app);
        FontOptions font = ReadFont(_app);

        // Which library version actually loaded into this add-in's isolated context, and its
        // version-only method (reflected, because the method name differs between builds).
        string version = CoexistenceMarker.Version;
        string asmVersion = typeof(CoexistenceMarker).Assembly.GetName().Version!.ToString();
        string greeting = (string)(typeof(CoexistenceMarker).GetMethod(VersionMethod)?.Invoke(null, null) ?? "<missing>");

        ModernWindow window = new ModernWindow(theme, font: font)
        {
            Title = DisplayName,
            Icon = TryLoadWindowIcon(),
            Content = BuildContent(version, asmVersion, greeting),
        };
        _ = new WindowInteropHelper(window) { Owner = new IntPtr(_app.MainFrameHWND) };
        window.Show();
    }

    private FrameworkElement BuildContent(string version, string asmVersion, string greeting)
    {
        StackPanel panel = new() { Margin = new Thickness(20), MinWidth = 360 };

        TextBlock title = new() { Text = DisplayName };
        title.SetResourceReference(FrameworkElement.StyleProperty, "TitleTextStyle");
        panel.Children.Add(title);

        panel.Children.Add(new TextBlock
        {
            Text = $"Loaded ModernUi {version}  (assembly {asmVersion})",
            Margin = new Thickness(0, 0, 0, 2),
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"{VersionMethod}() = \"{greeting}\"",
            Margin = new Thickness(0, 0, 0, 16),
        });

        StackPanel buttons = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        buttons.Children.Add(new Button { Content = "Default", Margin = new Thickness(0, 0, 8, 0) });
        Button accent = new() { Content = "Primary" };
        accent.SetResourceReference(FrameworkElement.StyleProperty, "AccentButton");
        buttons.Children.Add(accent);
        panel.Children.Add(buttons);

        panel.Children.Add(new System.Windows.Controls.TextBox { Text = "Styled text box", Margin = new Thickness(0, 0, 0, 8) });
        panel.Children.Add(new CheckBox { Content = "Styled check box", IsChecked = true, Margin = new Thickness(0, 0, 0, 8) });

        ComboBox combo = new() { Width = 200, HorizontalAlignment = HorizontalAlignment.Left };
        combo.Items.Add(new ComboBoxItem { Content = "First option" });
        combo.Items.Add(new ComboBoxItem { Content = "Second option" });
        combo.SelectedIndex = 0;
        panel.Children.Add(combo);

        return panel;
    }

    private static Theme ReadTheme(global::Inventor.Application app)
        => app.ThemeManager.ActiveTheme.Name == "LightTheme" ? Theme.Light : Theme.Dark;

    private static FontOptions ReadFont(global::Inventor.Application app)
    {
        try
        {
            return FontOptions.FromInventor(app.GeneralOptions.TextAppearance, app.GeneralOptions.TextSize);
        }
        catch
        {
            return FontOptions.Default;
        }
    }

    private void AddButtonToRibbons()
    {
        if (_app is null || _button is null)
        {
            return;
        }

        foreach (string ribbonName in new[] { "ZeroDoc", "Part", "Assembly", "Drawing" })
        {
            try
            {
                Ribbon ribbon = _app.UserInterfaceManager.Ribbons[ribbonName];
                RibbonTab tab = ribbon.RibbonTabs["id_TabTools"];
                RibbonPanel panel = tab.RibbonPanels.Add(
                    "Modern UI Coexistence",
                    "ExtrabbitCode_Coexistence_Panel_" + VersionMethod + "_" + ribbonName,
                    "{" + AddInGuid + "}");
                panel.CommandControls.AddButton(_button, true);
            }
            catch
            {
                // Some ribbons/tabs may not exist in every configuration — skip silently.
            }
        }
    }
}
