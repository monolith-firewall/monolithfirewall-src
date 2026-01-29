using System.Text.Json;
using Monolith.FireWall.Common.Models;
using Monolith.FireWall.Core.Models;
using Monolith.FireWall.Core.Services;

namespace Monolith.FireWall.Core.Transport.Handlers;

/// <summary>
/// Handles module service operations - list, status, start, stop, restart, enable, disable, logs.
/// </summary>
public sealed class ModuleServicesHandler : ICoreRequestHandler
{
    private static readonly HashSet<string> Actions = new(StringComparer.OrdinalIgnoreCase)
    {
        "module.services.list",
        "module.services.status",
        "module.services.start",
        "module.services.stop",
        "module.services.restart",
        "module.services.enable",
        "module.services.disable",
        "module.services.logs"
    };

    public bool CanHandle(string action) => Actions.Contains(action);

    public async Task<ApiResponse> HandleAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        var action = request.GetProperty("action").GetString() ?? string.Empty;

        return action.ToLowerInvariant() switch
        {
            "module.services.list" => await HandleListAsync(context, cancellationToken),
            "module.services.status" => await HandleStatusAsync(context, request, cancellationToken),
            "module.services.start" => await HandleStartAsync(context, request, cancellationToken),
            "module.services.stop" => await HandleStopAsync(context, request, cancellationToken),
            "module.services.restart" => await HandleRestartAsync(context, request, cancellationToken),
            "module.services.enable" => await HandleEnableAsync(context, request, cancellationToken),
            "module.services.disable" => await HandleDisableAsync(context, request, cancellationToken),
            "module.services.logs" => await HandleLogsAsync(context, request, cancellationToken),
            _ => new ApiResponse(false, null, $"Unknown action: {action}")
        };
    }

    /// <summary>
    /// List all module services with their status.
    /// </summary>
    private async Task<ApiResponse> HandleListAsync(CoreRequestContext context, CancellationToken cancellationToken)
    {
        try
        {
            var services = new List<object>();
            var allModules = context.ModuleRegistry.GetAllModules();
            var systemdManager = new SystemdServiceManager(context.CommandRunner);

            // Get interface assignments for binding resolution
            var interfacesByRole = await GetInterfacesByRoleAsync(context);

            foreach (var moduleInfo in allModules)
            {
                try
                {
                    var moduleServices = moduleInfo.Module.GetServices();
                    var moduleBindings = moduleInfo.Module.GetServiceBindings();

                    foreach (var serviceDef in moduleServices)
                    {
                        var status = await systemdManager.GetServiceStatusAsync(serviceDef.SystemdUnit, cancellationToken);

                        // Resolve bindings for this module
                        var resolvedBindings = ResolveServiceBindings(moduleBindings, interfacesByRole);

                        services.Add(new
                        {
                            systemdUnit = serviceDef.SystemdUnit,
                            name = serviceDef.Name,
                            moduleId = moduleInfo.Module.Id,
                            moduleName = moduleInfo.Module.Name,
                            packageId = moduleInfo.Package.Definition.Id,
                            packageName = moduleInfo.Package.Definition.Name,
                            activeState = status.RawActiveState,
                            enabledState = status.RawEnabledState,
                            isRunning = status.IsRunning,
                            isEnabled = status.IsEnabled,
                            requiredCapabilities = serviceDef.RequiredCapabilities,
                            bindings = resolvedBindings
                        });
                    }
                }
                catch (Exception ex)
                {
                    context.Logger.LogError(ex, $"Error getting services from module {moduleInfo.Module.Id}");
                }
            }

            return new ApiResponse(true, services, null);
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, "Error listing module services");
            return new ApiResponse(false, null, $"Error listing services: {ex.Message}");
        }
    }

    /// <summary>
    /// Get status for a specific service.
    /// </summary>
    private async Task<ApiResponse> HandleStatusAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        if (!CoreRequestParsing.TryGetPayload(request, out ServiceUnitRequest payload, out var parseError))
        {
            return new ApiResponse(false, null, parseError);
        }

        if (string.IsNullOrWhiteSpace(payload.SystemdUnit))
        {
            return new ApiResponse(false, null, "SystemdUnit is required");
        }

        try
        {
            var systemdManager = new SystemdServiceManager(context.CommandRunner);
            var status = await systemdManager.GetServiceStatusAsync(payload.SystemdUnit, cancellationToken);

            return new ApiResponse(true, new
            {
                systemdUnit = status.ServiceName,
                activeState = status.RawActiveState,
                enabledState = status.RawEnabledState,
                isRunning = status.IsRunning,
                isEnabled = status.IsEnabled
            }, null);
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, $"Error getting status for service {payload.SystemdUnit}");
            return new ApiResponse(false, null, $"Error getting service status: {ex.Message}");
        }
    }

    /// <summary>
    /// Start a service.
    /// </summary>
    private async Task<ApiResponse> HandleStartAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        if (!CoreRequestParsing.TryGetPayload(request, out ServiceUnitRequest payload, out var parseError))
        {
            return new ApiResponse(false, null, parseError);
        }

        if (string.IsNullOrWhiteSpace(payload.SystemdUnit))
        {
            return new ApiResponse(false, null, "SystemdUnit is required");
        }

        // Validate that this is a known module service
        if (!IsKnownModuleService(context, payload.SystemdUnit))
        {
            return new ApiResponse(false, null, $"Service {payload.SystemdUnit} is not a known module service");
        }

        try
        {
            var systemdManager = new SystemdServiceManager(context.CommandRunner);
            var result = await systemdManager.StartServiceAsync(payload.SystemdUnit, cancellationToken);

            if (result.Success)
            {
                context.Logger.LogInformation($"Service started: {payload.SystemdUnit}");
                return new ApiResponse(true, new { operation = "start", result = "started", systemdUnit = payload.SystemdUnit }, null);
            }

            return new ApiResponse(false, null, result.ErrorMessage ?? "Failed to start service");
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, $"Error starting service {payload.SystemdUnit}");
            return new ApiResponse(false, null, $"Error starting service: {ex.Message}");
        }
    }

    /// <summary>
    /// Stop a service.
    /// </summary>
    private async Task<ApiResponse> HandleStopAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        if (!CoreRequestParsing.TryGetPayload(request, out ServiceUnitRequest payload, out var parseError))
        {
            return new ApiResponse(false, null, parseError);
        }

        if (string.IsNullOrWhiteSpace(payload.SystemdUnit))
        {
            return new ApiResponse(false, null, "SystemdUnit is required");
        }

        // Validate that this is a known module service
        if (!IsKnownModuleService(context, payload.SystemdUnit))
        {
            return new ApiResponse(false, null, $"Service {payload.SystemdUnit} is not a known module service");
        }

        try
        {
            var systemdManager = new SystemdServiceManager(context.CommandRunner);
            var result = await systemdManager.StopServiceAsync(payload.SystemdUnit, cancellationToken);

            if (result.Success)
            {
                context.Logger.LogInformation($"Service stopped: {payload.SystemdUnit}");
                return new ApiResponse(true, new { operation = "stop", result = "stopped", systemdUnit = payload.SystemdUnit }, null);
            }

            return new ApiResponse(false, null, result.ErrorMessage ?? "Failed to stop service");
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, $"Error stopping service {payload.SystemdUnit}");
            return new ApiResponse(false, null, $"Error stopping service: {ex.Message}");
        }
    }

    /// <summary>
    /// Restart a service.
    /// </summary>
    private async Task<ApiResponse> HandleRestartAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        if (!CoreRequestParsing.TryGetPayload(request, out ServiceUnitRequest payload, out var parseError))
        {
            return new ApiResponse(false, null, parseError);
        }

        if (string.IsNullOrWhiteSpace(payload.SystemdUnit))
        {
            return new ApiResponse(false, null, "SystemdUnit is required");
        }

        // Validate that this is a known module service
        if (!IsKnownModuleService(context, payload.SystemdUnit))
        {
            return new ApiResponse(false, null, $"Service {payload.SystemdUnit} is not a known module service");
        }

        try
        {
            var systemdManager = new SystemdServiceManager(context.CommandRunner);
            var result = await systemdManager.RestartServiceAsync(payload.SystemdUnit, cancellationToken);

            if (result.Success)
            {
                context.Logger.LogInformation($"Service restarted: {payload.SystemdUnit}");
                return new ApiResponse(true, new { operation = "restart", result = "restarted", systemdUnit = payload.SystemdUnit }, null);
            }

            return new ApiResponse(false, null, result.ErrorMessage ?? "Failed to restart service");
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, $"Error restarting service {payload.SystemdUnit}");
            return new ApiResponse(false, null, $"Error restarting service: {ex.Message}");
        }
    }

    /// <summary>
    /// Enable a service to start on boot.
    /// </summary>
    private async Task<ApiResponse> HandleEnableAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        if (!CoreRequestParsing.TryGetPayload(request, out ServiceUnitRequest payload, out var parseError))
        {
            return new ApiResponse(false, null, parseError);
        }

        if (string.IsNullOrWhiteSpace(payload.SystemdUnit))
        {
            return new ApiResponse(false, null, "SystemdUnit is required");
        }

        // Validate that this is a known module service
        if (!IsKnownModuleService(context, payload.SystemdUnit))
        {
            return new ApiResponse(false, null, $"Service {payload.SystemdUnit} is not a known module service");
        }

        try
        {
            var systemdManager = new SystemdServiceManager(context.CommandRunner);
            var result = await systemdManager.EnableServiceAsync(payload.SystemdUnit, cancellationToken);

            if (result.Success)
            {
                context.Logger.LogInformation($"Service enabled: {payload.SystemdUnit}");
                return new ApiResponse(true, new { operation = "enable", result = "enabled", systemdUnit = payload.SystemdUnit }, null);
            }

            return new ApiResponse(false, null, result.ErrorMessage ?? "Failed to enable service");
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, $"Error enabling service {payload.SystemdUnit}");
            return new ApiResponse(false, null, $"Error enabling service: {ex.Message}");
        }
    }

    /// <summary>
    /// Disable a service from starting on boot.
    /// </summary>
    private async Task<ApiResponse> HandleDisableAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        if (!CoreRequestParsing.TryGetPayload(request, out ServiceUnitRequest payload, out var parseError))
        {
            return new ApiResponse(false, null, parseError);
        }

        if (string.IsNullOrWhiteSpace(payload.SystemdUnit))
        {
            return new ApiResponse(false, null, "SystemdUnit is required");
        }

        // Validate that this is a known module service
        if (!IsKnownModuleService(context, payload.SystemdUnit))
        {
            return new ApiResponse(false, null, $"Service {payload.SystemdUnit} is not a known module service");
        }

        try
        {
            var systemdManager = new SystemdServiceManager(context.CommandRunner);
            var result = await systemdManager.DisableServiceAsync(payload.SystemdUnit, cancellationToken);

            if (result.Success)
            {
                context.Logger.LogInformation($"Service disabled: {payload.SystemdUnit}");
                return new ApiResponse(true, new { operation = "disable", result = "disabled", systemdUnit = payload.SystemdUnit }, null);
            }

            return new ApiResponse(false, null, result.ErrorMessage ?? "Failed to disable service");
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, $"Error disabling service {payload.SystemdUnit}");
            return new ApiResponse(false, null, $"Error disabling service: {ex.Message}");
        }
    }

    /// <summary>
    /// Get logs for a service.
    /// </summary>
    private async Task<ApiResponse> HandleLogsAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        if (!CoreRequestParsing.TryGetPayload(request, out ServiceLogRequest payload, out var parseError))
        {
            return new ApiResponse(false, null, parseError);
        }

        if (string.IsNullOrWhiteSpace(payload.SystemdUnit))
        {
            return new ApiResponse(false, null, "SystemdUnit is required");
        }

        try
        {
            var logManager = new ServiceLogManager(context.CommandRunner);
            var query = new ServiceLogQuery
            {
                SystemdUnit = payload.SystemdUnit,
                Limit = payload.Limit ?? 100,
                Priority = payload.Priority
            };

            if (!string.IsNullOrWhiteSpace(payload.Since))
            {
                if (DateTime.TryParse(payload.Since, out var since))
                {
                    query.Since = since;
                }
            }

            if (!string.IsNullOrWhiteSpace(payload.Until))
            {
                if (DateTime.TryParse(payload.Until, out var until))
                {
                    query.Until = until;
                }
            }

            var result = await logManager.GetLogsAsync(query, cancellationToken);

            if (!result.Success)
            {
                return new ApiResponse(false, null, result.Error ?? "Failed to retrieve logs");
            }

            return new ApiResponse(true, new
            {
                logs = result.Logs.Select(l => new
                {
                    timestamp = l.Timestamp.ToString("o"),
                    message = l.Message,
                    priority = l.Priority,
                    priorityLevel = l.PriorityLevel,
                    hostname = l.Hostname,
                    unit = l.Unit,
                    pid = l.Pid,
                    identifier = l.Identifier
                }),
                totalCount = result.TotalCount
            }, null);
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, $"Error getting logs for service {payload.SystemdUnit}");
            return new ApiResponse(false, null, $"Error getting service logs: {ex.Message}");
        }
    }

    /// <summary>
    /// Check if a systemd unit is a known module service.
    /// </summary>
    private bool IsKnownModuleService(CoreRequestContext context, string systemdUnit)
    {
        var allModules = context.ModuleRegistry.GetAllModules();

        foreach (var moduleInfo in allModules)
        {
            try
            {
                var services = moduleInfo.Module.GetServices();
                if (services.Any(s => string.Equals(s.SystemdUnit, systemdUnit, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
            catch
            {
                // Ignore errors from individual modules
            }
        }

        return false;
    }

    /// <summary>
    /// Get interfaces by their assigned role (lan, wan, etc).
    /// </summary>
    private async Task<Dictionary<string, List<InterfaceInfo>>> GetInterfacesByRoleAsync(CoreRequestContext context)
    {
        var result = new Dictionary<string, List<InterfaceInfo>>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var snapshot = await context.InterfaceAssignments.GetSnapshotAsync(CancellationToken.None);

            foreach (var assignment in snapshot.Assigned)
            {
                var role = assignment.Role.ToString().ToLowerInvariant();
                if (role == "unknown") continue;

                if (!result.ContainsKey(role))
                {
                    result[role] = new List<InterfaceInfo>();
                }

                result[role].Add(new InterfaceInfo
                {
                    Name = assignment.Interface,
                    Ipv4Address = assignment.IpAddress,
                    Ipv6Address = assignment.Ipv6Address
                });
            }
        }
        catch
        {
            // Return empty dictionary on error
        }

        return result;
    }

    /// <summary>
    /// Resolve service bindings to actual interface IPs.
    /// </summary>
    private List<object> ResolveServiceBindings(
        IEnumerable<Common.Models.ServiceBindingDefinition> bindings,
        Dictionary<string, List<InterfaceInfo>> interfacesByRole)
    {
        var result = new List<object>();

        foreach (var binding in bindings)
        {
            var role = binding.InterfaceRole.ToLowerInvariant();

            if (interfacesByRole.TryGetValue(role, out var interfaces))
            {
                foreach (var iface in interfaces)
                {
                    // Determine which IP to use based on address family
                    string? ip = null;
                    if (binding.AddressFamily == "ipv4" && !string.IsNullOrWhiteSpace(iface.Ipv4Address))
                    {
                        ip = iface.Ipv4Address.Split('/')[0]; // Remove CIDR notation
                    }
                    else if (binding.AddressFamily == "ipv6" && !string.IsNullOrWhiteSpace(iface.Ipv6Address))
                    {
                        ip = iface.Ipv6Address.Split('/')[0];
                    }
                    else if (binding.AddressFamily == "both")
                    {
                        // For "both", prefer IPv4 if available
                        ip = !string.IsNullOrWhiteSpace(iface.Ipv4Address)
                            ? iface.Ipv4Address.Split('/')[0]
                            : iface.Ipv6Address?.Split('/')[0];
                    }

                    if (!string.IsNullOrWhiteSpace(ip))
                    {
                        result.Add(new
                        {
                            @interface = iface.Name,
                            ip = ip,
                            port = binding.Port,
                            protocol = binding.Protocol,
                            description = binding.Description
                        });
                    }
                }
            }
            else
            {
                // No interfaces assigned to this role, show binding without specific interface
                result.Add(new
                {
                    @interface = $"({role})",
                    ip = "*",
                    port = binding.Port,
                    protocol = binding.Protocol,
                    description = binding.Description
                });
            }
        }

        return result;
    }

    private class InterfaceInfo
    {
        public string Name { get; set; } = "";
        public string? Ipv4Address { get; set; }
        public string? Ipv6Address { get; set; }
    }
}

/// <summary>
/// Request payload for service operations requiring a systemd unit name.
/// </summary>
internal sealed class ServiceUnitRequest
{
    public string SystemdUnit { get; set; } = string.Empty;
}

/// <summary>
/// Request payload for service log queries.
/// </summary>
internal sealed class ServiceLogRequest
{
    public string SystemdUnit { get; set; } = string.Empty;
    public int? Limit { get; set; }
    public string? Since { get; set; }
    public string? Until { get; set; }
    public string? Priority { get; set; }
}
