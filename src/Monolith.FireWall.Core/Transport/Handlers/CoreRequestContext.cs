using Monolith.FireWall.Common.Interfaces;
using Monolith.FireWall.Core.Services;
using Monolith.FireWall.Core.Services.Firewall;
using Monolith.FireWall.Core.Services.Settings;

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
        FirewallManager firewallManager,
        StartupManager startupManager,
        WebUiSettingsManager webUiSettingsManager,
        WebUiServiceManager webUiServiceManager,
        BackupManager backupManager,
        Services.Platform.PlatformCommandRunner commandRunner,
        ISettingsService configService,
        GatewayGroupManager? gatewayGroupManager = null,
        GatewayHealthMonitor? gatewayHealthMonitor = null,
        InterfaceOperationalStateStore? operationalStateStore = null,
        GatewayHealthStore? gatewayHealthStore = null)
    {
        Logger = logger;
        ModuleRegistry = moduleRegistry;
        PackageStateStore = packageStateStore;
        PackageInstaller = packageInstaller;
        InterfaceAssignments = interfaceAssignments;
        RoutingManager = routingManager;
        TuneablesManager = tuneablesManager;
        MonitoringManager = monitoringManager;
        FirewallManager = firewallManager;
        StartupManager = startupManager;
        WebUiSettingsManager = webUiSettingsManager;
        WebUiServiceManager = webUiServiceManager;
        BackupManager = backupManager;
        CommandRunner = commandRunner;
        ConfigService = configService;
        GatewayGroupManager = gatewayGroupManager;
        GatewayHealthMonitor = gatewayHealthMonitor;
        OperationalStateStore = operationalStateStore;
        GatewayHealthStore = gatewayHealthStore;
    }

    public ILogger Logger { get; }
    public ModuleRegistry ModuleRegistry { get; }
    public PackageStateStore PackageStateStore { get; }
    public PackageInstaller PackageInstaller { get; }
    public InterfaceAssignmentManager InterfaceAssignments { get; }
    public RoutingManager RoutingManager { get; }
    public SystemTuneablesManager TuneablesManager { get; }
    public MonitoringManager MonitoringManager { get; }
    public FirewallManager FirewallManager { get; }
    public StartupManager StartupManager { get; }
    public WebUiSettingsManager WebUiSettingsManager { get; }
    public WebUiServiceManager WebUiServiceManager { get; }
    public BackupManager BackupManager { get; }
    public Services.Platform.PlatformCommandRunner CommandRunner { get; }

    /// <summary>
    /// Central configuration service for staged changes workflow.
    /// </summary>
    public ISettingsService ConfigService { get; }

    /// <summary>
    /// Gateway group manager for multi-WAN failover/load balancing.
    /// </summary>
    public GatewayGroupManager? GatewayGroupManager { get; }

    /// <summary>
    /// Gateway health monitor for health checks.
    /// </summary>
    public GatewayHealthMonitor? GatewayHealthMonitor { get; }

    /// <summary>
    /// Interface operational state store for real-time network state.
    /// </summary>
    public InterfaceOperationalStateStore? OperationalStateStore { get; }

    /// <summary>
    /// Gateway health store for health status data.
    /// </summary>
    public GatewayHealthStore? GatewayHealthStore { get; }
}
