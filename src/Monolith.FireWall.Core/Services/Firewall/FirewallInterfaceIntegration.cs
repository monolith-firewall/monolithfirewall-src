using System.Net;
using Monolith.FireWall.Common.Interfaces;
using Monolith.FireWall.Common.Services;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Services.Firewall;

/// <summary>
/// Integrates firewall rules with interface configuration.
/// Handles dynamic alias resolution, orphan rule detection, and role change handling.
/// </summary>
public sealed class FirewallInterfaceIntegration : INetworkStateListener
{
    private readonly FirewallDynamicAliasStore _dynamicAliasStore;
    private readonly InterfaceOperationalStateStore _operationalStateStore;
    private readonly InterfaceAssignmentStore _assignmentStore;
    private readonly LoggingManager _loggingManager;

    // Cache for resolved dynamic alias values
    private readonly Dictionary<string, string> _resolvedAliasCache = new();
    private readonly object _cacheLock = new();
    private DateTime _cacheExpiry = DateTime.MinValue;
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(30);

    public FirewallInterfaceIntegration(
        FirewallDynamicAliasStore dynamicAliasStore,
        InterfaceOperationalStateStore operationalStateStore,
        InterfaceAssignmentStore assignmentStore)
    {
        _dynamicAliasStore = dynamicAliasStore;
        _operationalStateStore = operationalStateStore;
        _assignmentStore = assignmentStore;
        _loggingManager = LoggingManager.Instance;
    }

    // ========================================================================
    // Dynamic Alias Resolution
    // ========================================================================

    /// <summary>
    /// Resolves a dynamic alias name to its current value.
    /// </summary>
    public async Task<string?> ResolveAliasAsync(string aliasName)
    {
        // Check cache first
        lock (_cacheLock)
        {
            if (DateTime.UtcNow < _cacheExpiry && _resolvedAliasCache.TryGetValue(aliasName, out var cached))
            {
                return cached;
            }
        }

        var alias = await _dynamicAliasStore.GetByNameAsync(aliasName);
        if (alias == null)
        {
            return null;
        }

        var resolved = await ResolveAliasCoreAsync(alias);

        // Update cache
        if (!string.IsNullOrEmpty(resolved))
        {
            lock (_cacheLock)
            {
                _resolvedAliasCache[aliasName] = resolved;
                _cacheExpiry = DateTime.UtcNow.Add(CacheLifetime);
            }
        }

        return resolved;
    }

    /// <summary>
    /// Resolves all dynamic aliases and returns a dictionary of name -> value.
    /// </summary>
    public async Task<Dictionary<string, string>> ResolveAllAliasesAsync()
    {
        var result = new Dictionary<string, string>();
        var aliases = await _dynamicAliasStore.GetAllAsync();

        foreach (var alias in aliases)
        {
            var resolved = await ResolveAliasCoreAsync(alias);
            if (!string.IsNullOrEmpty(resolved))
            {
                result[alias.Name] = resolved;
            }
        }

        // Update cache
        lock (_cacheLock)
        {
            _resolvedAliasCache.Clear();
            foreach (var kvp in result)
            {
                _resolvedAliasCache[kvp.Key] = kvp.Value;
            }
            _cacheExpiry = DateTime.UtcNow.Add(CacheLifetime);
        }

        return result;
    }

