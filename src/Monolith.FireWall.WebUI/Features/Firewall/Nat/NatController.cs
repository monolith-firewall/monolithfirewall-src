using Microsoft.AspNetCore.Mvc;
using Monolith.FireWall.WebUI.Features.Firewall.Nat;

namespace Monolith.FireWall.WebUI.Features.Firewall.Nat;

[ApiController]
[Route("api/firewall/nat")]
public class NatController : ControllerBase
{
    private readonly Services.CoreApiClient _coreClient;
    private readonly ILogger<NatController> _logger;

    public NatController(Services.CoreApiClient coreClient, ILogger<NatController> logger)
    {
        _coreClient = coreClient;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult> List([FromQuery] int limit = 100, [FromQuery] int offset = 0)
    {
        try
        {
            var coreRequest = new { action = "firewall.nat.list" };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing NAT rules");
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
                action = "firewall.nat.get",
                payload = new { id }
            };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting NAT rule {Id}", id);
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] NatRule rule)
    {
        try
        {
            var coreRequest = new
            {
                action = "firewall.nat.create",
                payload = new
                {
                    type = rule.Type,
                    @interface = rule.Interface,
                    addressFamily = rule.AddressFamily,
                    protocol = rule.Protocol,
                    sourceType = rule.SourceType,
                    sourceValue = rule.SourceValue,
                    sourcePort = rule.SourcePort,
                    destinationType = rule.DestinationType,
                    destinationValue = rule.DestinationValue,
                    destinationPort = rule.DestinationPort,
                    redirectTargetIp = rule.RedirectTargetIp,
                    redirectTargetPort = rule.RedirectTargetPort,
                    reflectionMode = rule.ReflectionMode,
                    description = rule.Description,
                    enabled = rule.Enabled
                }
            };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating NAT rule");
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> Update(int id, [FromBody] NatRule rule)
    {
        try
        {
            var coreRequest = new
            {
                action = "firewall.nat.update",
                payload = new
                {
                    id,
                    type = rule.Type,
                    @interface = rule.Interface,
                    addressFamily = rule.AddressFamily,
                    protocol = rule.Protocol,
                    sourceType = rule.SourceType,
                    sourceValue = rule.SourceValue,
                    sourcePort = rule.SourcePort,
                    destinationType = rule.DestinationType,
                    destinationValue = rule.DestinationValue,
                    destinationPort = rule.DestinationPort,
                    redirectTargetIp = rule.RedirectTargetIp,
                    redirectTargetPort = rule.RedirectTargetPort,
                    reflectionMode = rule.ReflectionMode,
                    description = rule.Description,
                    enabled = rule.Enabled
                }
            };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating NAT rule {Id}", id);
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
                action = "firewall.nat.delete",
                payload = new { id }
            };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting NAT rule {Id}", id);
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }

    [HttpPost("reorder")]
    public async Task<ActionResult> Reorder([FromBody] int[] ruleIds)
    {
        try
        {
            var coreRequest = new
            {
                action = "firewall.nat.reorder",
                payload = new { ruleIds = ruleIds.ToList() }
            };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reordering NAT rules");
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }
}
