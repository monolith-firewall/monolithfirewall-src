using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Services.Settings;

/// <summary>
/// Central service for managing all configuration with staged changes workflow.
/// Provides a unified interface for saving, reviewing, and applying settings.
/// </summary>
public interface ISettingsService
{
    #region Events

    /// <summary>
    /// Raised when the pending changes count changes.
    /// </summary>
    event EventHandler<PendingChangesEventArgs>? PendingChangesChanged;

    /// <summary>
    /// Raised when changes are applied.
    /// </summary>
    event EventHandler<ChangesAppliedEventArgs>? ChangesApplied;

    #endregion

    #region System Config

    /// <summary>
    /// Gets a system configuration value.
    /// </summary>
    Task<T?> GetSystemConfigAsync<T>(string key) where T : class;

    /// <summary>
    /// Gets a system configuration value with default.
    /// </summary>
    Task<T> GetSystemConfigAsync<T>(string key, T defaultValue) where T : class;

    /// <summary>
    /// Gets a system configuration as raw JSON string.
    /// </summary>
    Task<string?> GetSystemConfigJsonAsync(string key);

    /// <summary>
    /// Saves a system configuration (stages the change).
    /// </summary>
    Task<ChangeResult> SaveSystemConfigAsync<T>(string key, T value, string? changedBy = null, string? description = null) where T : class;

    /// <summary>
    /// Saves a system configuration from raw JSON (stages the change).
    /// </summary>
    Task<ChangeResult> SaveSystemConfigJsonAsync(string key, string valueJson, string? changedBy = null, string? description = null);

    /// <summary>
    /// Saves a system configuration directly (bypasses staging).
    /// </summary>
    Task<ChangeResult> SaveSystemConfigDirectAsync<T>(string key, T value, string? changedBy = null) where T : class;

    /// <summary>
    /// Saves a system configuration from raw JSON directly (bypasses staging).
    /// </summary>
    Task<ChangeResult> SaveSystemConfigJsonDirectAsync(string key, string valueJson, string? changedBy = null);

    #endregion

    #region Module Config

    /// <summary>
    /// Gets a module configuration.
    /// </summary>
    Task<T?> GetModuleConfigAsync<T>(string packageId, string moduleId) where T : class;

    /// <summary>
    /// Gets a module configuration with default.
    /// </summary>
    Task<T> GetModuleConfigAsync<T>(string packageId, string moduleId, T defaultValue) where T : class;

    /// <summary>
    /// Gets a module configuration as raw JSON string.
    /// </summary>
    Task<string?> GetModuleConfigJsonAsync(string packageId, string moduleId);

    /// <summary>
    /// Saves a module configuration (stages the change).
    /// </summary>
    Task<ChangeResult> SaveModuleConfigAsync<T>(string packageId, string moduleId, T value, string? changedBy = null, string? description = null) where T : class;

    /// <summary>
    /// Saves a module configuration from raw JSON (stages the change).
    /// </summary>
    Task<ChangeResult> SaveModuleConfigJsonAsync(string packageId, string moduleId, string valueJson, string? changedBy = null, string? description = null);

    /// <summary>
    /// Saves a module configuration directly (bypasses staging).
    /// </summary>
    Task<ChangeResult> SaveModuleConfigDirectAsync<T>(string packageId, string moduleId, T value, string? changedBy = null) where T : class;

    /// <summary>
    /// Saves a module configuration from raw JSON directly (bypasses staging).
    /// </summary>
    Task<ChangeResult> SaveModuleConfigJsonDirectAsync(string packageId, string moduleId, string valueJson, string? changedBy = null);

    #endregion

    #region Pending Changes

    /// <summary>
    /// Gets the count of pending changes.
    /// </summary>
    Task<int> GetPendingCountAsync();

    /// <summary>
    /// Gets all pending changes.
    /// </summary>
    Task<List<PendingChangeInfo>> GetPendingChangesAsync();

    /// <summary>
    /// Gets pending changes for a specific target.
    /// </summary>
    Task<List<PendingChangeInfo>> GetPendingChangesForTargetAsync(string targetType, string targetId);

    /// <summary>
    /// Discards a specific pending change.
    /// </summary>
    Task<bool> DiscardPendingChangeAsync(long changeId);

    /// <summary>
    /// Discards all pending changes.
    /// </summary>
    Task<int> DiscardAllPendingChangesAsync();

    /// <summary>
    /// Discards pending changes for a specific target.
    /// </summary>
    Task<int> DiscardPendingChangesForTargetAsync(string targetType, string targetId);

    #endregion

    #region Apply Changes

    /// <summary>
    /// Validates all pending changes without applying.
    /// </summary>
    Task<ValidationResult> ValidatePendingChangesAsync();

    /// <summary>
    /// Applies all pending changes.
    /// </summary>
    Task<ApplyResult> ApplyAllPendingChangesAsync(string? appliedBy = null);

    /// <summary>
    /// Applies a specific pending change.
    /// </summary>
    Task<ApplyResult> ApplyPendingChangeAsync(long changeId, string? appliedBy = null);

    /// <summary>
    /// Applies pending changes for a specific target.
    /// </summary>
    Task<ApplyResult> ApplyPendingChangesForTargetAsync(string targetType, string targetId, string? appliedBy = null);

    #endregion

    #region History

    /// <summary>
    /// Gets recent configuration change history.
    /// </summary>
    Task<List<ConfigHistoryInfo>> GetHistoryAsync(int limit = 50);

    /// <summary>
    /// Gets history for a specific target.
    /// </summary>
    Task<List<ConfigHistoryInfo>> GetHistoryForTargetAsync(string targetType, string targetId, int limit = 50);

    #endregion

    #region Config Appliers

    /// <summary>
    /// Registers a config applier for a specific config key.
    /// Use this to register appliers for specific settings like "system.hostname", "network.dns".
    /// </summary>
    void RegisterApplier(string configKey, IConfigApplier applier);

    /// <summary>
    /// Registers a config applier for all keys matching a prefix.
    /// Use this to register appliers for categories like "firewall.*" or "module.*".
    /// </summary>
    void RegisterApplierByPrefix(string keyPrefix, IConfigApplier applier);

    /// <summary>
    /// Unregisters a config applier by key.
    /// </summary>
    void UnregisterApplier(string configKey);

    /// <summary>
    /// Gets the applier for a specific config key.
    /// </summary>
    IConfigApplier? GetApplier(string configKey);

    #endregion
}

/// <summary>
/// Interface for applying configuration changes to the system.
/// Modules can implement this to handle their own config application.
/// </summary>
public interface IConfigApplier
{
    /// <summary>
    /// The target type this applier handles (e.g., "SystemConfig", "ModuleConfig").
    /// </summary>
    string TargetType { get; }

    /// <summary>
    /// Validates a configuration change before applying.
    /// </summary>
    Task<ValidationResult> ValidateAsync(string targetKey, string? currentValue, string? newValue);

    /// <summary>
    /// Applies a configuration change.
    /// </summary>
    Task<ApplyResult> ApplyAsync(string targetKey, string? previousValue, string? newValue);

    /// <summary>
    /// Rolls back a configuration change (if supported).
    /// </summary>
    Task<ApplyResult> RollbackAsync(string targetKey, string? currentValue, string? previousValue);

    /// <summary>
    /// Whether this applier supports rollback.
    /// </summary>
    bool SupportsRollback { get; }
}
