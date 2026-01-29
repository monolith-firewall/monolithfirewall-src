# Settings Management Overhaul Plan

## Executive Summary

This plan outlines a comprehensive overhaul of the Monolith Firewall settings management system, introducing:
- **Centralized Settings Service** - Single point for all configuration changes
- **Staged Changes Model** - Save changes to draft, then apply when ready
- **Pending Changes Indicator** - Visual UI element showing unapplied changes
- **New Database Schema** - Structured tables for module configs and system settings
- **Transaction Support** - Apply all changes atomically with rollback capability
- **Change History** - Full audit trail of all configuration changes

---

## Part 1: Database Schema Design

### 1.1 New Tables Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                     SETTINGS DATABASE SCHEMA                     │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌─────────────────────┐    ┌─────────────────────────────────┐ │
│  │   system_configs    │    │       module_configs            │ │
│  ├─────────────────────┤    ├─────────────────────────────────┤ │
│  │ id (PK)             │    │ id (PK)                         │ │
│  │ category            │    │ package_id                      │ │
│  │ key (unique)        │    │ module_id (unique)              │ │
│  │ value_json          │    │ config_json                     │ │
│  │ schema_version      │    │ schema_version                  │ │
│  │ updated_at          │    │ is_enabled                      │ │
│  │ updated_by          │    │ updated_at                      │ │
│  └─────────────────────┘    │ updated_by                      │ │
│                              └─────────────────────────────────┘ │
│                                                                  │
│  ┌─────────────────────────────────────────────────────────────┐ │
│  │                     pending_changes                          │ │
│  ├─────────────────────────────────────────────────────────────┤ │
│  │ id (PK)                                                      │ │
│  │ change_type (system_config | module_config | firewall | ...) │ │
│  │ target_key (the config key or module_id)                     │ │
│  │ target_category (for grouping in UI)                         │ │
│  │ description (human-readable summary)                         │ │
│  │ previous_json (snapshot before change)                       │ │
│  │ pending_json (the new value to apply)                        │ │
│  │ requires_restart (bool - service restart needed?)            │ │
│  │ requires_reboot (bool - system reboot needed?)               │ │
│  │ status (pending | applying | applied | failed | discarded)   │ │
│  │ error_message (if failed)                                    │ │
│  │ created_at                                                   │ │
│  │ created_by                                                   │ │
│  │ applied_at                                                   │ │
│  │ applied_by                                                   │ │
│  └─────────────────────────────────────────────────────────────┘ │
│                                                                  │
│  ┌─────────────────────────────────────────────────────────────┐ │
│  │                     config_history                           │ │
│  ├─────────────────────────────────────────────────────────────┤ │
│  │ id (PK)                                                      │ │
│  │ config_type (system_config | module_config)                  │ │
│  │ config_key                                                   │ │
│  │ action (created | updated | deleted | applied | rolled_back) │ │
│  │ old_value_json                                               │ │
│  │ new_value_json                                               │ │
│  │ changed_at                                                   │ │
│  │ changed_by                                                   │ │
│  │ change_source (webui | api | cli | startup)                  │ │
│  └─────────────────────────────────────────────────────────────┘ │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 1.2 System Configs Table

Replaces scattered system settings with a unified key-value store.

```csharp
[SQLiteTable("system_configs")]
public class SystemConfigEntity
{
    [SQLiteColumn("id", primaryKey: true, autoIncrement: true)]
    public int Id { get; set; }

    [SQLiteColumn("category")]
    public string Category { get; set; } = string.Empty; // "network", "system", "webui", "firewall"

    [SQLiteColumn("key", unique: true)]
    public string Key { get; set; } = string.Empty; // "network.dns", "system.hostname", etc.

    [SQLiteColumn("value_json")]
    public string ValueJson { get; set; } = "{}"; // JSON serialized value

    [SQLiteColumn("schema_version")]
    public int SchemaVersion { get; set; } = 1; // For migrations

    [SQLiteColumn("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [SQLiteColumn("updated_by")]
    public string UpdatedBy { get; set; } = string.Empty; // Username
}
```

**Predefined System Config Keys:**

| Category | Key | Value Structure |
|----------|-----|-----------------|
| system | system.hostname | `{ "hostname": "firewall" }` |
| system | system.domain | `{ "domain": "local" }` |
| system | system.timezone | `{ "timezone": "UTC" }` |
| network | network.dns | `{ "servers": ["8.8.8.8", "8.8.4.4"], "searchDomains": [] }` |
| network | network.ntp | `{ "servers": ["0.pool.ntp.org"], "enabled": true }` |
| network | network.ipv4_forwarding | `{ "enabled": true }` |
| network | network.ipv6_forwarding | `{ "enabled": false }` |
| webui | webui.ports | `{ "http": 80, "https": 443 }` |
| webui | webui.bindings | `{ "addresses": ["0.0.0.0"], "bindAll": true }` |
| webui | webui.session | `{ "timeoutMinutes": 30, "maxSessions": 10 }` |
| firewall | firewall.defaults | `{ "inputPolicy": "drop", "outputPolicy": "accept", "forwardPolicy": "drop" }` |
| firewall | firewall.logging | `{ "enabled": true, "level": "info" }` |

### 1.3 Module Configs Table

Unified storage for all package/module configurations.

```csharp
[SQLiteTable("module_configs")]
public class ModuleConfigEntity
{
    [SQLiteColumn("id", primaryKey: true, autoIncrement: true)]
    public int Id { get; set; }

    [SQLiteColumn("package_id")]
    public string PackageId { get; set; } = string.Empty; // "monolith-network"

    [SQLiteColumn("module_id", unique: true)]
    public string ModuleId { get; set; } = string.Empty; // "monolith-network.dhcp"

    [SQLiteColumn("config_json")]
    public string ConfigJson { get; set; } = "{}"; // Full module config

    [SQLiteColumn("schema_version")]
    public int SchemaVersion { get; set; } = 1;

    [SQLiteColumn("is_enabled")]
    public bool IsEnabled { get; set; } = true;

    [SQLiteColumn("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [SQLiteColumn("updated_by")]
    public string UpdatedBy { get; set; } = string.Empty;
}
```

**Example Module Configs:**

```json
// Module: monolith-network.dhcp
{
  "enabled": true,
  "interfaces": {
    "lan": {
      "enabled": true,
      "rangeStart": "192.168.1.100",
      "rangeEnd": "192.168.1.200",
      "leaseTime": 86400,
      "gateway": "192.168.1.1",
      "dnsServers": ["192.168.1.1"]
    }
  },
  "staticLeases": [
    { "mac": "00:11:22:33:44:55", "ip": "192.168.1.50", "hostname": "printer" }
  ]
}

// Module: monolith-network.dns
{
  "enabled": true,
  "listenAddresses": ["127.0.0.1", "192.168.1.1"],
  "forwarders": ["8.8.8.8", "8.8.4.4"],
  "localZones": [
    { "name": "local", "type": "static" }
  ],
  "dnssec": true
}
```

