using CL.SQLite.Models;
using System.Text.Json;

namespace Monolith.FireWall.Core.Models;

// ─────────────────────────────────────────────────────────────
// Database Entities (must match WebUI column names exactly)
// ─────────────────────────────────────────────────────────────

[SQLiteTable("users")]
[SQLiteIndex(new[] { "username" }, IsUnique = true, Name = "idx_users_username")]
[SQLiteIndex(new[] { "email" }, Name = "idx_users_email")]
public sealed class UserEntity
{
    [SQLiteColumn(IsPrimaryKey = true, IsAutoIncrement = true)]
    public int Id { get; set; }

    [SQLiteColumn(IsNotNull = true, IsUnique = true, Size = 100)]
    public string Username { get; set; } = string.Empty;

    [SQLiteColumn(IsNotNull = true, Size = 255)]
    public string Email { get; set; } = string.Empty;

    [SQLiteColumn(IsNotNull = true, ColumnName = "password_hash", Size = 255)]
    public string PasswordHash { get; set; } = string.Empty;

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, ColumnName = "roles")]
    public string RolesJson { get; set; } = "[]";

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, ColumnName = "dashboard_layout")]
    public string? DashboardLayoutJson { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.BOOLEAN, DefaultValue = "1")]
    public bool Enabled { get; set; } = true;

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME, ColumnName = "created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME, ColumnName = "updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [SQLiteColumn(Size = 10, DefaultValue = "'dark'")]
    public string Theme { get; set; } = "dark";

    public string[] GetRoles()
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(RolesJson) ?? Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public void SetRoles(string[] roles)
    {
        RolesJson = JsonSerializer.Serialize(roles);
    }
}

[SQLiteTable("user_groups")]
[SQLiteIndex(new[] { "name" }, IsUnique = true, Name = "idx_user_groups_name")]
public sealed class UserGroupEntity
{
    [SQLiteColumn(IsPrimaryKey = true, IsAutoIncrement = true)]
    public int Id { get; set; }

    [SQLiteColumn(IsNotNull = true, IsUnique = true, Size = 100)]
    public string Name { get; set; } = string.Empty;

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 500)]
    public string? Description { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, ColumnName = "permissions", IsNotNull = true)]
    public string PermissionsJson { get; set; } = "[]";

    [SQLiteColumn(DataType = SQLiteDataType.BOOLEAN, DefaultValue = "1")]
    public bool Enabled { get; set; } = true;

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME, ColumnName = "created_at", IsNotNull = true)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME, ColumnName = "updated_at", IsNotNull = true)]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public string[] GetPermissions()
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(PermissionsJson) ?? Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public void SetPermissions(string[] permissions)
    {
        PermissionsJson = JsonSerializer.Serialize(permissions);
    }
}

[SQLiteTable("user_group_members")]
[SQLiteIndex(new[] { "user_id", "group_id" }, IsUnique = true, Name = "idx_user_group_members_unique")]
[SQLiteIndex(new[] { "user_id" }, Name = "idx_user_group_members_user")]
[SQLiteIndex(new[] { "group_id" }, Name = "idx_user_group_members_group")]
public sealed class UserGroupMemberEntity
{
    [SQLiteColumn(IsPrimaryKey = true, IsAutoIncrement = true)]
    public int Id { get; set; }

    [SQLiteColumn(ColumnName = "user_id", IsNotNull = true)]
    public int UserId { get; set; }

    [SQLiteColumn(ColumnName = "group_id", IsNotNull = true)]
    public int GroupId { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME, ColumnName = "created_at", IsNotNull = true)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ─────────────────────────────────────────────────────────────
// View Models (returned by API)
// ─────────────────────────────────────────────────────────────

public sealed class UserView
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string[] Roles { get; set; } = Array.Empty<string>();
    public string Theme { get; set; } = "dark";
    public List<int> GroupIds { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class UserGroupView
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string[] Permissions { get; set; } = Array.Empty<string>();
    public bool Enabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// ─────────────────────────────────────────────────────────────
// Request Models (received from API)
// ─────────────────────────────────────────────────────────────

public sealed class UserCreateRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string[]? Roles { get; set; }
}

public sealed class UserUpdateRequest
{
    public int Id { get; set; }
    public string? Email { get; set; }
    public string? Password { get; set; }
    public string[]? Roles { get; set; }
    public bool? Enabled { get; set; }
}

public sealed class UserIdRequest
{
    public int Id { get; set; }
}

public sealed class UserLoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class UserLoginResponse
{
    public bool Success { get; set; }
    public UserView? User { get; set; }
    public string[]? Permissions { get; set; }
    public string? Error { get; set; }
}

public sealed class UserThemeRequest
{
    public int UserId { get; set; }
    public string Theme { get; set; } = "dark";
}

public sealed class UserPasswordChangeRequest
{
    public int UserId { get; set; }
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public sealed class UserGroupCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string[]? Permissions { get; set; }
}

public sealed class UserGroupUpdateRequest
{
    public int Id { get; set; }
    public string? Description { get; set; }
    public string[]? Permissions { get; set; }
    public bool? Enabled { get; set; }
}

public sealed class UserGroupIdRequest
{
    public int Id { get; set; }
}

public sealed class UserGroupMemberRequest
{
    public int UserId { get; set; }
    public int GroupId { get; set; }
}

public sealed class UserPermissionsRequest
{
    public int UserId { get; set; }
}
