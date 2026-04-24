using System.Text.Json;
using CodeLogic;
using CL.SQLite.Services;
using Monolith.FireWall.Common.Services;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Services.Firewall;

/// <summary>
/// Manages firewall schedules and determines if rules should be active based on time
/// </summary>
public sealed class FirewallScheduleManager
{
    private readonly LoggingManager _loggingManager;
    private CL.SQLite.SQLiteLibrary? _sqlite;
    private Repository<FirewallScheduleEntity>? _repository;

    public FirewallScheduleManager()
    {
        _loggingManager = LoggingManager.Instance;
        Initialize();
    }

    /// <summary>
    /// List all schedules
    /// </summary>
    public async Task<List<FirewallScheduleView>> ListSchedulesAsync()
    {
        if (_repository == null)
        {
            return new List<FirewallScheduleView>();
        }

        var result = await _repository.GetAllAsync();
        var entities = result.IsSuccess && result.Value != null
            ? result.Value.ToList()
            : new List<FirewallScheduleEntity>();

        return entities
            .Select(EntityToView)
            .ToList();
    }

    /// <summary>
    /// Get a schedule view by ID
    /// </summary>
    public async Task<FirewallScheduleView?> GetScheduleViewAsync(int id)
    {
        var entity = await GetScheduleByIdAsync(id);
        return entity == null ? null : EntityToView(entity);
    }

    /// <summary>
    /// Create a new schedule
    /// </summary>
    public async Task<(bool Success, string? Error, FirewallScheduleView? Schedule)> CreateScheduleAsync(FirewallScheduleRequest request)
    {
        if (_repository == null)
        {
            return (false, "Schedule storage not available", null);
        }

        var validation = ValidateRequest(request);
        if (!validation.Success)
        {
            return (false, validation.Error, null);
        }

        var now = DateTime.UtcNow;
        var timeRangesJson = SerializeTimeRanges(request.TimeRanges);

        var entity = new FirewallScheduleEntity
        {
            Name = request.Name!.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            TimeRanges = timeRangesJson,
            Enabled = request.Enabled,
            CreatedAt = now,
            UpdatedAt = now
        };

        var insert = await _repository.InsertAsync(entity);
        if (!insert.IsSuccess || insert.Value <= 0)
        {
            return (false, "Failed to create schedule", null);
        }

        entity.Id = (int)insert.Value;

        await _loggingManager.LogSecurityAsync(
            "Firewall",
            "Info",
            "FirewallSchedule",
            $"Created schedule '{entity.Name}'",
            details: new Dictionary<string, object>
            {
                ["scheduleId"] = entity.Id
            });

        return (true, null, EntityToView(entity));
    }

    /// <summary>
    /// Update an existing schedule
    /// </summary>
    public async Task<(bool Success, string? Error, FirewallScheduleView? Schedule)> UpdateScheduleAsync(int id, FirewallScheduleRequest request)
    {
        if (_repository == null)
        {
            return (false, "Schedule storage not available", null);
        }

        var entity = await GetScheduleByIdAsync(id);
        if (entity == null)
        {
            return (false, "Schedule not found", null);
        }

        var validation = ValidateRequest(request);
        if (!validation.Success)
        {
            return (false, validation.Error, null);
        }

        entity.Name = request.Name!.Trim();
        entity.Description = request.Description?.Trim() ?? string.Empty;
        entity.TimeRanges = SerializeTimeRanges(request.TimeRanges);
        entity.Enabled = request.Enabled;
        entity.UpdatedAt = DateTime.UtcNow;

        var update = await _repository.UpdateAsync(entity);
        if (!update.IsSuccess)
        {
            return (false, "Failed to update schedule", null);
        }

        await _loggingManager.LogSecurityAsync(
            "Firewall",
            "Info",
            "FirewallSchedule",
            $"Updated schedule '{entity.Name}'",
            details: new Dictionary<string, object>
            {
                ["scheduleId"] = entity.Id
            });

        return (true, null, EntityToView(entity));
    }