### 1.4 Pending Changes Table

Tracks all unapplied configuration changes.

```csharp
[SQLiteTable("pending_changes")]
public class PendingChangeEntity
{
    [SQLiteColumn("id", primaryKey: true, autoIncrement: true)]
    public int Id { get; set; }

    [SQLiteColumn("change_type")]
    public string ChangeType { get; set; } = string.Empty; // ConfigChangeType enum as string

    [SQLiteColumn("target_key")]
    public string TargetKey { get; set; } = string.Empty; // "system.hostname" or "monolith-network.dhcp"

    [SQLiteColumn("target_category")]
    public string TargetCategory { get; set; } = string.Empty; // For UI grouping

    [SQLiteColumn("description")]
    public string Description { get; set; } = string.Empty; // "Changed hostname to 'gateway'"

    [SQLiteColumn("previous_json")]
    public string PreviousJson { get; set; } = "{}"; // Snapshot before change

    [SQLiteColumn("pending_json")]
    public string PendingJson { get; set; } = "{}"; // New value to apply

    [SQLiteColumn("requires_restart")]
    public bool RequiresRestart { get; set; } = false;

    [SQLiteColumn("requires_reboot")]
    public bool RequiresReboot { get; set; } = false;

    [SQLiteColumn("status")]
    public string Status { get; set; } = "pending"; // pending, applying, applied, failed, discarded

    [SQLiteColumn("error_message")]
    public string? ErrorMessage { get; set; }

    [SQLiteColumn("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [SQLiteColumn("created_by")]
    public string CreatedBy { get; set; } = string.Empty;

    [SQLiteColumn("applied_at")]
    public DateTime? AppliedAt { get; set; }

    [SQLiteColumn("applied_by")]
    public string? AppliedBy { get; set; }
}

public enum ConfigChangeType
{
    SystemConfig,    // Changes to system_configs table
    ModuleConfig,    // Changes to module_configs table
    FirewallRule,    // Firewall rule changes
    FirewallNat,     // NAT rule changes
    FirewallAlias,   // Alias changes
    Interface,       // Network interface changes
    Route,           // Static route changes
    Gateway          // Gateway changes
}

public enum ChangeStatus
{
    Pending,
    Applying,
    Applied,
    Failed,
    Discarded
}
```

### 1.5 Config History Table

Audit trail for all configuration changes.

```csharp
[SQLiteTable("config_history")]
public class ConfigHistoryEntity
{
    [SQLiteColumn("id", primaryKey: true, autoIncrement: true)]
    public int Id { get; set; }

    [SQLiteColumn("config_type")]
    public string ConfigType { get; set; } = string.Empty;

    [SQLiteColumn("config_key")]
    public string ConfigKey { get; set; } = string.Empty;

    [SQLiteColumn("action")]
    public string Action { get; set; } = string.Empty; // created, updated, deleted, applied, rolled_back

    [SQLiteColumn("old_value_json")]
    public string? OldValueJson { get; set; }

    [SQLiteColumn("new_value_json")]
    public string? NewValueJson { get; set; }

    [SQLiteColumn("changed_at")]
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    [SQLiteColumn("changed_by")]
    public string ChangedBy { get; set; } = string.Empty;

    [SQLiteColumn("change_source")]
    public string ChangeSource { get; set; } = "webui"; // webui, api, cli, startup, rollback
}
```

---

## Part 2: Central Settings Service Architecture

### 2.1 Service Overview

```
┌──────────────────────────────────────────────────────────────────────────┐
│                        CENTRAL SETTINGS SERVICE                           │
├──────────────────────────────────────────────────────────────────────────┤
│                                                                           │
│  ┌─────────────────────────────────────────────────────────────────────┐ │
│  │                      ISettingsService                                │ │
│  │  (Main entry point for all settings operations)                      │ │
│  ├─────────────────────────────────────────────────────────────────────┤ │
│  │  // System configs                                                   │ │
│  │  GetSystemConfigAsync(key) → ConfigValue                            │ │
│  │  SetSystemConfigAsync(key, value, applyImmediately?) → ChangeResult │ │
│  │                                                                      │ │
│  │  // Module configs                                                   │ │
│  │  GetModuleConfigAsync(moduleId) → ModuleConfig                      │ │
│  │  SetModuleConfigAsync(moduleId, config, applyImmediately?)          │ │
│  │                                                                      │ │
│  │  // Pending changes                                                  │ │
│  │  GetPendingChangesAsync() → List<PendingChange>                     │ │
│  │  GetPendingCountAsync() → int                                       │ │
│  │  ApplyAllPendingAsync() → ApplyResult                               │ │
│  │  ApplyPendingAsync(changeId) → ApplyResult                          │ │
│  │  DiscardPendingAsync(changeId) → bool                               │ │
│  │  DiscardAllPendingAsync() → bool                                    │ │
│  │                                                                      │ │
│  │  // History                                                          │ │
│  │  GetHistoryAsync(filter) → List<ConfigHistory>                      │ │
│  │  RollbackToAsync(historyId) → RollbackResult                        │ │
│  └─────────────────────────────────────────────────────────────────────┘ │
│                              │                                            │
│                              ▼                                            │
│  ┌─────────────────────────────────────────────────────────────────────┐ │
│  │                    IConfigApplier Registry                           │ │
│  │  (Each config type has a registered applier)                         │ │
│  ├─────────────────────────────────────────────────────────────────────┤ │
│  │  SystemConfigAppliers:                                               │ │
│  │    "system.hostname"    → HostnameApplier                           │ │
│  │    "system.timezone"    → TimezoneApplier                           │ │
│  │    "network.dns"        → DnsApplier                                │ │
│  │    "network.ntp"        → NtpApplier                                │ │
│  │    "webui.ports"        → WebUiPortsApplier                         │ │
│  │                                                                      │ │
│  │  ModuleConfigAppliers:                                               │ │
│  │    "monolith-network.dhcp" → DhcpConfigApplier                      │ │
│  │    "monolith-network.dns"  → DnsServiceApplier                      │ │
│  │    "monolith-vpn.wireguard" → WireguardApplier                      │ │
│  │                                                                      │ │
│  │  SpecialAppliers:                                                    │ │
│  │    FirewallRuleApplier                                              │ │
│  │    InterfaceConfigApplier                                           │ │
│  │    RouteApplier                                                     │ │
│  └─────────────────────────────────────────────────────────────────────┘ │
│                                                                           │
└──────────────────────────────────────────────────────────────────────────┘
```

### 2.2 Core Interfaces

