using CL.SQLite.Models;

namespace Monolith.FireWall.Common.Models;

/// <summary>
/// Log entry model for application use
/// </summary>
public class LogEntry
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string LogType { get; set; } = ""; // "Monolith", "System", "Security"
    public string Category { get; set; } = ""; // "Auth", "Changes", "Firewall", etc.
    public string Level { get; set; } = "Info"; // "Info", "Warning", "Error", "Critical"
    public string Source { get; set; } = ""; // Package/Module/Service name
    public string Message { get; set; } = "";
    public Dictionary<string, object>? Details { get; set; }
    public int? UserId { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Log entry entity for SQLite storage
/// </summary>
[SQLiteTable("logs")]
[SQLiteIndex(new[] { "LogType", "Timestamp" }, Name = "idx_logs_type_time")]
public class LogEntryEntity
{
    [SQLiteColumn(IsPrimaryKey = true, IsAutoIncrement = true)]
    public int Id { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME, IsNotNull = true)]
    public DateTime Timestamp { get; set; }

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 32)]
    public string LogType { get; set; } = "";

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 64)]
    public string Category { get; set; } = "";

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 16)]
    public string Level { get; set; } = "Info";

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 128)]
    public string Source { get; set; } = "";

    [SQLiteColumn(DataType = SQLiteDataType.TEXT)]
    public string Message { get; set; } = "";

    [SQLiteColumn(DataType = SQLiteDataType.TEXT)]
    public string Details { get; set; } = "{}"; // JSON string

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public int? UserId { get; set; }

    [SQLiteColumn(Size = 64)]
    public string? IpAddress { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Query parameters for log filtering
/// </summary>
public class LogQueryParams
{
    public string? LogType { get; set; }
    public string? Category { get; set; }
    public string? Level { get; set; }
    public string? Source { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int Limit { get; set; } = 100;
    public int Offset { get; set; } = 0;
}

/// <summary>
/// Log query result with pagination
/// </summary>
public class LogQueryResult
{
    public List<LogEntry> Logs { get; set; } = new();
    public int TotalCount { get; set; }
    public int Limit { get; set; }
    public int Offset { get; set; }
}
