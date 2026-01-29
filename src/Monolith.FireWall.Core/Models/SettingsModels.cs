using CL.SQLite.Models;
using System.Text.Json.Serialization;

namespace Monolith.FireWall.Core.Models;

#region Database Entities

/// <summary>
/// Unified storage for system configuration settings.
/// Replaces scattered settings with a key-value store.
/// </summary>
[SQLiteTable("system_configs")]
[SQLiteIndex(new[] { "Category" }, Name = "idx_system_configs_category")]
public sealed class SystemConfigEntity
{
    [SQLiteColumn(IsPrimaryKey = true, IsAutoIncrement = true)]
    public int Id { get; set; }

    /// <summary>Category for grouping: network, system, webui, firewall, etc.</summary>
    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 64)]
    public string Category { get; set; } = string.Empty;

    /// <summary>Unique key: network.dns, system.hostname, etc.</summary>
    [SQLiteColumn(IsNotNull = true, IsUnique = true, DataType = SQLiteDataType.TEXT, Size = 128)]
    public string Key { get; set; } = string.Empty;

    /// <summary>JSON serialized value</summary>
    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT)]
    public string ValueJson { get; set; } = "{}";

    /// <summary>Schema version for migrations</summary>
    [SQLiteColumn(DataType = SQLiteDataType.INTEGER, DefaultValue = "1")]
    public int SchemaVersion { get; set; } = 1;

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 64)]
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Unified storage for module/package configurations.
/// Each module has one config entry with its full JSON configuration.
/// </summary>
[SQLiteTable("module_configs")]
[SQLiteIndex(new[] { "PackageId" }, Name = "idx_module_configs_package")]
public sealed class ModuleConfigEntity
{
    [SQLiteColumn(IsPrimaryKey = true, IsAutoIncrement = true)]
    public int Id { get; set; }

    /// <summary>Parent package: monolith-network, monolith-vpn, etc.</summary>
    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 128)]
    public string PackageId { get; set; } = string.Empty;

    /// <summary>Unique module identifier: monolith-network.dhcp, monolith-vpn.wireguard, etc.</summary>
    [SQLiteColumn(IsNotNull = true, IsUnique = true, DataType = SQLiteDataType.TEXT, Size = 192)]
    public string ModuleId { get; set; } = string.Empty;

    /// <summary>Full module configuration as JSON</summary>
    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT)]
    public string ConfigJson { get; set; } = "{}";

    /// <summary>Schema version for migrations</summary>
    [SQLiteColumn(DataType = SQLiteDataType.INTEGER, DefaultValue = "1")]
    public int SchemaVersion { get; set; } = 1;

    /// <summary>Whether the module is enabled</summary>
    [SQLiteColumn(DataType = SQLiteDataType.BOOLEAN, DefaultValue = "1")]
    public bool IsEnabled { get; set; } = true;

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 64)]
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Tracks unapplied configuration changes.
/// Enables staged changes workflow: save → review → apply
/// </summary>
[SQLiteTable("pending_changes")]
[SQLiteIndex(new[] { "Status" }, Name = "idx_pending_changes_status")]
[SQLiteIndex(new[] { "TargetCategory" }, Name = "idx_pending_changes_category")]
[SQLiteIndex(new[] { "CreatedAt" }, Name = "idx_pending_changes_created")]
public sealed class PendingChangeEntity
{
    [SQLiteColumn(IsPrimaryKey = true, IsAutoIncrement = true)]
    public int Id { get; set; }

    /// <summary>Type of change: SystemConfig, ModuleConfig, FirewallRule, etc.</summary>
    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 32)]
    public string ChangeType { get; set; } = string.Empty;

    /// <summary>Target key: system.hostname, monolith-network.dhcp, etc.</summary>
    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 192)]
    public string TargetKey { get; set; } = string.Empty;

    /// <summary>Category for UI grouping: Network, System, Firewall, etc.</summary>
    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 64)]
    public string TargetCategory { get; set; } = string.Empty;

    /// <summary>Human-readable description of the change</summary>
    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 512)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Snapshot of value before change (for rollback)</summary>
    [SQLiteColumn(DataType = SQLiteDataType.TEXT)]
    public string? PreviousJson { get; set; }

    /// <summary>New value to apply</summary>
    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT)]
    public string PendingJson { get; set; } = "{}";

    /// <summary>Does applying this change require a service restart?</summary>
    [SQLiteColumn(DataType = SQLiteDataType.BOOLEAN, DefaultValue = "0")]
    public bool RequiresRestart { get; set; }

    /// <summary>Does applying this change require a system reboot?</summary>
    [SQLiteColumn(DataType = SQLiteDataType.BOOLEAN, DefaultValue = "0")]
    public bool RequiresReboot { get; set; }

    /// <summary>Status: Pending, Applying, Applied, Failed, Discarded</summary>
    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 16, DefaultValue = "'Pending'")]
    public string Status { get; set; } = "Pending";

    /// <summary>Error message if status is Failed</summary>
    [SQLiteColumn(DataType = SQLiteDataType.TEXT)]
    public string? ErrorMessage { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 64)]
    public string? CreatedBy { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime? AppliedAt { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 64)]
    public string? AppliedBy { get; set; }
}

