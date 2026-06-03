using System.Drawing;
using System.Windows.Forms;

namespace ExtrabbitCode.Inventor.ModernUi.Demo.AddIn;

/// <summary>
/// Converts a <see cref="Image"/> to the <c>stdole.IPictureDisp</c> that Inventor's ribbon API
/// (<c>ButtonDefinition</c> icons) expects. Uses the standard <see cref="AxHost"/> helper.
/// </summary>
internal sealed class PictureConverter : AxHost
{
    private PictureConverter() : base("00000000-0000-0000-0000-000000000000")
    {
    }

    public static object ToPictureDisp(Image image) => GetIPictureDispFromPicture(image);
}
