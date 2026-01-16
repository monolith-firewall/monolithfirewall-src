using System.Text.Json;
using Monolith.FireWall.Common.Interfaces;
using Monolith.FireWall.Common.Models;

namespace Monolith.FireWall.Core.Services;

/// <summary>
/// Scans the packages directory for available packages
/// </summary>
public class PackageScanner
{
    private readonly ILogger _logger;

    public PackageScanner(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Scans the packages directory for packages
    /// </summary>
    public async Task<List<PackageDiscoveryInfo>> ScanPackagesAsync(string packagesDirectory)
    {
        var packages = new List<PackageDiscoveryInfo>();

        if (!Directory.Exists(packagesDirectory))
        {
            _logger.LogWarning($"Packages directory does not exist: {packagesDirectory}");
            return packages;
        }

        _logger.LogInformation($"Scanning for packages in: {packagesDirectory}");

        foreach (var packageDir in Directory.GetDirectories(packagesDirectory))
        {
            try
            {
                var discoveryInfo = await DiscoverPackageAsync(packageDir);
                if (discoveryInfo != null)
                {
                    packages.Add(discoveryInfo);
                    _logger.LogInformation($"Discovered package: {discoveryInfo.Manifest.Id} v{discoveryInfo.Manifest.Version}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error discovering package in {packageDir}");
            }
        }

        _logger.LogInformation($"Found {packages.Count} package(s)");
        return packages;
    }

    private async Task<PackageDiscoveryInfo?> DiscoverPackageAsync(string packageDir)
    {
        // Check for manifest.json
        var manifestPath = Path.Combine(packageDir, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            _logger.LogDebug($"No manifest.json found in {packageDir}");
            return null;
        }

        // Read and parse manifest
        var manifestJson = await File.ReadAllTextAsync(manifestPath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        var manifest = JsonSerializer.Deserialize<PackageManifest>(manifestJson, options);

        if (manifest == null)
        {
            _logger.LogWarning($"Failed to parse manifest.json in {packageDir}");
            return null;
        }

        // Check for backend directory
        var backendDir = Path.Combine(packageDir, "backend");
        if (!Directory.Exists(backendDir))
        {
            // Fallback: Check for build output (dev environment)
            var devBackendDir = Path.Combine(packageDir, "bin", "Release", "net10.0");
            if (Directory.Exists(devBackendDir))
            {
                _logger.LogDebug($"Using dev build directory for package {packageDir}: {devBackendDir}");
                backendDir = devBackendDir;
            }
            else
            {
                _logger.LogWarning($"No backend directory found in {packageDir}");
                return null;
            }
        }

        // Find main DLL
        var mainDll = FindMainDll(backendDir, manifest.Id);
        if (mainDll == null)
        {
            _logger.LogWarning($"Main DLL not found for package {manifest.Id}");
            return null;
        }

        // Find Views DLL (optional - only for RCL packages)
        var viewsDll = FindViewsDll(backendDir, manifest.Id);

        return new PackageDiscoveryInfo
        {
            Directory = packageDir,
            Manifest = manifest,
            MainDllPath = mainDll,
            ViewsDllPath = viewsDll
        };
    }

    private string? FindMainDll(string backendDir, string packageId)
    {
        // Convert package ID to DLL name
        // "monolith-network" -> "Monolith.Network.dll"
        var dllName = ConvertPackageIdToDllName(packageId);
        var dllPath = Path.Combine(backendDir, dllName);

        if (File.Exists(dllPath))
            return dllPath;

        // Fallback: search for any DLL matching the pattern
        var dllFiles = Directory.GetFiles(backendDir, "*.dll", SearchOption.TopDirectoryOnly);
        var matchingDll = dllFiles.FirstOrDefault(f =>
        {
            var fileName = Path.GetFileNameWithoutExtension(f);
            return fileName.Equals(packageId.Replace("-", "."), StringComparison.OrdinalIgnoreCase) ||
                   fileName.Equals(dllName.Replace(".dll", ""), StringComparison.OrdinalIgnoreCase);
        });

        return matchingDll;
    }

    private string? FindViewsDll(string backendDir, string packageId)
    {
        // Views DLL: "Monolith.Network.Views.dll"
        var viewsDllName = ConvertPackageIdToDllName(packageId).Replace(".dll", ".Views.dll");
        var viewsDllPath = Path.Combine(backendDir, viewsDllName);

        if (File.Exists(viewsDllPath))
            return viewsDllPath;

        return null; // Views DLL is optional
    }

    private string ConvertPackageIdToDllName(string packageId)
    {
        // "monolith-network" -> "Monolith.Network.dll"
        var parts = packageId.Split('-');
        var dllName = string.Join(".", parts.Select(p => char.ToUpper(p[0]) + p.Substring(1)));
        return $"{dllName}.dll";
    }
}

/// <summary>
/// Information about a discovered package
/// </summary>
public class PackageDiscoveryInfo
{
    public string Directory { get; set; } = "";
    public PackageManifest Manifest { get; set; } = null!;
    public string MainDllPath { get; set; } = "";
    public string? ViewsDllPath { get; set; }
    public bool HasRazorViews => ViewsDllPath != null;
}
