using System.Reflection;
using Monolith.FireWall.Common.Interfaces;
using Monolith.FireWall.Common.Models;

namespace Monolith.FireWall.Core.Services;

/// <summary>
/// Discovers Razor views in compiled Views assemblies
/// </summary>
public class RazorViewDiscovery
{
    private readonly ILogger _logger;

    public RazorViewDiscovery(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Discovers Razor views in a Views assembly
    /// </summary>
    public List<PageDefinition> DiscoverViews(Assembly viewsAssembly, string packageId, string packageName)
    {
        var views = new List<PageDefinition>();

        if (viewsAssembly == null)
        {
            _logger.LogWarning($"Views assembly is null for package {packageId}");
            return views;
        }

        _logger.LogInformation($"Discovering Razor views in {viewsAssembly.FullName}");

        try
        {
            // Method 1: Look for embedded resources (Razor views are embedded in RCL)
            var embeddedResources = viewsAssembly.GetManifestResourceNames()
                .Where(r => r.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase) ||
                           r.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
                .ToList();

            _logger.LogDebug($"Found {embeddedResources.Count} embedded view resource(s)");

            foreach (var resource in embeddedResources)
            {
                try
                {
                    var viewPath = ExtractViewPathFromResource(resource, packageName);
                    var route = GenerateRouteFromPath(viewPath, packageId);
                    var contentPath = $"/_content/{packageName}/{viewPath}";

                    views.Add(new PageDefinition(
                        route,
                        contentPath,
                        Array.Empty<string>() // Permissions will be set by module
                    ));

                    _logger.LogDebug($"Discovered view: {route} -> {contentPath}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Error processing view resource: {resource} - {ex.Message}");
                }
            }

            // Method 2: Look for types with PageAttribute (ASP.NET Core Razor Pages) - fallback
            if (views.Count == 0)
            {
                var pageTypes = viewsAssembly.GetTypes()
                    .Where(t => t.GetCustomAttributes()
                        .Any(a => a.GetType().Name.Contains("Page") || a.GetType().Name.Contains("Razor")))
                    .ToList();

                foreach (var pageType in pageTypes)
                {
                    try
                    {
                        var viewPath = ExtractViewPathFromType(pageType, packageName);
                        var route = GenerateRouteFromPath(viewPath, packageId);
                        var contentPath = $"/_content/{packageName}/{viewPath}";

                        views.Add(new PageDefinition(
                            route,
                            contentPath,
                            Array.Empty<string>()
                        ));

                        _logger.LogDebug($"Discovered view from type: {route} -> {contentPath}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Error processing page type: {pageType.FullName} - {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error discovering views in {viewsAssembly.FullName}: {ex.Message}");
        }

        _logger.LogInformation($"Discovered {views.Count} Razor view(s) for package {packageId}");
        return views;
    }

    private string ExtractViewPathFromResource(string resourceName, string packageName)
    {
        // Razor Class Library embeds views as: "Monolith.Network.Views.Pages.Dhcp.Config.cshtml"
        // We want: "Pages/Dhcp/Config.cshtml"

        // Remove package name prefix
        var prefix = $"{packageName.Replace("-", ".")}.Views.";
        if (resourceName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            resourceName = resourceName.Substring(prefix.Length);
        }

        // Replace dots with slashes (except file extension)
        var parts = resourceName.Split('.');
        if (parts.Length >= 2)
        {
            var extension = parts[parts.Length - 1]; // .cshtml or .razor
            var pathParts = parts.Take(parts.Length - 1);
            return string.Join("/", pathParts) + "." + extension;
        }

        return resourceName.Replace(".", "/");
    }

    private string GenerateRouteFromPath(string viewPath, string packageId)
    {
        // Convert "Pages/Dhcp/Config.cshtml" to "/p/monolith-network/dhcp/config"
        var pathWithoutExt = viewPath
            .Replace(".cshtml", "")
            .Replace(".razor", "")
            .Replace("Pages/", "")
            .Replace("pages/", "");

        var parts = pathWithoutExt.Split('/');
        if (parts.Length >= 2)
        {
            var module = parts[0].ToLowerInvariant();
            var page = parts.Length > 1 ? parts[1].ToLowerInvariant() : "index";
            return $"/p/{packageId.ToLowerInvariant()}/{module}/{page}";
        }

        return $"/p/{packageId.ToLowerInvariant()}/default/index";
    }

    private string ExtractViewPathFromType(Type pageType, string packageName)
    {
        // Extract view path from type name
        // Example: Monolith.Network.Pages.Dhcp.Config -> Pages/Dhcp/Config.cshtml
        var typeName = pageType.FullName ?? "";
        
        // Find "Pages" in the namespace
        var pagesIndex = typeName.IndexOf(".Pages.", StringComparison.Ordinal);
        if (pagesIndex >= 0)
        {
            var afterPages = typeName.Substring(pagesIndex + 7); // Skip ".Pages."
            var parts = afterPages.Split('.');
            return $"Pages/{string.Join("/", parts)}.cshtml";
        }

        // Fallback: use type name
        return $"Pages/{pageType.Name}.cshtml";
    }

    private string ExtractModuleFromPath(string path)
    {
        // Extract module name from path like "Pages/Dhcp/Config.cshtml"
        var parts = path.Split('/');
        if (parts.Length >= 2)
        {
            // Remove extension
            var moduleName = parts[1];
            if (moduleName.Contains('.'))
            {
                moduleName = moduleName.Substring(0, moduleName.LastIndexOf('.'));
            }
            return moduleName.ToLowerInvariant();
        }
        return "default";
    }

    private string ExtractPageFromPath(string path)
    {
        // Extract page name from path like "Pages/Dhcp/Config.cshtml"
        var parts = path.Split('/');
        if (parts.Length >= 3)
        {
            // Remove extension
            var pageName = parts[2];
            if (pageName.Contains('.'))
            {
                pageName = pageName.Substring(0, pageName.LastIndexOf('.'));
            }
            return pageName.ToLowerInvariant();
        }
        return "index";
    }
}
