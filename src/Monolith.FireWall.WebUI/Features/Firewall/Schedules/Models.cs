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

public class FirewallScheduleEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string TimeRanges { get; set; } = "[]"; // JSON array
    public int Enabled { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
