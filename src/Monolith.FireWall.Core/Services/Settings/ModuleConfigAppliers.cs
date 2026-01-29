using System.Text.Json;
using Monolith.FireWall.Common.Interfaces;
using Monolith.FireWall.Core.Models;
using Monolith.FireWall.Core.Services.Platform;

namespace Monolith.FireWall.Core.Services.Settings;

/// <summary>
/// Base class for module configuration appliers.
/// Modules can extend this to implement their own config application logic.
/// </summary>
public abstract class ModuleConfigApplierBase : IConfigApplier
{
    protected readonly ILogger Logger;
    protected readonly PlatformCommandRunner CommandRunner;

    public abstract string ModuleId { get; }
    public abstract string DisplayName { get; }
    public virtual string Category => "Modules";
    public virtual bool RequiresRestart => false;
    public virtual bool RequiresReboot => false;

    public string TargetType => "ModuleConfig";
    public virtual bool SupportsRollback => true;

    protected ModuleConfigApplierBase(ILogger logger, PlatformCommandRunner? commandRunner = null)
    {
        Logger = logger;
        CommandRunner = commandRunner ?? new PlatformCommandRunner();
    }

    public abstract Task<ValidationResult> ValidateAsync(string targetKey, string? currentValue, string? newValue);
    public abstract Task<ApplyResult> ApplyAsync(string targetKey, string? previousValue, string? newValue);

    public virtual async Task<ApplyResult> RollbackAsync(string targetKey, string? currentValue, string? previousValue)
    {
        if (string.IsNullOrWhiteSpace(previousValue))
        {
            return new ApplyResult { Success = false, Error = "No previous configuration to rollback to" };
        }

        return await ApplyAsync(targetKey, currentValue, previousValue);
    }

    /// <summary>
    /// Describes the change between old and new configuration.
    /// Override to provide more detailed change descriptions.
    /// </summary>
    public virtual string DescribeChange(string? oldJson, string? newJson)
    {
        return $"Updated {DisplayName} configuration";
    }
}

/// <summary>
/// Generic module applier that can be used for modules without specific apply logic.
/// Just stores the config in database without applying to the system.
/// </summary>
public sealed class GenericModuleApplier : IConfigApplier
{
    private readonly ILogger _logger;
    private readonly string _moduleId;

    public string TargetType => "ModuleConfig";
    public bool SupportsRollback => true;

    public GenericModuleApplier(ILogger logger, string moduleId)
    {
        _logger = logger;
        _moduleId = moduleId;
    }

    public Task<ValidationResult> ValidateAsync(string targetKey, string? currentValue, string? newValue)
    {
        if (string.IsNullOrWhiteSpace(newValue))
        {
            return Task.FromResult(new ValidationResult
            {
                IsValid = false,
                Errors = new List<string> { "Configuration value is required" }
            });
        }

        // Validate JSON format
        try
        {
            JsonDocument.Parse(newValue);
            return Task.FromResult(new ValidationResult { IsValid = true });
        }
        catch (JsonException ex)
        {
            return Task.FromResult(new ValidationResult
            {
                IsValid = false,
                Errors = new List<string> { $"Invalid JSON format: {ex.Message}" }
            });
        }
    }

    public Task<ApplyResult> ApplyAsync(string targetKey, string? previousValue, string? newValue)
    {
        // Generic applier just marks as success - the config is already stored in database
        _logger.LogInformation($"Applied module config for: {_moduleId}");
        return Task.FromResult(new ApplyResult { Success = true });
    }

    public Task<ApplyResult> RollbackAsync(string targetKey, string? currentValue, string? previousValue)
    {
        if (string.IsNullOrWhiteSpace(previousValue))
        {
            return Task.FromResult(new ApplyResult { Success = false, Error = "No previous configuration to rollback to" });
        }

        return ApplyAsync(targetKey, currentValue, previousValue);
    }
}

/// <summary>
/// Registry for module config appliers.
/// Modules register their appliers here on package load.
/// </summary>
public sealed class ModuleApplierRegistry
{
    private readonly ILogger _logger;
    private readonly Dictionary<string, IConfigApplier> _appliers = new(StringComparer.OrdinalIgnoreCase);

