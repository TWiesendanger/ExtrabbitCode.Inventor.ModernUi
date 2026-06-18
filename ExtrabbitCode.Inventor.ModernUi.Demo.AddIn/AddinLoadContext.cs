using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace ExtrabbitCode.Inventor.ModernUi.Demo.AddIn;

/// <summary>
/// Per-add-in isolation context. Resolves the add-in and its dependencies (notably its own copy of
/// ExtrabbitCode.Inventor.ModernUi) from the add-in's own folder, while deferring the shared WPF
/// framework to the default context. This is what lets this add-in coexist with other ModernUi
/// consumers in one Inventor.exe without an assembly-identity clash.
/// </summary>
public sealed class AddinLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly string _directory;

    public AddinLoadContext(string name, string mainAssemblyPath) : base(name, isCollectible: false)
    {
        _resolver = new AssemblyDependencyResolver(mainAssemblyPath);
        _directory = Path.GetDirectoryName(mainAssemblyPath)!;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Prefer the deps.json resolver; fall back to probing the add-in folder (the add-in's own
        // assembly isn't listed as a dependency of itself).
        string? path = _resolver.ResolveAssemblyToPath(assemblyName);
        if (path is null)
        {
            string candidate = Path.Combine(_directory, assemblyName.Name + ".dll");
            if (File.Exists(candidate))
            {
                path = candidate;
            }
        }

        // null -> let the shared default context load it (WPF, BCL, Inventor interop).
        return path is not null ? LoadFromAssemblyPath(path) : null;
    }
}
