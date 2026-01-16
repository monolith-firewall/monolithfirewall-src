using CL.SQLite.Models;
using System.Text.Json;

namespace Monolith.FireWall.WebUI.Features.Users.Models;

/// <summary>
/// User database model with CL.SQLite attributes
/// </summary>
[SQLiteTable("users")]
[SQLiteIndex(new[] { "username" }, IsUnique = true, Name = "idx_users_username")]
[SQLiteIndex(new[] { "email" }, Name = "idx_users_email")]
public class UserEntity
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
    public string Theme { get; set; } = "dark"; // "light", "dark", or "auto"

    /// <summary>
    /// Get roles as array
    /// </summary>
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

    /// <summary>
    /// Set roles
    /// </summary>
    public void SetRoles(string[] roles)
    {
        RolesJson = JsonSerializer.Serialize(roles);
    }
}
