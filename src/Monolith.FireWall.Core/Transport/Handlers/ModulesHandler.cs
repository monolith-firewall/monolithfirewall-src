using System.Text.Json;
using Monolith.FireWall.Common.Models;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Transport.Handlers;

public sealed class ModulesHandler : ICoreRequestHandler
{
    private static readonly HashSet<string> Actions = new(StringComparer.OrdinalIgnoreCase)
    {
        "modules.enable",
        "modules.disable"
    };

    public bool CanHandle(string action) => Actions.Contains(action);

    public async Task<ApiResponse> HandleAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        var action = request.GetProperty("action").GetString() ?? string.Empty;
        if (!CoreRequestParsing.TryGetPayload(request, out ModuleStateRequest moduleRequest, out var moduleError))
        {
            return new ApiResponse(false, null, moduleError);
        }

        var enable = action.EndsWith("enable", StringComparison.OrdinalIgnoreCase);
        var moduleUpdated = await context.PackageStateStore.SetModuleEnabledAsync(
            moduleRequest.PackageId,
            moduleRequest.ModuleId,
            enable);

        return moduleUpdated
            ? new ApiResponse(true, new { packageId = moduleRequest.PackageId, moduleId = moduleRequest.ModuleId, enabled = enable }, null)
            : new ApiResponse(false, null, "Failed to update module state");
    }
}
