using Microsoft.AspNetCore.SignalR;
using Monolith.FireWall.WebUI.Hubs.Events;

namespace Monolith.FireWall.WebUI.Hubs;

/// <summary>
/// Service to broadcast system events to SignalR clients subscribed to specific channels.
/// </summary>
public sealed class SystemEventsNotifier
{
    private readonly IHubContext<SystemEventsHub> _hubContext;
    private readonly ILogger<SystemEventsNotifier> _logger;

    public SystemEventsNotifier(
        IHubContext<SystemEventsHub> hubContext,
        ILogger<SystemEventsNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    #region Interface Events

    /// <summary>
    /// Notify clients subscribed to "interfaces" channel about a status change.
    /// </summary>
    public async Task NotifyInterfaceStatusChangedAsync(InterfaceStatusEvent e)
    {
        try
        {
            await _hubContext.Clients.Group("interfaces").SendAsync("InterfaceStatusChanged", e);
            _logger.LogDebug("Broadcast interface status: {Interface} = {Status}", e.InterfaceName, e.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast interface status for {Interface}", e.InterfaceName);
        }
    }

    /// <summary>
    /// Notify clients about multiple interface status changes at once (batch update).
    /// </summary>
    public async Task NotifyInterfaceStatusBatchAsync(IEnumerable<InterfaceStatusEvent> events)
    {
        try
        {
            var eventList = events.ToList();
            await _hubContext.Clients.Group("interfaces").SendAsync("InterfaceStatusBatch", eventList);
            _logger.LogDebug("Broadcast interface status batch: {Count} interfaces", eventList.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast interface status batch");
        }
    }

    #endregion

    #region Gateway Events

    /// <summary>
    /// Notify clients subscribed to "gateways" channel about a status change.
    /// </summary>
    public async Task NotifyGatewayStatusChangedAsync(GatewayStatusEvent e)
    {
        try
        {
            await _hubContext.Clients.Group("gateways").SendAsync("GatewayStatusChanged", e);
            _logger.LogDebug("Broadcast gateway status: {Gateway} = {Status}", e.Name, e.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast gateway status for {Gateway}", e.Name);
        }
    }

    /// <summary>
    /// Notify clients about multiple gateway status changes at once (batch update).
    /// </summary>
    public async Task NotifyGatewayStatusBatchAsync(IEnumerable<GatewayStatusEvent> events)
    {
        try
        {
            var eventList = events.ToList();
            await _hubContext.Clients.Group("gateways").SendAsync("GatewayStatusBatch", eventList);
            _logger.LogDebug("Broadcast gateway status batch: {Count} gateways", eventList.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast gateway status batch");
        }
    }

    #endregion

    #region System Metrics Events

    /// <summary>
    /// Notify clients subscribed to "system" channel about metrics update.
    /// </summary>
    public async Task NotifySystemMetricsAsync(SystemMetricsEvent e)
    {
        try
        {
            await _hubContext.Clients.Group("system").SendAsync("SystemMetricsUpdated", e);
            _logger.LogDebug("Broadcast system metrics: CPU={Cpu}%, Mem={Mem}%",
                e.CpuPercent.ToString("F1"), e.MemoryPercent.ToString("F1"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast system metrics");
        }
    }

    #endregion

    #region Service Events

    /// <summary>
    /// Notify clients subscribed to "services" channel about a service status change.
    /// </summary>
    public async Task NotifyServiceStatusChangedAsync(ServiceStatusEvent e)
    {
        try
        {
            await _hubContext.Clients.Group("services").SendAsync("ServiceStatusChanged", e);
            _logger.LogDebug("Broadcast service status: {Service} = {Status}", e.ServiceName, e.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast service status for {Service}", e.ServiceName);
        }
    }

    /// <summary>
    /// Notify clients about multiple service status changes at once (batch update).
    /// </summary>
    public async Task NotifyServiceStatusBatchAsync(IEnumerable<ServiceStatusEvent> events)
    {
        try
        {
            var eventList = events.ToList();
            await _hubContext.Clients.Group("services").SendAsync("ServiceStatusBatch", eventList);
            _logger.LogDebug("Broadcast service status batch: {Count} services", eventList.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast service status batch");
        }
    }

    #endregion

    #region Routing Events

    /// <summary>
    /// Notify clients subscribed to "routing" channel about routing table changes.
    /// </summary>
    public async Task NotifyRoutingChangedAsync(object routingData)
    {
        try
        {
            await _hubContext.Clients.Group("routing").SendAsync("RoutingTableChanged", routingData);
            _logger.LogDebug("Broadcast routing table changed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast routing change");
        }
    }

    #endregion

    #region Pending Changes Events (replaces PendingChangesNotifier)

    /// <summary>
    /// Notify clients subscribed to "pending" channel about pending count change.
    /// </summary>
    public async Task NotifyPendingCountChangedAsync(int count)
    {
        try
        {
            await _hubContext.Clients.Group("pending").SendAsync("PendingCountChanged", count);
            // Also broadcast to all clients for navbar badge
            await _hubContext.Clients.All.SendAsync("PendingCountChanged", count);
            _logger.LogDebug("Broadcast pending count: {Count}", count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast pending count");
        }
    }

    /// <summary>
    /// Notify clients that a new change was staged.
    /// </summary>
    public async Task NotifyChangeAddedAsync(PendingChangeNotification change)
    {
        try
        {
            await _hubContext.Clients.Group("pending").SendAsync("ChangeAdded", change);
            _logger.LogDebug("Broadcast change added: {TargetKey}", change.TargetKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast change added");
        }
    }

    /// <summary>
    /// Notify clients that changes were applied.
    /// </summary>
    public async Task NotifyChangesAppliedAsync(int appliedCount, int failedCount)
    {
        try
        {
            await _hubContext.Clients.Group("pending").SendAsync("ChangesApplied", new
            {
                appliedCount,
                failedCount,
                timestamp = DateTime.UtcNow
            });
            _logger.LogDebug("Broadcast changes applied: {Applied} applied, {Failed} failed", appliedCount, failedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast changes applied");
        }
    }

    /// <summary>
    /// Notify clients that changes were discarded.
    /// </summary>
    public async Task NotifyChangesDiscardedAsync(int discardedCount)
    {
        try
        {
            await _hubContext.Clients.Group("pending").SendAsync("ChangesDiscarded", new
            {
                discardedCount,
                timestamp = DateTime.UtcNow
            });
            _logger.LogDebug("Broadcast changes discarded: {Count}", discardedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast changes discarded");
        }
    }

    #endregion

    #region Generic Broadcast

    /// <summary>
    /// Broadcast a custom event to a specific channel.
    /// </summary>
    public async Task BroadcastToChannelAsync(string channel, string eventName, object data)
    {
        try
        {
            await _hubContext.Clients.Group(channel).SendAsync(eventName, data);
            _logger.LogDebug("Broadcast {Event} to channel {Channel}", eventName, channel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast {Event} to channel {Channel}", eventName, channel);
        }
    }

    /// <summary>
    /// Broadcast a custom event to all connected clients.
    /// </summary>
    public async Task BroadcastToAllAsync(string eventName, object data)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync(eventName, data);
            _logger.LogDebug("Broadcast {Event} to all clients", eventName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast {Event} to all clients", eventName);
        }
    }

    #endregion
}
