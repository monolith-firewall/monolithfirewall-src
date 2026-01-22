using System.Text.Json;
using Monolith.FireWall.Common.Models;

namespace Monolith.FireWall.WebUI.Services;

/// <summary>
/// Service for discovering installed packages, modules, permissions, and widgets dynamically.
/// </summary>
public class PackageDiscoveryService
{
    private readonly CoreApiClient _coreClient;
    private readonly ILogger<PackageDiscoveryService> _logger;
    private readonly Dictionary<string, object> _cache = new();
    private DateTime _cacheTime = DateTime.MinValue;
    private readonly TimeSpan _cacheTimeout = TimeSpan.FromMinutes(5);

    public PackageDiscoveryService(CoreApiClient coreClient, ILogger<PackageDiscoveryService> logger)
    {
        _coreClient = coreClient;
        _logger = logger;
    }

    /// <summary>
    /// Gets all installed packages.
    /// </summary>
    public async Task<List<PackageInfo>> GetInstalledPackagesAsync()
    {
        try
        {
            var request = new
            {
                action = "get-packages"
            };

            var requestJson = JsonSerializer.Serialize(request);
            var responseJson = await _coreClient.SendRequestAsync(requestJson, timeoutMs: 10000);
            var response = JsonSerializer.Deserialize<JsonElement>(responseJson);

            if (TryGetPropertyIgnoreCase(response, "success", out var success) && success.GetBoolean())
            {
                if (TryGetPropertyIgnoreCase(response, "data", out var data) && data.ValueKind == JsonValueKind.Array)
                {
                    var packages = new List<PackageInfo>();
                    foreach (var pkg in data.EnumerateArray())
                    {
                        packages.Add(new PackageInfo
                        {
                            Id = pkg.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                            Name = pkg.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
                            Version = pkg.TryGetProperty("version", out var ver) ? ver.GetString() ?? "" : "",
                            Description = pkg.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                            Modules = pkg.TryGetProperty("modules", out var modules) && modules.ValueKind == JsonValueKind.Array
                                ? modules.EnumerateArray().Select(m => new ModuleInfo
                                {
                                    Id = m.TryGetProperty("id", out var mid) ? mid.GetString() ?? "" : "",
                                    Name = m.TryGetProperty("name", out var mname) ? mname.GetString() ?? "" : "",
                                    Enabled = m.TryGetProperty("enabled", out var enabled) && enabled.GetBoolean(),
                                    RequiredPermissions = m.TryGetProperty("requiredPermissions", out var perms) && perms.ValueKind == JsonValueKind.Array
                                        ? perms.EnumerateArray().Select(p => p.GetString() ?? "").ToList()
                                        : new List<string>()
                                }).ToList()
                                : new List<ModuleInfo>()
                        });
                    }
                    return packages;
                }
            }

            _logger.LogWarning("Failed to parse packages response");
            return new List<PackageInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get installed packages");
            return new List<PackageInfo>();
        }
    }

    /// <summary>
    /// Gets all modules from all installed packages.
    /// </summary>
    public async Task<List<ModuleInfo>> GetAllModulesAsync()
    {
        try
        {
            var request = new
            {
                action = "get-modules"
            };

            var requestJson = JsonSerializer.Serialize(request);
            var responseJson = await _coreClient.SendRequestAsync(requestJson, timeoutMs: 10000);
            var response = JsonSerializer.Deserialize<JsonElement>(responseJson);

            if (TryGetPropertyIgnoreCase(response, "success", out var success) && success.GetBoolean())
            {
                if (TryGetPropertyIgnoreCase(response, "data", out var data) && data.ValueKind == JsonValueKind.Array)
                {
                    var modules = new List<ModuleInfo>();
                    foreach (var mod in data.EnumerateArray())
                    {
                        modules.Add(new ModuleInfo
                        {
                            Id = mod.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                            Name = mod.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
                            PackageId = mod.TryGetProperty("packageId", out var pkgId) ? pkgId.GetString() ?? "" : "",
                            PackageName = mod.TryGetProperty("packageName", out var pkgName) ? pkgName.GetString() ?? "" : "",
                            Enabled = mod.TryGetProperty("enabled", out var enabled) && enabled.GetBoolean(),
                            RequiredPermissions = mod.TryGetProperty("requiredPermissions", out var perms) && perms.ValueKind == JsonValueKind.Array
                                ? perms.EnumerateArray().Select(p => p.GetString() ?? "").ToList()
                                : new List<string>()
                        });
                    }
                    return modules;
                }
            }

            _logger.LogWarning("Failed to parse modules response");
            return new List<ModuleInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get modules");
            return new List<ModuleInfo>();
        }
    }

