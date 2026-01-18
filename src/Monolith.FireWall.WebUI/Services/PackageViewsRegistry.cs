using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Monolith.FireWall.WebUI.Services;

namespace Monolith.FireWall.WebUI.Services;

/// <summary>
/// Registers package Views assemblies with ASP.NET Core Razor engine
/// </summary>
public class PackageViewsRegistry
{
    private readonly CoreApiClient _coreClient;
    private readonly ILogger<PackageViewsRegistry> _logger;
    private readonly List<string> _registeredAssemblies = new();

    public PackageViewsRegistry(CoreApiClient coreClient, ILogger<PackageViewsRegistry> logger)
    {
        _coreClient = coreClient;
        _logger = logger;
    }

    /// <summary>
    /// Registers all package Views assemblies with the application
    /// Views are embedded in main DLL when using Microsoft.NET.Sdk.Razor
    /// </summary>
    public async Task RegisterViewsAssembliesAsync(ApplicationPartManager partManager)
    {
        try
        {
            var request = JsonSerializer.Serialize(new { action = "get-packages" });
            var responseJson = await _coreClient.SendRequestAsync(request);
            var response = JsonSerializer.Deserialize<JsonElement>(responseJson);

            _logger.LogDebug($"PackageViewsRegistry: Got response from Core API. Response keys: {string.Join(", ", response.EnumerateObject().Select(p => p.Name))}");

            // Check for both "success" and "Success" (case variations)
            if (!response.TryGetProperty("success", out var success))
                response.TryGetProperty("Success", out success);
            
            if (!success.GetBoolean())
            {
                _logger.LogWarning("PackageViewsRegistry: Core API returned success=false");
                return;
            }

            // Check for both "data" and "Data" (case variations)
            if (!response.TryGetProperty("data", out var data))
            {
                if (!response.TryGetProperty("Data", out data))
                {
                    _logger.LogWarning("PackageViewsRegistry: No data property in response");
                    return;
                }
            }

            var packages = JsonSerializer.Deserialize<List<JsonElement>>(data.GetRawText()) ?? new List<JsonElement>();

            foreach (var package in packages)
            {
                // Get views assembly path (which is main DLL path - views are embedded)
                if (!package.TryGetProperty("viewsAssemblyPath", out var pathEl))
                    continue;

                var assemblyPath = pathEl.GetString();
                if (string.IsNullOrEmpty(assemblyPath) || !File.Exists(assemblyPath))
                    continue;

                // Skip if already registered
                if (IsRegistered(assemblyPath))
                    continue;

                try
                {
                    var assembly = Assembly.LoadFrom(assemblyPath);
                    
                    // Register only compiled Razor parts so MVC won't scan for controllers
                    // Package assemblies should only expose Razor pages, not controllers
                    var assemblyPart = new CompiledRazorAssemblyPart(assembly);
                    partManager.ApplicationParts.Add(assemblyPart);
                    _registeredAssemblies.Add(assemblyPath);
                    _logger.LogInformation($"Registered Views assembly: {assembly.FullName} from {assemblyPath}");
                    
                    // Log embedded Razor Pages for debugging
                    var embeddedResources = assembly.GetManifestResourceNames()
                        .Where(r => r.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    if (embeddedResources.Count > 0)
                    {
                        _logger.LogDebug($"  Found {embeddedResources.Count} embedded Razor resource(s) in {assembly.FullName}");
                    }
                }
                catch (ReflectionTypeLoadException ex)
                {
                    // Some types may not load if they reference Core - that's OK for Razor pages
                    _logger.LogWarning($"Some types in {assemblyPath} could not be loaded (expected for package assemblies): {ex.Message}");
                    // Try to continue with what we can load
                    try
                    {
                        var assembly = Assembly.LoadFrom(assemblyPath);
                        var assemblyPart = new CompiledRazorAssemblyPart(assembly);
                        partManager.ApplicationParts.Add(assemblyPart);
                        _registeredAssemblies.Add(assemblyPath);
                        _logger.LogInformation($"Registered Views assembly (with type load exceptions): {assembly.FullName} from {assemblyPath}");
                    }
                    catch (Exception ex2)
                    {
                        _logger.LogError($"Failed to register Views assembly {assemblyPath}: {ex2.Message}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to register Views assembly {assemblyPath}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to register package Views assemblies: {ex.Message}");
        }
    }

    public bool IsRegistered(string assemblyPath)
    {
        return _registeredAssemblies.Contains(assemblyPath);
    }
}
