using Monolith.FireWall.Common.Interfaces;
using Monolith.FireWall.Core.Models;
using Monolith.FireWall.Core.Services.Platform;
using Adapter = Monolith.FireWall.Core.Services.ModuleContextAdapter;

namespace Monolith.FireWall.Core.Services;

/// <summary>
/// Generates configuration files for modules that implement IModuleConfigGenerator.
/// </summary>
public sealed class ModuleConfigGenerator
{
    private readonly ILogger _logger;
    private readonly ModuleRegistry _moduleRegistry;
    private readonly Services.Platform.PlatformExecutor _platformExecutor;

    public ModuleConfigGenerator(
        ILogger logger,
        ModuleRegistry moduleRegistry,
        Services.Platform.PlatformExecutor platformExecutor)
    {
        _logger = logger;
        _moduleRegistry = moduleRegistry;
        _platformExecutor = platformExecutor;
    }

    /// <summary>
    /// Generate configurations for all modules that implement IModuleConfigGenerator.
    /// </summary>
    public async Task<ModuleConfigGenerationSummary> GenerateAllModuleConfigsAsync(CancellationToken cancellationToken = default)
    {
        var summary = new ModuleConfigGenerationSummary
        {
            StartedAt = DateTime.UtcNow
        };

        _logger.LogInformation("Generating module configurations...");

        var allModules = _moduleRegistry.GetAllModules();
        var configGenerators = new List<(ModuleInfo ModuleInfo, IModuleConfigGenerator Generator)>();

        // Find all modules that implement IModuleConfigGenerator
        foreach (var moduleInfo in allModules)
        {
            if (moduleInfo.Module is IModuleConfigGenerator generator)
            {
                configGenerators.Add((moduleInfo, generator));
            }
        }

        if (configGenerators.Count == 0)
        {
            _logger.LogInformation("No modules implement IModuleConfigGenerator");
            summary.Success = true;
            summary.CompletedAt = DateTime.UtcNow;
            summary.Duration = summary.CompletedAt - summary.StartedAt;
            return summary;
        }

        _logger.LogInformation($"Found {configGenerators.Count} module(s) with config generators");

        // Generate configs for each module
        foreach (var (moduleInfo, generator) in configGenerators)
        {
            try
            {
                _logger.LogInformation($"Generating config for module: {moduleInfo.Module.Id}");

                // Clean/backup old config files before generating new ones
                await CleanOldConfigFilesAsync(generator, moduleInfo.Module.Id, cancellationToken);

                // Create module context
                var defaultCapabilities = PlatformCapabilityMapper.FromSystemPermissions(moduleInfo.Module.GetSystemPermissions());
                var moduleContext = new Adapter(
                    _logger,
                    moduleInfo.Package.Definition.Id,
                    moduleInfo.Module.Id,
                    _platformExecutor,
                    defaultCapabilities);

                // Generate config
                var result = await generator.GenerateConfigAsync(moduleContext, cancellationToken);

                var moduleResult = new ModuleConfigResult
                {
                    ModuleId = moduleInfo.Module.Id,
                    PackageId = moduleInfo.Package.Definition.Id,
                    Success = result.Success,
                    Error = result.Error,
                    GeneratedFiles = result.GeneratedFiles,
                    RequiresRestart = result.RequiresRestart || generator.RequiresServiceRestart,
                    Metadata = result.Metadata
                };

                summary.ModuleResults.Add(moduleResult);

                if (result.Success)
                {
                    // Validate that generated files only contain Monolith-managed content
                    var validationResult = await ValidateGeneratedConfigsAsync(generator, result.GeneratedFiles, moduleInfo.Module.Id);
                    if (!validationResult.IsValid)
                    {
                        _logger.LogWarning($"⚠ Config validation warning for {moduleInfo.Module.Id}: {validationResult.Warning}");
                    }

                    _logger.LogInformation($"✓ Generated config for {moduleInfo.Module.Id} ({result.GeneratedFiles.Count} file(s))");
                    if (moduleResult.RequiresRestart)
                    {
                        summary.ModulesRequiringRestart.Add(moduleInfo.Module.Id);
                    }
                }
                else
                {
                    _logger.LogWarning($"⚠ Failed to generate config for {moduleInfo.Module.Id}: {result.Error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating config for module {moduleInfo.Module.Id}");
                summary.ModuleResults.Add(new ModuleConfigResult
                {
                    ModuleId = moduleInfo.Module.Id,
                    PackageId = moduleInfo.Package.Definition.Id,
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        summary.Success = summary.ModuleResults.All(r => r.Success);
        summary.CompletedAt = DateTime.UtcNow;
        summary.Duration = summary.CompletedAt - summary.StartedAt;

        _logger.LogInformation($"Module config generation completed: {summary.ModuleResults.Count(r => r.Success)}/{summary.ModuleResults.Count} successful");

        return summary;
    }

    /// <summary>
    /// Clean or backup old config files before generating new ones.
    /// Ensures fresh config generation like pfSense.
    /// </summary>
    private async Task CleanOldConfigFilesAsync(
        IModuleConfigGenerator generator,
        string moduleId,
        CancellationToken cancellationToken)
    {
        try
        {
            var configPaths = generator.GetConfigFilePaths().ToList();
            if (configPaths.Count == 0)
            {
                return;
            }

            var backupDir = "/var/lib/monolith-firewall/backups/module-configs";
            Directory.CreateDirectory(backupDir);

            foreach (var configPath in configPaths)
            {
                if (!File.Exists(configPath))
                {
                    continue;
                }

                try
                {
                    // Check if file is managed by Monolith (contains our marker)
                    var content = await File.ReadAllTextAsync(configPath, cancellationToken);
                    var isManaged = content.Contains("# Generated by Monolith FireWall", StringComparison.OrdinalIgnoreCase) ||
                                   content.Contains("# Monolith Managed", StringComparison.OrdinalIgnoreCase);

                    if (isManaged)
                    {
                        // Backup managed file
                        var fileName = Path.GetFileName(configPath);
                        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                        var backupPath = Path.Combine(backupDir, $"{moduleId}-{fileName}.bak-{timestamp}");
                        
                        File.Copy(configPath, backupPath, overwrite: true);
                        _logger.LogInformation($"Backed up managed config: {configPath} -> {backupPath}");
                        
                        // Delete the old file to ensure fresh generation
                        File.Delete(configPath);
                        _logger.LogInformation($"Deleted old config file: {configPath}");
                    }
                    else
                    {
                        // File exists but not managed by Monolith - warn and backup
                        var fileName = Path.GetFileName(configPath);
                        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                        var backupPath = Path.Combine(backupDir, $"{moduleId}-{fileName}.bak-{timestamp}");
                        
                        File.Copy(configPath, backupPath, overwrite: true);
                        _logger.LogWarning($"Found unmanaged config file, backing up: {configPath} -> {backupPath}");
                        _logger.LogWarning($"  File will be overwritten with Monolith-managed config");
                        
                        // Delete to ensure clean generation
                        File.Delete(configPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error cleaning config file {configPath}: {ex.Message}");
                    // Continue with other files even if one fails
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error during config file cleanup for module {moduleId}");
        }
    }

    /// <summary>
    /// Validate that generated config files only contain Monolith-managed content.
    /// </summary>
    private async Task<ConfigValidationResult> ValidateGeneratedConfigsAsync(
        IModuleConfigGenerator generator,
        List<string> generatedFiles,
        string moduleId)
    {
        var result = new ConfigValidationResult { IsValid = true };

        foreach (var filePath in generatedFiles)
        {
            if (!File.Exists(filePath))
            {
                continue;
            }

            try
            {
                var content = await File.ReadAllTextAsync(filePath);
                
                // Check if file contains Monolith marker
                var hasMonolithMarker = content.Contains("# Generated by Monolith FireWall", StringComparison.OrdinalIgnoreCase) ||
                                       content.Contains("# Monolith Managed", StringComparison.OrdinalIgnoreCase);

                if (!hasMonolithMarker)
                {
                    result.IsValid = false;
                    result.Warning = $"Generated file {filePath} does not contain Monolith marker - may contain unmanaged content";
                    _logger.LogWarning($"⚠ {result.Warning}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error validating config file {filePath}");
            }
        }

        return result;
    }

    private sealed class ConfigValidationResult
    {
        public bool IsValid { get; set; }
        public string? Warning { get; set; }
    }
}

/// <summary>
/// Summary of module config generation for all modules.
/// </summary>
public sealed class ModuleConfigGenerationSummary
{
    public bool Success { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? Duration { get; set; }
    public List<ModuleConfigResult> ModuleResults { get; set; } = new();
    public List<string> ModulesRequiringRestart { get; set; } = new();
}

/// <summary>
/// Result of config generation for a single module.
/// </summary>
public sealed class ModuleConfigResult
{
    public string ModuleId { get; set; } = string.Empty;
    public string PackageId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<string> GeneratedFiles { get; set; } = new();
    public bool RequiresRestart { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}
