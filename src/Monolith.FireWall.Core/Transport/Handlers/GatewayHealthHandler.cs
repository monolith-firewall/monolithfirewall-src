using System.Text.Json;
using Monolith.FireWall.Common.Models;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Transport.Handlers;

/// <summary>
/// Handler for gateway health monitoring operations.
/// </summary>
public sealed class GatewayHealthHandler : ICoreRequestHandler
{
    private static readonly HashSet<string> Actions = new(StringComparer.OrdinalIgnoreCase)
    {
        "gateway.health.list",
        "gateway.health.get",
        "gateway.health.check",
        "gateway.health.check_all"
    };

    public bool CanHandle(string action) => Actions.Contains(action);

    public async Task<ApiResponse> HandleAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        var action = request.GetProperty("action").GetString() ?? string.Empty;

        switch (action)
        {
            case "gateway.health.list":
                return await HandleListHealthAsync(context, cancellationToken);

            case "gateway.health.get":
                return await HandleGetHealthAsync(context, request, cancellationToken);

            case "gateway.health.check":
                return await HandleCheckGatewayAsync(context, request, cancellationToken);

            case "gateway.health.check_all":
                return await HandleCheckAllAsync(context, cancellationToken);
        }

        return new ApiResponse(false, null, $"Unhandled action: {action}");
    }

    private static async Task<ApiResponse> HandleListHealthAsync(CoreRequestContext context, CancellationToken cancellationToken)
    {
        if (context.GatewayHealthStore == null)
        {
            return new ApiResponse(false, null, "Gateway health store not available");
        }

        var healthList = await context.GatewayHealthStore.GetAllHealthAsync();
        var views = healthList.Select(h => new GatewayHealthView
        {
            GatewayId = h.GatewayId,
            Status = h.Status.ToString().ToLowerInvariant(),
            LatencyMs = h.LatencyMs,
            PacketLossPercent = h.PacketLossPercent,
            ConsecutiveFailures = h.ConsecutiveFailures,
            ConsecutiveSuccesses = h.ConsecutiveSuccesses,
            LastCheckAt = h.LastCheckAt,
            LastStateChangeAt = h.LastStateChangeAt,
            LastError = h.LastError
        }).ToList();

        return new ApiResponse(true, views, null);
    }

    private static async Task<ApiResponse> HandleGetHealthAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        if (context.GatewayHealthStore == null)
        {
            return new ApiResponse(false, null, "Gateway health store not available");
        }

        if (!CoreRequestParsing.TryGetPayload(request, out GatewayIdRequest idRequest, out var error))
        {
            return new ApiResponse(false, null, error);
        }

        var health = await context.GatewayHealthStore.GetHealthAsync(idRequest.GatewayId);
        if (health == null)
        {
            return new ApiResponse(false, null, "Gateway health not found");
        }

        var view = new GatewayHealthView
        {
            GatewayId = health.GatewayId,
            Status = health.Status.ToString().ToLowerInvariant(),
            LatencyMs = health.LatencyMs,
            PacketLossPercent = health.PacketLossPercent,
            ConsecutiveFailures = health.ConsecutiveFailures,
            ConsecutiveSuccesses = health.ConsecutiveSuccesses,
            LastCheckAt = health.LastCheckAt,
            LastStateChangeAt = health.LastStateChangeAt,
            LastError = health.LastError
        };

        return new ApiResponse(true, view, null);
    }

    private static async Task<ApiResponse> HandleCheckGatewayAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        if (context.GatewayHealthMonitor == null)
        {
            return new ApiResponse(false, null, "Gateway health monitor not available");
        }

        if (!CoreRequestParsing.TryGetPayload(request, out GatewayIdRequest idRequest, out var error))
        {
            return new ApiResponse(false, null, error);
        }

        var result = await context.GatewayHealthMonitor.CheckGatewayNowAsync(idRequest.GatewayId, cancellationToken);
        if (result == null)
        {
            return new ApiResponse(false, null, "Gateway not found");
        }

        return new ApiResponse(true, result, null);
    }

    private static async Task<ApiResponse> HandleCheckAllAsync(CoreRequestContext context, CancellationToken cancellationToken)
    {
        if (context.GatewayHealthMonitor == null)
        {
            return new ApiResponse(false, null, "Gateway health monitor not available");
        }

        var results = await context.GatewayHealthMonitor.CheckAllGatewaysAsync(cancellationToken);

        var views = results.Select(r => new
        {
            gatewayId = r.GatewayId,
            gatewayName = r.GatewayName,
            statusChanged = r.StatusChanged,
            previousStatus = r.PreviousStatus.ToString().ToLowerInvariant(),
            newStatus = r.NewStatus.ToString().ToLowerInvariant(),
            latencyMs = r.LatencyMs,
            packetLossPercent = r.PacketLossPercent,
            error = r.Error
        }).ToList();

        return new ApiResponse(true, views, null);
    }

}

/// <summary>
/// Request with gateway ID.
/// </summary>
public sealed class GatewayIdRequest
{
    public int GatewayId { get; set; }
}
