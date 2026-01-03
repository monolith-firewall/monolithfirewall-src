using System.Text.Json;
using Monolith.FireWall.Common.Models;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Transport.Handlers;

public sealed class RoutingHandler : ICoreRequestHandler
{
    private static readonly HashSet<string> Actions = new(StringComparer.OrdinalIgnoreCase)
    {
        "routing.gateways.list",
        "routing.routes.list",
        "routing.routes.add",
        "routing.routes.remove"
    };

    public bool CanHandle(string action) => Actions.Contains(action);

    public async Task<ApiResponse> HandleAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        var action = request.GetProperty("action").GetString() ?? string.Empty;

        switch (action)
        {
            case "routing.gateways.list":
                var gateways = await context.RoutingManager.GetGatewaysAsync(cancellationToken);
                return new ApiResponse(true, gateways, null);

            case "routing.routes.list":
                var routes = await context.RoutingManager.GetStaticRoutesAsync(cancellationToken);
                return new ApiResponse(true, routes, null);

            case "routing.routes.add":
                if (!CoreRequestParsing.TryGetPayload(request, out StaticRouteRequest routeRequest, out var routeError))
                {
                    return new ApiResponse(false, null, routeError);
                }

                var addResult = await context.RoutingManager.AddRouteAsync(routeRequest, cancellationToken);
                return addResult.Success
                    ? new ApiResponse(true, addResult.Route, null)
                    : new ApiResponse(false, null, addResult.Error);

            case "routing.routes.remove":
                if (!CoreRequestParsing.TryGetPayload(request, out StaticRouteDeleteRequest routeDelete, out var routeDeleteError))
                {
                    return new ApiResponse(false, null, routeDeleteError);
                }

                var removeResult = await context.RoutingManager.DeleteRouteAsync(routeDelete.Id, cancellationToken);
                return removeResult.Success
                    ? new ApiResponse(true, new { id = routeDelete.Id }, null)
                    : new ApiResponse(false, null, removeResult.Error);
        }

        return new ApiResponse(false, null, $"Unhandled action: {action}");
    }
}
