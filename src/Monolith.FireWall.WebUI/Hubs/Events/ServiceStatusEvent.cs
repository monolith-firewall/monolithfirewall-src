namespace Monolith.FireWall.WebUI.Hubs.Events;

/// <summary>
/// Event payload for service status changes.
/// </summary>
public sealed record ServiceStatusEvent
{
    /// <summary>
    /// The service name (e.g., "isc-dhcp-server", "dnsmasq").
    /// </summary>
    public string ServiceName { get; init; } = string.Empty;

    /// <summary>
    /// Display name for the service.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Service status: "running", "stopped", "failed", "starting", "stopping".
    /// </summary>
    public string Status { get; init; } = "stopped";

    /// <summary>
    /// Whether the service is enabled to start on boot.
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// Process ID if running, null otherwise.
    /// </summary>
    public int? Pid { get; init; }

    /// <summary>
    /// Memory usage in bytes, if available.
    /// </summary>
    public long? MemoryBytes { get; init; }

    /// <summary>
    /// Associated module ID, if any.
    /// </summary>
    public string? ModuleId { get; init; }

    /// <summary>
    /// Timestamp when this event was generated.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
