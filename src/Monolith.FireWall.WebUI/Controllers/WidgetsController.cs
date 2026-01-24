using Microsoft.AspNetCore.Mvc;
using Monolith.FireWall.WebUI.Services;

namespace Monolith.FireWall.WebUI.Controllers;

[ApiController]
[Route("api/widgets")]
public class WidgetsController : ControllerBase
{
    private readonly PackageDiscoveryService _packageDiscovery;
    private readonly ILogger<WidgetsController> _logger;

    public WidgetsController(
        PackageDiscoveryService packageDiscovery,
        ILogger<WidgetsController> logger)
    {
        _packageDiscovery = packageDiscovery;
        _logger = logger;
    }

    /// <summary>
    /// Get all widgets from all installed packages.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllWidgets()
    {
        try
        {
            var widgets = await _packageDiscovery.GetAllWidgetsAsync();
            return Ok(new { widgets });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get widgets");
            return StatusCode(500, new { error = "Failed to retrieve widgets" });
        }
    }

    /// <summary>
    /// Get widgets for a specific package.
    /// </summary>
    [HttpGet("package/{packageId}")]
    public async Task<IActionResult> GetPackageWidgets(string packageId)
    {
        try
        {
            var widgets = await _packageDiscovery.GetAllWidgetsAsync();
            var packageWidgets = widgets
                .Where(w => string.Equals(w.PackageId, packageId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return Ok(new { widgets = packageWidgets });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get widgets for package {PackageId}", packageId);
            return StatusCode(500, new { error = "Failed to retrieve widgets" });
        }
    }
}
