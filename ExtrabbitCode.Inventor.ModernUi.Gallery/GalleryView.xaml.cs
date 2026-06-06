using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        ImageSource? iam = LoadIcon("iam64x64.png");
        ImageSource? ipt = LoadIcon("ipt64x64.png");
        ImageSource? idw = LoadIcon("idw64x64.png");
        ImageSource? ipn = LoadIcon("ipn64x64.png");
        ImageSource? other = LoadIcon("everythingelse64x64.png");

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
            new DemoItem("Expander", """
                <Expander Header="Output" IsExpanded="True">
                    <StackPanel>
                        <TextBlock Text="Body Name" Margin="0,0,0,4" />
                        <TextBox Text="Solid1" Width="200" HorizontalAlignment="Left" />
                    </StackPanel>
                </Expander>
                """),
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
            new DemoItem("Tree view with checkboxes", """
                <TreeView Width="280" Height="170" HorizontalAlignment="Left">
                    <TreeViewItem IsExpanded="True">
                        <TreeViewItem.Header>
                            <CheckBox Content="All layers" IsChecked="True" />
                        </TreeViewItem.Header>
                        <TreeViewItem>
                            <TreeViewItem.Header><CheckBox Content="Dimensions" IsChecked="True" /></TreeViewItem.Header>
                        </TreeViewItem>
                        <TreeViewItem>
                            <TreeViewItem.Header><CheckBox Content="Sketches" /></TreeViewItem.Header>
                        </TreeViewItem>
                        <TreeViewItem>
                            <TreeViewItem.Header><CheckBox Content="Work features" IsChecked="True" /></TreeViewItem.Header>
                        </TreeViewItem>
                    </TreeViewItem>
                </TreeView>
                """),
            new DemoItem("Assembly tree (file-type icons)", """
                <TreeView Width="300" Height="250" HorizontalAlignment="Left">
                    <TreeViewItem IsExpanded="True">
                        <TreeViewItem.Header>
                            <StackPanel Orientation="Horizontal">
                                <Image Source="/resources/iam64x64.png" Width="16" Height="16"
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
