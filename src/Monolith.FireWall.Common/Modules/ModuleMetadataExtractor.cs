using System.Collections.Concurrent;
using System.Reflection;
using Monolith.FireWall.Common.Attributes;
using Monolith.FireWall.Common.Controllers;
using Monolith.FireWall.Common.Interfaces;
using Monolith.FireWall.Common.Models;

namespace Monolith.FireWall.Common.Modules;

/// <summary>
/// Extracts module metadata from attributes.
/// </summary>
public static class ModuleMetadataExtractor
{
    private static readonly ConcurrentDictionary<Type, ModuleMetadata> _cache = new();

    /// <summary>
    /// Extract metadata from a module type.
    /// </summary>
    public static ModuleMetadata Extract(Type moduleType)
    {
        return _cache.GetOrAdd(moduleType, ExtractInternal);
    }

    private static ModuleMetadata ExtractInternal(Type moduleType)
    {
        var metadata = new ModuleMetadata();

        // Extract core module info
        var moduleAttr = moduleType.GetCustomAttribute<ModuleAttribute>();
        var packageAttr = moduleType.GetCustomAttribute<PackageAttribute>();

        var id = moduleAttr?.Id ?? "";
        var name = moduleAttr?.Name ?? "";
        var description = moduleAttr?.Description;
        var packageId = packageAttr?.PackageId ?? "";

        // Extract menu items
        var menuItems = new List<MenuDefinition>();
        foreach (var attr in moduleType.GetCustomAttributes<MenuItemAttribute>())
        {
            menuItems.Add(new MenuDefinition(
                attr.Id,
                attr.Label,
                attr.Icon,
                attr.Order,
                attr.RequiredPermissions ?? Array.Empty<string>(),
                null // Children handled separately if needed
            ));
        }

        // Extract pages
        var pages = new List<PageDefinition>();
        foreach (var attr in moduleType.GetCustomAttributes<PageAttribute>())
        {
            pages.Add(new PageDefinition(
                attr.Route,
                attr.RazorPath,
                attr.RequiredPermissions ?? Array.Empty<string>()
            ));
        }

        // Extract widgets
        var widgets = new List<WidgetDefinition>();
        foreach (var attr in moduleType.GetCustomAttributes<WidgetAttribute>())
        {
            widgets.Add(new WidgetDefinition(
                attr.Id,
                attr.Title,
                packageId,
                id,
                attr.Description,
                attr.Icon,
                attr.DefaultWidth,
                attr.DefaultHeight,
                attr.RefreshInterval,
                attr.RequiredPermissions ?? Array.Empty<string>()
            ));
        }

        // Extract templates
        var templates = new List<TemplateDefinition>();
        foreach (var attr in moduleType.GetCustomAttributes<TemplateAttribute>())
        {
            templates.Add(new TemplateDefinition(
                attr.Id,
                attr.ResourcePath,
                attr.OutputPath,
                attr.RequiresRoot
            ));
        }

        // Extract services
        var services = new List<ServiceDefinition>();
        foreach (var attr in moduleType.GetCustomAttributes<SystemdServiceAttribute>())
        {
            services.Add(new ServiceDefinition(
                attr.Name,
                attr.Unit,
                attr.RequiredCapabilities ?? Array.Empty<string>()
            ));
        }

        // Extract service bindings
        var serviceBindings = new List<ServiceBindingDefinition>();
        foreach (var attr in moduleType.GetCustomAttributes<ServiceBindingAttribute>())
        {
            serviceBindings.Add(new ServiceBindingDefinition(
                attr.Port,
                attr.Protocol,
                attr.InterfaceRole,
                attr.AddressFamily,
                attr.Description
            ));
        }

        // Extract APT dependencies
        var aptDependencies = new List<AptDependency>();
        foreach (var attr in moduleType.GetCustomAttributes<AptDependencyAttribute>())
        {
            aptDependencies.Add(new AptDependency(
                attr.PackageName,
                attr.MinVersion
            ));
        }

        // Extract permissions
        var permissions = new List<PermissionDefinition>();
        foreach (var attr in moduleType.GetCustomAttributes<PermissionAttribute>())
        {
            permissions.Add(new PermissionDefinition(
                attr.Id,
                attr.Name,
                attr.Category,
                attr.SubCategory ?? attr.Name
            ));
        }

        // Extract system permissions
        var systemPermissions = new List<SystemPermissionDefinition>();
        foreach (var attr in moduleType.GetCustomAttributes<SystemPermissionAttribute>())
        {
            systemPermissions.Add(new SystemPermissionDefinition(
                attr.Type,
                attr.Resource,
                attr.Justification
            ));
        }

        // Extract setup wizard pages
        var setupPages = new List<SetupWizardPage>();
        foreach (var attr in moduleType.GetCustomAttributes<SetupWizardPageAttribute>())
        {
            setupPages.Add(new SetupWizardPage
            {
                Id = attr.Id,
                Title = attr.Title,
                Description = attr.Description,
                Route = attr.Route,
                Order = attr.Order,
                IsRequired = attr.IsRequired,
                IsComplete = false,
                PackageId = packageId,
                ModuleId = id
            });
        }

        // Extract cron jobs from methods
        var cronJobs = new List<CronJobDefinition>();
        foreach (var method in moduleType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            var cronAttr = method.GetCustomAttribute<CronJobAttribute>();
            if (cronAttr != null)
            {
                var handler = CreateCronJobHandler(moduleType, method);
                cronJobs.Add(new CronJobDefinition(
                    cronAttr.Id,
                    cronAttr.Name,
                    cronAttr.CronExpression,
                    handler,
                    cronAttr.Enabled,
                    cronAttr.TimeoutSeconds,
                    cronAttr.MaxFailuresBeforeDisable
                ));
            }
        }

        // Discover controllers for this module
        var controllerTypes = new List<Type>();
        var routes = new List<RouteDefinition>();

        var assembly = moduleType.Assembly;
        var potentialControllers = assembly.GetTypes()
            .Where(t => typeof(ModuleController).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();

        foreach (var controllerType in potentialControllers)
        {
            var controllerAttr = controllerType.GetCustomAttribute<ModuleControllerAttribute>();
            string? controllerId = controllerAttr?.ModuleId;

            // If no explicit module ID, infer from naming convention (e.g., DhcpController -> dhcp)
            if (string.IsNullOrEmpty(controllerId))
            {
                var typeName = controllerType.Name;
                if (typeName.EndsWith("Controller", StringComparison.OrdinalIgnoreCase))
                {
                    controllerId = typeName[..^10].ToLowerInvariant();
                }
            }

            // Check if this controller belongs to this module
            if (!string.Equals(controllerId, id, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            controllerTypes.Add(controllerType);

            // Extract routes from controller
            foreach (var method in controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var routeAttrs = method.GetCustomAttributes<RouteActionAttribute>();
                foreach (var routeAttr in routeAttrs)
                {
                    var handler = CreateRouteHandler(controllerType, method);
                    routes.Add(new RouteDefinition(
                        routeAttr.Action,
                        handler,
                        routeAttr.RequiredPermissions ?? Array.Empty<string>()
                    ));
                }
            }
        }

        return new ModuleMetadata
        {
            Id = id,
            Name = name,
            PackageId = packageId,
            Description = description,
            Routes = routes,
            MenuItems = menuItems,
            Pages = pages,
            Widgets = widgets,
            Templates = templates,
            Services = services,
            ServiceBindings = serviceBindings,
            AptDependencies = aptDependencies,
            Permissions = permissions,
            SystemPermissions = systemPermissions,
            CronJobs = cronJobs,
            SetupPages = setupPages,
            ControllerTypes = controllerTypes
        };
    }

    private static Func<ApiRequest, Task<ApiResponse>> CreateRouteHandler(Type controllerType, MethodInfo method)
    {
        return async (request) =>
        {
            try
            {
                // Create controller instance
                var controller = (ModuleController?)Activator.CreateInstance(controllerType);
                if (controller == null)
                {
                    return new ApiResponse(false, null, $"Failed to create controller instance: {controllerType.Name}");
                }

                // Get module context from ambient context
                var context = ModuleContextAccessor.Current;
                if (context == null)
                {
                    return new ApiResponse(false, null, "No module context available");
                }

                // Set context on controller
                controller.SetContext(context, request);

                // Invoke the action method
                var result = method.Invoke(controller, null);

                if (result is Task<ApiResponse> taskResponse)
                {
                    return await taskResponse;
                }
                else if (result is Task task)
                {
                    await task;
                    // For void/Task methods, return success
                    return new ApiResponse(true, null, null);
                }
                else if (result is ApiResponse response)
                {
                    return response;
                }
                else
                {
                    // Return the result as data
                    return new ApiResponse(true, result, null);
                }
            }
            catch (TargetInvocationException ex)
            {
                // Unwrap the inner exception
                var innerEx = ex.InnerException ?? ex;
                return new ApiResponse(false, null, innerEx.Message);
            }
            catch (Exception ex)
            {
                return new ApiResponse(false, null, ex.Message);
            }
        };
    }

    private static Func<IModuleContext, CancellationToken, Task> CreateCronJobHandler(Type moduleType, MethodInfo method)
    {
        return async (context, cancellationToken) =>
        {
            // Get or create module instance
            var module = Activator.CreateInstance(moduleType);
            if (module == null)
            {
                throw new InvalidOperationException($"Failed to create module instance: {moduleType.Name}");
            }

            // If the module implements lifecycle, call OnStartAsync
            if (module is IMonolithModuleLifecycle lifecycle)
            {
                await lifecycle.OnStartAsync(context);
            }

            try
            {
                // Invoke the cron job method
                // Method signature should be: Task MethodName(IModuleContext context, CancellationToken token)
                var parameters = method.GetParameters();
                object?[] args;

                if (parameters.Length == 0)
                {
                    args = Array.Empty<object>();
                }
                else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(CancellationToken))
                {
                    args = new object[] { cancellationToken };
                }
                else if (parameters.Length == 1 && typeof(IModuleContext).IsAssignableFrom(parameters[0].ParameterType))
                {
                    args = new object[] { context };
                }
                else if (parameters.Length == 2)
                {
                    args = new object[] { context, cancellationToken };
                }
                else
                {
                    args = Array.Empty<object>();
                }

                var result = method.Invoke(module, args);

                if (result is Task task)
                {
                    await task;
                }
            }
            finally
            {
                // Call OnStopAsync if module implements lifecycle
                if (module is IMonolithModuleLifecycle lifecycle2)
                {
                    await lifecycle2.OnStopAsync(context);
                }
            }
        };
    }

    /// <summary>
    /// Clear the metadata cache. Useful for testing.
    /// </summary>
    public static void ClearCache()
    {
        _cache.Clear();
    }
}
