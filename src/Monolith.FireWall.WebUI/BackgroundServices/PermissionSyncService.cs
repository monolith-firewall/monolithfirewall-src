using Monolith.FireWall.WebUI.Features.Users.Services;
using Monolith.FireWall.WebUI.Services;

namespace Monolith.FireWall.WebUI.BackgroundServices;

/// <summary>
/// Background service that syncs package permissions with the Admin group
/// </summary>
public class PermissionSyncService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<PermissionSyncService> _logger;
    private readonly CoreApiClient _coreClient;

    public PermissionSyncService(
        IServiceProvider services,
        ILogger<PermissionSyncService> logger,
        CoreApiClient coreClient)
    {
        _services = services;
        _logger = logger;
        _coreClient = coreClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait a bit for Core to start and load packages
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncPermissionsAsync();
                
                // Check every 5 minutes for new packages/permissions
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing permissions");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    private async Task SyncPermissionsAsync()
    {
        try
        {
            // Get all menus (which include permissions)
            var menusRequest = System.Text.Json.JsonSerializer.Serialize(new { action = "get-menus" });
            var menusResponse = await _coreClient.SendRequestAsync(menusRequest);
            using var menusJson = System.Text.Json.JsonDocument.Parse(menusResponse);

            var permissions = new HashSet<string>();

            // Extract permissions from menus
            if (menusJson.RootElement.TryGetProperty("Data", out var dataElement) && 
                dataElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var menu in dataElement.EnumerateArray())
                {
                    ExtractPermissionsFromMenu(menu, permissions);
                }
            }

            // Also get packages and extract permissions from modules
            var packagesRequest = System.Text.Json.JsonSerializer.Serialize(new { action = "get-packages" });
            var packagesResponse = await _coreClient.SendRequestAsync(packagesRequest);
            using var packagesJson = System.Text.Json.JsonDocument.Parse(packagesResponse);

            if (packagesJson.RootElement.TryGetProperty("Data", out var packagesData) &&
                packagesData.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var package in packagesData.EnumerateArray())
                {
                    if (package.TryGetProperty("id", out var packageId))
                    {
                        var pkgId = packageId.GetString();
                        // Add wildcard permission for the package
                        permissions.Add($"{pkgId}.*");
                        
                        if (package.TryGetProperty("modules", out var modules) &&
                            modules.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var module in modules.EnumerateArray())
                            {
                                if (module.TryGetProperty("id", out var moduleId))
                                {
                                    var modId = moduleId.GetString();
                                    // Add wildcard permission for the module
                                    permissions.Add($"{pkgId}.{modId}.*");
                                }
                            }
                        }
                    }
                }
            }

            if (permissions.Count > 0)
            {
                using var scope = _services.CreateScope();
                var groupService = scope.ServiceProvider.GetRequiredService<UserGroupService>();
                
                var result = await groupService.AddPermissionsToAdminGroupAsync(permissions.ToArray());
                if (result)
                {
                    _logger.LogInformation("Successfully synced {Count} permissions to Admin group", permissions.Count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing permissions from Core");
        }
    }

    private void ExtractPermissionsFromMenu(System.Text.Json.JsonElement menu, HashSet<string> permissions)
    {
        // Extract RequiredPermissions
        if (menu.TryGetProperty("RequiredPermissions", out var reqPerms) &&
            reqPerms.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var perm in reqPerms.EnumerateArray())
            {
                var permStr = perm.GetString();
                if (!string.IsNullOrEmpty(permStr))
                {
                    permissions.Add(permStr);
                }
            }
        }

        // Recursively process children
        if (menu.TryGetProperty("Children", out var children) &&
            children.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var child in children.EnumerateArray())
            {
                ExtractPermissionsFromMenu(child, permissions);
            }
        }
    }
}
