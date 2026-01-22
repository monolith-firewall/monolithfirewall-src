using CodeLogic;
using Monolith.FireWall.WebUI.Services;
using Monolith.FireWall.WebUI.Middleware;
using Monolith.FireWall.WebUI.Middleware;
using Monolith.FireWall.WebUI.Features.Users.Repositories;
using Monolith.FireWall.WebUI.Features.Users.Services;
using Monolith.FireWall.WebUI.Features.SystemLogs;
using Monolith.FireWall.WebUI.Features.Firewall;
using Monolith.FireWall.WebUI.Features.Firewall.Aliases;
using Monolith.FireWall.WebUI.Features.Firewall.Nat;
using Monolith.FireWall.WebUI.Features.Firewall.VirtualIps;
using Monolith.FireWall.WebUI.Features.Firewall.TrafficShaper;
using Monolith.FireWall.WebUI.Features.Firewall.Schedules;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Text.Json;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Configure logging - enable Debug level for detailed diagnostics
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    options.FormatterName = "simple";
});
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Debug);
builder.Logging.AddFilter("Microsoft", LogLevel.Warning); // Reduce Microsoft noise
builder.Logging.AddFilter("System", LogLevel.Warning); // Reduce System noise
builder.Logging.AddFilter("Monolith.FireWall", LogLevel.Debug); // Full debug for our code

// Load WebUI settings from database via Core API
var webUiSettings = await LoadWebUiSettingsAsync();
var certificate = LoadOrCreateCertificate();

// Configure Kestrel to listen on configured ports and addresses
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    var httpPort = webUiSettings.HttpPort;
    var httpsPort = webUiSettings.HttpsPort;
    var bindingAddresses = webUiSettings.BindingAddresses;

    if (bindingAddresses.Count == 0 || webUiSettings.BindToAllInterfaces)
    {
        // Bind to all interfaces
        serverOptions.ListenAnyIP(httpPort);
        serverOptions.ListenAnyIP(httpsPort, listenOptions => listenOptions.UseHttps(certificate));
        Console.WriteLine($"WebUI configured: HTTP={httpPort}, HTTPS={httpsPort} (all interfaces)");
    }
    else
    {
        // Bind to specific addresses
        foreach (var address in bindingAddresses)
        {
            if (IPAddress.TryParse(address, out var ip))
            {
                serverOptions.Listen(ip, httpPort);
                serverOptions.Listen(ip, httpsPort, listenOptions => listenOptions.UseHttps(certificate));
            }
        }
        Console.WriteLine($"WebUI configured: HTTP={httpPort}, HTTPS={httpsPort} on {bindingAddresses.Count} interface(s)");
    }
});

// Add services
builder.Services.AddSingleton<CoreApiClient>();
builder.Services.AddSingleton<PackageViewRouter>();
builder.Services.AddSingleton<PackageViewsRegistry>();
builder.Services.AddSingleton<PackageDiscoveryService>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<PackageUpdatesClient>();
builder.Services.AddSingleton<RazorPartialRenderer>();
builder.Services.AddSingleton<PageContentRenderer>();
builder.Services.AddSingleton<UiManifestBuilder>();

// Frontend expects camelCase JSON for WebUI endpoints
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

// Register CL.SQLite and UserRepository
CL.SQLite.SQLiteLibrary? sqliteForDI = null;
try
{
    var initResult = await CodeLogic.CodeLogic.InitializeAsync(opts =>
    {
        opts.RootDirectory = "/var/lib/monolith-firewall/codelogic";
        opts.PluginsDirectory = "/var/lib/monolith-firewall/plugins";
    });

    if (initResult.Success && !initResult.IsFirstRun)
    {
        await CodeLogic.CodeLogic.ConfigureAsync();
        await CodeLogic.CodeLogic.StartAsync();
    }
    sqliteForDI = CodeLogic.Libs.Get<CL.SQLite.SQLiteLibrary>();
}
catch
{
    sqliteForDI = CodeLogic.Libs.Get<CL.SQLite.SQLiteLibrary>();
}

if (sqliteForDI != null)
{
    builder.Services.AddSingleton<CL.SQLite.SQLiteLibrary>(_ => sqliteForDI);
    builder.Services.AddSingleton<UserRepository>();
    builder.Services.AddSingleton<Monolith.FireWall.WebUI.Features.Users.Repositories.UserGroupRepository>();
    builder.Services.AddSingleton<UserService>();
    builder.Services.AddSingleton<Monolith.FireWall.WebUI.Features.Users.Services.UserGroupService>();
    builder.Services.AddSingleton<Monolith.FireWall.WebUI.Features.SystemLogs.SystemLogsManager>();
    
    // Firewall services
    builder.Services.AddSingleton<AliasesManager>();
    builder.Services.AddSingleton<NatManager>();
    builder.Services.AddSingleton<VirtualIpsManager>();
    builder.Services.AddSingleton<TrafficShaperManager>();
    builder.Services.AddSingleton<SchedulesManager>();
    builder.Services.AddSingleton<FirewallService>();
}

// Register custom application model provider to filter out controllers from package assemblies
// This prevents package assemblies (which reference Core) from being scanned for controllers
builder.Services.AddSingleton<Microsoft.AspNetCore.Mvc.ApplicationModels.IApplicationModelProvider, Monolith.FireWall.WebUI.Services.PackageControllerFeatureProvider>();

builder.Services.AddControllers(options =>
{
    // Disable response caching for all controllers
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.ResponseCacheAttribute
    {
        NoStore = true,
        Location = Microsoft.AspNetCore.Mvc.ResponseCacheLocation.None
    });
}).AddJsonOptions(options =>
{
    // Use camelCase for JSON serialization in controllers
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
});
builder.Services.AddRazorPages(options =>
{
    // Disable response caching for Razor pages
    options.Conventions.ConfigureFilter(new Microsoft.AspNetCore.Mvc.ResponseCacheAttribute
    {
        NoStore = true,
        Location = Microsoft.AspNetCore.Mvc.ResponseCacheLocation.None
    });
});

// Add background services
builder.Services.AddHostedService<Monolith.FireWall.WebUI.BackgroundServices.PermissionSyncService>();

