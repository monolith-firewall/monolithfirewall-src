using Microsoft.AspNetCore.Mvc;
using Monolith.FireWall.WebUI.Features.Users.Models;
using Monolith.FireWall.WebUI.Features.Users.Services;

namespace Monolith.FireWall.WebUI.Features.Users.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly UserService _userService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(UserService userService, ILogger<UsersController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<UserEntity>>> GetAllUsers()
    {
        var users = await _userService.GetAllUsersAsync();
        return Ok(new { success = true, data = users });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserEntity>> GetUser(int id)
    {
        var user = await _userService.GetUserByIdAsync(id);
        if (user == null)
            return NotFound(new { success = false, error = "User not found" });

        // Get user's groups and effective permissions
        var groupService = HttpContext.RequestServices.GetRequiredService<UserGroupService>();
        var groups = await groupService.GetUserGroupsAsync(id);
        var effectivePermissions = await groupService.GetUserEffectivePermissionsAsync(id);

        return Ok(new
        {
            success = true,
            data = new
            {
                id = user.Id,
                username = user.Username,
                email = user.Email,
                enabled = user.Enabled,
                roles = user.GetRoles(),
                groups = groups.Select(g => new { id = g.Id, name = g.Name }),
                permissions = effectivePermissions,
                createdAt = user.CreatedAt,
                updatedAt = user.UpdatedAt
            }
        });
    }

    [HttpPost]
    public async Task<ActionResult<UserEntity>> CreateUser([FromBody] CreateUserRequest request)
    {
        try
        {
            var user = await _userService.CreateUserAsync(
                request.Username,
                request.Email,
                request.Password,
                request.Roles ?? Array.Empty<string>()
            );
            return Ok(new { success = true, data = user });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<UserEntity>> UpdateUser(int id, [FromBody] UpdateUserRequest request)
    {
        try
        {
            var user = await _userService.GetUserByIdAsync(id);
            if (user == null)
                return NotFound(new { success = false, error = "User not found" });

            user.Email = request.Email ?? user.Email;
            if (request.Roles != null)
                user.SetRoles(request.Roles);
            user.Enabled = request.Enabled ?? user.Enabled;

            if (!string.IsNullOrEmpty(request.Password))
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
            }

            await _userService.UpdateUserAsync(user);
            return Ok(new { success = true, data = user });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteUser(int id)
    {
        var deleted = await _userService.DeleteUserAsync(id);
        if (!deleted)
            return NotFound(new { success = false, error = "User not found" });

        return Ok(new { success = true, data = new { deleted = true } });
    }
}

public record CreateUserRequest(
    string Username,
    string Email,
    string Password,
    string[]? Roles
);

public record UpdateUserRequest(
    string? Email,
    string? Password,
    string[]? Roles,
    bool? Enabled
);
