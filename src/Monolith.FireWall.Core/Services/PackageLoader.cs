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
    /// Load package from discovery info (Razor views are embedded in main DLL)
    /// </summary>
    public async Task<PackageInfo> LoadPackageAsync(PackageDiscoveryInfo discoveryInfo)
    {
        _logger.LogInformation($"Loading package: {discoveryInfo.Manifest.Id} v{discoveryInfo.Manifest.Version}");

        // Load main assembly (Razor views are embedded in main DLL when using Microsoft.NET.Sdk.Razor)
        var mainAssembly = Assembly.LoadFrom(discoveryInfo.MainDllPath);
        _logger.LogInformation($"Main assembly loaded: {mainAssembly.FullName}");

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

        // Discover Razor views from main assembly (views are embedded when using Microsoft.NET.Sdk.Razor)
        // Convert package ID to assembly name format for view discovery
        var assemblyName = ConvertPackageIdToAssemblyName(discoveryInfo.Manifest.Id);
        var viewDiscovery = new RazorViewDiscovery(_logger);
        var discoveredViews = viewDiscovery.DiscoverViews(mainAssembly, discoveryInfo.Manifest.Id, assemblyName);
        
        if (discoveredViews.Count > 0)
        {
            _logger.LogInformation($"Discovered {discoveredViews.Count} Razor view(s) for package {discoveryInfo.Manifest.Id}");
        }

        // Use mainAssembly for both backend and views (views are embedded in main DLL)
        return new PackageInfo(definition, package, mainAssembly, mainAssembly, null, discoveredViews, discoveryInfo.Directory);
    }

    /// <summary>
    /// Converts package ID to assembly name format
    /// monolith-network -> Monolith.Network
    /// </summary>
    private static string ConvertPackageIdToAssemblyName(string packageId)
    {
        if (string.IsNullOrEmpty(packageId))
            return packageId;

        var parts = packageId.Split('-', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(".", parts.Select(part => 
            part.Length > 0 
                ? char.ToUpper(part[0]) + part.Substring(1).ToLower() 
                : part));
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
