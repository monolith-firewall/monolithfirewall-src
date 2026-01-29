namespace Monolith.FireWall.WebUI.Hubs.Events;

/// <summary>
/// Event payload for system metrics updates.
/// </summary>
public sealed record SystemMetricsEvent
{
    /// <summary>
    /// CPU usage percentage (0-100).
    /// </summary>
    public double CpuPercent { get; init; }

    /// <summary>
    /// Memory usage percentage (0-100).
    /// </summary>
    public double MemoryPercent { get; init; }

    /// <summary>
    /// Memory used in bytes.
    /// </summary>
    public long MemoryUsedBytes { get; init; }

    /// <summary>
    /// Total memory in bytes.
    /// </summary>
    public long MemoryTotalBytes { get; init; }

    /// <summary>
    /// Root disk usage percentage (0-100).
    /// </summary>
    public double DiskPercent { get; init; }

    /// <summary>
    /// Disk used in bytes.
    /// </summary>
    public long DiskUsedBytes { get; init; }

    /// <summary>
    /// Total disk in bytes.
    /// </summary>
    public long DiskTotalBytes { get; init; }

    /// <summary>
    /// System uptime in seconds.
    /// </summary>
    public long UptimeSeconds { get; init; }

    /// <summary>
    /// Load average (1 minute).
    /// </summary>
    public double LoadAverage1 { get; init; }

    /// <summary>
    /// Load average (5 minutes).
    /// </summary>
    public double LoadAverage5 { get; init; }

    /// <summary>
    /// Load average (15 minutes).
    /// </summary>
    public double LoadAverage15 { get; init; }

    /// <summary>
    /// Alert level: null (normal), "warning", or "critical".
    /// </summary>
    public string? AlertLevel { get; init; }

    /// <summary>
    /// Timestamp when this event was generated.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
