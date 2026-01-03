using System.Text.Json;
using Monolith.FireWall.Common.Models;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Transport.Handlers;

public sealed class SystemTuneablesHandler : ICoreRequestHandler
{
    private static readonly HashSet<string> Actions = new(StringComparer.OrdinalIgnoreCase)
    {
        "system.tuneables.list",
        "system.tuneables.apply",
        "system.tuneables.save"
    };

    public bool CanHandle(string action) => Actions.Contains(action);

    public async Task<ApiResponse> HandleAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        var action = request.GetProperty("action").GetString() ?? string.Empty;

        switch (action)
        {
            case "system.tuneables.list":
                var tuneables = await context.TuneablesManager.GetTuneablesAsync(cancellationToken);
                return new ApiResponse(true, tuneables, null);

            case "system.tuneables.apply":
                if (!CoreRequestParsing.TryGetPayload(request, out TuneableApplyRequest tuneableRequest, out var tuneableError))
                {
                    return new ApiResponse(false, null, tuneableError);
                }

                var tuneableResult = await context.TuneablesManager.ApplyAsync(tuneableRequest, cancellationToken);
                return new ApiResponse(tuneableResult.Success, tuneableResult, tuneableResult.Error);

            case "system.tuneables.save":
                if (!CoreRequestParsing.TryGetPayload(request, out TuneableApplyRequest tuneableSaveRequest, out var tuneableSaveError))
                {
                    return new ApiResponse(false, null, tuneableSaveError);
                }

                var tuneableSaveResult = await context.TuneablesManager.SaveAsync(tuneableSaveRequest, cancellationToken);
                return new ApiResponse(tuneableSaveResult.Success, tuneableSaveResult, tuneableSaveResult.Error);
        }

        return new ApiResponse(false, null, $"Unhandled action: {action}");
    }
}