    /// <summary>
    /// Delete a schedule
    /// </summary>
    public async Task<bool> DeleteScheduleAsync(int id)
    {
        if (_repository == null)
        {
            return false;
        }

        var entity = await GetScheduleByIdAsync(id);
        if (entity == null)
        {
            return true;
        }

        var delete = await _repository.DeleteAsync(id);
        if (!delete.IsSuccess)
        {
            return false;
        }

        await _loggingManager.LogSecurityAsync(
            "Firewall",
            "Info",
            "FirewallSchedule",
            $"Deleted schedule '{entity.Name}'",
            details: new Dictionary<string, object>
            {
                ["scheduleId"] = entity.Id
            });

        return true;
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
        if (!result.IsSuccess || result.Value == null)
        {
            return null;
        }

        return result.Value.FirstOrDefault(s => s.Id == scheduleId);
    }

    private static FirewallScheduleView EntityToView(FirewallScheduleEntity entity)
    {
        return new FirewallScheduleView
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            TimeRanges = DeserializeTimeRanges(entity.TimeRanges),
            Enabled = entity.Enabled,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    private static List<FirewallScheduleTimeRange> DeserializeTimeRanges(string? timeRangesJson)
    {
        if (string.IsNullOrWhiteSpace(timeRangesJson))
        {
            return new List<FirewallScheduleTimeRange>();
        }

        try
        {
            // Try deserializing as FirewallScheduleTimeRange first
            var ranges = JsonSerializer.Deserialize<List<FirewallScheduleTimeRange>>(timeRangesJson);
            return ranges ?? new List<FirewallScheduleTimeRange>();
        }
        catch
        {
            return new List<FirewallScheduleTimeRange>();
        }
    }

    private static string SerializeTimeRanges(List<FirewallScheduleTimeRange>? timeRanges)
    {
        if (timeRanges == null || timeRanges.Count == 0)
        {
            return "[]";
        }

        return JsonSerializer.Serialize(timeRanges);
    }

    private (bool Success, string? Error) ValidateRequest(FirewallScheduleRequest? request)
    {
        if (request == null)
        {
            return (false, "Request is required");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return (false, "Name is required");
        }

        if (request.TimeRanges != null)
        {
            foreach (var range in request.TimeRanges)
            {
                if (string.IsNullOrWhiteSpace(range.Day))
                {
                    return (false, "Day is required for each time range");
                }

                if (string.IsNullOrWhiteSpace(range.StartTime))
                {
                    return (false, "Start time is required for each time range");
                }

                if (string.IsNullOrWhiteSpace(range.EndTime))
                {
                    return (false, "End time is required for each time range");
                }

                if (!TimeSpan.TryParse(range.StartTime, out _))
                {
                    return (false, $"Invalid start time format: {range.StartTime}");
                }

                if (!TimeSpan.TryParse(range.EndTime, out _))
                {
                    return (false, $"Invalid end time format: {range.EndTime}");
                }
            }
        }

        return (true, null);
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
            var ranges = JsonSerializer.Deserialize<List<TimeRange>>(schedule.TimeRanges);
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
            var sqlite = CodeLogic.Libraries.Get<CL.SQLite.SQLiteLibrary>();
            if (sqlite == null)
            {
                return;
            }

            _sqlite = sqlite;
            _repository = sqlite.GetRepository<FirewallScheduleEntity>();
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
[CL.SQLite.Models.SQLiteTable("firewall_schedules")]
public sealed class FirewallScheduleEntity
{
    [CL.SQLite.Models.SQLiteColumn(IsPrimaryKey = true, IsAutoIncrement = true)]
    public int Id { get; set; }

    [CL.SQLite.Models.SQLiteColumn(IsNotNull = true, DataType = CL.SQLite.Models.SQLiteDataType.TEXT, Size = 128)]
    public string Name { get; set; } = string.Empty;

    [CL.SQLite.Models.SQLiteColumn(DataType = CL.SQLite.Models.SQLiteDataType.TEXT, Size = 256)]
    public string Description { get; set; } = string.Empty;

    [CL.SQLite.Models.SQLiteColumn(DataType = CL.SQLite.Models.SQLiteDataType.TEXT)]
    public string TimeRanges { get; set; } = string.Empty; // JSON array

    [CL.SQLite.Models.SQLiteColumn(DataType = CL.SQLite.Models.SQLiteDataType.BOOLEAN, DefaultValue = "1")]
    public bool Enabled { get; set; } = true;

    [CL.SQLite.Models.SQLiteColumn(DataType = CL.SQLite.Models.SQLiteDataType.DATETIME)]
    public DateTime CreatedAt { get; set; }

    [CL.SQLite.Models.SQLiteColumn(DataType = CL.SQLite.Models.SQLiteDataType.DATETIME)]
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
