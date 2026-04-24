using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Monolith.FireWall.WebUI.Features.Users.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly Monolith.FireWall.WebUI.Services.CoreApiClient _coreClient;
    private readonly ILogger<UsersController> _logger;

    public UsersController(Monolith.FireWall.WebUI.Services.CoreApiClient coreClient, ILogger<UsersController> logger)
    {
        _coreClient = coreClient;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult> GetAllUsers()
    {
        try
        {
            var coreRequest = new { action = "users.list" };
            var responseJson = await _coreClient.SendRequestAsync(JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing users");
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult> GetUser(int id)
    {
        try
        {
            var coreRequest = new { action = "users.get", payload = new { id } };
            var responseJson = await _coreClient.SendRequestAsync(JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user {Id}", id);
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult> CreateUser([FromBody] JsonElement body)
    {
        try
        {
            var coreRequest = new { action = "users.create", payload = body };
            var responseJson = await _coreClient.SendRequestAsync(JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user");
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateUser(int id, [FromBody] JsonElement body)
    {
        try
        {
            // Merge id into the payload
            var payloadDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body.GetRawText()) ?? new();
            payloadDict["id"] = JsonSerializer.Deserialize<JsonElement>(id.ToString());
            var coreRequest = new { action = "users.update", payload = payloadDict };
            var responseJson = await _coreClient.SendRequestAsync(JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {Id}", id);
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteUser(int id)
    {
        try
        {
            var coreRequest = new { action = "users.delete", payload = new { id } };
            var responseJson = await _coreClient.SendRequestAsync(JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {Id}", id);
            return StatusCode(500, new { success = false, data = (object?)null, error = ex.Message });
        }
    }
}
