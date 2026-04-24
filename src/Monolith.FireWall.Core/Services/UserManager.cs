using CodeLogic;
using CL.SQLite.Services;
using Monolith.FireWall.Common.Services;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Services;

public sealed class UserManager
{
    private readonly LoggingManager _loggingManager;
    private CL.SQLite.SQLiteLibrary? _sqlite;
    private Repository<UserEntity>? _userRepository;
    private QueryBuilder<UserEntity>? _userQuery;
    private Repository<UserGroupEntity>? _groupRepository;
    private QueryBuilder<UserGroupEntity>? _groupQuery;
    private Repository<UserGroupMemberEntity>? _memberRepository;
    private QueryBuilder<UserGroupMemberEntity>? _memberQuery;

    public UserManager()
    {
        _loggingManager = LoggingManager.Instance;
        Initialize();
    }

    // ─────────────────────────────────────────────────────────
    // Initialization
    // ─────────────────────────────────────────────────────────

    private void Initialize()
    {
        try
        {
            var sqlite = CodeLogic.Libraries.Get<CL.SQLite.SQLiteLibrary>();
            if (sqlite == null) return;

            _sqlite = sqlite;
            _userRepository = sqlite.GetRepository<UserEntity>();
            _userQuery = sqlite.GetQueryBuilder<UserEntity>();
            _groupRepository = sqlite.GetRepository<UserGroupEntity>();
            _groupQuery = sqlite.GetQueryBuilder<UserGroupEntity>();
            _memberRepository = sqlite.GetRepository<UserGroupMemberEntity>();
            _memberQuery = sqlite.GetQueryBuilder<UserGroupMemberEntity>();
        }
        catch
        {
            _sqlite = null;
        }
    }

    /// <summary>
    /// Creates default admin user and Administrators group if they don't already exist.
    /// Called after table sync during startup.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_userRepository == null || _groupRepository == null) return;