```csharp
/// <summary>
/// Central service for all configuration management
/// </summary>
public interface ISettingsService
{
    // ═══════════════════════════════════════════════════════════════════
    // SYSTEM CONFIGURATION
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Get a system config value</summary>
    Task<T?> GetSystemConfigAsync<T>(string key) where T : class;

    /// <summary>Get a system config as raw JSON</summary>
    Task<string?> GetSystemConfigJsonAsync(string key);

    /// <summary>Set a system config value</summary>
    /// <param name="applyMode">How to handle the change</param>
    Task<ChangeResult> SetSystemConfigAsync<T>(string key, T value, ApplyMode applyMode = ApplyMode.Stage) where T : class;

    /// <summary>Get all system configs in a category</summary>
    Task<Dictionary<string, string>> GetSystemConfigsByCategoryAsync(string category);

    // ═══════════════════════════════════════════════════════════════════
    // MODULE CONFIGURATION
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Get a module's configuration</summary>
    Task<T?> GetModuleConfigAsync<T>(string moduleId) where T : class;

    /// <summary>Get module config as raw JSON</summary>
    Task<string?> GetModuleConfigJsonAsync(string moduleId);

    /// <summary>Set a module's configuration</summary>
    Task<ChangeResult> SetModuleConfigAsync<T>(string moduleId, T config, ApplyMode applyMode = ApplyMode.Stage) where T : class;

    /// <summary>Enable or disable a module</summary>
    Task<ChangeResult> SetModuleEnabledAsync(string moduleId, bool enabled, ApplyMode applyMode = ApplyMode.Stage);

    /// <summary>Get all module configs for a package</summary>
    Task<List<ModuleConfigInfo>> GetModuleConfigsByPackageAsync(string packageId);

    // ═══════════════════════════════════════════════════════════════════
    // PENDING CHANGES MANAGEMENT
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Get count of pending changes</summary>
    Task<int> GetPendingCountAsync();

    /// <summary>Get all pending changes</summary>
    Task<List<PendingChangeInfo>> GetPendingChangesAsync();

    /// <summary>Get pending changes grouped by category</summary>
    Task<Dictionary<string, List<PendingChangeInfo>>> GetPendingChangesByCategory();

    /// <summary>Apply all pending changes</summary>
    Task<ApplyResult> ApplyAllPendingAsync(string appliedBy);

    /// <summary>Apply specific pending changes</summary>
    Task<ApplyResult> ApplyPendingAsync(int[] changeIds, string appliedBy);

    /// <summary>Discard specific pending changes</summary>
    Task<bool> DiscardPendingAsync(int[] changeIds);

    /// <summary>Discard all pending changes</summary>
    Task<bool> DiscardAllPendingAsync();

    /// <summary>Check if there are any changes requiring restart/reboot</summary>
    Task<RestartRequirements> GetRestartRequirementsAsync();

    // ═══════════════════════════════════════════════════════════════════
    // HISTORY & ROLLBACK
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Get configuration history</summary>
    Task<List<ConfigHistoryInfo>> GetHistoryAsync(HistoryFilter? filter = null);

    /// <summary>Rollback to a specific history point</summary>
    Task<RollbackResult> RollbackToAsync(int historyId, string rolledBackBy);

    // ═══════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Fired when pending changes count changes</summary>
    event EventHandler<PendingChangesEventArgs>? PendingChangesChanged;

    /// <summary>Fired when changes are applied</summary>
    event EventHandler<ChangesAppliedEventArgs>? ChangesApplied;
}

/// <summary>
/// Determines how a configuration change is handled
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
```

### 2.3 Config Applier Interface

```csharp
/// <summary>
/// Interface for components that can apply configuration changes to the system
/// </summary>
public interface IConfigApplier
{
    /// <summary>The config key(s) this applier handles</summary>
    string[] SupportedKeys { get; }

    /// <summary>Display name for UI</summary>
    string DisplayName { get; }

    /// <summary>Category for grouping</summary>
    string Category { get; }

    /// <summary>Does applying this config require a service restart?</summary>
    bool RequiresRestart { get; }

    /// <summary>Does applying this config require a system reboot?</summary>
    bool RequiresReboot { get; }

    /// <summary>Validate a configuration before applying</summary>
    Task<ValidationResult> ValidateAsync(string configJson);

    /// <summary>Apply configuration to the system</summary>
    Task<ApplyResult> ApplyAsync(string configJson);

    /// <summary>Get the current applied state from the system</summary>
    Task<string> GetCurrentStateAsync();

    /// <summary>Generate a human-readable description of changes</summary>
    string DescribeChange(string oldJson, string newJson);
}

/// <summary>
/// Base class for module config appliers
/// </summary>
public abstract class ModuleConfigApplier : IConfigApplier
{
    public abstract string ModuleId { get; }
    public string[] SupportedKeys => new[] { ModuleId };
    public abstract string DisplayName { get; }
    public virtual string Category => "Modules";
    public virtual bool RequiresRestart => false;
    public virtual bool RequiresReboot => false;

    public abstract Task<ValidationResult> ValidateAsync(string configJson);
    public abstract Task<ApplyResult> ApplyAsync(string configJson);
    public abstract Task<string> GetCurrentStateAsync();

    public virtual string DescribeChange(string oldJson, string newJson)
    {
        return $"Updated {DisplayName} configuration";
    }
}
```

### 2.4 Settings Service Implementation

