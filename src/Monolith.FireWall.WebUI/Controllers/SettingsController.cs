using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Monolith.FireWall.WebUI.Hubs;
using Monolith.FireWall.WebUI.Services;

namespace Monolith.FireWall.WebUI.Controllers;

/// <summary>
/// Controller for configuration management with staged changes workflow.
/// </summary>
[ApiController]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
    private readonly CoreApiClient _coreClient;
    private readonly PendingChangesNotifier _notifier;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(
        CoreApiClient coreClient,
        PendingChangesNotifier notifier,
        ILogger<SettingsController> logger)
    {
        _coreClient = coreClient;
        _notifier = notifier;
        _logger = logger;
    }

    #region Pending Changes

    /// <summary>
    /// Gets the count of pending configuration changes.
    /// </summary>
    [HttpGet("pending/count")]
    public async Task<IActionResult> GetPendingCount()
    {
        return await SendCoreRequest(new { action = "config.pending.count" });
    }

    /// <summary>
    /// Gets all pending configuration changes.
    /// </summary>
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingChanges()
    {
        return await SendCoreRequest(new { action = "config.pending.list" });
    }

    /// <summary>
    /// Discards a specific pending change.
    /// </summary>
    [HttpDelete("pending/{changeId}")]
    public async Task<IActionResult> DiscardPendingChange(long changeId)
    {
        var result = await SendCoreRequestWithNotify(new { action = "config.pending.discard", changeId });
        return result;
    }

    /// <summary>
    /// Discards all pending changes.
    /// </summary>
    [HttpDelete("pending")]
    public async Task<IActionResult> DiscardAllPendingChanges()
    {
        var result = await SendCoreRequestWithNotify(new { action = "config.pending.discard-all" });
        return result;
    }

    #endregion

    #region Apply Changes

    /// <summary>
    /// Validates all pending changes without applying.
    /// </summary>
    [HttpPost("validate")]
    public async Task<IActionResult> ValidatePendingChanges()
    {
        return await SendCoreRequest(new { action = "config.validate" });
    }

    /// <summary>
    /// Applies all pending changes.
    /// </summary>
    [HttpPost("apply")]
    public async Task<IActionResult> ApplyAllChanges([FromBody] ApplyRequest? request = null)
    {
        return await SendCoreRequestWithNotify(new
        {
            action = "config.apply-all",
            appliedBy = request?.AppliedBy ?? User.Identity?.Name
        });
    }

    /// <summary>
    /// Applies a specific pending change.
    /// </summary>
    [HttpPost("apply/{changeId}")]
    public async Task<IActionResult> ApplyChange(long changeId, [FromBody] ApplyRequest? request = null)
    {
        return await SendCoreRequestWithNotify(new
        {
            action = "config.apply",
            changeId,
            appliedBy = request?.AppliedBy ?? User.Identity?.Name
        });
    }

    #endregion

    #region History

    /// <summary>
    /// Gets recent configuration change history.
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] int limit = 50)
    {
        return await SendCoreRequest(new { action = "config.history.list", limit });
    }

    /// <summary>
    /// Gets history for a specific target.
    /// </summary>
    [HttpGet("history/{targetType}/{targetId}")]
    public async Task<IActionResult> GetHistoryForTarget(string targetType, string targetId, [FromQuery] int limit = 50)
    {
        return await SendCoreRequest(new { action = "config.history.target", targetType, targetId, limit });
    }

    #endregion

    #region System Config

    /// <summary>
    /// Gets a system configuration by key.
    /// </summary>
    [HttpGet("system/{key}")]
    public async Task<IActionResult> GetSystemConfig(string key)
    {
        return await SendCoreRequest(new { action = "config.system.get", key });
    }

    /// <summary>
    /// Saves a system configuration (staged).
    /// </summary>
    [HttpPost("system/{key}")]
    public async Task<IActionResult> SaveSystemConfig(string key, [FromBody] JsonElement value, [FromQuery] string? description = null)
    {
        return await SendCoreRequestWithNotify(new
        {
            action = "config.system.save",
            key,
            value,
            changedBy = User.Identity?.Name,
            description
        });
    }

    /// <summary>
    /// Saves a system configuration directly (bypasses staging).
    /// </summary>
    [HttpPut("system/{key}")]
    public async Task<IActionResult> SaveSystemConfigDirect(string key, [FromBody] JsonElement value)
    {
        return await SendCoreRequest(new
        {
            action = "config.system.save-direct",
            key,
            value,
            changedBy = User.Identity?.Name
        });
    }

    #endregion

    #region Module Config

    /// <summary>
    /// Gets a module configuration.
    /// </summary>
    [HttpGet("module/{packageId}/{moduleId}")]
    public async Task<IActionResult> GetModuleConfig(string packageId, string moduleId)
    {
        return await SendCoreRequest(new { action = "config.module.get", packageId, moduleId });
    }

    /// <summary>
    /// Saves a module configuration (staged).
    /// </summary>
    [HttpPost("module/{packageId}/{moduleId}")]
    public async Task<IActionResult> SaveModuleConfig(string packageId, string moduleId, [FromBody] JsonElement value, [FromQuery] string? description = null)
    {
        return await SendCoreRequestWithNotify(new
        {
            action = "config.module.save",
            packageId,
            moduleId,
            value,
            changedBy = User.Identity?.Name,
            description
        });
    }

    /// <summary>
    /// Saves a module configuration directly (bypasses staging).
    /// </summary>
    [HttpPut("module/{packageId}/{moduleId}")]
    public async Task<IActionResult> SaveModuleConfigDirect(string packageId, string moduleId, [FromBody] JsonElement value)
    {
        return await SendCoreRequest(new
        {
            action = "config.module.save-direct",
            packageId,
            moduleId,
            value,
            changedBy = User.Identity?.Name
        });
    }

    #endregion

    #region Helpers

    private async Task<IActionResult> SendCoreRequest(object request)
    {
        try
        {
            var requestJson = JsonSerializer.Serialize(request);
            var responseJson = await _coreClient.SendRequestAsync(requestJson);
            var response = JsonSerializer.Deserialize<JsonElement>(responseJson);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error communicating with Core service");
            return Ok(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Sends request to Core and broadcasts pending count update via SignalR.
    /// </summary>
    private async Task<IActionResult> SendCoreRequestWithNotify(object request)
    {
        try
        {
            var requestJson = JsonSerializer.Serialize(request);
            var responseJson = await _coreClient.SendRequestAsync(requestJson);
            var response = JsonSerializer.Deserialize<JsonElement>(responseJson);

            // Check if the request was successful
            var success = false;
            if (response.TryGetProperty("Success", out var successProp))
            {
                success = successProp.GetBoolean();
            }
            else if (response.TryGetProperty("success", out var successPropLower))
            {
                success = successPropLower.GetBoolean();
            }

            // If successful, get the updated pending count and broadcast
            if (success)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var countRequest = JsonSerializer.Serialize(new { action = "config.pending.count" });
                        var countResponseJson = await _coreClient.SendRequestAsync(countRequest);
                        var countResponse = JsonSerializer.Deserialize<JsonElement>(countResponseJson);

                        int count = 0;
                        if (countResponse.TryGetProperty("Data", out var data) ||
                            countResponse.TryGetProperty("data", out data))
                        {
                            if (data.TryGetProperty("count", out var countProp) ||
                                data.TryGetProperty("Count", out countProp))
                            {
                                count = countProp.GetInt32();
                            }
                        }

                        await _notifier.NotifyPendingCountChangedAsync(count);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to broadcast pending count update");
                    }
                });
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error communicating with Core service");
            return Ok(new { success = false, error = ex.Message });
        }
    }

    #endregion
}

public class ApplyRequest
{
    public string? AppliedBy { get; set; }
}
