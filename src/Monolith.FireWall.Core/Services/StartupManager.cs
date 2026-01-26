using Monolith.FireWall.Common.Interfaces;
using Monolith.FireWall.Core.Models;
using Monolith.FireWall.Core.Services.Firewall;
using Monolith.FireWall.Core.Services.Settings;

namespace Monolith.FireWall.Core.Services;

/// <summary>
/// Orchestrates all system startup initialization on boot.
/// Reads configuration from database and applies it to the system.
/// </summary>
public sealed class StartupManager
{
    private readonly ILogger _logger;
    private readonly ISettingsService _settingsService;
    private readonly SystemTuneablesManager _tuneablesManager;
    private readonly InterfaceConfigApplier _interfaceConfigApplier;
    private readonly FirewallApplyManager _firewallApplyManager;
    private readonly ModuleConfigGenerator _moduleConfigGenerator;
    private readonly ModuleServiceManager _moduleServiceManager;
    private readonly GatewayManager? _gatewayManager;
    private readonly InterfaceOperationalStateStore? _operationalStateStore;
    private readonly InterfaceAssignmentStore? _interfaceAssignmentStore;
    private readonly NetworkInventoryService? _networkInventory;

    public StartupManager(
        ILogger logger,
        ISettingsService settingsService,
        SystemTuneablesManager tuneablesManager,
        InterfaceConfigApplier interfaceConfigApplier,
        FirewallApplyManager firewallApplyManager,
        ModuleConfigGenerator moduleConfigGenerator,
        ModuleServiceManager moduleServiceManager,
        GatewayManager? gatewayManager = null,
        InterfaceOperationalStateStore? operationalStateStore = null,
        InterfaceAssignmentStore? interfaceAssignmentStore = null,
        NetworkInventoryService? networkInventory = null)
    {
        _logger = logger;
        _settingsService = settingsService;
        _tuneablesManager = tuneablesManager;
        _interfaceConfigApplier = interfaceConfigApplier;
        _firewallApplyManager = firewallApplyManager;
        _moduleConfigGenerator = moduleConfigGenerator;
        _moduleServiceManager = moduleServiceManager;
        _gatewayManager = gatewayManager;
        _operationalStateStore = operationalStateStore;
        _interfaceAssignmentStore = interfaceAssignmentStore;
        _networkInventory = networkInventory;
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
                _logger.LogInformation("System settings applied");
            }
            else
            {
                _logger.LogWarning($"System settings partially applied: {systemResult.Error}");
            }

            // Step 1.5: Apply system tuneables (including IPv4 forwarding)
            _logger.LogInformation("Applying system tuneables...");
            var tuneablesResult = await ApplySystemTuneablesAsync(cancellationToken);
            result.Tuneables = tuneablesResult;
            if (tuneablesResult.Success)
            {
                _logger.LogInformation($"System tuneables applied ({tuneablesResult.AppliedCount}/{tuneablesResult.TotalCount} tuneable(s))");
                if (tuneablesResult.Warnings.Count > 0)
                {
                    foreach (var warning in tuneablesResult.Warnings)
                    {
                        _logger.LogWarning($"  -> {warning}");
                    }
                }
            }
            else
            {
                _logger.LogWarning($"System tuneables partially applied: {tuneablesResult.Error}");
            }

            // Step 1.6: Initialize gateways (sync dynamic gateways from system)
            if (_gatewayManager != null)
            {
                _logger.LogInformation("Initializing gateways...");
                try
                {
                    await _gatewayManager.SyncDynamicGatewaysAsync(cancellationToken);
                    var gateways = await _gatewayManager.GetGatewaysAsync(cancellationToken);
                    _logger.LogInformation($"Gateways initialized ({gateways.Count} gateway(s) found)");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Gateway initialization failed: {ex.Message}");
                }
            }

            // Step 2: Generate and apply interface configurations
            _logger.LogInformation("Generating interface configurations...");
            var interfaceResult = await GenerateInterfaceConfigsAsync(cancellationToken);
            result.Interfaces = interfaceResult;
            if (interfaceResult.Success)
            {
                _logger.LogInformation($"Interface configurations generated ({interfaceResult.GeneratedCount} interfaces)");
            }
            else
            {
                _logger.LogWarning($"Interface configuration failed: {interfaceResult.Error}");
            }

            // Step 2.5: Initialize operational state for assigned interfaces
            _logger.LogInformation("Initializing interface operational state...");
            var operationalStateResult = await InitializeOperationalStateAsync(cancellationToken);
            result.OperationalState = operationalStateResult;
            if (operationalStateResult.Success)
            {
                _logger.LogInformation($"Interface operational state initialized ({operationalStateResult.InitializedCount} interfaces)");
            }
            else
            {
                _logger.LogWarning($"Operational state initialization had errors: {operationalStateResult.Error}");
            }

