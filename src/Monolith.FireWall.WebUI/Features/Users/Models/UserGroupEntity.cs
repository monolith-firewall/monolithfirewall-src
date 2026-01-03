using CL.SQLite.Models;
using System.Text.Json;

namespace Monolith.FireWall.WebUI.Features.Users.Models;

/// <summary>
/// User group database model with CL.SQLite
/// </summary>
[SQLiteTable("user_groups")]
[SQLiteIndex(new[] { "name" }, IsUnique = true, Name = "idx_user_groups_name")]
public class UserGroupEntity
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

    /// <summary>
    /// Get permissions as array
    /// </summary>
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

    /// <summary>
    /// Set permissions
    /// </summary>
    public void SetPermissions(string[] permissions)
    {
        PermissionsJson = JsonSerializer.Serialize(permissions);
    }
}
