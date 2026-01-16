using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Monolith.FireWall.WebUI.Models;

namespace Monolith.FireWall.WebUI.Services;

/// <summary>
/// Simple page content renderer:
/// - Internal pages (firewall): Use normal ASP.NET Core routing via HTTP request
/// - Package pages: Use Razor Class Library rendering
/// </summary>
public class PageContentRenderer
{
    private readonly RazorPartialRenderer _razorRenderer;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PageContentRenderer> _logger;

    public PageContentRenderer(
        RazorPartialRenderer razorRenderer,
        IHttpClientFactory httpClientFactory,
        ILogger<PageContentRenderer> logger)
    {
        _razorRenderer = razorRenderer;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Renders a page by route and returns HTML with explicit asset lists
    /// </summary>
    public async Task<PageContent> RenderPageAsync(HttpContext httpContext, string route)
    {
        // Normalize route (ensure it starts with /)
        if (!route.StartsWith("/"))
        {
            route = "/" + route;
        }

        _logger.LogInformation("Rendering page for route: {Route}", route);

        string html = string.Empty;

        // PACKAGE PAGES: Use Razor Class Library rendering
        if (route.StartsWith("/p/"))
        {
            var parts = route.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            // Route format: /p/{package}/{module} or /p/{package}/{module}/{page}
            // After split: ["p", "package", "module"] or ["p", "package", "module", "page"]
            if (parts.Length < 3)
            {
                throw new FileNotFoundException($"Invalid package route format: {route}");
            }

            // Skip "p" at index 0
            var packageId = parts[1];
            var moduleId = parts[2];
            var pageId = parts.Length > 3 ? parts[3] : "config";

            _logger.LogInformation("Rendering package page: {PackageId}/{ModuleId}/{PageId}", packageId, moduleId, pageId);
            html = await _razorRenderer.RenderPackagePageAsync(httpContext, packageId, moduleId, pageId);
        }
        // INTERNAL PAGES (firewall, etc.): Use Razor Pages rendering directly (no HTTP request to avoid deadlock)
        else
        {
            _logger.LogInformation("Rendering internal page via Razor Pages: {Route}", route);
            html = await _razorRenderer.RenderPageAsync(httpContext, route);
        }

        if (string.IsNullOrWhiteSpace(html) || IsLoadingPage(html))
        {
            _logger.LogError("Failed to render page for route: {Route} - got empty or loading page HTML", route);
            throw new FileNotFoundException($"Page not found or failed to render: {route}");
        }

        // Extract content and assets from the HTML
        var content = ExtractPageContent(html);
        var assets = ExtractAssetsFromHtml(html);

        _logger.LogInformation("Successfully rendered route: {Route} ({ContentLength} chars, CSS: {CssCount}, JS: {JsCount})", 
            route, content.Length, assets.css.Count, assets.js.Count);

        return new PageContent
        {
            Html = content,
            CssAssets = assets.css,
            JsAssets = assets.js
        };
    }


    private string ExtractPageContent(string fullHtml)
    {
        _logger.LogDebug("Extracting page content from HTML ({Length} chars)", fullHtml.Length);

        // If HTML is already short and doesn't contain body/html tags, it's likely already extracted
        if (fullHtml.Length < 2000 && !fullHtml.Contains("<body", StringComparison.OrdinalIgnoreCase) && !fullHtml.Contains("<html", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("HTML appears to be already extracted content ({Length} chars)", fullHtml.Length);
            return fullHtml;
        }

        // Remove navigation
        var html = Regex.Replace(fullHtml, @"<nav[^>]*>.*?</nav>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        html = RemoveModuleAssetTags(html);

        // Try to find main content divs using balanced tag matching
        // Pattern 1: Find div with package-page class (match opening to closing tag)
        var packagePageStart = html.IndexOf("class=\"", StringComparison.OrdinalIgnoreCase);
        while (packagePageStart >= 0)
        {
            var classStart = html.IndexOf("class=\"", packagePageStart, StringComparison.OrdinalIgnoreCase);
            if (classStart < 0) break;
            
            var classEnd = html.IndexOf("\"", classStart + 7);
            if (classEnd < 0) break;
            
            var className = html.Substring(classStart + 7, classEnd - classStart - 7);
            if (className.Contains("package-page", StringComparison.OrdinalIgnoreCase))
            {
                // Find the opening div tag
                var divStart = html.LastIndexOf("<div", classStart);
                if (divStart >= 0)
                {
                    // Find matching closing tag using balanced matching
                    var extracted = ExtractBalancedTag(html, divStart);
                    if (!string.IsNullOrEmpty(extracted))
                    {
                        _logger.LogDebug("Extracted package-page div ({Length} chars)", extracted.Length);
                        return extracted;
                    }
                }
            }
            packagePageStart = html.IndexOf("class=\"", classEnd, StringComparison.OrdinalIgnoreCase);
        }

        // Pattern 2: Find container-fluid with p-4 class
        var containerStart = html.IndexOf("container-fluid", StringComparison.OrdinalIgnoreCase);
        if (containerStart >= 0)
        {
            var divStart = html.LastIndexOf("<div", containerStart);
            if (divStart >= 0)
            {
                var extracted = ExtractBalancedTag(html, divStart);
                if (!string.IsNullOrEmpty(extracted) && extracted.Contains("p-4", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("Extracted container-fluid div ({Length} chars)", extracted.Length);
                    return extracted;
                }
            }
        }

        // Pattern 3: Find div with id="page-content"
        var pageContentStart = html.IndexOf("id=\"page-content\"", StringComparison.OrdinalIgnoreCase);
        if (pageContentStart >= 0)
        {
            var divStart = html.LastIndexOf("<div", pageContentStart);
            if (divStart >= 0)
            {
                var extracted = ExtractBalancedTag(html, divStart);
                if (!string.IsNullOrEmpty(extracted))
                {
                    _logger.LogDebug("Extracted page-content div ({Length} chars)", extracted.Length);
                    return extracted;
                }
            }
        }

        // Fallback: return everything between body tags or just the HTML
        var bodyMatch = Regex.Match(html, @"<body[^>]*>(.*?)</body>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (bodyMatch.Success)
        {
            var bodyContent = bodyMatch.Groups[1].Value.Trim();
            _logger.LogDebug("Extracted body content ({Length} chars)", bodyContent.Length);
            return bodyContent;
        }

        // Last resort: return the HTML as-is (might already be just content)
        _logger.LogDebug("No pattern matched, returning HTML as-is ({Length} chars)", html.Length);
        return html;
    }

    private string ExtractBalancedTag(string html, int startIndex)
    {
        if (startIndex < 0 || startIndex >= html.Length) return string.Empty;

        // Find the opening tag end
        var tagEnd = html.IndexOf('>', startIndex);
        if (tagEnd < 0) return string.Empty;

        var tagName = "div";
        var tagStart = html.LastIndexOf('<', startIndex);
        if (tagStart >= 0)
        {
            var spaceIndex = html.IndexOfAny(new[] { ' ', '>', '\t', '\n' }, tagStart + 1);
            if (spaceIndex > tagStart + 1)
            {
                tagName = html.Substring(tagStart + 1, spaceIndex - tagStart - 1).ToLowerInvariant();
            }
        }

        var depth = 1;
        var pos = tagEnd + 1;
        var openTag = $"<{tagName}";
        var closeTag = $"</{tagName}>";

        while (pos < html.Length && depth > 0)
        {
            var nextOpen = html.IndexOf(openTag, pos, StringComparison.OrdinalIgnoreCase);
            var nextClose = html.IndexOf(closeTag, pos, StringComparison.OrdinalIgnoreCase);

            if (nextClose < 0) break;

            if (nextOpen >= 0 && nextOpen < nextClose)
            {
                depth++;
                pos = html.IndexOf('>', nextOpen) + 1;
            }
            else
            {
                depth--;
                if (depth == 0)
                {
                    return html.Substring(startIndex, nextClose + closeTag.Length - startIndex);
                }
                pos = nextClose + closeTag.Length;
            }
        }

        return string.Empty;
    }

    private (List<string> css, List<string> js) ExtractAssetsFromHtml(string html)
    {
        var css = new List<string>();
        var js = new List<string>();

        // Extract CSS links with data-module-css
        var cssMatches = Regex.Matches(html, @"<link[^>]*data-module-css[^>]*href=""([^""]+)""[^>]*>", RegexOptions.IgnoreCase);
        foreach (Match match in cssMatches)
        {
            var href = match.Groups[1].Value;
            if (!string.IsNullOrEmpty(href) && !css.Contains(href))
            {
                css.Add(href);
            }
        }

        // Extract JS scripts with data-module-js
        var jsMatches = Regex.Matches(html, @"<script[^>]*data-module-js[^>]*src=""([^""]+)""[^>]*>", RegexOptions.IgnoreCase);
        foreach (Match match in jsMatches)
        {
            var src = match.Groups[1].Value;
            if (!string.IsNullOrEmpty(src) && !js.Contains(src))
            {
                js.Add(src);
            }
        }

        return (css, js);
    }

    private static string RemoveModuleAssetTags(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html;
        }

        html = Regex.Replace(html, @"<link[^>]*data-module-css[^>]*>", "", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<script[^>]*data-module-js[^>]*>(.*?)</script>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        return html;
    }

    private bool IsLoadingPage(string html)
    {
        return html.Contains("Loading...") && html.Contains("Initializing application...");
    }
}