// Configure Razor view engine to look for views in package assemblies
builder.Services.Configure<Microsoft.AspNetCore.Mvc.Razor.RazorViewEngineOptions>(options =>
{
    // Add view location formats for package views (RCL assemblies)
    // Views are embedded in main DLL when using Microsoft.NET.Sdk.Razor
    // Embedded resources are like: Monolith.Diagnostics.Pages.Diagnostics.Config.cshtml
    // View location format: /_content/{AssemblyName}/Pages/{ViewPath}
    // {0} = controller/assembly name, {1} = view name (e.g., "Diagnostics/Config")
    options.ViewLocationFormats.Add("/_content/{0}/Pages/{1}" + Microsoft.AspNetCore.Mvc.Razor.RazorViewEngine.ViewExtension);
    options.ViewLocationFormats.Add("/_content/{0}/Views/{1}" + Microsoft.AspNetCore.Mvc.Razor.RazorViewEngine.ViewExtension);
    
    // Also add area view locations
    options.AreaViewLocationFormats.Add("/_content/{2}/Areas/{1}/Views/{0}" + Microsoft.AspNetCore.Mvc.Razor.RazorViewEngine.ViewExtension);
    
    // Add explicit paths for package pages (without _content prefix, just assembly name)
    options.ViewLocationFormats.Add("{0}/Pages/{1}" + Microsoft.AspNetCore.Mvc.Razor.RazorViewEngine.ViewExtension);
});

var app = builder.Build();


// Initialize UserService if CL.SQLite is available
if (sqliteForDI != null)
{
    var userService = app.Services.GetRequiredService<UserService>();
    await userService.InitializeAsync(sqliteForDI);
    Console.WriteLine("✓ UserService initialized with CL.SQLite");
    Console.WriteLine("✓ Admin user and Administrators group created (if first boot)");
    
    // Initialize System Logs database
    Monolith.FireWall.WebUI.Features.SystemLogs.SystemLogsDatabaseInit.InitializeTables();
    
    // Initialize Firewall database
    Monolith.FireWall.WebUI.Features.Firewall.FirewallDatabaseInit.InitializeAll();
}

// Register package Views assemblies with Razor engine
try
{
    var viewsRegistry = app.Services.GetRequiredService<PackageViewsRegistry>();
    var partManager = app.Services.GetRequiredService<Microsoft.AspNetCore.Mvc.ApplicationParts.ApplicationPartManager>();
    await viewsRegistry.RegisterViewsAssembliesAsync(partManager);
    Console.WriteLine("✓ Package Views assemblies registered");
}
catch (Exception ex)
{
    Console.WriteLine($"⚠ Failed to register package Views assemblies: {ex.Message}");
}

// No-cache middleware FIRST (before static files)
app.UseNoCache();

// Setup redirect middleware - must be early to catch all requests
app.UseMiddleware<SetupRedirectMiddleware>();

// Static files (WebUI) - with no-cache headers
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
        ctx.Context.Response.Headers.Append("Pragma", "no-cache");
        ctx.Context.Response.Headers.Append("Expires", "0");
    }
});

// SPA: rewrite HTML routes to the app shell (keeps /login and /setup separate)
app.Use(async (context, next) =>
{
    if (HttpMethods.IsGet(context.Request.Method))
    {
        var path = context.Request.Path.Value ?? "/";
        var accept = context.Request.Headers.Accept.ToString();
        var isAjax = context.Request.Headers["X-Requested-With"] == "XMLHttpRequest";
        var wantsHtml = accept.Contains("text/html", StringComparison.OrdinalIgnoreCase);
        var isStatic = Path.HasExtension(path);
        var isPublic =
            path.Equals("/", StringComparison.Ordinal) ||
            path.StartsWith("/login", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/setup", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/assets/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/_content/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/css/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/js/", StringComparison.OrdinalIgnoreCase);

        if (wantsHtml && !isAjax && !isStatic && !isPublic)
        {
            context.Request.Path = "/";
        }
    }

    await next();
});

// Routing
app.UseRouting();

// Authentication middleware
app.UseMiddleware<AuthenticationMiddleware>();

var packagesRoot = Environment.GetEnvironmentVariable("MONOLITH_PACKAGES_ROOT") ?? "/var/lib/monolith-firewall/packages";

// Custom route for package static files: /_content/{PackageName}/{**filePath}
// Maps /_content/Monolith.Network/js/file.js -> {packagesRoot}/monolith-network/wwwroot/js/file.js
app.MapGet("/_content/{packageName}/{**filePath}", async (HttpContext context, string packageName, string filePath) =>
{
    try
    {
        // Convert package name: Monolith.Network -> monolith-network
        var packageFolder = packageName.ToLowerInvariant().Replace(".", "-");
        var physicalPath = Path.Combine(packagesRoot, packageFolder, "wwwroot", filePath);
        
        if (System.IO.File.Exists(physicalPath))
        {
            var extension = System.IO.Path.GetExtension(physicalPath).ToLowerInvariant();
            var contentType = extension switch
            {
                ".js" => "application/javascript",
                ".css" => "text/css",
                ".json" => "application/json",
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".svg" => "image/svg+xml",
                ".woff" => "font/woff",
                ".woff2" => "font/woff2",
                ".ttf" => "font/ttf",
                ".eot" => "application/vnd.ms-fontobject",
                _ => "application/octet-stream"
            };
            
            context.Response.ContentType = contentType;
            context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
            context.Response.Headers.Append("Pragma", "no-cache");
            context.Response.Headers.Append("Expires", "0");
            
            await context.Response.SendFileAsync(physicalPath);
        }
        else
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsync($"File not found: {filePath}");
        }
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync($"Error: {ex.Message}");
    }
});

static string GetContentType(string path)
{
    var extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
    return extension switch
    {
        ".js" => "application/javascript",
        ".css" => "text/css",
        ".json" => "application/json",
        ".png" => "image/png",
        ".jpg" => "image/jpeg",
        ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".svg" => "image/svg+xml",
        ".woff" => "font/woff",
        ".woff2" => "font/woff2",
        ".ttf" => "font/ttf",
        ".eot" => "application/vnd.ms-fontobject",
        _ => "application/octet-stream"
    };
}

static void ApplyNoCacheHeaders(HttpResponse response)
{
    response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
    response.Headers.Append("Pragma", "no-cache");
    response.Headers.Append("Expires", "0");
}

static string? ResolveCandidatePath(string basePath, IEnumerable<string> candidates)
{
    var normalizedBase = System.IO.Path.GetFullPath(basePath);
    if (!normalizedBase.EndsWith(System.IO.Path.DirectorySeparatorChar))
    {
        normalizedBase += System.IO.Path.DirectorySeparatorChar;
    }

    foreach (var candidate in candidates)
    {
        var fullPath = System.IO.Path.GetFullPath(candidate);
        if (!fullPath.StartsWith(normalizedBase, StringComparison.Ordinal))
        {
            continue;
        }

        if (System.IO.File.Exists(fullPath))
        {
            return fullPath;
        }
    }

    return null;
}

