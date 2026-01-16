using System.Linq;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Matching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;

namespace Monolith.FireWall.WebUI.Services;

/// <summary>
/// Renders Razor pages as HTML partials (without layout) for SPA consumption
/// </summary>
public class RazorPartialRenderer
{
    private readonly IRazorViewEngine _viewEngine;
    private readonly ITempDataProvider _tempDataProvider;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RazorPartialRenderer> _logger;
    private readonly IPageFactoryProvider _pageFactoryProvider;
    private static bool _loggedRoutes = false;

    public RazorPartialRenderer(
        IRazorViewEngine viewEngine,
        ITempDataProvider tempDataProvider,
        IServiceProvider serviceProvider,
        ILogger<RazorPartialRenderer> logger,
        IPageFactoryProvider pageFactoryProvider)
    {
        _viewEngine = viewEngine;
        _tempDataProvider = tempDataProvider;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _pageFactoryProvider = pageFactoryProvider;
    }

    /// <summary>
    /// Renders a Razor page to HTML string without layout
    /// Uses the page route system since pages have @page directives
    /// </summary>
    /// <param name="httpContext">Current HTTP context</param>
    /// <param name="pageRoute">Route to the page (e.g., "/firewall/aliases" or "/Pages/Firewall/Aliases/Config")</param>
    /// <param name="model">Optional model to pass to the page</param>
    /// <returns>Rendered HTML string</returns>
    public async Task<string> RenderPageAsync(HttpContext httpContext, string pageRoute, object? model = null)
    {
        try
        {
            // If it's already a route (starts with /), use it directly
            if (pageRoute.StartsWith("/"))
            {
                return await RenderPageByRouteAsync(httpContext, pageRoute);
            }
            
            // If not a route, it's an invalid page route
            _logger.LogWarning($"Invalid page route format: {pageRoute}. Routes must start with '/'");
            throw new ArgumentException($"Invalid page route: {pageRoute}. Routes must start with '/'");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error rendering page: {pageRoute}");
            throw;
        }
    }

