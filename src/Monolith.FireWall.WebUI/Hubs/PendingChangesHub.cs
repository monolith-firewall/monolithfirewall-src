using Microsoft.AspNetCore.SignalR;

namespace Monolith.FireWall.WebUI.Hubs;

/// <summary>
/// SignalR hub for real-time pending changes notifications.
/// Clients connect to receive updates when configuration changes are staged, applied, or discarded.
/// </summary>
public sealed class PendingChangesHub : Hub
{
    private readonly ILogger<PendingChangesHub> _logger;

    public PendingChangesHub(ILogger<PendingChangesHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected to PendingChangesHub: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected from PendingChangesHub: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Client can request the current pending count.
    /// </summary>
    public async Task RequestPendingCount()
    {
        // The hub notifier service will handle sending the count
        _logger.LogDebug("Client {ConnectionId} requested pending count", Context.ConnectionId);
    }
}

/// <summary>
/// Service to broadcast pending changes updates to all connected SignalR clients.
/// </summary>
public sealed class PendingChangesNotifier
{
    private readonly IHubContext<PendingChangesHub> _hubContext;
    private readonly ILogger<PendingChangesNotifier> _logger;

    public PendingChangesNotifier(
        IHubContext<PendingChangesHub> hubContext,
        ILogger<PendingChangesNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Notify all clients that the pending changes count has updated.
    /// </summary>
    public async Task NotifyPendingCountChangedAsync(int count)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("PendingCountChanged", count);
            _logger.LogDebug("Broadcast pending count: {Count}", count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast pending count");
        }
    }

    /// <summary>
    /// Notify all clients that a new change was staged.
    /// </summary>
    public async Task NotifyChangeAddedAsync(PendingChangeNotification change)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("ChangeAdded", change);
            _logger.LogDebug("Broadcast change added: {TargetKey}", change.TargetKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast change added");
        }
    }

    /// <summary>
    /// Notify all clients that changes were applied.
    /// </summary>
    public async Task NotifyChangesAppliedAsync(int appliedCount, int failedCount)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("ChangesApplied", new
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
    /// Notify all clients that changes were discarded.
    /// </summary>
    public async Task NotifyChangesDiscardedAsync(int discardedCount)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("ChangesDiscarded", new
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
}

/// <summary>
/// Notification payload for a pending change.
/// </summary>
public sealed class PendingChangeNotification
{
    public int Id { get; set; }
    public string TargetKey { get; set; } = string.Empty;
    public string TargetCategory { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ChangeType { get; set; } = string.Empty;
    public bool RequiresRestart { get; set; }
    public bool RequiresReboot { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}
