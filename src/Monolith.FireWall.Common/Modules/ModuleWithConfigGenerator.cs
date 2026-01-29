using Monolith.FireWall.Common.Interfaces;

namespace Monolith.FireWall.Common.Modules;

/// <summary>
/// Base class for modules that generate configuration files.
/// </summary>
public abstract class ModuleWithConfigGenerator : MonolithModuleBase, IModuleConfigGenerator
{
    /// <summary>
    /// Whether the managed service requires restart after config changes.
    /// </summary>
    public abstract bool RequiresServiceRestart { get; }

    /// <summary>
    /// Get the paths of configuration files this module generates.
    /// </summary>
    public abstract IEnumerable<string> GetConfigFilePaths();

    /// <summary>
    /// Generate configuration files from database settings.
    /// </summary>
    public abstract Task<ModuleConfigGenerationResult> GenerateConfigAsync(
        IModuleContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Helper to write a config file with proper permissions.
    /// </summary>
    protected async Task WriteConfigFileAsync(string path, string content, CancellationToken cancellationToken)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await File.WriteAllTextAsync(path, content, cancellationToken);

        // Set permissions
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/bin/chmod",
                Arguments = $"644 {path}",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (process != null)
            {
                await process.WaitForExitAsync(cancellationToken);
            }
        }
        catch
        {
            // Best effort
        }
    }

    /// <summary>
    /// Helper to create a successful config generation result.
    /// </summary>
    protected static ModuleConfigGenerationResult SuccessResult(params string[] generatedFiles)
    {
        return new ModuleConfigGenerationResult
        {
            Success = true,
            GeneratedFiles = generatedFiles.ToList()
        };
    }

    /// <summary>
    /// Helper to create a failed config generation result.
    /// </summary>
    protected static ModuleConfigGenerationResult FailureResult(string error)
    {
        return new ModuleConfigGenerationResult
        {
            Success = false,
            Error = error
        };
    }
}
