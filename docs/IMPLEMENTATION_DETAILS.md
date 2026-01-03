# MonolithFireWall - Implementation Details

**Detailed implementation for PackageScanner, RazorViewDiscovery, and PackageViewRouter**

---

## PackageScanner Implementation

### File: `src/Monolith.FireWall.Core/Services/PackageScanner.cs`

```csharp
using System.Text.Json;
using Monolith.FireWall.Common.Models;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Services;

/// <summary>
/// Scans the packages directory for available packages
/// </summary>
public class PackageScanner
{
    private readonly ILogger _logger;

    public PackageScanner(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Scans the packages directory for packages
    /// </summary>
    public async Task<List<PackageDiscoveryInfo>> ScanPackagesAsync(string packagesDirectory)
    {
        var packages = new List<PackageDiscoveryInfo>();

        if (!Directory.Exists(packagesDirectory))
        {
            _logger.LogWarning($"Packages directory does not exist: {packagesDirectory}");
            return packages;
        }

        _logger.LogInformation($"Scanning for packages in: {packagesDirectory}");

        foreach (var packageDir in Directory.GetDirectories(packagesDirectory))
        {
            try
            {
                var discoveryInfo = await DiscoverPackageAsync(packageDir);
                if (discoveryInfo != null)
                {
                    packages.Add(discoveryInfo);
                    _logger.LogInformation($"Discovered package: {discoveryInfo.Manifest.Id} v{discoveryInfo.Manifest.Version}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error discovering package in {packageDir}");
            }
        }

        _logger.LogInformation($"Found {packages.Count} package(s)");
        return packages;
    }

    private async Task<PackageDiscoveryInfo?> DiscoverPackageAsync(string packageDir)
    {
        // Check for manifest.json
        var manifestPath = Path.Combine(packageDir, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            _logger.LogDebug($"No manifest.json found in {packageDir}");
            return null;
        }

        // Read and parse manifest
        var manifestJson = await File.ReadAllTextAsync(manifestPath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        var manifest = JsonSerializer.Deserialize<PackageManifest>(manifestJson, options);

        if (manifest == null)
        {
            _logger.LogWarning($"Failed to parse manifest.json in {packageDir}");
            return null;
        }

        // Check for backend directory
        var backendDir = Path.Combine(packageDir, "backend");
        if (!Directory.Exists(backendDir))
        {
            _logger.LogWarning($"No backend directory found in {packageDir}");
            return null;
        }

        // Find main DLL
        var mainDll = FindMainDll(backendDir, manifest.Id);
        if (mainDll == null)
        {
            _logger.LogWarning($"Main DLL not found for package {manifest.Id}");
            return null;
        }

        // Find Views DLL (optional - only for RCL packages)
        var viewsDll = FindViewsDll(backendDir, manifest.Id);

        return new PackageDiscoveryInfo
        {
            Directory = packageDir,
            Manifest = manifest,
            MainDllPath = mainDll,
            ViewsDllPath = viewsDll
        };
    }

    private string? FindMainDll(string backendDir, string packageId)
    {
        // Convert package ID to DLL name
        // "monolith-network" -> "Monolith.Network.dll"
        var dllName = ConvertPackageIdToDllName(packageId);
        var dllPath = Path.Combine(backendDir, dllName);

        if (File.Exists(dllPath))
            return dllPath;

        // Fallback: search for any DLL matching the pattern
        var dllFiles = Directory.GetFiles(backendDir, "*.dll", SearchOption.TopDirectoryOnly);
        var matchingDll = dllFiles.FirstOrDefault(f =>
        {
            var fileName = Path.GetFileNameWithoutExtension(f);
            return fileName.Equals(packageId.Replace("-", "."), StringComparison.OrdinalIgnoreCase) ||
                   fileName.Equals(dllName.Replace(".dll", ""), StringComparison.OrdinalIgnoreCase);
        });

        return matchingDll;
    }

    private string? FindViewsDll(string backendDir, string packageId)
    {
        // Views DLL: "Monolith.Network.Views.dll"
        var viewsDllName = ConvertPackageIdToDllName(packageId).Replace(".dll", ".Views.dll");
        var viewsDllPath = Path.Combine(backendDir, viewsDllName);

        if (File.Exists(viewsDllPath))
            return viewsDllPath;

        return null; // Views DLL is optional
    }

    private string ConvertPackageIdToDllName(string packageId)
    {
        // "monolith-network" -> "Monolith.Network.dll"
        var parts = packageId.Split('-');
        var dllName = string.Join(".", parts.Select(p => char.ToUpper(p[0]) + p.Substring(1)));
        return $"{dllName}.dll";
    }
}

/// <summary>
/// Information about a discovered package
/// </summary>
public class PackageDiscoveryInfo
{
    public string Directory { get; set; } = "";
    public PackageManifest Manifest { get; set; } = null!;
    public string MainDllPath { get; set; } = "";
    public string? ViewsDllPath { get; set; }
    public bool HasRazorViews => ViewsDllPath != null;
}
```