/// <summary>
/// Audit trail for all configuration changes.
/// Enables history viewing and rollback functionality.
/// </summary>
[SQLiteTable("config_history")]
[SQLiteIndex(new[] { "ConfigType", "ConfigKey" }, Name = "idx_config_history_type_key")]
[SQLiteIndex(new[] { "ChangedAt" }, Name = "idx_config_history_changed")]
public sealed class ConfigHistoryEntity
{
    [SQLiteColumn(IsPrimaryKey = true, IsAutoIncrement = true)]
    public int Id { get; set; }

    /// <summary>Type: system_config, module_config, firewall_rule, etc.</summary>
    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 32)]
    public string ConfigType { get; set; } = string.Empty;

    /// <summary>The config key or identifier</summary>
    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 192)]
    public string ConfigKey { get; set; } = string.Empty;

    /// <summary>Action: created, updated, deleted, applied, rolled_back</summary>
    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 32)]
    public string Action { get; set; } = string.Empty;

    /// <summary>Value before the change (null for created)</summary>
    [SQLiteColumn(DataType = SQLiteDataType.TEXT)]
    public string? OldValueJson { get; set; }

    /// <summary>Value after the change (null for deleted)</summary>
    [SQLiteColumn(DataType = SQLiteDataType.TEXT)]
    public string? NewValueJson { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 64)]
    public string? ChangedBy { get; set; }

    /// <summary>Source: webui, api, cli, startup, rollback</summary>
    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 32, DefaultValue = "'webui'")]
    public string ChangeSource { get; set; } = "webui";
}

#endregion

#region Enums

/// <summary>
/// Types of configuration changes that can be tracked.
/// </summary>
public enum ConfigChangeType
{
    SystemConfig,
    ModuleConfig,
    FirewallRule,
    FirewallNat,
    FirewallAlias,
    Interface,
    Route,
    Gateway
}

/// <summary>
/// Status of a pending change.
/// </summary>
public enum ChangeStatus
{
    Pending,
    Applying,
    Applied,
    Failed,
    Discarded
}

/// <summary>
/// Determines how a configuration change is handled.
/// </summary>
public enum ApplyMode
{
    /// <summary>Save to database and create pending change (default)</summary>
    Stage,

    /// <summary>Save to database and apply immediately to system</summary>
    Immediate,

    /// <summary>Only validate, don't save or apply</summary>
    ValidateOnly
}

/// <summary>
/// Actions that can be recorded in history.
/// </summary>
public enum HistoryAction
{
    Created,
    Updated,
    Deleted,
    Applied,
    RolledBack
}

/// <summary>
/// Sources of configuration changes.
/// </summary>
public enum ChangeSource
{
    WebUI,
    Api,
    Cli,
    Startup,
    Rollback,
    Migration
}

#endregion

#region View Models / DTOs