    /// <summary>
    /// Resolves alias text in a rule value, replacing $alias_name with actual values.
    /// </summary>
    public async Task<string> ResolveAliasesInTextAsync(string text)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains('$'))
        {
            return text;
        }

        var aliases = await ResolveAllAliasesAsync();
        var result = text;

        foreach (var alias in aliases)
        {
            result = result.Replace($"${alias.Key}", alias.Value);
        }

        return result;
    }

    private async Task<string?> ResolveAliasCoreAsync(FirewallDynamicAliasEntity alias)
    {
        var opState = await _operationalStateStore.GetAsync(alias.InterfaceName);
        if (opState == null)
        {
            return null;
        }

        var ipv4 = alias.AddressFamily == "ipv4";

        return alias.AliasType switch
        {
            DynamicAliasType.InterfaceIp => ipv4
                ? opState.CurrentIpv4Address
                : opState.CurrentIpv6Address,

            DynamicAliasType.InterfaceSubnet => ipv4
                ? CalculateSubnet(opState.CurrentIpv4Address, opState.CurrentIpv4Prefix)
                : CalculateSubnet(opState.CurrentIpv6Address, opState.CurrentIpv6Prefix),

            DynamicAliasType.InterfaceNetwork => ipv4
                ? FormatNetwork(opState.CurrentIpv4Address, opState.CurrentIpv4Prefix)
                : FormatNetwork(opState.CurrentIpv6Address, opState.CurrentIpv6Prefix),

            DynamicAliasType.GatewayAddress => opState.DhcpGateway,

            _ => null
        };
    }

    // ========================================================================
    // Standard Alias Setup
    // ========================================================================

    /// <summary>
    /// Ensures standard dynamic aliases exist for all assigned interfaces.
    /// </summary>
    public async Task EnsureStandardAliasesAsync()
    {
        var assignments = await _assignmentStore.GetAssignmentsAsync();

        foreach (var assignment in assignments)
        {
            var roleName = assignment.Role switch
            {
                InterfaceRole.Wan => "wan",
                InterfaceRole.Lan => "lan",
                InterfaceRole.Opt => $"opt{assignment.InterfaceName}",
                _ => null
            };

            if (!string.IsNullOrEmpty(roleName))
            {
                await _dynamicAliasStore.EnsureStandardAliasesAsync(assignment.InterfaceName, roleName);
            }
        }
    }

    /// <summary>
    /// Updates aliases when an interface role changes.
    /// </summary>
    public async Task OnInterfaceRoleChangedAsync(string interfaceName, InterfaceRole oldRole, InterfaceRole newRole)
    {
        // Remove old aliases if role was assigned
        if (oldRole != InterfaceRole.Unknown)
        {
            await _dynamicAliasStore.DeleteByInterfaceAsync(interfaceName);
        }

        // Create new aliases if role is assigned
        if (newRole != InterfaceRole.Unknown)
        {
            var roleName = newRole switch
            {
                InterfaceRole.Wan => "wan",
                InterfaceRole.Lan => "lan",
                InterfaceRole.Opt => $"opt{interfaceName}",
                _ => null
            };

            if (!string.IsNullOrEmpty(roleName))
            {
                await _dynamicAliasStore.EnsureStandardAliasesAsync(interfaceName, roleName);
            }
        }

        await _loggingManager.LogSystemAsync(
            "Firewall",
            "info",
            "InterfaceIntegration",
            $"Updated dynamic aliases for interface '{interfaceName}' role change: {oldRole} -> {newRole}");
    }

    // ========================================================================
    // Orphan Rule Detection
    // ========================================================================

    /// <summary>
    /// Checks for firewall rules that reference interfaces that no longer exist.
    /// </summary>
    public async Task<List<OrphanRuleInfo>> GetOrphanRulesAsync(List<FirewallRuleView> rules)
    {
        var orphans = new List<OrphanRuleInfo>();
        var assignments = await _assignmentStore.GetAssignmentsAsync();
        var assignedInterfaces = new HashSet<string>(
            assignments.Select(a => a.InterfaceName),
            StringComparer.OrdinalIgnoreCase);

        foreach (var rule in rules)
        {
            if (!assignedInterfaces.Contains(rule.Interface))
            {
                orphans.Add(new OrphanRuleInfo
                {
                    RuleId = rule.Id,
                    RuleNumber = rule.RuleNumber,
                    Interface = rule.Interface,
                    Description = rule.Description,
                    Reason = "Interface not assigned"
                });
            }
        }

        return orphans;
    }

    /// <summary>
    /// Checks for NAT rules that reference non-existent interfaces.
    /// </summary>
    public async Task<List<OrphanRuleInfo>> GetOrphanNatRulesAsync(List<FirewallNatRuleView> rules)
    {
        var orphans = new List<OrphanRuleInfo>();
        var assignments = await _assignmentStore.GetAssignmentsAsync();
        var assignedInterfaces = new HashSet<string>(
            assignments.Select(a => a.InterfaceName),
            StringComparer.OrdinalIgnoreCase);

        foreach (var rule in rules)
        {
            if (!string.IsNullOrEmpty(rule.Interface) && !assignedInterfaces.Contains(rule.Interface))
            {
                orphans.Add(new OrphanRuleInfo
                {
                    RuleId = rule.Id,
                    RuleNumber = 0,
                    Interface = rule.Interface,
                    Description = rule.Description ?? $"NAT Rule ID {rule.Id}",
                    Reason = "Interface not assigned"
                });
            }
        }

        return orphans;
    }

    // ========================================================================
    // INetworkStateListener Implementation (Common interface)
    // ========================================================================

    public async Task OnInterfaceStateChangedAsync(NetworkInterfaceChange change, CancellationToken cancellationToken)
    {
        switch (change.ChangeType)
        {
            case InterfaceChangeType.IpChanged:
            case InterfaceChangeType.IpAdded:
                // Invalidate cache when IP changes
                InvalidateCache();

                // Log for firewall alias resolution
                await _loggingManager.LogSystemAsync(
                    "Firewall",
                    "debug",
                    "InterfaceIntegration",
                    $"IP changed on '{change.InterfaceName}' - dynamic aliases will be re-resolved");
                break;

            case InterfaceChangeType.InterfaceRemoved:
                // Clean up aliases for removed interface
                await _dynamicAliasStore.DeleteByInterfaceAsync(change.InterfaceName);

                await _loggingManager.LogSystemAsync(
                    "Firewall",
                    "warning",
                    "InterfaceIntegration",
                    $"Interface '{change.InterfaceName}' removed - cleaned up dynamic aliases, check for orphan rules");
                break;
        }
    }

    public async Task OnGatewayHealthChangedAsync(NetworkGatewayChange change, CancellationToken cancellationToken)
    {
        // Gateway health changes don't affect firewall aliases directly
        // Log for awareness
        if (change.NewStatus == "offline")
        {
            await _loggingManager.LogSystemAsync(
                "Firewall",
                "debug",
                "InterfaceIntegration",
                $"Gateway '{change.GatewayName}' went offline - outbound routing may be affected");
        }
    }

    public Task OnLinkStateChangedAsync(NetworkLinkChange change, CancellationToken cancellationToken)
    {
        // Link state changes handled via the interface state change
        return Task.CompletedTask;
    }

    // ========================================================================
    // Interface Validation for Rules
    // ========================================================================

    /// <summary>
    /// Validates that all interfaces referenced in rules exist.
    /// </summary>
    public async Task<List<string>> ValidateRuleInterfacesAsync(List<FirewallRuleView> rules)
    {
        var errors = new List<string>();
        var assignments = await _assignmentStore.GetAssignmentsAsync();
        var assignedInterfaces = new HashSet<string>(
            assignments.Select(a => a.InterfaceName),
            StringComparer.OrdinalIgnoreCase);

        foreach (var rule in rules.Where(r => r.Enabled))
        {
            if (!assignedInterfaces.Contains(rule.Interface))
            {
                errors.Add($"Rule #{rule.RuleNumber} on '{rule.Interface}': interface not assigned");
            }
        }

        return errors;
    }

    /// <summary>
    /// Gets the operational state for an interface, useful for rule generation.
    /// </summary>
    public async Task<InterfaceOperationalStateView?> GetInterfaceStateAsync(string interfaceName)
    {
        var entity = await _operationalStateStore.GetAsync(interfaceName);
        if (entity == null)
        {
            return null;
        }

        return new InterfaceOperationalStateView
        {
            InterfaceName = entity.InterfaceName,
            LinkState = entity.LinkState.ToString().ToLowerInvariant(),
            MacAddress = entity.MacAddress,
            SpeedMbps = entity.SpeedMbps,
            Duplex = entity.Duplex,
            Mtu = entity.Mtu,
            CurrentIpv4Address = entity.CurrentIpv4Address,
            CurrentIpv4Prefix = entity.CurrentIpv4Prefix,
            CurrentIpv6Address = entity.CurrentIpv6Address,
            CurrentIpv6Prefix = entity.CurrentIpv6Prefix,
            DhcpServerAddress = entity.DhcpServerAddress,
            DhcpLeaseObtained = entity.DhcpLeaseObtained,
            DhcpLeaseExpires = entity.DhcpLeaseExpires,
            DhcpGateway = entity.DhcpGateway,
            HealthStatus = entity.HealthStatus.ToString().ToLowerInvariant(),
            LastSeenAt = entity.LastSeenAt,
            LastLinkChangeAt = entity.LastLinkChangeAt,
            LastIpChangeAt = entity.LastIpChangeAt
        };
    }

    // ========================================================================
    // Helpers
    // ========================================================================

    private void InvalidateCache()
    {
        lock (_cacheLock)
        {
            _resolvedAliasCache.Clear();
            _cacheExpiry = DateTime.MinValue;
        }
    }

    private static string? CalculateSubnet(string? address, int? prefix)
    {
        if (string.IsNullOrEmpty(address) || !prefix.HasValue)
        {
            return null;
        }

        try
        {
            var ip = IPAddress.Parse(address);
            var bytes = ip.GetAddressBytes();
            var maskBits = prefix.Value;

            // Calculate network address
            for (int i = 0; i < bytes.Length; i++)
            {
                if (maskBits >= 8)
                {
                    maskBits -= 8;
                }
                else if (maskBits > 0)
                {
                    bytes[i] &= (byte)(0xFF << (8 - maskBits));
                    maskBits = 0;
                }
                else
                {
                    bytes[i] = 0;
                }
            }

            return new IPAddress(bytes).ToString();
        }
        catch
        {
            return null;
        }
    }

    private static string? FormatNetwork(string? address, int? prefix)
    {
        var subnet = CalculateSubnet(address, prefix);
        if (subnet == null || !prefix.HasValue)
        {
            return null;
        }

        return $"{subnet}/{prefix.Value}";
    }
}

/// <summary>
/// Information about a firewall rule referencing a non-existent interface.
/// </summary>
public sealed class OrphanRuleInfo
{
    public int RuleId { get; set; }
    public int RuleNumber { get; set; }
    public string Interface { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Reason { get; set; } = string.Empty;
}