        try
        {
            // Create default Administrators group if not exists
            var existingGroup = await GetGroupByNameAsync("Administrators");
            int adminGroupId;
            if (existingGroup == null)
            {
                var groupEntity = new UserGroupEntity
                {
                    Name = "Administrators",
                    Description = "Full system access",
                    PermissionsJson = "[\"*\"]",
                    Enabled = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                var groupInsert = await _groupRepository.InsertAsync(groupEntity);
                adminGroupId = groupInsert.IsSuccess ? (int)groupInsert.Value : 0;

                if (adminGroupId > 0)
                {
                    Console.WriteLine("  ✓ Created default Administrators group");
                }
            }
            else
            {
                adminGroupId = existingGroup.Id;
            }

            // Create default admin user if not exists
            var existingAdmin = await GetUserByUsernameInternalAsync("admin");
            if (existingAdmin == null)
            {
                var passwordHash = BCrypt.Net.BCrypt.HashPassword("monolith");
                var adminUser = new UserEntity
                {
                    Username = "admin",
                    Email = "admin@localhost",
                    PasswordHash = passwordHash,
                    RolesJson = "[\"admin\"]",
                    Enabled = true,
                    Theme = "dark",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var userInsert = await _userRepository.InsertAsync(adminUser);
                if (userInsert.IsSuccess && userInsert.Value > 0)
                {
                    adminUser.Id = (int)userInsert.Value;
                    Console.WriteLine("  ✓ Created default admin user (password: monolith)");

                    // Add admin to Administrators group
                    if (adminGroupId > 0)
                    {
                        await AddUserToGroupAsync(adminUser.Id, adminGroupId);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ Failed to initialize default user/group: {ex.Message}");
        }
    }

    // ─────────────────────────────────────────────────────────
    // User CRUD
    // ─────────────────────────────────────────────────────────

    public async Task<List<UserView>> ListUsersAsync()
    {
        if (_userRepository == null) return new List<UserView>();

        var result = await _userRepository.GetAllAsync();
        if (!result.IsSuccess || result.Value == null) return new List<UserView>();

        var views = new List<UserView>();
        foreach (var entity in result.Value)
        {
            views.Add(await BuildUserViewAsync(entity));
        }
        return views;
    }

    public async Task<UserView?> GetUserAsync(int id)
    {
        var entity = await GetUserEntityAsync(id);
        return entity == null ? null : await BuildUserViewAsync(entity);
    }

    public async Task<UserEntity?> GetUserByUsernameAsync(string username)
    {
        return await GetUserByUsernameInternalAsync(username);
    }

    public async Task<(bool Success, string? Error, UserView? User)> CreateUserAsync(UserCreateRequest request)
    {
        if (_userRepository == null) return (false, "User storage not available", null);

        if (string.IsNullOrWhiteSpace(request.Username))
            return (false, "Username is required", null);

        if (string.IsNullOrWhiteSpace(request.Password))
            return (false, "Password is required", null);

        if (request.Password.Length < 6)
            return (false, "Password must be at least 6 characters", null);

        // Check for duplicate username
        var existing = await GetUserByUsernameInternalAsync(request.Username.Trim());
        if (existing != null)
            return (false, "Username already exists", null);

        var now = DateTime.UtcNow;
        var entity = new UserEntity
        {
            Username = request.Username.Trim(),
            Email = request.Email?.Trim() ?? string.Empty,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Enabled = true,
            Theme = "dark",
            CreatedAt = now,
            UpdatedAt = now
        };
        entity.SetRoles(request.Roles ?? Array.Empty<string>());

        var insert = await _userRepository.InsertAsync(entity);
        if (!insert.IsSuccess || insert.Value <= 0)
            return (false, "Failed to create user", null);

        entity.Id = (int)insert.Value;

        await _loggingManager.LogMonolithAsync(
            "User", "Info", "UserManagement",
            $"Created user '{entity.Username}'",
            details: new Dictionary<string, object>
            {
                ["userId"] = entity.Id,
                ["username"] = entity.Username
            });

        return (true, null, await BuildUserViewAsync(entity));
    }

    public async Task<(bool Success, string? Error, UserView? User)> UpdateUserAsync(UserUpdateRequest request)
    {
        if (_userRepository == null) return (false, "User storage not available", null);

        var entity = await GetUserEntityAsync(request.Id);
        if (entity == null) return (false, "User not found", null);

        if (request.Email != null)
            entity.Email = request.Email.Trim();

        if (request.Password != null)
        {
            if (request.Password.Length < 6)
                return (false, "Password must be at least 6 characters", null);
            entity.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        }

        if (request.Roles != null)
            entity.SetRoles(request.Roles);

        if (request.Enabled.HasValue)
            entity.Enabled = request.Enabled.Value;

        entity.UpdatedAt = DateTime.UtcNow;

        var update = await _userRepository.UpdateAsync(entity);
        if (!update.IsSuccess)
            return (false, "Failed to update user", null);

        await _loggingManager.LogMonolithAsync(
            "User", "Info", "UserManagement",
            $"Updated user '{entity.Username}'",
            details: new Dictionary<string, object>
            {
                ["userId"] = entity.Id,
                ["username"] = entity.Username
            });

        return (true, null, await BuildUserViewAsync(entity));
    }

    public async Task<bool> DeleteUserAsync(int id)
    {
        if (_userRepository == null) return false;

        var entity = await GetUserEntityAsync(id);
        if (entity == null) return false;

        // Prevent deleting the last admin
        if (entity.GetRoles().Contains("admin"))
        {
            var allUsers = await _userRepository.GetAllAsync();
            if (allUsers.IsSuccess && allUsers.Value != null)
            {
                var adminCount = allUsers.Value.Count(u => u.GetRoles().Contains("admin"));
                if (adminCount <= 1) return false;
            }
        }

        // Remove from all groups first
        await RemoveUserFromAllGroupsAsync(id);

        var result = await _userRepository.DeleteAsync(id);
        if (result.IsSuccess)
        {
            await _loggingManager.LogMonolithAsync(
                "User", "Info", "UserManagement",
                $"Deleted user '{entity.Username}'",
                details: new Dictionary<string, object>
                {
                    ["userId"] = entity.Id,
                    ["username"] = entity.Username
                });
        }

        return result.IsSuccess;
    }

    // ─────────────────────────────────────────────────────────
    // Authentication
    // ─────────────────────────────────────────────────────────

    public async Task<(bool Success, UserView? User, string[]? Permissions)> ValidateLoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return (false, null, null);

        var entity = await GetUserByUsernameInternalAsync(username);
        if (entity == null) return (false, null, null);

        if (!entity.Enabled) return (false, null, null);

        if (!BCrypt.Net.BCrypt.Verify(password, entity.PasswordHash))
            return (false, null, null);

        var permissions = await GetUserEffectivePermissionsAsync(entity.Id);
        var view = await BuildUserViewAsync(entity);

        await _loggingManager.LogMonolithAsync(
            "Auth", "Info", "UserManagement",
            $"User '{entity.Username}' logged in",
            userId: entity.Id);

        return (true, view, permissions);
    }

    // ─────────────────────────────────────────────────────────
    // Theme & Password
    // ─────────────────────────────────────────────────────────

    public async Task<string> GetUserThemeAsync(int userId)
    {
        var entity = await GetUserEntityAsync(userId);
        return entity?.Theme ?? "dark";
    }

    public async Task<bool> UpdateUserThemeAsync(int userId, string theme)
    {
        if (_userRepository == null) return false;

        var entity = await GetUserEntityAsync(userId);
        if (entity == null) return false;

        // Validate theme
        if (theme != "dark" && theme != "light" && theme != "auto")
            return false;

        entity.Theme = theme;
        entity.UpdatedAt = DateTime.UtcNow;

        var result = await _userRepository.UpdateAsync(entity);
        return result.IsSuccess;
    }

    public async Task<(bool Success, string? Error)> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
    {
        if (_userRepository == null) return (false, "User storage not available");

        var entity = await GetUserEntityAsync(userId);
        if (entity == null) return (false, "User not found");

        if (!BCrypt.Net.BCrypt.Verify(currentPassword, entity.PasswordHash))
            return (false, "Current password is incorrect");

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            return (false, "New password must be at least 6 characters");

        entity.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        entity.UpdatedAt = DateTime.UtcNow;

        var result = await _userRepository.UpdateAsync(entity);
        if (!result.IsSuccess)
            return (false, "Failed to update password");

        await _loggingManager.LogMonolithAsync(
            "User", "Info", "UserManagement",
            $"Password changed for user '{entity.Username}'",
            userId: entity.Id);

        return (true, null);
    }

    // ─────────────────────────────────────────────────────────
    // Group CRUD
    // ─────────────────────────────────────────────────────────

    public async Task<List<UserGroupView>> ListGroupsAsync()
    {
        if (_groupRepository == null) return new List<UserGroupView>();

        var result = await _groupRepository.GetAllAsync();
        if (!result.IsSuccess || result.Value == null) return new List<UserGroupView>();

        return result.Value.Select(BuildGroupView).ToList();
    }

    public async Task<UserGroupView?> GetGroupAsync(int id)
    {
        var entity = await GetGroupEntityAsync(id);
        return entity == null ? null : BuildGroupView(entity);
    }

    public async Task<(bool Success, string? Error, UserGroupView? Group)> CreateGroupAsync(UserGroupCreateRequest request)
    {
        if (_groupRepository == null) return (false, "Group storage not available", null);

        if (string.IsNullOrWhiteSpace(request.Name))
            return (false, "Group name is required", null);

        // Check for duplicate name
        var existing = await GetGroupByNameAsync(request.Name.Trim());
        if (existing != null)
            return (false, "Group name already exists", null);

        var now = DateTime.UtcNow;
        var entity = new UserGroupEntity
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Enabled = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        entity.SetPermissions(request.Permissions ?? Array.Empty<string>());

        var insert = await _groupRepository.InsertAsync(entity);
        if (!insert.IsSuccess || insert.Value <= 0)
            return (false, "Failed to create group", null);

        entity.Id = (int)insert.Value;

        await _loggingManager.LogMonolithAsync(
            "User", "Info", "UserManagement",
            $"Created group '{entity.Name}'",
            details: new Dictionary<string, object>
            {
                ["groupId"] = entity.Id,
                ["groupName"] = entity.Name
            });

        return (true, null, BuildGroupView(entity));
    }

    public async Task<(bool Success, string? Error, UserGroupView? Group)> UpdateGroupAsync(UserGroupUpdateRequest request)
    {
        if (_groupRepository == null) return (false, "Group storage not available", null);

        var entity = await GetGroupEntityAsync(request.Id);
        if (entity == null) return (false, "Group not found", null);

        if (request.Description != null)
            entity.Description = request.Description.Trim();

        if (request.Permissions != null)
            entity.SetPermissions(request.Permissions);

        if (request.Enabled.HasValue)
            entity.Enabled = request.Enabled.Value;

        entity.UpdatedAt = DateTime.UtcNow;

        var update = await _groupRepository.UpdateAsync(entity);
        if (!update.IsSuccess)
            return (false, "Failed to update group", null);

        await _loggingManager.LogMonolithAsync(
            "User", "Info", "UserManagement",
            $"Updated group '{entity.Name}'",
            details: new Dictionary<string, object>
            {
                ["groupId"] = entity.Id,
                ["groupName"] = entity.Name
            });

        return (true, null, BuildGroupView(entity));
    }

    public async Task<bool> DeleteGroupAsync(int id)
    {
        if (_groupRepository == null) return false;

        var entity = await GetGroupEntityAsync(id);
        if (entity == null) return false;

        // Remove all members first
        await RemoveAllGroupMembersAsync(id);

        var result = await _groupRepository.DeleteAsync(id);
        if (result.IsSuccess)
        {
            await _loggingManager.LogMonolithAsync(
                "User", "Info", "UserManagement",
                $"Deleted group '{entity.Name}'",
                details: new Dictionary<string, object>
                {
                    ["groupId"] = entity.Id,
                    ["groupName"] = entity.Name
                });
        }

        return result.IsSuccess;
    }

    // ─────────────────────────────────────────────────────────
    // Group Membership
    // ─────────────────────────────────────────────────────────

    public async Task<List<UserView>> GetGroupUsersAsync(int groupId)
    {
        if (_memberRepository == null) return new List<UserView>();

        var allMembers = await _memberRepository.GetAllAsync();
        if (!allMembers.IsSuccess || allMembers.Value == null) return new List<UserView>();

        var userIds = allMembers.Value
            .Where(m => m.GroupId == groupId)
            .Select(m => m.UserId)
            .ToList();

        var views = new List<UserView>();
        foreach (var userId in userIds)
        {
            var user = await GetUserAsync(userId);
            if (user != null) views.Add(user);
        }
        return views;
    }

    public async Task<bool> AddUserToGroupAsync(int userId, int groupId)
    {
        if (_memberRepository == null || _memberQuery == null) return false;

        // Check if already a member
        var existing = await _memberQuery
            .Where(m => m.UserId == userId && m.GroupId == groupId)
            .FirstOrDefaultAsync();

        if (existing.IsSuccess && existing.Value != null)
            return true; // Already a member

        var member = new UserGroupMemberEntity
        {
            UserId = userId,
            GroupId = groupId,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _memberRepository.InsertAsync(member);
        return result.IsSuccess;
    }

    public async Task<bool> RemoveUserFromGroupAsync(int userId, int groupId)
    {
        if (_memberRepository == null || _memberQuery == null) return false;

        var result = await _memberQuery
            .Where(m => m.UserId == userId && m.GroupId == groupId)
            .FirstOrDefaultAsync();

        if (result.IsSuccess && result.Value != null)
        {
            var deleteResult = await _memberRepository.DeleteAsync(result.Value.Id);
            return deleteResult.IsSuccess;
        }

        return false;
    }

    public async Task<string[]> GetUserEffectivePermissionsAsync(int userId)
    {
        var entity = await GetUserEntityAsync(userId);
        if (entity == null) return Array.Empty<string>();

        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // If user has admin role, grant wildcard
        var roles = entity.GetRoles();
        if (roles.Contains("admin"))
        {
            return new[] { "*" };
        }

        // Aggregate permissions from all enabled groups the user belongs to
        var groupIds = await GetUserGroupIdsAsync(userId);
        foreach (var groupId in groupIds)
        {
            var group = await GetGroupEntityAsync(groupId);
            if (group == null || !group.Enabled) continue;

            var groupPerms = group.GetPermissions();
            if (groupPerms.Contains("*"))
                return new[] { "*" };

            foreach (var perm in groupPerms)
            {
                permissions.Add(perm);
            }
        }

        return permissions.ToArray();
    }

    public async Task<List<int>> GetUserGroupIdsAsync(int userId)
    {
        if (_memberRepository == null) return new List<int>();

        var allMembers = await _memberRepository.GetAllAsync();
        if (!allMembers.IsSuccess || allMembers.Value == null) return new List<int>();

        return allMembers.Value
            .Where(m => m.UserId == userId)
            .Select(m => m.GroupId)
            .ToList();
    }

    // ─────────────────────────────────────────────────────────
    // Internal Helpers
    // ─────────────────────────────────────────────────────────

    private async Task<UserEntity?> GetUserEntityAsync(int id)
    {
        if (_userRepository == null) return null;
        var result = await _userRepository.GetByIdAsync(id);
        return result.IsSuccess ? result.Value : null;
    }

    private async Task<UserEntity?> GetUserByUsernameInternalAsync(string username)
    {
        if (_userQuery == null) return null;
        var result = await _userQuery
            .Where(u => u.Username == username)
            .FirstOrDefaultAsync();
        return result.IsSuccess ? result.Value : null;
    }

    private async Task<UserGroupEntity?> GetGroupEntityAsync(int id)
    {
        if (_groupRepository == null) return null;
        var result = await _groupRepository.GetByIdAsync(id);
        return result.IsSuccess ? result.Value : null;
    }

    private async Task<UserGroupEntity?> GetGroupByNameAsync(string name)
    {
        if (_groupQuery == null) return null;
        var result = await _groupQuery
            .Where(g => g.Name == name)
            .FirstOrDefaultAsync();
        return result.IsSuccess ? result.Value : null;
    }

    private async Task RemoveUserFromAllGroupsAsync(int userId)
    {
        if (_memberRepository == null) return;
        var allMembers = await _memberRepository.GetAllAsync();
        if (!allMembers.IsSuccess || allMembers.Value == null) return;

        foreach (var member in allMembers.Value.Where(m => m.UserId == userId))
        {
            await _memberRepository.DeleteAsync(member.Id);
        }
    }

    private async Task RemoveAllGroupMembersAsync(int groupId)
    {
        if (_memberRepository == null) return;
        var allMembers = await _memberRepository.GetAllAsync();
        if (!allMembers.IsSuccess || allMembers.Value == null) return;

        foreach (var member in allMembers.Value.Where(m => m.GroupId == groupId))
        {
            await _memberRepository.DeleteAsync(member.Id);
        }
    }

    private async Task<UserView> BuildUserViewAsync(UserEntity entity)
    {
        var groupIds = await GetUserGroupIdsAsync(entity.Id);
        return new UserView
        {
            Id = entity.Id,
            Username = entity.Username,
            Email = entity.Email,
            Enabled = entity.Enabled,
            Roles = entity.GetRoles(),
            Theme = entity.Theme,
            GroupIds = groupIds,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    private static UserGroupView BuildGroupView(UserGroupEntity entity)
    {
        return new UserGroupView
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description ?? string.Empty,
            Permissions = entity.GetPermissions(),
            Enabled = entity.Enabled,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
