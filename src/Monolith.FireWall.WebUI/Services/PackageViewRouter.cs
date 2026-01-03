using System.Text.Json;
using Monolith.FireWall.Common.Models;

namespace Monolith.FireWall.WebUI.Services;

/// <summary>
/// Routes requests to package Razor views
/// </summary>
public class PackageViewRouter
{
    private readonly CoreApiClient _coreClient;
    private readonly ILogger<PackageViewRouter> _logger;
    private Dictionary<string, PageDefinition>? _cachedPages;
    private DateTime _cacheTime = DateTime.MinValue;
    private readonly TimeSpan _cacheTimeout = TimeSpan.FromMinutes(5);

    public PackageViewRouter(CoreApiClient coreClient, ILogger<PackageViewRouter> logger)
    {
        _coreClient = coreClient;
        _logger = logger;
    }

    /// <summary>
    /// Gets a page definition by route
    /// </summary>
    public async Task<PageDefinition?> GetPageAsync(string route)
    {
        // Refresh cache if expired
        if (_cachedPages == null || DateTime.UtcNow - _cacheTime > _cacheTimeout)
        {
            await RefreshCacheAsync();
        }

        if (_cachedPages != null && _cachedPages.TryGetValue(route, out var page))
        {
            return page;
        }

        return null;
    }

    /// <summary>
    /// Gets all pages from Core
    /// </summary>
    public async Task<List<PageDefinition>> GetAllPagesAsync()
    {
        // Refresh cache if expired
        if (_cachedPages == null || DateTime.UtcNow - _cacheTime > _cacheTimeout)
        {
            await RefreshCacheAsync();
        }

        return _cachedPages?.Values.ToList() ?? new List<PageDefinition>();
    }

    private async Task RefreshCacheAsync()
    {
        try
        {
            var request = new
            {
                package = "system",
                module = "core",
                action = "get-pages"  // Changed from get-all-pages
            };

            var requestJson = JsonSerializer.Serialize(request);
            var responseJson = await _coreClient.SendRequestAsync(requestJson);
            var response = JsonSerializer.Deserialize<JsonElement>(responseJson);

            // Core returns: { "Success": true, "Data": [ {page objects} ] }
            if (response.TryGetProperty("Success", out var successPascal) && successPascal.GetBoolean())
            {
                if (response.TryGetProperty("Data", out var dataPascal))
                {
                    var pageList = JsonSerializer.Deserialize<List<PageDefinition>>(dataPascal.GetRawText()) ?? new List<PageDefinition>();
                    _cachedPages = pageList.ToDictionary(p => p.Route, StringComparer.OrdinalIgnoreCase);
                    _cacheTime = DateTime.UtcNow;
                    _logger.LogInformation($"Refreshed page cache: {_cachedPages.Count} page(s)");
                    return;
                }
            }
            
            // Try camelCase fallback
            if (response.TryGetProperty("success", out var success) && success.GetBoolean())
            {
                if (response.TryGetProperty("data", out var data))
                {
                    var pageList = JsonSerializer.Deserialize<List<PageDefinition>>(data.GetRawText()) ?? new List<PageDefinition>();
                    _cachedPages = pageList.ToDictionary(p => p.Route, StringComparer.OrdinalIgnoreCase);
                    _cacheTime = DateTime.UtcNow;
                    _logger.LogInformation($"Refreshed page cache: {_cachedPages.Count} page(s)");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to refresh page cache: {ex.Message}");
            // Keep existing cache on error
        }
    }

    /// <summary>
    /// Checks if user has permission to access a page
    /// </summary>
    public bool HasPermission(UserContext? user, PageDefinition page)
    {
        if (user == null)
            return false;

        if (page.RequiredPermissions.Length == 0)
            return true;

        // Check if user has any of the required permissions
        return page.RequiredPermissions.Any(perm =>
            user.Permissions.Contains(perm) || user.Permissions.Contains("*"));
    }
}
