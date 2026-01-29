using System.Text.Json;
using Monolith.FireWall.Common.Models;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Transport.Handlers;

/// <summary>
/// Handler for configuration management with staged changes workflow.
/// </summary>
public sealed class ConfigHandler : ICoreRequestHandler
{
    private static readonly HashSet<string> Actions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Pending changes
        "config.pending.count",
        "config.pending.list",
        "config.pending.discard",
        "config.pending.discard-all",

        // Apply changes
        "config.apply",
        "config.apply-all",
        "config.validate",

        // History
        "config.history.list",
        "config.history.target",

        // System config
        "config.system.get",
        "config.system.save",
        "config.system.save-direct",

        // Module config
        "config.module.get",
        "config.module.save",
        "config.module.save-direct"
    };

    public bool CanHandle(string action) => Actions.Contains(action);

    public async Task<ApiResponse> HandleAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        if (context.ConfigService == null)
        {
            return new ApiResponse(false, null, "Configuration service not available");
        }

        var action = request.GetProperty("action").GetString() ?? string.Empty;

        switch (action)
        {
            // Pending changes
            case "config.pending.count":
                return await HandlePendingCountAsync(context);

            case "config.pending.list":
                return await HandlePendingListAsync(context);

            case "config.pending.discard":
                return await HandlePendingDiscardAsync(context, request);

            case "config.pending.discard-all":
                return await HandlePendingDiscardAllAsync(context);

            // Apply changes
            case "config.apply":
                return await HandleApplyAsync(context, request);

            case "config.apply-all":
                return await HandleApplyAllAsync(context, request);

            case "config.validate":
                return await HandleValidateAsync(context);

            // History
            case "config.history.list":
                return await HandleHistoryListAsync(context, request);

            case "config.history.target":
                return await HandleHistoryTargetAsync(context, request);

            // System config
            case "config.system.get":
                return await HandleSystemGetAsync(context, request);

            case "config.system.save":
                return await HandleSystemSaveAsync(context, request);

            case "config.system.save-direct":
                return await HandleSystemSaveDirectAsync(context, request);

            // Module config
            case "config.module.get":
                return await HandleModuleGetAsync(context, request);

            case "config.module.save":
                return await HandleModuleSaveAsync(context, request);

            case "config.module.save-direct":
                return await HandleModuleSaveDirectAsync(context, request);
        }

        return new ApiResponse(false, null, $"Unhandled action: {action}");
    }

    #region Pending Changes Handlers

    private static async Task<ApiResponse> HandlePendingCountAsync(CoreRequestContext context)
    {
        var count = await context.ConfigService!.GetPendingCountAsync();
        return new ApiResponse(true, new { count }, null);
    }

    private static async Task<ApiResponse> HandlePendingListAsync(CoreRequestContext context)
    {
        var changes = await context.ConfigService!.GetPendingChangesAsync();
        return new ApiResponse(true, new { changes }, null);
    }

    private static async Task<ApiResponse> HandlePendingDiscardAsync(CoreRequestContext context, JsonElement request)
    {
        if (!request.TryGetProperty("changeId", out var changeIdElement))
        {
            return new ApiResponse(false, null, "changeId is required");
        }

        var changeId = changeIdElement.GetInt64();
        var success = await context.ConfigService!.DiscardPendingChangeAsync(changeId);

        return new ApiResponse(success, new { discarded = success }, success ? null : "Failed to discard change");
    }

    private static async Task<ApiResponse> HandlePendingDiscardAllAsync(CoreRequestContext context)
    {
        var count = await context.ConfigService!.DiscardAllPendingChangesAsync();
        return new ApiResponse(true, new { discardedCount = count }, null);
    }

    #endregion

    #region Apply Handlers

    private static async Task<ApiResponse> HandleApplyAsync(CoreRequestContext context, JsonElement request)
    {
        if (!request.TryGetProperty("changeId", out var changeIdElement))
        {
            return new ApiResponse(false, null, "changeId is required");
        }

        var changeId = changeIdElement.GetInt64();
        string? appliedBy = null;
        if (request.TryGetProperty("appliedBy", out var appliedByElement))
        {
            appliedBy = appliedByElement.GetString();
        }

        var result = await context.ConfigService!.ApplyPendingChangeAsync(changeId, appliedBy);

        return new ApiResponse(result.Success, new
        {
            result.Success,
            result.AppliedCount,
            result.FailedCount,
            result.RequiresRestart,
            result.RequiresReboot,
            result.Results,
            result.Error
        }, result.Success ? null : result.Error);
    }

    private static async Task<ApiResponse> HandleApplyAllAsync(CoreRequestContext context, JsonElement request)
    {
        string? appliedBy = null;
        if (request.TryGetProperty("appliedBy", out var appliedByElement))
        {
            appliedBy = appliedByElement.GetString();
        }

        var result = await context.ConfigService!.ApplyAllPendingChangesAsync(appliedBy);

        return new ApiResponse(result.Success, new
        {
            result.Success,
            result.AppliedCount,
            result.FailedCount,
            result.RequiresRestart,
            result.RequiresReboot,
            result.Results,
            result.Error
        }, result.Success ? null : result.Error);
    }

    private static async Task<ApiResponse> HandleValidateAsync(CoreRequestContext context)
    {
        var result = await context.ConfigService!.ValidatePendingChangesAsync();

        return new ApiResponse(result.IsValid, new
        {
            result.IsValid,
            result.Errors,
            result.Warnings
        }, result.IsValid ? null : "Validation failed");
    }

    #endregion

    #region History Handlers

    private static async Task<ApiResponse> HandleHistoryListAsync(CoreRequestContext context, JsonElement request)
    {
        int limit = 50;
        if (request.TryGetProperty("limit", out var limitElement))
        {
            limit = limitElement.GetInt32();
        }

        var history = await context.ConfigService!.GetHistoryAsync(limit);
        return new ApiResponse(true, new { history }, null);
    }

    private static async Task<ApiResponse> HandleHistoryTargetAsync(CoreRequestContext context, JsonElement request)
    {
        if (!request.TryGetProperty("targetType", out var targetTypeElement) ||
            !request.TryGetProperty("targetId", out var targetIdElement))
        {
            return new ApiResponse(false, null, "targetType and targetId are required");
        }

        var targetType = targetTypeElement.GetString() ?? string.Empty;
        var targetId = targetIdElement.GetString() ?? string.Empty;

        int limit = 50;
        if (request.TryGetProperty("limit", out var limitElement))
        {
            limit = limitElement.GetInt32();
        }

        var history = await context.ConfigService!.GetHistoryForTargetAsync(targetType, targetId, limit);
        return new ApiResponse(true, new { history }, null);
    }

    #endregion

    #region System Config Handlers

    private static async Task<ApiResponse> HandleSystemGetAsync(CoreRequestContext context, JsonElement request)
    {
        if (!request.TryGetProperty("key", out var keyElement))
        {
            return new ApiResponse(false, null, "key is required");
        }

        var key = keyElement.GetString() ?? string.Empty;

        // Get as raw JSON string
        var configJson = await context.ConfigService!.GetSystemConfigJsonAsync(key);
        if (string.IsNullOrEmpty(configJson))
        {
            return new ApiResponse(true, new { key, config = (object?)null }, null);
        }

        // Parse JSON to return as object
        var config = JsonSerializer.Deserialize<JsonElement>(configJson);
        return new ApiResponse(true, new { key, config }, null);
    }

    private static async Task<ApiResponse> HandleSystemSaveAsync(CoreRequestContext context, JsonElement request)
    {
        if (!request.TryGetProperty("key", out var keyElement) ||
            !request.TryGetProperty("value", out var valueElement))
        {
            return new ApiResponse(false, null, "key and value are required");
        }

        var key = keyElement.GetString() ?? string.Empty;
        string? changedBy = null;
        string? description = null;

        if (request.TryGetProperty("changedBy", out var changedByElement))
        {
            changedBy = changedByElement.GetString();
        }
        if (request.TryGetProperty("description", out var descElement))
        {
            description = descElement.GetString();
        }

        // Save value as raw JSON
        var valueJson = valueElement.GetRawText();
        var result = await context.ConfigService!.SaveSystemConfigJsonAsync(key, valueJson, changedBy, description);

        return new ApiResponse(result.Success, new
        {
            result.Success,
            result.Staged,
            result.PendingChangeId,
            result.ErrorMessage
        }, result.Success ? null : result.ErrorMessage);
    }

    private static async Task<ApiResponse> HandleSystemSaveDirectAsync(CoreRequestContext context, JsonElement request)
    {
        if (!request.TryGetProperty("key", out var keyElement) ||
            !request.TryGetProperty("value", out var valueElement))
        {
            return new ApiResponse(false, null, "key and value are required");
        }

        var key = keyElement.GetString() ?? string.Empty;
        string? changedBy = null;

        if (request.TryGetProperty("changedBy", out var changedByElement))
        {
            changedBy = changedByElement.GetString();
        }

        var valueJson = valueElement.GetRawText();
        var result = await context.ConfigService!.SaveSystemConfigJsonDirectAsync(key, valueJson, changedBy);

        return new ApiResponse(result.Success, new
        {
            result.Success,
            result.ErrorMessage
        }, result.Success ? null : result.ErrorMessage);
    }

    #endregion

    #region Module Config Handlers

    private static async Task<ApiResponse> HandleModuleGetAsync(CoreRequestContext context, JsonElement request)
    {
        if (!request.TryGetProperty("packageId", out var packageIdElement) ||
            !request.TryGetProperty("moduleId", out var moduleIdElement))
        {
            return new ApiResponse(false, null, "packageId and moduleId are required");
        }

        var packageId = packageIdElement.GetString() ?? string.Empty;
        var moduleId = moduleIdElement.GetString() ?? string.Empty;

        var configJson = await context.ConfigService!.GetModuleConfigJsonAsync(packageId, moduleId);
        if (string.IsNullOrEmpty(configJson))
        {
            return new ApiResponse(true, new { packageId, moduleId, config = (object?)null }, null);
        }

        // Parse JSON to return as object
        var config = JsonSerializer.Deserialize<JsonElement>(configJson);
        return new ApiResponse(true, new { packageId, moduleId, config }, null);
    }

    private static async Task<ApiResponse> HandleModuleSaveAsync(CoreRequestContext context, JsonElement request)
    {
        if (!request.TryGetProperty("packageId", out var packageIdElement) ||
            !request.TryGetProperty("moduleId", out var moduleIdElement) ||
            !request.TryGetProperty("value", out var valueElement))
        {
            return new ApiResponse(false, null, "packageId, moduleId, and value are required");
        }

        var packageId = packageIdElement.GetString() ?? string.Empty;
        var moduleId = moduleIdElement.GetString() ?? string.Empty;
        string? changedBy = null;
        string? description = null;

        if (request.TryGetProperty("changedBy", out var changedByElement))
        {
            changedBy = changedByElement.GetString();
        }
        if (request.TryGetProperty("description", out var descElement))
        {
            description = descElement.GetString();
        }

        var valueJson = valueElement.GetRawText();
        var result = await context.ConfigService!.SaveModuleConfigJsonAsync(packageId, moduleId, valueJson, changedBy, description);

        return new ApiResponse(result.Success, new
        {
            result.Success,
            result.Staged,
            result.PendingChangeId,
            result.ErrorMessage
        }, result.Success ? null : result.ErrorMessage);
    }

    private static async Task<ApiResponse> HandleModuleSaveDirectAsync(CoreRequestContext context, JsonElement request)
    {
        if (!request.TryGetProperty("packageId", out var packageIdElement) ||
            !request.TryGetProperty("moduleId", out var moduleIdElement) ||
            !request.TryGetProperty("value", out var valueElement))
        {
            return new ApiResponse(false, null, "packageId, moduleId, and value are required");
        }

        var packageId = packageIdElement.GetString() ?? string.Empty;
        var moduleId = moduleIdElement.GetString() ?? string.Empty;
        string? changedBy = null;

        if (request.TryGetProperty("changedBy", out var changedByElement))
        {
            changedBy = changedByElement.GetString();
        }

        var valueJson = valueElement.GetRawText();
        var result = await context.ConfigService!.SaveModuleConfigJsonDirectAsync(packageId, moduleId, valueJson, changedBy);

        return new ApiResponse(result.Success, new
        {
            result.Success,
            result.ErrorMessage
        }, result.Success ? null : result.ErrorMessage);
    }

    #endregion
}
