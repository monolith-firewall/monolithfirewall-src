using Monolith.FireWall.WebUI.Features.Firewall.Aliases;
using Monolith.FireWall.WebUI.Features.Firewall.Nat;
using Monolith.FireWall.WebUI.Features.Firewall.VirtualIps;
using Monolith.FireWall.WebUI.Features.Firewall.TrafficShaper;
using Monolith.FireWall.WebUI.Features.Firewall.Schedules;

namespace Monolith.FireWall.WebUI.Features.Firewall;

/// <summary>
/// Centralized service for firewall operations
/// </summary>
public class FirewallService
{
    private readonly AliasesManager _aliasesManager;
    private readonly NatManager _natManager;
    private readonly VirtualIpsManager _virtualIpsManager;
    private readonly TrafficShaperManager _trafficShaperManager;
    private readonly SchedulesManager _schedulesManager;
    private int _pendingChangesCount = 0;

    public FirewallService(
        AliasesManager aliasesManager,
        NatManager natManager,
        VirtualIpsManager virtualIpsManager,
        TrafficShaperManager trafficShaperManager,
        SchedulesManager schedulesManager)
    {
        _aliasesManager = aliasesManager;
        _natManager = natManager;
        _virtualIpsManager = virtualIpsManager;
        _trafficShaperManager = trafficShaperManager;
        _schedulesManager = schedulesManager;
    }

    // Managers
    public AliasesManager Aliases => _aliasesManager;
    public NatManager Nat => _natManager;
    public VirtualIpsManager VirtualIps => _virtualIpsManager;
    public TrafficShaperManager TrafficShaper => _trafficShaperManager;
    public SchedulesManager Schedules => _schedulesManager;

    // Status and configuration
    public async Task<FirewallStatus> GetStatusAsync()
    {
        return new FirewallStatus
        {
            IsActive = true, // TODO: Check actual firewall status
            PendingChanges = _pendingChangesCount,
            LastApplied = DateTime.UtcNow, // TODO: Track actual last apply time
            AliasesCount = (await _aliasesManager.ListAliasesAsync()).Count,
            NatRulesCount = (await _natManager.ListRulesAsync()).Count,
            VirtualIpsCount = (await _virtualIpsManager.ListVirtualIpsAsync()).Count,
            TrafficShaperRulesCount = (await _trafficShaperManager.ListRulesAsync()).Count,
            SchedulesCount = (await _schedulesManager.ListSchedulesAsync()).Count
        };
    }

    public async Task<FirewallConfig> GetConfigAsync()
    {
        return new FirewallConfig
        {
            Enabled = true,
            DefaultAction = "deny", // TODO: Get from actual config
            LogLevel = "info"
        };
    }

    public int GetPendingChangesCount()
    {
        return _pendingChangesCount;
    }

    public void MarkPendingChanges()
    {
        _pendingChangesCount++;
    }

    // Apply/Discard
    public async Task<bool> ApplyChangesAsync()
    {
        try
        {
            // TODO: Implement actual firewall rule application
            // 1. Read all configurations from database
            // 2. Generate iptables/ipset/tc rules
            // 3. Apply to system
            // 4. Clear pending changes
            
            _pendingChangesCount = 0;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DiscardChangesAsync()
    {
        try
        {
            // TODO: Implement discard logic
            // 1. Revert pending changes in database
            // 2. Clear pending changes
            
            _pendingChangesCount = 0;
            return true;
        }
        catch
        {
            return false;
        }
    }
}

public class FirewallStatus
{
    public bool IsActive { get; set; }
    public int PendingChanges { get; set; }
    public DateTime LastApplied { get; set; }
    public int AliasesCount { get; set; }
    public int NatRulesCount { get; set; }
    public int VirtualIpsCount { get; set; }
    public int TrafficShaperRulesCount { get; set; }
    public int SchedulesCount { get; set; }
}

public class FirewallConfig
{
    public bool Enabled { get; set; }
    public string DefaultAction { get; set; } = "deny";
    public string LogLevel { get; set; } = "info";
}
