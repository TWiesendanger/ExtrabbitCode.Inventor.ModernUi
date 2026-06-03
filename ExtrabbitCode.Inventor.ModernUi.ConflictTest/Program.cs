using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Windows;
using System.Windows.Controls;

// The ultimate coexistence test for ExtrabbitCode.Inventor.ModernUi: load TWO DIFFERENT VERSIONS of
// the library (V1 in plugins\A, V2 in plugins\B — built by the .csproj), each into its own isolated
// AssemblyLoadContext (the "manual isolation" pattern an Inventor add-in uses), and in each:
//
//   * call a method that exists ONLY in that version (GetV1 / GetV2) — proving no type unification,
//   * confirm the OTHER version's method is absent in this copy,
//   * theme a real Window with the styled controls — proving no DependencyProperty / resource clash.
//
// Two add-ins shipping two different library versions into one Inventor.exe is exactly this. PASS
// means they coexist with zero conflicts.

namespace ExtrabbitCode.Inventor.ModernUi.ConflictTest;

internal static class Program
{
    private const string LibraryName = "ExtrabbitCode.Inventor.ModernUi";

    [STAThread]
    private static int Main()
    {
        string root = AppContext.BaseDirectory;
        string dirA = Path.Combine(root, "plugins", "A");
        string dirB = Path.Combine(root, "plugins", "B");

        Console.WriteLine("Loading two DIFFERENT versions of the library, each in its own AssemblyLoadContext:");
        Console.WriteLine();

        try
        {
            Result a = Exercise(dirA, expectedVersion: "V1", ownMethod: "GetV1", foreignMethod: "GetV2");
            Result b = Exercise(dirB, expectedVersion: "V2", ownMethod: "GetV2", foreignMethod: "GetV1");

            bool versionsDiffer = a.AssemblyVersion != b.AssemblyVersion;
            bool ok = a.Ok && b.Ok && versionsDiffer;

            Console.WriteLine();
            Console.WriteLine($"Versions differ: {a.AssemblyVersion} vs {b.AssemblyVersion}  ->  {versionsDiffer}");
            Console.WriteLine();

            if (ok)
            {
                Console.WriteLine("RESULT: PASS - two different library versions coexist in one process: each ran its");
                Console.WriteLine("        own version-only method and themed a window, with no conflicts.");
                return 0;
            }

            Console.WriteLine("RESULT: FAIL - coexistence expectations were not met (see details above).");
            return 1;
        }
        catch (Exception ex)
        {
            Exception rootEx = ex;
            while (rootEx.InnerException is not null)
            {
                rootEx = rootEx.InnerException;
            }

            Console.WriteLine();
            Console.WriteLine("RESULT: FAIL - a copy could not initialize.");
            Console.WriteLine($"  {rootEx.GetType().FullName}: {rootEx.Message}");
            return 1;
        }
    }

    private static Result Exercise(string pluginDir, string expectedVersion, string ownMethod, string foreignMethod)
    {
        string name = Path.GetFileName(pluginDir);
        string libPath = Path.Combine(pluginDir, LibraryName + ".dll");
        if (!File.Exists(libPath))
        {
            throw new FileNotFoundException($"Version not built: {libPath}. Build the ConflictTest project first.");
        }

        AddinLoadContext alc = new AddinLoadContext(name, libPath);
        Assembly lib = alc.LoadFromAssemblyName(new AssemblyName(LibraryName));
        Version asmVersion = lib.GetName().Version!;

        // 1. Version-only method: must have its own, must NOT have the other version's.
        Type marker = lib.GetType($"{LibraryName}.CoexistenceMarker", throwOnError: true)!;
        string version = (string)marker.GetProperty("Version")!.GetValue(null)!;
        MethodInfo? own = marker.GetMethod(ownMethod);
        MethodInfo? foreign = marker.GetMethod(foreignMethod);
        string greeting = own is not null ? (string)own.Invoke(null, null)! : "<missing>";

        bool versionMethodsOk = version == expectedVersion && own is not null && foreign is null;

        // 2. Theme a real window with styled controls (exercises static ctors + resource resolution).
        bool windowOk = ThemeAWindow(lib, $"[{name}] {greeting}");

        Console.WriteLine($"[{name}] asm {asmVersion}  marker={version}  {ownMethod}()=\"{greeting}\"  {foreignMethod} present: {foreign is not null}  window: {(windowOk ? "ok" : "FAILED")}");
        Console.WriteLine($"        ctx: {AssemblyLoadContext.GetLoadContext(lib)?.Name}  @ {lib.Location}");

        return new Result(asmVersion.ToString(), versionMethodsOk && windowOk);
    }

    private static bool ThemeAWindow(Assembly lib, string content)
    {
        Type modernUi = lib.GetType($"{LibraryName}.ModernUi", throwOnError: true)!;
        Type themeType = lib.GetType($"{LibraryName}.Theme", throwOnError: true)!;
        object darkTheme = Enum.Parse(themeType, "Dark");
        MethodInfo apply = modernUi.GetMethod("Apply", BindingFlags.Public | BindingFlags.Static)!;

        Window window = new() { Title = content, Width = 360, Height = 240 };
        apply.Invoke(null, [window, darkTheme, null, null]);
        window.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Children =
            {
                new Button { Content = content },
                new TextBox { Text = "styled" },
                new CheckBox { Content = "styled", IsChecked = true },
            },
        };
        window.Show();
        window.Close();
        return true;
    }

    private readonly record struct Result(string AssemblyVersion, bool Ok);

    // Manual isolation: each plugin folder loads in its own context, resolving its assemblies from
    // that folder's deps.json while deferring the WPF framework to the shared default context. This
    // mirrors the AddinLoadContext pattern an Inventor add-in uses to isolate its dependencies.
    private sealed class AddinLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public AddinLoadContext(string name, string mainAssemblyPath) : base(name, isCollectible: false)
            => _resolver = new AssemblyDependencyResolver(mainAssemblyPath);

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            string? path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path is not null ? LoadFromAssemblyPath(path) : null; // null -> shared default context
        }
    }
}