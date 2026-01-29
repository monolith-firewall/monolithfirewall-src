using System.Text.Json;
using Monolith.FireWall.Common.Models;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Transport.Handlers;

/// <summary>
/// Handler for gateway group operations (multi-WAN failover/load balancing).
/// </summary>
public sealed class GatewayGroupHandler : ICoreRequestHandler
{
    private static readonly HashSet<string> Actions = new(StringComparer.OrdinalIgnoreCase)
    {
        "gateway.groups.list",
        "gateway.groups.get",
        "gateway.groups.create",
        "gateway.groups.update",
        "gateway.groups.delete",
        "gateway.groups.evaluate"
    };

    public bool CanHandle(string action) => Actions.Contains(action);

    public async Task<ApiResponse> HandleAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        if (context.GatewayGroupManager == null)
        {
            return new ApiResponse(false, null, "Gateway group manager not available");
        }

        var action = request.GetProperty("action").GetString() ?? string.Empty;

        switch (action)
        {
            case "gateway.groups.list":
                return await HandleListAsync(context, cancellationToken);

            case "gateway.groups.get":
                return await HandleGetAsync(context, request, cancellationToken);

            case "gateway.groups.create":
                return await HandleCreateAsync(context, request, cancellationToken);

            case "gateway.groups.update":
                return await HandleUpdateAsync(context, request, cancellationToken);

            case "gateway.groups.delete":
                return await HandleDeleteAsync(context, request, cancellationToken);

            case "gateway.groups.evaluate":
                return await HandleEvaluateAsync(context, cancellationToken);
        }

        return new ApiResponse(false, null, $"Unhandled action: {action}");
    }

    private static async Task<ApiResponse> HandleListAsync(CoreRequestContext context, CancellationToken cancellationToken)
    {
        var groups = await context.GatewayGroupManager!.GetGroupsAsync(cancellationToken);
        return new ApiResponse(true, groups, null);
    }

    private static async Task<ApiResponse> HandleGetAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        if (!CoreRequestParsing.TryGetPayload(request, out GatewayGroupDeleteRequest idRequest, out var error))
        {
            return new ApiResponse(false, null, error);
        }

        var group = await context.GatewayGroupManager!.GetGroupAsync(idRequest.Id, cancellationToken);
        if (group == null)
        {
            return new ApiResponse(false, null, "Gateway group not found");
        }

        return new ApiResponse(true, group, null);
    }

    private static async Task<ApiResponse> HandleCreateAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        if (!CoreRequestParsing.TryGetPayload(request, out GatewayGroupRequest groupRequest, out var error))
        {
            return new ApiResponse(false, null, error);
        }

        var result = await context.GatewayGroupManager!.CreateGroupAsync(groupRequest, cancellationToken);
        return result.Success
            ? new ApiResponse(true, result.Group, null)
            : new ApiResponse(false, null, result.Error);
    }

    private static async Task<ApiResponse> HandleUpdateAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        // Get ID from payload
        if (!request.TryGetProperty("payload", out var payload))
        {
            return new ApiResponse(false, null, "Missing payload");
        }

        if (!payload.TryGetProperty("id", out var idProp) || !idProp.TryGetInt32(out var id))
        {
            return new ApiResponse(false, null, "Missing or invalid id");
        }

        if (!CoreRequestParsing.TryGetPayload(request, out GatewayGroupRequest groupRequest, out var error))
        {
            return new ApiResponse(false, null, error);
        }

        var result = await context.GatewayGroupManager!.UpdateGroupAsync(id, groupRequest, cancellationToken);
        return result.Success
            ? new ApiResponse(true, result.Group, null)
            : new ApiResponse(false, null, result.Error);
    }

    private static async Task<ApiResponse> HandleDeleteAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        if (!CoreRequestParsing.TryGetPayload(request, out GatewayGroupDeleteRequest deleteRequest, out var error))
        {
            return new ApiResponse(false, null, error);
        }

        var result = await context.GatewayGroupManager!.DeleteGroupAsync(deleteRequest.Id, cancellationToken);
        return result.Success
            ? new ApiResponse(true, new { id = deleteRequest.Id }, null)
            : new ApiResponse(false, null, result.Error);
    }

    private static async Task<ApiResponse> HandleEvaluateAsync(CoreRequestContext context, CancellationToken cancellationToken)
    {
        // Trigger evaluation of all gateway groups (useful after manual health check)
        await context.GatewayGroupManager!.EvaluateGroupsAsync(cancellationToken);
        return new ApiResponse(true, new { message = "Gateway groups evaluated" }, null);
    }
}
