namespace Monolith.FireWall.WebUI.Hubs.Events;

/// <summary>
/// Event payload for interface status changes.
/// </summary>
public sealed record InterfaceStatusEvent
{
    /// <summary>
    /// The interface name (e.g., "eth0", "wg0").
    /// </summary>
    public string InterfaceName { get; init; } = string.Empty;

    /// <summary>
    /// Interface status: "up" or "down".
    /// </summary>
    public string Status { get; init; } = "down";

    /// <summary>
    /// IPv4 address if assigned, null otherwise.
    /// </summary>
    public string? IpAddress { get; init; }

    /// <summary>
    /// IPv6 address if assigned, null otherwise.
    /// </summary>
    public string? Ipv6Address { get; init; }

    /// <summary>
    /// Total bytes received.
    /// </summary>
    public long RxBytes { get; init; }

    /// <summary>
    /// Total bytes transmitted.
    /// </summary>
    public long TxBytes { get; init; }

    /// <summary>
    /// Bytes received per second (for rate display).
    /// </summary>
    public long RxBytesPerSec { get; init; }

    /// <summary>
    /// Bytes transmitted per second (for rate display).
    /// </summary>
    public long TxBytesPerSec { get; init; }

    /// <summary>
    /// Link speed in Mbps, if available.
    /// </summary>
    public int? LinkSpeedMbps { get; init; }

    /// <summary>
    /// MAC address.
    /// </summary>
    public string? MacAddress { get; init; }

    /// <summary>
    /// Timestamp when this event was generated.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
