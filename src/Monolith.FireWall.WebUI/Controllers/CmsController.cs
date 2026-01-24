using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Linq;
using System.Text.Json;
using Monolith.FireWall.WebUI.Models;
using Monolith.FireWall.WebUI.Services;

namespace Monolith.FireWall.WebUI.Controllers;

[ApiController]
[Route("api/cms")]
public sealed class CmsController : ControllerBase
{
    private readonly UiManifestBuilder _manifestBuilder;
    private readonly PageContentRenderer _pageRenderer;
    private readonly ILogger<CmsController> _logger;

    public CmsController(
        UiManifestBuilder manifestBuilder,
        PageContentRenderer pageRenderer,
        ILogger<CmsController> logger)
    {
        _manifestBuilder = manifestBuilder;
        _pageRenderer = pageRenderer;
        _logger = logger;
    }

    [HttpGet("menu")]
    public async Task<IActionResult> GetMenu(CancellationToken ct)
    {
        try
        {
            var manifest = await _manifestBuilder.BuildAsync(ct);
            return Ok(new { success = true, data = manifest });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get CMS menu");
            return StatusCode(500, ex.Message);
        }
    }

    [HttpGet("menu.json")]
    public async Task<IActionResult> GetMenuJson(CancellationToken ct)
    {
        try
        {
            var manifest = await _manifestBuilder.BuildAsync(ct);
            var menu = manifest.Menu ?? new List<UiMenuItem>();
            return Ok(new { success = true, menu = menu });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get menu JSON");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    [HttpGet("manifest")]
    public async Task<IActionResult> GetManifest(CancellationToken ct)
    {
        try
        {
            var manifest = await _manifestBuilder.BuildAsync(ct);
            return Ok(new { success = true, data = manifest });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get CMS manifest");
            return StatusCode(500, ex.Message);
        }
    }

    [HttpGet("page")]
    public Task<IActionResult> GetPage([FromQuery] string route)
        => RenderPageResponse(route);

    [HttpGet("page/{**route}")]
    public Task<IActionResult> GetPageByRoute(string route)
        => RenderPageResponse(route);

    private async Task<IActionResult> RenderPageResponse(string route)
    {
        var normalizedRoute = NormalizeRoute(route);
        if (string.IsNullOrWhiteSpace(normalizedRoute))
        {
            normalizedRoute = "/";
        }

        try
        {
            var content = await _pageRenderer.RenderPageAsync(HttpContext, normalizedRoute);
            var manifest = await _manifestBuilder.BuildAsync(HttpContext.RequestAborted);
            var routeDef = manifest.Routes.FirstOrDefault(r =>
                string.Equals(r.Path, normalizedRoute, StringComparison.OrdinalIgnoreCase));

            var css = NormalizeAssets(normalizedRoute, content.CssAssets);
            var js = NormalizeAssets(normalizedRoute, content.JsAssets);

            if (routeDef?.Assets != null)
            {
                css = MergeAssets(css, BuildAssetUrls(routeDef, "css"));
                js = MergeAssets(js, BuildAssetUrls(routeDef, "js"));
            }

            return Ok(new PageContentResponse
            {
                Success = true,
                Html = content.Html,
                Assets = new PageAssets
                {
                    Css = css,
                    Js = js
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to render CMS page for route {Route}", normalizedRoute);
            return Ok(new PageContentResponse
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    private static string NormalizeRoute(string? route)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            return "/";
        }

        var trimmed = route.Trim();
        if (!trimmed.StartsWith("/"))
        {
            trimmed = "/" + trimmed;
        }

        if (trimmed.Length > 1 && trimmed.EndsWith("/"))
        {
            trimmed = trimmed.TrimEnd('/');
        }

        return trimmed;
    }

    private static List<string> NormalizeAssets(string route, IEnumerable<string> assets)
    {
        var list = new List<string>();
        foreach (var asset in assets)
        {
            if (string.IsNullOrWhiteSpace(asset))
            {
                continue;
            }

            list.Add(NormalizeAsset(route, asset));
        }

        return list;
    }

    private static string NormalizeAsset(string route, string asset)
    {
        if (!string.IsNullOrWhiteSpace(route) && route.StartsWith("/p/", StringComparison.OrdinalIgnoreCase))
        {
            var parts = route.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3 && asset.StartsWith("/_content/", StringComparison.OrdinalIgnoreCase))
            {
                var packageId = parts[1];
                var moduleId = parts[2];
                var fileName = Path.GetFileName(asset);
                return $"/assets/package/{packageId}/{moduleId}/{fileName}";
            }
        }

        return asset;
    }

    private static List<string> MergeAssets(List<string> existing, IEnumerable<string> extra)
    {
        foreach (var asset in extra)
        {
            if (string.IsNullOrWhiteSpace(asset))
            {
                continue;
            }

            if (!existing.Any(existingAsset => string.Equals(existingAsset, asset, StringComparison.OrdinalIgnoreCase)))
            {
                existing.Add(asset);
            }
        }

        return existing;
    }

    private static IEnumerable<string> BuildAssetUrls(UiRoute route, string type)
    {
        var assets = type == "css" ? route.Assets?.Css : route.Assets?.Js;
        var extras = type == "css" ? route.Assets?.ExtraCss : route.Assets?.ExtraJs;
        if ((assets == null || assets.Count == 0) && (extras == null || extras.Count == 0))
        {
            yield break;
        }

        var module = route.Meta.TryGetValue("module", out var mod) ? mod?.ToString() : null;
        var packageId = route.Meta.TryGetValue("packageId", out var pkg) ? pkg?.ToString() : null;
        var moduleId = route.Meta.TryGetValue("moduleId", out var pkgModule) ? pkgModule?.ToString() : null;

        foreach (var asset in (assets ?? Enumerable.Empty<string>()).Concat(extras ?? Enumerable.Empty<string>()))
        {
            if (string.IsNullOrWhiteSpace(asset))
            {
                continue;
            }

            if (asset.StartsWith("/"))
            {
                yield return asset;
                continue;
            }

            if (string.Equals(route.Kind, "package", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(packageId) &&
                !string.IsNullOrWhiteSpace(moduleId))
            {
                var ext = type == "css" ? ".css" : ".js";
                yield return $"/assets/package/{packageId}/{moduleId}/{asset}{ext}";
                continue;
            }

            var moduleSegment = string.IsNullOrWhiteSpace(module) ? "pages" : module;
            var extension = type == "css" ? ".css" : ".js";
            yield return $"/assets/pages/{moduleSegment}/{asset}{extension}";
        }
    }
}
