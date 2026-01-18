namespace Monolith.FireWall.WebUI.Services;

/// <summary>
/// Controller feature provider that filters out controllers from package assemblies.
/// Package assemblies may reference Monolith.FireWall.Core which isn't available in WebUI runtime.
/// Uses IApplicationModelProvider approach instead of ControllerFeatureProvider.
/// </summary>
public class PackageControllerFeatureProvider : Microsoft.AspNetCore.Mvc.ApplicationModels.IApplicationModelProvider
{
    private static readonly HashSet<string> PackageAssemblies = new(StringComparer.OrdinalIgnoreCase)
    {
        "Monolith.Diagnostics",
        "Monolith.Network",
        "Monolith.Vpn"
    };

    public int Order => -1000; // Run early

    public void OnProvidersExecuting(Microsoft.AspNetCore.Mvc.ApplicationModels.ApplicationModelProviderContext context)
    {
        // Remove controllers from package assemblies
        var controllersToRemove = context.Result.Controllers
            .Where(c => 
            {
                var assemblyName = c.ControllerType.Assembly.GetName().Name;
                return assemblyName != null && PackageAssemblies.Contains(assemblyName);
            })
            .ToList();

        foreach (var controller in controllersToRemove)
        {
            context.Result.Controllers.Remove(controller);
        }
    }

    public void OnProvidersExecuted(Microsoft.AspNetCore.Mvc.ApplicationModels.ApplicationModelProviderContext context)
    {
        // No-op
    }
}