```csharp
public class SettingsService : ISettingsService
{
    private readonly ISystemConfigStore _systemConfigStore;
    private readonly IModuleConfigStore _moduleConfigStore;
    private readonly IPendingChangesStore _pendingChangesStore;
    private readonly IConfigHistoryStore _historyStore;
    private readonly IConfigApplierRegistry _applierRegistry;
    private readonly ILogger<SettingsService> _logger;

    public event EventHandler<PendingChangesEventArgs>? PendingChangesChanged;
    public event EventHandler<ChangesAppliedEventArgs>? ChangesApplied;

    public async Task<ChangeResult> SetSystemConfigAsync<T>(
        string key,
        T value,
        ApplyMode applyMode = ApplyMode.Stage) where T : class
    {
        var json = JsonSerializer.Serialize(value);
        var applier = _applierRegistry.GetApplier(key);

        // Step 1: Validate
        if (applier != null)
        {
            var validation = await applier.ValidateAsync(json);
            if (!validation.IsValid)
            {
                return ChangeResult.ValidationFailed(validation.Errors);
            }
        }

        if (applyMode == ApplyMode.ValidateOnly)
        {
            return ChangeResult.ValidationSuccess();
        }

        // Step 2: Get current value for history/rollback
        var existing = await _systemConfigStore.GetAsync(key);
        var previousJson = existing?.ValueJson ?? "{}";

        // Step 3: Save to database
        var entity = existing ?? new SystemConfigEntity { Key = key };
        entity.ValueJson = json;
        entity.Category = GetCategoryFromKey(key);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = GetCurrentUser();

        await _systemConfigStore.UpsertAsync(entity);

        // Step 4: Record history
        await _historyStore.AddAsync(new ConfigHistoryEntity
        {
            ConfigType = "system_config",
            ConfigKey = key,
            Action = existing == null ? "created" : "updated",
            OldValueJson = previousJson,
            NewValueJson = json,
            ChangedBy = entity.UpdatedBy,
            ChangeSource = "webui"
        });

        // Step 5: Handle apply mode
        if (applyMode == ApplyMode.Immediate)
        {
            // Apply immediately
            if (applier != null)
            {
                var applyResult = await applier.ApplyAsync(json);
                if (!applyResult.Success)
                {
                    return ChangeResult.ApplyFailed(applyResult.Error);
                }
            }
            return ChangeResult.AppliedSuccessfully();
        }
        else // Stage mode
        {
            // Create pending change
            var pendingChange = new PendingChangeEntity
            {
                ChangeType = ConfigChangeType.SystemConfig.ToString(),
                TargetKey = key,
                TargetCategory = entity.Category,
                Description = applier?.DescribeChange(previousJson, json) ?? $"Changed {key}",
                PreviousJson = previousJson,
                PendingJson = json,
                RequiresRestart = applier?.RequiresRestart ?? false,
                RequiresReboot = applier?.RequiresReboot ?? false,
                Status = ChangeStatus.Pending.ToString(),
                CreatedBy = entity.UpdatedBy
            };

            await _pendingChangesStore.AddAsync(pendingChange);

            // Notify listeners
            PendingChangesChanged?.Invoke(this, new PendingChangesEventArgs
            {
                PendingCount = await GetPendingCountAsync()
            });

            return ChangeResult.Staged(pendingChange.Id);
        }
    }

    public async Task<ApplyResult> ApplyAllPendingAsync(string appliedBy)
    {
        var pending = await _pendingChangesStore.GetByStatusAsync(ChangeStatus.Pending);
        if (!pending.Any())
        {
            return ApplyResult.NothingToApply();
        }

        var results = new List<SingleApplyResult>();
        var appliedIds = new List<int>();
        var failedIds = new List<int>();
        var requiresRestart = false;
        var requiresReboot = false;

        foreach (var change in pending)
        {
            change.Status = ChangeStatus.Applying.ToString();
            await _pendingChangesStore.UpdateAsync(change);

            try
            {
                var applier = _applierRegistry.GetApplier(change.TargetKey);
                if (applier != null)
                {
                    var result = await applier.ApplyAsync(change.PendingJson);
                    if (result.Success)
                    {
                        change.Status = ChangeStatus.Applied.ToString();
                        change.AppliedAt = DateTime.UtcNow;
                        change.AppliedBy = appliedBy;
                        appliedIds.Add(change.Id);

                        if (change.RequiresRestart) requiresRestart = true;
                        if (change.RequiresReboot) requiresReboot = true;

                        results.Add(SingleApplyResult.Success(change.Id, change.TargetKey));
                    }
                    else
                    {
                        change.Status = ChangeStatus.Failed.ToString();
                        change.ErrorMessage = result.Error;
                        failedIds.Add(change.Id);
                        results.Add(SingleApplyResult.Failed(change.Id, change.TargetKey, result.Error));
                    }
                }
                else
                {
                    // No applier - just mark as applied (config-only change)
                    change.Status = ChangeStatus.Applied.ToString();
                    change.AppliedAt = DateTime.UtcNow;
                    change.AppliedBy = appliedBy;
                    appliedIds.Add(change.Id);
                    results.Add(SingleApplyResult.Success(change.Id, change.TargetKey));
                }
            }
            catch (Exception ex)
            {
                change.Status = ChangeStatus.Failed.ToString();
                change.ErrorMessage = ex.Message;
                failedIds.Add(change.Id);
                results.Add(SingleApplyResult.Failed(change.Id, change.TargetKey, ex.Message));
            }

            await _pendingChangesStore.UpdateAsync(change);
        }

        // Record history for batch apply
        await _historyStore.AddAsync(new ConfigHistoryEntity
        {
            ConfigType = "batch_apply",
            ConfigKey = "batch",
            Action = "applied",
            NewValueJson = JsonSerializer.Serialize(new { applied = appliedIds, failed = failedIds }),
            ChangedBy = appliedBy,
            ChangeSource = "webui"
        });

        // Notify listeners
        PendingChangesChanged?.Invoke(this, new PendingChangesEventArgs
        {
            PendingCount = await GetPendingCountAsync()
        });

        ChangesApplied?.Invoke(this, new ChangesAppliedEventArgs
        {
            AppliedCount = appliedIds.Count,
            FailedCount = failedIds.Count,
            RequiresRestart = requiresRestart,
            RequiresReboot = requiresReboot
        });

        return new ApplyResult
        {
            Success = !failedIds.Any(),
            AppliedCount = appliedIds.Count,
            FailedCount = failedIds.Count,
            Results = results,
            RequiresRestart = requiresRestart,
            RequiresReboot = requiresReboot
        };
    }
}
```

---

## Part 3: UI Component - Pending Changes Indicator

### 3.1 Design Mockup

```
┌────────────────────────────────────────────────────────────────────────────────┐
│  ┌──────┐                                              ┌─────┐ ┌──────────────┐│
│  │ LOGO │  Dashboard   Firewall   Network   System     │ ⚙️3 │ │ 👤 admin  ▼ ││
│  └──────┘                                              └─────┘ └──────────────┘│
├────────────────────────────────────────────────────────────────────────────────┤
│                                                                                 │
│  When badge clicked, dropdown appears:                                          │
│                                                                                 │
│  ┌─────────────────────────────────────────────────────────────────────────┐   │
│  │  ⚙️ Pending Changes (3)                                        [Apply All] │   │
│  ├─────────────────────────────────────────────────────────────────────────┤   │
│  │                                                                          │   │
│  │  🌐 Network                                                              │   │
│  │  ├─ Changed DNS servers                               [Apply] [Discard] │   │
│  │  └─ Updated NTP configuration                         [Apply] [Discard] │   │
│  │                                                                          │   │
│  │  🔥 Firewall                                                             │   │
│  │  └─ Modified 2 firewall rules                         [Apply] [Discard] │   │
│  │                                                                          │   │
│  │  ⚠️ Requires service restart after applying                              │   │
│  │                                                                          │   │
│  ├─────────────────────────────────────────────────────────────────────────┤   │
│  │  [View All Changes]                              [Discard All]          │   │
│  └─────────────────────────────────────────────────────────────────────────┘   │
│                                                                                 │
└────────────────────────────────────────────────────────────────────────────────┘
```

