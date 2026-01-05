using Monolith.FireWall.Common.Interfaces;
using Monolith.FireWall.Common.Models;
using Monolith.FireWall.Core.Models;
using Monolith.FireWall.Core.Services.Platform;

namespace Monolith.FireWall.Core.Services;

/// <summary>
/// Manages systemd services for modules.
/// Handles starting, restarting, and managing service dependencies.
/// </summary>
public sealed class ModuleServiceManager
{
    private readonly ILogger _logger;
    private readonly ModuleRegistry _moduleRegistry;
    private readonly PlatformCommandRunner _commandRunner;

    public ModuleServiceManager(
        ILogger logger,
        ModuleRegistry moduleRegistry,
        PlatformCommandRunner commandRunner)
    {
        _logger = logger;
        _moduleRegistry = moduleRegistry;
        _commandRunner = commandRunner;
    }

    /// <summary>
    /// Start or restart services for modules that require it.
    /// </summary>
    /// <param name="modulesRequiringRestart">List of module IDs that require service restart</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing which services were started/restarted</returns>
    public async Task<ServiceManagementResult> ManageModuleServicesAsync(
        IEnumerable<string> modulesRequiringRestart,
        CancellationToken cancellationToken = default)
    {
        var result = new ServiceManagementResult
        {
            StartedAt = DateTime.UtcNow
        };

        try
        {
            // Collect all services from modules
            var moduleServices = CollectModuleServices();
            if (moduleServices.Count == 0)
            {
                _logger.LogInformation("No module services found");
                result.Success = true;
                result.CompletedAt = DateTime.UtcNow;
                result.Duration = result.CompletedAt - result.StartedAt;
                return result;
            }

            _logger.LogInformation($"Found {moduleServices.Count} service(s) from {moduleServices.Select(s => s.ModuleId).Distinct().Count()} module(s)");

            // Determine which services need to be started/restarted
            var servicesToManage = DetermineServicesToManage(moduleServices, modulesRequiringRestart);

            if (servicesToManage.Count == 0)
            {
                _logger.LogInformation("No services need to be started or restarted");
                result.Success = true;
                result.CompletedAt = DateTime.UtcNow;
                result.Duration = result.CompletedAt - result.StartedAt;
                return result;
            }

            // Sort services by dependencies (simple topological sort)
            var sortedServices = SortServicesByDependencies(servicesToManage, moduleServices);

            // Start/restart services in order
            foreach (var serviceInfo in sortedServices)
            {
                try
                {
                    var serviceResult = await ManageServiceAsync(serviceInfo, cancellationToken);
                    result.ServiceResults.Add(serviceResult);

                    if (serviceResult.Success)
                    {
                        if (serviceResult.Action == "started")
                        {
                            result.ServicesStarted.Add(serviceInfo.SystemdUnit);
                        }
                        else if (serviceResult.Action == "restarted")
                        {
                            result.ServicesRestarted.Add(serviceInfo.SystemdUnit);
                        }
                    }
                    else
                    {
                        result.ServicesFailed.Add(serviceInfo.SystemdUnit);
                        _logger.LogWarning($"Failed to manage service {serviceInfo.SystemdUnit}: {serviceResult.Error}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error managing service {serviceInfo.SystemdUnit}");
                    result.ServiceResults.Add(new ServiceResult
                    {
                        SystemdUnit = serviceInfo.SystemdUnit,
                        Success = false,
                        Error = ex.Message
                    });
                    result.ServicesFailed.Add(serviceInfo.SystemdUnit);
                }
            }

            result.Success = result.ServicesFailed.Count == 0;
            result.CompletedAt = DateTime.UtcNow;
            result.Duration = result.CompletedAt - result.StartedAt;

            _logger.LogInformation($"Service management completed: {result.ServicesStarted.Count} started, {result.ServicesRestarted.Count} restarted, {result.ServicesFailed.Count} failed");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            result.CompletedAt = DateTime.UtcNow;
            result.Duration = result.CompletedAt - result.StartedAt;
            _logger.LogError(ex, "Service management failed");
        }

        return result;
    }

    /// <summary>
    /// Collect all services defined by modules.
    /// </summary>
    private List<ModuleServiceInfo> CollectModuleServices()
    {
        var services = new List<ModuleServiceInfo>();
        var allModules = _moduleRegistry.GetAllModules();

        foreach (var moduleInfo in allModules)
        {
            try
            {
                var moduleServices = moduleInfo.Module.GetServices();
                foreach (var serviceDef in moduleServices)
                {
                    services.Add(new ModuleServiceInfo
                    {
                        ModuleId = moduleInfo.Module.Id,
                        PackageId = moduleInfo.Package.Definition.Id,
                        Name = serviceDef.Name,
                        SystemdUnit = serviceDef.SystemdUnit,
                        RequiredCapabilities = serviceDef.RequiredCapabilities
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting services from module {moduleInfo.Module.Id}");
            }
        }

        return services;
    }

    /// <summary>
    /// Determine which services need to be started or restarted.
    /// </summary>
    private List<ModuleServiceInfo> DetermineServicesToManage(
        List<ModuleServiceInfo> allServices,
        IEnumerable<string> modulesRequiringRestart)
    {
        var modulesRequiringRestartSet = new HashSet<string>(modulesRequiringRestart, StringComparer.OrdinalIgnoreCase);
        var servicesToManage = new List<ModuleServiceInfo>();

        foreach (var service in allServices)
        {
            // Check if service is already running
            var isRunning = IsServiceRunning(service.SystemdUnit).Result;
            
            if (modulesRequiringRestartSet.Contains(service.ModuleId))
            {
                // Module requires restart - restart the service
                servicesToManage.Add(service);
            }
            else if (!isRunning)
            {
                // Service is not running - start it
                servicesToManage.Add(service);
            }
        }

        return servicesToManage;
    }

    /// <summary>
    /// Sort services by dependencies (simple implementation - services from same module stay together).
    /// </summary>
    private List<ModuleServiceInfo> SortServicesByDependencies(
        List<ModuleServiceInfo> services,
        List<ModuleServiceInfo> allServices)
    {
        // Simple implementation: group by module, maintain order within module
        // Future: implement proper topological sort based on service dependencies
        return services
            .OrderBy(s => s.PackageId)
            .ThenBy(s => s.ModuleId)
            .ThenBy(s => s.Name)
            .ToList();
    }

    /// <summary>
    /// Manage a single service (start if not running, restart if running).
    /// </summary>
    private async Task<ServiceResult> ManageServiceAsync(
        ModuleServiceInfo serviceInfo,
        CancellationToken cancellationToken)
    {
        var result = new ServiceResult
        {
            SystemdUnit = serviceInfo.SystemdUnit,
            ModuleId = serviceInfo.ModuleId
        };

        try
        {
            // Check if service is running
            var isRunning = await IsServiceRunning(serviceInfo.SystemdUnit);

            if (isRunning)
            {
                // Restart the service
                _logger.LogInformation($"Restarting service: {serviceInfo.SystemdUnit}");
                var restartResult = await _commandRunner.RunAsync(new PlatformCommand
                {
                    FileName = "systemctl",
                    Arguments = $"restart {serviceInfo.SystemdUnit}",
                    UseSudo = true,
                    TimeoutMs = 30000
                }, cancellationToken);

                if (restartResult.ExitCode == 0)
                {
                    result.Success = true;
                    result.Action = "restarted";
                    _logger.LogInformation($"✓ Service restarted: {serviceInfo.SystemdUnit}");
                }
                else
                {
                    result.Success = false;
                    result.Error = restartResult.StdErr ?? $"systemctl restart exited with code {restartResult.ExitCode}";
                }
            }
            else
            {
                // Start the service
                _logger.LogInformation($"Starting service: {serviceInfo.SystemdUnit}");
                
                // First, enable the service if not already enabled
                var enableResult = await _commandRunner.RunAsync(new PlatformCommand
                {
                    FileName = "systemctl",
                    Arguments = $"enable {serviceInfo.SystemdUnit}",
                    UseSudo = true,
                    TimeoutMs = 10000
                }, cancellationToken);

                // Then start it
                var startResult = await _commandRunner.RunAsync(new PlatformCommand
                {
                    FileName = "systemctl",
                    Arguments = $"start {serviceInfo.SystemdUnit}",
                    UseSudo = true,
                    TimeoutMs = 30000
                }, cancellationToken);

                if (startResult.ExitCode == 0)
                {
                    result.Success = true;
                    result.Action = "started";
                    _logger.LogInformation($"✓ Service started: {serviceInfo.SystemdUnit}");
                }
                else
                {
                    result.Success = false;
                    result.Error = startResult.StdErr ?? $"systemctl start exited with code {startResult.ExitCode}";
                }
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Check if a systemd service is currently running.
    /// </summary>
    private async Task<bool> IsServiceRunning(string systemdUnit)
    {
        try
        {
            var result = await _commandRunner.RunAsync(new PlatformCommand
            {
                FileName = "systemctl",
                Arguments = $"is-active {systemdUnit}",
                UseSudo = false,
                TimeoutMs = 5000
            }, CancellationToken.None);

            // Exit code 0 means service is active
            return result.ExitCode == 0;
        }
        catch
        {
            // On error, assume service is not running
            return false;
        }
    }

    /// <summary>
    /// Get status of all module services.
    /// </summary>
    public async Task<List<ServiceStatus>> GetServiceStatusesAsync(CancellationToken cancellationToken = default)
    {
        var statuses = new List<ServiceStatus>();
        var moduleServices = CollectModuleServices();

        foreach (var service in moduleServices)
        {
            try
            {
                var isRunning = await IsServiceRunning(service.SystemdUnit);
                var isEnabled = await IsServiceEnabled(service.SystemdUnit);

                statuses.Add(new ServiceStatus
                {
                    SystemdUnit = service.SystemdUnit,
                    ModuleId = service.ModuleId,
                    PackageId = service.PackageId,
                    Name = service.Name,
                    IsRunning = isRunning,
                    IsEnabled = isEnabled
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting status for service {service.SystemdUnit}");
            }
        }

        return statuses;
    }

    /// <summary>
    /// Check if a systemd service is enabled.
    /// </summary>
    private async Task<bool> IsServiceEnabled(string systemdUnit)
    {
        try
        {
            var result = await _commandRunner.RunAsync(new PlatformCommand
            {
                FileName = "systemctl",
                Arguments = $"is-enabled {systemdUnit}",
                UseSudo = false,
                TimeoutMs = 5000
            }, CancellationToken.None);

            // Exit code 0 means service is enabled
            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Information about a module service.
/// </summary>
internal sealed class ModuleServiceInfo
{
    public string ModuleId { get; set; } = string.Empty;
    public string PackageId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SystemdUnit { get; set; } = string.Empty;
    public string[] RequiredCapabilities { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Result of managing a single service.
/// </summary>
public sealed class ServiceResult
{
    public string SystemdUnit { get; set; } = string.Empty;
    public string ModuleId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string Action { get; set; } = string.Empty; // "started" or "restarted"
}

/// <summary>
/// Result of service management operation.
/// </summary>
public sealed class ServiceManagementResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? Duration { get; set; }
    public List<ServiceResult> ServiceResults { get; set; } = new();
    public List<string> ServicesStarted { get; set; } = new();
    public List<string> ServicesRestarted { get; set; } = new();
    public List<string> ServicesFailed { get; set; } = new();
}

/// <summary>
/// Status of a module service.
/// </summary>
public sealed class ServiceStatus
{
    public string SystemdUnit { get; set; } = string.Empty;
    public string ModuleId { get; set; } = string.Empty;
    public string PackageId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsRunning { get; set; }
    public bool IsEnabled { get; set; }
}