    public ModuleApplierRegistry(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Registers an applier for a module.
    /// </summary>
    public void RegisterApplier(string moduleId, IConfigApplier applier)
    {
        _appliers[moduleId] = applier;
        _logger.LogInformation($"Registered config applier for module: {moduleId}");
    }

    /// <summary>
    /// Unregisters an applier for a module.
    /// </summary>
    public void UnregisterApplier(string moduleId)
    {
        if (_appliers.Remove(moduleId))
        {
            _logger.LogInformation($"Unregistered config applier for module: {moduleId}");
        }
    }

    /// <summary>
    /// Gets the applier for a module.
    /// Returns a generic applier if no specific applier is registered.
    /// </summary>
    public IConfigApplier GetApplier(string moduleId)
    {
        if (_appliers.TryGetValue(moduleId, out var applier))
        {
            return applier;
        }

        // Return a generic applier that just stores the config
        return new GenericModuleApplier(_logger, moduleId);
    }

    /// <summary>
    /// Checks if a specific applier is registered for a module.
    /// </summary>
    public bool HasApplier(string moduleId)
    {
        return _appliers.ContainsKey(moduleId);
    }

    /// <summary>
    /// Gets all registered module IDs with specific appliers.
    /// </summary>
    public IEnumerable<string> GetRegisteredModuleIds()
    {
        return _appliers.Keys;
    }
}

#region Example Module Appliers

/// <summary>
/// Example: DHCP Server configuration applier.
/// This shows how a module would implement its own applier.
/// </summary>
public class DhcpConfigApplier : ModuleConfigApplierBase
{
    public override string ModuleId => "monolith-network.dhcp";
    public override string DisplayName => "DHCP Server";
    public override string Category => "Network";
    public override bool RequiresRestart => true;

    public DhcpConfigApplier(ILogger logger, PlatformCommandRunner? commandRunner = null)
        : base(logger, commandRunner)
    {
    }

    public override Task<ValidationResult> ValidateAsync(string targetKey, string? currentValue, string? newValue)
    {
        if (string.IsNullOrWhiteSpace(newValue))
        {
            return Task.FromResult(new ValidationResult
            {
                IsValid = false,
                Errors = new List<string> { "DHCP configuration is required" }
            });
        }

        try
        {
            var config = JsonSerializer.Deserialize<DhcpModuleConfig>(newValue);
            if (config == null)
            {
                return Task.FromResult(new ValidationResult
                {
                    IsValid = false,
                    Errors = new List<string> { "Invalid DHCP configuration format" }
                });
            }

            // TODO: Add validation for IP ranges, lease times, etc.
            return Task.FromResult(new ValidationResult { IsValid = true });
        }
        catch (JsonException ex)
        {
            return Task.FromResult(new ValidationResult
            {
                IsValid = false,
                Errors = new List<string> { $"Invalid JSON format: {ex.Message}" }
            });
        }
    }

    public override async Task<ApplyResult> ApplyAsync(string targetKey, string? previousValue, string? newValue)
    {
        if (string.IsNullOrWhiteSpace(newValue))
        {
            return new ApplyResult { Success = false, Error = "No DHCP configuration provided" };
        }

        try
        {
            var config = JsonSerializer.Deserialize<DhcpModuleConfig>(newValue);
            if (config == null)
            {
                return new ApplyResult { Success = false, Error = "Invalid DHCP configuration" };
            }

            // TODO: Generate dhcpd.conf and restart service
            // For now, just log and return success
            Logger.LogInformation($"DHCP config would be applied: enabled={config.Enabled}");

            return new ApplyResult { Success = true, RequiresRestart = true };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"Failed to apply DHCP configuration: {ex.Message}");
            return new ApplyResult { Success = false, Error = ex.Message };
        }
    }