### 3.2 HTML Structure

```html
<!-- In _Layout.cshtml or top-navbar -->
<div class="nav-item dropdown" id="pending-changes-indicator">
    <a class="nav-link position-relative" href="#" role="button"
       data-bs-toggle="dropdown" aria-expanded="false" id="pending-changes-toggle">
        <i class="fa-solid fa-gear"></i>
        <span class="position-absolute top-0 start-100 translate-middle badge rounded-pill bg-warning text-dark d-none"
              id="pending-changes-badge">
            0
        </span>
    </a>

    <div class="dropdown-menu dropdown-menu-end pending-changes-dropdown"
         id="pending-changes-dropdown" style="min-width: 400px;">

        <!-- Header -->
        <div class="dropdown-header d-flex justify-content-between align-items-center">
            <span><i class="fa-solid fa-gear me-2"></i>Pending Changes</span>
            <button class="btn btn-primary btn-sm" id="btn-apply-all-changes">
                <i class="fa-solid fa-check me-1"></i>Apply All
            </button>
        </div>

        <div class="dropdown-divider"></div>

        <!-- Changes list (populated by JS) -->
        <div id="pending-changes-list" class="pending-changes-list">
            <!-- Will be populated dynamically -->
        </div>

        <!-- Warnings -->
        <div id="pending-changes-warnings" class="px-3 py-2 d-none">
            <div class="alert alert-warning mb-0 py-2 small">
                <i class="fa-solid fa-triangle-exclamation me-1"></i>
                <span id="pending-changes-warning-text"></span>
            </div>
        </div>

        <div class="dropdown-divider"></div>

        <!-- Footer -->
        <div class="dropdown-footer d-flex justify-content-between px-3 py-2">
            <a href="/system/pending-changes" class="btn btn-outline-secondary btn-sm">
                View All
            </a>
            <button class="btn btn-outline-danger btn-sm" id="btn-discard-all-changes">
                Discard All
            </button>
        </div>
    </div>
</div>
```

### 3.3 JavaScript Implementation

