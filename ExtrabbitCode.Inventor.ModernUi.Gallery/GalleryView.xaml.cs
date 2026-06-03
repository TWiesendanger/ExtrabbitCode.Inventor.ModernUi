using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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

    public GalleryView()
    {
        InitializeComponent();
        _pages = BuildPages();
        NavList.ItemsSource = _pages;
        NavList.SelectedIndex = 0;

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

        PageTitle.Text = page.Name;
        ContentHost.Children.Clear();
        foreach (DemoItem item in page.Items)
        {
            ContentHost.Children.Add(BuildBlock(item));
        }
    }

    private void OnToggleTheme(object sender, RoutedEventArgs e)
    {
        _theme = _theme == Theme.Light ? Theme.Dark : Theme.Light;

        Window? window = Window.GetWindow(this);
        if (window is not null)
        {
            ModernUi.SetTheme(window, _theme);
        }

        UpdateInfo();
        UpdateToggleIcon();
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
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 24) };

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

        stack.Children.Add(BuildCodeBox(item.Xaml));
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

    private static FrameworkElement BuildCodeBox(string code)
    {
        var border = new Border { CornerRadius = new CornerRadius(4), Padding = new Thickness(12), BorderThickness = new Thickness(1) };
        border.SetResourceReference(Border.BackgroundProperty, "Brush.Control");
        border.SetResourceReference(Border.BorderBrushProperty, "Brush.Border");

        var box = new TextBox
        {
            Text = code,
            IsReadOnly = true,
            Style = null, // strip the themed TextBox style; this is a code label, not an input
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            FontFamily = new FontFamily("Cascadia Code, Consolas, Courier New"),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        box.SetResourceReference(ForegroundProperty, "Brush.ForegroundMuted");
        border.Child = box;
        return border;
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
        new DemoPage("Buttons",
        [
            new DemoItem("Default button", """
                <Button Content="Default" Width="120" HorizontalAlignment="Left" />
                """),
            new DemoItem("Accent button", """
                <Button Content="Primary" Width="120" HorizontalAlignment="Left"
                        Style="{DynamicResource AccentButton}" />
                """),
            new DemoItem("Disabled button", """
                <Button Content="Disabled" Width="120" HorizontalAlignment="Left" IsEnabled="False" />
                """),
        ]),

        new DemoPage("Text input",
        [
            new DemoItem("Text box", """
                <TextBox Width="240" HorizontalAlignment="Left" Text="Editable text" />
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
        ]),

        new DemoPage("Display",
        [
            new DemoItem("Progress bar", """
                <ProgressBar Value="60" Maximum="100" />
                """),
            new DemoItem("Card", """
                <Border Style="{DynamicResource Card}">
                    <TextBlock Text="Card surface" />
                </Border>
                """),
        ]),
    ];
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
