using CL.SQLite.Models;

namespace Monolith.FireWall.Core.Models;

/// <summary>
/// Setup state entity - tracks if this is a fresh installation
/// Singleton record (always ID 1)
/// </summary>
[SQLiteTable("setup_state")]
public class SetupStateEntity
{
    [SQLiteColumn(IsPrimaryKey = true)]
    public int Id { get; set; } = 1; // Always use ID 1 (singleton)

    [SQLiteColumn(DataType = CL.SQLite.Models.SQLiteDataType.BOOLEAN, DefaultValue = "1")]
    public bool IsFreshInstall { get; set; } = true;

    [SQLiteColumn(DataType = CL.SQLite.Models.SQLiteDataType.BOOLEAN, DefaultValue = "0")]
    public bool SetupCompleted { get; set; } = false;

    [SQLiteColumn(DataType = CL.SQLite.Models.SQLiteDataType.DATETIME)]
    public DateTime? FirstRunAt { get; set; }

    [SQLiteColumn(DataType = CL.SQLite.Models.SQLiteDataType.DATETIME)]
    public DateTime? SetupCompletedAt { get; set; }

    [SQLiteColumn(DataType = CL.SQLite.Models.SQLiteDataType.DATETIME)]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
