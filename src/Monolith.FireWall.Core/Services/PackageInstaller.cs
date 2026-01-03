using System.IO.Compression;
using System.Text.Json;
using Monolith.FireWall.Common.Interfaces;
using Monolith.FireWall.Common.Models;
using Monolith.FireWall.Common.Services;
using Monolith.FireWall.Core.Configuration;
using Monolith.FireWall.Core.Models;

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

    public PackageInstaller(
        ILogger logger,
        PackageScanner scanner,
        PackageLoader loader,
        ModuleRegistry registry,
        PackageStateStore stateStore,
        CoreConfiguration config)
    {
        _logger = logger;
        _scanner = scanner;
        _loader = loader;
        _registry = registry;
        _stateStore = stateStore;
        _config = config;
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
            var isUpdate = Directory.Exists(targetDir);
            if (isUpdate)
            {
                if (!overwrite)
                {
                    return PackageInstallResult.Fail("Package already installed");
                }

                Directory.Delete(targetDir, recursive: true);
            }

            Directory.CreateDirectory(targetDir);
            CopyDirectory(stagingDir, targetDir);

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

            await TryReloadPackageAsync(manifest.Id);
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

    private async Task TryReloadPackageAsync(string packageId)
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
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to reload package {packageId}");
        }
    }

    private static async Task<PackageManifest?> ReadManifestAsync(string manifestPath, CancellationToken cancellationToken)
    {
        try
        {
            var json = await File.ReadAllTextAsync(manifestPath, cancellationToken);
            return JsonSerializer.Deserialize<PackageManifest>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
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
