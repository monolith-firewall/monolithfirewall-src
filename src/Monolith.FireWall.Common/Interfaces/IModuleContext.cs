namespace Monolith.FireWall.Common.Interfaces;

/// <summary>
/// Context provided to modules.
/// </summary>
public interface IModuleContext
{
    /// <summary>Module ID</summary>
    string ModuleId { get; }

    /// <summary>Get configuration value</summary>
    Task<string?> GetConfigAsync(string key);

    /// <summary>Set configuration value</summary>
    Task SetConfigAsync(string key, string value);

    /// <summary>Get service from DI container</summary>
    T GetService<T>() where T : class;

    /// <summary>Logger instance</summary>
    ILogger Logger { get; }

    /// <summary>
    /// Network state provider for accessing operational network state.
    /// May be null if network state monitoring is not available.
    /// </summary>
    INetworkStateProvider? NetworkState { get; }
}
