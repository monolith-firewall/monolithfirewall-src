using System.Text.Json;
using Monolith.FireWall.Common.Models;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Transport.Handlers;

public sealed class SystemMetadataHandler : ICoreRequestHandler
{
    private static readonly HashSet<string> Actions = new(StringComparer.OrdinalIgnoreCase)
    {
        "get-menus",
        "get-all-pages",
        "get-pages",
        "get-widgets",
        "get-modules",
        "get-packages"
    };

    public bool CanHandle(string action) => Actions.Contains(action);

    public async Task<ApiResponse> HandleAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        return request.TryGetProperty("action", out var actionEl)
            ? await HandleActionAsync(context, actionEl.GetString() ?? string.Empty, cancellationToken)
            : new ApiResponse(false, null, "Action is required");
    }

    private async Task<ApiResponse> HandleActionAsync(CoreRequestContext context, string action, CancellationToken cancellationToken)
    {
        switch (action)
        {
            case "get-menus":
                var menus = context.ModuleRegistry.GetAllModules()
                    .SelectMany(m => m.Module.GetMenuItems().Select(menu => new
                    {
                        menu.Id,
                        menu.Label,
                        menu.Icon,
                        menu.Order,
                        menu.RequiredPermissions,
                        menu.Children,
                        PackageId = m.Package.Definition.Id,
                        PackageName = m.Package.Definition.Name,
                        ModuleId = m.Module.Id,
                        ModuleName = m.Module.Name
                    }))
                    .ToList();
                return new ApiResponse(true, menus, null);

            case "get-all-pages":
                var allPages = context.ModuleRegistry.GetAllPages().ToList();
                return new ApiResponse(true, new { pages = allPages }, null);

            case "get-pages":
                var pages = context.ModuleRegistry.GetAllModules()
                    .SelectMany(m => m.Module.GetPages())
                    .ToList();
                return new ApiResponse(true, pages, null);

            case "get-widgets":
                var widgets = context.ModuleRegistry.GetAllModules()
                    .SelectMany(m => m.Module.GetWidgets())
                    .ToList();
                return new ApiResponse(true, widgets, null);

            case "get-modules":
                var moduleStateByPackage = new Dictionary<string, Dictionary<string, ModuleStateEntity>>(StringComparer.OrdinalIgnoreCase);
                foreach (var package in context.ModuleRegistry.GetAllPackages())
                {
                    moduleStateByPackage[package.Definition.Id] = await context.PackageStateStore.GetModuleStatesAsync(package.Definition.Id);
                }

                var modulesList = context.ModuleRegistry.GetAllModules(includeDisabled: true)
                    .Select(m =>
                    {
                        var stateMap = moduleStateByPackage.TryGetValue(m.Package.Definition.Id, out var map)
                            ? map
                            : null;
                        var enabled = stateMap != null && stateMap.TryGetValue(m.Module.Id, out var state)
                            ? state.Enabled
                            : true;

                        return new
                        {
                            id = m.Module.Id,
                            name = m.Module.Name,
                            packageId = m.Package.Definition.Id,
                            packageName = m.Package.Definition.Name,
                            enabled,
                            requiredPermissions = m.Module.GetRequiredPermissions().Select(r => r.Id).ToList(),
                            systemPermissions = m.Module.GetSystemPermissions().Select(sp => new
                            {
                                type = sp.Type.ToString(),
                                resource = sp.Resource,
                                justification = sp.Justification
                            }).ToList()
                        };
                    })
                    .ToList();
                return new ApiResponse(true, modulesList, null);

            case "get-packages":
                var installations = await context.PackageStateStore.GetPackagesAsync();
                var installMap = installations.ToDictionary(p => p.PackageId, p => p, StringComparer.OrdinalIgnoreCase);
                var packages = new List<object>();

                foreach (var package in context.ModuleRegistry.GetAllPackages())
                {
                    var moduleStates = await context.PackageStateStore.GetModuleStatesAsync(package.Definition.Id);
                    installMap.TryGetValue(package.Definition.Id, out var installInfo);

                    packages.Add(new
                    {
                        id = package.Definition.Id,
                        name = package.Definition.Name,
                        version = package.Definition.Version,
                        description = package.Definition.Description,
                        author = package.Definition.Author,
                        hasRazorViews = package.HasRazorViews,
                        viewsAssemblyPath = GetViewsAssemblyPath(package),
                        viewsAssemblyName = package.MainAssembly?.FullName, // Views are in main assembly
                        packageDirectory = package.PackageDirectory,
                        installedVersion = installInfo?.Version,
                        installedAt = installInfo?.InstalledAt,
                        installSource = installInfo?.Source,
                        modules = package.Definition.GetModules().Select(m => new
                        {
                            id = m.Id,
                            name = m.Name,
                            enabled = moduleStates.TryGetValue(m.Id, out var state) ? state.Enabled : true,
                            requiredPermissions = m.GetRequiredPermissions().Select(r => r.Id).ToList(),
                            systemPermissions = m.GetSystemPermissions().Select(sp => new
                            {
                                type = sp.Type.ToString(),
                                resource = sp.Resource,
                                justification = sp.Justification
                            }).ToList()
                        }).ToList()
                    });
                }

                return new ApiResponse(true, packages, null);
        }

        return new ApiResponse(false, null, $"Unhandled action: {action}");
    }

    private static string? GetViewsAssemblyPath(PackageInfo package)
    {
        // Views are embedded in main assembly when using Microsoft.NET.Sdk.Razor
        // Return main assembly path as views assembly path
        if (package.MainAssembly == null)
        {
            return null;
        }

        try
        {
            var location = package.MainAssembly.Location;
            if (!string.IsNullOrEmpty(location))
            {
                return location;
            }

            var codeBase = package.MainAssembly.CodeBase;
            if (!string.IsNullOrEmpty(codeBase))
            {
                var uri = new Uri(codeBase);
                return uri.LocalPath;
            }
        }
        catch
        {
            // Location may not be available.
        }

        return null;
    }
}
