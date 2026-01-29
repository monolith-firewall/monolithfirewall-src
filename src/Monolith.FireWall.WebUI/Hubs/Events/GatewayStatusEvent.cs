namespace Monolith.FireWall.WebUI.Hubs.Events;

/// <summary>
/// Event payload for gateway status changes.
/// </summary>
public sealed record GatewayStatusEvent
{
    /// <summary>
    /// The gateway ID.
    /// </summary>
    public int GatewayId { get; init; }

    /// <summary>
    /// Gateway name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gateway address.
    /// </summary>
    public string Address { get; init; } = string.Empty;

    /// <summary>
    /// Gateway status: "online", "offline", or "degraded".
    /// </summary>
    public string Status { get; init; } = "offline";

    /// <summary>
    /// Round-trip latency in milliseconds, null if offline.
    /// </summary>
    public int? LatencyMs { get; init; }

    /// <summary>
    /// Packet loss percentage (0-100).
    /// </summary>
    public double PacketLossPercent { get; init; }

    /// <summary>
    /// Interface this gateway is bound to.
    /// </summary>
    public string? Interface { get; init; }

    /// <summary>
    /// Whether this is the default gateway.
    /// </summary>
    public bool IsDefault { get; init; }

    /// <summary>
    /// Timestamp when this event was generated.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
