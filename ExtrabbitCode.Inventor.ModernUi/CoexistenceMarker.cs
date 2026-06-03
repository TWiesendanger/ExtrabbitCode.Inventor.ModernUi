namespace ExtrabbitCode.Inventor.ModernUi;

/// <summary>
/// Test-only marker whose exposed member differs per build constant (<c>MODERNUI_V2</c>). The
/// coexistence test builds this library twice — once normally (V1) and once with
/// <c>-p:DefineConstants=MODERNUI_V2</c> (V2) — so the two builds expose <b>different</b> methods
/// (<c>GetV1</c> vs <c>GetV2</c>). Proving at runtime that one build has a method the other
/// lacks is the strongest evidence that two independent versions are loaded side by side, with no
/// type unification and no shared global state. Not part of the styling surface.
/// </summary>
public static class CoexistenceMarker
{
    /// <summary>This build's version tag ("V1" or "V2").</summary>
    public static string Version =>
#if MODERNUI_V2
        "V2";
#else
        "V1";
#endif

#if MODERNUI_V2
    /// <summary>Present only in the V2 build.</summary>
    public static string GetV2() => "Hello from ModernUi V2";
#else
    /// <summary>Present only in the V1 build.</summary>
    public static string GetV1() => "Hello from ModernUi V1";
#endif
}
