using Monolith.FireWall.Common.Interfaces;
using Monolith.FireWall.Common.Models;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Services;

public class ModuleRegistry
{
    private readonly ILogger _logger;
    private readonly Dictionary<string, ModuleInfo> _modules = new();
    private readonly Dictionary<(string, string, string), RouteDefinition> _routes = new();
    private readonly Dictionary<string, PackageInfo> _packages = new();
    private readonly PackageStateStore? _stateStore;

    public ModuleRegistry(ILogger logger, PackageStateStore? stateStore = null)
    {
        _logger = logger;
        _stateStore = stateStore;
    }

    public void RegisterPackage(PackageInfo package)
    {
        _logger.LogInformation($"Registering package: {package.Definition.Id}");
        _packages[package.Definition.Id] = package;

        try
        {
            _logger.LogInformation($"  Calling GetModules()...");
            var modulesEnumerable = package.Definition.GetModules();
            _logger.LogInformation($"  GetModules() returned enumerable of type: {modulesEnumerable.GetType().FullName}");
            
            // Try to get count if it's a collection
            if (modulesEnumerable is System.Collections.ICollection collection)
            {
                _logger.LogInformation($"  Enumerable is ICollection with Count: {collection.Count}");
            }
            
            var modules = new List<IMonolithModule>();
            int count = 0;
            var enumerator = modulesEnumerable.GetEnumerator();
            _logger.LogInformation($"  Got enumerator, starting iteration...");
            try
            {
                while (enumerator.MoveNext())
                {
                    try
                    {
                        count++;
                        _logger.LogInformation($"  MoveNext() returned true for iteration #{count}");
                        var module = enumerator.Current;
                        _logger.LogInformation($"  Got module from Current: {module?.GetType().FullName ?? "null"}");
                        if (module != null)
                        {
                            _logger.LogInformation($"  Materializing module #{count}: {module.Id}");
                            modules.Add(module);
                            _logger.LogInformation($"  Successfully materialized module: {module.Id}");
                        }
                        else
                        {
                            _logger.LogWarning($"  Module #{count} is null!");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"  Exception while materializing module #{count}: {ex.GetType().Name}: {ex.Message}");
                        _logger.LogError(ex, $"  Stack trace: {ex.StackTrace}");
                    }
                }
                _logger.LogInformation($"  MoveNext() returned false, iteration complete. Processed {count} iteration(s), added {modules.Count} module(s)");
            }
            finally
            {
                enumerator?.Dispose();
            }
            _logger.LogInformation($"  Found {modules.Count} module(s) in package after materialization");
            
            foreach (var module in modules)
            {
                try
                {
                    _logger.LogInformation($"  Registering module: {module.Id}");
                    _modules[module.Id] = new ModuleInfo(module, package);

                    // Register routes
                    foreach (var route in module.GetRoutes())
                    {
                        var key = (package.Definition.Id, module.Id, route.Action);
                        _routes[key] = route;
                        _logger.LogDebug($"    Route registered: {route.Action}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"  Failed to register module: {module.Id}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to get modules from package: {package.Definition.Id}");
        }

        // Register discovered Razor views
        if (package.HasRazorViews && package.DiscoveredViews.Count > 0)
        {
            _logger.LogInformation($"  Registering {package.DiscoveredViews.Count} discovered Razor view(s)");
            foreach (var view in package.DiscoveredViews)
            {
                _logger.LogDebug($"    View registered: {view.Route} -> {view.RazorPath}");
            }
        }
    }

    public void UnregisterPackage(string packageId)
    {
        _logger.LogInformation($"Unregistering package: {packageId}");

        // Remove all modules for this package
        var moduleIds = _modules
            .Where(kv => kv.Value.Package.Definition.Id == packageId)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var moduleId in moduleIds)
        {
            _modules.Remove(moduleId);
        }

        // Remove all routes for this package
        var routeKeys = _routes.Keys
            .Where(k => k.Item1 == packageId)
            .ToList();

        foreach (var key in routeKeys)
        {
            _routes.Remove(key);
        }

        _packages.Remove(packageId);
    }

    public RouteDefinition? GetRoute(string packageId, string moduleId, string action)
    {
        if (!IsModuleEnabled(packageId, moduleId))
        {
            return null;
        }

        _routes.TryGetValue((packageId, moduleId, action), out var route);
        return route;
    }

    public ModuleInfo? GetModule(string moduleId)
    {
        _modules.TryGetValue(moduleId, out var module);
        return module;
    }

    public ModuleInfo? GetModule(string packageId, string moduleId)
    {
        var module = _modules.Values.FirstOrDefault(m =>
            string.Equals(m.Package.Definition.Id, packageId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(m.Module.Id, moduleId, StringComparison.OrdinalIgnoreCase));
        if (module == null)
        {
            return null;
        }

        return IsModuleEnabled(packageId, moduleId) ? module : null;
    }

    public IEnumerable<ModuleInfo> GetAllModules(bool includeDisabled = false)
    {
        if (includeDisabled)
        {
            return _modules.Values;
        }

        return _modules.Values.Where(m => IsModuleEnabled(m.Package.Definition.Id, m.Module.Id));
    }

    public IEnumerable<PackageInfo> GetAllPackages()
    {
        return _packages.Values;
    }

    /// <summary>
    /// Gets all discovered Razor pages from all packages
    /// </summary>
    public IEnumerable<PageDefinition> GetAllPages()
    {
        return _packages.Values
            .Where(p => p.HasRazorViews)
            .SelectMany(p => p.DiscoveredViews);
    }

    /// <summary>
    /// Gets a page by route
    /// </summary>
    public PageDefinition? GetPage(string route)
    {
        return GetAllPages().FirstOrDefault(p => p.Route.Equals(route, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets all pages for a specific package
    /// </summary>
    public IEnumerable<PageDefinition> GetPagesForPackage(string packageId)
    {
        if (_packages.TryGetValue(packageId, out var package) && package.HasRazorViews)
        {
            return package.DiscoveredViews;
        }
        return Enumerable.Empty<PageDefinition>();
    }

    private bool IsModuleEnabled(string packageId, string moduleId)
    {
        if (_stateStore == null)
        {
            return true;
        }

        try
        {
            return _stateStore.IsModuleEnabledAsync(packageId, moduleId).GetAwaiter().GetResult();
        }
        catch
        {
            return true;
        }
    }
}
