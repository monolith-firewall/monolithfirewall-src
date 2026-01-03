using Microsoft.AspNetCore.Mvc;
using Monolith.FireWall.WebUI.Features.Firewall.Aliases;

namespace Monolith.FireWall.WebUI.Features.Firewall.Aliases;

[ApiController]
[Route("api/firewall/aliases")]
public class AliasesController : ControllerBase
{
    private readonly Services.CoreApiClient _coreClient;
    private readonly ILogger<AliasesController> _logger;

    public AliasesController(Services.CoreApiClient coreClient, ILogger<AliasesController> logger)
    {
        _coreClient = coreClient;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult> List([FromQuery] int limit = 100, [FromQuery] int offset = 0)
    {
        try
        {
            var coreRequest = new { action = "firewall.aliases.list" };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing aliases");
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
                action = "firewall.aliases.get",
                payload = new { id }
            };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting alias {Id}", id);
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }

    [HttpGet("{name}/resolve")]
    public async Task<ActionResult> Resolve(string name)
    {
        try
        {
            var coreRequest = new
            {
                action = "firewall.aliases.resolve",
                payload = new { name }
            };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving alias {Name}", name);
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] FirewallAlias alias)
    {
        try
        {
            var coreRequest = new
            {
                action = "firewall.aliases.create",
                payload = new
                {
                    name = alias.Name,
                    type = alias.Type,
                    description = alias.Description,
                    content = alias.Content?.ToList() ?? new List<string>(),
                    enabled = alias.Enabled
                }
            };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating alias");
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> Update(int id, [FromBody] FirewallAlias alias)
    {
        try
        {
            var coreRequest = new
            {
                action = "firewall.aliases.update",
                payload = new
                {
                    id,
                    name = alias.Name,
                    type = alias.Type,
                    description = alias.Description,
                    content = alias.Content?.ToList() ?? new List<string>(),
                    enabled = alias.Enabled
                }
            };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating alias {Id}", id);
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
                action = "firewall.aliases.delete",
                payload = new { id }
            };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting alias {Id}", id);
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }
}
