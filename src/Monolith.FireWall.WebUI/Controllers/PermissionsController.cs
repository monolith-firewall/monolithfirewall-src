using Microsoft.AspNetCore.Mvc;
using Monolith.FireWall.WebUI.Services;
using System.Text.Json;

namespace Monolith.FireWall.WebUI.Controllers;

[ApiController]
[Route("api/permissions")]
public class PermissionsController : ControllerBase
{
    private readonly CoreApiClient _coreClient;
    private readonly ILogger<PermissionsController> _logger;

    public PermissionsController(
        CoreApiClient coreClient,
        ILogger<PermissionsController> logger)
    {
        _coreClient = coreClient;
        _logger = logger;
    }

    /// <summary>
    /// Get all permissions from all installed packages.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllPermissions()
    {
        try
        {
            // Query Core API for all modules to get permissions
            var request = JsonSerializer.Serialize(new { action = "get-modules" });
            _logger.LogDebug("Sending request to Core API: get-modules");
            
            string responseJson;
            try
            {
                responseJson = await _coreClient.SendRequestAsync(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to communicate with Core API");
                return StatusCode(500, new { success = false, error = $"Core API communication failed: {ex.Message}" });
            }
            
            if (string.IsNullOrWhiteSpace(responseJson))
            {
                _logger.LogError("Core API returned empty response");
                return StatusCode(500, new { success = false, error = "Core API returned empty response" });
            }
            
            Dictionary<string, object>? response;
            try
            {
                response = JsonSerializer.Deserialize<Dictionary<string, object>>(responseJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize Core API response: {Response}", responseJson);
                return StatusCode(500, new { success = false, error = $"Failed to parse Core API response: {ex.Message}" });
            }
            
            if (response == null || !(GetDictBoolHelper(response, "success") ?? GetDictBoolHelper(response, "Success") ?? false))
            {
                _logger.LogWarning("Core API returned unsuccessful response: {Response}", responseJson);
                return StatusCode(500, new { success = false, error = "Failed to get modules from Core" });
            }

            if (!response.TryGetValue("data", out var dataObj) && !response.TryGetValue("Data", out dataObj))
            {
                _logger.LogWarning("Core API response missing data field: {Response}", responseJson);
                return StatusCode(500, new { success = false, error = "No data in response" });
            }

            // Extract permissions from modules
            List<Dictionary<string, object>>? modules = null;
            try
            {
                var modulesJson = JsonSerializer.Serialize(dataObj);
                modules = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(modulesJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize modules data");
                return StatusCode(500, new { success = false, error = $"Failed to parse modules data: {ex.Message}" });
            }
            
            var allPermissions = new List<Dictionary<string, object>>();
            var permissionSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (modules != null && modules.Count > 0)
            {
                _logger.LogDebug("Processing {Count} modules for permissions", modules.Count);
                
                foreach (var module in modules)
                {
                    var packageId = GetDictStringHelper(module, "packageId") ?? GetDictStringHelper(module, "PackageId") ?? "core";
                    var packageName = GetDictStringHelper(module, "packageName") ?? GetDictStringHelper(module, "PackageName") ?? "Core";
                    var moduleId = GetDictStringHelper(module, "id") ?? GetDictStringHelper(module, "Id") ?? "";
                    var moduleName = GetDictStringHelper(module, "name") ?? GetDictStringHelper(module, "Name") ?? "";
                    
                    // Get requiredPermissions array
                    object? permsObj = null;
                    if (module.TryGetValue("requiredPermissions", out var rp))
                        permsObj = rp;
                    else if (module.TryGetValue("RequiredPermissions", out var rp2))
                        permsObj = rp2;
                    
                    if (permsObj != null)
                    {
                        try
                        {
                            List<string> permIds = new List<string>();
                            
                            // Handle JsonElement
                            if (permsObj is JsonElement jsonElement)
                            {
                                if (jsonElement.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var item in jsonElement.EnumerateArray())
                                    {
                                        if (item.ValueKind == JsonValueKind.String)
                                        {
                                            var permId = item.GetString();
                                            if (!string.IsNullOrWhiteSpace(permId))
                                                permIds.Add(permId);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // Try to deserialize as list
                                var permsJson = JsonSerializer.Serialize(permsObj);
                                var deserialized = JsonSerializer.Deserialize<List<string>>(permsJson);
                                if (deserialized != null)
                                    permIds = deserialized;
                            }
                            
                            foreach (var permId in permIds)
                            {
                                if (string.IsNullOrWhiteSpace(permId) || permissionSet.Contains(permId))
                                    continue;
                                    
                                permissionSet.Add(permId);
                                
                                // Parse permission ID to extract category
                                var parts = permId.Split('.');
                                var category = parts.Length > 0 ? parts[0] : "Other";
                                var subcategory = parts.Length > 1 ? parts[1] : "";
                                var action = parts.Length > 2 ? parts[2] : "";
                                
                                // Generate display name
                                var displayName = action == "*" ? "All Actions" : 
                                                 action != "" ? ToTitleHelper(action) :
                                                 subcategory == "*" ? "All " + ToTitleHelper(category) :
                                                 subcategory != "" ? ToTitleHelper(subcategory) :
                                                 category == "*" ? "All Permissions" :
                                                 ToTitleHelper(category);

                                allPermissions.Add(new Dictionary<string, object>
                                {
                                    ["id"] = permId,
                                    ["name"] = displayName,
                                    ["category"] = ToTitleHelper(category),
                                    ["subcategory"] = subcategory != "" && subcategory != "*" ? ToTitleHelper(subcategory) : "",
                                    ["packageId"] = packageId,
                                    ["moduleId"] = moduleId,
                                    ["description"] = $"Permission from {packageName} / {moduleName}"
                                });
                            }
                            
                            if (permIds.Count > 0)
                            {
                                _logger.LogDebug("Module {ModuleId} ({PackageId}) contributed {Count} permissions", moduleId, packageId, permIds.Count);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error processing permissions for module {ModuleId}", moduleId);
                        }
                    }
                }
            }

            // Add core system permissions
            var corePerms = new[]
            {
                new Dictionary<string, object> { ["id"] = "system.users.read", ["name"] = "View Users", ["category"] = "System", ["subcategory"] = "Users", ["packageId"] = "core", ["moduleId"] = "system", ["description"] = "View user list and details" },
                new Dictionary<string, object> { ["id"] = "system.users.write", ["name"] = "Manage Users", ["category"] = "System", ["subcategory"] = "Users", ["packageId"] = "core", ["moduleId"] = "system", ["description"] = "Create and edit users" },
                new Dictionary<string, object> { ["id"] = "system.users.delete", ["name"] = "Delete Users", ["category"] = "System", ["subcategory"] = "Users", ["packageId"] = "core", ["moduleId"] = "system", ["description"] = "Delete users" },
                new Dictionary<string, object> { ["id"] = "system.groups.read", ["name"] = "View Groups", ["category"] = "System", ["subcategory"] = "Groups", ["packageId"] = "core", ["moduleId"] = "system", ["description"] = "View group list and details" },
                new Dictionary<string, object> { ["id"] = "system.groups.write", ["name"] = "Manage Groups", ["category"] = "System", ["subcategory"] = "Groups", ["packageId"] = "core", ["moduleId"] = "system", ["description"] = "Create and edit groups" },
                new Dictionary<string, object> { ["id"] = "system.groups.delete", ["name"] = "Delete Groups", ["category"] = "System", ["subcategory"] = "Groups", ["packageId"] = "core", ["moduleId"] = "system", ["description"] = "Delete groups" },
                new Dictionary<string, object> { ["id"] = "system.permissions.read", ["name"] = "View Permissions", ["category"] = "System", ["subcategory"] = "Permissions", ["packageId"] = "core", ["moduleId"] = "system", ["description"] = "View available permissions" },
                new Dictionary<string, object> { ["id"] = "system.settings.read", ["name"] = "View Settings", ["category"] = "System", ["subcategory"] = "Settings", ["packageId"] = "core", ["moduleId"] = "system", ["description"] = "View system settings" },
                new Dictionary<string, object> { ["id"] = "system.settings.write", ["name"] = "Manage Settings", ["category"] = "System", ["subcategory"] = "Settings", ["packageId"] = "core", ["moduleId"] = "system", ["description"] = "Modify system settings" },
                new Dictionary<string, object> { ["id"] = "*", ["name"] = "All Permissions", ["category"] = "System", ["subcategory"] = "All", ["packageId"] = "core", ["moduleId"] = "system", ["description"] = "Full system access" }
            };

            foreach (var perm in corePerms)
            {
                var permId = perm["id"].ToString() ?? "";
                if (!permissionSet.Contains(permId))
                {
                    permissionSet.Add(permId);
                    allPermissions.Add(perm);
                }
            }

            _logger.LogInformation("Returning {Count} total permissions", allPermissions.Count);
            return Ok(new { success = true, data = allPermissions });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting permissions");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    // Helper methods (same as in Program.cs)
    private static bool? GetDictBoolHelper(Dictionary<string, object> dict, string key)
    {
        if (dict.TryGetValue(key, out var value))
        {
            if (value is bool b) return b;
            if (value is JsonElement je && je.ValueKind == JsonValueKind.True) return true;
            if (value is JsonElement je2 && je2.ValueKind == JsonValueKind.False) return false;
        }
        return null;
    }

    private static string? GetDictStringHelper(Dictionary<string, object> dict, string key)
    {
        if (dict.TryGetValue(key, out var value))
        {
            return value?.ToString();
        }
        return null;
    }

    private static string ToTitleHelper(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return slug ?? "";
        var parts = (slug ?? "").Split(new[] { '-', '.' }, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts.Select(s =>
        {
            if (string.IsNullOrEmpty(s)) return s;
            if (s.Length == 1) return char.ToUpperInvariant(s[0]).ToString();
            return char.ToUpperInvariant(s[0]) + s.Substring(1).ToLowerInvariant();
        }));
    }

    /// <summary>
    /// Get permissions grouped by category.
    /// </summary>
    [HttpGet("categories")]
    public async Task<IActionResult> GetPermissionsByCategory()
    {
        try
        {
            var permissionsResult = await GetAllPermissionsInternalAsync();
            if (!permissionsResult.success)
            {
                return StatusCode(500, new { error = permissionsResult.error });
            }

            var permissions = permissionsResult.permissions;
            var categories = permissions
                .GroupBy(p => GetDictStringHelper(p, "category") ?? "Other")
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(p => GetDictStringHelper(p, "id") ?? "").Where(id => !string.IsNullOrEmpty(id)).ToList()
                );

            // Also include wildcard permissions (e.g., "network.*")
            var wildcards = permissions
                .Select(p => GetDictStringHelper(p, "id") ?? "")
                .Where(id => !string.IsNullOrEmpty(id))
                .Select(id => id.Split('.').Take(2))
                .Where(parts => parts.Count() == 2)
                .Select(parts => string.Join(".", parts) + ".*")
                .Distinct()
                .ToList();

            return Ok(new { categories, wildcards });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get permissions by category");
            return StatusCode(500, new { error = "Failed to retrieve permissions" });
        }
    }

    /// <summary>
    /// Get permissions grouped by package.
    /// </summary>
    [HttpGet("by-package")]
    public async Task<IActionResult> GetPermissionsByPackage()
    {
        try
        {
            var permissionsResult = await GetAllPermissionsInternalAsync();
            if (!permissionsResult.success)
            {
                return StatusCode(500, new { error = permissionsResult.error });
            }

            var permissions = permissionsResult.permissions;
            var byPackage = permissions
                .GroupBy(p => new 
                { 
                    PackageId = GetDictStringHelper(p, "packageId") ?? "core",
                    PackageName = GetDictStringHelper(p, "packageId") ?? "Core" // Use packageId as name fallback
                })
                .ToDictionary(
                    g => g.Key.PackageId,
                    g => new
                    {
                        packageName = g.Key.PackageName,
                        permissions = g.Select(p => new
                        {
                            id = GetDictStringHelper(p, "id"),
                            name = GetDictStringHelper(p, "name"),
                            category = GetDictStringHelper(p, "category"),
                            subcategory = GetDictStringHelper(p, "subcategory"),
                            moduleId = GetDictStringHelper(p, "moduleId")
                        }).ToList()
                    }
                );

            return Ok(new { packages = byPackage });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get permissions by package");
            return StatusCode(500, new { error = "Failed to retrieve permissions" });
        }
    }

    /// <summary>
    /// Get permissions for a specific package.
    /// </summary>
    [HttpGet("{packageId}")]
    public async Task<IActionResult> GetPackagePermissions(string packageId)
    {
        try
        {
            var permissionsResult = await GetAllPermissionsInternalAsync();
            if (!permissionsResult.success)
            {
                return StatusCode(500, new { error = permissionsResult.error });
            }

            var permissions = permissionsResult.permissions;
            var packagePermissions = permissions
                .Where(p => string.Equals(GetDictStringHelper(p, "packageId") ?? "core", packageId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (packagePermissions.Count == 0)
            {
                return NotFound(new { error = $"No permissions found for package: {packageId}" });
            }

            return Ok(new { permissions = packagePermissions });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get permissions for package {PackageId}", packageId);
            return StatusCode(500, new { error = "Failed to retrieve permissions" });
        }
    }

    /// <summary>
    /// Internal helper method to get all permissions from Core API
    /// </summary>
    private async Task<(bool success, List<Dictionary<string, object>> permissions, string? error)> GetAllPermissionsInternalAsync()
    {
        try
        {
            // Query Core API for all modules to get permissions
            var request = JsonSerializer.Serialize(new { action = "get-modules" });
            string responseJson;
            try
            {
                responseJson = await _coreClient.SendRequestAsync(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to communicate with Core API");
                return (false, new List<Dictionary<string, object>>(), $"Core API communication failed: {ex.Message}");
            }
            
            if (string.IsNullOrWhiteSpace(responseJson))
            {
                return (false, new List<Dictionary<string, object>>(), "Core API returned empty response");
            }
            
            var response = JsonSerializer.Deserialize<Dictionary<string, object>>(responseJson);
            if (response == null)
            {
                return (false, new List<Dictionary<string, object>>(), "Failed to deserialize Core API response");
            }
            
            try
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize Core API response");
                return (false, new List<Dictionary<string, object>>(), $"Failed to parse Core API response: {ex.Message}");
            }
            
            if (response == null || !(GetDictBoolHelper(response, "success") ?? GetDictBoolHelper(response, "Success") ?? false))
            {
                return (false, new List<Dictionary<string, object>>(), "Failed to get modules from Core");
            }

            if (!response.TryGetValue("data", out var dataObj) && !response.TryGetValue("Data", out dataObj))
            {
                return (false, new List<Dictionary<string, object>>(), "No data in response");
            }

            // Extract permissions from modules (same logic as GetAllPermissions)
            List<Dictionary<string, object>>? modules = null;
            try
            {
                var modulesJson = JsonSerializer.Serialize(dataObj);
                modules = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(modulesJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize modules data");
                return (false, new List<Dictionary<string, object>>(), $"Failed to parse modules data: {ex.Message}");
            }
            
            var allPermissions = new List<Dictionary<string, object>>();
            var permissionSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (modules != null && modules.Count > 0)
            {
                foreach (var module in modules)
                {
                    var packageId = GetDictStringHelper(module, "packageId") ?? GetDictStringHelper(module, "PackageId") ?? "core";
                    var packageName = GetDictStringHelper(module, "packageName") ?? GetDictStringHelper(module, "PackageName") ?? "Core";
                    var moduleId = GetDictStringHelper(module, "id") ?? GetDictStringHelper(module, "Id") ?? "";
                    var moduleName = GetDictStringHelper(module, "name") ?? GetDictStringHelper(module, "Name") ?? "";
                    
                    object? permsObj = null;
                    if (module.TryGetValue("requiredPermissions", out var rp))
                        permsObj = rp;
                    else if (module.TryGetValue("RequiredPermissions", out var rp2))
                        permsObj = rp2;
                    
                    if (permsObj != null)
                    {
                        try
                        {
                            List<string> permIds = new List<string>();
                            
                            if (permsObj is JsonElement jsonElement)
                            {
                                if (jsonElement.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var item in jsonElement.EnumerateArray())
                                    {
                                        if (item.ValueKind == JsonValueKind.String)
                                        {
                                            var permId = item.GetString();
                                            if (!string.IsNullOrWhiteSpace(permId))
                                                permIds.Add(permId);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                var permsJson = JsonSerializer.Serialize(permsObj);
                                var deserialized = JsonSerializer.Deserialize<List<string>>(permsJson);
                                if (deserialized != null)
                                    permIds = deserialized;
                            }
                            
                            foreach (var permId in permIds)
                            {
                                if (string.IsNullOrWhiteSpace(permId) || permissionSet.Contains(permId))
                                    continue;
                                    
                                permissionSet.Add(permId);
                                
                                var parts = permId.Split('.');
                                var category = parts.Length > 0 ? parts[0] : "Other";
                                var subcategory = parts.Length > 1 ? parts[1] : "";
                                var action = parts.Length > 2 ? parts[2] : "";
                                
                                var displayName = action == "*" ? "All Actions" : 
                                                 action != "" ? ToTitleHelper(action) :
                                                 subcategory == "*" ? "All " + ToTitleHelper(category) :
                                                 subcategory != "" ? ToTitleHelper(subcategory) :
                                                 category == "*" ? "All Permissions" :
                                                 ToTitleHelper(category);

                                allPermissions.Add(new Dictionary<string, object>
                                {
                                    ["id"] = permId,
                                    ["name"] = displayName,
                                    ["category"] = ToTitleHelper(category),
                                    ["subcategory"] = subcategory != "" && subcategory != "*" ? ToTitleHelper(subcategory) : "",
                                    ["packageId"] = packageId,
                                    ["moduleId"] = moduleId,
                                    ["description"] = $"Permission from {packageName} / {moduleName}"
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error processing permissions for module {ModuleId}", moduleId);
                        }
                    }
                }
            }

            // Add core system permissions
            var corePerms = new[]
            {
                new Dictionary<string, object> { ["id"] = "system.users.read", ["name"] = "View Users", ["category"] = "System", ["subcategory"] = "Users", ["packageId"] = "core", ["moduleId"] = "system", ["description"] = "View user list and details" },
                new Dictionary<string, object> { ["id"] = "system.users.write", ["name"] = "Manage Users", ["category"] = "System", ["subcategory"] = "Users", ["packageId"] = "core", ["moduleId"] = "system", ["description"] = "Create and edit users" },
                new Dictionary<string, object> { ["id"] = "system.users.delete", ["name"] = "Delete Users", ["category"] = "System", ["subcategory"] = "Users", ["packageId"] = "core", ["moduleId"] = "system", ["description"] = "Delete users" },
                new Dictionary<string, object> { ["id"] = "system.groups.read", ["name"] = "View Groups", ["category"] = "System", ["subcategory"] = "Groups", ["packageId"] = "core", ["moduleId"] = "system", ["description"] = "View group list and details" },
                new Dictionary<string, object> { ["id"] = "system.groups.write", ["name"] = "Manage Groups", ["category"] = "System", ["subcategory"] = "Groups", ["packageId"] = "core", ["moduleId"] = "system", ["description"] = "Create and edit groups" },
                new Dictionary<string, object> { ["id"] = "system.groups.delete", ["name"] = "Delete Groups", ["category"] = "System", ["subcategory"] = "Groups", ["packageId"] = "core", ["moduleId"] = "system", ["description"] = "Delete groups" },
                new Dictionary<string, object> { ["id"] = "system.permissions.read", ["name"] = "View Permissions", ["category"] = "System", ["subcategory"] = "Permissions", ["packageId"] = "core", ["moduleId"] = "system", ["description"] = "View available permissions" },
                new Dictionary<string, object> { ["id"] = "system.settings.read", ["name"] = "View Settings", ["category"] = "System", ["subcategory"] = "Settings", ["packageId"] = "core", ["moduleId"] = "system", ["description"] = "View system settings" },
                new Dictionary<string, object> { ["id"] = "system.settings.write", ["name"] = "Manage Settings", ["category"] = "System", ["subcategory"] = "Settings", ["packageId"] = "core", ["moduleId"] = "system", ["description"] = "Modify system settings" },
                new Dictionary<string, object> { ["id"] = "*", ["name"] = "All Permissions", ["category"] = "System", ["subcategory"] = "All", ["packageId"] = "core", ["moduleId"] = "system", ["description"] = "Full system access" }
            };

            foreach (var perm in corePerms)
            {
                var permId = perm["id"].ToString() ?? "";
                if (!permissionSet.Contains(permId))
                {
                    permissionSet.Add(permId);
                    allPermissions.Add(perm);
                }
            }

            return (true, allPermissions, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting permissions");
            return (false, new List<Dictionary<string, object>>(), ex.Message);
        }
    }
}
