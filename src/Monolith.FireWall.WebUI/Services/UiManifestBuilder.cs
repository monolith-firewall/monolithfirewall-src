using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Monolith.FireWall.WebUI.Models;

namespace Monolith.FireWall.WebUI.Services;

public sealed class UiManifestBuilder
{
    private readonly IWebHostEnvironment _env;
    private readonly CoreApiClient _coreApi;
    private readonly EndpointDataSource _endpoints;
    private readonly ILogger<UiManifestBuilder> _logger;
    private readonly Dictionary<string, string> _packageNames = new(StringComparer.OrdinalIgnoreCase);

    public UiManifestBuilder(
        IWebHostEnvironment env,
        CoreApiClient coreApi,
        EndpointDataSource endpoints,
        ILogger<UiManifestBuilder> logger)
    {
        _env = env;
        _coreApi = coreApi;
        _endpoints = endpoints;
        _logger = logger;
    }

    public async Task<UiManifest> BuildAsync(CancellationToken ct = default)
    {
        var manifest = await LoadInternalManifestAsync(ct);
        var baseManifest = await LoadBaseManifestAsync(ct);
        MergeBaseManifest(manifest, baseManifest);
        MergeBaseMenu(manifest, baseManifest);

        // Merge dynamic routes from Core (package pages)
        await MergeCorePagesAsync(manifest, ct);
        await MergeCoreMenusAsync(manifest, ct);

        // Merge scanned firewall routes from Razor endpoints (if present)
        MergeFirewallRoutesFromEndpoints(manifest);

        // Merge firewall routes from filesystem (Config.cshtml without @page)
        MergeFirewallRoutesFromFiles(manifest);

        // Build menu dynamically from discovered routes
        BuildMenuFromRoutes(manifest);
        BuildPackagesMenu(manifest);
        
        // Resolve all menu item paths from routeId
        ResolveMenuPaths(manifest);

        // Include interface assignments in metadata for easy access by frontend routers/components
        await IncludeInterfaceAssignmentsAsync(manifest, ct);

        // Final materialization safety net
        manifest.Materialize();

        return manifest;
    }

