using Monolith.FireWall.Common.Interfaces;
using Monolith.FireWall.Core.Services;
using Monolith.FireWall.Core.Services.Firewall;

namespace Monolith.FireWall.Core.Transport.Handlers;

public sealed class CoreRequestContext
{
    public CoreRequestContext(
        ILogger logger,
        ModuleRegistry moduleRegistry,
        PackageStateStore packageStateStore,
        PackageInstaller packageInstaller,
        InterfaceAssignmentManager interfaceAssignments,
        RoutingManager routingManager,
        SystemTuneablesManager tuneablesManager,
        MonitoringManager monitoringManager,
        SystemSettingsManager settingsManager,
        FirewallManager firewallManager,
        StartupManager startupManager,
        WebUiSettingsManager webUiSettingsManager,
        WebUiServiceManager webUiServiceManager)
    {
        Logger = logger;
        ModuleRegistry = moduleRegistry;
        PackageStateStore = packageStateStore;
        PackageInstaller = packageInstaller;
        InterfaceAssignments = interfaceAssignments;
        RoutingManager = routingManager;
        TuneablesManager = tuneablesManager;
        MonitoringManager = monitoringManager;
        SettingsManager = settingsManager;
        FirewallManager = firewallManager;
        StartupManager = startupManager;
        WebUiSettingsManager = webUiSettingsManager;
        WebUiServiceManager = webUiServiceManager;
    }

    public ILogger Logger { get; }
    public ModuleRegistry ModuleRegistry { get; }
    public PackageStateStore PackageStateStore { get; }
    public PackageInstaller PackageInstaller { get; }
    public InterfaceAssignmentManager InterfaceAssignments { get; }
    public RoutingManager RoutingManager { get; }
    public SystemTuneablesManager TuneablesManager { get; }
    public MonitoringManager MonitoringManager { get; }
    public SystemSettingsManager SettingsManager { get; }
    public FirewallManager FirewallManager { get; }
    public StartupManager StartupManager { get; }
    public WebUiSettingsManager WebUiSettingsManager { get; }
    public WebUiServiceManager WebUiServiceManager { get; }
}
