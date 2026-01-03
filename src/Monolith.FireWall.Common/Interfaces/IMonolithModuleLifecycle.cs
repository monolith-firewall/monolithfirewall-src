namespace Monolith.FireWall.Common.Interfaces;

/// <summary>
/// Module lifecycle hooks.
/// </summary>
public interface IMonolithModuleLifecycle
{
    /// <summary>Called when module starts</summary>
    Task OnStartAsync(IModuleContext context);

    /// <summary>Called when module stops</summary>
    Task OnStopAsync(IModuleContext context);

    /// <summary>Called when module config changes</summary>
    Task OnConfigChangedAsync(string key, string? oldValue, string? newValue);
}
