using Microsoft.AspNetCore.Mvc;
namespace Monolith.FireWall.WebUI.Features.Firewall;

[ApiController]
[Route("api/firewall")]
public class FirewallController : ControllerBase
{
    private readonly Services.CoreApiClient _coreClient;
    private readonly ILogger<FirewallController> _logger;

    public FirewallController(Services.CoreApiClient coreClient, ILogger<FirewallController> logger)
    {
        _coreClient = coreClient;
        _logger = logger;
    }

    [HttpGet("status")]
    public async Task<ActionResult> GetStatus()
    {
        try
        {
            var coreRequest = new { action = "firewall.status" };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting firewall status");
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }

    [HttpGet("config")]
    public async Task<ActionResult> GetConfig()
    {
        try
        {
            var coreRequest = new { action = "firewall.config" };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting firewall config");
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }

    [HttpGet("pending-changes")]
    public async Task<ActionResult> GetPendingChanges()
    {
        try
        {
            var coreRequest = new { action = "firewall.pending" };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending changes count");
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }

    [HttpPost("apply")]
    public async Task<ActionResult> Apply()
    {
        try
        {
            var coreRequest = new { action = "firewall.apply" };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying firewall changes");
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }

    [HttpPost("discard")]
    public async Task<ActionResult> Discard()
    {
        try
        {
            var coreRequest = new { action = "firewall.discard" };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discarding firewall changes");
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }

    [HttpGet("preview")]
    public async Task<ActionResult> Preview()
    {
        try
        {
            var coreRequest = new { action = "firewall.preview" };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating firewall preview");
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }
}
