using System;
using System.IO.Compression;
using System.Text.Json;
using Monolith.FireWall.Common.Interfaces;
using Monolith.FireWall.Common.Models;
using Monolith.FireWall.Common.Services;
using Monolith.FireWall.Core.Configuration;
using Monolith.FireWall.Core.Models;
using Monolith.FireWall.Core.Services.Platform;
using Monolith.FireWall.Platform.Models;
using CodeLogic;
using CL.SQLite.Services;
using System.Reflection;

namespace Monolith.FireWall.Core.Services;

public sealed class PackageInstaller
{
    private readonly ILogger _logger;
    private readonly PackageScanner _scanner;
    private readonly PackageLoader _loader;
    private readonly ModuleRegistry _registry;
    private readonly PackageStateStore _stateStore;
    private readonly LoggingManager _loggingManager;
    private readonly CoreConfiguration _config;
    private readonly PlatformCommandRunner _commandRunner;

    private static readonly string[] RestartUnits =
    {
        "monolith-firewall-core.service",
        "monolith-firewall-webui.service"
    };

    public PackageInstaller(
        ILogger logger,
        PackageScanner scanner,
        PackageLoader loader,
        ModuleRegistry registry,
        PackageStateStore stateStore,
        PlatformCommandRunner commandRunner,
        CoreConfiguration config)
    {
        _logger = logger;
        _scanner = scanner;
        _loader = loader;
        _registry = registry;
        _stateStore = stateStore;
        _config = config;
        _commandRunner = commandRunner;
        _loggingManager = LoggingManager.Instance;
    }

    public async Task<PackageInstallResult> InstallAsync(string packagePath, bool overwrite, string? expectedPackageId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            return PackageInstallResult.Fail("Package path is required");
        }

        if (!File.Exists(packagePath))
        {
            return PackageInstallResult.Fail($"Package not found: {packagePath}");
        }

        if (!packagePath.EndsWith(".mfwpkg", StringComparison.OrdinalIgnoreCase))
        {
            return PackageInstallResult.Fail("Unsupported package format");
        }

