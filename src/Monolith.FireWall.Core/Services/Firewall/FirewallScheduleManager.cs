using CodeLogic;
using CL.SQLite.Services;

namespace Monolith.FireWall.Core.Services.Firewall;

/// <summary>
/// Manages firewall schedules and determines if rules should be active based on time
/// </summary>
public sealed class FirewallScheduleManager
{
    private CL.SQLite.SQLiteLibrary? _sqlite;
    private Repository<FirewallScheduleEntity>? _repository;

    public FirewallScheduleManager()
    {
        Initialize();
    }

    /// <summary>
    /// Check if a schedule is currently active based on the current time
    /// </summary>
    public async Task<bool> IsScheduleActiveAsync(int scheduleId, DateTime? currentTime = null)
    {
        if (_repository == null)
        {
            // If database is not available, assume schedule is active
            return true;
        }

        var time = currentTime ?? DateTime.Now;
        var schedule = await GetScheduleByIdAsync(scheduleId);

        if (schedule == null || !schedule.Enabled)
        {
            // If schedule doesn't exist or is disabled, treat as always active
            return true;
        }

        return IsTimeInSchedule(schedule, time);
    }

    /// <summary>
    /// Get a schedule by ID
    /// </summary>
    public async Task<FirewallScheduleEntity?> GetScheduleByIdAsync(int scheduleId)
    {
        if (_repository == null)
        {
            return null;
        }

        var result = await _repository.GetAllAsync();
        if (!result.IsSuccess || result.Data == null)
        {
            return null;
        }

        return result.Data.FirstOrDefault(s => s.Id == scheduleId);
    }

    /// <summary>
    /// Check if a given time falls within the schedule's time ranges
    /// </summary>
    private bool IsTimeInSchedule(FirewallScheduleEntity schedule, DateTime time)
    {
        if (string.IsNullOrWhiteSpace(schedule.TimeRanges))
        {
            // No time ranges defined - always active
            return true;
        }

        try
        {
            var ranges = System.Text.Json.JsonSerializer.Deserialize<List<TimeRange>>(schedule.TimeRanges);
            if (ranges == null || ranges.Count == 0)
            {
                // No time ranges - always active
                return true;
            }

            var currentDayOfWeek = GetDayOfWeekString(time.DayOfWeek);
            var currentTime = time.TimeOfDay;

            // Check if current day and time matches any range
            foreach (var range in ranges)
            {
                // Check if the day matches (case-insensitive)
                if (!string.Equals(range.Day, currentDayOfWeek, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Parse start and end times
                if (!TimeSpan.TryParse(range.StartTime, out var startTime) ||
                    !TimeSpan.TryParse(range.EndTime, out var endTime))
                {
                    continue;
                }

                // Check if current time is within the range
                if (currentTime >= startTime && currentTime <= endTime)
                {
                    return true;
                }
            }

            // No matching time range found
            return false;
        }
        catch
        {
            // If parsing fails, assume schedule is active
            return true;
        }
    }

    /// <summary>
    /// Convert DayOfWeek enum to lowercase string (monday, tuesday, etc.)
    /// </summary>
    private string GetDayOfWeekString(DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Monday => "monday",
            DayOfWeek.Tuesday => "tuesday",
            DayOfWeek.Wednesday => "wednesday",
            DayOfWeek.Thursday => "thursday",
            DayOfWeek.Friday => "friday",
            DayOfWeek.Saturday => "saturday",
            DayOfWeek.Sunday => "sunday",
            _ => "monday"
        };
    }

    private void Initialize()
    {
        try
        {
            var sqlite = CodeLogic.Libs.Get<CL.SQLite.SQLiteLibrary>();
            if (sqlite == null)
            {
                return;
            }

            _sqlite = sqlite;
            _repository = sqlite.CreateRepository<FirewallScheduleEntity>();
        }
        catch
        {
            _sqlite = null;
            _repository = null;
        }
    }
}

/// <summary>
/// Firewall schedule entity (matches WebUI entity)
/// </summary>
public sealed class FirewallScheduleEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TimeRanges { get; set; } = string.Empty; // JSON array
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Time range model (for JSON deserialization)
/// </summary>
public sealed class TimeRange
{
    public string Day { get; set; } = string.Empty; // "monday", "tuesday", etc.
    public string StartTime { get; set; } = string.Empty; // "09:00"
    public string EndTime { get; set; } = string.Empty; // "17:00"
}
