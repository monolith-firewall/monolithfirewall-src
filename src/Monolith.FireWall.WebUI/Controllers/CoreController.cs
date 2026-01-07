using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Monolith.FireWall.WebUI.Services;

namespace Monolith.FireWall.WebUI.Controllers;

[ApiController]
[Route("api/core")]
public class CoreController : ControllerBase
{
    private readonly CoreApiClient _coreClient;
    private readonly ILogger<CoreController> _logger;

    public CoreController(CoreApiClient coreClient, ILogger<CoreController> logger)
    {
        _coreClient = coreClient;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Get([FromQuery] string action)
    {
        try
        {
            if (string.IsNullOrEmpty(action))
            {
                return BadRequest(new { success = false, error = "Action parameter is required" });
            }

            // Create request JSON for Core
            var request = new
            {
                action = action
            };

            var requestJson = JsonSerializer.Serialize(request);
            var responseJson = await _coreClient.SendRequestAsync(requestJson);
            
            // Parse the response from Core
            var response = JsonSerializer.Deserialize<JsonElement>(responseJson);
            
            // Return the response as-is (Core already formats it correctly)
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Core API request: {Action}", action);
            // Return a safe fallback response
            return Ok(new { success = false, error = ex.Message });
        }
    }

    [HttpPost("")]
    public async Task<IActionResult> Post([FromBody] JsonElement requestBody)
    {
        try
        {
            // Extract action from request body
            if (!requestBody.TryGetProperty("action", out var actionElement))
            {
                return BadRequest(new { success = false, error = "Action is required in request body" });
            }

            var action = actionElement.GetString();
            if (string.IsNullOrEmpty(action))
            {
                return BadRequest(new { success = false, error = "Action cannot be empty" });
            }

            // Send request to Core
            var requestJson = requestBody.GetRawText();
            var responseJson = await _coreClient.SendRequestAsync(requestJson);
            
            // Parse the response from Core
            var response = JsonSerializer.Deserialize<JsonElement>(responseJson);
            
            // Return the response as-is (Core already formats it correctly)
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Core API POST request");
            return Ok(new { success = false, error = ex.Message });
        }
    }
}