    private async Task IncludeInterfaceAssignmentsAsync(UiManifest manifest, CancellationToken ct)
    {
        try
        {
            var assignments = await _coreApi.SendRequestAsync(JsonSerializer.Serialize(new { action = "interfaces.assignments.list" }));
            
            // Fully materialize the response JSON into a standard object (POCO) to avoid any JsonElement references
            var response = JsonSerializer.Deserialize<Dictionary<string, object>>(assignments);
            if (response == null) return;

            // Core returns data: [...] or Data: [...]
            if (response.TryGetValue("Data", out var data) || response.TryGetValue("data", out data))
            {
                if (data != null)
                {
                    // Convert the data part to a stable object by re-deserializing it into a generic object
                    // This ensures no JsonElement references are left in the tree.
                    var materializedData = JsonSerializer.Deserialize<object>(JsonSerializer.Serialize(data));
                    manifest.Metadata["interfaces"] = materializedData;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to include interface assignments in manifest. assignments response was potentially invalid or caused materialization error.");
        }
    }

    private void MergeBaseMenu(UiManifest target, UiManifest source)
    {
        if (source?.Menu == null || source.Menu.Count == 0)
        {
            return;
        }

        target.Menu = CloneMenuItems(source.Menu);
        
        // Ensure group icons are set if missing
        foreach (var menuItem in target.Menu)
        {
            if (string.IsNullOrWhiteSpace(menuItem.Icon))
            {
                menuItem.Icon = GetDefaultGroupIcon(menuItem.Label);
            }
        }
    }

    private List<UiMenuItem> CloneMenuItems(IEnumerable<UiMenuItem> items)
    {
        var result = new List<UiMenuItem>();
        foreach (var item in items)
        {
            var clone = new UiMenuItem
            {
                Label = item.Label,
                RouteId = item.RouteId,
                Path = item.Path,
                Icon = item.Icon
            };

            if (item.Children != null && item.Children.Count > 0)
            {
                clone.Children = CloneMenuItems(item.Children);
            }

            result.Add(clone);
        }
        return result;
    }

    private void MergeBaseManifest(UiManifest target, UiManifest source)
    {
        if (source == null || source.Routes.Count == 0)
        {
            return;
        }

        foreach (var baseRoute in source.Routes)
        {
            if (string.IsNullOrWhiteSpace(baseRoute.Path))
            {
                continue;
            }

            var existing = target.Routes.FirstOrDefault(r =>
                string.Equals(r.Id, baseRoute.Id, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r.Path, baseRoute.Path, StringComparison.OrdinalIgnoreCase));

            if (existing == null)
            {
                var route = new UiRoute
                {
                    Id = string.IsNullOrWhiteSpace(baseRoute.Id) ? BuildRouteIdFromPath(baseRoute.Path) : baseRoute.Id,
                    Path = baseRoute.Path,
                    Title = baseRoute.Title,
                    Kind = baseRoute.Kind,
                    RequiresAuth = baseRoute.RequiresAuth,
                    Assets = baseRoute.Assets,
                    Shell = baseRoute.Shell,
                    Meta = new Dictionary<string, object?>(baseRoute.Meta ?? new Dictionary<string, object?>())
                };

                if (!string.Equals(route.Kind, "package", StringComparison.OrdinalIgnoreCase) &&
                    !route.Meta.ContainsKey("module"))
                {
                    route.Meta["module"] = GetInternalModuleName(route.Path);
                }

                target.Routes.Add(route);
                continue;
            }

            if (string.IsNullOrWhiteSpace(existing.Title))
            {
                existing.Title = baseRoute.Title;
            }

            if (string.IsNullOrWhiteSpace(existing.Kind))
            {
                existing.Kind = baseRoute.Kind;
            }

            if (existing.Assets == null)
            {
                existing.Assets = baseRoute.Assets;
            }

            if (existing.Shell == null)
            {
                existing.Shell = baseRoute.Shell;
            }

            if (!string.Equals(existing.Kind, "package", StringComparison.OrdinalIgnoreCase) &&
                !existing.Meta.ContainsKey("module"))
            {
                existing.Meta["module"] = GetInternalModuleName(existing.Path);
            }
        }

        if (string.IsNullOrWhiteSpace(target.DefaultRouteId) ||
            !target.Routes.Any(r => string.Equals(r.Id, target.DefaultRouteId, StringComparison.OrdinalIgnoreCase)))
        {
            if (!string.IsNullOrWhiteSpace(source.DefaultRouteId) &&
                target.Routes.Any(r => string.Equals(r.Id, source.DefaultRouteId, StringComparison.OrdinalIgnoreCase)))
            {
                target.DefaultRouteId = source.DefaultRouteId;
            }
        }

        if (string.IsNullOrWhiteSpace(target.LoginRouteId) ||
            !target.Routes.Any(r => string.Equals(r.Id, target.LoginRouteId, StringComparison.OrdinalIgnoreCase)))
        {
            if (!string.IsNullOrWhiteSpace(source.LoginRouteId) &&
                target.Routes.Any(r => string.Equals(r.Id, source.LoginRouteId, StringComparison.OrdinalIgnoreCase)))
            {
                target.LoginRouteId = source.LoginRouteId;
            }
        }
    }

    private async Task MergeCoreMenusAsync(UiManifest manifest, CancellationToken ct)
    {
        try
        {
            var responseJson = await _coreApi.SendRequestAsync(JsonSerializer.Serialize(new { action = "get-menus" }));
            var response = JsonSerializer.Deserialize<Dictionary<string, object>>(responseJson);
            if (response == null) return;

            var success = GetDictBool(response, "success") ?? GetDictBool(response, "Success") ?? false;
            if (!success)
            {
                _logger.LogWarning("Core get-menus returned success=false");
                return;
            }

            if (!response.TryGetValue("data", out var dataObj) && !response.TryGetValue("Data", out dataObj))
            {
                _logger.LogWarning("Core get-menus returned no data");
                return;
            }

            // Materialize the list fully
            var dataJson = JsonSerializer.Serialize(dataObj);
            var menus = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(dataJson);
            if (menus == null) return;

            foreach (var menu in menus)
            {
                var packageId = GetDictString(menu, "packageId") ?? GetDictString(menu, "PackageId");
                var packageName = GetDictString(menu, "packageName") ?? GetDictString(menu, "PackageName");
                var moduleId = GetDictString(menu, "moduleId") ?? GetDictString(menu, "ModuleId");
                var label = GetDictString(menu, "label") ?? GetDictString(menu, "Label");
                var menuId = GetDictString(menu, "id") ?? GetDictString(menu, "Id");

                if (string.IsNullOrWhiteSpace(packageId) || string.IsNullOrWhiteSpace(moduleId))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(packageName))
                {
                    _packageNames[packageId] = packageName;
                }

                // Extract page name from menu ID (e.g., "diagnostics-ping" -> "ping")
                // Menu ID format is typically "{moduleId}-{pageName}"
                string? pageName = null;
                if (!string.IsNullOrWhiteSpace(menuId) && menuId.Contains('-'))
                {
                    var parts = menuId.Split('-', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 1 && string.Equals(parts[0], moduleId, StringComparison.OrdinalIgnoreCase))
                    {
                        // Menu ID is like "diagnostics-ping", extract "ping"
                        pageName = string.Join("-", parts.Skip(1));
                    }
                }

                // Build route path with page name if available
                var routePath = string.IsNullOrWhiteSpace(pageName)
                    ? $"/p/{packageId}/{moduleId}"
                    : $"/p/{packageId}/{moduleId}/{pageName}";
                var routeId = BuildRouteIdFromPath(routePath);

                var existingRoute = manifest.Routes.FirstOrDefault(r =>
                    string.Equals(r.Id, routeId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(r.Path, routePath, StringComparison.OrdinalIgnoreCase));

                // Extract icon from menu if available
                var icon = GetDictString(menu, "icon") ?? GetDictString(menu, "Icon");

                if (existingRoute == null)
                {
                    var route = new UiRoute
                    {
                        Id = routeId,
                        Path = routePath,
                        Title = label ?? routePath,
                        Kind = "package",
                        RequiresAuth = true,
                        Meta = new Dictionary<string, object?>
                        {
                            ["packageId"] = packageId,
                            ["moduleId"] = moduleId,
                            ["pageId"] = pageName ?? "config"
                        }
                    };

                    // Store icon in route meta for menu building
                    if (!string.IsNullOrWhiteSpace(icon))
                    {
                        route.Meta["icon"] = icon;
                    }

                    EnsurePackageAssets(route);
                    manifest.Routes.Add(route);
                }
                else if (!string.IsNullOrWhiteSpace(label))
                {
                    existingRoute.Title = label;
                    // Update pageId if we extracted it
                    if (!string.IsNullOrWhiteSpace(pageName) && existingRoute.Meta != null)
                    {
                        existingRoute.Meta["pageId"] = pageName;
                    }
                    // Update icon if provided
                    if (!string.IsNullOrWhiteSpace(icon) && existingRoute.Meta != null)
                    {
                        existingRoute.Meta["icon"] = icon;
                    }
                }

                if (existingRoute != null)
                {
                    EnsurePackageAssets(existingRoute);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to merge Core menus into UI manifest");
        }
    }

    private bool? GetDictBool(Dictionary<string, object> dict, string key)
    {
        if (dict.TryGetValue(key, out var val) && val != null)
        {
            if (val is JsonElement elem)
            {
                if (elem.ValueKind == JsonValueKind.True) return true;
                if (elem.ValueKind == JsonValueKind.False) return false;
            }
            if (val is bool b) return b;
            if (bool.TryParse(val.ToString(), out var parsed)) return parsed;
        }
        return null;
    }

    private string? GetDictString(Dictionary<string, object> dict, string key)
    {
        if (dict.TryGetValue(key, out var val) && val != null)
        {
            if (val is JsonElement elem && elem.ValueKind == JsonValueKind.String) return elem.GetString();
            return val.ToString();
        }
        return null;
    }

    private void BuildPackagesMenu(UiManifest manifest)
    {
        try
        {
            var packageRoutes = manifest.Routes
                .Where(r => string.Equals(r.Kind, "package", StringComparison.OrdinalIgnoreCase))
                .ToList();

            _logger.LogDebug("Found {Count} package routes for menu building", packageRoutes.Count);

            var packages = packageRoutes
                .Select(r => new
                {
                    Route = r,
                    PackageId = r.Meta.TryGetValue("packageId", out var pkg) ? pkg?.ToString() : null,
                    ModuleId = r.Meta.TryGetValue("moduleId", out var mod) ? mod?.ToString() : null
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.PackageId) && !string.IsNullOrWhiteSpace(x.ModuleId))
                .GroupBy(x => x.PackageId!, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key)
                .ToList();

            _logger.LogDebug("Grouped into {Count} packages with modules", packages.Count);

            // Always add the Packages menu item, even if empty
            var packageGroup = new UiMenuItem
            {
                Label = "Packages",
                Icon = "fa-solid fa-box-open",
                Children = packages.Select(pkgGroup =>
                {
                    var pkgLabel = _packageNames.TryGetValue(pkgGroup.Key, out var name) ? name : pkgGroup.Key;
                    var moduleItems = pkgGroup
                        .OrderBy(x => x.ModuleId, StringComparer.OrdinalIgnoreCase)
                        .Select(x => new UiMenuItem
                        {
                            Label = x.Route.Title,
                            RouteId = x.Route.Id,
                            Path = x.Route.Path,
                            Icon = GetRouteIcon(x.Route)
                        })
                        .ToList();

                    _logger.LogDebug("Package {PackageId} has {Count} module items", pkgGroup.Key, moduleItems.Count);

                    return new UiMenuItem
                    {
                        Label = pkgLabel,
                        Icon = "fa-solid fa-box-open",
                        Children = moduleItems
                    };
                }).ToList()
            };

            // Remove any existing Packages menu item first
            manifest.Menu.RemoveAll(m => string.Equals(m.Label, "Packages", StringComparison.OrdinalIgnoreCase));
            manifest.Menu.Add(packageGroup);
            
            _logger.LogDebug("Added Packages menu with {Count} package groups", packageGroup.Children?.Count ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to build packages menu");
            // Even on error, add an empty Packages menu item so the UI can render it
            manifest.Menu.RemoveAll(m => string.Equals(m.Label, "Packages", StringComparison.OrdinalIgnoreCase));
            manifest.Menu.Add(new UiMenuItem
            {
                Label = "Packages",
                Children = new List<UiMenuItem>()
            });
        }
    }

    private async Task<UiManifest> LoadInternalManifestAsync(CancellationToken ct)
    {
        var manifest = new UiManifest();
        var pagesRoot = Path.Combine(_env.ContentRootPath, "Pages");
        if (!Directory.Exists(pagesRoot))
        {
            _logger.LogWarning("Pages directory not found: {Path}", pagesRoot);
            return manifest;
        }

        var skipFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "_ViewImports.cshtml",
            "_ViewStart.cshtml",
            "_Layout.cshtml",
            "PackagePageWrapper.cshtml"
        };

        var files = Directory.EnumerateFiles(pagesRoot, "*.cshtml", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            if (skipFiles.Contains(fileName))
            {
                continue;
            }

            var relativePath = Path.GetRelativePath(pagesRoot, file);
            if (relativePath.StartsWith($"SystemLogs{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            {
                // Prefer the newer System/Logs.cshtml page.
                continue;
            }

            string content;
            try
            {
                content = await File.ReadAllTextAsync(file, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read page file: {Path}", file);
                continue;
            }

            var route = TryParsePageRoute(content);
            if (string.IsNullOrWhiteSpace(route))
            {
                continue;
            }

            if (string.Equals(route, "/", StringComparison.Ordinal))
            {
                // App shell route should not be part of the CMS manifest.
                continue;
            }

            if (route.StartsWith("/p/", StringComparison.OrdinalIgnoreCase))
            {
                // Package routes are provided by Core.
                continue;
            }

            var moduleName = GetInternalModuleName(route);
            var routeId = BuildRouteIdFromPath(route);
            var kind = GetRouteKind(route);
            var title = TryParseTitle(content) ?? ToTitle(route.Trim('/').Split('/').LastOrDefault() ?? route);
            var requiresAuth = !route.StartsWith("/login", StringComparison.OrdinalIgnoreCase) &&
                               !route.StartsWith("/setup", StringComparison.OrdinalIgnoreCase);

            if (manifest.Routes.Any(r => string.Equals(r.Id, routeId, StringComparison.OrdinalIgnoreCase)) ||
                manifest.Routes.Any(r => string.Equals(r.Path, route, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var uiRoute = new UiRoute
            {
                Id = routeId,
                Path = route,
                Title = title,
                Kind = kind,
                RequiresAuth = requiresAuth,
                Meta = new Dictionary<string, object?>
                {
                    ["module"] = moduleName
                },
                Assets = new UiRouteAssets
                {
                    Js = new List<string> { moduleName },
                    Css = new List<string>()
                }
            };

            // Check if CSS file exists before adding it
            var cssPath = Path.Combine(_env.WebRootPath, "css", $"{moduleName}.css");
            if (File.Exists(cssPath))
            {
                uiRoute.Assets.Css.Add(moduleName);
            }
            else if (moduleName == "dashboard")
            {
                // Fallback for dashboard if not in root css
                uiRoute.Assets.Css.Add("dashboard");
            }
            else if (kind == "firewall")
            {
                uiRoute.Assets.Css.Add("firewall");
            }

            // Special case for settings: it needs sub-modules
            if (moduleName == "settings")
            {
                if (!uiRoute.Assets.Js.Contains("settings-system")) uiRoute.Assets.Js.Add("settings-system");
                if (!uiRoute.Assets.Js.Contains("settings-webui")) uiRoute.Assets.Js.Add("settings-webui");
            }

            manifest.Routes.Add(uiRoute);
        }

        if (manifest.Routes.Any(r => r.Path.Equals("/dashboard", StringComparison.OrdinalIgnoreCase)))
        {
            manifest.DefaultRouteId = "dashboard";
        }

        if (manifest.Routes.Any(r => r.Path.Equals("/login", StringComparison.OrdinalIgnoreCase)))
        {
            manifest.LoginRouteId = "login";
        }

        return manifest;
    }

    private async Task<UiManifest> LoadBaseManifestAsync(CancellationToken ct)
    {
        var path = Path.Combine(_env.WebRootPath, "page", "routes.json");
        if (!File.Exists(path))
        {
            _logger.LogWarning("Base UI routes file not found: {Path}", path);
            return new UiManifest();
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, ct);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var manifest = JsonSerializer.Deserialize<UiManifest>(json, options) ?? new UiManifest();
            manifest.Materialize();
            return manifest;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load base UI routes file");
            return new UiManifest();
        }
    }

    private async Task MergeCorePagesAsync(UiManifest manifest, CancellationToken ct)
    {
        try
        {
            // Same shape as /api/core?action=get-pages
            var responseJson = await _coreApi.SendRequestAsync(JsonSerializer.Serialize(new { action = "get-pages" }));
            var response = JsonSerializer.Deserialize<Dictionary<string, object>>(responseJson);
            if (response == null) return;

            var success = GetDictBool(response, "success") ?? GetDictBool(response, "Success") ?? false;
            if (!success)
            {
                _logger.LogWarning("Core get-pages returned success=false");
                return;
            }

            if (!response.TryGetValue("data", out var dataObj) && !response.TryGetValue("Data", out dataObj))
            {
                _logger.LogWarning("Core get-pages returned no data");
                return;
            }

            // Materialize the list fully
            var dataJson = JsonSerializer.Serialize(dataObj);
            var pages = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(dataJson);
            if (pages == null) return;

            foreach (var page in pages)
            {
                var route = (GetDictString(page, "route") ?? GetDictString(page, "Route"))?.Trim();
                if (string.IsNullOrWhiteSpace(route) || !route.StartsWith("/"))
                {
                    continue;
                }

                var title = (GetDictString(page, "title") ?? GetDictString(page, "Title")) ?? route;

                // Route id: normalize to a stable id (p.<package>.<module>[.<page>])
                var routeId = BuildRouteIdFromPath(route);

                // Avoid duplicates: base manifest wins if it already defines the path/id.
                if (manifest.Routes.Any(r => string.Equals(r.Id, routeId, StringComparison.OrdinalIgnoreCase)) ||
                    manifest.Routes.Any(r => string.Equals(r.Path, route, StringComparison.OrdinalIgnoreCase)))
                {
                    var existing = manifest.Routes.FirstOrDefault(r =>
                        string.Equals(r.Id, routeId, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(r.Path, route, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        EnsurePackageAssets(existing);
                    }
                    continue;
                }

                if (route.StartsWith("/p/", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = route.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3 && (string.Equals(title, route, StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(title)))
                    {
                        title = ToTitle(parts[2]);
                    }
                }

                var uiRoute = new UiRoute
                {
                    Id = routeId,
                    Path = route,
                    Title = title,
                    Kind = route.StartsWith("/p/", StringComparison.OrdinalIgnoreCase) ? "package" : "internal",
                    RequiresAuth = true
                };

                if (uiRoute.Kind == "package")
                {
                    // meta: package/module/page
                    var parts = route.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3)
                    {
                        uiRoute.Meta["packageId"] = parts[1];
                        uiRoute.Meta["moduleId"] = parts[2];
                        uiRoute.Meta["pageId"] = parts.Length > 3 ? parts[3] : "config";
                    }

                    EnsurePackageAssets(uiRoute);
                }

                manifest.Routes.Add(uiRoute);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to merge Core pages into UI manifest");
        }
    }

    private void MergeFirewallRoutesFromEndpoints(UiManifest manifest)
    {
        // If firewall pages are Razor pages they will appear as endpoints like /firewall/aliases
        // Only match exact routes (not parameterized templates like /firewall/{module})
        var firewallEndpoints = _endpoints.Endpoints
            .OfType<RouteEndpoint>()
            .Where(e =>
            {
                var pattern = e.RoutePattern.RawText;
                if (string.IsNullOrWhiteSpace(pattern))
                {
                    return false;
                }

                // Must start with /firewall/
                if (!pattern.StartsWith("/firewall/", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                // Skip parameterized routes (contain {})
                if (pattern.Contains('{'))
                {
                    return false;
                }

                return true;
            })
            .Select(e => e.RoutePattern.RawText!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var path in firewallEndpoints)
        {
            var id = BuildRouteIdFromPath(path);
            if (manifest.Routes.Any(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase)) ||
                manifest.Routes.Any(r => string.Equals(r.Path, path, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var moduleName = path.Split('/').LastOrDefault() ?? "firewall";
            manifest.Routes.Add(new UiRoute
            {
                Id = id,
                Path = path,
                Title = ToTitle(moduleName),
                Kind = "firewall",
                RequiresAuth = true,
                Meta = new Dictionary<string, object?>
                {
                    ["module"] = moduleName
                },
                Assets = new UiRouteAssets
                {
                    Js = new List<string> { moduleName },
                    Css = new List<string> { "firewall" }
                }
            });
        }
    }

    private void MergeFirewallRoutesFromFiles(UiManifest manifest)
    {
        var firewallRoot = Path.Combine(_env.ContentRootPath, "Pages", "Firewall");
        if (!Directory.Exists(firewallRoot))
        {
            return;
        }

        foreach (var dir in Directory.EnumerateDirectories(firewallRoot))
        {
            var configPath = Path.Combine(dir, "Config.cshtml");
            if (!File.Exists(configPath))
            {
                continue;
            }

            var content = File.ReadAllText(configPath);
            if (content.Contains("@page", StringComparison.OrdinalIgnoreCase))
            {
                // This route will already be picked up by Razor endpoints.
                continue;
            }

            var folderName = Path.GetFileName(dir);
            if (string.IsNullOrWhiteSpace(folderName))
            {
                continue;
            }

            var route = $"/firewall/{ToKebabCase(folderName)}";
            var id = BuildRouteIdFromPath(route);
            if (manifest.Routes.Any(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase)) ||
                manifest.Routes.Any(r => string.Equals(r.Path, route, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            manifest.Routes.Add(new UiRoute
            {
                Id = id,
                Path = route,
                Title = ToTitle(folderName),
                Kind = "firewall",
                RequiresAuth = true,
                Meta = new Dictionary<string, object?>
                {
                    ["module"] = ToKebabCase(folderName)
                },
                Assets = new UiRouteAssets
                {
                    Js = new List<string> { ToKebabCase(folderName) },
                    Css = new List<string> { "firewall" }
                }
            });
        }
    }

    private static string BuildRouteIdFromPath(string path)
    {
        // /p/monolith-network/dhcp -> p.monolith-network.dhcp
        // /system/advanced -> system.advanced
        // /firewall/virtual-ips -> firewall.virtual-ips
        var parts = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return string.Join('.', parts);
    }

    private static void EnsurePackageAssets(UiRoute route)
    {
        if (!string.Equals(route.Kind, "package", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!route.Meta.TryGetValue("moduleId", out var moduleObj) || moduleObj == null)
        {
            return;
        }

        var moduleId = moduleObj.ToString();
        if (string.IsNullOrWhiteSpace(moduleId))
        {
            return;
        }

        route.Assets ??= new UiRouteAssets();

        if (!route.Assets.Js.Any(asset => string.Equals(asset, moduleId, StringComparison.OrdinalIgnoreCase)))
        {
            route.Assets.Js.Add(moduleId);
        }

        if (!route.Assets.Css.Any(asset => string.Equals(asset, moduleId, StringComparison.OrdinalIgnoreCase)))
        {
            route.Assets.Css.Add(moduleId);
        }
    }

    private void BuildMenuFromRoutes(UiManifest manifest)
    {
        // If a base menu is already provided (e.g., routes.json), honor it to preserve labels/icons.
        if (manifest.Menu.Count > 0)
        {
            return;
        }

        manifest.Menu.Clear();

        AddMenuGroup(manifest, "System", route =>
            route.Path.StartsWith("/system/", StringComparison.OrdinalIgnoreCase) ||
            route.Path.Equals("/users", StringComparison.OrdinalIgnoreCase) ||
            route.Path.Equals("/groups", StringComparison.OrdinalIgnoreCase) ||
            route.Path.Equals("/permissions", StringComparison.OrdinalIgnoreCase) ||
            route.Path.Equals("/about", StringComparison.OrdinalIgnoreCase));

        AddMenuGroup(manifest, "Interfaces", route =>
            route.Path.StartsWith("/interfaces", StringComparison.OrdinalIgnoreCase));

        AddMenuGroup(manifest, "Firewall", route =>
            route.Path.StartsWith("/firewall/", StringComparison.OrdinalIgnoreCase));

        AddMenuGroup(manifest, "Status", route =>
            route.Path.StartsWith("/status/", StringComparison.OrdinalIgnoreCase));
    }

    private void AddMenuGroup(UiManifest manifest, string label, Func<UiRoute, bool> predicate)
    {
        var routes = manifest.Routes
            .Where(predicate)
            .Where(r => !r.Path.Contains("{", StringComparison.Ordinal))
            .Where(r => !string.Equals(r.Kind, "login", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(r.Kind, "setup", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(r.Kind, "package", StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.Title)
            .ToList();

        if (routes.Count == 0)
        {
            return;
        }

        // Get default icon for menu group
        var groupIcon = GetDefaultGroupIcon(label);

        var menuItem = new UiMenuItem
        {
            Label = label,
            Icon = groupIcon,
            Children = routes.Select(route => new UiMenuItem
            {
                Label = route.Title,
                RouteId = route.Id,
                Path = route.Path ?? string.Empty, // Ensure path is set
                Icon = GetRouteIcon(route)
            }).ToList()
        };

        manifest.Menu.Add(menuItem);
    }

    private static string GetDefaultGroupIcon(string label)
    {
        return label.ToLowerInvariant() switch
        {
            "system" => "fa-solid fa-gear",
            "interfaces" => "fa-solid fa-network-wired",
            "firewall" => "fa-solid fa-shield-halved",
            "status" => "fa-solid fa-chart-line",
            "packages" => "fa-solid fa-box-open",
            _ => "fa-solid fa-circle-dot"
        };
    }

    private static string? GetRouteIcon(UiRoute route)
    {
        // Check if route has icon in meta
        if (route.Meta.TryGetValue("icon", out var iconObj) && iconObj != null)
        {
            return iconObj.ToString();
        }

        // Default icons based on route path
        var path = route.Path.ToLowerInvariant();
        if (path.Contains("settings")) return "fa-solid fa-sliders";
        if (path.Contains("packages")) return "fa-solid fa-box-open";
        if (path.Contains("modules")) return "fa-solid fa-puzzle-piece";
        if (path.Contains("routing")) return "fa-solid fa-route";
        if (path.Contains("logs")) return "fa-solid fa-clipboard-list";
        if (path.Contains("backup")) return "fa-solid fa-floppy-disk";
        if (path.Contains("users")) return "fa-solid fa-users";
        if (path.Contains("groups")) return "fa-solid fa-user-group";
        if (path.Contains("permissions")) return "fa-solid fa-key";
        if (path.Contains("network-cards")) return "fa-solid fa-microchip";
        if (path.Contains("rules")) return "fa-solid fa-shield-halved";
        if (path.Contains("aliases")) return "fa-solid fa-list-check";
        if (path.Contains("/nat")) return "fa-solid fa-right-left";
        if (path.Contains("virtual-ips")) return "fa-solid fa-circle-nodes";
        if (path.Contains("traffic-shaper")) return "fa-solid fa-wave-square";
        if (path.Contains("schedules")) return "fa-solid fa-calendar-days";
        if (path.Contains("system")) return "fa-solid fa-gauge-high";
        if (path.Contains("interfaces")) return "fa-solid fa-network-wired";
        if (path.Contains("services")) return "fa-solid fa-server";
        if (path.Contains("states")) return "fa-solid fa-network-wired";
        if (path.Contains("routing-status")) return "fa-solid fa-route";

        return null;
    }

    private static string? TryParsePageRoute(string content)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            content,
            @"@page\s+""([^""]+)""",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static string? TryParseTitle(string content)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            content,
            @"ViewData\[\s*""Title""\s*\]\s*=\s*""([^""]+)""",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        match = System.Text.RegularExpressions.Regex.Match(
            content,
            @"ViewData\[\s*""Title""\s*\]\s*=\s*'([^']+)'",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static string GetRouteKind(string route)
    {
        if (route.StartsWith("/login", StringComparison.OrdinalIgnoreCase))
        {
            return "login";
        }

        if (route.StartsWith("/setup", StringComparison.OrdinalIgnoreCase))
        {
            return "setup";
        }

        if (route.StartsWith("/firewall/", StringComparison.OrdinalIgnoreCase))
        {
            return "firewall";
        }

        return "internal";
    }

    private static string GetInternalModuleName(string route)
    {
        if (route.StartsWith("/status/", StringComparison.OrdinalIgnoreCase))
        {
            return "status";
        }

        return route.ToLowerInvariant() switch
        {
            "/dashboard" => "dashboard",
            "/users" => "users",
            "/groups" => "groups",
            "/permissions" => "permissions",
            "/profile" => "profile",
            "/system/packages" => "packages",
            "/system/modules" => "modules",
            "/system/updates" => "updates",
            "/system/settings" => "settings",
            "/system/advanced" => "advanced-settings",
            "/system/routing" => "routing",
            "/system/logs" => "system-logs",
            "/system/backup" => "backup",
            "/interfaces" => "interfaces",
            "/interfaces/network-cards" => "network-cards",
            "/about" => "about",
            _ => route.Trim('/').Split('/').LastOrDefault() ?? "page"
        };
    }

    private static string ToTitle(string slug)
        => string.Join(' ', (slug ?? "").Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Length > 0 ? char.ToUpperInvariant(s[0]) + s[1..] : s));

    private static string ToKebabCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var chars = new List<char>();
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (char.IsUpper(ch) && i > 0)
            {
                chars.Add('-');
            }
            chars.Add(char.ToLowerInvariant(ch));
        }

        return new string(chars.ToArray());
    }

    private void ResolveMenuPaths(UiManifest manifest)
    {
        foreach (var menuItem in manifest.Menu)
        {
            ResolveMenuItemPath(menuItem, manifest);
        }
    }

    private void ResolveMenuItemPath(UiMenuItem item, UiManifest manifest)
    {
        // If path is missing but routeId exists, resolve it from routes
        if (string.IsNullOrWhiteSpace(item.Path) && !string.IsNullOrWhiteSpace(item.RouteId))
        {
            var route = manifest.Routes.FirstOrDefault(r => 
                string.Equals(r.Id, item.RouteId, StringComparison.OrdinalIgnoreCase));
            if (route != null && !string.IsNullOrWhiteSpace(route.Path))
            {
                item.Path = route.Path;
            }
        }

        // Recursively resolve children
        if (item.Children != null && item.Children.Count > 0)
        {
            foreach (var child in item.Children)
            {
                ResolveMenuItemPath(child, manifest);
            }
        }
    }
}