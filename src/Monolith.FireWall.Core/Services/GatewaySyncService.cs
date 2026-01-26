using Monolith.FireWall.Common.Services;
using System.Threading;

namespace Monolith.FireWall.Core.Services;

/// <summary>
/// Background service that periodically syncs dynamic gateways and triggers health checks.
/// Coordinates between GatewayManager and GatewayHealthMonitor.
/// </summary>
public sealed class GatewaySyncService
{
    private readonly GatewayManager _gatewayManager;
    private readonly GatewayHealthMonitor? _healthMonitor;
    private readonly LoggingManager _loggingManager;
    private Task? _loopTask;
    private readonly TimeSpan _syncInterval = TimeSpan.FromSeconds(60);
    private readonly TimeSpan _healthCheckInterval = TimeSpan.FromSeconds(10);
    private DateTime _lastHealthCheck = DateTime.MinValue;

    public GatewaySyncService(
        GatewayManager gatewayManager,
        GatewayHealthMonitor? healthMonitor = null)
    {
        _gatewayManager = gatewayManager;
        _healthMonitor = healthMonitor;
        _loggingManager = LoggingManager.Instance;
    }

    public void Start(CancellationToken cancellationToken)
    {
        if (_loopTask != null)
        {
            return;
        }

        _loopTask = Task.Run(() => RunLoopAsync(cancellationToken), cancellationToken);
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        // Initial sync on startup
        await SyncGatewaysAsync(cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Run health checks more frequently than sync
                if (_healthMonitor != null && DateTime.UtcNow - _lastHealthCheck >= _healthCheckInterval)
                {
                    await RunHealthChecksAsync(cancellationToken);
                    _lastHealthCheck = DateTime.UtcNow;
                }

                // Sync dynamic gateways periodically
                await SyncGatewaysAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                await _loggingManager.LogSystemAsync(
                    "Routing",
                    "error",
                    "GatewaySyncService",
                    "Gateway sync/health check cycle failed",
                    new Dictionary<string, object>
                    {
                        ["error"] = ex.Message
                    });
            }

            try
            {
                // Sleep for the shorter interval to keep health checks frequent
                var sleepTime = _healthMonitor != null ? _healthCheckInterval : _syncInterval;
                await Task.Delay(sleepTime, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task SyncGatewaysAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _gatewayManager.SyncDynamicGatewaysAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await _loggingManager.LogSystemAsync(
                "Routing",
                "error",
                "GatewaySyncService",
                "Gateway sync failed",
                new Dictionary<string, object>
                {
                    ["error"] = ex.Message
                });
        }
    }

    private async Task RunHealthChecksAsync(CancellationToken cancellationToken)
    {
        if (_healthMonitor == null)
        {
            return;
        }

        try
        {
            var results = await _healthMonitor.CheckAllGatewaysAsync(cancellationToken);

            // Log any state changes
            foreach (var result in results.Where(r => r.StatusChanged))
            {
                await _loggingManager.LogSystemAsync(
                    "Routing",
                    result.NewStatus == Models.GatewayHealthStatus.Online ? "info" : "warning",
                    "GatewaySyncService",
                    $"Gateway health changed: {result.GatewayName} is now {result.NewStatus}",
                    new Dictionary<string, object>
                    {
                        ["gatewayId"] = result.GatewayId,
                        ["previousStatus"] = result.PreviousStatus.ToString(),
                        ["newStatus"] = result.NewStatus.ToString(),
                        ["latencyMs"] = result.LatencyMs ?? 0,
                        ["packetLoss"] = result.PacketLossPercent ?? 0
                    });
            }
        }
        catch (Exception ex)
        {
            await _loggingManager.LogSystemAsync(
                "Routing",
                "error",
                "GatewaySyncService",
                "Gateway health check failed",
                new Dictionary<string, object>
                {
                    ["error"] = ex.Message
                });
        }
    }
}
