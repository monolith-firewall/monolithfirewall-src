using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Monolith.FireWall.WebUI.Services;

/// <summary>
/// Controller feature provider that filters out controllers from package assemblies.
/// Package assemblies may reference Monolith.FireWall.Core which isn't available in WebUI runtime.
/// </summary>
public class PackageControllerFeatureProvider : ControllerFeatureProvider
{
    private static readonly HashSet<string> PackageAssemblies = new(StringComparer.OrdinalIgnoreCase)
    {
        "Monolith.Diagnostics",
        "Monolith.Network",
        "Monolith.Vpn"
    };

    protected override bool IsController(TypeInfo typeInfo)
    {
        // Skip package assemblies - they may have types that reference Core
        var assemblyName = typeInfo.Assembly.GetName().Name;
        if (assemblyName != null && PackageAssemblies.Contains(assemblyName))
        {
            return false;
        }

        // Use base implementation for other assemblies
        try
        {
            return base.IsController(typeInfo);
        }
        catch (ReflectionTypeLoadException)
        {
            // Some types may not load if they reference Core - skip them
            return false;
        }
        catch (Exception)
        {
            // Any other exception loading the type - skip it
            return false;
        }
    }
}
