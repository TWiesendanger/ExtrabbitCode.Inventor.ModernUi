using System.Runtime.InteropServices;

namespace ExtrabbitCode.Inventor.ModernUi.Coexistence.B;

/// <summary>
/// Coexistence test add-in B. Ships ModernUi <b>V2</b> and exercises its version-only method
/// <c>GetV2()</c> (which does not exist in V1). Loads its library copy isolated via
/// <see cref="IsolatedApplicationAddInServer"/>.
/// </summary>
[Guid(Guid)]
[ProgId("ExtrabbitCode.Inventor.ModernUi.Coexistence.B.AddInServerB")]
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
public sealed class AddInServerB : CoexistenceAddInBase
{
    public const string Guid = "3c646f71-d600-43f9-8373-40dd27209bd5";

    protected override string AddInGuid => Guid;

    protected override string DisplayName => "Modern UI Coexistence B (V2)";

    protected override string VersionMethod => "GetV2";
}