```javascript
// /js/core/pending-changes.js

var Monolith = window.Monolith || {};

Monolith.PendingChanges = {
    pollInterval: null,
    pollIntervalMs: 5000, // Poll every 5 seconds
    lastCount: 0,

    init: function() {
        this.bindEvents();
        this.startPolling();
        this.loadPendingChanges();
    },

    bindEvents: function() {
        $('#btn-apply-all-changes').off('click').on('click', () => this.applyAll());
        $('#btn-discard-all-changes').off('click').on('click', () => this.discardAll());

        $(document).off('click', '.btn-apply-single').on('click', '.btn-apply-single', (e) => {
            const id = $(e.currentTarget).data('id');
            this.applySingle(id);
        });

        $(document).off('click', '.btn-discard-single').on('click', '.btn-discard-single', (e) => {
            const id = $(e.currentTarget).data('id');
            this.discardSingle(id);
        });

        // Refresh when dropdown opens
        $('#pending-changes-toggle').off('show.bs.dropdown').on('show.bs.dropdown', () => {
            this.loadPendingChanges();
        });
    },

    startPolling: function() {
        if (this.pollInterval) clearInterval(this.pollInterval);
        this.pollInterval = setInterval(() => this.checkPendingCount(), this.pollIntervalMs);
    },

    stopPolling: function() {
        if (this.pollInterval) {
            clearInterval(this.pollInterval);
            this.pollInterval = null;
        }
    },

    checkPendingCount: async function() {
        try {
            const response = await Monolith.API.get('/api/settings/pending/count');
            const count = response.Data?.count || response.data?.count || 0;
            this.updateBadge(count);

            // If count changed, refresh the list if dropdown is open
            if (count !== this.lastCount) {
                this.lastCount = count;
                if ($('#pending-changes-dropdown').hasClass('show')) {
                    this.loadPendingChanges();
                }
            }
        } catch (error) {
            console.error('Failed to check pending count:', error);
        }
    },

    updateBadge: function(count) {
        const badge = $('#pending-changes-badge');
        if (count > 0) {
            badge.text(count > 99 ? '99+' : count).removeClass('d-none');
            // Add subtle animation
            badge.addClass('pulse-animation');
            setTimeout(() => badge.removeClass('pulse-animation'), 300);
        } else {
            badge.addClass('d-none');
        }
    },

    loadPendingChanges: async function() {
        try {
            const response = await Monolith.API.get('/api/settings/pending');
            const changes = response.Data || response.data || [];
            this.renderChanges(changes);
        } catch (error) {
            console.error('Failed to load pending changes:', error);
            this.renderError();
        }
    },

    renderChanges: function(changes) {
        const container = $('#pending-changes-list');
        const warningsContainer = $('#pending-changes-warnings');
        const warningText = $('#pending-changes-warning-text');

        if (!changes || changes.length === 0) {
            container.html(`
                <div class="text-center text-muted py-4">
                    <i class="fa-solid fa-check-circle fa-2x mb-2"></i>
                    <p class="mb-0">No pending changes</p>
                </div>
            `);
            $('#btn-apply-all-changes, #btn-discard-all-changes').prop('disabled', true);
            warningsContainer.addClass('d-none');
            return;
        }

        $('#btn-apply-all-changes, #btn-discard-all-changes').prop('disabled', false);

        // Group by category
        const grouped = this.groupByCategory(changes);

        let html = '';
        for (const [category, categoryChanges] of Object.entries(grouped)) {
            html += `
                <div class="pending-category">
                    <div class="pending-category-header px-3 py-1 bg-light">
                        <small class="fw-bold text-muted">
                            ${this.getCategoryIcon(category)} ${category}
                        </small>
                    </div>
                    <div class="pending-category-items">
            `;

            for (const change of categoryChanges) {
                html += `
                    <div class="pending-change-item d-flex justify-content-between align-items-center px-3 py-2">
                        <div class="pending-change-info">
                            <div class="pending-change-desc">${this.escapeHtml(change.description)}</div>
                            <small class="text-muted">${this.formatTime(change.createdAt)}</small>
                        </div>
                        <div class="pending-change-actions btn-group btn-group-sm">
                            <button class="btn btn-outline-success btn-apply-single"
                                    data-id="${change.id}" title="Apply">
                                <i class="fa-solid fa-check"></i>
                            </button>
                            <button class="btn btn-outline-danger btn-discard-single"
                                    data-id="${change.id}" title="Discard">
                                <i class="fa-solid fa-times"></i>
                            </button>
                        </div>
                    </div>
                `;
            }

            html += '</div></div>';
        }

        container.html(html);

        // Show warnings if needed
        const requiresRestart = changes.some(c => c.requiresRestart);
        const requiresReboot = changes.some(c => c.requiresReboot);

        if (requiresRestart || requiresReboot) {
            let warning = '';
            if (requiresReboot) {
                warning = 'System reboot required after applying';
            } else if (requiresRestart) {
                warning = 'Service restart required after applying';
            }
            warningText.text(warning);
            warningsContainer.removeClass('d-none');
        } else {
            warningsContainer.addClass('d-none');
        }
    },

    groupByCategory: function(changes) {
        return changes.reduce((groups, change) => {
            const category = change.targetCategory || 'Other';
            if (!groups[category]) groups[category] = [];
            groups[category].push(change);
            return groups;
        }, {});
    },

    getCategoryIcon: function(category) {
        const icons = {
            'Network': '<i class="fa-solid fa-network-wired"></i>',
            'System': '<i class="fa-solid fa-server"></i>',
            'Firewall': '<i class="fa-solid fa-shield-halved"></i>',
            'WebUI': '<i class="fa-solid fa-desktop"></i>',
            'Modules': '<i class="fa-solid fa-puzzle-piece"></i>',
            'Other': '<i class="fa-solid fa-cog"></i>'
        };
        return icons[category] || icons['Other'];
    },

    applyAll: async function() {
        if (!confirm('Apply all pending changes? This may affect system connectivity.')) {
            return;
        }

        try {
            Monolith.UI.showLoading('#pending-changes-list');
            const response = await Monolith.API.post('/api/settings/pending/apply-all');

            if (response.Success || response.success) {
                const data = response.Data || response.data || {};
                Monolith.UI.toast(
                    `Applied ${data.appliedCount || 0} changes successfully`,
                    'success'
                );

                if (data.requiresRestart) {
                    this.showRestartPrompt();
                } else if (data.requiresReboot) {
                    this.showRebootPrompt();
                }

                this.loadPendingChanges();
            } else {
                Monolith.UI.toast(
                    response.Error || response.error || 'Failed to apply changes',
                    'error'
                );
            }
        } catch (error) {
            Monolith.UI.toast('Failed to apply changes: ' + error.message, 'error');
        }
    },

    applySingle: async function(id) {
        try {
            const response = await Monolith.API.post('/api/settings/pending/apply', { ids: [id] });
            if (response.Success || response.success) {
                Monolith.UI.toast('Change applied successfully', 'success');
                this.loadPendingChanges();
            } else {
                Monolith.UI.toast(response.Error || 'Failed to apply change', 'error');
            }
        } catch (error) {
            Monolith.UI.toast('Failed to apply change', 'error');
        }
    },

    discardAll: async function() {
        if (!confirm('Discard all pending changes? This cannot be undone.')) {
            return;
        }

        try {
            const response = await Monolith.API.post('/api/settings/pending/discard-all');
            if (response.Success || response.success) {
                Monolith.UI.toast('All changes discarded', 'info');
                this.loadPendingChanges();
            } else {
                Monolith.UI.toast('Failed to discard changes', 'error');
            }
        } catch (error) {
            Monolith.UI.toast('Failed to discard changes', 'error');
        }
    },

    discardSingle: async function(id) {
        try {
            const response = await Monolith.API.post('/api/settings/pending/discard', { ids: [id] });
            if (response.Success || response.success) {
                Monolith.UI.toast('Change discarded', 'info');
                this.loadPendingChanges();
            } else {
                Monolith.UI.toast('Failed to discard change', 'error');
            }
        } catch (error) {
            Monolith.UI.toast('Failed to discard change', 'error');
        }
    },

    showRestartPrompt: function() {
        Monolith.UI.confirm(
            'Some changes require a service restart to take effect. Restart now?',
            async () => {
                try {
                    await Monolith.API.post('/api/system/restart-services');
                    Monolith.UI.toast('Services restarting...', 'info');
                } catch (error) {
                    Monolith.UI.toast('Failed to restart services', 'error');
                }
            }
        );
    },

    showRebootPrompt: function() {
        Monolith.UI.confirm(
            'Some changes require a system reboot to take effect. Reboot now?',
            async () => {
                try {
                    await Monolith.API.post('/api/system/reboot');
                    Monolith.UI.toast('System rebooting...', 'warning');
                } catch (error) {
                    Monolith.UI.toast('Failed to reboot system', 'error');
                }
            }
        );
    },

    escapeHtml: function(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    },

    formatTime: function(timestamp) {
        if (!timestamp) return '';
        const date = new Date(timestamp);
        const now = new Date();
        const diff = now - date;

        if (diff < 60000) return 'Just now';
        if (diff < 3600000) return Math.floor(diff / 60000) + ' min ago';
        if (diff < 86400000) return Math.floor(diff / 3600000) + ' hours ago';
        return date.toLocaleDateString();
    },

    renderError: function() {
        $('#pending-changes-list').html(`
            <div class="text-center text-danger py-3">
                <i class="fa-solid fa-exclamation-triangle"></i>
                <p class="mb-0 small">Failed to load changes</p>
            </div>
        `);
    },

    // Called from other pages when they make changes
    notifyChange: function() {
        this.checkPendingCount();
    }
};

// Initialize on page load
$(document).ready(function() {
    if ($('#pending-changes-indicator').length) {
        Monolith.PendingChanges.init();
    }
});
```

### 3.4 CSS Styles

```css
/* /css/pending-changes.css */

.pending-changes-dropdown {
    max-height: 500px;
    overflow-y: auto;
}

.pending-changes-list {
    max-height: 300px;
    overflow-y: auto;
}

.pending-category-header {
    position: sticky;
    top: 0;
    z-index: 1;
    border-bottom: 1px solid var(--bs-border-color);
}

.pending-change-item {
    border-bottom: 1px solid var(--bs-border-color-translucent);
    transition: background-color 0.15s ease;
}

.pending-change-item:hover {
    background-color: var(--bs-tertiary-bg);
}

.pending-change-item:last-child {
    border-bottom: none;
}

.pending-change-desc {
    font-size: 0.9rem;
}

.pending-change-actions .btn {
    padding: 0.2rem 0.4rem;
}

/* Badge pulse animation */
@keyframes pulse {
    0% { transform: scale(1); }
    50% { transform: scale(1.2); }
    100% { transform: scale(1); }
}

.pulse-animation {
    animation: pulse 0.3s ease-in-out;
}

/* Warning badge color for pending changes */
#pending-changes-badge {
    font-size: 0.65rem;
    padding: 0.25em 0.5em;
}
```

---

## Part 4: API Endpoints

### 4.1 Settings API Routes

