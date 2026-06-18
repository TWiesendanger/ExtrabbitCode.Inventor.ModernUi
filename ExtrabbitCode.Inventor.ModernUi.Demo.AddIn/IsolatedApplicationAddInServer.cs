using System.Reflection;
using System.Runtime.Loader;
using Inventor;

namespace ExtrabbitCode.Inventor.ModernUi.Demo.AddIn;

/// <summary>
/// Base add-in server that runs the add-in inside its own <see cref="AddinLoadContext"/>.
/// <para>
/// Inventor activates the add-in in the default load context. On first <see cref="Activate"/> we
/// detect that, build an <see cref="AddinLoadContext"/> for this add-in's folder, re-create the
/// concrete add-in type inside it, and delegate to that isolated instance. The second time through
/// (now running in the custom context) we run the real <see cref="OnActivate"/>. Every type the
/// add-in touches — including its private copy of the ModernUi library — therefore loads isolated,
/// so this add-in coexists with other ModernUi consumers in the same Inventor process.
/// </para>
/// </summary>
public abstract class IsolatedApplicationAddInServer : ApplicationAddInServer
{
    private object? _isolatedInstance;

    public object? Automation => null;

    public void Activate(ApplicationAddInSite addInSiteObject, bool firstTime)
    {
        Type type = GetType();

        if (AssemblyLoadContext.GetLoadContext(type.Assembly) == AssemblyLoadContext.Default)
        {
            AddinLoadContext context = new(type.Name, type.Assembly.Location);
            Assembly isolatedAssembly = context.LoadFromAssemblyName(type.Assembly.GetName());
            Type isolatedType = isolatedAssembly.GetType(type.FullName!, throwOnError: true)!;
            _isolatedInstance = Activator.CreateInstance(isolatedType);
            isolatedType.GetMethod(nameof(Activate))!
                .Invoke(_isolatedInstance, [addInSiteObject, firstTime]);
            return;
        }

        OnActivate(addInSiteObject, firstTime);
    }

    public void Deactivate()
    {
        if (_isolatedInstance is not null)
        {
            _isolatedInstance.GetType().GetMethod(nameof(Deactivate))!.Invoke(_isolatedInstance, null);
            _isolatedInstance = null;
            return;
        }

        OnDeactivate();
    }

    [Obsolete("Deprecated in the Inventor API; required for COM compatibility.")]
    public void ExecuteCommand(int commandID)
    {
    }

    /// <summary>Runs in the isolated context. Implement the real add-in behavior here.</summary>
    protected abstract void OnActivate(ApplicationAddInSite site, bool firstTime);

    /// <summary>Runs in the isolated context.</summary>
    protected abstract void OnDeactivate();
}
