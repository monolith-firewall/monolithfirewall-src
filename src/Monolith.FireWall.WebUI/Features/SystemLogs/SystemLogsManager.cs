using Monolith.FireWall.Common.Models;
using Monolith.FireWall.Common.Services;

namespace Monolith.FireWall.WebUI.Features.SystemLogs;

/// <summary>
/// Manager for System Logs module
/// </summary>
public class SystemLogsManager
{
    private readonly LoggingManager _loggingManager;

    public SystemLogsManager()
    {
        _loggingManager = LoggingManager.Instance;
    }

    /// <summary>
    /// Query Monolith logs
    /// </summary>
    public async Task<LogQueryResult> QueryMonolithLogsAsync(LogQueryParams queryParams)
    {
        queryParams.LogType = "Monolith";
        return await _loggingManager.QueryLogsAsync(queryParams);
    }

    /// <summary>
    /// Query System logs
    /// </summary>
    public async Task<LogQueryResult> QuerySystemLogsAsync(LogQueryParams queryParams)
    {
        queryParams.LogType = "System";
        return await _loggingManager.QueryLogsAsync(queryParams);
    }

    /// <summary>
    /// Query Security logs
    /// </summary>
    public async Task<LogQueryResult> QuerySecurityLogsAsync(LogQueryParams queryParams)
    {
        queryParams.LogType = "Security";
        return await _loggingManager.QueryLogsAsync(queryParams);
    }
}
