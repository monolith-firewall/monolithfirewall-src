using Microsoft.AspNetCore.Mvc;
using Monolith.FireWall.WebUI.Features.Firewall;
using Monolith.FireWall.WebUI.Features.Firewall.Schedules;

namespace Monolith.FireWall.WebUI.Features.Firewall.Schedules;

[ApiController]
[Route("api/firewall/schedules")]
public class SchedulesController : ControllerBase
{
    private readonly FirewallService _firewallService;
    private readonly ILogger<SchedulesController> _logger;

    public SchedulesController(FirewallService firewallService, ILogger<SchedulesController> logger)
    {
        _firewallService = firewallService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult> List([FromQuery] int limit = 100, [FromQuery] int offset = 0)
    {
        try
        {
            var schedules = await _firewallService.Schedules.ListSchedulesAsync();
            var totalCount = schedules.Count;
            var paginated = schedules.Skip(offset).Take(limit).ToList();
            
            return Ok(new
            {
                success = true,
                data = new
                {
                    items = paginated,
                    totalCount,
                    limit,
                    offset
                },
                error = (string?)null
            });
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
            var schedule = await _firewallService.Schedules.GetScheduleAsync(id);
            if (schedule == null)
                return NotFound(new { success = false, data = (object?)null, error = "Schedule not found" });

            return Ok(new { success = true, data = schedule, error = (string?)null });
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
            var schedule = await _firewallService.Schedules.GetScheduleAsync(id);
            if (schedule == null)
                return NotFound(new { success = false, data = (object?)null, error = "Schedule not found" });

            // TODO: Implement actual schedule active check based on current time
            var isActive = false; // Placeholder
            
            return Ok(new { success = true, data = new { isActive }, error = (string?)null });
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
            var created = await _firewallService.Schedules.CreateScheduleAsync(schedule);
            _firewallService.MarkPendingChanges();
            
            return CreatedAtAction(nameof(Get), new { id = created.Id }, 
                new { success = true, data = created, error = (string?)null });
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
            var updated = await _firewallService.Schedules.UpdateScheduleAsync(id, schedule);
            _firewallService.MarkPendingChanges();
            
            return Ok(new { success = true, data = updated, error = (string?)null });
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
            var success = await _firewallService.Schedules.DeleteScheduleAsync(id);
            if (!success)
                return NotFound(new { success = false, data = (object?)null, error = "Schedule not found" });

            _firewallService.MarkPendingChanges();
            return Ok(new { success = true, data = (object?)null, error = (string?)null });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting schedule {Id}", id);
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }
}