static string? ResolveInternalAsset(Microsoft.AspNetCore.Hosting.IWebHostEnvironment env, string filePath)
{
    var webRoot = env.WebRootPath;
    var extension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
    var fileName = System.IO.Path.GetFileName(filePath);
    var candidates = new List<string>();

    if (extension == ".js")
    {
        candidates.Add(System.IO.Path.Combine(webRoot, "js", filePath));
        candidates.Add(System.IO.Path.Combine(webRoot, "js", "pages", filePath));
        if (!string.Equals(filePath, fileName, StringComparison.Ordinal))
        {
            candidates.Add(System.IO.Path.Combine(webRoot, "js", fileName));
            candidates.Add(System.IO.Path.Combine(webRoot, "js", "pages", fileName));
        }
    }
    else if (extension == ".css")
    {
        candidates.Add(System.IO.Path.Combine(webRoot, "css", filePath));
        if (!string.Equals(filePath, fileName, StringComparison.Ordinal))
        {
            candidates.Add(System.IO.Path.Combine(webRoot, "css", fileName));
        }
    }
    else
    {
        candidates.Add(System.IO.Path.Combine(webRoot, filePath));
    }

    return ResolveCandidatePath(webRoot, candidates);
}

static string? ResolvePackageAsset(string packagesRoot, string packageFolder, string filePath)
{
    var basePath = Path.Combine(packagesRoot, packageFolder, "wwwroot");
    var extension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
    var fileName = System.IO.Path.GetFileName(filePath);
    var candidates = new List<string>();

    if (extension == ".js")
    {
        candidates.Add(System.IO.Path.Combine(basePath, "js", filePath));
        if (!string.Equals(filePath, fileName, StringComparison.Ordinal))
        {
            candidates.Add(System.IO.Path.Combine(basePath, "js", fileName));
        }
    }
    else if (extension == ".css")
    {
        candidates.Add(System.IO.Path.Combine(basePath, "css", filePath));
        if (!string.Equals(filePath, fileName, StringComparison.Ordinal))
        {
            candidates.Add(System.IO.Path.Combine(basePath, "css", fileName));
        }
    }

    candidates.Add(System.IO.Path.Combine(basePath, filePath));
    return ResolveCandidatePath(basePath, candidates);
}


static bool IsPrivateIpAddress(string host)
{
    if (string.IsNullOrWhiteSpace(host))
        return false;

    // Remove port if present (host:port format)
    var hostOnly = host;
    var colonIndex = host.IndexOf(':');
    if (colonIndex > 0)
    {
        hostOnly = host.Substring(0, colonIndex);
    }

    // Try to parse as IP address
    if (!System.Net.IPAddress.TryParse(hostOnly, out var ipAddress))
        return false;

    var bytes = ipAddress.GetAddressBytes();

    // IPv4 private ranges:
    // 10.0.0.0/8 (10.0.0.0 to 10.255.255.255)
    // 172.16.0.0/12 (172.16.0.0 to 172.31.255.255)
    // 192.168.0.0/16 (192.168.0.0 to 192.168.255.255)
    // 127.0.0.0/8 (127.0.0.0 to 127.255.255.255) - loopback
    if (bytes.Length == 4)
    {
        // 10.x.x.x
        if (bytes[0] == 10)
            return true;
        
        // 172.16.x.x to 172.31.x.x
        if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            return true;
        
        // 192.168.x.x
        if (bytes[0] == 192 && bytes[1] == 168)
            return true;
        
        // 127.x.x.x (loopback)
        if (bytes[0] == 127)
            return true;
    }
    // IPv6 private ranges (simplified check)
    else if (bytes.Length == 16)
    {
        // fc00::/7 (unique local addresses)
        if (bytes[0] == 0xfc || bytes[0] == 0xfd)
            return true;
        
        // ::1 (loopback)
        if (ipAddress.Equals(System.Net.IPAddress.IPv6Loopback))
            return true;
    }

    return false;
}

static void CleanupOldCacheFiles(string cacheDir, TimeSpan maxAge)
{
    try
    {
        if (!Directory.Exists(cacheDir))
            return;

        var cutoffTime = DateTime.UtcNow - maxAge;
        var files = Directory.GetFiles(cacheDir, "*.mfwpkg");
        
        foreach (var file in files)
        {
            try
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.LastWriteTimeUtc < cutoffTime)
                {
                    // Try to delete, but don't fail if file is locked
                    try
                    {
                        File.Delete(file);
                    }
                    catch (IOException)
                    {
                        // File might be in use, skip it
                    }
                }
            }
            catch
            {
                // Skip files we can't access
            }
        }
    }
    catch
    {
        // Best effort cleanup, don't throw
    }
}

static async Task<string> DownloadPackageAsync(string packageId, string downloadUrl, string? expectedSha256, bool allowInsecureHttp, string updateServerBaseUrl, CancellationToken cancellationToken)
{
    if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri))
    {
        throw new Exception("Invalid download URL");
    }

    if (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
    {
        // Extract host without port (Uri.Host already excludes port, but be safe)
        var host = uri.Host;
        if (string.IsNullOrWhiteSpace(host) && !string.IsNullOrWhiteSpace(uri.Authority))
        {
            // Fallback: extract host from authority (host:port format)
            var authorityParts = uri.Authority.Split(':');
            host = authorityParts[0];
        }

        var isLocalhost =
            string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase);

        var isPrivateIp = IsPrivateIpAddress(host);

        // Check if this is the configured update server (trusted source)
        var isUpdateServer = false;
        if (!string.IsNullOrWhiteSpace(updateServerBaseUrl) && Uri.TryCreate(updateServerBaseUrl, UriKind.Absolute, out var updateServerUri))
        {
            isUpdateServer = string.Equals(host, updateServerUri.Host, StringComparison.OrdinalIgnoreCase);
        }

        // Allow HTTP for:
        // 1. Localhost
        // 2. Private IPs (local network)
        // 3. Configured update server (trusted source)
        // 4. Any address if allowInsecureHttp is true
        if (!allowInsecureHttp && !isLocalhost && !isPrivateIp && !isUpdateServer)
        {
            throw new Exception($"Only HTTPS downloads are allowed for public addresses. Use HTTP only for localhost, private IP addresses, or the configured update server. (Host: {host}, isPrivateIp: {isPrivateIp}, isUpdateServer: {isUpdateServer}, allowInsecureHttp: {allowInsecureHttp})");
        }
    }

    var cacheDir = "/var/lib/monolith-firewall/packages-cache";
    Directory.CreateDirectory(cacheDir);
    
    // Use GUID to ensure unique filename and avoid conflicts
    var fileName = $"{packageId}-{Guid.NewGuid():N}.mfwpkg";
    var targetPath = Path.Combine(cacheDir, fileName);

    // Clean up old cache files (older than 1 hour) to prevent disk space issues
    try
    {
        CleanupOldCacheFiles(cacheDir, TimeSpan.FromHours(1));
    }
    catch
    {
        // Best effort cleanup, don't fail if cleanup fails
    }

    using var client = new HttpClient();
    using var response = await client.GetAsync(uri, cancellationToken);
    if (!response.IsSuccessStatusCode)
    {
        throw new Exception($"Download failed: {response.StatusCode}");
    }

    // Use FileStream with FileShare.None to ensure exclusive access
    await using var fs = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
    await response.Content.CopyToAsync(fs, cancellationToken);
    await fs.FlushAsync(cancellationToken);

    if (!string.IsNullOrWhiteSpace(expectedSha256))
    {
        var expected = expectedSha256.Trim().ToLowerInvariant();
        expected = expected.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
            ? expected["sha256:".Length..]
            : expected;

        await using var verifyStream = File.OpenRead(targetPath);
        var hash = await SHA256.HashDataAsync(verifyStream, cancellationToken);
        var actual = Convert.ToHexString(hash).ToLowerInvariant();

        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
        {
            try 
            { 
                // Wait a bit and retry deletion in case file is still open
                await Task.Delay(100, cancellationToken);
                File.Delete(targetPath); 
            } 
            catch 
            { 
                // Best effort cleanup
            }
            throw new Exception("Package checksum verification failed");
        }
    }

    return targetPath;
}

