using CodeLogic;
using Monolith.FireWall.Core.Services;
using Monolith.FireWall.Core.Transport;
using CodeLogic.Logging;
using Adapter = Monolith.FireWall.Core.Services.ModuleContextAdapter;
using Monolith.FireWall.Core.Services.Platform;
using Monolith.FireWall.Common.Models;
using Monolith.FireWall.Core.Models;
using Monolith.FireWall.Core.Services.Firewall;
using System.Reflection;

namespace Monolith.FireWall.Core;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("═══════════════════════════════════════════════════");
        Console.WriteLine("  MonolithFireWall Core v1.0.0");
        Console.WriteLine("═══════════════════════════════════════════════════\n");

        // Phase 1: Initialize CodeLogic
        Console.WriteLine("[1/4] Initializing CodeLogic framework...");
        var initResult = await CodeLogic.CodeLogic.InitializeAsync(opts =>
        {
            opts.RootDirectory = "/var/lib/monolith-firewall/codelogic";
            opts.PluginsDirectory = "/var/lib/monolith-firewall/plugins";
        });

        if (!initResult.Success)
        {
            Console.WriteLine($"ERROR: Initialization failed: {initResult.Message}");
            return;
        }

        if (initResult.IsFirstRun)
        {
            Console.WriteLine("\n════════════════════════════════════════════════════");
            Console.WriteLine("  FIRST RUN DETECTED");
            Console.WriteLine("════════════════════════════════════════════════════");
            Console.WriteLine($"\nConfiguration generated at:");
            Console.WriteLine($"  /var/lib/monolith-firewall/codelogic/CodeLogic.json");
            Console.WriteLine($"\nPackages directory created at:");
            Console.WriteLine($"  /var/lib/monolith-firewall/codelogic/Packages/");
            Console.WriteLine($"\nContinuing with startup (first run complete)...");
            Console.WriteLine("════════════════════════════════════════════════════\n");
            // Continue instead of returning - first run is just config generation
        }

        Console.WriteLine("✓ CodeLogic initialized");

        // Phase 2: Configure
        Console.WriteLine("[2/4] Configuring libraries...");
        await CodeLogic.CodeLogic.ConfigureAsync();
        Console.WriteLine("✓ Libraries configured");

        // Phase 3: Initialize and Start
        Console.WriteLine("[3/4] Initializing and starting libraries...");
        await CodeLogic.CodeLogic.StartAsync();
        Console.WriteLine("✓ All libraries started\n");

        // Phase 4: Start Core components
        Console.WriteLine("[4/4] Starting Core components...");

        // Load Core configuration
        var config = LoadCoreConfiguration();
        Console.WriteLine($"  ✓ Loaded configuration (Packages: {config.PackagesDirectory}, Logs: {config.LogDirectory})");

        // Ensure log directory exists
        Directory.CreateDirectory(config.LogDirectory);

        // Create CodeLogic logger
        var coreLogger = new Logger("CORE", config.LogDirectory, LogLevel.Info, new LoggingOptions
        {
            EnableConsoleOutput = true,
            EnableDebugMode = config.EnableDebugMode
        });
        var logger = new CodeLogicLoggerAdapter(coreLogger);

        // Get CL.SQLite
        var sqlite = CodeLogic.Libs.Get<CL.SQLite.SQLiteLibrary>();
        if (sqlite == null)
        {
            Console.WriteLine("  ✗ CL.SQLite library not found");
            return;
        }
        Console.WriteLine("  ✓ CL.SQLite library loaded");

        try
        {
            if (sqlite.TableSyncService == null)
            {
                Console.WriteLine("  ⚠ SQLite table sync service not available");
            }
            else
            {
                await sqlite.TableSyncService.SyncTableAsync<InterfaceAssignmentEntity>();
                await sqlite.TableSyncService.SyncTableAsync<GatewayEntity>();
                await sqlite.TableSyncService.SyncTableAsync<StaticRouteEntity>();
                await sqlite.TableSyncService.SyncTableAsync<PackageInstallationEntity>();
                await sqlite.TableSyncService.SyncTableAsync<ModuleStateEntity>();
                await sqlite.TableSyncService.SyncTableAsync<LogEntryEntity>();
                await sqlite.TableSyncService.SyncTableAsync<SystemTuneableEntity>();
                await sqlite.TableSyncService.SyncTableAsync<SystemSettingsEntity>();
                await sqlite.TableSyncService.SyncTableAsync<MonitorDefinitionEntity>();
                await sqlite.TableSyncService.SyncTableAsync<MonitorStatusEntity>();
                await sqlite.TableSyncService.SyncTableAsync<SystemNotificationEntity>();
                await sqlite.TableSyncService.SyncTableAsync<FirewallAliasEntity>();
                await sqlite.TableSyncService.SyncTableAsync<FirewallAliasEntryEntity>();
                await sqlite.TableSyncService.SyncTableAsync<FirewallNatRuleEntity>();
                await sqlite.TableSyncService.SyncTableAsync<FirewallNatSettingsEntity>();
                await sqlite.TableSyncService.SyncTableAsync<FirewallDefaultsEntity>();
                await sqlite.TableSyncService.SyncTableAsync<FirewallInterfaceSettingsEntity>();
                await sqlite.TableSyncService.SyncTableAsync<FirewallScheduleEntity>();
                await sqlite.TableSyncService.SyncTableAsync<FirewallRuleEntity>();
                await sqlite.TableSyncService.SyncTableAsync<WebUiSettingsEntity>();
                
                // Sync DHCP tables (from monolith-network package)
                // Note: These are in a package, but we sync them here to ensure tables exist
                try
                {
                    // Use reflection to sync DHCP entities if the package is loaded
                    var dhcpInterfaceType = Type.GetType("Monolith.Network.Modules.Dhcp.DhcpInterfaceEntity, Monolith.Network");
                    var dhcpSettingsType = Type.GetType("Monolith.Network.Modules.Dhcp.DhcpSettingsEntity, Monolith.Network");
                    var dhcpLeaseType = Type.GetType("Monolith.Network.Modules.Dhcp.DhcpLeaseEntity, Monolith.Network");
                    
                    if (dhcpInterfaceType != null)
                    {
                        var syncMethod = typeof(CL.SQLite.Services.TableSyncService).GetMethod("SyncTableAsync", new[] { typeof(CancellationToken) });
                        if (syncMethod != null)
                        {
                            var genericMethod = syncMethod.MakeGenericMethod(dhcpInterfaceType);
                            await (Task)genericMethod.Invoke(sqlite.TableSyncService, new object[] { CancellationToken.None })!;
                        }
                    }
                    if (dhcpSettingsType != null)
                    {
                        var syncMethod = typeof(CL.SQLite.Services.TableSyncService).GetMethod("SyncTableAsync", new[] { typeof(CancellationToken) });
                        if (syncMethod != null)
                        {
                            var genericMethod = syncMethod.MakeGenericMethod(dhcpSettingsType);
                            await (Task)genericMethod.Invoke(sqlite.TableSyncService, new object[] { CancellationToken.None })!;
                        }
                    }
                    if (dhcpLeaseType != null)
                    {
                        var syncMethod = typeof(CL.SQLite.Services.TableSyncService).GetMethod("SyncTableAsync", new[] { typeof(CancellationToken) });
                        if (syncMethod != null)
                        {
                            var genericMethod = syncMethod.MakeGenericMethod(dhcpLeaseType);
                            await (Task)genericMethod.Invoke(sqlite.TableSyncService, new object[] { CancellationToken.None })!;
                        }
                    }
                }
                catch (Exception ex)
                {
                    coreLogger.Info($"Could not sync DHCP tables (package may not be loaded yet): {ex.Message}");
                }
                Console.WriteLine("  ✓ Core database tables synchronized");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ Failed to sync Core tables: {ex.Message}");
        }

        // Create Core services
        var packageScanner = new PackageScanner(logger);
        var packageLoader = new PackageLoader(logger);
        var packageStateStore = new PackageStateStore();
        var moduleRegistry = new ModuleRegistry(logger, packageStateStore);
        var setupManager = new SetupManager(logger, moduleRegistry);
        var platformPolicy = new Services.Platform.PlatformPolicyStore(config.PlatformPolicyPath);
        var platformExecutor = new Services.Platform.PlatformExecutor(moduleRegistry, platformPolicy);
        var interfaceAssignmentStore = new InterfaceAssignmentStore();
        var commandRunner = new Services.Platform.PlatformCommandRunner();
        var networkInventory = new NetworkInventoryService(commandRunner);
        var interfaceConfigManager = new InterfaceConfigManager();
        var settingsCommandRunner = new Services.Platform.PlatformCommandRunner();
        var settingsManager = new SystemSettingsManager(new SystemSettingsStore(), settingsCommandRunner);
        var firewallManager = new Services.Firewall.FirewallManager(commandRunner, interfaceAssignmentStore);
        var tuneablesManager = new SystemTuneablesManager(
            new SystemTuneablesStore(),
            commandRunner);
        var interfaceAssignmentManager = new InterfaceAssignmentManager(
            interfaceAssignmentStore,
            networkInventory,
            interfaceConfigManager,
            commandRunner,
            settingsManager,
            tuneablesManager);
        var gatewayStore = new GatewayStore();
        var gatewayManager = new GatewayManager(gatewayStore, commandRunner);
        var gatewaySyncService = new GatewaySyncService(gatewayManager);
        var routingManager = new RoutingManager(
            new RoutingStore(),
            gatewayManager,
            commandRunner,
            networkInventory);
        var monitoringStore = new MonitoringStore();
        var monitoringManager = new MonitoringManager(
            monitoringStore,
            commandRunner,
            routingManager);
        
        // Create StartupManager for boot initialization
        var interfaceConfigApplier = new InterfaceConfigApplier(
            logger,
            interfaceAssignmentStore,
            interfaceConfigManager,
            settingsManager);
        var moduleConfigGenerator = new ModuleConfigGenerator(
            logger,
            moduleRegistry,
            platformExecutor);
        var moduleServiceManager = new ModuleServiceManager(
            logger,
            moduleRegistry,
            commandRunner);
        var startupManager = new StartupManager(
            logger,
            settingsManager,
            tuneablesManager,
            interfaceConfigApplier,
            firewallManager.ApplyManager,
            moduleConfigGenerator,
            moduleServiceManager);
        
        // Create WebUI services
        var webUiSettingsManager = new WebUiSettingsManager(logger);
        var webUiServiceManager = new WebUiServiceManager(logger, commandRunner);
        
        // Create Backup manager
        var backupManager = new Services.BackupManager(logger, commandRunner);
        
        var socketListener = new UnixSocketListener(
            logger,
            moduleRegistry,
            platformExecutor,
            packageStateStore,
            new PackageInstaller(logger, packageScanner, packageLoader, moduleRegistry, packageStateStore, commandRunner, config),
            interfaceAssignmentManager,
            routingManager,
            tuneablesManager,
            monitoringManager,
            settingsManager,
            firewallManager,
            setupManager,
            startupManager,
            webUiSettingsManager,
            webUiServiceManager,
            backupManager,
            commandRunner,
            config.SocketPath,
            config.MaxConcurrentConnections
        );

        // Phase 2: Scan for packages
        coreLogger.Info($"Scanning for packages in {config.PackagesDirectory}");
        var discoveredPackages = await packageScanner.ScanPackagesAsync(config.PackagesDirectory);
        coreLogger.Info($"Found {discoveredPackages.Count} package(s)");

        // Get localization settings from CodeLogic
        var codeLogicConfig = CodeLogic.CodeLogic.GetConfiguration();

        // Load each discovered package
        foreach (var discoveryInfo in discoveredPackages)
        {
            try
            {
                // Load package (supports RCL with Views DLL)
                var packageInfo = await packageLoader.LoadPackageAsync(discoveryInfo);
                
                if (packageInfo.HasRazorViews)
                {
                    coreLogger.Info($"Package has Razor views: {discoveryInfo.ViewsDllPath}");
                    coreLogger.Info($"  → Discovered {packageInfo.DiscoveredViews.Count} view(s)");
                    foreach (var view in packageInfo.DiscoveredViews)
                    {
                        coreLogger.Info($"    - {view.Route} -> {view.RazorPath}");
                    }
                }

                    // Setup localization for this package
                    var packageLocDir = Path.Combine("/var/lib/monolith-firewall/codelogic/localization", packageInfo.Definition.Id);
                    var packageLocManager = new CodeLogic.Localization.LocalizationManager(
                        packageLocDir,
                        codeLogicConfig.Localization.DefaultCulture
                    );

                    // Register package localizations
                    packageInfo.Package.RegisterLocalizations(packageLocManager);

                    // Generate and load localization files
                    await packageLocManager.GenerateAllTemplatesAsync(codeLogicConfig.Localization.SupportedCultures);
                    await packageLocManager.LoadAllAsync(codeLogicConfig.Localization.SupportedCultures);

                    // Create package context
                    var packageContext = new ProgramPackageContext(logger, packageInfo.Definition.Id, packageLocManager);
                    await packageInfo.Package.OnLoadAsync(packageContext);

                    // Register modules
                    moduleRegistry.RegisterPackage(packageInfo);

                    // Ensure modules are enabled by default (if not already set)
                    await EnsureModulesEnabledAsync(packageInfo, packageStateStore, logger);

                    // Sync database tables for this package (if not already done during installation)
                    await SyncPackageTablesAsync(packageInfo, sqlite, logger);

                    // Apply firewall intents (managed rules)
                    await ApplyFirewallIntentsAsync(discoveryInfo.Manifest, firewallManager, interfaceAssignmentStore, logger);

                    // Start lifecycle-aware modules
                    foreach (var moduleInfo in moduleRegistry.GetAllModules())
                    {
                        if (moduleInfo.Package.Definition.Id == packageInfo.Definition.Id &&
                            moduleInfo.Module is Monolith.FireWall.Common.Interfaces.IMonolithModuleLifecycle lifecycle)
                        {
                            var defaultCapabilities = PlatformCapabilityMapper.FromSystemPermissions(moduleInfo.Module.GetSystemPermissions());
                            var moduleContext = new Adapter(
                                logger,
                                packageInfo.Definition.Id,
                                moduleInfo.Module.Id,
                                platformExecutor,
                                defaultCapabilities);
                            await lifecycle.OnStartAsync(moduleContext);
                        }
                    }

                    coreLogger.Info($"Loaded package: {packageInfo.Definition.Name} v{packageInfo.Definition.Version}");
                    if (packageInfo.HasRazorViews)
                    {
                        coreLogger.Info($"  → Razor views available in Views assembly");
                    }
                }
                catch (Exception ex)
                {
                    coreLogger.Error($"Failed to load package {discoveryInfo.Manifest.Id}: {ex.Message}", ex);
                }
        }

        // Create shutdown token
        var cts = new CancellationTokenSource();

        // Start named pipe listener
        Console.WriteLine("→ Starting Unix socket listener...");
        socketListener.Start();
        Console.WriteLine("✓ Unix socket listener started");
        gatewaySyncService.Start(cts.Token);
        Console.WriteLine("✓ Gateway sync service started");
        monitoringManager.Start(cts.Token);
        Console.WriteLine("✓ Monitoring scheduler started");
        coreLogger.Info("Core components started");
        Console.WriteLine("✓ Core components started\n");

        Console.WriteLine("═══════════════════════════════════════════════════");
        Console.WriteLine("  MonolithFireWall Core is running");
        Console.WriteLine("  Press Ctrl+C to stop");
        Console.WriteLine("═══════════════════════════════════════════════════\n");

        // Wait for shutdown signal
        Console.CancelKeyPress += (s, e) =>
        {
            Console.WriteLine("\n\nShutdown signal received...");
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            await Task.Delay(-1, cts.Token);
        }
        catch (TaskCanceledException)
        {
            // Expected when canceled
        }

        // Stop
        Console.WriteLine("[4/4] Stopping libraries...");
        await CodeLogic.CodeLogic.StopAsync();
        Console.WriteLine("✓ All libraries stopped\n");

        Console.WriteLine("════════════════════════════════════════════════════");
        Console.WriteLine("  MonolithFireWall Core stopped gracefully");
        Console.WriteLine("════════════════════════════════════════════════════\n");
    }

    static Configuration.CoreConfiguration LoadCoreConfiguration()
    {
        var configPath = "/var/lib/monolith-firewall/codelogic/core-config.json";

        if (File.Exists(configPath))
        {
            var json = File.ReadAllText(configPath);
            var config = System.Text.Json.JsonSerializer.Deserialize<Configuration.CoreConfiguration>(json);
            if (config != null)
                return config;
        }

        // Return defaults if file doesn't exist
        return new Configuration.CoreConfiguration();
    }

    private static async Task ApplyFirewallIntentsAsync(
        PackageManifest manifest,
        FirewallManager firewallManager,
        InterfaceAssignmentStore interfaceStore,
        Monolith.FireWall.Common.Interfaces.ILogger logger)
    {
        if (manifest.FirewallIntents == null || manifest.FirewallIntents.Length == 0)
        {
            return;
        }

        var assignments = await interfaceStore.GetAssignmentsAsync();

        foreach (var intent in manifest.FirewallIntents)
        {
            var targets = ResolveIntentTargets(intent, assignments);
            if (targets.Count == 0)
            {
                logger.LogWarning($"Firewall intent for {manifest.Id} has no matching interfaces");
                continue;
            }

            foreach (var iface in targets)
            {
                var request = new FirewallManagedRuleRequest
                {
                    PackageId = manifest.Id,
                    ModuleId = intent.ModuleId,
                    Interface = iface,
                    Direction = intent.Direction,
                    Action = intent.Action,
                    AddressFamily = intent.AddressFamily,
                    Protocol = intent.Protocol,
                    SourceType = intent.SourceType,
                    SourceValue = intent.SourceValue,
                    SourcePort = intent.SourcePort,
                    DestinationType = intent.DestinationType,
                    DestinationValue = intent.DestinationValue,
                    DestinationPort = intent.DestinationPort,
                    Description = intent.Description,
                    Enabled = intent.Enabled
                };

                var result = await firewallManager.Rules.UpsertManagedRuleAsync(request);
                if (!result.Success)
                {
                    logger.LogWarning($"Failed to apply firewall intent for {manifest.Id}: {result.Error}");
                }
            }
        }
    }

    private static List<string> ResolveIntentTargets(FirewallIntentDefinition intent, List<InterfaceAssignmentEntity> assignments)
    {
        var targets = new List<string>();

        if (!string.IsNullOrWhiteSpace(intent.Interface))
        {
            targets.Add(intent.Interface.Trim());
            return targets;
        }

        if (!string.IsNullOrWhiteSpace(intent.InterfaceRole))
        {
            var role = ParseInterfaceRole(intent.InterfaceRole);
            targets.AddRange(assignments
                .Where(a => a.Role == role)
                .Select(a => a.InterfaceName));
        }

        return targets.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static InterfaceRole ParseInterfaceRole(string role)
    {
        if (Enum.TryParse<InterfaceRole>(role, true, out var parsed))
        {
            return parsed;
        }

        return role.ToLowerInvariant() switch
        {
            "lan" => InterfaceRole.Lan,
            "wan" => InterfaceRole.Wan,
            "opt" => InterfaceRole.Opt,
            _ => InterfaceRole.Opt
        };
    }

    /// <summary>
    /// Ensures all modules in a package are enabled by default (if not already set in database).
    /// </summary>
    private static async Task EnsureModulesEnabledAsync(
        PackageInfo packageInfo,
        PackageStateStore stateStore,
        Monolith.FireWall.Common.Interfaces.ILogger logger)
    {
        try
        {
            var modules = packageInfo.Definition.GetModules();
            foreach (var module in modules)
            {
                // Check if module state exists
                var existingState = await stateStore.GetModuleStateAsync(packageInfo.Definition.Id, module.Id);
                if (existingState == null)
                {
                    // No state exists, enable by default
                    await stateStore.SetModuleEnabledAsync(packageInfo.Definition.Id, module.Id, enabled: true);
                    logger.LogInformation($"Enabled module: {packageInfo.Definition.Id}/{module.Id}");
                }
                // If state exists, leave it as-is (user may have disabled it)
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning($"Failed to ensure modules enabled for package {packageInfo.Definition.Id}: {ex.Message}");
            // Don't fail package loading if module enabling fails
        }
    }

    /// <summary>
    /// Syncs database tables for all modules in a package by scanning for SQLite entity types.
    /// This is called during package loading at startup as a fallback if tables weren't synced during installation.
    /// </summary>
    private static async Task SyncPackageTablesAsync(
        PackageInfo packageInfo,
        CL.SQLite.SQLiteLibrary? sqlite,
        Monolith.FireWall.Common.Interfaces.ILogger logger)
    {
        logger.LogInformation($"[SYNC] METHOD CALLED for package: {packageInfo.Definition.Id}");
        try
        {
            logger.LogInformation($"[SYNC] Starting table sync for package: {packageInfo.Definition.Id}");
            
            if (sqlite?.TableSyncService == null)
            {
                logger.LogWarning($"[SYNC] SQLite or TableSyncService not available for package: {packageInfo.Definition.Id}");
                return;
            }

            // Get the main assembly
            var mainAssembly = packageInfo.MainAssembly;
            if (mainAssembly == null)
            {
                logger.LogWarning($"[SYNC] Main assembly not found for package: {packageInfo.Definition.Id}");
                return;
            }

            logger.LogInformation($"[SYNC] Scanning assembly: {mainAssembly.FullName}");

            // Find all types that have SQLiteTable attribute
            var entityTypes = mainAssembly.GetTypes()
                .Where(t => t.GetCustomAttribute<CL.SQLite.Models.SQLiteTableAttribute>() != null)
                .ToList();

            logger.LogInformation($"[SYNC] Found {entityTypes.Count} SQLite entity type(s) in package {packageInfo.Definition.Id}");

            if (entityTypes.Count == 0)
            {
                logger.LogInformation($"[SYNC] No SQLite entities found in package: {packageInfo.Definition.Id}");
                return;
            }

            logger.LogInformation($"[SYNC] Syncing database tables for package: {packageInfo.Definition.Id}");

            // Sync each entity type
            foreach (var entityType in entityTypes)
            {
                try
                {
                    var tableAttr = entityType.GetCustomAttribute<CL.SQLite.Models.SQLiteTableAttribute>();
                    var tableName = tableAttr?.TableName ?? entityType.Name;
                    
                    // Use reflection to call SyncTableAsync<T> with the entity type
                    var syncMethod = typeof(CL.SQLite.Services.TableSyncService).GetMethod("SyncTableAsync", new[] { typeof(CancellationToken) });
                    if (syncMethod != null)
                    {
                        var genericMethod = syncMethod.MakeGenericMethod(entityType);
                        var task = (Task)genericMethod.Invoke(sqlite.TableSyncService, new object[] { CancellationToken.None })!;
                        await task;
                        logger.LogInformation($"  ✓ Table {tableName} synced");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning($"  ✗ Failed to sync table for {entityType.Name}: {ex.Message}");
                }
            }

            logger.LogInformation($"✓ Database tables synced for package: {packageInfo.Definition.Id}");
        }
        catch (Exception ex)
        {
            logger.LogWarning($"Failed to sync database tables for package {packageInfo.Definition.Id}: {ex.Message}");
            // Don't fail package loading if table sync fails
        }
    }
}

// Package context for Program.cs package loading
internal class ProgramPackageContext : Monolith.FireWall.Common.Interfaces.IPackageContext
{
    private readonly Monolith.FireWall.Common.Interfaces.ILogger _logger;
    private readonly CodeLogic.Localization.ILocalizationManager _localization;

    public ProgramPackageContext(
        Monolith.FireWall.Common.Interfaces.ILogger logger,
        string packageId,
        CodeLogic.Localization.ILocalizationManager localization)
    {
        _logger = logger;
        PackageId = packageId;
        _localization = localization;
    }

    public string PackageId { get; }
    public Monolith.FireWall.Common.Interfaces.ILogger Logger => _logger;
    public CodeLogic.Localization.ILocalizationManager Localization => _localization;
}
