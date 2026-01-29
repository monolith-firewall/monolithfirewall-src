using System.Text.Json;
using Monolith.FireWall.Common.Interfaces;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Services.Settings;

/// <summary>
/// Central service for managing all configuration with staged changes workflow.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private readonly ILogger _logger;
    private readonly SystemConfigStore _systemConfigStore;
    private readonly ModuleConfigStore _moduleConfigStore;
    private readonly PendingChangesStore _pendingChangesStore;
    private readonly ConfigHistoryStore _historyStore;
    private readonly Dictionary<string, IConfigApplier> _appliers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IConfigApplier> _prefixAppliers = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public event EventHandler<PendingChangesEventArgs>? PendingChangesChanged;
    public event EventHandler<ChangesAppliedEventArgs>? ChangesApplied;

    public SettingsService(ILogger logger)
    {
        _logger = logger;
        _systemConfigStore = new SystemConfigStore();
        _moduleConfigStore = new ModuleConfigStore();
        _pendingChangesStore = new PendingChangesStore();
        _historyStore = new ConfigHistoryStore();
    }

    #region System Config

    public async Task<T?> GetSystemConfigAsync<T>(string key) where T : class
    {
        var entity = await _systemConfigStore.GetAsync(key);
        if (entity == null || string.IsNullOrEmpty(entity.ValueJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>(entity.ValueJson, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to deserialize system config '{key}': {ex.Message}");
            return null;
        }
    }

    public async Task<T> GetSystemConfigAsync<T>(string key, T defaultValue) where T : class
    {
        var result = await GetSystemConfigAsync<T>(key);
        return result ?? defaultValue;
    }

    public async Task<ChangeResult> SaveSystemConfigAsync<T>(string key, T value, string? changedBy = null, string? description = null) where T : class
    {
        try
        {
            var existing = await _systemConfigStore.GetAsync(key);
            var previousJson = existing?.ValueJson;
            var newJson = JsonSerializer.Serialize(value, JsonOptions);

            // Check if value actually changed
            if (previousJson == newJson)
            {
                return ChangeResult.AppliedSuccessfully();
            }

            // Create pending change
            var change = new PendingChangeEntity
            {
                ChangeType = "SystemConfig",
                TargetKey = key,
                TargetCategory = SystemConfigKeys.GetCategory(key),
                Description = description ?? $"Update system config: {key}",
                PreviousJson = previousJson,
                PendingJson = newJson,
                CreatedBy = changedBy
            };

            var added = await _pendingChangesStore.AddAsync(change);
            if (added == null)
            {
                return ChangeResult.Error("Failed to save pending change");
            }

            await RaisePendingChangesChangedAsync();

            return ChangeResult.StagedWithPendingId(added.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to save system config '{key}': {ex.Message}");
            return ChangeResult.Error(ex.Message);
        }
    }

    public async Task<ChangeResult> SaveSystemConfigDirectAsync<T>(string key, T value, string? changedBy = null) where T : class
    {
        try
        {
            var existing = await _systemConfigStore.GetAsync(key);
            var previousJson = existing?.ValueJson;
            var newJson = JsonSerializer.Serialize(value, JsonOptions);

            var entity = new SystemConfigEntity
            {
                Category = SystemConfigKeys.GetCategory(key),
                Key = key,
                ValueJson = newJson,
                UpdatedBy = changedBy
            };

            var success = await _systemConfigStore.UpsertAsync(entity);
            if (!success)
            {
                return ChangeResult.Error("Failed to save config");
            }

            // Record in history
            await _historyStore.AddAsync(new ConfigHistoryEntity
            {
                ConfigType = "system_config",
                ConfigKey = key,
                Action = existing == null ? "created" : "updated",
                OldValueJson = previousJson,
                NewValueJson = newJson,
                ChangedBy = changedBy,
                ChangeSource = "webui"
            });

            return ChangeResult.AppliedSuccessfully();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to save system config directly '{key}': {ex.Message}");
            return ChangeResult.Error(ex.Message);
        }
    }

    public async Task<string?> GetSystemConfigJsonAsync(string key)
    {
        var entity = await _systemConfigStore.GetAsync(key);
        return entity?.ValueJson;
    }

    public async Task<ChangeResult> SaveSystemConfigJsonAsync(string key, string valueJson, string? changedBy = null, string? description = null)
    {
        try
        {
            var existing = await _systemConfigStore.GetAsync(key);
            var previousJson = existing?.ValueJson;

            // Check if value actually changed
            if (previousJson == valueJson)
            {
                return ChangeResult.AppliedSuccessfully();
            }

            // Create pending change
            var change = new PendingChangeEntity
            {
                ChangeType = "SystemConfig",
                TargetKey = key,
                TargetCategory = SystemConfigKeys.GetCategory(key),
                Description = description ?? $"Update system config: {key}",
                PreviousJson = previousJson,
                PendingJson = valueJson,
                CreatedBy = changedBy
            };

            var added = await _pendingChangesStore.AddAsync(change);
            if (added == null)
            {
                return ChangeResult.Error("Failed to save pending change");
            }

            await RaisePendingChangesChangedAsync();

            return ChangeResult.StagedWithPendingId(added.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to save system config '{key}': {ex.Message}");
            return ChangeResult.Error(ex.Message);
        }
    }

    public async Task<ChangeResult> SaveSystemConfigJsonDirectAsync(string key, string valueJson, string? changedBy = null)
    {
        try
        {
            var existing = await _systemConfigStore.GetAsync(key);
            var previousJson = existing?.ValueJson;

            var entity = new SystemConfigEntity
            {
                Category = SystemConfigKeys.GetCategory(key),
                Key = key,
                ValueJson = valueJson,
                UpdatedBy = changedBy
            };

            var success = await _systemConfigStore.UpsertAsync(entity);
            if (!success)
            {
                return ChangeResult.Error("Failed to save config");
            }

            // Record in history
            await _historyStore.AddAsync(new ConfigHistoryEntity
            {
                ConfigType = "system_config",
                ConfigKey = key,
                Action = existing == null ? "created" : "updated",
                OldValueJson = previousJson,
                NewValueJson = valueJson,
                ChangedBy = changedBy,
                ChangeSource = "webui"
            });

            return ChangeResult.AppliedSuccessfully();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to save system config directly '{key}': {ex.Message}");
            return ChangeResult.Error(ex.Message);
        }
    }

    #endregion

    #region Module Config

    public async Task<T?> GetModuleConfigAsync<T>(string packageId, string moduleId) where T : class
    {
        var entity = await _moduleConfigStore.GetAsync(packageId, moduleId);
        if (entity == null || string.IsNullOrEmpty(entity.ConfigJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>(entity.ConfigJson, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to deserialize module config '{packageId}/{moduleId}': {ex.Message}");
            return null;
        }
    }

    public async Task<T> GetModuleConfigAsync<T>(string packageId, string moduleId, T defaultValue) where T : class
    {
        var result = await GetModuleConfigAsync<T>(packageId, moduleId);
        return result ?? defaultValue;
    }

    public async Task<ChangeResult> SaveModuleConfigAsync<T>(string packageId, string moduleId, T value, string? changedBy = null, string? description = null) where T : class
    {
        try
        {
            var targetKey = $"{packageId}.{moduleId}";
            var existing = await _moduleConfigStore.GetAsync(packageId, moduleId);
            var previousJson = existing?.ConfigJson;
            var newJson = JsonSerializer.Serialize(value, JsonOptions);

            // Check if value actually changed
            if (previousJson == newJson)
            {
                return ChangeResult.AppliedSuccessfully();
            }

            // Create pending change
            var change = new PendingChangeEntity
            {
                ChangeType = "ModuleConfig",
                TargetKey = targetKey,
                TargetCategory = "Modules",
                Description = description ?? $"Update module config: {targetKey}",
                PreviousJson = previousJson,
                PendingJson = newJson,
                CreatedBy = changedBy
            };

            var added = await _pendingChangesStore.AddAsync(change);
            if (added == null)
            {
                return ChangeResult.Error("Failed to save pending change");
            }

            await RaisePendingChangesChangedAsync();

            return ChangeResult.StagedWithPendingId(added.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to save module config '{packageId}/{moduleId}': {ex.Message}");
            return ChangeResult.Error(ex.Message);
        }
    }

    public async Task<ChangeResult> SaveModuleConfigDirectAsync<T>(string packageId, string moduleId, T value, string? changedBy = null) where T : class
    {
        try
        {
            var targetKey = $"{packageId}.{moduleId}";
            var existing = await _moduleConfigStore.GetAsync(packageId, moduleId);
            var previousJson = existing?.ConfigJson;
            var newJson = JsonSerializer.Serialize(value, JsonOptions);

            var entity = new ModuleConfigEntity
            {
                PackageId = packageId,
                ModuleId = moduleId,
                ConfigJson = newJson,
                UpdatedBy = changedBy
            };

            var success = await _moduleConfigStore.UpsertAsync(entity);
            if (!success)
            {
                return ChangeResult.Error("Failed to save config");
            }

            // Record in history
            await _historyStore.AddAsync(new ConfigHistoryEntity
            {
                ConfigType = "module_config",
                ConfigKey = targetKey,
                Action = existing == null ? "created" : "updated",
                OldValueJson = previousJson,
                NewValueJson = newJson,
                ChangedBy = changedBy,
                ChangeSource = "webui"
            });

            return ChangeResult.AppliedSuccessfully();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to save module config directly '{packageId}/{moduleId}': {ex.Message}");
            return ChangeResult.Error(ex.Message);
        }
    }

    public async Task<string?> GetModuleConfigJsonAsync(string packageId, string moduleId)
    {
        var entity = await _moduleConfigStore.GetAsync(packageId, moduleId);
        return entity?.ConfigJson;
    }

    public async Task<ChangeResult> SaveModuleConfigJsonAsync(string packageId, string moduleId, string valueJson, string? changedBy = null, string? description = null)
    {
        try
        {
            var targetKey = $"{packageId}.{moduleId}";
            var existing = await _moduleConfigStore.GetAsync(packageId, moduleId);
            var previousJson = existing?.ConfigJson;

            // Check if value actually changed
            if (previousJson == valueJson)
            {
                return ChangeResult.AppliedSuccessfully();
            }

            // Create pending change
            var change = new PendingChangeEntity
            {
                ChangeType = "ModuleConfig",
                TargetKey = targetKey,
                TargetCategory = "Modules",
                Description = description ?? $"Update module config: {targetKey}",
                PreviousJson = previousJson,
                PendingJson = valueJson,
                CreatedBy = changedBy
            };

            var added = await _pendingChangesStore.AddAsync(change);
            if (added == null)
            {
                return ChangeResult.Error("Failed to save pending change");
            }

            await RaisePendingChangesChangedAsync();

            return ChangeResult.StagedWithPendingId(added.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to save module config '{packageId}/{moduleId}': {ex.Message}");
            return ChangeResult.Error(ex.Message);
        }
    }

    public async Task<ChangeResult> SaveModuleConfigJsonDirectAsync(string packageId, string moduleId, string valueJson, string? changedBy = null)
    {
        try
        {
            var targetKey = $"{packageId}.{moduleId}";
            var existing = await _moduleConfigStore.GetAsync(packageId, moduleId);
            var previousJson = existing?.ConfigJson;

            var entity = new ModuleConfigEntity
            {
                PackageId = packageId,
                ModuleId = moduleId,
                ConfigJson = valueJson,
                UpdatedBy = changedBy
            };

            var success = await _moduleConfigStore.UpsertAsync(entity);
            if (!success)
            {
                return ChangeResult.Error("Failed to save config");
            }

            // Record in history
            await _historyStore.AddAsync(new ConfigHistoryEntity
            {
                ConfigType = "module_config",
                ConfigKey = targetKey,
                Action = existing == null ? "created" : "updated",
                OldValueJson = previousJson,
                NewValueJson = valueJson,
                ChangedBy = changedBy,
                ChangeSource = "webui"
            });

            return ChangeResult.AppliedSuccessfully();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to save module config directly '{packageId}/{moduleId}': {ex.Message}");
            return ChangeResult.Error(ex.Message);
        }
    }

    #endregion

    #region Pending Changes

    public async Task<int> GetPendingCountAsync()
    {
        return await _pendingChangesStore.GetPendingCountAsync();
    }

    public async Task<List<Models.PendingChangeInfo>> GetPendingChangesAsync()
    {
        var entities = await _pendingChangesStore.GetByStatusAsync("Pending");
        return entities.Select(Models.PendingChangeInfo.FromEntity).ToList();
    }

    public async Task<List<Models.PendingChangeInfo>> GetPendingChangesForTargetAsync(string targetType, string targetId)
    {
        var entities = await _pendingChangesStore.GetByTargetAsync(targetId);
        return entities.Select(Models.PendingChangeInfo.FromEntity).ToList();
    }

    public async Task<bool> DiscardPendingChangeAsync(long changeId)
    {
        var result = await _pendingChangesStore.DeleteAsync((int)changeId);
        if (result)
        {
            await RaisePendingChangesChangedAsync();
        }
        return result;
    }

    public async Task<int> DiscardAllPendingChangesAsync()
    {
        var count = await _pendingChangesStore.ClearAllPendingAsync();
        if (count > 0)
        {
            await RaisePendingChangesChangedAsync();
        }
        return count;
    }

    public async Task<int> DiscardPendingChangesForTargetAsync(string targetType, string targetId)
    {
        var count = await _pendingChangesStore.DeleteByTargetAsync(targetId);
        if (count > 0)
        {
            await RaisePendingChangesChangedAsync();
        }
        return count;
    }

    #endregion

    #region Apply Changes

    public async Task<ValidationResult> ValidatePendingChangesAsync()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        var pending = await _pendingChangesStore.GetByStatusAsync("Pending");

        foreach (var change in pending)
        {
            // Look up applier by target key (e.g., "system.hostname", "network.dns")
            var applier = GetApplier(change.TargetKey);

            if (applier != null)
            {
                var result = await applier.ValidateAsync(change.TargetKey, change.PreviousJson, change.PendingJson);
                if (!result.IsValid)
                {
                    errors.AddRange(result.Errors.Select(e => $"[{change.TargetKey}] {e}"));
                }
                warnings.AddRange(result.Warnings.Select(w => $"[{change.TargetKey}] {w}"));
            }
            else
            {
                // No applier - just warn
                warnings.Add($"[{change.TargetKey}] No applier registered, config will be saved but not applied to system");
            }
        }

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        };
    }

    public async Task<ApplyResult> ApplyAllPendingChangesAsync(string? appliedBy = null)
    {
        var pending = await _pendingChangesStore.GetByStatusAsync("Pending");
        return await ApplyChangesAsync(pending, appliedBy);
    }

    public async Task<ApplyResult> ApplyPendingChangeAsync(long changeId, string? appliedBy = null)
    {
        var change = await _pendingChangesStore.GetAsync((int)changeId);
        if (change == null || change.Status != "Pending")
        {
            return new ApplyResult { Success = false, Error = "Change not found or already applied" };
        }

        return await ApplyChangesAsync(new List<PendingChangeEntity> { change }, appliedBy);
    }

    public async Task<ApplyResult> ApplyPendingChangesForTargetAsync(string targetType, string targetId, string? appliedBy = null)
    {
        var pending = await _pendingChangesStore.GetByTargetAsync(targetId);
        return await ApplyChangesAsync(pending, appliedBy);
    }

    private async Task<ApplyResult> ApplyChangesAsync(List<PendingChangeEntity> changes, string? appliedBy)
    {
        if (changes.Count == 0)
        {
            return ApplyResult.NothingToApply();
        }

        int appliedCount = 0;
        int failedCount = 0;
        var results = new List<SingleApplyResult>();
        bool requiresRestart = false;
        bool requiresReboot = false;

        foreach (var change in changes)
        {
            try
            {
                // First, save the config to the database
                bool saved = await SaveConfigToDatabase(change);
                if (!saved)
                {
                    failedCount++;
                    results.Add(SingleApplyResult.Failed(change.Id, change.TargetKey, "Failed to save config"));
                    await _pendingChangesStore.MarkFailedAsync(change.Id, "Failed to save config");
                    continue;
                }

                // Then, apply using the applier if available (lookup by target key)
                var applier = GetApplier(change.TargetKey);

                if (applier != null)
                {
                    var applyResult = await applier.ApplyAsync(change.TargetKey, change.PreviousJson, change.PendingJson);
                    if (!applyResult.Success)
                    {
                        failedCount++;
                        results.Add(SingleApplyResult.Failed(change.Id, change.TargetKey, applyResult.Error));
                        await _pendingChangesStore.MarkFailedAsync(change.Id, applyResult.Error);
                        await _historyStore.RecordFromChangeAsync(change, false, applyResult.Error);
                        continue;
                    }
                }

                // Mark as applied
                await _pendingChangesStore.MarkAppliedAsync(change.Id, appliedBy);
                await _historyStore.RecordFromChangeAsync(change, true);
                results.Add(SingleApplyResult.Succeeded(change.Id, change.TargetKey));
                appliedCount++;

                // Track restart/reboot requirements
                if (change.RequiresRestart) requiresRestart = true;
                if (change.RequiresReboot) requiresReboot = true;
            }
            catch (Exception ex)
            {
                failedCount++;
                results.Add(SingleApplyResult.Failed(change.Id, change.TargetKey, ex.Message));
                await _pendingChangesStore.MarkFailedAsync(change.Id, ex.Message);
                await _historyStore.RecordFromChangeAsync(change, false, ex.Message);
            }
        }

        await RaisePendingChangesChangedAsync();

        ChangesApplied?.Invoke(this, new ChangesAppliedEventArgs
        {
            AppliedCount = appliedCount,
            FailedCount = failedCount,
            RequiresRestart = requiresRestart,
            RequiresReboot = requiresReboot
        });

        return new ApplyResult
        {
            Success = failedCount == 0,
            AppliedCount = appliedCount,
            FailedCount = failedCount,
            RequiresRestart = requiresRestart,
            RequiresReboot = requiresReboot,
            Results = results
        };
    }

    private async Task<bool> SaveConfigToDatabase(PendingChangeEntity change)
    {
        try
        {
            if (change.ChangeType == "SystemConfig")
            {
                var entity = new SystemConfigEntity
                {
                    Category = SystemConfigKeys.GetCategory(change.TargetKey),
                    Key = change.TargetKey,
                    ValueJson = change.PendingJson,
                    UpdatedBy = change.CreatedBy
                };
                return await _systemConfigStore.UpsertAsync(entity);
            }
            else if (change.ChangeType == "ModuleConfig")
            {
                var parts = change.TargetKey.Split('.', 2);
                if (parts.Length != 2) return false;

                var entity = new ModuleConfigEntity
                {
                    PackageId = parts[0],
                    ModuleId = parts[1],
                    ConfigJson = change.PendingJson,
                    UpdatedBy = change.CreatedBy
                };
                return await _moduleConfigStore.UpsertAsync(entity);
            }

            return true; // Unknown type, just mark as saved
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to save config to database: {ex.Message}");
            return false;
        }
    }

    #endregion

    #region History

    public async Task<List<Models.ConfigHistoryInfo>> GetHistoryAsync(int limit = 50)
    {
        var entities = await _historyStore.GetRecentAsync(limit);
        return entities.Select(Models.ConfigHistoryInfo.FromEntity).ToList();
    }

    public async Task<List<Models.ConfigHistoryInfo>> GetHistoryForTargetAsync(string targetType, string targetId, int limit = 50)
    {
        var entities = await _historyStore.GetByConfigKeyAsync(targetType, targetId, limit);
        return entities.Select(Models.ConfigHistoryInfo.FromEntity).ToList();
    }

    #endregion

    #region Config Appliers

    public void RegisterApplier(string configKey, IConfigApplier applier)
    {
        lock (_lock)
        {
            _appliers[configKey] = applier;
        }
        _logger.LogInformation($"Registered config applier for key: {configKey}");
    }

    public void RegisterApplierByPrefix(string keyPrefix, IConfigApplier applier)
    {
        lock (_lock)
        {
            _prefixAppliers[keyPrefix] = applier;
        }
        _logger.LogInformation($"Registered config applier for prefix: {keyPrefix}*");
    }

    public void UnregisterApplier(string configKey)
    {
        lock (_lock)
        {
            _appliers.Remove(configKey);
        }
    }

    public IConfigApplier? GetApplier(string configKey)
    {
        lock (_lock)
        {
            // First, try exact key match
            if (_appliers.TryGetValue(configKey, out var applier))
            {
                return applier;
            }

            // Then, try prefix match (longest prefix first)
            var matchingPrefixes = _prefixAppliers.Keys
                .Where(p => configKey.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(p => p.Length);

            foreach (var prefix in matchingPrefixes)
            {
                if (_prefixAppliers.TryGetValue(prefix, out var prefixApplier))
                {
                    return prefixApplier;
                }
            }

            return null;
        }
    }

    #endregion

    #region Helpers

    private async Task RaisePendingChangesChangedAsync()
    {
        var count = await GetPendingCountAsync();
        PendingChangesChanged?.Invoke(this, new PendingChangesEventArgs { PendingCount = count });
    }

    #endregion
}
