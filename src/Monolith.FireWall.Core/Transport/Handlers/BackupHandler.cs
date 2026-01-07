using System.Text.Json;
using Monolith.FireWall.Common.Models;
using Monolith.FireWall.Core.Models;
using Monolith.FireWall.Core.Services;

namespace Monolith.FireWall.Core.Transport.Handlers;

/// <summary>
/// Handles backup and restore requests
/// </summary>
public sealed class BackupHandler : ICoreRequestHandler
{
    private static readonly HashSet<string> Actions = new(StringComparer.OrdinalIgnoreCase)
    {
        "backup.create",
        "backup.list",
        "backup.restore",
        "backup.delete",
        "backup.info",
        "backup.settings.get",
        "backup.settings.update"
    };

    public bool CanHandle(string action) => Actions.Contains(action);

    public async Task<ApiResponse> HandleAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        if (!request.TryGetProperty("action", out var actionElement))
        {
            return new ApiResponse(false, null, "Action is required");
        }
        
        var action = actionElement.GetString() ?? string.Empty;

        try
        {
            switch (action.ToLower())
            {
                case "backup.create":
                    return await HandleCreateBackupAsync(context, request, cancellationToken);

                case "backup.list":
                    return await HandleListBackupsAsync(context, cancellationToken);

                case "backup.restore":
                    return await HandleRestoreBackupAsync(context, request, cancellationToken);

                case "backup.delete":
                    return await HandleDeleteBackupAsync(context, request, cancellationToken);

                case "backup.info":
                    return await HandleGetBackupInfoAsync(context, request, cancellationToken);

                case "backup.settings.get":
                    return await HandleGetSettingsAsync(context, cancellationToken);

                case "backup.settings.update":
                    return await HandleUpdateSettingsAsync(context, request, cancellationToken);

                default:
                    return new ApiResponse(false, null, $"Unknown backup action: {action}");
            }
        }
        catch (Exception ex)
        {
            return new ApiResponse(false, null, ex.Message);
        }
    }

    private async Task<ApiResponse> HandleCreateBackupAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        string? description = null;
        
        // Try to get payload from payload property (preferred)
        if (request.TryGetProperty("payload", out var payloadElement))
        {
            if (payloadElement.TryGetProperty("description", out var descElement))
            {
                description = descElement.GetString();
            }
            else
            {
                // Try to deserialize the whole payload
                try
                {
                    var payloadJson = payloadElement.GetRawText();
                    var createRequest = JsonSerializer.Deserialize<BackupCreateRequest>(payloadJson);
                    description = createRequest?.Description;
                }
                catch
                {
                    // Ignore
                }
            }
        }
        // Try to get payload from body property (fallback)
        else if (request.TryGetProperty("body", out var bodyElement))
        {
            if (bodyElement.TryGetProperty("description", out var descElement))
            {
                description = descElement.GetString();
            }
            else
            {
                // Try to deserialize the whole body
                try
                {
                    var bodyJson = bodyElement.GetRawText();
                    var createRequest = JsonSerializer.Deserialize<BackupCreateRequest>(bodyJson);
                    description = createRequest?.Description;
                }
                catch
                {
                    // Ignore
                }
            }
        }

        var result = await context.BackupManager.CreateBackupAsync(description, cancellationToken);
        if (!result.Success)
        {
            return new ApiResponse(false, null, result.Error);
        }

        return new ApiResponse(true, result.Backup, null);
    }

    private async Task<ApiResponse> HandleListBackupsAsync(CoreRequestContext context, CancellationToken cancellationToken)
    {
        var backups = await context.BackupManager.ListBackupsAsync(cancellationToken);
        return new ApiResponse(true, backups, null);
    }

    private async Task<ApiResponse> HandleRestoreBackupAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        string? fileName = null;
        
        // Try to get payload from payload property (preferred)
        if (request.TryGetProperty("payload", out var payloadElement))
        {
            if (payloadElement.TryGetProperty("fileName", out var fileNameElement))
            {
                fileName = fileNameElement.GetString();
            }
            else
            {
                // Try to deserialize the whole payload
                try
                {
                    var payloadJson = payloadElement.GetRawText();
                    var restoreRequest = JsonSerializer.Deserialize<BackupRestoreRequest>(payloadJson);
                    fileName = restoreRequest?.FileName;
                }
                catch
                {
                    // Ignore
                }
            }
        }
        // Try to get payload from body property (fallback)
        else if (request.TryGetProperty("body", out var bodyElement))
        {
            if (bodyElement.TryGetProperty("fileName", out var fileNameElement))
            {
                fileName = fileNameElement.GetString();
            }
            else
            {
                // Try to deserialize the whole body
                try
                {
                    var bodyJson = bodyElement.GetRawText();
                    var restoreRequest = JsonSerializer.Deserialize<BackupRestoreRequest>(bodyJson);
                    fileName = restoreRequest?.FileName;
                }
                catch
                {
                    // Ignore
                }
            }
        }

        if (string.IsNullOrEmpty(fileName))
        {
            return new ApiResponse(false, null, "Backup file name is required");
        }

        var result = await context.BackupManager.RestoreBackupAsync(fileName, cancellationToken);
        if (!result.Success)
        {
            return new ApiResponse(false, null, result.Error);
        }

        return new ApiResponse(true, new { message = result.Message }, null);
    }

    private async Task<ApiResponse> HandleDeleteBackupAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        string? fileName = null;
        
        // Try to get payload from payload property (preferred)
        if (request.TryGetProperty("payload", out var payloadElement))
        {
            if (payloadElement.TryGetProperty("fileName", out var fileNameElement))
            {
                fileName = fileNameElement.GetString();
            }
            else
            {
                // Try to deserialize the whole payload
                try
                {
                    var payloadJson = payloadElement.GetRawText();
                    var deleteRequest = JsonSerializer.Deserialize<BackupDeleteRequest>(payloadJson);
                    fileName = deleteRequest?.FileName;
                }
                catch
                {
                    // Ignore
                }
            }
        }
        // Try to get payload from body property (fallback)
        else if (request.TryGetProperty("body", out var bodyElement))
        {
            if (bodyElement.TryGetProperty("fileName", out var fileNameElement))
            {
                fileName = fileNameElement.GetString();
            }
            else
            {
                // Try to deserialize the whole body
                try
                {
                    var bodyJson = bodyElement.GetRawText();
                    var deleteRequest = JsonSerializer.Deserialize<BackupDeleteRequest>(bodyJson);
                    fileName = deleteRequest?.FileName;
                }
                catch
                {
                    // Ignore
                }
            }
        }

        if (string.IsNullOrEmpty(fileName))
        {
            return new ApiResponse(false, null, "Backup file name is required");
        }

        var result = await context.BackupManager.DeleteBackupAsync(fileName, cancellationToken);
        if (!result.Success)
        {
            return new ApiResponse(false, null, result.Error);
        }

        return new ApiResponse(true, new { message = result.Message }, null);
    }

    private async Task<ApiResponse> HandleGetBackupInfoAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        string? fileName = null;
        if (request.TryGetProperty("payload", out var payloadElement))
        {
            if (payloadElement.TryGetProperty("fileName", out var nameElement))
            {
                fileName = nameElement.GetString();
            }
        }

        if (string.IsNullOrEmpty(fileName))
        {
            return new ApiResponse(false, null, "Backup file name is required");
        }

        var info = await context.BackupManager.GetBackupInfoAsync(fileName, cancellationToken);
        if (info == null)
        {
            return new ApiResponse(false, null, "Backup not found");
        }

        return new ApiResponse(true, info, null);
    }

    private async Task<ApiResponse> HandleGetSettingsAsync(CoreRequestContext context, CancellationToken cancellationToken)
    {
        var settings = context.BackupManager.GetSettings();
        return new ApiResponse(true, settings, null);
    }

    private async Task<ApiResponse> HandleUpdateSettingsAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        BackupSettings? settings = null;
        
        // Try to get payload from payload property (preferred)
        if (request.TryGetProperty("payload", out var payloadElement))
        {
            if (payloadElement.TryGetProperty("settings", out var settingsElement))
            {
                var settingsJson = settingsElement.GetRawText();
                settings = JsonSerializer.Deserialize<BackupSettings>(settingsJson);
            }
            else
            {
                // Try to deserialize the whole payload as settings
                try
                {
                    var payloadJson = payloadElement.GetRawText();
                    settings = JsonSerializer.Deserialize<BackupSettings>(payloadJson);
                }
                catch
                {
                    // Ignore
                }
            }
        }
        // Try to get payload from body property (fallback)
        else if (request.TryGetProperty("body", out var bodyElement))
        {
            if (bodyElement.TryGetProperty("settings", out var settingsElement))
            {
                var settingsJson = settingsElement.GetRawText();
                settings = JsonSerializer.Deserialize<BackupSettings>(settingsJson);
            }
        }

        if (settings == null)
        {
            return new ApiResponse(false, null, "Backup settings are required");
        }

        var result = await context.BackupManager.UpdateSettingsAsync(settings, cancellationToken);
        if (!result)
        {
            return new ApiResponse(false, null, "Failed to update backup settings");
        }

        return new ApiResponse(true, settings, null);
    }
}