            // Step 3: Generate module configurations
            _logger.LogInformation("Generating module configurations...");
            var moduleConfigResult = await GenerateModuleConfigsAsync(cancellationToken);
            result.Modules = moduleConfigResult;
            if (moduleConfigResult.Success)
            {
                _logger.LogInformation($"Module configurations generated ({moduleConfigResult.ModuleResults.Count} module(s))");
                if (moduleConfigResult.ModulesRequiringRestart.Count > 0)
                {
                    _logger.LogInformation($"  -> {moduleConfigResult.ModulesRequiringRestart.Count} module(s) require service restart");
                }
            }
            else
            {
                _logger.LogWarning($"Module config generation had errors");
            }

            // Step 4: Start/restart module services
            _logger.LogInformation("Managing module services...");
            var serviceResult = await StartModuleServicesAsync(moduleConfigResult.ModulesRequiringRestart, cancellationToken);
            result.Services = serviceResult;
            if (serviceResult.Success)
            {
                _logger.LogInformation($"Module services managed: {serviceResult.ServicesStarted.Count} started, {serviceResult.ServicesRestarted.Count} restarted");
                if (serviceResult.ServicesFailed.Count > 0)
                {
                    _logger.LogWarning($"  -> {serviceResult.ServicesFailed.Count} service(s) failed");
                }
            }
            else
            {
                _logger.LogWarning($"Service management failed: {serviceResult.Error}");
            }

