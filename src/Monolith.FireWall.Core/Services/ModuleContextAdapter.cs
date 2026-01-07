using Monolith.FireWall.Common.Interfaces;
using Monolith.FireWall.Platform;
using Monolith.FireWall.Platform.Models;

namespace Monolith.FireWall.Core.Services;

/// <summary>
/// Adapter to convert logger to IModuleContext
/// </summary>
public class ModuleContextAdapter : IModuleContext
{
    private readonly string _packageId;
    private readonly Services.Platform.PlatformExecutor? _platformExecutor;
    private readonly PlatformCapability _defaultCapabilities;

    public ModuleContextAdapter(
        ILogger logger,
        string packageId,
        string moduleId,
        Services.Platform.PlatformExecutor? platformExecutor = null,
        PlatformCapability defaultCapabilities = PlatformCapability.None)
    {
        _packageId = packageId;
        ModuleId = moduleId;
        Logger = logger;
        _platformExecutor = platformExecutor;
        _defaultCapabilities = defaultCapabilities;
    }

    public string ModuleId { get; }
    public ILogger Logger { get; }

    public Task<string?> GetConfigAsync(string key)
    {
        // For now, return null - will be implemented with CL.SQLite later
        return Task.FromResult<string?>(null);
    }

    public Task SetConfigAsync(string key, string value)
    {
        // For now, do nothing - will be implemented with CL.SQLite later
        return Task.CompletedTask;
    }

    public T GetService<T>() where T : class
    {
        // Service locator implementation
        if (typeof(T) == typeof(ILogger))
        {
            return (T)(object)Logger;
        }

        if (typeof(T) == typeof(Services.Platform.PlatformExecutor) && _platformExecutor != null)
        {
            return (T)(object)_platformExecutor;
        }

        if (typeof(T) == typeof(CL.SQLite.SQLiteLibrary))
        {
            var sqlite = CodeLogic.Libs.Get<CL.SQLite.SQLiteLibrary>();
            if (sqlite != null)
                return (T)(object)sqlite;
        }

        if (typeof(T) == typeof(IPlatformClient) && _platformExecutor != null)
        {
            var client = new PlatformClient(async (request, cancellationToken) =>
            {
                request.Context.PackageId ??= _packageId;
                request.Context.ModuleId ??= ModuleId;
                request.Context.CorrelationId ??= Guid.NewGuid().ToString("N");
                var allowedCapabilities = _defaultCapabilities;
                request.Context.Capabilities = request.Context.Capabilities == PlatformCapability.None
                    ? allowedCapabilities
                    : request.Context.Capabilities & allowedCapabilities;
                return await _platformExecutor.HandleAsync(request, cancellationToken);
            });
            return (T)(object)client;
        }

        throw new InvalidOperationException($"Service {typeof(T).Name} not found");
    }
}
