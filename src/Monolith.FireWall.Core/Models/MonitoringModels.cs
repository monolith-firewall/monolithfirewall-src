using CL.SQLite.Models;

namespace Monolith.FireWall.Core.Models;

[SQLiteTable("monitor_definitions")]
public sealed class MonitorDefinitionEntity
{
    [SQLiteColumn(IsPrimaryKey = true, IsAutoIncrement = true)]
    public int Id { get; set; }

    [SQLiteColumn(IsNotNull = true, IsUnique = true, DataType = SQLiteDataType.TEXT, Size = 64)]
    public string Key { get; set; } = string.Empty;

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 128)]
    public string Name { get; set; } = string.Empty;

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 64)]
    public string Type { get; set; } = string.Empty;

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public int IntervalSeconds { get; set; } = 60;

    [SQLiteColumn(DataType = SQLiteDataType.BOOLEAN, DefaultValue = "1")]
    public bool Enabled { get; set; } = true;

    [SQLiteColumn(DataType = SQLiteDataType.TEXT)]
    public string? ConfigJson { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime CreatedAt { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime UpdatedAt { get; set; }
}

[SQLiteTable("monitor_status")]
public sealed class MonitorStatusEntity
{
    [SQLiteColumn(IsPrimaryKey = true, IsAutoIncrement = true)]
    public int Id { get; set; }

    [SQLiteColumn(IsNotNull = true, IsUnique = true, DataType = SQLiteDataType.TEXT, Size = 64)]
    public string MonitorKey { get; set; } = string.Empty;

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 16)]
    public string Status { get; set; } = "unknown";

    [SQLiteColumn(DataType = SQLiteDataType.TEXT)]
    public string? Message { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime? LastCheckAt { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime? LastSuccessAt { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime? LastFailureAt { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public int? LastDurationMs { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public int? LastLatencyMs { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public int ConsecutiveFailures { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime UpdatedAt { get; set; }
}

[SQLiteTable("system_notifications")]
public sealed class SystemNotificationEntity
{
    [SQLiteColumn(IsPrimaryKey = true, IsAutoIncrement = true)]
    public int Id { get; set; }

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 32)]
    public string Type { get; set; } = "monitor";

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 16)]
    public string Severity { get; set; } = "info";

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 160)]
    public string Title { get; set; } = string.Empty;

    [SQLiteColumn(DataType = SQLiteDataType.TEXT)]
    public string? Message { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 64)]
    public string? MonitorKey { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.TEXT)]
    public string? DetailsJson { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime CreatedAt { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime? ReadAt { get; set; }
}

public sealed class MonitorStatusView
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public int IntervalSeconds { get; set; }
    public string Status { get; set; } = "unknown";
    public string? Message { get; set; }
    public DateTime? LastCheckAt { get; set; }
    public DateTime? LastSuccessAt { get; set; }
    public DateTime? LastFailureAt { get; set; }
    public int? LastDurationMs { get; set; }
    public int? LastLatencyMs { get; set; }
    public int ConsecutiveFailures { get; set; }
}

public sealed class NotificationView
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? MonitorKey { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}

public sealed class NotificationSummaryView
{
    public List<NotificationView> Notifications { get; set; } = new();
    public int UnreadCount { get; set; }
}

public sealed class MonitorUpdateRequest
{
    public string Key { get; set; } = string.Empty;
    public bool? Enabled { get; set; }
    public int? IntervalSeconds { get; set; }
    public string? ConfigJson { get; set; }
}

public sealed class NotificationReadRequest
{
    public List<int> Ids { get; set; } = new();
    public bool All { get; set; }
}

public sealed class NotificationQuery
{
    public int Limit { get; set; } = 20;
    public bool UnreadOnly { get; set; }
}