```csharp
// SettingsController.cs

[ApiController]
[Route("api/settings")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly ISettingsService _settingsService;

    // ═══════════════════════════════════════════════════════════════════
    // SYSTEM CONFIGS
    // ═══════════════════════════════════════════════════════════════════

    [HttpGet("system/{key}")]
    public async Task<IActionResult> GetSystemConfig(string key)
    {
        var value = await _settingsService.GetSystemConfigJsonAsync(key);
        if (value == null) return NotFound();
        return Ok(ApiResponse.Success(JsonDocument.Parse(value)));
    }

    [HttpPost("system/{key}")]
    public async Task<IActionResult> SetSystemConfig(
        string key,
        [FromBody] JsonElement value,
        [FromQuery] bool applyImmediately = false)
    {
        var mode = applyImmediately ? ApplyMode.Immediate : ApplyMode.Stage;
        var result = await _settingsService.SetSystemConfigAsync(key, value.GetRawText(), mode);

        if (!result.Success)
            return BadRequest(ApiResponse.Error(result.ErrorMessage));

        return Ok(ApiResponse.Success(result));
    }

    [HttpGet("system/category/{category}")]
    public async Task<IActionResult> GetSystemConfigsByCategory(string category)
    {
        var configs = await _settingsService.GetSystemConfigsByCategoryAsync(category);
        return Ok(ApiResponse.Success(configs));
    }

    // ═══════════════════════════════════════════════════════════════════
    // MODULE CONFIGS
    // ═══════════════════════════════════════════════════════════════════

    [HttpGet("module/{moduleId}")]
    public async Task<IActionResult> GetModuleConfig(string moduleId)
    {
        var config = await _settingsService.GetModuleConfigJsonAsync(moduleId);
        if (config == null) return NotFound();
        return Ok(ApiResponse.Success(JsonDocument.Parse(config)));
    }

    [HttpPost("module/{moduleId}")]
    public async Task<IActionResult> SetModuleConfig(
        string moduleId,
        [FromBody] JsonElement config,
        [FromQuery] bool applyImmediately = false)
    {
        var mode = applyImmediately ? ApplyMode.Immediate : ApplyMode.Stage;
        var result = await _settingsService.SetModuleConfigAsync(moduleId, config.GetRawText(), mode);

        if (!result.Success)
            return BadRequest(ApiResponse.Error(result.ErrorMessage));

        return Ok(ApiResponse.Success(result));
    }

    [HttpPost("module/{moduleId}/enable")]
    public async Task<IActionResult> SetModuleEnabled(
        string moduleId,
        [FromBody] EnableRequest request)
    {
        var result = await _settingsService.SetModuleEnabledAsync(
            moduleId, request.Enabled, request.ApplyImmediately ? ApplyMode.Immediate : ApplyMode.Stage);
        return Ok(ApiResponse.Success(result));
    }

    // ═══════════════════════════════════════════════════════════════════
    // PENDING CHANGES
    // ═══════════════════════════════════════════════════════════════════

    [HttpGet("pending/count")]
    public async Task<IActionResult> GetPendingCount()
    {
        var count = await _settingsService.GetPendingCountAsync();
        return Ok(ApiResponse.Success(new { count }));
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingChanges()
    {
        var changes = await _settingsService.GetPendingChangesAsync();
        return Ok(ApiResponse.Success(changes));
    }

    [HttpGet("pending/grouped")]
    public async Task<IActionResult> GetPendingChangesGrouped()
    {
        var grouped = await _settingsService.GetPendingChangesByCategory();
        return Ok(ApiResponse.Success(grouped));
    }

    [HttpPost("pending/apply-all")]
    public async Task<IActionResult> ApplyAllPending()
    {
        var user = User.Identity?.Name ?? "system";
        var result = await _settingsService.ApplyAllPendingAsync(user);
        return Ok(ApiResponse.Success(result));
    }

    [HttpPost("pending/apply")]
    public async Task<IActionResult> ApplyPending([FromBody] ApplyRequest request)
    {
        var user = User.Identity?.Name ?? "system";
        var result = await _settingsService.ApplyPendingAsync(request.Ids, user);
        return Ok(ApiResponse.Success(result));
    }

    [HttpPost("pending/discard-all")]
    public async Task<IActionResult> DiscardAllPending()
    {
        var success = await _settingsService.DiscardAllPendingAsync();
        return Ok(ApiResponse.Success(new { success }));
    }

    [HttpPost("pending/discard")]
    public async Task<IActionResult> DiscardPending([FromBody] DiscardRequest request)
    {
        var success = await _settingsService.DiscardPendingAsync(request.Ids);
        return Ok(ApiResponse.Success(new { success }));
    }

    // ═══════════════════════════════════════════════════════════════════
    // HISTORY
    // ═══════════════════════════════════════════════════════════════════

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        [FromQuery] string? configType = null,
        [FromQuery] string? configKey = null,
        [FromQuery] int limit = 100)
    {
        var filter = new HistoryFilter
        {
            ConfigType = configType,
            ConfigKey = configKey,
            Limit = limit
        };
        var history = await _settingsService.GetHistoryAsync(filter);
        return Ok(ApiResponse.Success(history));
    }

    [HttpPost("history/{id}/rollback")]
    public async Task<IActionResult> Rollback(int id)
    {
        var user = User.Identity?.Name ?? "system";
        var result = await _settingsService.RollbackToAsync(id, user);

        if (!result.Success)
            return BadRequest(ApiResponse.Error(result.ErrorMessage));

        return Ok(ApiResponse.Success(result));
    }
}
```

---

## Part 5: Migration Plan

### 5.1 Phase 1: Database Schema (Week 1)

1. **Create new entity classes**
   - `SystemConfigEntity`
   - `ModuleConfigEntity`
   - `PendingChangeEntity`
   - `ConfigHistoryEntity`

2. **Add table sync in Program.cs**
   ```csharp
   await sqlite.TableSyncService.SyncTableAsync<SystemConfigEntity>();
   await sqlite.TableSyncService.SyncTableAsync<ModuleConfigEntity>();
   await sqlite.TableSyncService.SyncTableAsync<PendingChangeEntity>();
   await sqlite.TableSyncService.SyncTableAsync<ConfigHistoryEntity>();
   ```

3. **Create migration service** to copy existing settings to new tables
   ```csharp
   public class SettingsMigrationService
   {
       public async Task MigrateFromLegacyAsync()
       {
           // Migrate SystemSettingsEntity → system_configs
           // Migrate existing module configs → module_configs
           // Migrate firewall settings → appropriate tables
       }
   }
   ```

### 5.2 Phase 2: Core Services (Week 2)

1. **Implement store classes**
   - `SystemConfigStore`
   - `ModuleConfigStore`
   - `PendingChangesStore`
   - `ConfigHistoryStore`

2. **Implement `ISettingsService`**
   - Core methods for get/set configs
   - Pending changes management
   - History tracking

