using CodeLogic;
using Monolith.FireWall.Core.Services;
using Monolith.FireWall.Core.Transport;
using CodeLogic.Logging;
using Adapter = Monolith.FireWall.Core.Services.ModuleContextAdapter;
using Monolith.FireWall.Core.Services.Platform;
using Monolith.FireWall.Common.Models;
using Monolith.FireWall.Core.Models;
using Monolith.FireWall.Core.Services.Firewall;

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
            Console.WriteLine($"\nPlease configure the system and restart.");
            Console.WriteLine("════════════════════════════════════════════════════\n");
            return;
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
        Console.WriteLine($"  ✓ Loaded configuration (Packages: {config.PackagesDirectory})");

        // Create CodeLogic logger
        var coreLogger = new Logger("CORE", "/var/lib/monolith-firewall/codelogic/Framework/logs", LogLevel.Info, new LoggingOptions
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
                await sqlite.TableSyncService.SyncTableAsync<FirewallRuleEntity>();
                await sqlite.TableSyncService.SyncTableAsync<FirewallDefaultsEntity>();
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
        var platformPolicy = new Services.Platform.PlatformPolicyStore(config.PlatformPolicyPath);
        var platformExecutor = new Services.Platform.PlatformExecutor(moduleRegistry, platformPolicy);
        var interfaceAssignmentStore = new InterfaceAssignmentStore();
        var commandRunner = new Services.Platform.PlatformCommandRunner();
        var networkInventory = new NetworkInventoryService(commandRunner);
        var interfaceConfigManager = new InterfaceConfigManager();
        var settingsManager = new SystemSettingsManager(new SystemSettingsStore());
        var firewallManager = new Services.Firewall.FirewallManager(commandRunner, interfaceAssignmentStore);
        var interfaceAssignmentManager = new InterfaceAssignmentManager(
            interfaceAssignmentStore,
            networkInventory,
            interfaceConfigManager,
            commandRunner,
            settingsManager);
        var routingManager = new RoutingManager(
            new RoutingStore(),
            commandRunner,
            networkInventory);
        var tuneablesManager = new SystemTuneablesManager(
            new SystemTuneablesStore(),
            commandRunner);
        var monitoringStore = new MonitoringStore();
        var monitoringManager = new MonitoringManager(
            monitoringStore,
            commandRunner,
            routingManager);
        var socketListener = new UnixSocketListener(
            logger,
            moduleRegistry,
            platformExecutor,
            packageStateStore,
            new PackageInstaller(logger, packageScanner, packageLoader, moduleRegistry, packageStateStore, config),
            interfaceAssignmentManager,
            routingManager,
            tuneablesManager,
            monitoringManager,
            settingsManager,
            firewallManager,
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
