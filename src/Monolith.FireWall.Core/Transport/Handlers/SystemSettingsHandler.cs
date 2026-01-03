using System.Text.Json;
using Monolith.FireWall.Common.Models;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Transport.Handlers;

public sealed class SystemSettingsHandler : ICoreRequestHandler
{
    private static readonly HashSet<string> Actions = new(StringComparer.OrdinalIgnoreCase)
    {
        "system.settings.get",
        "system.settings.update"
    };

    public bool CanHandle(string action) => Actions.Contains(action);

    public async Task<ApiResponse> HandleAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        var action = request.GetProperty("action").GetString() ?? string.Empty;

        switch (action)
        {
            case "system.settings.get":
                var settings = await context.SettingsManager.GetSettingsAsync();
                return new ApiResponse(true, settings, null);

            case "system.settings.update":
                if (!CoreRequestParsing.TryGetPayload(request, out SystemSettingsUpdateRequest settingsRequest, out var settingsError))
                {
                    return new ApiResponse(false, null, settingsError);
                }

                var settingsUpdateResult = await context.SettingsManager.UpdateSettingsAsync(settingsRequest);
                if (!settingsUpdateResult.Success)
                {
                    return new ApiResponse(false, null, settingsUpdateResult.Error);
                }

                var updatedSettings = await context.SettingsManager.GetSettingsAsync();
                return new ApiResponse(true, updatedSettings, null);
        }

        return new ApiResponse(false, null, $"Unhandled action: {action}");
    }
}
