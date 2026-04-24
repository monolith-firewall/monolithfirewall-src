using System.Text.Json;
using CodeLogic;
using Monolith.FireWall.Common.Models;

namespace Monolith.FireWall.Common.Services;

/// <summary>
/// Centralized logging service for Monolith Firewall
/// </summary>
public class LoggingManager
{
    private static LoggingManager? _instance;
    private CL.SQLite.SQLiteLibrary? _sqlite;
    private CL.SQLite.Services.Repository<LogEntryEntity>? _repository;

    private LoggingManager()
    {
        InitializeRepository();
    }

    public static LoggingManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new LoggingManager();
            }
            return _instance;
        }
    }

    private void InitializeRepository()
    {
        try
        {
            var sqlite = CodeLogic.Libraries.Get<CL.SQLite.SQLiteLibrary>();
            if (sqlite != null)
            {
                _sqlite = sqlite;
                _repository = sqlite.GetRepository<LogEntryEntity>();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize LoggingManager repository: {ex.Message}");
        }
    }

    /// <summary>
    /// Log a Monolith event (Auth, Changes, Package, Module, User, Permission)
    /// </summary>
    public async Task LogMonolithAsync(
        string category,
        string level,
        string source,
        string message,
        int? userId = null,
        string? ipAddress = null,
        Dictionary<string, object>? details = null)
    {
        await LogAsync("Monolith", category, level, source, message, userId, ipAddress, details);
    }

    /// <summary>
    /// Log a System event (Service, Configuration, Network, Storage, Update)
    /// </summary>
    public async Task LogSystemAsync(
        string category,
        string level,
        string source,
        string message,
        Dictionary<string, object>? details = null)
    {
        await LogAsync("System", category, level, source, message, null, null, details);
    }

    /// <summary>
    /// Log a Security event (Firewall, Intrusion, Access, Threat, Audit)
    /// </summary>
    public async Task LogSecurityAsync(
        string category,
        string level,
        string source,
        string message,
        int? userId = null,
        string? ipAddress = null,
        Dictionary<string, object>? details = null)
    {
        await LogAsync("Security", category, level, source, message, userId, ipAddress, details);
    }

    /// <summary>
    /// Internal method to write log entry
    /// </summary>
    private async Task LogAsync(
        string logType,
        string category,
        string level,
        string source,
        string message,
        int? userId,
        string? ipAddress,
        Dictionary<string, object>? details)
    {
        try
        {
            if (_repository == null)
            {
                Console.WriteLine($"Warning: LoggingManager repository not initialized. Log: {logType}/{category}/{level} - {message}");
                return;
            }

            var entity = new LogEntryEntity
            {
                Timestamp = DateTime.UtcNow,
                LogType = logType,
                Category = category,
                Level = level,
                Source = source,
                Message = message,
                Details = details != null ? JsonSerializer.Serialize(details) : "{}",
                UserId = userId,
                IpAddress = ipAddress,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _repository.InsertAsync(entity);
            if (result.IsSuccess && result.Value > 0)
            {
                entity.Id = (int)result.Value;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to write log entry: {ex.Message}");
        }
    }

    /// <summary>
    /// Query logs with filtering and pagination
    /// </summary>
    public async Task<LogQueryResult> QueryLogsAsync(LogQueryParams queryParams)
    {
        try
        {
            if (_sqlite == null)
            {
                return new LogQueryResult { Limit = queryParams.Limit, Offset = queryParams.Offset };
            }

            var query = _sqlite.GetQueryBuilder<LogEntryEntity>().Select(e => e);

            // Apply filters
            if (!string.IsNullOrEmpty(queryParams.LogType))
            {
                query = query.Where(e => e.LogType == queryParams.LogType);
            }

            if (!string.IsNullOrEmpty(queryParams.Category))
            {
                query = query.Where(e => e.Category == queryParams.Category);
            }

            if (!string.IsNullOrEmpty(queryParams.Level))
            {
                query = query.Where(e => e.Level == queryParams.Level);
            }

            if (!string.IsNullOrEmpty(queryParams.Source))
            {
                query = query.Where(e => e.Source == queryParams.Source);
            }

            if (queryParams.StartDate.HasValue)
            {
                query = query.Where(e => e.Timestamp >= queryParams.StartDate.Value);
            }

            if (queryParams.EndDate.HasValue)
            {
                query = query.Where(e => e.Timestamp <= queryParams.EndDate.Value);
            }

            // Get total count
            var countResult = await query.CountAsync();
            var totalCount = countResult.IsSuccess ? (int)countResult.Value : 0;

            // Apply pagination and ordering
            var logsResult = await query
                .OrderByDescending(e => e.Timestamp)
                .Skip(queryParams.Offset)
                .Take(queryParams.Limit)
                .ToListAsync();

            var entities = logsResult.IsSuccess && logsResult.Value != null ? logsResult.Value : new List<LogEntryEntity>();

            // Convert entities to models
            var logs = entities.Select(e => EntityToLogEntry(e)).ToList();

            return new LogQueryResult
            {
                Logs = logs,
                TotalCount = totalCount,
                Limit = queryParams.Limit,
                Offset = queryParams.Offset
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to query logs: {ex.Message}");
            return new LogQueryResult { Limit = queryParams.Limit, Offset = queryParams.Offset };
        }
    }

    /// <summary>
    /// Convert entity to model
    /// </summary>
    private LogEntry EntityToLogEntry(LogEntryEntity entity)
    {
        Dictionary<string, object>? details = null;
        try
        {
            if (!string.IsNullOrEmpty(entity.Details) && entity.Details != "{}")
            {
                details = JsonSerializer.Deserialize<Dictionary<string, object>>(entity.Details);
            }
        }
        catch
        {
            // Ignore JSON parsing errors
        }

        return new LogEntry
        {
            Id = entity.Id,
            Timestamp = entity.Timestamp,
            LogType = entity.LogType,
            Category = entity.Category,
            Level = entity.Level,
            Source = entity.Source,
            Message = entity.Message,
            Details = details,
            UserId = entity.UserId,
            IpAddress = entity.IpAddress,
            CreatedAt = entity.CreatedAt
        };
    }
}
