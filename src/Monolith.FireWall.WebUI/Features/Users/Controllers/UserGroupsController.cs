using Microsoft.AspNetCore.Mvc;
using Monolith.FireWall.WebUI.Features.Users.Models;
using Monolith.FireWall.WebUI.Features.Users.Services;

namespace Monolith.FireWall.WebUI.Features.Users.Controllers;

[ApiController]
[Route("api/usergroups")]
public class UserGroupsController : ControllerBase
{
    private readonly UserGroupService _service;
    private readonly ILogger<UserGroupsController> _logger;

    public UserGroupsController(UserGroupService service, ILogger<UserGroupsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var groups = await _service.GetAllGroupsAsync();
            var groupsData = groups.Select(g => new
            {
                id = g.Id,
                name = g.Name,
                description = g.Description,
                permissions = g.GetPermissions(),
                enabled = g.Enabled,
                createdAt = g.CreatedAt,
                updatedAt = g.UpdatedAt
            });
            return Ok(new { success = true, data = groupsData });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all groups");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var group = await _service.GetGroupByIdAsync(id);
            if (group == null)
                return NotFound(new { success = false, error = "Group not found" });

            return Ok(new
            {
                success = true,
                data = new
                {
                    id = group.Id,
                    name = group.Name,
                    description = group.Description,
                    permissions = group.GetPermissions(),
                    enabled = group.Enabled,
                    createdAt = group.CreatedAt,
                    updatedAt = group.UpdatedAt
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting group {Id}", id);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGroupRequest request)
    {
        try
        {
            var id = await _service.CreateGroupAsync(
                request.Name,
                request.Description,
                request.Permissions ?? Array.Empty<string>());

            if (id > 0)
            {
                var group = await _service.GetGroupByIdAsync(id);
                return CreatedAtAction(nameof(GetById), new { id }, new { success = true, data = group });
            }

            return BadRequest(new { success = false, error = "Failed to create group" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating group");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateGroupRequest request)
    {
        try
        {
            var success = await _service.UpdateGroupAsync(
                id,
                request.Description,
                request.Permissions,
                request.Enabled);

            if (success)
            {
                var group = await _service.GetGroupByIdAsync(id);
                return Ok(new { success = true, data = group });
            }

            return NotFound(new { success = false, error = "Group not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating group {Id}", id);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var success = await _service.DeleteGroupAsync(id);
            if (success)
                return Ok(new { success = true });

            return NotFound(new { success = false, error = "Group not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting group {Id}", id);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    [HttpGet("{id}/users")]
    public async Task<IActionResult> GetGroupUsers(int id)
    {
        try
        {
            var users = await _service.GetGroupUsersAsync(id);
            return Ok(new { success = true, data = users });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting group users {Id}", id);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    [HttpPost("{id}/users/{userId}")]
    public async Task<IActionResult> AddUserToGroup(int id, int userId)
    {
        try
        {
            var success = await _service.AddUserToGroupAsync(userId, id);
            if (success)
                return Ok(new { success = true });

            return BadRequest(new { success = false, error = "Failed to add user to group" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding user to group");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    [HttpDelete("{id}/users/{userId}")]
    public async Task<IActionResult> RemoveUserFromGroup(int id, int userId)
    {
        try
        {
            var success = await _service.RemoveUserFromGroupAsync(userId, id);
            if (success)
                return Ok(new { success = true });

            return BadRequest(new { success = false, error = "Failed to remove user from group" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing user from group");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    [HttpGet("user/{userId}/permissions")]
    public async Task<IActionResult> GetUserEffectivePermissions(int userId)
    {
        try
        {
            var permissions = await _service.GetUserEffectivePermissionsAsync(userId);
            return Ok(new { success = true, data = permissions });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user permissions");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }
}

public record CreateGroupRequest(
    string Name,
    string? Description,
    string[]? Permissions
);

public record UpdateGroupRequest(
    string? Description,
    string[]? Permissions,
    bool? Enabled
);
