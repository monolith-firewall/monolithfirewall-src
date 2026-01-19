using System.Text.Json;
using Monolith.FireWall.Common.Models;
using Monolith.FireWall.Core.Models;
using Monolith.FireWall.Platform.Models;

namespace Monolith.FireWall.Core.Transport.Handlers;

public sealed class NetworkCardHandler : ICoreRequestHandler
{
    private static readonly HashSet<string> Actions = new(StringComparer.OrdinalIgnoreCase)
    {
        "network.cards.list",
        "network.cards.get",
        "network.cards.speed.set",
        "network.cards.offloads.set",
        "network.cards.buffers.set",
        "network.cards.coalescing.set",
        "network.cards.pause.set",
        "network.cards.revert"
    };

    public bool CanHandle(string action) => Actions.Contains(action);

    public async Task<ApiResponse> HandleAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        var action = request.GetProperty("action").GetString() ?? string.Empty;

        // Get NetworkCardService from context (we'll need to add it)
        var networkCardService = new Services.NetworkCardService(
            context.CommandRunner,
            new Services.NetworkInventoryService(context.CommandRunner)
        );

        switch (action)
        {
            case "network.cards.list":
                var cards = await networkCardService.GetAllCardsAsync(cancellationToken);
                return new ApiResponse(true, cards, null);

            case "network.cards.get":
                if (!CoreRequestParsing.TryGetPayload(request, out NetworkCardGetRequest cardRequest, out var cardError))
                {
                    return new ApiResponse(false, null, cardError);
                }

                var card = await networkCardService.GetCardInfoAsync(cardRequest.Interface, cancellationToken);
                if (card == null)
                {
                    return new ApiResponse(false, null, "Network card not found or ethtool not available");
                }

                return new ApiResponse(true, card, null);

            case "network.cards.speed.set":
                if (!CoreRequestParsing.TryGetPayload(request, out NetworkCardSpeedRequest speedRequest, out var speedError))
                {
                    return new ApiResponse(false, null, speedError);
                }

                var speedResult = await networkCardService.SetSpeedAsync(speedRequest, cancellationToken);
                return speedResult
                    ? new ApiResponse(true, new { @interface = speedRequest.Interface }, null)
                    : new ApiResponse(false, null, "Failed to set speed/duplex settings");

            case "network.cards.offloads.set":
                if (!CoreRequestParsing.TryGetPayload(request, out NetworkCardOffloadRequest offloadRequest, out var offloadError))
                {
                    return new ApiResponse(false, null, offloadError);
                }

                var offloadResult = await networkCardService.SetOffloadsAsync(offloadRequest, cancellationToken);
                return offloadResult
                    ? new ApiResponse(true, new { @interface = offloadRequest.Interface }, null)
                    : new ApiResponse(false, null, "Failed to set offload settings");

            case "network.cards.buffers.set":
                if (!CoreRequestParsing.TryGetPayload(request, out NetworkCardBufferRequest bufferRequest, out var bufferError))
                {
                    return new ApiResponse(false, null, bufferError);
                }

                var bufferResult = await networkCardService.SetBuffersAsync(bufferRequest, cancellationToken);
                return bufferResult
                    ? new ApiResponse(true, new { @interface = bufferRequest.Interface }, null)
                    : new ApiResponse(false, null, "Failed to set buffer settings");

            case "network.cards.coalescing.set":
                if (!CoreRequestParsing.TryGetPayload(request, out NetworkCardCoalescingRequest coalescingRequest, out var coalescingError))
                {
                    return new ApiResponse(false, null, coalescingError);
                }

                var coalescingResult = await networkCardService.SetCoalescingAsync(coalescingRequest, cancellationToken);
                return coalescingResult
                    ? new ApiResponse(true, new { @interface = coalescingRequest.Interface }, null)
                    : new ApiResponse(false, null, "Failed to set coalescing settings");

            case "network.cards.pause.set":
                if (!CoreRequestParsing.TryGetPayload(request, out NetworkCardPauseRequest pauseRequest, out var pauseError))
                {
                    return new ApiResponse(false, null, pauseError);
                }

                var pauseResult = await networkCardService.SetPauseAsync(pauseRequest, cancellationToken);
                return pauseResult
                    ? new ApiResponse(true, new { @interface = pauseRequest.Interface }, null)
                    : new ApiResponse(false, null, "Failed to set pause frame settings");

            case "network.cards.revert":
                if (!CoreRequestParsing.TryGetPayload(request, out NetworkCardRevertRequest revertRequest, out var revertError))
                {
                    return new ApiResponse(false, null, revertError);
                }

                var revertResult = await networkCardService.RevertToDefaultsAsync(revertRequest.Interface, cancellationToken);
                return revertResult
                    ? new ApiResponse(true, new { @interface = revertRequest.Interface }, null)
                    : new ApiResponse(false, null, "Failed to revert to defaults");

            default:
                return new ApiResponse(false, null, $"Unhandled action: {action}");
        }
    }
}

// Request models
public sealed class NetworkCardGetRequest
{
    public string Interface { get; set; } = string.Empty;
}

public sealed class NetworkCardRevertRequest
{
    public string Interface { get; set; } = string.Empty;
}