// Unified asset routes
// Internal pages: /assets/pages/{module}/{file}.js|css
app.MapGet("/assets/pages/{module}/{**filePath}", async (HttpContext context, string module, string filePath, Microsoft.AspNetCore.Hosting.IWebHostEnvironment env) =>
{
    if (string.IsNullOrWhiteSpace(filePath))
    {
        context.Response.StatusCode = 404;
        return;
    }

    try
    {
        var resolvedPath = ResolveInternalAsset(env, filePath);
        if (resolvedPath == null)
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsync($"File not found: {filePath}");
            return;
        }

        context.Response.ContentType = GetContentType(resolvedPath);
        ApplyNoCacheHeaders(context.Response);
        await context.Response.SendFileAsync(resolvedPath);
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync($"Error: {ex.Message}");
    }
});

// Package pages: /assets/package/{package}/{module}/{file}.js|css
app.MapGet("/assets/package/{package}/{module}/{**filePath}", async (HttpContext context, string package, string module, string filePath) =>
{
    if (string.IsNullOrWhiteSpace(filePath))
    {
        context.Response.StatusCode = 404;
        return;
    }

    try
    {
        var packageFolder = package.ToLowerInvariant();
        var resolvedPath = ResolvePackageAsset(packagesRoot, packageFolder, filePath);
        if (resolvedPath == null)
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsync($"File not found: {filePath}");
            return;
        }

        context.Response.ContentType = GetContentType(resolvedPath);
        ApplyNoCacheHeaders(context.Response);
        await context.Response.SendFileAsync(resolvedPath);
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync($"Error: {ex.Message}");
    }
});

// Controllers (includes Firewall API controllers)
// Note: Package assemblies are registered as CompiledRazorAssemblyPart only, so they won't be scanned for controllers
app.MapControllers();

// Razor Pages (login, setup, package pages, app shell)
app.MapRazorPages();

// Login endpoint
app.MapPost("/api/auth/login", async (HttpContext context, UserService userService) =>
{
    try
    {
        var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
        var loginData = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(body);

        var username = loginData.GetProperty("username").GetString();
        var password = loginData.GetProperty("password").GetString();

        var user = await userService.ValidateLoginAsync(username ?? "", password ?? "");
        
        if (user == null)
        {
            return Results.Json(new { success = false, error = "Invalid credentials" });
        }

        // Create user context
        var userContext = new Monolith.FireWall.Common.Models.UserContext(
            user.Id,
            user.Username,
            user.GetRoles(),
            new[] { "*" }
        );

        // Set session
        AuthenticationMiddleware.SetUserSession(context, userContext);

        return Results.Json(new { 
            success = true, 
            data = new { 
                authenticated = true,
                user = new {
                    id = user.Id,
                    username = user.Username,
                    email = user.Email,
                    roles = user.GetRoles()
                }
            } 
        });
    }
    catch (Exception ex)
    {
        return Results.Json(new { success = false, error = ex.Message });
    }
});

app.MapPost("/api/auth/logout", (HttpContext context) =>
{
    AuthenticationMiddleware.ClearUserSession(context);
    return Results.Json(new { success = true });
});

