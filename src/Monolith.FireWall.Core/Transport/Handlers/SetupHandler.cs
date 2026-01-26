using System.Text.Json;
using Monolith.FireWall.Common.Interfaces;
using Monolith.FireWall.Common.Models;
using Monolith.FireWall.Core.Services;

namespace Monolith.FireWall.Core.Transport.Handlers;

/// <summary>
/// Handles setup wizard API requests
/// </summary>
public class SetupHandler : ICoreRequestHandler
{
    private static readonly HashSet<string> Actions = new(StringComparer.OrdinalIgnoreCase)
    {
        "setup.status",
        "setup.complete-step",
        "setup.packages",
        "setup.finish",
        "setup.skip",
        "setup.skip-step",
        "setup.network-suggestions"
    };

    private readonly SetupManager _setupManager;
    private readonly InitialNetworkCaptureService? _networkCaptureService;
    private readonly ILogger _logger;

    public SetupHandler(
        SetupManager setupManager,
        ILogger logger,
        InitialNetworkCaptureService? networkCaptureService = null)
    {
        _setupManager = setupManager;
        _logger = logger;
        _networkCaptureService = networkCaptureService;
    }

    public bool CanHandle(string action) => Actions.Contains(action);

    public async Task<ApiResponse> HandleAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        try
        {
            var action = request.GetProperty("action").GetString() ?? string.Empty;

            return action switch
            {
                "setup.status" => HandleGetStatus(),
                "setup.complete-step" => HandleCompleteStep(request),
                "setup.packages" => HandleGetPackages(),
                "setup.finish" => await HandleFinishAsync(request),
                "setup.skip" => await HandleSkipAsync(),
                "setup.skip-step" => HandleSkipStep(request),
                "setup.network-suggestions" => await HandleNetworkSuggestionsAsync(cancellationToken),
                _ => new ApiResponse(false, null, $"Unknown setup action: {action}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error handling setup request");
            return new ApiResponse(false, null, $"Error: {ex.Message}");
        }
    }

    private ApiResponse HandleGetStatus()
    {
        var status = _setupManager.GetSetupStatus();
        return new ApiResponse(true, status, null);
    }

    private ApiResponse HandleCompleteStep(JsonElement request)
    {
        try
        {
            var stepRequest = JsonSerializer.Deserialize<CompleteStepRequest>(
                request.GetRawText(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            if (stepRequest == null || string.IsNullOrEmpty(stepRequest.StepId))
            {
                return new ApiResponse(false, null, "Invalid step request");
            }

            _setupManager.CompleteStep(stepRequest.StepId, stepRequest.Data);
            return new ApiResponse(true, new { stepId = stepRequest.StepId, completed = true }, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing setup step");
            return new ApiResponse(false, null, $"Error completing step: {ex.Message}");
        }
    }

    private ApiResponse HandleGetPackages()
    {
        var packages = _setupManager.GetPackageSetupPages();
        return new ApiResponse(true, new { packages }, null);
    }

    private async Task<ApiResponse> HandleFinishAsync(JsonElement request)
    {
        try
        {
            var finishRequest = new FinishSetupRequest();
            try
            {
                finishRequest = JsonSerializer.Deserialize<FinishSetupRequest>(
                    request.GetRawText(),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                ) ?? new FinishSetupRequest();
            }
            catch
            {
                // Use defaults if deserialization fails
            }

            await _setupManager.FinishSetupAsync(finishRequest.SkipRemaining);
            return new ApiResponse(true, new { completed = true }, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finishing setup");
            return new ApiResponse(false, null, $"Error finishing setup: {ex.Message}");
        }
    }

    private async Task<ApiResponse> HandleSkipAsync()
    {
        try
        {
            await _setupManager.SkipSetupAsync();
            return new ApiResponse(true, new { skipped = true }, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error skipping setup");
            return new ApiResponse(false, null, $"Error skipping setup: {ex.Message}");
        }
    }

    private ApiResponse HandleSkipStep(JsonElement request)
    {
        try
        {
            var stepId = request.TryGetProperty("stepId", out var stepIdEl)
                ? stepIdEl.GetString()
                : null;

            if (string.IsNullOrEmpty(stepId))
            {
                return new ApiResponse(false, null, "Step ID is required");
            }

            // Mark step as completed (skipped)
            _setupManager.CompleteStep(stepId, null);
            return new ApiResponse(true, new { stepId, skipped = true }, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error skipping setup step");
            return new ApiResponse(false, null, $"Error skipping step: {ex.Message}");
        }
    }

    private async Task<ApiResponse> HandleNetworkSuggestionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_networkCaptureService == null)
            {
                return new ApiResponse(false, null, "Network capture service not available");
            }

            var suggestions = await _networkCaptureService.GetNetworkSuggestionsAsync(cancellationToken);

            // Convert to JSON-friendly format
            var result = new
            {
                interfaces = suggestions.Select(s => new
                {
                    interfaceName = s.InterfaceName,
                    suggestedRole = s.Role.ToString().ToLowerInvariant(),
                    suggestedName = s.SuggestedName,
                    suggestedIpMode = s.SuggestedIpMode.ToString().ToLowerInvariant(),
                    reason = s.Reason,
                    currentIp = s.CurrentIpAddress,
                    currentPrefix = s.CurrentPrefix,
                    currentGateway = s.CurrentGateway,
                    hasDefaultGateway = !string.IsNullOrEmpty(s.CurrentGateway),
                    linkUp = s.LinkUp,
                    macAddress = s.MacAddress,
                    confidence = s.Confidence
                }).ToList()
            };

            return new ApiResponse(true, result, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting network suggestions");
            return new ApiResponse(false, null, $"Error getting network suggestions: {ex.Message}");
        }
    }
}
