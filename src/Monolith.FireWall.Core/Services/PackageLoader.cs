using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Monolith.FireWall.Common.Interfaces;
using Monolith.FireWall.Common.Models;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Services;

public class PackageLoader
{
    private readonly ILogger _logger;
    private readonly Dictionary<string, AssemblyLoadContext> _contexts = new();

    public PackageLoader(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Load package from directory (legacy method - use LoadPackageAsync(PackageDiscoveryInfo) instead)
    /// </summary>
    public async Task<PackageInfo> LoadPackageAsync(string packageDir)
    {
        // Create a scanner to discover the package
        var scanner = new PackageScanner(_logger);
        var discovered = await scanner.ScanPackagesAsync(Path.GetDirectoryName(packageDir) ?? "");
        var discoveryInfo = discovered.FirstOrDefault(p => p.Directory == packageDir);
        
        if (discoveryInfo == null)
        {
            throw new FileNotFoundException($"Package not found in directory: {packageDir}");
        }

        return await LoadPackageAsync(discoveryInfo);
    }

    /// <summary>
    /// Load package from discovery info (supports RCL with Views DLL)
    /// </summary>
    public async Task<PackageInfo> LoadPackageAsync(PackageDiscoveryInfo discoveryInfo)
    {
        _logger.LogInformation($"Loading package: {discoveryInfo.Manifest.Id} v{discoveryInfo.Manifest.Version}");

        // Load main assembly
        var mainAssembly = Assembly.LoadFrom(discoveryInfo.MainDllPath);
        _logger.LogInformation($"Main assembly loaded: {mainAssembly.FullName}");

        // Load Views assembly (if exists - RCL packages)
        Assembly? viewsAssembly = null;
        if (discoveryInfo.ViewsDllPath != null && File.Exists(discoveryInfo.ViewsDllPath))
        {
            viewsAssembly = Assembly.LoadFrom(discoveryInfo.ViewsDllPath);
            _logger.LogInformation($"Views assembly loaded: {viewsAssembly.FullName} (Razor Class Library)");
        }

        // Find package definition type
        var allTypes = mainAssembly.GetTypes();
        var defType = allTypes
            .FirstOrDefault(t => typeof(IMonolithPackageDefinition).IsAssignableFrom(t) && !t.IsInterface);

        if (defType == null)
        {
            throw new InvalidOperationException($"No IMonolithPackageDefinition implementation found in {mainAssembly.FullName}");
        }

        var definition = (IMonolithPackageDefinition)Activator.CreateInstance(defType)!;
        _logger.LogInformation($"Package definition created: {definition.Name}");

        // Find package instance type
        var pkgType = mainAssembly.GetTypes()
            .FirstOrDefault(t => typeof(IMonolithPackage).IsAssignableFrom(t) && !t.IsInterface);

        if (pkgType == null)
        {
            throw new InvalidOperationException($"No IMonolithPackage implementation found in {mainAssembly.FullName}");
        }

        var package = (IMonolithPackage)Activator.CreateInstance(pkgType)!;

        // Discover Razor views if Views assembly exists
        List<PageDefinition> discoveredViews = new();
        if (viewsAssembly != null)
        {
            var viewDiscovery = new RazorViewDiscovery(_logger);
            discoveredViews = viewDiscovery.DiscoverViews(viewsAssembly, discoveryInfo.Manifest.Id, definition.Name);
            
            if (discoveredViews.Count > 0)
            {
                _logger.LogInformation($"Discovered {discoveredViews.Count} Razor view(s) for package {discoveryInfo.Manifest.Id}");
            }
        }

        return new PackageInfo(definition, package, mainAssembly, viewsAssembly, null, discoveredViews, discoveryInfo.Directory);
    }

    public void UnloadPackage(string packageId)
    {
        if (_contexts.TryGetValue(packageId, out var context))
        {
            _logger.LogInformation($"Unloading package: {packageId}");
            context.Unload();
            _contexts.Remove(packageId);
        }
    }
}
