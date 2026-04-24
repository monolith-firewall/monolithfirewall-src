using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Monolith.FireWall.WebUI.Features.Users.Controllers;

[ApiController]
[Route("api/usergroups")]
public class UserGroupsController : ControllerBase
{
    private readonly Monolith.FireWall.WebUI.Services.CoreApiClient _coreClient;
    private readonly ILogger<UserGroupsController> _logger;

    public UserGroupsController(Monolith.FireWall.WebUI.Services.CoreApiClient coreClient, ILogger<UserGroupsController> logger)
    {
        _coreClient = coreClient;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult> GetAll()
    {
        try
        {
            var coreRequest = new { action = "users.groups.list" };
            var responseJson = await _coreClient.SendRequestAsync(JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing groups");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult> GetById(int id)
    {
        try
        {
            var coreRequest = new { action = "users.groups.get", payload = new { id } };
            var responseJson = await _coreClient.SendRequestAsync(JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting group {Id}", id);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] JsonElement body)
    {
        try
        {
            var coreRequest = new { action = "users.groups.create", payload = body };
            var responseJson = await _coreClient.SendRequestAsync(JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating group");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> Update(int id, [FromBody] JsonElement body)
    {
        try
        {
            var payloadDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body.GetRawText()) ?? new();
            payloadDict["id"] = JsonSerializer.Deserialize<JsonElement>(id.ToString());
            var coreRequest = new { action = "users.groups.update", payload = payloadDict };
            var responseJson = await _coreClient.SendRequestAsync(JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating group {Id}", id);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        try
        {
            var coreRequest = new { action = "users.groups.delete", payload = new { id } };
            var responseJson = await _coreClient.SendRequestAsync(JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting group {Id}", id);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    [HttpGet("{id}/users")]
    public async Task<ActionResult> GetGroupUsers(int id)
    {
        try
        {
            var coreRequest = new { action = "users.groups.users", payload = new { groupId = id } };
            var responseJson = await _coreClient.SendRequestAsync(JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting group users {Id}", id);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    [HttpPost("{id}/users/{userId}")]
    public async Task<ActionResult> AddUserToGroup(int id, int userId)
    {
        try
        {
            var coreRequest = new { action = "users.groups.adduser", payload = new { userId, groupId = id } };
            var responseJson = await _coreClient.SendRequestAsync(JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding user to group");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    [HttpDelete("{id}/users/{userId}")]
    public async Task<ActionResult> RemoveUserFromGroup(int id, int userId)
    {
        try
        {
            var coreRequest = new { action = "users.groups.removeuser", payload = new { userId, groupId = id } };
            var responseJson = await _coreClient.SendRequestAsync(JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing user from group");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    [HttpGet("user/{userId}/permissions")]
    public async Task<ActionResult> GetUserEffectivePermissions(int userId)
    {
        try
        {
            var coreRequest = new { action = "users.permissions", payload = new { userId } };
            var responseJson = await _coreClient.SendRequestAsync(JsonSerializer.Serialize(coreRequest));
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user permissions");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }
}
