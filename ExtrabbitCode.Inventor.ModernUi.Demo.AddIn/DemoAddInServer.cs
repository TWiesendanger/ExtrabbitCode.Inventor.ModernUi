using System.Runtime.InteropServices;
using System.Windows.Interop;
using Inventor;

namespace ExtrabbitCode.Inventor.ModernUi.Demo.AddIn;

/// <summary>
/// Minimal Inventor 2025+ add-in that demonstrates the ExtrabbitCode Modern UI library. It reads
/// the active Inventor theme and the Inventor UI font, then shows the styled control gallery in a
/// themed window. Intentionally tiny — it is a visual showcase, not a feature.
/// </summary>
[Guid(AddInGuid)]
[ProgId("ExtrabbitCode.Inventor.ModernUi.Demo.DemoAddInServer")]
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
public class DemoAddInServer : ApplicationAddInServer
{
    private const string AddInGuid = "80423f77-2aef-4502-91da-29bd193cc7bd";
    private const string ButtonInternalName = "ExtrabbitCode_ModernUi_ShowGallery";

    private Application? _app;
    private ButtonDefinition? _button;
    private System.Drawing.Image? _icon16;
    private System.Drawing.Image? _icon32;

    public void Activate(ApplicationAddInSite addInSiteObject, bool firstTime)
    {
        _app = addInSiteObject.Application;

        _icon16 = LoadImage("ModernUi-16.png");
        _icon32 = LoadImage("ModernUi-32.png");

        ControlDefinitions defs = _app.CommandManager.ControlDefinitions;
        _button = defs.AddButtonDefinition(
            "Modern UI\nGallery",
            ButtonInternalName,
            CommandTypesEnum.kShapeEditCmdType,
            "{" + AddInGuid + "}",
            "Show the Modern UI control gallery",
            "Opens a themed dialog showcasing the ExtrabbitCode Modern UI controls in the active Inventor theme and font.",
            PictureConverter.ToPictureDisp(_icon16),
            PictureConverter.ToPictureDisp(_icon32));

        _button.OnExecute += OnShowGallery;

        AddButtonToRibbons();
    }

    public void Deactivate()
    {
        if (_button is not null)
        {
            _button.OnExecute -= OnShowGallery;
        }

        _button = null;
        _app = null;
        _icon16?.Dispose();
        _icon16 = null;
        _icon32?.Dispose();
        _icon32 = null;

        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    private static System.Drawing.Image LoadImage(string resourceName)
    {
        using System.IO.Stream stream = typeof(DemoAddInServer).Assembly.GetManifestResourceStream(resourceName)!;
        using var source = System.Drawing.Image.FromStream(stream);
        return new System.Drawing.Bitmap(source); // independent copy so the stream can close
    }

    /// <summary>Legacy member of the add-in interface; unused.</summary>
    public void ExecuteCommand(int commandID)
    {
    }

    /// <summary>No automation object is exposed.</summary>
    public object? Automation => null;

    private void OnShowGallery(NameValueMap context)
    {
        if (_app is null)
        {
            return;
        }

        Theme theme = ReadTheme(_app);
        (FontOptions font, string fontLabel) = ReadFont(_app);

        GalleryView gallery = new();
        gallery.Initialize(theme, $"Inventor font: {fontLabel}");

        ModernWindow window = new(theme, font: font)
        {
            Title = "Modern UI — control gallery",
            Content = gallery,
        };

        // Own the dialog to Inventor's main window so it stays on top of Inventor.
        _ = new WindowInteropHelper(window) { Owner = new IntPtr(_app.MainFrameHWND) };
        window.Show();
    }

    /// <summary>Reads the active Inventor theme (Inventor 2025+); defaults to Light otherwise.</summary>
    private static Theme ReadTheme(Application app)
    {
        return app.ThemeManager.ActiveTheme.Name == "LightTheme" ? Theme.Light : Theme.Dark;
    }

    /// <summary>Reads Inventor's UI font from GeneralOptions and maps it to <see cref="FontOptions"/>.</summary>
    private static (FontOptions font, string label) ReadFont(Application app)
    {
        try
        {
            string family = app.GeneralOptions.TextAppearance;
            int points = app.GeneralOptions.TextSize;
            FontOptions font = FontOptions.FromInventor(family, points);
            return (font, $"{family} {points}pt → {font.NormalSize:0.#}px");
        }
        catch
        {
            FontOptions font = FontOptions.Default;
            return (font, $"{font.Family.Source} {font.NormalSize:0.#}px (system)");
        }
    }

    /// <summary>Adds the button to the Tools tab of the common ribbons (best-effort per ribbon).</summary>
    private void AddButtonToRibbons()
    {
        if (_app is null || _button is null)
        {
            return;
        }

        string[] ribbonNames = ["ZeroDoc", "Part", "Assembly", "Drawing"];
        foreach (string ribbonName in ribbonNames)
        {
            try
            {
                Ribbon ribbon = _app.UserInterfaceManager.Ribbons[ribbonName];
                RibbonTab tab = ribbon.RibbonTabs["id_TabTools"];
                RibbonPanel panel = tab.RibbonPanels.Add(
                    "Modern UI",
                    "ExtrabbitCode_ModernUi_Panel_" + ribbonName,
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
