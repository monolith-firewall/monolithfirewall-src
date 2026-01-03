using Microsoft.AspNetCore.Mvc;

namespace Monolith.FireWall.WebUI.Features.Firewall.Rules;

[ApiController]
[Route("api/firewall/rules")]
public class RulesController : ControllerBase
{
    private readonly Services.CoreApiClient _coreClient;
    private readonly ILogger<RulesController> _logger;

    public RulesController(Services.CoreApiClient coreClient, ILogger<RulesController> logger)
    {
        _coreClient = coreClient;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult> List()
    {
        try
        {
            var coreRequest = new { action = "firewall.rules.list" };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing firewall rules");
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult> Get(int id)
    {
        try
        {
            var coreRequest = new { action = "firewall.rules.get", payload = new { id } };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting firewall rule {Id}", id);
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] RuleRequest request)
    {
        try
        {
            var coreRequest = new { action = "firewall.rules.create", payload = request };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating firewall rule");
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult> Update(int id, [FromBody] RuleRequest request)
    {
        try
        {
            var payload = new RuleUpdateRequest(request) { Id = id };
            var coreRequest = new { action = "firewall.rules.update", payload };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating firewall rule {Id}", id);
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> Delete(int id)
    {
        try
        {
            var coreRequest = new { action = "firewall.rules.delete", payload = new { id } };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting firewall rule {Id}", id);
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }

    [HttpPost("reorder")]
    public async Task<ActionResult> Reorder([FromBody] RuleReorderRequest request)
    {
        try
        {
            var coreRequest = new { action = "firewall.rules.reorder", payload = request };
            var responseJson = await _coreClient.SendRequestAsync(System.Text.Json.JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reordering firewall rules");
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }
}

public class RuleRequest
{
    public string? Interface { get; set; }
    public string? Direction { get; set; }
    public string? Action { get; set; }
    public string? AddressFamily { get; set; }
    public string? Protocol { get; set; }
    public string? SourceType { get; set; }
    public string? SourceValue { get; set; }
    public string? SourcePort { get; set; }
    public string? DestinationType { get; set; }
    public string? DestinationValue { get; set; }
    public string? DestinationPort { get; set; }
    public string? Gateway { get; set; }
    public bool LogEnabled { get; set; }
    public bool Enabled { get; set; } = true;
    public string? Description { get; set; }
}

public sealed class RuleUpdateRequest : RuleRequest
{
    public RuleUpdateRequest(RuleRequest source)
    {
        Interface = source.Interface;
        Direction = source.Direction;
        Action = source.Action;
        AddressFamily = source.AddressFamily;
        Protocol = source.Protocol;
        SourceType = source.SourceType;
        SourceValue = source.SourceValue;
        SourcePort = source.SourcePort;
        DestinationType = source.DestinationType;
        DestinationValue = source.DestinationValue;
        DestinationPort = source.DestinationPort;
        Gateway = source.Gateway;
        LogEnabled = source.LogEnabled;
        Enabled = source.Enabled;
        Description = source.Description;
    }

    public int Id { get; set; }
}

public sealed class RuleReorderRequest
{
    public string? Interface { get; set; }
    public List<int> RuleIds { get; set; } = new();
}
