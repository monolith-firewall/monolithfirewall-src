using Monolith.FireWall.Core.Services.Platform;
using Monolith.FireWall.Platform.Models;

namespace Monolith.FireWall.Core.Services;

/// <summary>
/// Manages log viewing for systemd services via journalctl.
/// Provides structured log queries with filtering support.
/// </summary>
public sealed class ServiceLogManager
{
    private readonly PlatformCommandRunner _commandRunner;

    public ServiceLogManager(PlatformCommandRunner? commandRunner = null)
    {
        _commandRunner = commandRunner ?? new PlatformCommandRunner();
    }

    /// <summary>
    /// Get logs for a systemd service unit from journalctl.
    /// </summary>
    public async Task<ServiceLogQueryResult> GetLogsAsync(
        ServiceLogQuery query,
        CancellationToken cancellationToken = default)
    {
        var result = new ServiceLogQueryResult
        {
            Success = true,
            Logs = new List<ServiceLogEntry>(),
            TotalCount = 0
        };

        if (string.IsNullOrWhiteSpace(query.SystemdUnit))
        {
            result.Success = false;
            result.Error = "SystemdUnit is required";
            return result;
        }

        try
        {
            // Build journalctl command arguments
            var args = $"-u {query.SystemdUnit} --no-pager --output=json";

            // Add time filters
            if (query.Since.HasValue)
            {
                args += $" --since \"{query.Since.Value:yyyy-MM-dd HH:mm:ss}\"";
            }
            if (query.Until.HasValue)
            {
                args += $" --until \"{query.Until.Value:yyyy-MM-dd HH:mm:ss}\"";
            }

            // Add priority filter
            if (!string.IsNullOrWhiteSpace(query.Priority))
            {
                var priorityLevel = ParsePriority(query.Priority);
                if (priorityLevel.HasValue)
                {
                    args += $" -p {priorityLevel.Value}";
                }
            }

            // Limit lines
            var limit = query.Limit ?? 100;
            args += $" -n {limit}";

            // Get reverse chronological order (newest first)
            args += " --reverse";

            var command = new PlatformCommand
            {
                FileName = "journalctl",
                Arguments = args,
                UseSudo = false,
                TimeoutMs = 30000
            };

            var cmdResult = await _commandRunner.RunAsync(command, cancellationToken);

            if (cmdResult.ExitCode != 0)
            {
                // Non-zero exit may mean no logs found
                if (string.IsNullOrWhiteSpace(cmdResult.StdOut) && string.IsNullOrWhiteSpace(cmdResult.StdErr))
                {
                    return result;
                }

                result.Success = false;
                result.Error = cmdResult.StdErr ?? "Failed to retrieve logs from journalctl";
                return result;
            }

            if (string.IsNullOrWhiteSpace(cmdResult.StdOut))
            {
                return result;
            }

            // Parse JSON log entries (each line is a JSON object)
            var logLines = cmdResult.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var entries = new List<ServiceLogEntry>();

            foreach (var line in logLines)
            {
                try
                {
                    var entry = ParseJsonLogEntry(line);
                    if (entry != null)
                    {
                        entries.Add(entry);
                    }
                }
                catch
                {
                    // Skip malformed lines
                }
            }

            result.Logs = entries;
            result.TotalCount = entries.Count;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = $"Error retrieving service logs: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Parse a JSON log entry from journalctl --output=json
    /// </summary>
    private ServiceLogEntry? ParseJsonLogEntry(string jsonLine)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(jsonLine);
            var root = doc.RootElement;

            var entry = new ServiceLogEntry();

            // Parse timestamp (microseconds since epoch)
            if (root.TryGetProperty("__REALTIME_TIMESTAMP", out var timestampEl))
            {
                if (long.TryParse(timestampEl.GetString(), out var microseconds))
                {
                    entry.Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(microseconds / 1000).UtcDateTime;
                }
            }

            // Parse message
            if (root.TryGetProperty("MESSAGE", out var messageEl))
            {
                entry.Message = messageEl.GetString() ?? string.Empty;
            }

            // Parse priority
            if (root.TryGetProperty("PRIORITY", out var priorityEl))
            {
                if (int.TryParse(priorityEl.GetString(), out var priority))
                {
                    entry.Priority = PriorityToString(priority);
                    entry.PriorityLevel = priority;
                }
            }

            // Parse hostname
            if (root.TryGetProperty("_HOSTNAME", out var hostnameEl))
            {
                entry.Hostname = hostnameEl.GetString();
            }

            // Parse unit
            if (root.TryGetProperty("_SYSTEMD_UNIT", out var unitEl))
            {
                entry.Unit = unitEl.GetString();
            }

            // Parse PID
            if (root.TryGetProperty("_PID", out var pidEl))
            {
                if (int.TryParse(pidEl.GetString(), out var pid))
                {
                    entry.Pid = pid;
                }
            }

            // Parse identifier (SYSLOG_IDENTIFIER)
            if (root.TryGetProperty("SYSLOG_IDENTIFIER", out var identEl))
            {
                entry.Identifier = identEl.GetString();
            }

            return entry;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Convert priority string to syslog level
    /// </summary>
    private int? ParsePriority(string priority)
    {
        return priority.ToLowerInvariant() switch
        {
            "emerg" or "emergency" => 0,
            "alert" => 1,
            "crit" or "critical" => 2,
            "err" or "error" => 3,
            "warning" or "warn" => 4,
            "notice" => 5,
            "info" => 6,
            "debug" => 7,
            _ => null
        };
    }

    /// <summary>
    /// Convert syslog priority level to string
    /// </summary>
    private string PriorityToString(int priority)
    {
        return priority switch
        {
            0 => "emerg",
            1 => "alert",
            2 => "crit",
            3 => "err",
            4 => "warning",
            5 => "notice",
            6 => "info",
            7 => "debug",
            _ => "unknown"
        };
    }
}

/// <summary>
/// Query parameters for service logs
/// </summary>
public sealed class ServiceLogQuery
{
    public string SystemdUnit { get; set; } = string.Empty;
    public DateTime? Since { get; set; }
    public DateTime? Until { get; set; }
    public int? Limit { get; set; } = 100;
    public string? Priority { get; set; }
}

/// <summary>
/// Result of a service log query
/// </summary>
public sealed class ServiceLogQueryResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<ServiceLogEntry> Logs { get; set; } = new();
    public int TotalCount { get; set; }
}

/// <summary>
/// Parsed service log entry
/// </summary>
public sealed class ServiceLogEntry
{
    public DateTime Timestamp { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public int PriorityLevel { get; set; }
    public string? Hostname { get; set; }
    public string? Unit { get; set; }
    public int? Pid { get; set; }
    public string? Identifier { get; set; }
}
