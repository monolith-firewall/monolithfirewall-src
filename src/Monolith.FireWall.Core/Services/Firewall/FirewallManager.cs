using Monolith.FireWall.Core.Services.Platform;

namespace Monolith.FireWall.Core.Services.Firewall;

public sealed class FirewallManager
{
    public FirewallManager(PlatformCommandRunner commandRunner, InterfaceAssignmentStore interfaceStore)
    {
        Aliases = new FirewallAliasManager();
        Nat = new FirewallNatManager();
        NatSettings = new FirewallNatSettingsManager();
        Defaults = new FirewallDefaultsManager();
        Rules = new FirewallRulesManager(interfaceStore);
        ApplyManager = new FirewallApplyManager(Aliases, Nat, NatSettings, Rules, Defaults, interfaceStore, commandRunner);
    }

    public FirewallAliasManager Aliases { get; }
    public FirewallNatManager Nat { get; }
    public FirewallNatSettingsManager NatSettings { get; }
    public FirewallDefaultsManager Defaults { get; }
    public FirewallRulesManager Rules { get; }
    public FirewallApplyManager ApplyManager { get; }
}
