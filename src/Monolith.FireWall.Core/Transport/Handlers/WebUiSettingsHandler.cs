using System.Text.Json;
using Monolith.FireWall.Common.Models;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Transport.Handlers;

public sealed class WebUiSettingsHandler : ICoreRequestHandler
{
    private static readonly HashSet<string> Actions = new(StringComparer.OrdinalIgnoreCase)
    {
        "webui.settings.get",
        "webui.settings.update",
        "webui.service.restart"
    };

    public bool CanHandle(string action) => Actions.Contains(action);

    public async Task<ApiResponse> HandleAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        var action = request.GetProperty("action").GetString() ?? string.Empty;

        switch (action)
        {
            case "webui.settings.get":
                var settings = await context.WebUiSettingsManager.GetSettingsAsync();
                return new ApiResponse(true, settings, null);

            case "webui.settings.update":
                if (!CoreRequestParsing.TryGetPayload(request, out WebUiSettingsUpdateRequest updateRequest, out var updateError))
                {
                    return new ApiResponse(false, null, updateError);
                }

                var updateResult = await context.WebUiSettingsManager.UpdateSettingsAsync(updateRequest);
                if (!updateResult.Success)
                {
                    return new ApiResponse(false, null, updateResult.Error);
                }

                return new ApiResponse(true, updateResult, null);

            case "webui.service.restart":
                var restartResult = await context.WebUiServiceManager.RestartServiceAsync(cancellationToken);
                if (!restartResult.Success)
                {
                    return new ApiResponse(false, null, restartResult.Error);
                }

                return new ApiResponse(true, restartResult, null);
        }

        return new ApiResponse(false, null, $"Unhandled action: {action}");
    }
}