    public override string DescribeChange(string? oldJson, string? newJson)
    {
        if (string.IsNullOrEmpty(oldJson))
        {
            return "Initial DHCP server configuration";
        }

        try
        {
            var oldConfig = JsonSerializer.Deserialize<DhcpModuleConfig>(oldJson);
            var newConfig = JsonSerializer.Deserialize<DhcpModuleConfig>(newJson ?? "{}");

            if (oldConfig?.Enabled != newConfig?.Enabled)
            {
                return newConfig?.Enabled == true ? "Enabled DHCP server" : "Disabled DHCP server";
            }
        }
        catch
        {
            // Fall through to default
        }

        return "Updated DHCP server configuration";
    }
}

/// <summary>
/// Example: DNS Resolver configuration applier.
/// </summary>
public class DnsResolverApplier : ModuleConfigApplierBase
{
    public override string ModuleId => "monolith-network.dns";
    public override string DisplayName => "DNS Resolver";
    public override string Category => "Network";
    public override bool RequiresRestart => true;

    public DnsResolverApplier(ILogger logger, PlatformCommandRunner? commandRunner = null)
        : base(logger, commandRunner)
    {
    }

    public override Task<ValidationResult> ValidateAsync(string targetKey, string? currentValue, string? newValue)
    {
        if (string.IsNullOrWhiteSpace(newValue))
        {
            return Task.FromResult(new ValidationResult
            {
                IsValid = false,
                Errors = new List<string> { "DNS configuration is required" }
            });
        }

        try
        {
            var config = JsonSerializer.Deserialize<DnsModuleConfig>(newValue);
            if (config == null)
            {
                return Task.FromResult(new ValidationResult
                {
                    IsValid = false,
                    Errors = new List<string> { "Invalid DNS configuration format" }
                });
            }

            return Task.FromResult(new ValidationResult { IsValid = true });
        }
        catch (JsonException ex)
        {
            return Task.FromResult(new ValidationResult
            {
                IsValid = false,
                Errors = new List<string> { $"Invalid JSON format: {ex.Message}" }
            });
        }
    }

    public override async Task<ApplyResult> ApplyAsync(string targetKey, string? previousValue, string? newValue)
    {
        if (string.IsNullOrWhiteSpace(newValue))
        {
            return new ApplyResult { Success = false, Error = "No DNS configuration provided" };
        }

        try
        {
            var config = JsonSerializer.Deserialize<DnsModuleConfig>(newValue);
            if (config == null)
            {
                return new ApplyResult { Success = false, Error = "Invalid DNS configuration" };
            }

            // TODO: Generate unbound/dnsmasq config and restart service
            Logger.LogInformation($"DNS resolver config would be applied: enabled={config.Enabled}");

            return new ApplyResult { Success = true, RequiresRestart = true };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"Failed to apply DNS configuration: {ex.Message}");
            return new ApplyResult { Success = false, Error = ex.Message };
        }
    }
}

#endregion

#region Module Config Models

/// <summary>
/// DHCP module configuration model.
/// </summary>
public class DhcpModuleConfig
{
    public bool Enabled { get; set; }
    public Dictionary<string, DhcpInterfaceConfig>? Interfaces { get; set; }
    public List<DhcpStaticLease>? StaticLeases { get; set; }
}

public class DhcpInterfaceConfig
{
    public bool Enabled { get; set; }
    public string? RangeStart { get; set; }
    public string? RangeEnd { get; set; }
    public int LeaseTime { get; set; } = 86400;
    public string? Gateway { get; set; }
    public List<string>? DnsServers { get; set; }
}

public class DhcpStaticLease
{
    public string? Mac { get; set; }
    public string? Ip { get; set; }
    public string? Hostname { get; set; }
}

/// <summary>
/// DNS module configuration model.
/// </summary>
public class DnsModuleConfig
{
    public bool Enabled { get; set; }
    public List<string>? ListenAddresses { get; set; }
    public List<string>? Forwarders { get; set; }
    public bool Dnssec { get; set; }
    public List<DnsLocalZone>? LocalZones { get; set; }
}

public class DnsLocalZone
{
    public string? Name { get; set; }
    public string? Type { get; set; }
}

#endregion
