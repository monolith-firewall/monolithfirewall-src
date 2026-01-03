using Microsoft.AspNetCore.Mvc;

namespace Monolith.FireWall.WebUI.Features.Firewall.Rules;

[ApiController]
[Route("api/firewall/defaults")]
public class DefaultsController : ControllerBase
{
    private readonly Services.CoreApiClient _coreClient;
    private readonly ILogger<DefaultsController> _logger;

    public DefaultsController(Services.CoreApiClient coreClient, ILogger<DefaultsController> logger)
    {
        _coreClient = coreClient;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult> Get()
    {
        try
        {
            var coreRequest = new { action = "firewall.defaults.get" };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting firewall defaults");
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult> Update([FromBody] DefaultsRequest request)
    {
        try
        {
            var coreRequest = new { action = "firewall.defaults.update", payload = request };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating firewall defaults");
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }
}

public sealed class DefaultsRequest
{
    public string? LanDefaultAction { get; set; }
    public string? WanDefaultAction { get; set; }
    public string? OptDefaultAction { get; set; }
    public bool BlockReservedOnWan { get; set; }
    public bool AllowManagementWebUi { get; set; }
}
