namespace Monolith.FireWall.Common.Interfaces;

/// <summary>
/// Provides access to network operational state information.
/// This interface allows packages to query current network status
/// without depending on Core implementation details.
/// </summary>
public interface INetworkStateProvider
{
    /// <summary>
    /// Gets the operational state of a specific interface.
    /// </summary>
    /// <param name="interfaceName">The interface name (e.g., "eth0")</param>
    /// <returns>The interface state, or null if not found</returns>
    Task<InterfaceOperationalState?> GetInterfaceStateAsync(string interfaceName);

    /// <summary>
    /// Gets the operational state of all interfaces.
    /// </summary>
    Task<IReadOnlyList<InterfaceOperationalState>> GetAllInterfaceStatesAsync();

    /// <summary>
    /// Gets the health status of a specific gateway.
    /// </summary>
    /// <param name="gatewayId">The gateway database ID</param>
    /// <returns>The gateway health, or null if not found</returns>
    Task<GatewayHealth?> GetGatewayHealthAsync(int gatewayId);

    /// <summary>
    /// Gets the health status of all gateways.
    /// </summary>
    Task<IReadOnlyList<GatewayHealth>> GetAllGatewayHealthAsync();

    /// <summary>
    /// Subscribes to interface state changes.
    /// </summary>
    /// <param name="handler">The handler to call when state changes</param>
    /// <returns>A disposable that unsubscribes when disposed</returns>
    IDisposable SubscribeToInterfaceChanges(Action<InterfaceStateChangeEvent> handler);

    /// <summary>
    /// Subscribes to gateway health changes.
    /// </summary>
    /// <param name="handler">The handler to call when health changes</param>
    /// <returns>A disposable that unsubscribes when disposed</returns>
    IDisposable SubscribeToGatewayChanges(Action<GatewayStateChangeEvent> handler);
}

/// <summary>
/// Represents the operational state of a network interface.
/// </summary>
public sealed class InterfaceOperationalState
{
    /// <summary>Interface name (e.g., "eth0")</summary>
    public string InterfaceName { get; set; } = string.Empty;

    /// <summary>Link state: "up", "down", "dormant", "unknown"</summary>
    public string LinkState { get; set; } = "unknown";

    /// <summary>MAC address</summary>
    public string? MacAddress { get; set; }

    /// <summary>Link speed in Mbps</summary>
    public int? SpeedMbps { get; set; }

    /// <summary>Duplex mode: "full", "half"</summary>
    public string? Duplex { get; set; }

    /// <summary>MTU</summary>
    public int? Mtu { get; set; }

    /// <summary>Current IPv4 address</summary>
    public string? CurrentIpv4Address { get; set; }

    /// <summary>Current IPv4 prefix length</summary>
    public int? CurrentIpv4Prefix { get; set; }

    /// <summary>Current IPv6 address</summary>
    public string? CurrentIpv6Address { get; set; }

    /// <summary>Current IPv6 prefix length</summary>
    public int? CurrentIpv6Prefix { get; set; }

    /// <summary>DHCP-provided gateway</summary>
    public string? DhcpGateway { get; set; }

    /// <summary>Health status: "healthy", "degraded", "down", "unknown"</summary>
    public string HealthStatus { get; set; } = "unknown";

    /// <summary>Last time this interface was seen</summary>
    public DateTime LastSeenAt { get; set; }

    /// <summary>Last time link state changed</summary>
    public DateTime? LastLinkChangeAt { get; set; }

    /// <summary>Last time IP address changed</summary>
    public DateTime? LastIpChangeAt { get; set; }
}

/// <summary>
/// Represents the health status of a gateway.
/// </summary>
public sealed class GatewayHealth
{
    /// <summary>Gateway database ID</summary>
    public int GatewayId { get; set; }

    /// <summary>Gateway name</summary>
    public string GatewayName { get; set; } = string.Empty;

    /// <summary>Gateway address</summary>
    public string GatewayAddress { get; set; } = string.Empty;

    /// <summary>Health status: "online", "offline", "degraded", "unknown"</summary>
    public string Status { get; set; } = "unknown";

    /// <summary>Latency in milliseconds</summary>
    public int? LatencyMs { get; set; }

    /// <summary>Packet loss percentage</summary>
    public double? PacketLossPercent { get; set; }

    /// <summary>Number of consecutive probe failures</summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>Number of consecutive probe successes</summary>
    public int ConsecutiveSuccesses { get; set; }

    /// <summary>Last health check time</summary>
    public DateTime? LastCheckAt { get; set; }

    /// <summary>Last time health status changed</summary>
    public DateTime? LastStateChangeAt { get; set; }

    /// <summary>Last error message</summary>
    public string? LastError { get; set; }
}

/// <summary>
/// Event raised when interface state changes.
/// </summary>
public sealed class InterfaceStateChangeEvent
{
    /// <summary>Interface name</summary>
    public string InterfaceName { get; set; } = string.Empty;

    /// <summary>Type of change: "link_up", "link_down", "ip_changed", etc.</summary>
    public string ChangeType { get; set; } = string.Empty;

    /// <summary>Previous state</summary>
    public InterfaceOperationalState? PreviousState { get; set; }

    /// <summary>New state</summary>
    public InterfaceOperationalState? NewState { get; set; }

    /// <summary>When the change occurred</summary>
    public DateTime OccurredAt { get; set; }
}

/// <summary>
/// Event raised when gateway health changes.
/// </summary>
public sealed class GatewayStateChangeEvent
{
    /// <summary>Gateway database ID</summary>
    public int GatewayId { get; set; }

    /// <summary>Previous status</summary>
    public string PreviousStatus { get; set; } = string.Empty;

    /// <summary>New status</summary>
    public string NewStatus { get; set; } = string.Empty;

    /// <summary>When the change occurred</summary>
    public DateTime OccurredAt { get; set; }
}