---

## RazorViewDiscovery Implementation

### File: `src/Monolith.FireWall.Core/Services/RazorViewDiscovery.cs`

```csharp
using System.Reflection;
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
            // Method 1: Look for types with PageAttribute (ASP.NET Core Razor Pages)
            var pageTypes = viewsAssembly.GetTypes()
                .Where(t => t.GetCustomAttribute<Microsoft.AspNetCore.Mvc.RazorPages.PageAttribute>() != null)
                .ToList();

            foreach (var pageType in pageTypes)
            {
                var pageAttribute = pageType.GetCustomAttribute<Microsoft.AspNetCore.Mvc.RazorPages.PageAttribute>();
                if (pageAttribute != null && !string.IsNullOrEmpty(pageAttribute.Route))
                {
                    var route = NormalizeRoute(pageAttribute.Route);
                    var viewPath = ExtractViewPathFromType(pageType, packageName);
                    var contentPath = $"/_content/{packageName}/{viewPath}";

                    views.Add(new PageDefinition(
                        route,
                        contentPath,
                        Array.Empty<string>(), // Permissions will be set by module
                        null,
                        null
                    ));

                    _logger.LogDebug($"Discovered view: {route} -> {contentPath}");
                }
            }

            // Method 2: Look for embedded resources (fallback)
            if (views.Count == 0)
            {
                var embeddedResources = viewsAssembly.GetManifestResourceNames()
                    .Where(r => r.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var resource in embeddedResources)
                {
                    var viewPath = resource.Replace(".cshtml", "").Replace(".", "/");
                    var route = $"/p/{packageId.ToLowerInvariant()}/{ExtractModuleFromPath(viewPath)}/{ExtractPageFromPath(viewPath)}";
                    var contentPath = $"/_content/{packageName}/{viewPath}.cshtml";

                    views.Add(new PageDefinition(
                        route,
                        contentPath,
                        Array.Empty<string>(),
                        null,
                        null
                    ));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error discovering views in {viewsAssembly.FullName}");
        }

        _logger.LogInformation($"Discovered {views.Count} Razor view(s)");
        return views;
    }

    private string NormalizeRoute(string route)
    {
        // Ensure route starts with /
        if (!route.StartsWith("/"))
            route = "/" + route;

        // Remove trailing slashes
        route = route.TrimEnd('/');

        return route;
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
        // Extract module name from path like "Pages/Dhcp/Config"
        var parts = path.Split('/');
        if (parts.Length >= 2)
            return parts[1].ToLowerInvariant();
        return "default";
    }

    private string ExtractPageFromPath(string path)
    {
        // Extract page name from path like "Pages/Dhcp/Config"
        var parts = path.Split('/');
        if (parts.Length >= 3)
            return parts[2].ToLowerInvariant();
        return "index";
    }
}
```

---

## PackageViewRouter Implementation

### File: `src/Monolith.FireWall.WebUI/Services/PackageViewRouter.cs`

