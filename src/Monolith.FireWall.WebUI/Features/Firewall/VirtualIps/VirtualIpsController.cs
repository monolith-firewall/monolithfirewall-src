using Microsoft.AspNetCore.Mvc;
using Monolith.FireWall.WebUI.Features.Firewall.VirtualIps;

namespace Monolith.FireWall.WebUI.Features.Firewall.VirtualIps;

[ApiController]
[Route("api/firewall/virtual-ips")]
public class VirtualIpsController : ControllerBase
{
    private readonly Services.CoreApiClient _coreClient;
    private readonly ILogger<VirtualIpsController> _logger;

    public VirtualIpsController(Services.CoreApiClient coreClient, ILogger<VirtualIpsController> logger)
    {
        _coreClient = coreClient;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult> List([FromQuery] int limit = 100, [FromQuery] int offset = 0)
    {
        try
        {
            var coreRequest = new { action = "firewall.virtualips.list" };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
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
            var coreRequest = new
            {
                action = "firewall.virtualips.get",
                payload = new { id }
            };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
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
            var coreRequest = new
            {
                action = "firewall.virtualips.create",
                payload = new
                {
                    name = vip.Name,
                    type = vip.Type,
                    @interface = vip.Interface,
                    address = vip.Address,
                    subnetBits = vip.SubnetBits,
                    description = vip.Description,
                    enabled = vip.Enabled,
                    vhid = vip.Vhid,
                    carpPassword = vip.CarpPassword,
                    advskew = vip.Advskew
                }
            };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
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
            var coreRequest = new
            {
                action = "firewall.virtualips.update",
                payload = new
                {
                    id,
                    name = vip.Name,
                    type = vip.Type,
                    @interface = vip.Interface,
                    address = vip.Address,
                    subnetBits = vip.SubnetBits,
                    description = vip.Description,
                    enabled = vip.Enabled,
                    vhid = vip.Vhid,
                    carpPassword = vip.CarpPassword,
                    advskew = vip.Advskew
                }
            };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
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
            var coreRequest = new
            {
                action = "firewall.virtualips.delete",
                payload = new { id }
            };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting virtual IP {Id}", id);
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }
}
