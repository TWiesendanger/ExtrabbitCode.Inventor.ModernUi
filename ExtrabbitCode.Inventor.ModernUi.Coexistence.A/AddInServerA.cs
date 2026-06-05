using System.Runtime.InteropServices;

namespace ExtrabbitCode.Inventor.ModernUi.Coexistence.A;

/// <summary>
/// Coexistence test add-in A. Ships ModernUi <b>V1</b> and exercises its version-only method
/// <c>GetV1()</c>. Loads its library copy isolated via <see cref="IsolatedApplicationAddInServer"/>.
/// </summary>
[Guid(Guid)]
[ProgId("ExtrabbitCode.Inventor.ModernUi.Coexistence.A.AddInServerA")]
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
public sealed class AddInServerA : CoexistenceAddInBase
{
    public const string Guid = "5f78f343-a56f-43b8-8454-5db53f2d9dbe";

    protected override string AddInGuid => Guid;

    protected override string DisplayName => "Modern UI Coexistence A (V1)";

    protected override string VersionMethod => "GetV1";
}
