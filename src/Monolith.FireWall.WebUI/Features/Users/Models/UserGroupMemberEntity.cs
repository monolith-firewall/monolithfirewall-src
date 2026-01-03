using CL.SQLite.Models;

namespace Monolith.FireWall.WebUI.Features.Users.Models;

/// <summary>
/// User-Group membership relationship
/// </summary>
[SQLiteTable("user_group_members")]
[SQLiteIndex(new[] { "user_id", "group_id" }, IsUnique = true, Name = "idx_user_group_members_unique")]
[SQLiteIndex(new[] { "user_id" }, Name = "idx_user_group_members_user")]
[SQLiteIndex(new[] { "group_id" }, Name = "idx_user_group_members_group")]
public class UserGroupMemberEntity
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
