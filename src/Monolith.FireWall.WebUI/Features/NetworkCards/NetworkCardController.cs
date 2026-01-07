using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Monolith.FireWall.WebUI.Services;

namespace Monolith.FireWall.WebUI.Features.NetworkCards;

[ApiController]
[Route("api/system/network-cards")]
public class NetworkCardController : ControllerBase
{
    private readonly CoreApiClient _coreClient;
    private readonly ILogger<NetworkCardController> _logger;

    public NetworkCardController(CoreApiClient coreClient, ILogger<NetworkCardController> logger)
    {
        _coreClient = coreClient;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult> List()
    {
        try
        {
            var coreRequest = new { action = "network.cards.list" };
            var responseJson = await _coreClient.SendRequestAsync(JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing network cards");
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }

    [HttpGet("{interface}")]
    public async Task<ActionResult> Get(string @interface)
    {
        try
        {
            var coreRequest = new
            {
                action = "network.cards.get",
                payload = new { @interface }
            };
            var responseJson = await _coreClient.SendRequestAsync(JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting network card {Interface}", @interface);
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }

    [HttpPost("{interface}/speed")]
    public async Task<ActionResult> SetSpeed(string @interface, [FromBody] NetworkCardSpeedRequest request)
    {
        try
        {
            request.Interface = @interface;
            var coreRequest = new
            {
                action = "network.cards.speed.set",
                payload = request
            };
            var responseJson = await _coreClient.SendRequestAsync(JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting speed for {Interface}", @interface);
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }

    [HttpPost("{interface}/offloads")]
    public async Task<ActionResult> SetOffloads(string @interface, [FromBody] NetworkCardOffloadRequest request)
    {
        try
        {
            request.Interface = @interface;
            var coreRequest = new
            {
                action = "network.cards.offloads.set",
                payload = request
            };
            var responseJson = await _coreClient.SendRequestAsync(JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting offloads for {Interface}", @interface);
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }

    [HttpPost("{interface}/buffers")]
    public async Task<ActionResult> SetBuffers(string @interface, [FromBody] NetworkCardBufferRequest request)
    {
        try
        {
            request.Interface = @interface;
            var coreRequest = new
            {
                action = "network.cards.buffers.set",
                payload = request
            };
            var responseJson = await _coreClient.SendRequestAsync(JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting buffers for {Interface}", @interface);
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }

    [HttpPost("{interface}/revert")]
    public async Task<ActionResult> Revert(string @interface)
    {
        try
        {
            var coreRequest = new
            {
                action = "network.cards.revert",
                payload = new { @interface }
            };
            var responseJson = await _coreClient.SendRequestAsync(JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reverting {Interface} to defaults", @interface);
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }
}

// Request models for WebUI
public sealed class NetworkCardSpeedRequest
{
    public string Interface { get; set; } = string.Empty;
    public string? Speed { get; set; }
    public string? Duplex { get; set; }
    public bool? AutoNegotiation { get; set; }
}

public sealed class NetworkCardOffloadRequest
{
    public string Interface { get; set; } = string.Empty;
    public Dictionary<string, bool> Offloads { get; set; } = new();
}

public sealed class NetworkCardBufferRequest
{
    public string Interface { get; set; } = string.Empty;
    public Dictionary<string, int> Buffers { get; set; } = new();
}