            // Step 5: Apply firewall rules
            _logger.LogInformation("Applying firewall rules...");
            var firewallResult = await ApplyFirewallRulesAsync(cancellationToken);
            result.Firewall = firewallResult;
            if (firewallResult.Success)
            {
                _logger.LogInformation($"Firewall rules applied ({firewallResult.RulesApplied} rules)");
            }
            else
            {
                _logger.LogWarning($"Firewall application failed: {firewallResult.Error}");
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
    /// Apply system settings from database using the new ISettingsService.
    /// </summary>
    public async Task<SystemSettingsResult> ApplySystemSettingsAsync(CancellationToken cancellationToken = default)
    {
        var result = new SystemSettingsResult { Success = true };

        try
        {
            // Read settings from new system_configs table
            var hostname = await _settingsService.GetSystemConfigAsync<HostnameConfig>(SystemConfigKeys.Hostname);
            var timezone = await _settingsService.GetSystemConfigAsync<TimezoneConfig>(SystemConfigKeys.Timezone);
            var dns = await _settingsService.GetSystemConfigAsync<DnsConfig>(SystemConfigKeys.Dns);
            var ntp = await _settingsService.GetSystemConfigAsync<NtpConfig>(SystemConfigKeys.Ntp);

            // Apply each setting using its registered applier
            if (hostname != null)
            {
                var applier = _settingsService.GetApplier(SystemConfigKeys.Hostname);
                if (applier != null)
                {
                    var hostnameJson = System.Text.Json.JsonSerializer.Serialize(hostname);
                    var applyResult = await applier.ApplyAsync(SystemConfigKeys.Hostname, null, hostnameJson);
                    result.HostnameApplied = applyResult.Success;
                    if (!applyResult.Success)
                    {
                        _logger.LogWarning($"Hostname apply failed: {applyResult.Error}");
                    }
                    else
                    {
                        _logger.LogInformation($"  -> Hostname: {hostname.Hostname}");
                    }
                }
            }

            if (timezone != null)
            {
                var applier = _settingsService.GetApplier(SystemConfigKeys.Timezone);
                if (applier != null)
                {
                    var timezoneJson = System.Text.Json.JsonSerializer.Serialize(timezone);
                    var applyResult = await applier.ApplyAsync(SystemConfigKeys.Timezone, null, timezoneJson);
                    result.TimezoneApplied = applyResult.Success;
                    if (!applyResult.Success)
                    {
                        _logger.LogWarning($"Timezone apply failed: {applyResult.Error}");
                    }
                    else
                    {
                        _logger.LogInformation($"  -> Timezone: {timezone.Timezone}");
                    }
                }
            }

            if (dns != null && dns.Servers.Count > 0)
            {
                var applier = _settingsService.GetApplier(SystemConfigKeys.Dns);
                if (applier != null)
                {
                    var dnsJson = System.Text.Json.JsonSerializer.Serialize(dns);
                    var applyResult = await applier.ApplyAsync(SystemConfigKeys.Dns, null, dnsJson);
                    result.DnsApplied = applyResult.Success;
                    if (!applyResult.Success)
                    {
                        _logger.LogWarning($"DNS apply failed: {applyResult.Error}");
                    }
                    else
                    {
                        _logger.LogInformation($"  -> DNS: {string.Join(", ", dns.Servers)}");
                    }
                }
            }

            if (ntp != null && ntp.Servers.Count > 0)
            {
                var applier = _settingsService.GetApplier(SystemConfigKeys.Ntp);
                if (applier != null)
                {
                    var ntpJson = System.Text.Json.JsonSerializer.Serialize(ntp);
                    var applyResult = await applier.ApplyAsync(SystemConfigKeys.Ntp, null, ntpJson);
                    result.NtpApplied = applyResult.Success;
                    if (!applyResult.Success)
                    {
                        _logger.LogWarning($"NTP apply failed: {applyResult.Error}");
                    }
                    else
                    {
                        _logger.LogInformation($"  -> NTP: {string.Join(", ", ntp.Servers)}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply system settings");
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
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
                    _logger.LogInformation($"  -> IPv4 forwarding enabled: {ipForwardResult.AppliedValue}");
                }
                else
                {
                    _logger.LogWarning($"  -> IPv4 forwarding failed to apply: {ipForwardResult?.Error ?? "unknown error"}");
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

    /// <summary>
    /// Initialize operational state for all assigned interfaces.
    /// Captures current link state, IP addresses, and other runtime information.
    /// </summary>
    public async Task<OperationalStateStartupResult> InitializeOperationalStateAsync(CancellationToken cancellationToken = default)
    {
        var result = new OperationalStateStartupResult { Success = true };

        if (_operationalStateStore == null || _interfaceAssignmentStore == null || _networkInventory == null)
        {
            result.Success = true;
            result.InitializedCount = 0;
            return result;
        }

        try
        {
            // Get all interface assignments
            var assignments = await _interfaceAssignmentStore.GetAssignmentsAsync();
            if (assignments.Count == 0)
            {
                return result;
            }

            // Get current network state
            var interfaces = await _networkInventory.ListInterfacesAsync();
            var allAddresses = await _networkInventory.ListAddressesAsync(null, cancellationToken);
            var addressMap = allAddresses
                .GroupBy(a => a.Interface, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            var now = DateTime.UtcNow;
            var initialized = 0;
            var errors = new List<string>();

            foreach (var assignment in assignments)
            {
                try
                {
                    var iface = interfaces.FirstOrDefault(i =>
                        string.Equals(i.Name, assignment.InterfaceName, StringComparison.OrdinalIgnoreCase));

                    // Get or create operational state
                    var opState = await _operationalStateStore.GetAsync(assignment.InterfaceName)
                                  ?? new InterfaceOperationalStateEntity { InterfaceName = assignment.InterfaceName };

                    // Update link state
                    opState.LinkState = iface?.IsUp == true ? LinkState.Up : LinkState.Down;
                    opState.MacAddress = iface?.MacAddress;
                    opState.LastSeenAt = now;
                    opState.LastLinkChangeAt = now;

                    // Update IP addresses
                    if (addressMap.TryGetValue(assignment.InterfaceName, out var addresses))
                    {
                        var ipv4 = addresses.FirstOrDefault(a =>
                            string.Equals(a.Family, "inet", StringComparison.OrdinalIgnoreCase));
                        var ipv6 = addresses.FirstOrDefault(a =>
                            string.Equals(a.Family, "inet6", StringComparison.OrdinalIgnoreCase) &&
                            !a.Address.StartsWith("fe80:", StringComparison.OrdinalIgnoreCase));

                        if (ipv4 != null)
                        {
                            opState.CurrentIpv4Address = ipv4.Address;
                            opState.CurrentIpv4Prefix = ipv4.PrefixLength;
                        }

                        if (ipv6 != null)
                        {
                            opState.CurrentIpv6Address = ipv6.Address;
                            opState.CurrentIpv6Prefix = ipv6.PrefixLength;
                        }
                    }

                    // Determine health status
                    opState.HealthStatus = opState.LinkState == LinkState.Up
                        ? InterfaceHealthStatus.Healthy
                        : InterfaceHealthStatus.Down;

                    await _operationalStateStore.UpsertAsync(opState);
                    initialized++;

                    _logger.LogDebug($"  -> {assignment.InterfaceName}: {opState.LinkState}, {opState.CurrentIpv4Address ?? "no IPv4"}");
                }
                catch (Exception ex)
                {
                    errors.Add($"{assignment.InterfaceName}: {ex.Message}");
                }
            }

            result.InitializedCount = initialized;
            result.TotalCount = assignments.Count;
            result.Errors = errors;

            if (errors.Count > 0)
            {
                result.Success = false;
                result.Error = $"{errors.Count} interface(s) failed to initialize";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize operational state");
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }
}
