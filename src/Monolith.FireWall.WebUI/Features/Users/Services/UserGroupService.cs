using Monolith.FireWall.WebUI.Features.Users.Models;
using Monolith.FireWall.WebUI.Features.Users.Repositories;

namespace Monolith.FireWall.WebUI.Features.Users.Services;

public class UserGroupService
{
    private readonly UserGroupRepository _repository;
    private readonly UserRepository _userRepository;
    private readonly ILogger<UserGroupService> _logger;

    public UserGroupService(
        UserGroupRepository repository,
        UserRepository userRepository,
        ILogger<UserGroupService> logger)
    {
        _repository = repository;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<List<UserGroupEntity>> GetAllGroupsAsync()
    {
        return await _repository.GetAllAsync();
    }

    public async Task<UserGroupEntity?> GetGroupByIdAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public async Task<UserGroupEntity?> GetGroupByNameAsync(string name)
    {
        return await _repository.GetByNameAsync(name);
    }

    public async Task<int> CreateGroupAsync(string name, string? description, string[] permissions)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Group name is required");

        var existing = await _repository.GetByNameAsync(name);
        if (existing != null)
            throw new InvalidOperationException($"Group '{name}' already exists");

        var group = new UserGroupEntity
        {
            Name = name,
            Description = description,
            Enabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        group.SetPermissions(permissions);

        return await _repository.CreateAsync(group);
    }

    public async Task<bool> UpdateGroupAsync(int id, string? description, string[]? permissions, bool? enabled)
    {
        var group = await _repository.GetByIdAsync(id);
        if (group == null)
            return false;

        if (description != null)
            group.Description = description;

        if (permissions != null)
            group.SetPermissions(permissions);

        if (enabled.HasValue)
            group.Enabled = enabled.Value;

        return await _repository.UpdateAsync(group);
    }

    public async Task<bool> DeleteGroupAsync(int id)
    {
        return await _repository.DeleteAsync(id);
    }

    public async Task<List<UserGroupEntity>> GetUserGroupsAsync(int userId)
    {
        return await _repository.GetUserGroupsAsync(userId);
    }

    public async Task<bool> AddUserToGroupAsync(int userId, int groupId)
    {
        return await _repository.AddUserToGroupAsync(userId, groupId);
    }

    public async Task<bool> RemoveUserFromGroupAsync(int userId, int groupId)
    {
        return await _repository.RemoveUserFromGroupAsync(userId, groupId);
    }

    public async Task<List<Models.UserEntity>> GetGroupUsersAsync(int groupId)
    {
        var userIds = await _repository.GetGroupUserIdsAsync(groupId);
        var users = new List<Models.UserEntity>();

        foreach (var userId in userIds)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user != null)
                users.Add(user);
        }

        return users;
    }

    public async Task<string[]> GetUserEffectivePermissionsAsync(int userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return Array.Empty<string>();

        var userRoles = user.GetRoles();
        var permissions = new HashSet<string>();

        // Add user's direct roles as permissions
        foreach (var role in userRoles)
        {
            permissions.Add(role);
        }

        // Add permissions from groups
        var groups = await GetUserGroupsAsync(userId);
        foreach (var group in groups.Where(g => g.Enabled))
        {
            var groupPerms = group.GetPermissions();
            foreach (var perm in groupPerms)
            {
                permissions.Add(perm);
            }
        }

        return permissions.ToArray();
    }

    /// <summary>
    /// Add permissions to the Admin group (for new packages)
    /// </summary>
    public async Task<bool> AddPermissionsToAdminGroupAsync(string[] newPermissions)
    {
        try
        {
            // Check for "Administrators" group (preferred) or "Admin" group (legacy)
            var adminGroup = await _repository.GetByNameAsync("Administrators");
            if (adminGroup == null)
            {
                adminGroup = await _repository.GetByNameAsync("Admin");
            }
            
            if (adminGroup == null)
            {
                _logger.LogWarning("Administrators/Admin group not found, cannot add permissions");
                return false;
            }

            var currentPerms = adminGroup.GetPermissions().ToList();
            var permissionsAdded = false;

            foreach (var perm in newPermissions)
            {
                if (!currentPerms.Contains(perm))
                {
                    currentPerms.Add(perm);
                    permissionsAdded = true;
                }
            }

            if (permissionsAdded)
            {
                adminGroup.SetPermissions(currentPerms.ToArray());
                var result = await _repository.UpdateAsync(adminGroup);
                if (result)
                {
                    _logger.LogInformation("Added {Count} new permissions to Admin group", newPermissions.Length);
                }
                return result;
            }

            return true; // No new permissions to add
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding permissions to Admin group");
            return false;
        }
    }
}