/// <summary>
/// Information about a pending change for API/UI.
/// </summary>
public sealed class PendingChangeInfo
{
    public int Id { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public string TargetKey { get; set; } = string.Empty;
    public string TargetCategory { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool RequiresRestart { get; set; }
    public bool RequiresReboot { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }

    public static PendingChangeInfo FromEntity(PendingChangeEntity entity) => new()
    {
        Id = entity.Id,
        ChangeType = entity.ChangeType,
        TargetKey = entity.TargetKey,
        TargetCategory = entity.TargetCategory,
        Description = entity.Description,
        RequiresRestart = entity.RequiresRestart,
        RequiresReboot = entity.RequiresReboot,
        Status = entity.Status,
        CreatedAt = entity.CreatedAt,
        CreatedBy = entity.CreatedBy
    };
}

/// <summary>
/// Information about a module config for API/UI.
/// </summary>
public sealed class ModuleConfigInfo
{
    public int Id { get; set; }
    public string PackageId { get; set; } = string.Empty;
    public string ModuleId { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public static ModuleConfigInfo FromEntity(ModuleConfigEntity entity) => new()
    {
        Id = entity.Id,
        PackageId = entity.PackageId,
        ModuleId = entity.ModuleId,
        IsEnabled = entity.IsEnabled,
        UpdatedAt = entity.UpdatedAt,
        UpdatedBy = entity.UpdatedBy
    };
}

/// <summary>
/// Information about a config history entry for API/UI.
/// </summary>
public sealed class ConfigHistoryInfo
{
    public int Id { get; set; }
    public string ConfigType { get; set; } = string.Empty;
    public string ConfigKey { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
    public string? ChangedBy { get; set; }
    public string ChangeSource { get; set; } = string.Empty;

    public static ConfigHistoryInfo FromEntity(ConfigHistoryEntity entity) => new()
    {
        Id = entity.Id,
        ConfigType = entity.ConfigType,
        ConfigKey = entity.ConfigKey,
        Action = entity.Action,
        ChangedAt = entity.ChangedAt,
        ChangedBy = entity.ChangedBy,
        ChangeSource = entity.ChangeSource
    };
}

/// <summary>
/// Filter for querying config history.
/// </summary>
public sealed class HistoryFilter
{
    public string? ConfigType { get; set; }
    public string? ConfigKey { get; set; }
    public string? Action { get; set; }
    public string? ChangedBy { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Limit { get; set; } = 100;
    public int Offset { get; set; } = 0;
}

/// <summary>
/// Restart requirements based on pending changes.
/// </summary>
public sealed class RestartRequirements
{
    public bool RequiresRestart { get; set; }
    public bool RequiresReboot { get; set; }
    public List<string> RestartServices { get; set; } = new();
    public List<string> RestartReasons { get; set; } = new();
    public List<string> RebootReasons { get; set; } = new();
}

#endregion

#region Result Types

/// <summary>
/// Result of a configuration change operation.
/// </summary>
public sealed class ChangeResult
{
    public bool Success { get; set; }
    public bool Staged { get; set; }
    public int? PendingChangeId { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> ValidationErrors { get; set; } = new();

    public static ChangeResult AppliedSuccessfully() => new() { Success = true, Staged = false };
    public static ChangeResult StagedWithPendingId(int pendingId) => new() { Success = true, Staged = true, PendingChangeId = pendingId };
    public static ChangeResult ValidationSuccess() => new() { Success = true };
    public static ChangeResult ValidationFailed(IEnumerable<string> errors) => new() { Success = false, ValidationErrors = errors.ToList() };
    public static ChangeResult ApplyFailed(string? error) => new() { Success = false, ErrorMessage = error };
    public static ChangeResult Error(string message) => new() { Success = false, ErrorMessage = message };
}

/// <summary>
/// Result of applying a single pending change.
/// </summary>
public sealed class SingleApplyResult
{
    public int ChangeId { get; set; }
    public string TargetKey { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }

    public static SingleApplyResult Succeeded(int id, string key) => new() { ChangeId = id, TargetKey = key, Success = true };
    public static SingleApplyResult Failed(int id, string key, string? error) => new() { ChangeId = id, TargetKey = key, Success = false, Error = error };
}

/// <summary>
/// Result of applying pending changes.
/// </summary>
public sealed class ApplyResult
{
    public bool Success { get; set; }
    public int AppliedCount { get; set; }
    public int FailedCount { get; set; }
    public bool RequiresRestart { get; set; }
    public bool RequiresReboot { get; set; }
    public List<SingleApplyResult> Results { get; set; } = new();
    public string? Error { get; set; }

    public static ApplyResult NothingToApply() => new() { Success = true, AppliedCount = 0, FailedCount = 0 };
}

/// <summary>
/// Result of a validation operation.
/// </summary>
public sealed class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    public static ValidationResult Valid() => new() { IsValid = true };
    public static ValidationResult Invalid(string error) => new() { IsValid = false, Errors = new() { error } };
    public static ValidationResult Invalid(IEnumerable<string> errors) => new() { IsValid = false, Errors = errors.ToList() };
}

/// <summary>
/// Result of a rollback operation.
/// </summary>
public sealed class RollbackResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int? NewPendingChangeId { get; set; }

    public static RollbackResult Succeeded(int? pendingId = null) => new() { Success = true, NewPendingChangeId = pendingId };
    public static RollbackResult Failed(string error) => new() { Success = false, ErrorMessage = error };
}

#endregion

#region Event Args

/// <summary>
/// Event args for pending changes count updates.
/// </summary>
public sealed class PendingChangesEventArgs : EventArgs
{
    public int PendingCount { get; set; }
    public bool RequiresRestart { get; set; }
    public bool RequiresReboot { get; set; }
}

/// <summary>
/// Event args for when changes are applied.
/// </summary>
public sealed class ChangesAppliedEventArgs : EventArgs
{
    public int AppliedCount { get; set; }
    public int FailedCount { get; set; }
    public bool RequiresRestart { get; set; }
    public bool RequiresReboot { get; set; }
    public List<string> FailedKeys { get; set; } = new();
}

#endregion

#region System Config Value Types

/// <summary>
/// DNS configuration structure.
/// </summary>
public sealed class DnsConfig
{
    [JsonPropertyName("servers")]
    public List<string> Servers { get; set; } = new();

    [JsonPropertyName("searchDomains")]
    public List<string> SearchDomains { get; set; } = new();
}

/// <summary>
/// NTP configuration structure.
/// </summary>
public sealed class NtpConfig
{
    [JsonPropertyName("servers")]
    public List<string> Servers { get; set; } = new();

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Hostname configuration structure.
/// </summary>
public sealed class HostnameConfig
{
    [JsonPropertyName("hostname")]
    public string Hostname { get; set; } = string.Empty;

    [JsonPropertyName("domain")]
    public string? Domain { get; set; }
}

/// <summary>
/// Timezone configuration structure.
/// </summary>
public sealed class TimezoneConfig
{
    [JsonPropertyName("timezone")]
    public string Timezone { get; set; } = "UTC";
}

/// <summary>
/// WebUI ports configuration structure.
/// </summary>
public sealed class WebUiPortsConfig
{
    [JsonPropertyName("httpPort")]
    public int HttpPort { get; set; } = 80;

    [JsonPropertyName("httpsPort")]
    public int HttpsPort { get; set; } = 443;
}

/// <summary>
/// WebUI bindings configuration structure.
/// </summary>
public sealed class WebUiBindingsConfig
{
    [JsonPropertyName("bindAll")]
    public bool BindAll { get; set; } = true;

    [JsonPropertyName("addresses")]
    public List<string> Addresses { get; set; } = new();
}

/// <summary>
/// IP forwarding configuration structure.
/// </summary>
public sealed class IpForwardingConfig
{
    [JsonPropertyName("ipv4")]
    public bool Ipv4 { get; set; } = true;

    [JsonPropertyName("ipv6")]
    public bool Ipv6 { get; set; } = false;
}

/// <summary>
/// Firewall defaults configuration structure.
/// </summary>
public sealed class FirewallDefaultsConfig
{
    [JsonPropertyName("inputPolicy")]
    public string InputPolicy { get; set; } = "drop";

    [JsonPropertyName("outputPolicy")]
    public string OutputPolicy { get; set; } = "accept";

    [JsonPropertyName("forwardPolicy")]
    public string ForwardPolicy { get; set; } = "drop";
}

#endregion

#region Well-known Config Keys

/// <summary>
/// Well-known system configuration keys.
/// </summary>
public static class SystemConfigKeys
{
    // System category
    public const string Hostname = "system.hostname";
    public const string Timezone = "system.timezone";

    // Network category
    public const string Dns = "network.dns";
    public const string Ntp = "network.ntp";
    public const string IpForwarding = "network.ip_forwarding";

    // WebUI category
    public const string WebUiPorts = "webui.ports";
    public const string WebUiBindings = "webui.bindings";
    public const string WebUiSession = "webui.session";

    // Firewall category
    public const string FirewallDefaults = "firewall.defaults";
    public const string FirewallLogging = "firewall.logging";

    /// <summary>
    /// Get the category from a config key.
    /// </summary>
    public static string GetCategory(string key)
    {
        var dotIndex = key.IndexOf('.');
        return dotIndex > 0 ? key[..dotIndex] : "other";
    }

    /// <summary>
    /// Get human-readable display name for a category.
    /// </summary>
    public static string GetCategoryDisplayName(string category) => category.ToLowerInvariant() switch
    {
        "system" => "System",
        "network" => "Network",
        "webui" => "Web UI",
        "firewall" => "Firewall",
        "modules" => "Modules",
        _ => category
    };
}

#endregion
