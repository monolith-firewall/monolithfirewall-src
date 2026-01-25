namespace Monolith.FireWall.Common.Interfaces;

/// <summary>
/// Interface for components (packages/modules) that want to receive
/// network state change notifications.
/// </summary>
public interface INetworkStateListener
{
    /// <summary>
    /// Called when an interface's state changes (link up/down, IP changed, etc.).
    /// </summary>
    /// <param name="change">The change event details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task OnInterfaceStateChangedAsync(NetworkInterfaceChange change, CancellationToken cancellationToken);

    /// <summary>
    /// Called when a gateway's health status changes.
    /// </summary>
    /// <param name="change">The change event details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task OnGatewayHealthChangedAsync(NetworkGatewayChange change, CancellationToken cancellationToken);

    /// <summary>
    /// Called when link state changes (cable plugged/unplugged).
    /// This is a convenience method for link-specific changes.
    /// </summary>
    /// <param name="change">The change event details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task OnLinkStateChangedAsync(NetworkLinkChange change, CancellationToken cancellationToken);
}

/// <summary>
/// Represents an interface state change event.
/// </summary>
public sealed class NetworkInterfaceChange
{
    /// <summary>Interface name (e.g., "eth0")</summary>
    public string InterfaceName { get; set; } = string.Empty;

    /// <summary>Type of change</summary>
    public InterfaceChangeType ChangeType { get; set; }

    /// <summary>Previous IP address (for IP changes)</summary>
    public string? PreviousIpAddress { get; set; }

    /// <summary>New IP address (for IP changes)</summary>
    public string? NewIpAddress { get; set; }

    /// <summary>Previous gateway (for gateway changes)</summary>
    public string? PreviousGateway { get; set; }

    /// <summary>New gateway (for gateway changes)</summary>
    public string? NewGateway { get; set; }

    /// <summary>When the change occurred</summary>
    public DateTime OccurredAt { get; set; }

    /// <summary>Additional details as JSON</summary>
    public string? DetailsJson { get; set; }
}

/// <summary>
/// Represents a gateway health change event.
/// </summary>
public sealed class NetworkGatewayChange
{
    /// <summary>Gateway database ID</summary>
    public int GatewayId { get; set; }

    /// <summary>Gateway name</summary>
    public string GatewayName { get; set; } = string.Empty;

    /// <summary>Gateway address</summary>
    public string GatewayAddress { get; set; } = string.Empty;

    /// <summary>Previous health status</summary>
    public string PreviousStatus { get; set; } = string.Empty;

    /// <summary>New health status</summary>
    public string NewStatus { get; set; } = string.Empty;

    /// <summary>Current latency in ms</summary>
    public int? LatencyMs { get; set; }

    /// <summary>Current packet loss percentage</summary>
    public double? PacketLossPercent { get; set; }

    /// <summary>When the change occurred</summary>
    public DateTime OccurredAt { get; set; }
}

/// <summary>
/// Represents a link state change event (simplified).
/// </summary>
public sealed class NetworkLinkChange
{
    /// <summary>Interface name (e.g., "eth0")</summary>
    public string InterfaceName { get; set; } = string.Empty;

    /// <summary>True if link is now up, false if down</summary>
    public bool IsUp { get; set; }

    /// <summary>When the change occurred</summary>
    public DateTime OccurredAt { get; set; }
}

/// <summary>
/// Types of interface state changes.
/// </summary>
public enum InterfaceChangeType
{
    /// <summary>Unknown change</summary>
    Unknown = 0,

    /// <summary>Interface link came up</summary>
    LinkUp = 1,

    /// <summary>Interface link went down</summary>
    LinkDown = 2,

    /// <summary>IP address was added</summary>
    IpAdded = 3,

    /// <summary>IP address was removed</summary>
    IpRemoved = 4,

    /// <summary>IP address changed</summary>
    IpChanged = 5,

    /// <summary>Gateway was added (DHCP)</summary>
    GatewayAdded = 6,

    /// <summary>Gateway was removed</summary>
    GatewayRemoved = 7,

    /// <summary>Gateway changed (DHCP)</summary>
    GatewayChanged = 8,

    /// <summary>New interface appeared</summary>
    InterfaceAdded = 9,

    /// <summary>Interface was removed</summary>
    InterfaceRemoved = 10,

    /// <summary>DHCP lease was obtained</summary>
    DhcpLeaseObtained = 11,

    /// <summary>DHCP lease was renewed</summary>
    DhcpLeaseRenewed = 12,

    /// <summary>DHCP lease expired</summary>
    DhcpLeaseExpired = 13
}
