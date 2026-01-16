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
    /// </summary>
    public async Task RegisterViewsAssembliesAsync(ApplicationPartManager partManager)
    {
        try
        {
            var request = new
            {
                action = "get-packages"
            };

            var requestJson = JsonSerializer.Serialize(request);
            var responseJson = await _coreClient.SendRequestAsync(requestJson);
            var response = JsonSerializer.Deserialize<JsonElement>(responseJson);

            if (response.TryGetProperty("success", out var success) && success.GetBoolean())
            {
                if (response.TryGetProperty("data", out var data))
                {
                    var packages = JsonSerializer.Deserialize<List<JsonElement>>(data.GetRawText()) ?? new List<JsonElement>();

                    foreach (var package in packages)
                    {
                        var packageId = package.TryGetProperty("id", out var idEl) ? idEl.GetString() : "unknown";
                        var packageName = package.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : packageId;
                        
                        if (package.TryGetProperty("hasRazorViews", out var hasViews) && hasViews.GetBoolean())
                        {
                            _logger.LogWarning($"[PackageViewsRegistry] Package '{packageId}' reports hasRazorViews=true");
                            
                            if (package.TryGetProperty("viewsAssemblyPath", out var assemblyPathEl))
                            {
                                var assemblyPath = assemblyPathEl.GetString();
                                if (!string.IsNullOrEmpty(assemblyPath) && File.Exists(assemblyPath))
                                {
                                    try
                                    {
                                        // Load the Views assembly
                                        var assembly = Assembly.LoadFrom(assemblyPath);
                                        
                                        // Add as an application part
                                        var assemblyPart = new AssemblyPart(assembly);
                                        partManager.ApplicationParts.Add(assemblyPart);
                                        
                                        _registeredAssemblies.Add(assemblyPath);
                                        _logger.LogInformation($"[PackageViewsRegistry] ✓ Registered Views assembly: {assembly.FullName} from {assemblyPath}");
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError($"[PackageViewsRegistry] ✗ Failed to register Views assembly {assemblyPath}: {ex.Message}");
                                    }
                                }
                                else
                                {
                                    _logger.LogWarning($"[PackageViewsRegistry] ✗ Package '{packageId}' has hasRazorViews=true but viewsAssemblyPath is missing or file doesn't exist: '{assemblyPath}'");
                                }
                            }
                            else
                            {
                                _logger.LogWarning($"[PackageViewsRegistry] ✗ Package '{packageId}' has hasRazorViews=true but no viewsAssemblyPath property");
                            }
                        }
                        else
                        {
                            _logger.LogDebug($"[PackageViewsRegistry] Package '{packageId}' has no Razor Views (hasRazorViews=false or missing)");
                        }
                    }
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
