using Microsoft.AspNetCore.Mvc;
using Monolith.FireWall.WebUI.Features.Firewall;
using Monolith.FireWall.WebUI.Features.Firewall.TrafficShaper;

namespace Monolith.FireWall.WebUI.Features.Firewall.TrafficShaper;

[ApiController]
[Route("api/firewall/traffic-shaper")]
public class TrafficShaperController : ControllerBase
{
    private readonly FirewallService _firewallService;
    private readonly ILogger<TrafficShaperController> _logger;

    public TrafficShaperController(FirewallService firewallService, ILogger<TrafficShaperController> logger)
    {
        _firewallService = firewallService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult> List([FromQuery] int limit = 100, [FromQuery] int offset = 0)
    {
        try
        {
            var rules = await _firewallService.TrafficShaper.ListRulesAsync();
            var totalCount = rules.Count;
            var paginated = rules.Skip(offset).Take(limit).ToList();
            
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
            _logger.LogError(ex, "Error listing traffic shaper rules");
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult> Get(int id)
    {
        try
        {
            var rule = await _firewallService.TrafficShaper.GetRuleAsync(id);
            if (rule == null)
                return NotFound(new { success = false, data = (object?)null, error = "Traffic shaper rule not found" });

            return Ok(new { success = true, data = rule, error = (string?)null });
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
            var created = await _firewallService.TrafficShaper.CreateRuleAsync(rule);
            _firewallService.MarkPendingChanges();
            
            return CreatedAtAction(nameof(Get), new { id = created.Id }, 
                new { success = true, data = created, error = (string?)null });
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
            var updated = await _firewallService.TrafficShaper.UpdateRuleAsync(id, rule);
            _firewallService.MarkPendingChanges();
            
            return Ok(new { success = true, data = updated, error = (string?)null });
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
            var success = await _firewallService.TrafficShaper.DeleteRuleAsync(id);
            if (!success)
                return NotFound(new { success = false, data = (object?)null, error = "Traffic shaper rule not found" });

            _firewallService.MarkPendingChanges();
            return Ok(new { success = true, data = (object?)null, error = (string?)null });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting traffic shaper rule {Id}", id);
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }
}
