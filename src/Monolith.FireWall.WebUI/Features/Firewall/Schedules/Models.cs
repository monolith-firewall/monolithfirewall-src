namespace Monolith.FireWall.WebUI.Features.Firewall.Schedules;

public class ScheduleTimeRange
{
    public string Day { get; set; } = ""; // monday, tuesday, ..., all, weekdays, weekends
    public string StartTime { get; set; } = "00:00"; // HH:mm format
    public string EndTime { get; set; } = "23:59"; // HH:mm format
}

public class FirewallSchedule
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<ScheduleTimeRange> TimeRanges { get; set; } = new();
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

[CL.SQLite.Models.SQLiteTable("firewall_schedules")]
public class FirewallScheduleEntity
{
    [CL.SQLite.Models.SQLiteColumn(IsPrimaryKey = true, IsAutoIncrement = true)]
    public int Id { get; set; }

    [CL.SQLite.Models.SQLiteColumn(IsNotNull = true, DataType = CL.SQLite.Models.SQLiteDataType.TEXT, Size = 128)]
    public string Name { get; set; } = "";

    [CL.SQLite.Models.SQLiteColumn(DataType = CL.SQLite.Models.SQLiteDataType.TEXT, Size = 256)]
    public string Description { get; set; } = "";

    [CL.SQLite.Models.SQLiteColumn(DataType = CL.SQLite.Models.SQLiteDataType.TEXT)]
    public string TimeRanges { get; set; } = "[]"; // JSON array

    [CL.SQLite.Models.SQLiteColumn(DataType = CL.SQLite.Models.SQLiteDataType.BOOLEAN, DefaultValue = "1")]
    public bool Enabled { get; set; } = true;

    [CL.SQLite.Models.SQLiteColumn(DataType = CL.SQLite.Models.SQLiteDataType.DATETIME)]
    public DateTime CreatedAt { get; set; }

    [CL.SQLite.Models.SQLiteColumn(DataType = CL.SQLite.Models.SQLiteDataType.DATETIME)]
    public DateTime UpdatedAt { get; set; }
}