        var stagingRoot = "/var/lib/monolith-firewall/packages-staging";
        Directory.CreateDirectory(stagingRoot);
        var stagingDir = Path.Combine(stagingRoot, $"pkg-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingDir);

        try
        {
            ZipFile.ExtractToDirectory(packagePath, stagingDir, overwriteFiles: true);
            var manifestPath = Path.Combine(stagingDir, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                return PackageInstallResult.Fail("manifest.json missing in package");
            }

            var manifest = await ReadManifestAsync(manifestPath, cancellationToken);
            if (manifest == null)
            {
                return PackageInstallResult.Fail("Failed to parse manifest.json");
            }

            if (!string.IsNullOrWhiteSpace(expectedPackageId) &&
                !string.Equals(expectedPackageId, manifest.Id, StringComparison.OrdinalIgnoreCase))
            {
                return PackageInstallResult.Fail("Package ID does not match manifest");
            }

            var targetDir = Path.Combine(_config.PackagesDirectory, manifest.Id);
            var existingState = await _stateStore.GetPackageAsync(manifest.Id);
            var isUpdate = Directory.Exists(targetDir) && existingState != null;

            // If the directory exists but we have no state, treat it as stale and allow reinstall.
            if (Directory.Exists(targetDir) && existingState != null && !overwrite)
            {
                return PackageInstallResult.Fail("Package already installed");
            }

            // For stale dirs or updates with overwrite, clear the target directory to ensure a clean install.
            if (Directory.Exists(targetDir))
            {
                Directory.Delete(targetDir, recursive: true);
            }

            Directory.CreateDirectory(targetDir);
            CopyDirectory(stagingDir, targetDir);

            // Install bundled deb packages BEFORE setting package state
            var debInstallResult = await InstallBundledDebsAsync(targetDir, manifest, cancellationToken);
            if (!debInstallResult.Success)
            {
                // Clean up on failure
                try
                {
                    if (Directory.Exists(targetDir))
                    {
                        Directory.Delete(targetDir, recursive: true);
                    }
                }
                catch
                {
                    // Best effort cleanup
                }
                return debInstallResult;
            }

            await _stateStore.SetPackageInstalledAsync(manifest.Id, manifest.Version, "local", log: false);
            await _loggingManager.LogMonolithAsync(
                "Package",
                "info",
                "PackageInstaller",
                isUpdate ? $"Updated package {manifest.Id}" : $"Installed package {manifest.Id}",
                null,
                null,
                new Dictionary<string, object>
                {
                    ["packageId"] = manifest.Id,
                    ["version"] = manifest.Version,
                    ["update"] = isUpdate
                });

            var packageInfo = await TryReloadPackageAsync(manifest.Id, cancellationToken);
            return PackageInstallResult.Ok(manifest, manifest.RequiresRestart, isUpdate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Package install failed");
            await _loggingManager.LogMonolithAsync(
                "Package",
                "error",
                "PackageInstaller",
                $"Failed to install package from {packagePath}",
                null,
                null,
                new Dictionary<string, object>
                {
                    ["packagePath"] = packagePath,
                    ["error"] = ex.Message
                });
            return PackageInstallResult.Fail(ex.Message);
        }
        finally
        {
            try
            {
                if (Directory.Exists(stagingDir))
                {
                    Directory.Delete(stagingDir, recursive: true);
                }
            }
            catch
            {
                // Best effort cleanup.
            }
        }
    }

    public void ScheduleRestartIfNeeded(bool restartServices, bool requiresRestart, string packageId)
    {
        if (!restartServices || !requiresRestart)
        {
            return;
        }

        // Return success to caller first, then restart in background.
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(1500));

                if (!_commandRunner.CommandExists("systemctl") && !_commandRunner.CommandExists("/bin/systemctl") && !_commandRunner.CommandExists("/usr/bin/systemctl"))
                {
                    await _loggingManager.LogMonolithAsync(
                        "Package",
                        "warning",
                        "PackageInstaller",
                        "Restart requested but systemctl not found",
                        null,
                        null,
                        new Dictionary<string, object>
                        {
                            ["packageId"] = packageId
                        });
                    return;
                }

                var cmd = new PlatformCommand
                {
                    FileName = "systemctl",
                    Arguments = $"restart {string.Join(" ", RestartUnits)}",
                    TimeoutMs = 60_000,
                    UseSudo = false
                };

                var result = await _commandRunner.RunAsync(cmd, CancellationToken.None);
                if (result.ExitCode != 0)
                {
                    await _loggingManager.LogMonolithAsync(
                        "Package",
                        "error",
                        "PackageInstaller",
                        "Service restart failed after package install",
                        null,
                        null,
                        new Dictionary<string, object>
                        {
                            ["packageId"] = packageId,
                            ["exitCode"] = result.ExitCode,
                            ["stderr"] = result.StdErr ?? string.Empty
                        });
                }
                else
                {
                    await _loggingManager.LogMonolithAsync(
                        "Package",
                        "info",
                        "PackageInstaller",
                        "Services restarted after package install",
                        null,
                        null,
                        new Dictionary<string, object>
                        {
                            ["packageId"] = packageId,
                            ["units"] = string.Join(",", RestartUnits)
                        });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restart services after package install");
            }
        });
    }

    public async Task<PackageInstallResult> RemoveAsync(string packageId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            return PackageInstallResult.Fail("Package ID is required");
        }

        var targetDir = Path.Combine(_config.PackagesDirectory, packageId);
        if (!Directory.Exists(targetDir))
        {
            return PackageInstallResult.Fail("Package not installed");
        }

        try
        {
            Directory.Delete(targetDir, recursive: true);
            _registry.UnregisterPackage(packageId);
            await _stateStore.RemovePackageAsync(packageId, log: false);
            await _stateStore.ClearModuleStatesAsync(packageId);
            await _loggingManager.LogMonolithAsync(
                "Package",
                "warning",
                "PackageInstaller",
                $"Removed package {packageId}",
                null,
                null,
                new Dictionary<string, object>
                {
                    ["packageId"] = packageId
                });

            return PackageInstallResult.Ok(null, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Package removal failed");
            await _loggingManager.LogMonolithAsync(
                "Package",
                "error",
                "PackageInstaller",
                $"Failed to remove package {packageId}",
                null,
                null,
                new Dictionary<string, object>
                {
                    ["packageId"] = packageId,
                    ["error"] = ex.Message
                });
            return PackageInstallResult.Fail(ex.Message);
        }
    }

    private async Task<PackageInfo?> TryReloadPackageAsync(string packageId, CancellationToken cancellationToken)
    {
        try
        {
            _registry.UnregisterPackage(packageId);
            var discovered = await _scanner.ScanPackagesAsync(_config.PackagesDirectory);
            var target = discovered.FirstOrDefault(p => p.Manifest.Id == packageId);
            if (target != null)
            {
                var packageInfo = await _loader.LoadPackageAsync(target);
                _registry.RegisterPackage(packageInfo);
                
                // Sync database tables for all modules in this package
                await SyncPackageModuleTablesAsync(packageInfo, cancellationToken);

                // Enable all modules in the package by default (if not already set)
                await EnablePackageModulesAsync(packageInfo, cancellationToken);

                // Start lifecycle modules so they receive a context immediately after install/update.
                // This is important for packages that trigger config generation on writes.
                await StartPackageLifecycleAsync(packageInfo, cancellationToken);
                
                return packageInfo;
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to reload package {packageId}");
            return null;
        }
    }

    private async Task StartPackageLifecycleAsync(PackageInfo packageInfo, CancellationToken cancellationToken)
    {
        var modules = _registry
            .GetAllModules(includeDisabled: true)
            .Where(m => string.Equals(m.Package.Definition.Id, packageInfo.Definition.Id, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var moduleInfo in modules)
        {
            if (moduleInfo.Module is not IMonolithModuleLifecycle lifecycle)
            {
                continue;
            }

            try
            {
                // Minimal context: modules can still access SQLite via CodeLogic.Libs.
                var context = new ModuleContextAdapter(_logger, packageInfo.Definition.Id, moduleInfo.Module.Id);
                await lifecycle.OnStartAsync(context);
                _logger.LogInformation($"Started module lifecycle: {packageInfo.Definition.Id}/{moduleInfo.Module.Id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to start module lifecycle: {packageInfo.Definition.Id}/{moduleInfo.Module.Id}");
            }
        }
    }

    private static async Task<PackageManifest?> ReadManifestAsync(string manifestPath, CancellationToken cancellationToken)
    {
        try
        {
            var json = await File.ReadAllTextAsync(manifestPath, cancellationToken);
            return JsonSerializer.Deserialize<PackageManifest>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            });
        }
        catch (JsonException ex)
        {
            // Log the error for debugging
            System.Diagnostics.Debug.WriteLine($"Failed to parse manifest.json: {ex.Message}");
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        foreach (var directory in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, directory);
            Directory.CreateDirectory(Path.Combine(destinationDir, relative));
        }

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, file);
            var target = Path.Combine(destinationDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target) ?? destinationDir);
            File.Copy(file, target, overwrite: true);
        }
    }

    /// <summary>
    /// Enables all modules in a package by default (if not already set in database).
    /// </summary>
    private async Task EnablePackageModulesAsync(PackageInfo packageInfo, CancellationToken cancellationToken)
    {
        try
        {
            var modules = packageInfo.Definition.GetModules();
            foreach (var module in modules)
            {
                // Check if module state exists
                var existingState = await _stateStore.GetModuleStateAsync(packageInfo.Definition.Id, module.Id);
                if (existingState == null)
                {
                    // No state exists, enable by default
                    await _stateStore.SetModuleEnabledAsync(packageInfo.Definition.Id, module.Id, enabled: true);
                    _logger.LogInformation($"Enabled module: {packageInfo.Definition.Id}/{module.Id}");
                }
                // If state exists, leave it as-is (user may have disabled it)
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to enable modules for package {packageInfo.Definition.Id}: {ex.Message}");
            // Don't fail installation if module enabling fails
        }
    }

    /// <summary>
    /// Syncs database tables for all modules in a package by scanning for SQLite entity types.
    /// </summary>
    private async Task SyncPackageModuleTablesAsync(PackageInfo packageInfo, CancellationToken cancellationToken)
    {
        try
        {
            var sqlite = CodeLogic.Libs.Get<CL.SQLite.SQLiteLibrary>();
            if (sqlite?.TableSyncService == null)
            {
                _logger.LogWarning($"SQLite library or TableSyncService not available - cannot sync tables for package {packageInfo.Definition.Id}");
                return;
            }

            _logger.LogInformation($"Syncing database tables for package: {packageInfo.Definition.Id}");

            // Get the main assembly
            var mainAssembly = packageInfo.MainAssembly;
            if (mainAssembly == null)
            {
                _logger.LogWarning($"Main assembly not found for package {packageInfo.Definition.Id}");
                return;
            }

            // Find all types that have SQLiteTable attribute
            var entityTypes = mainAssembly.GetTypes()
                .Where(t => t.GetCustomAttribute<CL.SQLite.Models.SQLiteTableAttribute>() != null)
                .ToList();

            if (entityTypes.Count == 0)
            {
                _logger.LogInformation($"  No SQLite entities found in package {packageInfo.Definition.Id}");
                return;
            }

            _logger.LogInformation($"  Found {entityTypes.Count} SQLite entity type(s)");

            // Sync each entity type
            foreach (var entityType in entityTypes)
            {
                try
                {
                    var tableAttr = entityType.GetCustomAttribute<CL.SQLite.Models.SQLiteTableAttribute>();
                    var tableName = tableAttr?.TableName ?? entityType.Name;
                    
                    _logger.LogInformation($"  Syncing table: {tableName} ({entityType.Name})");
                    
                    // Use reflection to call SyncTableAsync<T> with the entity type
                    var syncMethod = typeof(TableSyncService).GetMethod("SyncTableAsync", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                        null,
                        new[] { typeof(CancellationToken) },
                        null);
                    
                    if (syncMethod != null)
                    {
                        var genericMethod = syncMethod.MakeGenericMethod(entityType);
                        var taskObject = genericMethod.Invoke(sqlite.TableSyncService, new object[] { cancellationToken });
                        
                        if (taskObject is Task task)
                        {
                            await task.ConfigureAwait(false);
                            _logger.LogInformation($"    ✓ Table {tableName} synced successfully");
                        }
                        else
                        {
                            _logger.LogWarning($"    ✗ SyncTableAsync did not return a Task");
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"    ✗ SyncTableAsync method not found on TableSyncService");
                    }
                }
                catch (TargetInvocationException tie) when (tie.InnerException != null)
                {
                    _logger.LogError(tie.InnerException, $"    ✗ Failed to sync table for {entityType.Name}: {tie.InnerException.Message}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"    ✗ Failed to sync table for {entityType.Name}: {ex.Message}");
                }
            }

            _logger.LogInformation($"✓ Database tables synced for package: {packageInfo.Definition.Id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to sync database tables for package {packageInfo.Definition.Id}: {ex.Message}");
            // Don't fail the installation if table sync fails - it can be done later
        }
    }

    /// <summary>
    /// Installs bundled .deb packages from the package's debs/ directory.
    /// </summary>
    private async Task WaitForDpkgLockAsync(CancellationToken cancellationToken)
    {
        const int maxWaitSeconds = 300; // 5 minutes max wait
        const int checkIntervalMs = 1000; // Check every second
        var lockPath = "/var/lib/dpkg/lock-frontend";
        var lockFile = "/var/lib/dpkg/lock";
        var startTime = DateTime.UtcNow;

        while ((DateTime.UtcNow - startTime).TotalSeconds < maxWaitSeconds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Check if lock files exist and are locked
            var hasLock = false;
            
            // Check for lock-frontend
            if (File.Exists(lockPath))
            {
                try
                {
                    // Try to check if a process is holding the lock using lsof (more reliable than fuser)
                    var checkCmd = new PlatformCommand
                    {
                        FileName = "lsof",
                        Arguments = $"{lockPath} 2>/dev/null || true",
                        TimeoutMs = 5000,
                        UseSudo = false
                    };
                    var lsofResult = await _commandRunner.RunAsync(checkCmd, cancellationToken);
                    if (lsofResult.ExitCode == 0 && !string.IsNullOrWhiteSpace(lsofResult.StdOut))
                    {
                        hasLock = true;
                    }
                }
                catch
                {
                    // lsof might not be available, check if file is locked by trying to open it exclusively
                    try
                    {
                        using var fs = File.Open(lockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                        fs.Close();
                    }
                    catch (IOException)
                    {
                        hasLock = true;
                    }
                }
            }

            // Check for main lock file
            if (!hasLock && File.Exists(lockFile))
            {
                try
                {
                    using var fs = File.Open(lockFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                }
                catch (IOException)
                {
                    hasLock = true;
                }
            }

            // Check for running dpkg/apt processes
            if (!hasLock)
            {
                var psCmd = new PlatformCommand
                {
                    FileName = "pgrep",
                    Arguments = "-f '(dpkg|apt-get|apt)'",
                    TimeoutMs = 5000,
                    UseSudo = false
                };
                var psResult = await _commandRunner.RunAsync(psCmd, cancellationToken);
                if (psResult.ExitCode == 0 && !string.IsNullOrWhiteSpace(psResult.StdOut))
                {
                    hasLock = true;
                }
            }

            if (!hasLock)
            {
                // No locks found, we can proceed
                return;
            }

            // Wait a bit and check again
            _logger.LogInformation("Waiting for dpkg lock to be released...");
            await Task.Delay(checkIntervalMs, cancellationToken);
        }

        // If we get here, we've waited too long
        _logger.LogWarning("Timeout waiting for dpkg lock after 5 minutes. Proceeding anyway...");
    }

    private async Task<PackageInstallResult> InstallBundledDebsAsync(
        string packageDir,
        PackageManifest manifest,
        CancellationToken cancellationToken)
    {
        if (manifest.BundledDebs == null || manifest.BundledDebs.Length == 0)
        {
            _logger.LogInformation($"No bundled deb packages to install for {manifest.Id}");
            return PackageInstallResult.Ok(manifest, false, false);
        }

        var debsDir = Path.Combine(packageDir, "debs");
        if (!Directory.Exists(debsDir))
        {
            _logger.LogWarning($"Bundled debs directory not found at {debsDir}, skipping deb installation");
            return PackageInstallResult.Ok(manifest, false, false);
        }

        var debFiles = new List<string>();
        foreach (var bundledDeb in manifest.BundledDebs)
        {
            var debPath = Path.Combine(debsDir, bundledDeb.FileName);
            if (!File.Exists(debPath))
            {
                return PackageInstallResult.Fail($"Bundled deb file not found: {bundledDeb.FileName} (expected at {debPath})");
            }
            debFiles.Add(debPath);
        }

        if (debFiles.Count == 0)
        {
            _logger.LogInformation($"No deb files found in {debsDir}");
            return PackageInstallResult.Ok(manifest, false, false);
        }

        try
        {
            // Wait for any existing dpkg processes to complete before installing
            await WaitForDpkgLockAsync(cancellationToken);
            
            _logger.LogInformation($"Installing {debFiles.Count} bundled deb package(s) for {manifest.Id}...");

            // Install deb packages using dpkg
            // Quote each path to handle spaces
            var debList = string.Join(" ", debFiles.Select(f => $"\"{f}\""));
            
            // Set environment variables for non-interactive installation
            var envVars = new Dictionary<string, string>
            {
                ["DEBIAN_FRONTEND"] = "noninteractive",
                ["APT_LISTBUGS_FRONTEND"] = "none",
                ["APT_LISTCHANGES_FRONTEND"] = "none"
            };
            
            var cmd = new PlatformCommand
            {
                FileName = "dpkg",
                Arguments = $"--force-confdef --force-confold -i {debList}",
                TimeoutMs = 300_000, // 5 minutes
                UseSudo = true,
                EnvironmentVariables = envVars
            };

            var result = await _commandRunner.RunAsync(cmd, cancellationToken);

            if (result.ExitCode != 0)
            {
                // Try to fix broken dependencies
                _logger.LogInformation("Attempting to fix broken dependencies with apt-get install -f...");
                var fixEnvVars = new Dictionary<string, string>
                {
                    ["DEBIAN_FRONTEND"] = "noninteractive",
                    ["APT_LISTBUGS_FRONTEND"] = "none",
                    ["APT_LISTCHANGES_FRONTEND"] = "none"
                };
                
                var fixCmd = new PlatformCommand
                {
                    FileName = "apt-get",
                    Arguments = "install -f -y",
                    TimeoutMs = 300_000,
                    UseSudo = true,
                    EnvironmentVariables = fixEnvVars
                };
                
                var fixResult = await _commandRunner.RunAsync(fixCmd, cancellationToken);
                if (fixResult.ExitCode != 0)
                {
                    var errorMsg = result.StdErr ?? fixResult.StdErr ?? "Unknown error";
                    return PackageInstallResult.Fail(
                        $"Failed to install bundled deb packages. dpkg exit: {result.ExitCode}, " +
                        $"apt-get fix exit: {fixResult.ExitCode}. Error: {errorMsg}");
                }
            }

            _logger.LogInformation($"Successfully installed {debFiles.Count} bundled deb package(s)");
            await _loggingManager.LogMonolithAsync(
                "Package",
                "info",
                "PackageInstaller",
                $"Installed {debFiles.Count} bundled deb packages for {manifest.Id}",
                null,
                null,
                new Dictionary<string, object>
                {
                    ["packageId"] = manifest.Id,
                    ["debCount"] = debFiles.Count,
                    ["debPackages"] = string.Join(", ", manifest.BundledDebs.Select(b => b.PackageName))
                });

            return PackageInstallResult.Ok(manifest, false, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to install bundled deb packages for {manifest.Id}");
            return PackageInstallResult.Fail($"Failed to install bundled deb packages: {ex.Message}");
        }
    }
}

public sealed class PackageInstallResult
{
    public bool Success { get; set; }
    public PackageManifest? Manifest { get; set; }
    public bool RequiresRestart { get; set; }
    public bool IsUpdate { get; set; }
    public string? Error { get; set; }

    public static PackageInstallResult Ok(PackageManifest? manifest, bool requiresRestart, bool isUpdate = false)
    {
        return new PackageInstallResult
        {
            Success = true,
            Manifest = manifest,
            RequiresRestart = requiresRestart,
            IsUpdate = isUpdate
        };
    }

    public static PackageInstallResult Fail(string error)
    {
        return new PackageInstallResult
        {
            Success = false,
            Error = error
        };
    }
}
