using CodeLogic;
using Monolith.FireWall.Common.Interfaces;
using Monolith.FireWall.Core.Services.Platform;

namespace Monolith.FireWall.Core.Services.Firewall;

public sealed class FirewallManager
{
    public FirewallManager(
        PlatformCommandRunner commandRunner,
        InterfaceAssignmentStore interfaceStore,
        ILogger logger,
        FirewallInterfaceIntegration? interfaceIntegration = null)
    {
        Aliases = new FirewallAliasManager();
        Nat = new FirewallNatManager();
        NatSettings = new FirewallNatSettingsManager();
        Defaults = new FirewallDefaultsManager();
        InterfaceSettings = new FirewallInterfaceSettingsManager();
        Rules = new FirewallRulesManager(interfaceStore);
        States = new FirewallStatesManager(commandRunner, logger, interfaceStore);
        ApplyManager = new FirewallApplyManager(Aliases, Nat, NatSettings, Rules, Defaults, InterfaceSettings, interfaceStore, commandRunner, interfaceIntegration);
    }

    public FirewallAliasManager Aliases { get; }
    public FirewallNatManager Nat { get; }
    public FirewallNatSettingsManager NatSettings { get; }
    public FirewallDefaultsManager Defaults { get; }
    public FirewallInterfaceSettingsManager InterfaceSettings { get; }
    public FirewallRulesManager Rules { get; }
    public FirewallStatesManager States { get; }
    public FirewallApplyManager ApplyManager { get; }
}