    /// <summary>
    /// Gets all widgets from all installed packages.
    /// </summary>
    public async Task<List<WidgetInfo>> GetAllWidgetsAsync()
    {
        try
        {
            var request = new
            {
                action = "get-widgets"
            };

            var requestJson = JsonSerializer.Serialize(request);
            var responseJson = await _coreClient.SendRequestAsync(requestJson, timeoutMs: 10000);
            var response = JsonSerializer.Deserialize<JsonElement>(responseJson);

            if (TryGetPropertyIgnoreCase(response, "success", out var success) && success.GetBoolean())
            {
                if (TryGetPropertyIgnoreCase(response, "data", out var data) && data.ValueKind == JsonValueKind.Array)
                {
                    var widgets = new List<WidgetInfo>();
                    foreach (var widget in data.EnumerateArray())
                    {
                        widgets.Add(new WidgetInfo
                        {
                            Id = widget.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                            Name = widget.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
                            PackageId = widget.TryGetProperty("packageId", out var pkgId) ? pkgId.GetString() ?? "" : "",
                            ModuleId = widget.TryGetProperty("moduleId", out var modId) ? modId.GetString() ?? "" : ""
                        });
                    }
                    return widgets;
                }
            }

            _logger.LogWarning("Failed to parse widgets response");
            return new List<WidgetInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get widgets");
            return new List<WidgetInfo>();
        }
    }

    /// <summary>
    /// Finds the package that provides a specific module.
    /// </summary>
    public async Task<string?> FindPackageByModuleAsync(string moduleId)
    {
        var modules = await GetAllModulesAsync();
        var module = modules.FirstOrDefault(m => 
            string.Equals(m.Id, moduleId, StringComparison.OrdinalIgnoreCase) && m.Enabled);
        return module?.PackageId;
    }

    /// <summary>
    /// Checks if a package is installed.
    /// </summary>
    public async Task<bool> IsPackageInstalledAsync(string packageId)
    {
        var packages = await GetInstalledPackagesAsync();
        return packages.Any(p => string.Equals(p.Id, packageId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets all permissions from all installed packages.
    /// </summary>
    public async Task<List<PermissionInfo>> GetAllPermissionsAsync()
    {
        var modules = await GetAllModulesAsync();
        var permissions = new List<PermissionInfo>();

        foreach (var module in modules)
        {
            foreach (var permId in module.RequiredPermissions)
            {
                // Parse permission ID to extract category/subcategory
                // Format: category.subcategory.action (e.g., "network.dhcp.read")
                var parts = permId.Split('.');
                var category = parts.Length > 0 ? parts[0] : "Other";
                var subcategory = parts.Length > 1 ? parts[1] : "";
                var action = parts.Length > 2 ? parts[2] : "";

                permissions.Add(new PermissionInfo
                {
                    Id = permId,
                    Name = FormatPermissionName(permId),
                    Category = FormatCategoryName(category),
                    Subcategory = FormatCategoryName(subcategory),
                    PackageId = module.PackageId,
                    PackageName = module.PackageName,
                    ModuleId = module.Id,
                    ModuleName = module.Name
                });
            }
        }

        return permissions.DistinctBy(p => p.Id).ToList();
    }

    private static string FormatPermissionName(string permissionId)
    {
        // Convert "network.dhcp.read" -> "View DHCP Configuration"
        var parts = permissionId.Split('.');
        if (parts.Length < 3) return permissionId;

        var action = parts[^1].ToLower();
        var subcategory = parts.Length > 1 ? parts[^2] : "";
        
        var actionName = action switch
        {
            "read" => "View",
            "write" => "Manage",
            "execute" => "Execute",
            _ => action
        };

        return $"{actionName} {FormatCategoryName(subcategory)}";
    }

    private static string FormatCategoryName(string category)
    {
        if (string.IsNullOrEmpty(category)) return "";
        
        // Convert "dhcp" -> "DHCP", "dns" -> "DNS", "network" -> "Network"
        return string.Join(" ", category
            .Split(new[] { '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(word => char.ToUpper(word[0]) + word.Substring(1).ToLower()));
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        // Try exact match first
        if (element.TryGetProperty(propertyName, out value))
            return true;

        // Try PascalCase
        var pascalCase = char.ToUpper(propertyName[0]) + propertyName.Substring(1);
        if (element.TryGetProperty(pascalCase, out value))
            return true;

        // Try camelCase
        var camelCase = char.ToLower(propertyName[0]) + propertyName.Substring(1);
        if (element.TryGetProperty(camelCase, out value))
            return true;

        value = default;
        return false;
    }
}

public class PackageInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string? Description { get; set; }
    public List<ModuleInfo> Modules { get; set; } = new();
}

public class ModuleInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string PackageId { get; set; } = "";
    public string PackageName { get; set; } = "";
    public bool Enabled { get; set; }
    public List<string> RequiredPermissions { get; set; } = new();
}

public class WidgetInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string PackageId { get; set; } = "";
    public string ModuleId { get; set; } = "";
}

public class PermissionInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Subcategory { get; set; } = "";
    public string PackageId { get; set; } = "";
    public string PackageName { get; set; } = "";
    public string ModuleId { get; set; } = "";
    public string ModuleName { get; set; } = "";
}
