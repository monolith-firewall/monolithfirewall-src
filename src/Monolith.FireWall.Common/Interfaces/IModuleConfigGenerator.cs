namespace Monolith.FireWall.Common.Interfaces;

/// <summary>
/// Interface for modules that can generate configuration files from database settings.
/// Modules implementing this interface will have their configs generated during system startup.
/// </summary>
public interface IModuleConfigGenerator
{
    /// <summary>
    /// Generate configuration files for this module from database settings.
    /// </summary>
    /// <param name="context">Module context providing access to logger, platform executor, etc.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing generated config files and whether service restart is needed</returns>
    Task<ModuleConfigGenerationResult> GenerateConfigAsync(IModuleContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Get list of config file paths that this module generates.
    /// Used for validation and cleanup.
    /// </summary>
    /// <returns>List of absolute file paths</returns>
    IEnumerable<string> GetConfigFilePaths();

    /// <summary>
    /// Whether this module requires a service restart after config generation.
    /// </summary>
    bool RequiresServiceRestart { get; }
}

/// <summary>
/// Result of module config generation.
/// </summary>
public sealed class ModuleConfigGenerationResult
{
    /// <summary>
    /// Whether config generation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if generation failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// List of config files that were generated.
    /// </summary>
    public List<string> GeneratedFiles { get; set; } = new();

    /// <summary>
    /// Whether a service restart is required.
    /// </summary>
    public bool RequiresRestart { get; set; }

    /// <summary>
    /// Additional metadata about the generation process.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}
