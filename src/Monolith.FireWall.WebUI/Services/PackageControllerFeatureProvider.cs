using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Monolith.FireWall.WebUI.Services;

/// <summary>
/// Controller feature provider that filters out controllers from package assemblies.
/// Package assemblies may reference Monolith.FireWall.Core which isn't available in WebUI runtime.
/// </summary>
public class PackageControllerFeatureProvider : IControllerFeatureProvider
{
    private static readonly HashSet<string> PackageAssemblies = new(StringComparer.OrdinalIgnoreCase)
    {
        "Monolith.Diagnostics",
        "Monolith.Network",
        "Monolith.Vpn"
    };

    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        foreach (var part in parts)
        {
            if (part is AssemblyPart assemblyPart)
            {
                var assemblyName = assemblyPart.Assembly.GetName().Name;
                
                // Skip package assemblies - they may have types that reference Core
                if (assemblyName != null && PackageAssemblies.Contains(assemblyName))
                {
                    continue;
                }
                
                // For other assemblies, try to get types but handle ReflectionTypeLoadException
                try
                {
                    var types = assemblyPart.Assembly.GetTypes();
                    foreach (var type in types)
                    {
                        if (IsController(type))
                        {
                            feature.Controllers.Add(type);
                        }
                    }
                }
                catch (ReflectionTypeLoadException ex)
                {
                    // Some types may not load - that's OK, use what we can
                    var loadedTypes = ex.Types.Where(t => t != null).ToList();
                    foreach (var type in loadedTypes)
                    {
                        if (IsController(type))
                        {
                            feature.Controllers.Add(type);
                        }
                    }
                }
            }
        }
    }

    private static bool IsController(Type type)
    {
        if (type == null || !type.IsClass || type.IsAbstract || type.IsGenericType)
        {
            return false;
        }

        // Check if it's a controller (ends with "Controller" or has [Controller] attribute)
        if (type.Name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Check for [Controller] attribute
        return type.GetCustomAttribute<Microsoft.AspNetCore.Mvc.ControllerAttribute>() != null;
    }
}
