using System.Text.Json;
using Monolith.FireWall.Common.Models;

namespace Monolith.FireWall.Core.Transport.Handlers;

public sealed class StartupHandler : ICoreRequestHandler
{
    private static readonly HashSet<string> Actions = new(StringComparer.OrdinalIgnoreCase)
    {
        "startup.initialize"
    };

    public bool CanHandle(string action) => Actions.Contains(action);

    public async Task<ApiResponse> HandleAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        var action = request.GetProperty("action").GetString() ?? string.Empty;

        switch (action)
        {
            case "startup.initialize":
                var result = await context.StartupManager.InitializeSystemAsync(cancellationToken);
                return new ApiResponse(result.Success, result, result.Error);
        }

        return new ApiResponse(false, null, $"Unhandled action: {action}");
    }
}
