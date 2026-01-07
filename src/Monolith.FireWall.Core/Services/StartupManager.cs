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
    private readonly SystemTuneablesManager _tuneablesManager;
    private readonly InterfaceConfigApplier _interfaceConfigApplier;
    private readonly FirewallApplyManager _firewallApplyManager;
    private readonly ModuleConfigGenerator _moduleConfigGenerator;
    private readonly ModuleServiceManager _moduleServiceManager;

    public StartupManager(
        ILogger logger,
        SystemSettingsManager systemSettingsManager,
        SystemTuneablesManager tuneablesManager,
        InterfaceConfigApplier interfaceConfigApplier,
        FirewallApplyManager firewallApplyManager,
        ModuleConfigGenerator moduleConfigGenerator,
        ModuleServiceManager moduleServiceManager)
    {
        _logger = logger;
        _systemSettingsManager = systemSettingsManager;
        _tuneablesManager = tuneablesManager;
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

            // Step 1.5: Apply system tuneables (including IPv4 forwarding)
            _logger.LogInformation("Applying system tuneables...");
            var tuneablesResult = await ApplySystemTuneablesAsync(cancellationToken);
            result.Tuneables = tuneablesResult;
            if (tuneablesResult.Success)
            {
                _logger.LogInformation($"✓ System tuneables applied ({tuneablesResult.AppliedCount}/{tuneablesResult.TotalCount} tuneable(s))");
                if (tuneablesResult.Warnings.Count > 0)
                {
                    foreach (var warning in tuneablesResult.Warnings)
                    {
                        _logger.LogWarning($"  → {warning}");
                    }
                }
            }
            else
            {
                _logger.LogWarning($"⚠ System tuneables partially applied: {tuneablesResult.Error}");
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
    /// Apply stored system tuneables from database.
    /// Used during startup initialization to enable features like IPv4 forwarding.
    /// </summary>
    public async Task<TuneablesStartupResult> ApplySystemTuneablesAsync(CancellationToken cancellationToken = default)
    {
        var result = new TuneablesStartupResult();
        
        try
        {
            // Get all tuneables (includes stored desired values and current system values)
            var allTuneables = await _tuneablesManager.GetTuneablesAsync(cancellationToken);
            result.TotalCount = allTuneables.Count;
            
            // Find tuneables that need to be applied (desired value differs from current)
            var toApply = allTuneables
                .Where(t => 
                    !string.IsNullOrWhiteSpace(t.DesiredValue) && 
                    t.DesiredValue != t.CurrentValue)
                .ToList();
            
            if (toApply.Count == 0)
            {
                result.Success = true;
                result.AppliedCount = 0;
                return result;
            }
            
            // Build apply request
            var request = new TuneableApplyRequest
            {
                Items = toApply.Select(t => new TuneableUpdate
                {
                    Key = t.Key,
                    Value = t.DesiredValue
                }).ToList()
            };
            
            // Apply tuneables
            var applyResult = await _tuneablesManager.ApplyAsync(request, cancellationToken);
            
            result.Success = applyResult.Success;
            result.AppliedCount = applyResult.Results?.Count(r => r.Success) ?? 0;
            result.Error = applyResult.Error;
            result.Warnings = applyResult.Results?
                .Where(r => !r.Success && !string.IsNullOrWhiteSpace(r.Error))
                .Select(r => $"{r.Key}: {r.Error}")
                .ToList() ?? new List<string>();
            
            // Log critical tuneables
            var ipForward = toApply.FirstOrDefault(t => t.Key == "net.ipv4.ip_forward");
            if (ipForward != null)
            {
                var ipForwardResult = applyResult.Results?.FirstOrDefault(r => r.Key == "net.ipv4.ip_forward");
                if (ipForwardResult?.Success == true)
                {
                    _logger.LogInformation($"  → IPv4 forwarding enabled: {ipForwardResult.AppliedValue}");
                }
                else
                {
                    _logger.LogWarning($"  → IPv4 forwarding failed to apply: {ipForwardResult?.Error ?? "unknown error"}");
                    result.Warnings.Add("IPv4 forwarding not applied - routing may not work");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply system tuneables");
            result.Success = false;
            result.Error = ex.Message;
        }
        
        return result;
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


