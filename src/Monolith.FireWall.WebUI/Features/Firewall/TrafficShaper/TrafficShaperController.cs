using Microsoft.AspNetCore.Mvc;
using Monolith.FireWall.WebUI.Features.Firewall.TrafficShaper;

namespace Monolith.FireWall.WebUI.Features.Firewall.TrafficShaper;

[ApiController]
[Route("api/firewall/traffic-shaper")]
public class TrafficShaperController : ControllerBase
{
    private readonly Services.CoreApiClient _coreClient;
    private readonly ILogger<TrafficShaperController> _logger;

    public TrafficShaperController(Services.CoreApiClient coreClient, ILogger<TrafficShaperController> logger)
    {
        _coreClient = coreClient;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult> List([FromQuery] int limit = 100, [FromQuery] int offset = 0)
    {
        try
        {
            var coreRequest = new { action = "firewall.trafficshaper.list" };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing traffic shaper rules");
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult> Get(int id)
    {
        try
        {
            var coreRequest = new
            {
                action = "firewall.trafficshaper.get",
                payload = new { id }
            };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting traffic shaper rule {Id}", id);
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] TrafficShaperRule rule)
    {
        try
        {
            var coreRequest = new
            {
                action = "firewall.trafficshaper.create",
                payload = new
                {
                    name = rule.Name,
                    @interface = rule.Interface,
                    bandwidthUp = rule.BandwidthUp,
                    bandwidthDown = rule.BandwidthDown,
                    scheduler = rule.Scheduler,
                    description = rule.Description,
                    enabled = rule.Enabled
                }
            };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating traffic shaper rule");
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> Update(int id, [FromBody] TrafficShaperRule rule)
    {
        try
        {
            var coreRequest = new
            {
                action = "firewall.trafficshaper.update",
                payload = new
                {
                    id,
                    name = rule.Name,
                    @interface = rule.Interface,
                    bandwidthUp = rule.BandwidthUp,
                    bandwidthDown = rule.BandwidthDown,
                    scheduler = rule.Scheduler,
                    description = rule.Description,
                    enabled = rule.Enabled
                }
            };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating traffic shaper rule {Id}", id);
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        try
        {
            var coreRequest = new
            {
                action = "firewall.trafficshaper.delete",
                payload = new { id }
            };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting traffic shaper rule {Id}", id);
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }
}
