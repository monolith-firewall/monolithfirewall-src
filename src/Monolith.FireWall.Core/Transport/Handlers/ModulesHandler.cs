using System.Text.Json;
using Monolith.FireWall.Common.Models;
using Monolith.FireWall.Core.Models;
using Monolith.FireWall.Core.Services;

namespace Monolith.FireWall.Core.Transport.Handlers;

public sealed class ModulesHandler : ICoreRequestHandler
{
    private static readonly HashSet<string> Actions = new(StringComparer.OrdinalIgnoreCase)
    {
        "modules.enable",
        "modules.disable"
    };

    public bool CanHandle(string action) => Actions.Contains(action);

    public async Task<ApiResponse> HandleAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        var action = request.GetProperty("action").GetString() ?? string.Empty;
        if (!CoreRequestParsing.TryGetPayload(request, out ModuleStateRequest moduleRequest, out var moduleError))
        {
            return new ApiResponse(false, null, moduleError);
        }

        var enable = action.EndsWith("enable", StringComparison.OrdinalIgnoreCase);
        var moduleUpdated = await context.PackageStateStore.SetModuleEnabledAsync(
            moduleRequest.PackageId,
            moduleRequest.ModuleId,
            enable);

        if (!moduleUpdated)
        {
            return new ApiResponse(false, null, "Failed to update module state");
        }

        // Start or stop module services based on enable/disable action
        var serviceResults = await ManageModuleServicesAsync(
            context,
            moduleRequest.PackageId,
            moduleRequest.ModuleId,
            enable,
            cancellationToken);

        return new ApiResponse(true, new
        {
            packageId = moduleRequest.PackageId,
            moduleId = moduleRequest.ModuleId,
            enabled = enable,
            serviceResults = serviceResults
        }, null);
    }

    /// <summary>
    /// Start or stop services associated with a module.
    /// </summary>
    private async Task<List<object>> ManageModuleServicesAsync(
        CoreRequestContext context,
        string packageId,
        string moduleId,
        bool enable,
        CancellationToken cancellationToken)
    {
        var results = new List<object>();

        try
        {
            // Find the module
            var moduleInfo = context.ModuleRegistry.GetAllModules()
                .FirstOrDefault(m =>
                    string.Equals(m.Module.Id, moduleId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(m.Package.Definition.Id, packageId, StringComparison.OrdinalIgnoreCase));

            if (moduleInfo == null)
            {
                context.Logger.LogWarning($"Module not found for service management: {packageId}/{moduleId}");
                return results;
            }

            // Get services for this module
            var services = moduleInfo.Module.GetServices();
            if (!services.Any())
            {
                return results;
            }

            var systemdManager = new SystemdServiceManager(context.CommandRunner);

            foreach (var service in services)
            {
                try
                {
                    ServiceOperationResult result;

                    if (enable)
                    {
                        // Enable and start the service
                        await systemdManager.EnableServiceAsync(service.SystemdUnit, cancellationToken);
                        result = await systemdManager.StartServiceAsync(service.SystemdUnit, cancellationToken);
                        context.Logger.LogInformation($"Module {moduleId}: Started service {service.SystemdUnit} (success: {result.Success})");
                    }
                    else
                    {
                        // Stop and disable the service
                        result = await systemdManager.StopServiceAsync(service.SystemdUnit, cancellationToken);
                        await systemdManager.DisableServiceAsync(service.SystemdUnit, cancellationToken);
                        context.Logger.LogInformation($"Module {moduleId}: Stopped service {service.SystemdUnit} (success: {result.Success})");
                    }

                    results.Add(new
                    {
                        systemdUnit = service.SystemdUnit,
                        name = service.Name,
                        operation = enable ? "start" : "stop",
                        success = result.Success,
                        error = result.ErrorMessage
                    });
                }
                catch (Exception ex)
                {
                    context.Logger.LogError(ex, $"Error managing service {service.SystemdUnit} for module {moduleId}");
                    results.Add(new
                    {
                        systemdUnit = service.SystemdUnit,
                        name = service.Name,
                        operation = enable ? "start" : "stop",
                        success = false,
                        error = ex.Message
                    });
                }
            }
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, $"Error managing services for module {packageId}/{moduleId}");
        }

        return results;
    }
}
