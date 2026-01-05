using Monolith.FireWall.Common.Interfaces;
using Monolith.FireWall.Core.Models;
using Monolith.FireWall.Core.Services.Firewall;

namespace Monolith.FireWall.Core.Services;

/// <summary>
/// Orchestrates all system startup initialization on boot.
/// Reads configuration from database and applies it to the system.
/// </summary>
public sealed class StartupManager
{
    private readonly ILogger _logger;
    private readonly SystemSettingsManager _systemSettingsManager;
    private readonly InterfaceConfigApplier _interfaceConfigApplier;
    private readonly FirewallApplyManager _firewallApplyManager;
    private readonly ModuleConfigGenerator _moduleConfigGenerator;
    private readonly ModuleServiceManager _moduleServiceManager;

    public StartupManager(
        ILogger logger,
        SystemSettingsManager systemSettingsManager,
        InterfaceConfigApplier interfaceConfigApplier,
        FirewallApplyManager firewallApplyManager,
        ModuleConfigGenerator moduleConfigGenerator,
        ModuleServiceManager moduleServiceManager)
    {
        _logger = logger;
        _systemSettingsManager = systemSettingsManager;
        _interfaceConfigApplier = interfaceConfigApplier;
        _firewallApplyManager = firewallApplyManager;
        _moduleConfigGenerator = moduleConfigGenerator;
        _moduleServiceManager = moduleServiceManager;
    }

    /// <summary>
    /// Initialize the entire system from stored configuration.
    /// </summary>
    public async Task<StartupResult> InitializeSystemAsync(CancellationToken cancellationToken = default)
    {
        var result = new StartupResult
        {
            StartedAt = DateTime.UtcNow
        };

        _logger.LogInformation("Starting system initialization...");

        try
        {
            // Step 1: Apply system settings (hostname, timezone, DNS, NTP)
            _logger.LogInformation("Applying system settings...");
            var systemResult = await ApplySystemSettingsAsync(cancellationToken);
            result.SystemSettings = systemResult;
            if (systemResult.Success)
            {
                _logger.LogInformation("✓ System settings applied");
            }
            else
            {
                _logger.LogWarning($"⚠ System settings partially applied: {systemResult.Error}");
            }

            // Step 2: Generate and apply interface configurations
            _logger.LogInformation("Generating interface configurations...");
            var interfaceResult = await GenerateInterfaceConfigsAsync(cancellationToken);
            result.Interfaces = interfaceResult;
            if (interfaceResult.Success)
            {
                _logger.LogInformation($"✓ Interface configurations generated ({interfaceResult.GeneratedCount} interfaces)");
            }
            else
            {
                _logger.LogWarning($"⚠ Interface configuration failed: {interfaceResult.Error}");
            }

            // Step 3: Generate module configurations
            _logger.LogInformation("Generating module configurations...");
            var moduleConfigResult = await GenerateModuleConfigsAsync(cancellationToken);
            result.Modules = moduleConfigResult;
            if (moduleConfigResult.Success)
            {
                _logger.LogInformation($"✓ Module configurations generated ({moduleConfigResult.ModuleResults.Count} module(s))");
                if (moduleConfigResult.ModulesRequiringRestart.Count > 0)
                {
                    _logger.LogInformation($"  → {moduleConfigResult.ModulesRequiringRestart.Count} module(s) require service restart");
                }
            }
            else
            {
                _logger.LogWarning($"⚠ Module config generation had errors");
            }

            // Step 4: Start/restart module services
            _logger.LogInformation("Managing module services...");
            var serviceResult = await StartModuleServicesAsync(moduleConfigResult.ModulesRequiringRestart, cancellationToken);
            result.Services = serviceResult;
            if (serviceResult.Success)
            {
                _logger.LogInformation($"✓ Module services managed: {serviceResult.ServicesStarted.Count} started, {serviceResult.ServicesRestarted.Count} restarted");
                if (serviceResult.ServicesFailed.Count > 0)
                {
                    _logger.LogWarning($"  → {serviceResult.ServicesFailed.Count} service(s) failed");
                }
            }
            else
            {
                _logger.LogWarning($"⚠ Service management failed: {serviceResult.Error}");
            }

            // Step 5: Apply firewall rules
            _logger.LogInformation("Applying firewall rules...");
            var firewallResult = await ApplyFirewallRulesAsync(cancellationToken);
            result.Firewall = firewallResult;
            if (firewallResult.Success)
            {
                _logger.LogInformation($"✓ Firewall rules applied ({firewallResult.RulesApplied} rules)");
            }
            else
            {
                _logger.LogWarning($"⚠ Firewall application failed: {firewallResult.Error}");
            }

            result.Success = true;
            result.CompletedAt = DateTime.UtcNow;
            result.Duration = result.CompletedAt - result.StartedAt;
            if (result.Duration.HasValue)
            {
                _logger.LogInformation($"System initialization completed in {result.Duration.Value.TotalSeconds:F2} seconds");
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            result.CompletedAt = DateTime.UtcNow;
            result.Duration = result.CompletedAt - result.StartedAt;
            _logger.LogError(ex, "System initialization failed");
        }

        return result;
    }

    /// <summary>
    /// Apply system settings from database.
    /// </summary>
    public async Task<SystemSettingsResult> ApplySystemSettingsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _systemSettingsManager.ApplyStoredSettingsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply system settings");
            return new SystemSettingsResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Generate and apply interface configurations from database.
    /// </summary>
    public async Task<InterfaceConfigResult> GenerateInterfaceConfigsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _interfaceConfigApplier.ApplyStoredConfigsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate interface configurations");
            return new InterfaceConfigResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Generate configurations for all modules.
    /// </summary>
    public async Task<ModuleConfigGenerationSummary> GenerateModuleConfigsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _moduleConfigGenerator.GenerateAllModuleConfigsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate module configurations");
            return new ModuleConfigGenerationSummary
            {
                Success = false,
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Start or restart module services.
    /// </summary>
    public async Task<ServiceManagementResult> StartModuleServicesAsync(
        IEnumerable<string> modulesRequiringRestart,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _moduleServiceManager.ManageModuleServicesAsync(modulesRequiringRestart, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to manage module services");
            return new ServiceManagementResult
            {
                Success = false,
                Error = ex.Message,
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Apply firewall rules from database.
    /// </summary>
    public async Task<FirewallStartupResult> ApplyFirewallRulesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var applyResult = await _firewallApplyManager.ApplyAsync(cancellationToken);
            return new FirewallStartupResult
            {
                Success = applyResult.Success,
                Error = applyResult.Error,
                RulesApplied = 0, // Count would need to come from FirewallManager
                Warnings = applyResult.Warnings
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply firewall rules");
            return new FirewallStartupResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }
}


