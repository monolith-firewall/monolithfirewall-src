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
            var manifest = JsonSerializer.Deserialize<PackageManifest>(manifestJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            });

        if (manifest == null)
        {
            _logger.LogWarning($"Failed to parse manifest.json in {packageDir}");
            return null;
        }

        // Check for backend directory
        var backendDir = Path.Combine(packageDir, "backend");
        if (!Directory.Exists(backendDir))
        {
            _logger.LogWarning($"No backend directory found in {packageDir}");
            return null;
        }

        // Find main DLL
        var mainDll = FindMainDll(backendDir, manifest.Id);
        if (mainDll == null)
        {
            _logger.LogWarning($"Main DLL not found for package {manifest.Id}");
            return null;
        }

        // Views are embedded in main DLL when using Microsoft.NET.Sdk.Razor
        return new PackageDiscoveryInfo
        {
            Directory = packageDir,
            Manifest = manifest,
            MainDllPath = mainDll
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

        _logger.LogWarning($"Main DLL not found at expected path: {dllPath}");
        return null;
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
    // Views are embedded in main DLL when using Microsoft.NET.Sdk.Razor
    public bool HasRazorViews => !string.IsNullOrEmpty(MainDllPath);
}