3. **Create `IConfigApplier` implementations**
   - Start with system configs (hostname, DNS, NTP)
   - Add module config appliers

### 5.3 Phase 3: API & Integration (Week 3)

1. **Create `SettingsController`** with all endpoints

2. **Update existing handlers** to use new service
   - `SystemSettingsHandler` → delegate to `ISettingsService`
   - `FirewallHandler` → integrate pending changes

3. **Add SignalR hub** for real-time pending count updates (optional)

### 5.4 Phase 4: UI Implementation (Week 4)

1. **Add pending changes indicator** to navbar

2. **Create `/js/core/pending-changes.js`**

3. **Update settings pages** to show staged vs. applied state

4. **Create history/audit page**

### 5.5 Phase 5: Module Migration (Ongoing)

1. **Update each module** to use `ISettingsService`
   - DHCP module
   - DNS module
   - VPN modules
   - etc.

2. **Create appliers** for each module

---

## Part 6: Example Usage

### 6.1 Settings Page Using New System

```javascript
// settings-system.js (updated)

var SettingsSystem = {
    init: function() {
        this.loadSettings();
        this.bindEvents();
    },

    loadSettings: async function() {
        try {
            // Load from new API
            const response = await Monolith.API.get('/api/settings/system/category/system');
            const configs = response.Data || response.data || {};

            // Populate form
            if (configs['system.hostname']) {
                const hostname = JSON.parse(configs['system.hostname']);
                $('#hostname').val(hostname.hostname);
            }
            // ... etc
        } catch (error) {
            Monolith.UI.toast('Failed to load settings', 'error');
        }
    },

    saveSettings: async function() {
        const hostname = $('#hostname').val().trim();

        try {
            // Save using new API - will create pending change
            const response = await Monolith.API.post('/api/settings/system/system.hostname', {
                hostname: hostname
            });

            if (response.Success) {
                if (response.Data.staged) {
                    Monolith.UI.toast('Changes saved. Click Apply to activate.', 'info');
                    // Notify pending changes indicator
                    Monolith.PendingChanges.notifyChange();
                } else {
                    Monolith.UI.toast('Settings applied successfully', 'success');
                }
            }
        } catch (error) {
            Monolith.UI.toast('Failed to save settings', 'error');
        }
    },

    // Option to apply immediately (for critical settings)
    saveAndApply: async function() {
        const hostname = $('#hostname').val().trim();

        const response = await Monolith.API.post(
            '/api/settings/system/system.hostname?applyImmediately=true',
            { hostname: hostname }
        );
        // ...
    }
};
```

### 6.2 Module Package Using New System

```csharp
// In monolith-network package's DHCP module

public class DhcpConfigApplier : ModuleConfigApplier
{
    public override string ModuleId => "monolith-network.dhcp";
    public override string DisplayName => "DHCP Server";
    public override string Category => "Network";
    public override bool RequiresRestart => true; // Requires dhcpd restart

    public override async Task<ValidationResult> ValidateAsync(string configJson)
    {
        try
        {
            var config = JsonSerializer.Deserialize<DhcpConfig>(configJson);

            // Validate IP ranges
            foreach (var iface in config.Interfaces.Values)
            {
                if (!IsValidIpRange(iface.RangeStart, iface.RangeEnd))
                {
                    return ValidationResult.Error($"Invalid IP range for {iface.Name}");
                }
            }

            return ValidationResult.Success();
        }
        catch (Exception ex)
        {
            return ValidationResult.Error($"Invalid config format: {ex.Message}");
        }
    }

    public override async Task<ApplyResult> ApplyAsync(string configJson)
    {
        try
        {
            var config = JsonSerializer.Deserialize<DhcpConfig>(configJson);

            // Generate dhcpd.conf
            var confContent = GenerateDhcpdConf(config);
            await File.WriteAllTextAsync("/etc/dhcp/dhcpd.conf", confContent);

            // Restart service if enabled
            if (config.Enabled)
            {
                await _processRunner.RunAsync("systemctl", "restart dhcpd");
            }
            else
            {
                await _processRunner.RunAsync("systemctl", "stop dhcpd");
            }

            return ApplyResult.Success();
        }
        catch (Exception ex)
        {
            return ApplyResult.Error(ex.Message);
        }
    }

    public override string DescribeChange(string oldJson, string newJson)
    {
        var oldConfig = JsonSerializer.Deserialize<DhcpConfig>(oldJson);
        var newConfig = JsonSerializer.Deserialize<DhcpConfig>(newJson);

        var changes = new List<string>();

        if (oldConfig.Enabled != newConfig.Enabled)
        {
            changes.Add(newConfig.Enabled ? "Enabled DHCP server" : "Disabled DHCP server");
        }

        // Compare interfaces, leases, etc.
        // ...

        return changes.Any() ? string.Join(", ", changes) : "Updated DHCP configuration";
    }
}
```

---

## Part 7: Considerations

### 7.1 What Uses the New System (Opt-in)

| Component | Uses Pending Changes? | Notes |
|-----------|----------------------|-------|
| System hostname | ✅ Yes | Needs apply |
| System timezone | ✅ Yes | Needs apply |
| DNS servers | ✅ Yes | Needs apply |
| NTP servers | ✅ Yes | Needs apply |
| Firewall rules | ✅ Yes | Already has staged model |
| NAT rules | ✅ Yes | Already has staged model |
| Static routes | ✅ Yes | Needs apply |
| Gateways | ✅ Yes | Needs apply |
| Interface config | ✅ Yes | Needs apply |
| Module configs | ✅ Yes | Each module decides |
| WebUI ports | ❌ No | Immediate (requires restart) |
| User management | ❌ No | Immediate |
| Session settings | ❌ No | Immediate |
| Dashboard layout | ❌ No | Immediate (user preference) |

### 7.2 Backward Compatibility

- Keep existing API endpoints working during migration
- Add deprecation warnings in logs
- Provide migration script for existing configs
- Document breaking changes in release notes

### 7.3 Security Considerations

- All config changes must be authenticated
- Audit trail captures user and timestamp
- Rollback requires appropriate permissions
- Validate all JSON input before storing

### 7.4 Performance Considerations

- Batch multiple changes when possible
- Lazy-load pending changes list
- Index database columns for fast queries
- Consider caching frequently-accessed configs

---

## Summary

This overhaul provides:

1. **Unified Storage** - All configs in two structured tables
2. **Staged Changes** - Save now, apply when ready
3. **Visual Feedback** - Pending changes indicator in navbar
4. **Audit Trail** - Full history of all changes
5. **Rollback** - Ability to revert to previous states
6. **Validation** - Check configs before applying
7. **Flexibility** - Opt-in model, not everything requires staging

The implementation is modular and can be rolled out incrementally, starting with system configs and gradually adding module support.