    /// <summary>
    /// Renders a Razor page from a package (Razor Pages are embedded in main DLL)
    /// Package pages are Razor Pages with @page directives like: @page "/p/monolith-network/dhcp/config"
    /// We need to use the view engine with the correct view path format for RCL embedded views
    /// </summary>
    /// <param name="httpContext">Current HTTP context</param>
    /// <param name="packageId">Package ID (e.g., "monolith-network")</param>
    /// <param name="moduleId">Module ID (e.g., "dhcp")</param>
    /// <param name="pageId">Page ID (e.g., "config")</param>
    /// <returns>Rendered HTML string</returns>
    public async Task<string> RenderPackagePageAsync(
        HttpContext httpContext, 
        string packageId, 
        string moduleId, 
        string pageId)
    {
        // Package pages are Razor Pages with @page directives
        // The compiled class name is: AspNetCoreGeneratedDocument.Pages_{Module}_{Page}
        // Example: AspNetCoreGeneratedDocument.Pages_Dhcp_Config
        var assemblyName = ToAssemblyName(packageId);
        var modulePascal = ToPascalCase(moduleId);
        var pagePascal = ToPascalCase(pageId);
        
        // Try to find and instantiate the compiled Razor Page class directly
        var compiledClassName = $"AspNetCoreGeneratedDocument.Pages_{modulePascal}_{pagePascal}";
        
        _logger.LogDebug($"Trying to load compiled Razor Page class: {compiledClassName} from assembly {assemblyName}");
        
        try
        {
            // First try to get assembly from ApplicationPartManager
            var partManager = httpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Mvc.ApplicationParts.ApplicationPartManager>();
            var assemblyPart = partManager.ApplicationParts
                .OfType<Microsoft.AspNetCore.Mvc.ApplicationParts.AssemblyPart>()
                .FirstOrDefault(ap => ap.Assembly.GetName().Name == assemblyName);
            
            Assembly? assembly = null;
            
            if (assemblyPart != null)
            {
                assembly = assemblyPart.Assembly;
                _logger.LogDebug($"Found assembly {assemblyName} in ApplicationPartManager");
            }
            else
            {
                // Fallback: Load assembly directly from file path via Core API
                _logger.LogDebug($"Assembly {assemblyName} not in ApplicationPartManager, trying to load from file path");
                
                var coreClient = httpContext.RequestServices.GetRequiredService<CoreApiClient>();
                var request = JsonSerializer.Serialize(new { action = "get-packages" });
                var responseJson = await coreClient.SendRequestAsync(request);
                var response = JsonSerializer.Deserialize<JsonElement>(responseJson);
                
                if (response.TryGetProperty("Success", out var success) && success.GetBoolean())
                {
                    if (response.TryGetProperty("Data", out var data))
                    {
                        var packages = JsonSerializer.Deserialize<List<JsonElement>>(data.GetRawText()) ?? new List<JsonElement>();
                        var package = packages.FirstOrDefault(p => 
                            p.TryGetProperty("id", out var id) && 
                            id.GetString()?.Equals(packageId, StringComparison.OrdinalIgnoreCase) == true);
                        
                        if (package.TryGetProperty("viewsAssemblyPath", out var pathEl))
                        {
                            var assemblyPath = pathEl.GetString();
                            if (!string.IsNullOrEmpty(assemblyPath) && File.Exists(assemblyPath))
                            {
                                assembly = Assembly.LoadFrom(assemblyPath);
                                _logger.LogDebug($"Loaded assembly {assemblyName} directly from {assemblyPath}");
                            }
                        }
                    }
                }
            }
            
            if (assembly != null)
            {
                _logger.LogDebug($"Looking for type: {compiledClassName} in assembly {assemblyName}");
                
                // Try to find the type - it might be in a different namespace
                var pageType = assembly.GetType(compiledClassName, throwOnError: false);
                
                // If not found, try searching all types
                if (pageType == null)
                {
                    try
                    {
                        var allTypes = assembly.GetTypes();
                        pageType = allTypes.FirstOrDefault(t => 
                            t.Name.Contains($"Pages_{modulePascal}_{pagePascal}") && 
                            typeof(PageBase).IsAssignableFrom(t));
                        
                        if (pageType != null)
                        {
                            _logger.LogDebug($"Found Razor Page type by search: {pageType.FullName}");
                        }
                        else
                        {
                            // Log available page types for debugging
                            var pageTypes = allTypes.Where(t => t.Name.Contains("Pages_")).Select(t => t.Name).Take(10).ToList();
                            _logger.LogDebug($"Available page types in {assemblyName}: {string.Join(", ", pageTypes)}");
                        }
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        _logger.LogWarning($"Error loading types from {assemblyName}: {ex.Message}");
                        // Try to get types that did load
                        var loadedTypes = ex.Types.Where(t => t != null).ToList();
                        pageType = loadedTypes.FirstOrDefault(t => 
                            t.Name.Contains($"Pages_{modulePascal}_{pagePascal}") && 
                            typeof(PageBase).IsAssignableFrom(t));
                    }
                }
                
                if (pageType != null && typeof(PageBase).IsAssignableFrom(pageType))
                {
                    _logger.LogInformation($"Found compiled Razor Page class: {pageType.FullName}");
                    
                    // Create page instance
                    var page = (PageBase)Activator.CreateInstance(pageType)!;
                    
                    // Create page context - let the page create its own ViewData if it has a strongly-typed model
                    var pageContext = new PageContext(new ActionContext(
                        httpContext,
                        httpContext.GetRouteData(),
                        new ActionDescriptor()))
                    {
                        HttpContext = httpContext
                    };
                    
                    // Try to create ViewData using the page's ViewData property type
                    try
                    {
                        // Get the ViewData property from the page to see its type
                        var viewDataProperty = pageType.GetProperty("ViewData", BindingFlags.Public | BindingFlags.Instance);
                        if (viewDataProperty != null && viewDataProperty.PropertyType.IsGenericType)
                        {
                            // Page has strongly-typed ViewData, create it using reflection
                            var viewDataType = viewDataProperty.PropertyType;
                            var viewData = Activator.CreateInstance(viewDataType, 
                                new EmptyModelMetadataProvider(), 
                                new ModelStateDictionary()) as ViewDataDictionary;
                            pageContext.ViewData = viewData ?? new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary());
                        }
                        else
                        {
                            // Use standard ViewDataDictionary
                            pageContext.ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary());
                        }
                    }
                    catch
                    {
                        // Fallback to standard ViewDataDictionary
                        pageContext.ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary());
                    }
                    
                    page.PageContext = pageContext;
                    
                    // Create view context and render
                    using var sw = new StringWriter();
                    page.ViewContext = new ViewContext(
                        pageContext,
                        new EmptyView(),
                        pageContext.ViewData,
                        new TempDataDictionary(httpContext, _tempDataProvider),
                        sw,
                        new HtmlHelperOptions());
                    
                    await page.ExecuteAsync();
                    return sw.ToString();
                }
                else
                {
                    _logger.LogWarning($"Compiled Razor Page class not found: {compiledClassName} in assembly {assemblyName}");
                }
            }
            else
            {
                _logger.LogWarning($"Could not load assembly {assemblyName} from ApplicationPartManager or file path");
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug($"Failed to load compiled Razor Page class: {ex.Message}");
        }
        
        // Fallback: Try Config page if pageId is not "config"
        if (!string.Equals(pageId, "config", StringComparison.OrdinalIgnoreCase))
        {
            var configClassName = $"AspNetCoreGeneratedDocument.Pages_{modulePascal}_Config";
            _logger.LogDebug($"Trying Config fallback: {configClassName}");
            
            try
            {
                var partManager = httpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Mvc.ApplicationParts.ApplicationPartManager>();
                var assemblyPart = partManager.ApplicationParts
                    .OfType<Microsoft.AspNetCore.Mvc.ApplicationParts.AssemblyPart>()
                    .FirstOrDefault(ap => ap.Assembly.GetName().Name == assemblyName);
                
                if (assemblyPart != null)
                {
                    var assembly = assemblyPart.Assembly;
                    var pageType = assembly.GetType(configClassName, throwOnError: false);
                    
                    if (pageType != null && typeof(PageBase).IsAssignableFrom(pageType))
                    {
                        var page = (PageBase)Activator.CreateInstance(pageType)!;
                        var pageContext = new PageContext(new ActionContext(
                            httpContext,
                            httpContext.GetRouteData(),
                            new ActionDescriptor()))
                        {
                            ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()),
                            HttpContext = httpContext
                        };
                        
                        page.PageContext = pageContext;
                        using var sw = new StringWriter();
                        page.ViewContext = new ViewContext(
                            pageContext,
                            new EmptyView(),
                            pageContext.ViewData,
                            new TempDataDictionary(httpContext, _tempDataProvider),
                            sw,
                            new HtmlHelperOptions());
                        
                        await page.ExecuteAsync();
                        return sw.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"Failed to load Config Razor Page: {ex.Message}");
            }
        }
        
        throw new FileNotFoundException(
            $"Package page not found: {packageId}/{moduleId}/{pageId}. " +
            $"Tried compiled Razor Page classes: AspNetCoreGeneratedDocument.Pages_{modulePascal}_{pagePascal}, " +
            $"AspNetCoreGeneratedDocument.Pages_{modulePascal}_Config. " +
            $"Assembly {assemblyName} may not be registered or pages not compiled correctly.");
    }
    
    // Helper class for empty view
    private class EmptyView : IView
    {
        public string Path => "";
        public Task RenderAsync(ViewContext context) => Task.CompletedTask;
    }

    /// <summary>
    /// Converts package ID to assembly name
    /// monolith-network -> Monolith.Network
    /// monolith-vpn -> Monolith.Vpn
    /// </summary>
    private static string ToAssemblyName(string packageId)
    {
        if (string.IsNullOrEmpty(packageId))
            return packageId;

        // Split by hyphen and capitalize each part
        var parts = packageId.Split('-', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(".", parts.Select(part => 
            part.Length > 0 
                ? char.ToUpper(part[0]) + part.Substring(1).ToLower() 
                : part));
    }

    /// <summary>
    /// Renders a Razor page by its route (for pages with @page directives)
    /// Makes an internal HTTP request to the route to get the rendered page
    /// </summary>
    private async Task<string> RenderPageByRouteAsync(HttpContext httpContext, string route)
    {
        try
        {
            // Try direct endpoint execution first (faster, no network overhead)
            // Save original state
            var originalPath = httpContext.Request.Path;
            var originalQuery = httpContext.Request.QueryString;
            var originalResponseBody = httpContext.Response.Body;
            var originalStatusCode = httpContext.Response.StatusCode;
            var originalContentType = httpContext.Response.ContentType;
            var originalEndpoint = httpContext.GetEndpoint();
            
            // Create a memory stream to capture the response
            using var ms = new MemoryStream();
            httpContext.Response.Body = ms;
            httpContext.Response.StatusCode = 200;
            httpContext.Response.ContentType = "text/html";
            
            try
            {
                // Set the route path
                httpContext.Request.Path = route;
                httpContext.Request.QueryString = QueryString.Empty;
                httpContext.SetEndpoint(null); // Clear endpoint to force re-matching
                
                // Get the endpoint data source
                var endpointDataSource = httpContext.RequestServices.GetRequiredService<EndpointDataSource>();
                var endpoints = endpointDataSource.Endpoints;
                
                // Log all routes for debugging (first time only)
                if (!_loggedRoutes)
                {
                    var allRoutePatterns = endpoints
                        .OfType<RouteEndpoint>()
                        .Select(e => e.RoutePattern.RawText ?? "unknown")
                        .Where(r => !string.IsNullOrEmpty(r))
                        .OrderBy(r => r)
                        .ToList();
                    var packageRoutes = allRoutePatterns.Where(r => r.Contains("/p/")).ToList();
                    _logger.LogInformation($"Total registered routes: {allRoutePatterns.Count}. Package routes ({packageRoutes.Count}): {string.Join(", ", packageRoutes.Take(20))}");
                    _loggedRoutes = true;
                }
                
                // Log package routes when trying to match
                if (route.StartsWith("/p/"))
                {
                    var packageEndpoints = endpoints
                        .OfType<RouteEndpoint>()
                        .Where(ep => IsRazorPageEndpoint(ep))
                        .Where(ep => (ep.RoutePattern.RawText ?? "").Contains("/p/"))
                        .Select(ep => ep.RoutePattern.RawText ?? "unknown")
                        .ToList();
                    _logger.LogDebug($"Trying to match route {route}. Available package endpoints: {string.Join(", ", packageEndpoints)}");
                }
                
                // Find matching endpoint - try exact match first
                // IMPORTANT: Only match Razor Pages endpoints, skip catch-all routes
                RouteEndpoint? matchedEndpoint = null;
                var routeWithoutLeadingSlash = route.TrimStart('/');
                
                // Helper to check if endpoint is a Razor Page (not a catch-all)
                bool IsRazorPageEndpoint(RouteEndpoint ep)
                {
                    // Skip catch-all routes (like {**path})
                    var pattern = ep.RoutePattern.RawText ?? "";
                    // Skip empty/root patterns unless we're rendering the root route itself
                    if (string.IsNullOrWhiteSpace(pattern) || pattern == "/")
                    {
                        return string.Equals(route, "/", StringComparison.Ordinal);
                    }
                    if (pattern.Contains("{**") || pattern.Contains("**"))
                    {
                        return false;
                    }
                    
                    // Skip index.html or fallback routes
                    if (pattern.Contains("index", StringComparison.OrdinalIgnoreCase) && 
                        !pattern.Contains("/p/") && !pattern.Contains("firewall"))
                    {
                        return false;
                    }
                    
                    // For package routes, prefer Razor Pages over minimal API routes
                    // Razor Pages have patterns like /p/{package}/{module}/{page?}
                    // Minimal API routes have patterns like /p/{package}/{module} (without optional params)
                    if (route.StartsWith("/p/"))
                    {
                        // Prefer Razor Pages (they have optional parameters like {page?})
                        if (pattern.Contains("{page?}") || pattern.Contains("{page}?"))
                        {
                            return true;
                        }
                        // Also accept minimal API routes as fallback
                        if (pattern.StartsWith("/p/", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                        return false;
                    }
                    
                    // Prefer Razor Pages (they usually have /Pages/ or firewall/)
                    if (pattern.Contains("/Pages/") || pattern.Contains("firewall", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                    
                    return true;
                }
                
                // First pass: exact matches (Razor Pages only)
                foreach (var ep in endpoints)
                {
                    if (ep is RouteEndpoint routeEndpoint && IsRazorPageEndpoint(routeEndpoint))
                    {
                        var pattern = routeEndpoint.RoutePattern;
                        var patternText = pattern.RawText ?? "";
                        var patternWithoutSlash = patternText.TrimStart('/');
                        
                        // Exact match
                        if (patternText == route || patternWithoutSlash == routeWithoutLeadingSlash)
                        {
                            matchedEndpoint = routeEndpoint;
                            _logger.LogInformation($"Found exact route match: {route} -> {patternText}");
                            break;
                        }
                    }
                }
                
                // Second pass: check if route ends with pattern (for Razor Pages)
                if (matchedEndpoint == null)
                {
                    foreach (var ep in endpoints)
                    {
                        if (ep is RouteEndpoint routeEndpoint && IsRazorPageEndpoint(routeEndpoint))
                        {
                            var pattern = routeEndpoint.RoutePattern;
                            var patternText = pattern.RawText ?? "";
                            var patternWithoutSlash = patternText.TrimStart('/');
                            
                            // Check if route ends with pattern (common for Razor Pages)
                            if (!string.IsNullOrEmpty(patternText) &&
                                !string.IsNullOrEmpty(patternWithoutSlash) &&
                                (route.EndsWith(patternWithoutSlash, StringComparison.OrdinalIgnoreCase) ||
                                 routeWithoutLeadingSlash.EndsWith(patternWithoutSlash, StringComparison.OrdinalIgnoreCase)))
                            {
                                matchedEndpoint = routeEndpoint;
                                _logger.LogInformation($"Found route match (ends with): {route} -> {patternText}");
                                break;
                            }
                        }
                    }
                }
                
                // Third pass: match parameterized routes (e.g. /p/{package}/{module}/{page})
                // Prioritize Razor Pages (with optional params) over minimal API routes
                if (matchedEndpoint == null)
                {
                    // First, try Razor Pages (they have optional parameters)
                    var razorPageEndpoints = endpoints
                        .OfType<RouteEndpoint>()
                        .Where(ep => IsRazorPageEndpoint(ep))
                        .Where(ep => 
                        {
                            var pattern = ep.RoutePattern.RawText ?? "";
                            return pattern.Contains("{page?}") || pattern.Contains("{page}?");
                        })
                        .ToList();
                    
                    foreach (var routeEndpoint in razorPageEndpoints)
                    {
                        var patternText = routeEndpoint.RoutePattern.RawText ?? "";
                        if (string.IsNullOrEmpty(patternText) || patternText == "/")
                        {
                            continue;
                        }

                        if (RoutePatternMatches(patternText, route))
                        {
                            matchedEndpoint = routeEndpoint;
                            _logger.LogInformation($"Found Razor Page route match (pattern): {route} -> {patternText}");
                            break;
                        }
                    }
                    
                    // If no Razor Page found, try minimal API routes as fallback
                    if (matchedEndpoint == null)
                    {
                        foreach (var ep in endpoints)
                        {
                            if (ep is RouteEndpoint routeEndpoint && IsRazorPageEndpoint(routeEndpoint))
                            {
                                var patternText = routeEndpoint.RoutePattern.RawText ?? "";
                                if (string.IsNullOrEmpty(patternText) || patternText == "/")
                                {
                                    continue;
                                }

                                // Skip if we already tried this (it's a Razor Page)
                                if (patternText.Contains("{page?}") || patternText.Contains("{page}?"))
                                {
                                    continue;
                                }

                                if (RoutePatternMatches(patternText, route))
                                {
                                    matchedEndpoint = routeEndpoint;
                                    _logger.LogInformation($"Found minimal API route match (pattern): {route} -> {patternText}");
                                    break;
                                }
                            }
                        }
                    }
                }
                
                if (matchedEndpoint != null && matchedEndpoint.RequestDelegate != null)
                {
                    _logger.LogInformation($"Executing endpoint for route: {route}");
                    // Set the endpoint
                    httpContext.SetEndpoint(matchedEndpoint);
                    ApplyRouteValues(httpContext, matchedEndpoint.RoutePattern.RawText ?? string.Empty, route);
                    
                    // Execute the endpoint
                    await matchedEndpoint.RequestDelegate(httpContext);
                    
                    // Check if response was successful
                    if (httpContext.Response.StatusCode == 200)
                    {
                        ms.Position = 0;
                        using var reader = new StreamReader(ms);
                        var fullHtml = await reader.ReadToEndAsync();
                        _logger.LogInformation($"Endpoint returned HTML ({fullHtml.Length} chars). First 200 chars: {fullHtml.Substring(0, Math.Min(200, fullHtml.Length))}");
                        
                        // Check if we got the default "Loading..." HTML
                        if (fullHtml.Contains("Loading...") && fullHtml.Contains("Initializing application"))
                        {
                            _logger.LogWarning($"Endpoint returned default 'Loading...' HTML instead of page content for route: {route}");
                            throw new FileNotFoundException($"Page route returned default HTML instead of page content: {route}");
                        }
                        
                        // Check if we got the default "Loading..." HTML (indicates route didn't match or wrong endpoint)
                        if (fullHtml.Contains("Loading...") && fullHtml.Contains("Initializing application") && fullHtml.Length < 1000)
                        {
                            _logger.LogError($"Endpoint returned default 'Loading...' HTML instead of page content for route: {route}. This suggests the route didn't match or the wrong endpoint was executed.");
                            throw new FileNotFoundException($"Page route returned default HTML instead of page content: {route}. Route may not be registered correctly.");
                        }
                        
                        // Extract just the page content (Razor Pages with Layout = null should only have their content)
                        // But if we got full HTML, extract the body content
                        var extractedHtml = ExtractPageContent(fullHtml);
                        
                        // Verify we got actual content, not just the loading message
                        if (extractedHtml.Contains("Loading...") && extractedHtml.Contains("Initializing application") && extractedHtml.Length < 1000)
                        {
                            _logger.LogError($"Extracted content is still the default 'Loading...' HTML for route: {route}");
                            throw new FileNotFoundException($"Page content extraction failed - still getting default HTML for route: {route}");
                        }
                        
                        _logger.LogInformation($"Extracted page content ({extractedHtml.Length} chars)");
                        return extractedHtml;
                    }
                    else
                    {
                        ms.Position = 0;
                        using var reader = new StreamReader(ms);
                        var errorContent = await reader.ReadToEndAsync();
                        _logger.LogWarning($"Page returned status {httpContext.Response.StatusCode} for route {route}. Content: {errorContent.Substring(0, Math.Min(200, errorContent.Length))}");
                        throw new FileNotFoundException($"Page returned status {httpContext.Response.StatusCode}: {route}");
                    }
                }
                
                // Log available routes for debugging
                var allRoutes = endpoints
                    .OfType<RouteEndpoint>()
                    .Select(e => e.RoutePattern.RawText ?? "unknown")
                    .Where(r => !string.IsNullOrEmpty(r))
                    .ToList();
                
                var relevantRoutes = allRoutes
                    .Where(r => r.Contains("firewall", StringComparison.OrdinalIgnoreCase) || 
                               r.Contains("/p/", StringComparison.OrdinalIgnoreCase))
                    .Take(30)
                    .ToList();
                
                _logger.LogWarning($"Page route not found: {route}");
                _logger.LogWarning($"Relevant routes found: {string.Join(", ", relevantRoutes)}");
                _logger.LogWarning($"Total routes available: {allRoutes.Count}");
                
                // Check if route exists but with different casing
                var routeLower = route.ToLowerInvariant();
                var matchingRoute = allRoutes.FirstOrDefault(r => r.ToLowerInvariant() == routeLower);
                if (matchingRoute != null)
                {
                    _logger.LogWarning($"Found route with different casing: {matchingRoute} (requested: {route})");
                    throw new FileNotFoundException($"Page route not found: {route} (found similar route with different casing: {matchingRoute})");
                }
                
                throw new FileNotFoundException($"Page route not found: {route}");
            }
            finally
            {
                // Restore original state
                httpContext.Request.Path = originalPath;
                httpContext.Request.QueryString = originalQuery;
                httpContext.Response.Body = originalResponseBody;
                httpContext.Response.StatusCode = originalStatusCode;
                httpContext.Response.ContentType = originalContentType;
                httpContext.SetEndpoint(originalEndpoint);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error rendering page by route: {route}");
            throw;
        }
    }

    private static void ApplyRouteValues(HttpContext httpContext, string pattern, string route)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return;
        }

        var patternSegments = pattern.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var routeSegments = route.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        
        // Check if pattern has optional parameters at the end
        var hasOptionalAtEnd = patternSegments.Length > 0 && 
                              patternSegments[patternSegments.Length - 1].StartsWith("{", StringComparison.Ordinal) &&
                              patternSegments[patternSegments.Length - 1].EndsWith("?}", StringComparison.Ordinal);
        
        // Route must have at least the required segments (all but optional ones)
        // and at most all segments (including optional)
        var minRequired = hasOptionalAtEnd ? patternSegments.Length - 1 : patternSegments.Length;
        if (routeSegments.Length < minRequired || routeSegments.Length > patternSegments.Length)
        {
            return;
        }

        var routeValues = new RouteValueDictionary();
        var segmentsToMatch = Math.Min(patternSegments.Length, routeSegments.Length);
        for (var i = 0; i < segmentsToMatch; i++)
        {
            var patternSegment = patternSegments[i];
            if (patternSegment.StartsWith("{", StringComparison.Ordinal) &&
                (patternSegment.EndsWith("}", StringComparison.Ordinal) || 
                 patternSegment.EndsWith("?}", StringComparison.Ordinal)))
            {
                var token = patternSegment.Trim('{', '}', '?');
                var name = token.Split(new[] { ':' }, 2)[0];
                if (!string.IsNullOrWhiteSpace(name) && i < routeSegments.Length)
                {
                    routeValues[name] = routeSegments[i];
                }
            }
        }

        if (routeValues.Count > 0)
        {
            httpContext.Request.RouteValues = routeValues;
        }
    }

    /// <summary>
    /// Extracts just the page content from full HTML
    /// Removes DOCTYPE, html, head, body tags and navigation
    /// </summary>
    private string ExtractPageContent(string fullHtml)
    {
        try
        {
            // If HTML doesn't start with DOCTYPE or html tag, it's likely already just the content
            var trimmed = fullHtml.TrimStart();
            if (!trimmed.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) &&
                !trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase))
            {
                return fullHtml;
            }
            
            // Extract body content
            var bodyStart = fullHtml.IndexOf("<body", StringComparison.OrdinalIgnoreCase);
            if (bodyStart < 0)
            {
                // No body tag, return as-is
                return fullHtml;
            }
            
            var bodyTagEnd = fullHtml.IndexOf('>', bodyStart) + 1;
            var bodyEnd = fullHtml.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
            if (bodyEnd <= bodyTagEnd)
            {
                return fullHtml;
            }
            
            var bodyContent = fullHtml.Substring(bodyTagEnd, bodyEnd - bodyTagEnd);
            
            // Remove navigation bar if present (it's in the SPA already)
            var navStart = bodyContent.IndexOf("<nav", StringComparison.OrdinalIgnoreCase);
            while (navStart >= 0)
            {
                var navEnd = bodyContent.IndexOf("</nav>", navStart, StringComparison.OrdinalIgnoreCase);
                if (navEnd >= 0)
                {
                    bodyContent = bodyContent.Remove(navStart, navEnd + 6 - navStart);
                    navStart = bodyContent.IndexOf("<nav", StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    break;
                }
            }
            
            // Look for main content div - find opening tag first
            var contentDivPatterns = new[]
            {
                @"<div[^>]*class\s*=\s*[""'][^""']*package-page[^""']*[""'][^>]*>",
                @"<div[^>]*class\s*=\s*[""'][^""']*container-fluid[^""']*p-4[^""']*[""'][^>]*>",
                @"<div[^>]*id\s*=\s*[""']page-content[""'][^>]*>"
            };
            
            foreach (var pattern in contentDivPatterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(bodyContent, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var startPos = match.Index;
                    var tagEnd = bodyContent.IndexOf('>', startPos) + 1;
                    
                    // Find matching closing div by counting depth
                    var depth = 1;
                    var pos = tagEnd;
                    var endPos = -1;
                    
                    while (depth > 0 && pos < bodyContent.Length)
                    {
                        var nextOpen = bodyContent.IndexOf("<div", pos, StringComparison.OrdinalIgnoreCase);
                        var nextClose = bodyContent.IndexOf("</div>", pos, StringComparison.OrdinalIgnoreCase);
                        
                        if (nextClose < 0) break;
                        
                        if (nextOpen >= 0 && nextOpen < nextClose)
                        {
                            depth++;
                            pos = nextOpen + 4;
                        }
                        else
                        {
                            depth--;
                            if (depth == 0)
                            {
                                endPos = nextClose + 6;
                                break;
                            }
                            pos = nextClose + 6;
                        }
                    }
                    
                    if (endPos > startPos)
                    {
                        return bodyContent.Substring(startPos, endPos - startPos);
                    }
                }
            }
            
            // If no specific content div found, look for first container-fluid div
            var containerMatch = System.Text.RegularExpressions.Regex.Match(
                bodyContent,
                @"<div[^>]*class\s*=\s*[""'][^""']*container-fluid[^""']*[""'][^>]*>",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            if (containerMatch.Success)
            {
                var startPos = containerMatch.Index;
                var tagEnd = bodyContent.IndexOf('>', startPos) + 1;
                var depth = 1;
                var pos = tagEnd;
                var endPos = -1;
                
                while (depth > 0 && pos < bodyContent.Length)
                {
                    var nextOpen = bodyContent.IndexOf("<div", pos, StringComparison.OrdinalIgnoreCase);
                    var nextClose = bodyContent.IndexOf("</div>", pos, StringComparison.OrdinalIgnoreCase);
                    
                    if (nextClose < 0) break;
                    
                    if (nextOpen >= 0 && nextOpen < nextClose)
                    {
                        depth++;
                        pos = nextOpen + 4;
                    }
                    else
                    {
                        depth--;
                        if (depth == 0)
                        {
                            endPos = nextClose + 6;
                            break;
                        }
                        pos = nextClose + 6;
                    }
                }
                
                if (endPos > startPos)
                {
                    return bodyContent.Substring(startPos, endPos - startPos);
                }
            }
            
            // Fallback: return body content without nav
            return bodyContent.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Error extracting page content: {ex.Message}. Returning full HTML.");
            return fullHtml;
        }
    }

    private static string ToPascalCase(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return string.Join("", value.Split('-', '_')
            .Select(part => part.Length > 0 
                ? char.ToUpper(part[0]) + part.Substring(1).ToLower() 
                : part));
    }

    private static bool RoutePatternMatches(string pattern, string route)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        var patternSegments = pattern.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var routeSegments = route.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        // Check if pattern has optional parameters at the end
        var hasOptionalAtEnd = patternSegments.Length > 0 && 
                              patternSegments[patternSegments.Length - 1].StartsWith("{", StringComparison.Ordinal) &&
                              patternSegments[patternSegments.Length - 1].EndsWith("?}", StringComparison.Ordinal);

        // Route must have at least the required segments (all but optional ones)
        // and at most all segments (including optional)
        var minRequired = hasOptionalAtEnd ? patternSegments.Length - 1 : patternSegments.Length;
        if (routeSegments.Length < minRequired || routeSegments.Length > patternSegments.Length)
        {
            return false;
        }

        // Match segments up to the length of the route
        var segmentsToMatch = Math.Min(patternSegments.Length, routeSegments.Length);
        for (var i = 0; i < segmentsToMatch; i++)
        {
            var patternSegment = patternSegments[i];
            var routeSegment = routeSegments[i];

            // Skip parameter segments (both required and optional)
            if (patternSegment.StartsWith("{", StringComparison.Ordinal) &&
                (patternSegment.EndsWith("}", StringComparison.Ordinal) || 
                 patternSegment.EndsWith("?}", StringComparison.Ordinal)))
            {
                continue;
            }

            // For literal segments, they must match
            if (!string.Equals(patternSegment, routeSegment, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
