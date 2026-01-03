using Microsoft.AspNetCore.Mvc;
using Monolith.FireWall.WebUI.Features.Firewall;
using Monolith.FireWall.WebUI.Features.Firewall.VirtualIps;

namespace Monolith.FireWall.WebUI.Features.Firewall.VirtualIps;

[ApiController]
[Route("api/firewall/virtual-ips")]
public class VirtualIpsController : ControllerBase
{
    private readonly FirewallService _firewallService;
    private readonly ILogger<VirtualIpsController> _logger;

    public VirtualIpsController(FirewallService firewallService, ILogger<VirtualIpsController> logger)
    {
        _firewallService = firewallService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult> List([FromQuery] int limit = 100, [FromQuery] int offset = 0)
    {
        try
        {
            var virtualIps = await _firewallService.VirtualIps.ListVirtualIpsAsync();
            var totalCount = virtualIps.Count;
            var paginated = virtualIps.Skip(offset).Take(limit).ToList();
            
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
            _logger.LogError(ex, "Error listing virtual IPs");
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult> Get(int id)
    {
        try
        {
            var vip = await _firewallService.VirtualIps.GetVirtualIpAsync(id);
            if (vip == null)
                return NotFound(new { success = false, data = (object?)null, error = "Virtual IP not found" });

            return Ok(new { success = true, data = vip, error = (string?)null });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting virtual IP {Id}", id);
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] VirtualIp vip)
    {
        try
        {
            var created = await _firewallService.VirtualIps.CreateVirtualIpAsync(vip);
            _firewallService.MarkPendingChanges();
            
            return CreatedAtAction(nameof(Get), new { id = created.Id }, 
                new { success = true, data = created, error = (string?)null });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating virtual IP");
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> Update(int id, [FromBody] VirtualIp vip)
    {
        try
        {
            var updated = await _firewallService.VirtualIps.UpdateVirtualIpAsync(id, vip);
            _firewallService.MarkPendingChanges();
            
            return Ok(new { success = true, data = updated, error = (string?)null });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating virtual IP {Id}", id);
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        try
        {
            var success = await _firewallService.VirtualIps.DeleteVirtualIpAsync(id);
            if (!success)
                return NotFound(new { success = false, data = (object?)null, error = "Virtual IP not found" });

            _firewallService.MarkPendingChanges();
            return Ok(new { success = true, data = (object?)null, error = (string?)null });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting virtual IP {Id}", id);
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }
}
