using Microsoft.AspNetCore.SignalR;

namespace Monolith.FireWall.WebUI.Hubs;

/// <summary>
/// Unified SignalR hub for real-time system events.
/// Clients subscribe to channels to receive relevant updates.
///
/// Available channels:
/// - "interfaces" - Interface status changes (up/down, IP changes, traffic)
/// - "gateways" - Gateway status changes (online/offline, latency)
/// - "services" - Service status changes (started/stopped)
/// - "system" - System metrics (CPU, memory, disk)
/// - "routing" - Routing table changes
/// - "pending" - Pending configuration changes
/// </summary>
public sealed class SystemEventsHub : Hub
{
    private readonly ILogger<SystemEventsHub> _logger;

    /// <summary>
    /// Valid channel names that clients can subscribe to.
    /// </summary>
    private static readonly HashSet<string> ValidChannels = new(StringComparer.OrdinalIgnoreCase)
    {
        "interfaces",
        "gateways",
        "services",
        "system",
        "routing",
        "pending"
    };

    public SystemEventsHub(ILogger<SystemEventsHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogDebug("Client connected to SystemEventsHub: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogDebug("Client disconnected from SystemEventsHub: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Subscribe to a channel to receive events.
    /// </summary>
    /// <param name="channel">Channel name (e.g., "interfaces", "gateways")</param>
    public async Task Subscribe(string channel)
    {
        if (string.IsNullOrWhiteSpace(channel))
        {
            _logger.LogWarning("Client {ConnectionId} tried to subscribe to empty channel", Context.ConnectionId);
            return;
        }

        if (!ValidChannels.Contains(channel))
        {
            _logger.LogWarning("Client {ConnectionId} tried to subscribe to invalid channel: {Channel}",
                Context.ConnectionId, channel);
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, channel);
        _logger.LogDebug("Client {ConnectionId} subscribed to channel: {Channel}", Context.ConnectionId, channel);
    }

    /// <summary>
    /// Unsubscribe from a channel.
    /// </summary>
    /// <param name="channel">Channel name</param>
    public async Task Unsubscribe(string channel)
    {
        if (string.IsNullOrWhiteSpace(channel))
        {
            return;
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, channel);
        _logger.LogDebug("Client {ConnectionId} unsubscribed from channel: {Channel}", Context.ConnectionId, channel);
    }

    /// <summary>
    /// Subscribe to multiple channels at once.
    /// </summary>
    /// <param name="channels">List of channel names</param>
    public async Task SubscribeMany(string[] channels)
    {
        if (channels == null || channels.Length == 0)
        {
            return;
        }

        foreach (var channel in channels)
        {
            await Subscribe(channel);
        }
    }

    /// <summary>
    /// Unsubscribe from multiple channels at once.
    /// </summary>
    /// <param name="channels">List of channel names</param>
    public async Task UnsubscribeMany(string[] channels)
    {
        if (channels == null || channels.Length == 0)
        {
            return;
        }

        foreach (var channel in channels)
        {
            await Unsubscribe(channel);
        }
    }
}
