using Microsoft.AspNetCore.Mvc;
using Monolith.FireWall.WebUI.Features.Firewall.Schedules;

namespace Monolith.FireWall.WebUI.Features.Firewall.Schedules;

[ApiController]
[Route("api/firewall/schedules")]
public class SchedulesController : ControllerBase
{
    private readonly Services.CoreApiClient _coreClient;
    private readonly ILogger<SchedulesController> _logger;

    public SchedulesController(Services.CoreApiClient coreClient, ILogger<SchedulesController> logger)
    {
        _coreClient = coreClient;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult> List([FromQuery] int limit = 100, [FromQuery] int offset = 0)
    {
        try
        {
            var coreRequest = new { action = "firewall.schedules.list" };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing schedules");
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
                action = "firewall.schedules.get",
                payload = new { id }
            };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting schedule {Id}", id);
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }

    [HttpGet("{id}/active")]
    public async Task<ActionResult> IsActive(int id)
    {
        try
        {
            var coreRequest = new
            {
                action = "firewall.schedules.active",
                payload = new { id }
            };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking schedule active status {Id}", id);
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] FirewallSchedule schedule)
    {
        try
        {
            var coreRequest = new
            {
                action = "firewall.schedules.create",
                payload = new
                {
                    name = schedule.Name,
                    description = schedule.Description,
                    timeRanges = schedule.TimeRanges,
                    enabled = schedule.Enabled
                }
            };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating schedule");
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> Update(int id, [FromBody] FirewallSchedule schedule)
    {
        try
        {
            var coreRequest = new
            {
                action = "firewall.schedules.update",
                payload = new
                {
                    id,
                    name = schedule.Name,
                    description = schedule.Description,
                    timeRanges = schedule.TimeRanges,
                    enabled = schedule.Enabled
                }
            };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating schedule {Id}", id);
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
                action = "firewall.schedules.delete",
                payload = new { id }
            };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting schedule {Id}", id);
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }
}