```csharp
using System.Text.Json;
using Monolith.FireWall.Common.Models;
using Monolith.FireWall.WebUI.Middleware;
using Microsoft.AspNetCore.Mvc;

namespace Monolith.FireWall.WebUI.Services;

/// <summary>
/// Routes requests to package Razor views
/// </summary>
public class PackageViewRouter
{
    private readonly CoreApiClient _coreClient;
    private readonly ILogger<PackageViewRouter> _logger;
    private Dictionary<string, PageDefinition>? _cachedPages;
    private DateTime _cacheTime = DateTime.MinValue;
    private readonly TimeSpan _cacheTimeout = TimeSpan.FromMinutes(5);

    public PackageViewRouter(CoreApiClient coreClient, ILogger<PackageViewRouter> logger)
    {
        _coreClient = coreClient;
        _logger = logger;
    }

    /// <summary>
    /// Get all page definitions from Core
    /// </summary>
    private async Task<Dictionary<string, PageDefinition>> GetPagesAsync()
    {
        // Check cache
        if (_cachedPages != null && DateTime.UtcNow - _cacheTime < _cacheTimeout)
        {
            return _cachedPages;
        }

        try
        {
            var request = new { action = "get-pages" };
            var requestJson = JsonSerializer.Serialize(request);
            var responseJson = await _coreClient.SendRequestAsync(requestJson);
            var response = JsonSerializer.Deserialize<ApiResponse>(responseJson);

            if (response?.Success == true && response.Data != null)
            {
                var dataJson = JsonSerializer.Serialize(response.Data);
                var pages = JsonSerializer.Deserialize<List<PageDefinition>>(dataJson);

                if (pages != null)
                {
                    _cachedPages = pages.ToDictionary(p => p.Route.ToLowerInvariant());
                    _cacheTime = DateTime.UtcNow;
                    return _cachedPages;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading pages from Core");
        }

        return _cachedPages ?? new Dictionary<string, PageDefinition>();
    }

    /// <summary>
    /// Get page definition by route
    /// </summary>
    public async Task<PageDefinition?> GetPageDefinitionAsync(string route)
    {
        var pages = await GetPagesAsync();
        return pages.TryGetValue(route.ToLowerInvariant(), out var page) ? page : null;
    }

    /// <summary>
    /// Check if user has permission to access page
    /// </summary>
    public bool HasPermission(PageDefinition page, UserContext? user)
    {
        if (user == null)
            return false;

        if (page.RequiredPermissions == null || page.RequiredPermissions.Length == 0)
            return true;

        return page.RequiredPermissions.Any(perm =>
            user.Permissions.Contains(perm) ||
            user.Permissions.Contains("*"));
    }

    /// <summary>
    /// Render a package Razor view
    /// </summary>
    public async Task<IResult> RenderViewAsync(
        string package,
        string module,
        string page,
        HttpContext httpContext,
        IWebHostEnvironment env)
    {
        var route = $"/p/{package}/{module}/{page}";
        var pageDef = await GetPageDefinitionAsync(route);

        if (pageDef == null)
        {
            _logger.LogWarning($"Page not found: {route}");
            return Results.NotFound();
        }

        // Check permissions
        var user = AuthenticationMiddleware.GetUser(httpContext);
        if (!HasPermission(pageDef, user))
        {
            _logger.LogWarning($"User {user?.Username} denied access to {route}");
            return Results.Forbid();
        }

        // Render Razor view
        // The view path should be in the format: /_content/{PackageName}/Pages/{Module}/{Page}.cshtml
        // ASP.NET Core will automatically resolve this from registered RCLs
        
        try
        {
            // Use View() result to render Razor view
            // The view path is stored in pageDef.AssetPath (which is the Razor view path)
            return Results.View(pageDef.AssetPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error rendering view: {pageDef.AssetPath}");
            return Results.Problem("Error rendering view");
        }
    }

    public void InvalidateCache()
    {
        _cachedPages = null;
        _cacheTime = DateTime.MinValue;
    }
}
```

---

## Updated PackageLoader

### File: `src/Monolith.FireWall.Core/Services/PackageLoader.cs` (Updated)

