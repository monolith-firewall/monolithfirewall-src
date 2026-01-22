using Microsoft.AspNetCore.Mvc;
using Monolith.FireWall.WebUI.Services;

namespace Monolith.FireWall.WebUI.Controllers;

[ApiController]
[Route("api/permissions")]
public class PermissionsController : ControllerBase
{
    private readonly PackageDiscoveryService _packageDiscovery;
    private readonly ILogger<PermissionsController> _logger;

    public PermissionsController(
        PackageDiscoveryService packageDiscovery,
        ILogger<PermissionsController> logger)
    {
        _packageDiscovery = packageDiscovery;
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
            var permissions = await _packageDiscovery.GetAllPermissionsAsync();
            return Ok(new { permissions });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get permissions");
            return StatusCode(500, new { error = "Failed to retrieve permissions" });
        }
    }

    /// <summary>
    /// Get permissions grouped by category.
    /// </summary>
    [HttpGet("categories")]
    public async Task<IActionResult> GetPermissionsByCategory()
    {
        try
        {
            var permissions = await _packageDiscovery.GetAllPermissionsAsync();
            var categories = permissions
                .GroupBy(p => p.Category)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(p => p.Id).ToList()
                );

            // Also include wildcard permissions (e.g., "network.*")
            var wildcards = permissions
                .Select(p => p.Id.Split('.').Take(2))
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
            var permissions = await _packageDiscovery.GetAllPermissionsAsync();
            var byPackage = permissions
                .GroupBy(p => new { p.PackageId, p.PackageName })
                .ToDictionary(
                    g => g.Key.PackageId,
                    g => new
                    {
                        packageName = g.Key.PackageName,
                        permissions = g.Select(p => new
                        {
                            p.Id,
                            p.Name,
                            p.Category,
                            p.Subcategory,
                            p.ModuleId,
                            p.ModuleName
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
            var permissions = await _packageDiscovery.GetAllPermissionsAsync();
            var packagePermissions = permissions
                .Where(p => string.Equals(p.PackageId, packageId, StringComparison.OrdinalIgnoreCase))
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
}
