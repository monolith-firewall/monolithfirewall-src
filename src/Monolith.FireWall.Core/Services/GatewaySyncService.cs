using Monolith.FireWall.Common.Services;
using System.Threading;

namespace Monolith.FireWall.Core.Services;

public sealed class GatewaySyncService
{
    private readonly GatewayManager _gatewayManager;
    private readonly LoggingManager _loggingManager;
    private Task? _loopTask;

    public GatewaySyncService(GatewayManager gatewayManager)
    {
        _gatewayManager = gatewayManager;
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
        while (!cancellationToken.IsCancellationRequested)
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

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(60), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
