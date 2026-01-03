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
            await sqlite.TableSyncService!.SyncTableAsync<UserEntity>();
            await sqlite.TableSyncService!.SyncTableAsync<Models.UserGroupEntity>();
            await sqlite.TableSyncService!.SyncTableAsync<Models.UserGroupMemberEntity>();
            _logger.LogInformation("User tables synced");

            // Check for admin user
            var admin = await _repository.GetByUsernameAsync("admin");
            if (admin == null)
            {
                _logger.LogInformation("Creating default admin user...");
                var adminUser = new UserEntity
                {
                    Username = "admin",
                    Email = "admin@monolith.local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin"),
                    Enabled = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                adminUser.SetRoles(new[] { "admin" });

                await _repository.CreateAsync(adminUser);
                _logger.LogInformation("Default admin user created (username: admin, password: admin)");
                
                // Create default admin group
                var groupRepo = new Repositories.UserGroupRepository(sqlite);
                await sqlite.TableSyncService!.SyncTableAsync<Models.UserGroupEntity>();
                await sqlite.TableSyncService!.SyncTableAsync<Models.UserGroupMemberEntity>();
                
                var adminGroup = await groupRepo.GetByNameAsync("Administrators");
                if (adminGroup == null)
                {
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
                    
                    // Add admin user to Administrators group
                    if (groupId > 0)
                    {
                        await groupRepo.AddUserToGroupAsync(adminUser.Id, groupId);
                        _logger.LogInformation("Created Administrators group and added admin user");
                    }
                }
            }
            else
            {
                _logger.LogInformation("Admin user already exists");
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
}
