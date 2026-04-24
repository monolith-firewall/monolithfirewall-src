using Monolith.FireWall.WebUI.Features.Users.Models;
using Monolith.FireWall.WebUI.Features.Users.Repositories;

namespace Monolith.FireWall.WebUI.Features.Users.Services;

public class UserService
{
    private readonly UserRepository _repository;
    private readonly ILogger<UserService> _logger;
    private bool _initialized = false;

    public UserService(UserRepository repository, ILogger<UserService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Initialize user service - syncs table and creates default admin
    /// </summary>
    public async Task InitializeAsync(CL.SQLite.SQLiteLibrary sqlite)
    {
        if (_initialized) return;

        try
        {
            _logger.LogInformation("UserService initializing...");

            // Sync tables
            await sqlite.TableSync!.SyncTableAsync<UserEntity>();
            await sqlite.TableSync!.SyncTableAsync<Models.UserGroupEntity>();
            await sqlite.TableSync!.SyncTableAsync<Models.UserGroupMemberEntity>();
            _logger.LogInformation("User tables synced");

            // Check for admin user
            var admin = await _repository.GetByUsernameAsync("admin");
            UserEntity adminUser;
            bool userCreated = false;
            
            if (admin == null)
            {
                _logger.LogInformation("Creating default admin user...");
                adminUser = new UserEntity
                {
                    Username = "admin",
                    Email = "admin@monolith.local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin"),
                    Enabled = true,
                    Theme = "dark", // Default theme
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                adminUser.SetRoles(new[] { "admin" });

                var userId = await _repository.CreateAsync(adminUser);
                if (userId > 0)
                {
                    adminUser.Id = userId;
                    userCreated = true;
                    _logger.LogInformation("Default admin user created (username: admin, password: admin, id: {UserId})", userId);
                }
                else
                {
                    _logger.LogError("Failed to create admin user - CreateAsync returned invalid ID");
                    throw new Exception("Failed to create admin user");
                }
            }
            else
            {
                adminUser = admin;
                _logger.LogInformation("Admin user already exists (id: {UserId})", adminUser.Id);
                
                // Ensure admin user has roles set
                var currentRoles = adminUser.GetRoles();
                if (currentRoles.Length == 0)
                {
                    adminUser.SetRoles(new[] { "admin" });
                    await _repository.UpdateAsync(adminUser);
                    _logger.LogInformation("Added 'admin' role to existing admin user");
                }
            }
            
            // Ensure Administrators group exists and admin user is in it
            var groupRepo = new Repositories.UserGroupRepository(sqlite);
            await sqlite.TableSync!.SyncTableAsync<Models.UserGroupEntity>();
            await sqlite.TableSync!.SyncTableAsync<Models.UserGroupMemberEntity>();
            
            // Check for "Administrators" group (preferred) or "Admin" group (legacy)
            var adminGroup = await groupRepo.GetByNameAsync("Administrators");
            if (adminGroup == null)
            {
                // Try legacy "Admin" name
                adminGroup = await groupRepo.GetByNameAsync("Admin");
            }
            
            if (adminGroup == null)
            {
                // Create Administrators group
                adminGroup = new Models.UserGroupEntity
                {
                    Name = "Administrators",
                    Description = "Full system administrators with all permissions",
                    Enabled = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                adminGroup.SetPermissions(new[] { "*" }); // All permissions
                var groupId = await groupRepo.CreateAsync(adminGroup);
                
                if (groupId > 0)
                {
                    adminGroup.Id = groupId;
                    var added = await groupRepo.AddUserToGroupAsync(adminUser.Id, groupId);
                    if (added)
                    {
                        _logger.LogInformation("Created Administrators group (id: {GroupId}) and added admin user (id: {UserId})", groupId, adminUser.Id);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to add admin user (id: {UserId}) to Administrators group (id: {GroupId})", adminUser.Id, groupId);
                    }
                }
                else
                {
                    _logger.LogError("Failed to create Administrators group - CreateAsync returned invalid ID");
                }
            }
            else
            {
                // Group exists, ensure admin user is in it
                var userGroupIds = await groupRepo.GetUserGroupIdsAsync(adminUser.Id);
                if (!userGroupIds.Contains(adminGroup.Id))
                {
                    var added = await groupRepo.AddUserToGroupAsync(adminUser.Id, adminGroup.Id);
                    if (added)
                    {
                        _logger.LogInformation("Added admin user (id: {UserId}) to Administrators group (id: {GroupId})", adminUser.Id, adminGroup.Id);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to add admin user (id: {UserId}) to Administrators group (id: {GroupId})", adminUser.Id, adminGroup.Id);
                    }
                }
                else
                {
                    _logger.LogInformation("Admin user (id: {UserId}) is already in Administrators group (id: {GroupId})", adminUser.Id, adminGroup.Id);
                }
            }

            _initialized = true;
            _logger.LogInformation("UserService initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing UserService");
            throw;
        }
    }

    public async Task<List<UserEntity>> GetAllUsersAsync()
    {
        return await _repository.GetAllAsync();
    }

    public async Task<UserEntity?> GetUserByIdAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public async Task<UserEntity?> GetUserByUsernameAsync(string username)
    {
        return await _repository.GetByUsernameAsync(username);
    }

    public async Task<UserEntity> CreateUserAsync(string username, string email, string password, string[] roles)
    {
        var user = new UserEntity
        {
            Username = username,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Enabled = true,
            Theme = "dark", // Default theme
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        user.SetRoles(roles);

        var id = await _repository.CreateAsync(user);
        user.Id = id;
        return user;
    }

    public async Task<bool> UpdateUserAsync(UserEntity user)
    {
        user.UpdatedAt = DateTime.UtcNow;
        return await _repository.UpdateAsync(user);
    }

    public async Task<bool> DeleteUserAsync(int id)
    {
        return await _repository.DeleteAsync(id);
    }

    public async Task<UserEntity?> ValidateLoginAsync(string username, string password)
    {
        var user = await _repository.GetByUsernameAsync(username);
        if (user == null || !user.Enabled)
            return null;

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return null;

        return user;
    }

    /// <summary>
    /// Get user's theme preference
    /// </summary>
    public async Task<string> GetUserThemeAsync(int userId)
    {
        var user = await _repository.GetByIdAsync(userId);
        if (user == null)
            return "dark"; // Default
        
        return string.IsNullOrEmpty(user.Theme) ? "dark" : user.Theme;
    }

    /// <summary>
    /// Update user's theme preference
    /// </summary>
    public async Task<bool> UpdateUserThemeAsync(int userId, string theme)
    {
        // Validate theme value
        if (theme != "light" && theme != "dark" && theme != "auto")
        {
            throw new ArgumentException("Theme must be 'light', 'dark', or 'auto'");
        }

        var user = await _repository.GetByIdAsync(userId);
        if (user == null)
            return false;

        user.Theme = theme;
        user.UpdatedAt = DateTime.UtcNow;
        return await _repository.UpdateAsync(user);
    }
}