// Get current user
// Profile API routes
app.MapPost("/api/profile/update", async (HttpContext context, UserService userService) =>
{
    try
    {
        // TODO: Implement profile update
        var response = new
        {
            Success = true,
            Data = (object?)null,
            Error = (string?)null
        };
        return Results.Json(response);
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

app.MapPost("/api/profile/change-password", async (HttpContext context, UserService userService) =>
{
    try
    {
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync();
        // TODO: Implement password change
        var response = new
        {
            Success = true,
            Data = (object?)null,
            Error = (string?)null
        };
        return Results.Json(response);
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

// Theme endpoints
app.MapGet("/api/users/profile/theme", async (HttpContext httpContext, UserService userService) =>
{
    try
    {
        var user = AuthenticationMiddleware.GetUser(httpContext);
        if (user == null)
        {
            return Results.Json(new { success = false, error = "Not authenticated" }, statusCode: 401);
        }

        var theme = await userService.GetUserThemeAsync(user.UserId);
        return Results.Json(new { success = true, data = new { theme } });
    }
    catch (Exception ex)
    {
        var logger = httpContext.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Error getting user theme");
        return Results.Json(new { success = false, error = ex.Message }, statusCode: 500);
    }
});

app.MapPut("/api/users/profile/theme", async (HttpContext httpContext, UserService userService, ILogger<Program> logger) =>
{
    try
    {
        var user = AuthenticationMiddleware.GetUser(httpContext);
        if (user == null)
        {
            return Results.Json(new { success = false, error = "Not authenticated" }, statusCode: 401);
        }

        var body = await new StreamReader(httpContext.Request.Body).ReadToEndAsync();
        var request = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(body);
        
        if (!request.TryGetProperty("theme", out var themeEl))
        {
            return Results.Json(new { success = false, error = "Theme is required" }, statusCode: 400);
        }

        var theme = themeEl.GetString();
        if (string.IsNullOrEmpty(theme))
        {
            return Results.Json(new { success = false, error = "Theme cannot be empty" }, statusCode: 400);
        }

        var updated = await userService.UpdateUserThemeAsync(user.UserId, theme);
        if (!updated)
        {
            return Results.Json(new { success = false, error = "Failed to update theme" }, statusCode: 500);
        }

        logger.LogInformation("User {UserId} theme updated to {Theme}", user.UserId, theme);
        return Results.Json(new { success = true, data = new { theme } });
    }
    catch (ArgumentException ex)
    {
        return Results.Json(new { success = false, error = ex.Message }, statusCode: 400);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error updating user theme");
        return Results.Json(new { success = false, error = ex.Message }, statusCode: 500);
    }
});

app.MapGet("/api/user/current", async (HttpContext httpContext, UserService userService) =>
{
    var user = AuthenticationMiddleware.GetUser(httpContext);
    if (user == null)
    {
        // Return 200 with success: false instead of 401 for auth check endpoint
        return Results.Json(new { success = false, authenticated = false });
    }

    // Get full user entity to include theme
    var userEntity = await userService.GetUserByIdAsync(user.UserId);
    
    return Results.Json(new {
        success = true,
        authenticated = true,
        data = new {
            id = user.UserId,
            username = user.Username,
            roles = user.Roles,
            permissions = user.Permissions,
            theme = userEntity?.Theme ?? "dark"
        }
    });
});


// Package pages are handled by Razor Pages via PackagePageWrapper.cshtml
// Route: /p/{package}/{module}/{page?} is defined in PackagePageWrapper.cshtml

// Interfaces API routes
app.MapGet("/api/interfaces/assignments", async (HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        var coreRequest = new { action = "interfaces.assignments.list" };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

app.MapGet("/api/interfaces/vlans", async (HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        var coreRequest = new { action = "interfaces.vlans.list" };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

app.MapGet("/api/interfaces/bridges", async (HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        var coreRequest = new { action = "interfaces.bridges.list" };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

app.MapGet("/api/interfaces/available", async (HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        var coreRequest = new { action = "interfaces.available.list" };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

app.MapPost("/api/interfaces/assignments", async (HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        using var doc = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
        var coreRequest = new
        {
            action = "interfaces.assignments.save",
            payload = doc.RootElement
        };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

app.MapDelete("/api/interfaces/assignments/{iface}", async (string iface, CoreApiClient coreClient) =>
{
    try
    {
        var coreRequest = new
        {
            action = "interfaces.assignments.delete",
            payload = new { Interface = iface }
        };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

app.MapDelete("/api/interfaces/unmanaged/{iface}", async (string iface, CoreApiClient coreClient) =>
{
    try
    {
        var coreRequest = new
        {
            action = "interfaces.unmanaged.delete",
            payload = new { Interface = iface }
        };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

app.MapPost("/api/interfaces/unmanaged/{iface}/assign", async (string iface, CoreApiClient coreClient) =>
{
    try
    {
        var coreRequest = new
        {
            action = "interfaces.unmanaged.assign",
            payload = new { Interface = iface }
        };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

app.MapPost("/api/interfaces/config/check", async (HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        var coreRequest = new { action = "interfaces.config.check" };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

app.MapPost("/api/interfaces/config/apply", async (HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        var coreRequest = new { action = "interfaces.config.apply" };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

app.MapPost("/api/interfaces/config/apply-now", async (HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        var coreRequest = new { action = "interfaces.config.apply-now" };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

// Firewall States API routes
app.MapGet("/api/firewall/states", async (HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        // Parse query parameters
        var protocol = context.Request.Query["protocol"].ToString();
        var sourceIp = context.Request.Query["sourceIp"].ToString();
        var destIp = context.Request.Query["destIp"].ToString();
        var sourcePort = context.Request.Query["sourcePort"].ToString();
        var destPort = context.Request.Query["destPort"].ToString();
        var state = context.Request.Query["state"].ToString();
        var iface = context.Request.Query["interface"].ToString();
        var direction = context.Request.Query["direction"].ToString();
        var search = context.Request.Query["search"].ToString();
        var minAge = context.Request.Query["minAge"].ToString();
        var page = context.Request.Query["page"].ToString();
        var pageSize = context.Request.Query["pageSize"].ToString();

        var payload = new Dictionary<string, object?>();
        if (!string.IsNullOrWhiteSpace(protocol)) payload["protocol"] = protocol;
        if (!string.IsNullOrWhiteSpace(sourceIp)) payload["sourceIp"] = sourceIp;
        if (!string.IsNullOrWhiteSpace(destIp)) payload["destIp"] = destIp;
        if (!string.IsNullOrWhiteSpace(sourcePort)) payload["sourcePort"] = sourcePort;
        if (!string.IsNullOrWhiteSpace(destPort)) payload["destPort"] = destPort;
        if (!string.IsNullOrWhiteSpace(state)) payload["state"] = state;
        if (!string.IsNullOrWhiteSpace(iface)) payload["interface"] = iface;
        if (!string.IsNullOrWhiteSpace(direction)) payload["direction"] = direction;
        if (!string.IsNullOrWhiteSpace(search)) payload["search"] = search;
        if (!string.IsNullOrWhiteSpace(minAge) && int.TryParse(minAge, out var minAgeVal)) payload["minAge"] = minAgeVal;
        if (!string.IsNullOrWhiteSpace(page) && int.TryParse(page, out var pageVal)) payload["page"] = pageVal;
        if (!string.IsNullOrWhiteSpace(pageSize) && int.TryParse(pageSize, out var pageSizeVal)) payload["pageSize"] = pageSizeVal;

        var coreRequest = new
        {
            action = "firewall.states.list",
            payload = payload.Count > 0 ? payload : null
        };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

app.MapPost("/api/firewall/states/kill", async (HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync();
        var request = JsonSerializer.Deserialize<JsonElement>(body);
        
        if (!request.TryGetProperty("id", out var idEl))
        {
            return Results.Json(new { Success = false, Data = (object?)null, Error = "State ID is required" }, statusCode: 400);
        }

        var coreRequest = new
        {
            action = "firewall.states.kill",
            payload = new { id = idEl.GetString() }
        };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

// Diagnostic endpoint to check connection tracking status
app.MapGet("/api/firewall/states/diagnostic", async (HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        var diagnostic = new Dictionary<string, object>();
        
        // Check if /proc/net/nf_conntrack exists
        var procPath = "/proc/net/nf_conntrack";
        diagnostic["procExists"] = File.Exists(procPath);
        
        if (File.Exists(procPath))
        {
            try
            {
                var lines = await File.ReadAllLinesAsync(procPath);
                diagnostic["lineCount"] = lines.Length;
                if (lines.Length > 0)
                {
                    diagnostic["firstLine"] = lines[0].Substring(0, Math.Min(300, lines[0].Length));
                    diagnostic["sampleLines"] = lines.Take(3).Select(l => l.Substring(0, Math.Min(200, l.Length))).ToArray();
                }
            }
            catch (Exception ex)
            {
                diagnostic["procReadError"] = ex.Message;
            }
        }
        
        // Check if conntrack command exists
        var conntrackExists = File.Exists("/usr/bin/conntrack") || File.Exists("/usr/sbin/conntrack") || File.Exists("/bin/conntrack");
        diagnostic["conntrackExists"] = conntrackExists;
        
        // Try to get actual states count
        var coreRequest = new { action = "firewall.states.list", payload = new { page = 1, pageSize = 1 } };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        var response = JsonSerializer.Deserialize<JsonElement>(responseJson);
        if (response.TryGetProperty("Data", out var data) && data.TryGetProperty("Total", out var total))
        {
            diagnostic["totalStates"] = total.GetInt32();
        }
        
        return Results.Json(new { Success = true, Data = diagnostic, Error = (string?)null });
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

// Monitoring API routes
app.MapGet("/api/monitoring/status", async (HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        var coreRequest = new { action = "monitoring.status.list" };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

app.MapGet("/api/monitoring/notifications", async (HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        var limit = 20;
        var unreadOnly = false;
        if (int.TryParse(context.Request.Query["limit"], out var limitValue))
        {
            limit = limitValue;
        }

        if (bool.TryParse(context.Request.Query["unreadOnly"], out var unreadValue))
        {
            unreadOnly = unreadValue;
        }

        var coreRequest = new
        {
            action = "monitoring.notifications.list",
            payload = new { limit, unreadOnly }
        };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

app.MapPost("/api/monitoring/notifications/read", async (HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        using var doc = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
        var coreRequest = new
        {
            action = "monitoring.notifications.read",
            payload = doc.RootElement
        };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

app.MapPost("/api/monitoring/monitors/update", async (HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        using var doc = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
        var coreRequest = new
        {
            action = "monitoring.monitor.update",
            payload = doc.RootElement
        };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

app.MapPost("/api/interfaces/config/fix", async (HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        var coreRequest = new { action = "interfaces.config.fix" };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

// Routing API routes
app.MapPost("/api/system/command", async (HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        using var doc = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
        var coreRequest = new
        {
            action = "system.command.run",
            payload = doc.RootElement
        };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

app.MapGet("/api/routing/status", async (HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        var coreRequest = new
        {
            action = "routing.status"
        };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

app.MapGet("/api/routing/gateways", async (HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        var coreRequest = new { action = "routing.gateways.list" };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

app.MapPost("/api/routing/gateways", async (HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        using var doc = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
        var coreRequest = new
        {
            action = "routing.gateways.create",
            payload = doc.RootElement
        };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

app.MapDelete("/api/routing/gateways/{id:int}", async (int id, CoreApiClient coreClient) =>
{
    try
    {
        var coreRequest = new
        {
            action = "routing.gateways.delete",
            payload = new { Id = id }
        };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

app.MapGet("/api/routing/routes", async (HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        var coreRequest = new { action = "routing.routes.list" };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

app.MapPost("/api/routing/routes", async (HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        using var doc = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
        var coreRequest = new
        {
            action = "routing.routes.add",
            payload = doc.RootElement
        };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

app.MapDelete("/api/routing/routes/{id:int}", async (int id, CoreApiClient coreClient) =>
{
    try
    {
        var coreRequest = new
        {
            action = "routing.routes.remove",
            payload = new { Id = id }
        };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

// System tuneables API routes
app.MapGet("/api/system/tuneables", async (HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        var coreRequest = new { action = "system.tuneables.list" };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

app.MapPost("/api/system/tuneables/apply", async (HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        using var doc = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
        var coreRequest = new
        {
            action = "system.tuneables.apply",
            payload = doc.RootElement
        };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

app.MapPost("/api/system/tuneables/save", async (HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        using var doc = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
        var coreRequest = new
        {
            action = "system.tuneables.save",
            payload = doc.RootElement
        };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

// System settings API routes
app.MapGet("/api/system/settings", async (HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        var coreRequest = new { action = "system.settings.get" };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

app.MapPost("/api/system/settings", async (HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        using var doc = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
        var coreRequest = new
        {
            action = "system.settings.update",
            payload = doc.RootElement
        };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

// Setup wizard API routes
app.MapGet("/api/setup/status", async (HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        var coreRequest = new { action = "setup.status" };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

app.MapPost("/api/setup/complete-step", async (HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        using var doc = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
        var coreRequest = new
        {
            action = "setup.complete-step",
            stepId = doc.RootElement.GetProperty("stepId").GetString(),
            data = doc.RootElement.TryGetProperty("data", out var dataEl) ? dataEl : (JsonElement?)null
        };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

app.MapGet("/api/setup/packages", async (HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        var coreRequest = new { action = "setup.packages" };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

app.MapGet("/api/setup/package/{packageId}/{pageId}", async (string packageId, string pageId, HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        // This endpoint can be used by packages to provide setup page content
        // For now, we'll return a simple response indicating the page should be loaded via route
        // Packages can override this behavior by providing their own API endpoints
        return Results.Json(new { Success = true, Data = new { packageId, pageId, route = $"/setup/package/{packageId}/{pageId}" }, Error = (string?)null });
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

app.MapPost("/api/setup/package/{packageId}/{pageId}", async (string packageId, string pageId, HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        // This endpoint can be used by packages to save setup page data
        // Packages can provide their own handlers for this
        using var doc = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
        var data = doc.RootElement;
        
        // For now, just acknowledge receipt
        // Packages should implement their own save logic via module callbacks or API endpoints
        return Results.Json(new { Success = true, Data = new { packageId, pageId, saved = true }, Error = (string?)null });
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

app.MapPost("/api/setup/finish", async (HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        using var doc = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
        var coreRequest = new
        {
            action = "setup.finish",
            skipRemaining = doc.RootElement.TryGetProperty("skipRemaining", out var skipEl) && skipEl.GetBoolean()
        };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

app.MapPost("/api/setup/skip", async (HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        var coreRequest = new { action = "setup.skip" };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

app.MapPost("/api/setup/skip-step", async (HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        using var doc = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
        var stepId = doc.RootElement.GetProperty("stepId").GetString();
        var coreRequest = new
        {
            action = "setup.skip-step",
            stepId = stepId
        };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

app.MapGet("/api/system/settings/timezones", async (HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        var coreRequest = new { action = "system.settings.timezones" };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

// WebUI settings API routes
app.MapGet("/api/webui/settings", async (HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        var coreRequest = new { action = "webui.settings.get" };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

app.MapPost("/api/webui/settings", async (HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        using var doc = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
        var coreRequest = new
        {
            action = "webui.settings.update",
            payload = doc.RootElement
        };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

app.MapPost("/api/webui/service/restart", async (HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        var coreRequest = new { action = "webui.service.restart" };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

// Firewall API routes are now handled by FirewallController via app.MapControllers()

// Firewall pages route - serve Razor pages as SPA partials
app.MapGet("/firewall/{module}", async (HttpContext context, string module) =>
{
    try
    {
        // Convert kebab-case to PascalCase: virtual-ips -> VirtualIps
        var modulePascal = module;
        if (module.Contains("-"))
        {
            var parts = module.Split('-');
            modulePascal = string.Join("", parts.Select(p => char.ToUpper(p[0]) + p.Substring(1)));
        }
        else
        {
            modulePascal = char.ToUpper(module[0]) + module.Substring(1);
        }
        
        var pagePath = $"Pages/Firewall/{modulePascal}/Config.cshtml";
        var env = context.RequestServices.GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
        var fullPath = System.IO.Path.Combine(env.ContentRootPath, pagePath);
        
        if (System.IO.File.Exists(fullPath))
        {
            // Send the Razor page file content (SPA partial)
            context.Response.ContentType = "text/html";
            var content = await System.IO.File.ReadAllTextAsync(fullPath);
            await context.Response.WriteAsync(content);
        }
        else
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsync($"Page not found: {module}");
        }
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync($"Error: {ex.Message}");
    }
});

// Package API route: /api/packages/{package}/modules/{module}/{action}
app.MapMethods("/api/packages/{package}/modules/{module}/{action}", new[] { "GET", "POST", "PUT", "DELETE" }, 
    async (HttpContext context, string package, string module, string action, CoreApiClient coreClient) =>
{
    try
    {
        // Read request body for POST/PUT
        string? requestBody = null;
        if (context.Request.Method == "POST" || context.Request.Method == "PUT")
        {
            using var reader = new StreamReader(context.Request.Body);
            requestBody = await reader.ReadToEndAsync();
        }

        // Build Core API request in the correct format
        var coreRequest = new
        {
            packageId = package,
            moduleId = module,
            action = action,
            body = requestBody
        };

        var requestJson = System.Text.Json.JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);

        // Return response
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(responseJson);
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        var errorResponse = System.Text.Json.JsonSerializer.Serialize(new
        {
            success = false,
            error = ex.Message
        });
        await context.Response.WriteAsync(errorResponse);
    }
});

app.MapGet("/api/packages/available", async (HttpContext context, PackageUpdatesClient updatesClient) =>
{
    try
    {
        var version = context.Request.Query["version"].ToString();
        if (string.IsNullOrWhiteSpace(version))
        {
            version = "1.0.0";
        }

        var packages = await updatesClient.GetAvailablePackagesAsync(version, context.RequestAborted);
        return Results.Json(new
        {
            Success = true,
            Data = new
            {
                packages,
                fetchedAtUtc = updatesClient.LastFetchUtc
            },
            Error = (string?)null
        });
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

app.MapPost("/api/packages/install", async (HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        using var doc = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
        var root = doc.RootElement;
        var packageId = root.TryGetProperty("packageId", out var packageIdEl) ? packageIdEl.GetString() : null;
        var downloadUrl = root.TryGetProperty("downloadUrl", out var urlEl) ? urlEl.GetString() : null;
        var sha256 = root.TryGetProperty("sha256", out var shaEl) ? shaEl.GetString() : null;
        var sourcePath = root.TryGetProperty("sourcePath", out var pathEl) ? pathEl.GetString() : null;
        var overwrite = !root.TryGetProperty("overwrite", out var overwriteEl) || overwriteEl.GetBoolean();
        var restartServices = !root.TryGetProperty("restartServices", out var restartEl) || restartEl.GetBoolean();
        var config = context.RequestServices.GetRequiredService<IConfiguration>();
        var allowInsecureHttp = config.GetValue<bool>("PackageUpdates:AllowInsecureHttp");
        var updateServerBaseUrl = config["PackageUpdates:BaseUrl"] ?? "https://updates.monolithfirewall.com/api/v1/packages";

        if (string.IsNullOrWhiteSpace(packageId))
        {
            return Results.Json(new { Success = false, Data = (object?)null, Error = "packageId is required" }, statusCode: 400);
        }

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                return Results.Json(new { Success = false, Data = (object?)null, Error = "downloadUrl or sourcePath is required" }, statusCode: 400);
            }

            sourcePath = await DownloadPackageAsync(packageId, downloadUrl, sha256, allowInsecureHttp, updateServerBaseUrl, context.RequestAborted);
        }

        var coreRequest = new
        {
            action = "packages.install",
            payload = new
            {
                packageId,
                sourcePath,
                overwrite,
                restartServices
            }
        };

        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

app.MapPost("/api/packages/uninstall", async (HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        using var doc = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
        var root = doc.RootElement;
        var packageId = root.TryGetProperty("packageId", out var packageIdEl) ? packageIdEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(packageId))
        {
            return Results.Json(new { Success = false, Data = (object?)null, Error = "packageId is required" }, statusCode: 400);
        }

        var coreRequest = new
        {
            action = "packages.uninstall",
            payload = new
            {
                packageId
            }
        };

        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

app.MapPost("/api/modules/state", async (HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        using var doc = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
        var root = doc.RootElement;
        var packageId = root.TryGetProperty("packageId", out var packageIdEl) ? packageIdEl.GetString() : null;
        var moduleId = root.TryGetProperty("moduleId", out var moduleIdEl) ? moduleIdEl.GetString() : null;
        var enabled = root.TryGetProperty("enabled", out var enabledEl) && enabledEl.GetBoolean();

        if (string.IsNullOrWhiteSpace(packageId) || string.IsNullOrWhiteSpace(moduleId))
        {
            return Results.Json(new { Success = false, Data = (object?)null, Error = "packageId and moduleId are required" }, statusCode: 400);
        }

        var coreRequest = new
        {
            action = enabled ? "modules.enable" : "modules.disable",
            payload = new
            {
                packageId,
                moduleId,
                enabled
            }
        };

        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

// System Logs API routes
app.MapGet("/api/logs/monolith", async (HttpContext context, SystemLogsManager logsManager) =>
{
    try
    {
        var queryParams = new Monolith.FireWall.Common.Models.LogQueryParams
        {
            Category = context.Request.Query["category"].ToString(),
            Level = context.Request.Query["level"].ToString(),
            Source = context.Request.Query["source"].ToString(),
            Limit = int.TryParse(context.Request.Query["limit"].ToString(), out var limit) ? limit : 100,
            Offset = int.TryParse(context.Request.Query["offset"].ToString(), out var offset) ? offset : 0
        };

        if (DateTime.TryParse(context.Request.Query["startDate"].ToString(), out var startDate))
            queryParams.StartDate = startDate;
        if (DateTime.TryParse(context.Request.Query["endDate"].ToString(), out var endDate))
            queryParams.EndDate = endDate;

        var result = await logsManager.QueryMonolithLogsAsync(queryParams);
        return Results.Json(new { Success = true, Data = result, Error = (string?)null });
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

app.MapGet("/api/logs/system", async (HttpContext context, SystemLogsManager logsManager) =>
{
    try
    {
        var queryParams = new Monolith.FireWall.Common.Models.LogQueryParams
        {
            Category = context.Request.Query["category"].ToString(),
            Level = context.Request.Query["level"].ToString(),
            Source = context.Request.Query["source"].ToString(),
            Limit = int.TryParse(context.Request.Query["limit"].ToString(), out var limit) ? limit : 100,
            Offset = int.TryParse(context.Request.Query["offset"].ToString(), out var offset) ? offset : 0
        };

        if (DateTime.TryParse(context.Request.Query["startDate"].ToString(), out var startDate))
            queryParams.StartDate = startDate;
        if (DateTime.TryParse(context.Request.Query["endDate"].ToString(), out var endDate))
            queryParams.EndDate = endDate;

        var result = await logsManager.QuerySystemLogsAsync(queryParams);
        return Results.Json(new { Success = true, Data = result, Error = (string?)null });
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

app.MapGet("/api/logs/security", async (HttpContext context, SystemLogsManager logsManager) =>
{
    try
    {
        var queryParams = new Monolith.FireWall.Common.Models.LogQueryParams
        {
            Category = context.Request.Query["category"].ToString(),
            Level = context.Request.Query["level"].ToString(),
            Source = context.Request.Query["source"].ToString(),
            Limit = int.TryParse(context.Request.Query["limit"].ToString(), out var limit) ? limit : 100,
            Offset = int.TryParse(context.Request.Query["offset"].ToString(), out var offset) ? offset : 0
        };

        if (DateTime.TryParse(context.Request.Query["startDate"].ToString(), out var startDate))
            queryParams.StartDate = startDate;
        if (DateTime.TryParse(context.Request.Query["endDate"].ToString(), out var endDate))
            queryParams.EndDate = endDate;

        var result = await logsManager.QuerySecurityLogsAsync(queryParams);
        return Results.Json(new { Success = true, Data = result, Error = (string?)null });
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Data = (object?)null, Error = ex.Message }, statusCode: 500);
    }
});

// Default SPA shell - only for non-API routes to avoid ambiguity with controllers
app.MapWhen(
    ctx => !ctx.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase),
    branch =>
    {
        branch.UseRouting();
        branch.UseEndpoints(endpoints =>
        {
            var fallbackEndpoint = endpoints.MapFallbackToPage("/App");
            fallbackEndpoint.Add(builder =>
            {
                if (builder is Microsoft.AspNetCore.Routing.RouteEndpointBuilder routeBuilder)
                {
                    // Ensure this stays lower priority than controller routes
                    routeBuilder.Order = int.MaxValue;
                }
            });
        });
    });

app.Run();

static X509Certificate2 LoadOrCreateCertificate()
{
    var certPath = "/etc/monolith-firewall/monolith-firewall.pfx";
    X509Certificate2? certificate = null;

    if (File.Exists(certPath))
    {
        try
        {
            certificate = new X509Certificate2(certPath, "monolith-firewall");
            Console.WriteLine("✓ Loaded existing SSL certificate");
        }
        catch
        {
            certificate = null;
        }
    }

    if (certificate != null)
    {
        return certificate;
    }

    Console.WriteLine("→ Generating self-signed SSL certificate...");
    using var rsa = RSA.Create(2048);
    var request = new CertificateRequest(
        "CN=MonolithFireWall",
        rsa,
        HashAlgorithmName.SHA256,
        RSASignaturePadding.Pkcs1);

    request.CertificateExtensions.Add(
        new X509KeyUsageExtension(
            X509KeyUsageFlags.DataEncipherment | X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature,
            false));

    request.CertificateExtensions.Add(
        new X509EnhancedKeyUsageExtension(
            new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") },
            false));

    var sanBuilder = new SubjectAlternativeNameBuilder();
    sanBuilder.AddDnsName("localhost");
    sanBuilder.AddDnsName("monolith-firewall");
    sanBuilder.AddIpAddress(IPAddress.Loopback);
    sanBuilder.AddIpAddress(IPAddress.IPv6Loopback);
    request.CertificateExtensions.Add(sanBuilder.Build());

    var generated = request.CreateSelfSigned(
        DateTimeOffset.Now.AddDays(-1),
        DateTimeOffset.Now.AddYears(10));

    certificate = new X509Certificate2(generated.Export(X509ContentType.Pfx, "monolith-firewall"), "monolith-firewall");

    try
    {
        Directory.CreateDirectory("/etc/monolith-firewall");
        File.WriteAllBytes(certPath, certificate.Export(X509ContentType.Pfx, "monolith-firewall"));
        Console.WriteLine($"✓ SSL certificate saved to {certPath}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠ Could not save certificate: {ex.Message}");
    }

    return certificate;
}

/// <summary>
/// Load WebUI settings from database via Core API
/// </summary>
static async Task<WebUiSettings> LoadWebUiSettingsAsync()
{
    var defaultSettings = new WebUiSettings
    {
        HttpPort = 80,
        HttpsPort = 443,
        BindToAllInterfaces = true,
        BindingAddresses = new List<string>()
    };

    // Try to load from Core API (database)
    try
    {
        var socketPath = "/var/lib/monolith-firewall/run/monolith-core.sock";
        if (File.Exists(socketPath))
        {
            // Wait a moment for Core to be ready
            await Task.Delay(500);
            
            var request = JsonSerializer.Serialize(new { action = "webui.settings.get" });
            var response = await SendCoreRequestAsync(socketPath, request);
            
            if (response != null && response.Contains("\"Success\":true", StringComparison.OrdinalIgnoreCase))
            {
                using var doc = JsonDocument.Parse(response);
                if (doc.RootElement.TryGetProperty("Data", out var data))
                {
                    var httpPort = data.TryGetProperty("httpPort", out var http) ? http.GetInt32() : 80;
                    var httpsPort = data.TryGetProperty("httpsPort", out var https) ? https.GetInt32() : 443;
                    var bindToAll = data.TryGetProperty("bindToAllInterfaces", out var all) && all.GetBoolean();
                    var addresses = new List<string>();
                    
                    if (data.TryGetProperty("bindingAddresses", out var addrs) && addrs.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var addr in addrs.EnumerateArray())
                        {
                            if (addr.ValueKind == JsonValueKind.String)
                            {
                                addresses.Add(addr.GetString() ?? "");
                            }
                        }
                    }

                    Console.WriteLine($"✓ Loaded WebUI settings from database: HTTP={httpPort}, HTTPS={httpsPort}");
                    return new WebUiSettings
                    {
                        HttpPort = httpPort,
                        HttpsPort = httpsPort,
                        BindToAllInterfaces = bindToAll,
                        BindingAddresses = addresses
                    };
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠ Could not load WebUI settings from Core API: {ex.Message}");
    }

    Console.WriteLine($"✓ Using default WebUI settings: HTTP=80, HTTPS=443 (all interfaces)");
    return defaultSettings;
}

static async Task<string?> SendCoreRequestAsync(string socketPath, string request)
{
    try
    {
        // Use CoreApiClient approach - create a temporary client
        var client = new CoreApiClient();
        return await client.SendRequestAsync(request);
    }
    catch
    {
        return null;
    }
}

sealed class WebUiSettings
{
    public int HttpPort { get; set; }
    public int HttpsPort { get; set; }
    public bool BindToAllInterfaces { get; set; }
    public List<string> BindingAddresses { get; set; } = new();
}
