using System.Reflection;
using System.Runtime.InteropServices;

namespace ExtrabbitCode.Inventor.ModernUi.Coexistence;

/// <summary>
/// Converts an image to the IPictureDisp Inventor ribbon icons expect — the same approach used in
/// the other ExtrabbitCode Inventor add-ins (<c>Bitmap.GetHicon</c> + <c>OleCreatePictureIndirect</c>).
/// Uses System.Drawing for the bitmap; no WinForms. Returns the picture as <see cref="object"/> with
/// the IPictureDisp IID, so no direct <c>stdole</c> reference is required.
/// </summary>
internal static class PictureDispConverter
{
    private const int PictypeIcon = 3;
    private static readonly Guid IPictureDispGuid = new("7BF80981-BF32-101A-8BBB-00AA00300CAB");

    [StructLayout(LayoutKind.Sequential)]
    private struct PictDesc
    {
        public int cbSizeofstruct;
        public int picType;
        public IntPtr handle;
    }

    [DllImport("OleAut32.dll", ExactSpelling = true, PreserveSig = false)]
    private static extern void OleCreatePictureIndirect(
        ref PictDesc pPictDesc,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Bool)] bool fOwn,
        [MarshalAs(UnmanagedType.IUnknown)] out object picture);

    /// <summary>Loads an embedded PNG and returns its IPictureDisp, or null on any failure.</summary>
    public static object? FromResource(Assembly assembly, string resourceName)
    {
        try
        {
            using System.IO.Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                return null;
            }

            using System.Drawing.Bitmap bitmap = new(stream);
            return ToIPictureDisp(bitmap);
        }
        catch
        {
            return null;
        }
    }

    public static object ToIPictureDisp(System.Drawing.Bitmap bitmap)
    {
        IntPtr hIcon = bitmap.GetHicon();
        PictDesc desc = new()
        {
            cbSizeofstruct = Marshal.SizeOf<PictDesc>(),
            picType = PictypeIcon,
            handle = hIcon,
        };
        Guid guid = IPictureDispGuid;
        // fOwn=true transfers hIcon ownership to the OLE picture object.
        OleCreatePictureIndirect(ref desc, ref guid, true, out object picture);
        return picture;
    }
}