```csharp
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Monolith.FireWall.Common.Interfaces;
using Monolith.FireWall.Common.Models;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Services;

public class PackageLoader
{
    private readonly ILogger _logger;
    private readonly RazorViewDiscovery _viewDiscovery;
    private readonly Dictionary<string, AssemblyLoadContext> _contexts = new();

    public PackageLoader(ILogger logger, RazorViewDiscovery viewDiscovery)
    {
        _logger = logger;
        _viewDiscovery = viewDiscovery;
    }

    public async Task<PackageInfo> LoadPackageAsync(PackageDiscoveryInfo discoveryInfo)
    {
        _logger.LogInformation($"Loading package: {discoveryInfo.Manifest.Id}");

        // Load main assembly
        var mainAssembly = Assembly.LoadFrom(discoveryInfo.MainDllPath);
        _logger.LogInformation($"Main assembly loaded: {mainAssembly.FullName}");

        // Load Views assembly (if exists)
        Assembly? viewsAssembly = null;
        if (discoveryInfo.ViewsDllPath != null && File.Exists(discoveryInfo.ViewsDllPath))
        {
            viewsAssembly = Assembly.LoadFrom(discoveryInfo.ViewsDllPath);
            _logger.LogInformation($"Views assembly loaded: {viewsAssembly.FullName}");
        }

        // Find package definition
        var defType = mainAssembly.GetTypes()
            .FirstOrDefault(t => typeof(IMonolithPackageDefinition).IsAssignableFrom(t) && !t.IsInterface);

        if (defType == null)
        {
            throw new InvalidOperationException($"No IMonolithPackageDefinition found in {mainAssembly.FullName}");
        }

        var definition = (IMonolithPackageDefinition)Activator.CreateInstance(defType)!;
        _logger.LogInformation($"Package definition created: {definition.Name}");

        // Find package instance
        var pkgType = mainAssembly.GetTypes()
            .FirstOrDefault(t => typeof(IMonolithPackage).IsAssignableFrom(t) && !t.IsInterface);

        if (pkgType == null)
        {
            throw new InvalidOperationException($"No IMonolithPackage found in {mainAssembly.FullName}");
        }

        var package = (IMonolithPackage)Activator.CreateInstance(pkgType)!;

        // Discover Razor views if Views assembly exists
        List<PageDefinition> discoveredViews = new();
        if (viewsAssembly != null)
        {
            var packageName = ExtractPackageName(discoveryInfo.MainDllPath);
            discoveredViews = _viewDiscovery.DiscoverViews(viewsAssembly, definition.Id, packageName);
            _logger.LogInformation($"Discovered {discoveredViews.Count} Razor view(s)");
        }

        return new PackageInfo(definition, package, mainAssembly, viewsAssembly, discoveredViews);
    }

    private string ExtractPackageName(string dllPath)
    {
        // Extract package name from DLL path
        // e.g., "/opt/.../Monolith.Network.dll" -> "Monolith.Network"
        var fileName = Path.GetFileNameWithoutExtension(dllPath);
        return fileName;
    }

    public void UnloadPackage(string packageId)
    {
        if (_contexts.TryGetValue(packageId, out var context))
        {
            _logger.LogInformation($"Unloading package: {packageId}");
            context.Unload();
            _contexts.Remove(packageId);
        }
    }
}
```

---

## Updated PackageInfo Model

### File: `src/Monolith.FireWall.Core/Models/PackageInfo.cs`

```csharp
using System.Reflection;
using Monolith.FireWall.Common.Interfaces;
using Monolith.FireWall.Common.Models;

namespace Monolith.FireWall.Core.Models;

public class PackageInfo
{
    public IMonolithPackageDefinition Definition { get; }
    public IMonolithPackage Package { get; }
    public Assembly MainAssembly { get; }
    public Assembly? ViewsAssembly { get; }
    public List<PageDefinition> DiscoveredViews { get; }

    public PackageInfo(
        IMonolithPackageDefinition definition,
        IMonolithPackage package,
        Assembly mainAssembly,
        Assembly? viewsAssembly,
        List<PageDefinition> discoveredViews)
    {
        Definition = definition;
        Package = package;
        MainAssembly = mainAssembly;
        ViewsAssembly = viewsAssembly;
        DiscoveredViews = discoveredViews;
    }

    public bool HasRazorViews => ViewsAssembly != null && DiscoveredViews.Count > 0;
}
```

---

## WebUI Program.cs Integration

### File: `src/Monolith.FireWall.WebUI/Program.cs` (Updated sections)

```csharp
// Add PackageViewRouter service
builder.Services.AddSingleton<PackageViewRouter>();

// ... existing code ...

// Register package Razor views
var coreClient = builder.Services.BuildServiceProvider().GetRequiredService<CoreApiClient>();
var packagesResponse = await GetPackagesFromCore(coreClient);
foreach (var package in packagesResponse)
{
    if (package.HasRazorViews)
    {
        // Register Views assembly with ASP.NET Core
        var viewsAssemblyPath = package.ViewsAssemblyPath; // Get from Core
        if (File.Exists(viewsAssemblyPath))
        {
            var viewsAssembly = Assembly.LoadFrom(viewsAssemblyPath);
            builder.Services.Configure<RazorViewEngineOptions>(options =>
            {
                options.FileProviders.Add(
                    new Microsoft.Extensions.FileProviders.EmbeddedFileProvider(viewsAssembly)
                );
            });
        }
    }
}

// ... existing code ...

// Package page routing
app.MapGet("/p/{package}/{module}/{page}", async (
    string package,
    string module,
    string page,
    HttpContext httpContext,
    PackageViewRouter router,
    IWebHostEnvironment env) =>
{
    return await router.RenderViewAsync(package, module, page, httpContext, env);
});
```

---

## Next Steps

1. Implement PackageScanner in Core
2. Implement RazorViewDiscovery in Core
3. Update PackageLoader to use discovery
4. Implement PackageViewRouter in WebUI
5. Update WebUI Program.cs to register views
6. Test with a sample RCL package

---

**These implementations provide the foundation for dynamic package loading with Razor Class Libraries!**
