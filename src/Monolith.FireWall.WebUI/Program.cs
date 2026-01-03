using CodeLogic;
using Monolith.FireWall.WebUI.Services;
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
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Text.Json;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

var bindingConfig = LoadWebUiBindings("/etc/monolith-firewall/webui-bindings.json");
var bindingAddresses = ResolveBindingAddresses(bindingConfig);
var certificate = LoadOrCreateCertificate();

// Configure Kestrel to listen on ports 80 and 443
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    if (bindingAddresses.Count == 0)
    {
        serverOptions.ListenAnyIP(80);
        serverOptions.ListenAnyIP(443, listenOptions => listenOptions.UseHttps(certificate));
        return;
    }

    foreach (var address in bindingAddresses)
    {
        serverOptions.Listen(address, 80);
        serverOptions.Listen(address, 443, listenOptions => listenOptions.UseHttps(certificate));
    }
});

// Add services
builder.Services.AddSingleton<CoreApiClient>();
builder.Services.AddSingleton<PackageViewRouter>();
builder.Services.AddSingleton<PackageViewsRegistry>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<PackageUpdatesClient>();

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

builder.Services.AddControllers();
builder.Services.AddRazorPages();

// Add background services
builder.Services.AddHostedService<Monolith.FireWall.WebUI.BackgroundServices.PermissionSyncService>();

// Configure Razor view engine to look for views in package assemblies
builder.Services.Configure<Microsoft.AspNetCore.Mvc.Razor.RazorViewEngineOptions>(options =>
{
    // Add view location formats for package views
    options.ViewLocationFormats.Add("/_content/{0}/Pages/{1}" + Microsoft.AspNetCore.Mvc.Razor.RazorViewEngine.ViewExtension);
    options.ViewLocationFormats.Add("/_content/{0}/Views/{1}" + Microsoft.AspNetCore.Mvc.Razor.RazorViewEngine.ViewExtension);
    
    // Also add area view locations
    options.AreaViewLocationFormats.Add("/_content/{2}/Areas/{1}/Views/{0}" + Microsoft.AspNetCore.Mvc.Razor.RazorViewEngine.ViewExtension);
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

// Routing
app.UseRouting();

// Authentication middleware
app.UseMiddleware<AuthenticationMiddleware>();

// Custom route for package static files: /_content/{PackageName}/{**filePath}
// Maps /_content/Monolith.Network/js/file.js -> /opt/monolith-firewall/packages/monolith-network/wwwroot/js/file.js
app.MapGet("/_content/{packageName}/{**filePath}", async (HttpContext context, string packageName, string filePath) =>
{
    try
    {
        // Convert package name: Monolith.Network -> monolith-network
        var packageFolder = packageName.ToLowerInvariant().Replace(".", "-");
        var physicalPath = $"/opt/monolith-firewall/packages/{packageFolder}/wwwroot/{filePath}";
        
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

static string? ResolvePackageAsset(string packageFolder, string filePath)
{
    var basePath = $"/opt/monolith-firewall/packages/{packageFolder}/wwwroot";
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

static string NormalizeModuleKey(string value)
{
    return new string(value
        .Where(char.IsLetterOrDigit)
        .Select(char.ToLowerInvariant)
        .ToArray());
}

static string? FindModuleFolder(string basePagesPath, string module)
{
    if (!Directory.Exists(basePagesPath))
    {
        return null;
    }

    var moduleKey = NormalizeModuleKey(module);
    foreach (var dir in Directory.GetDirectories(basePagesPath))
    {
        var name = Path.GetFileName(dir);
        if (NormalizeModuleKey(name) == moduleKey)
        {
            return name;
        }
    }

    return null;
}

static async Task<string> DownloadPackageAsync(string packageId, string downloadUrl, CancellationToken cancellationToken)
{
    if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri))
    {
        throw new Exception("Invalid download URL");
    }

    if (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
    {
        throw new Exception("Only HTTPS downloads are allowed");
    }

    var cacheDir = "/var/lib/monolith-firewall/packages-cache";
    Directory.CreateDirectory(cacheDir);
    var fileName = $"{packageId}-{DateTime.UtcNow:yyyyMMddHHmmss}.mfwpkg";
    var targetPath = Path.Combine(cacheDir, fileName);

    using var client = new HttpClient();
    using var response = await client.GetAsync(uri, cancellationToken);
    if (!response.IsSuccessStatusCode)
    {
        throw new Exception($"Download failed: {response.StatusCode}");
    }

    await using var fs = File.Create(targetPath);
    await response.Content.CopyToAsync(fs, cancellationToken);

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
        var resolvedPath = ResolvePackageAsset(packageFolder, filePath);
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
app.MapControllers();

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

app.MapGet("/api/user/current", async (HttpContext httpContext) =>
{
    var user = AuthenticationMiddleware.GetUser(httpContext);
    if (user == null)
    {
        // Return 200 with success: false instead of 401 for auth check endpoint
        return Results.Json(new { success = false, authenticated = false });
    }

    return Results.Json(new {
        success = true,
        authenticated = true,
        data = new {
            id = user.UserId,
            username = user.Username,
            roles = user.Roles,
            permissions = user.Permissions
        }
    });
});

// Package page route (module only): /p/{package}/{module}
// This loads the default page (Index.cshtml or Config.cshtml) for the module
app.MapGet("/p/{package}/{module}", async (HttpContext context, string package, string module) =>
{
    var route = $"/p/{package.ToLowerInvariant()}/{module.ToLowerInvariant()}";
    
    // Convert package name to folder name: monolith-network
    var packageFolder = package.ToLowerInvariant();
    var pagesPath = $"/opt/monolith-firewall/packages/{packageFolder}/Pages";
    var moduleFolder = FindModuleFolder(pagesPath, module);
    
    // Map module to file path
    // Example: dhcp -> Pages/Dhcp/Index.cshtml or Pages/Dhcp/Config.cshtml
    var moduleCap = moduleFolder ?? (char.ToUpper(module[0]) + module.Substring(1));
    
    // Try Index.cshtml first, then Config.cshtml as fallback
    var indexPath = $"/opt/monolith-firewall/packages/{packageFolder}/Pages/{moduleCap}/Index.cshtml";
    var configPath = $"/opt/monolith-firewall/packages/{packageFolder}/Pages/{moduleCap}/Config.cshtml";
    
    string? filePath = null;
    if (System.IO.File.Exists(indexPath))
    {
        filePath = indexPath;
    }
    else if (System.IO.File.Exists(configPath))
    {
        filePath = configPath;
    }
    
    if (filePath != null)
    {
        var content = await System.IO.File.ReadAllTextAsync(filePath);
        context.Response.ContentType = "text/html";
        context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
        context.Response.Headers.Append("Pragma", "no-cache");
        context.Response.Headers.Append("Expires", "0");
        await context.Response.WriteAsync(content);
    }
    else
    {
        context.Response.StatusCode = 404;
        await context.Response.WriteAsync($@"<!DOCTYPE html>
<html>
<head>
    <title>Page Not Found</title>
    <link href=""/css/bootstrap.min.css"" rel=""stylesheet"" />
    <link href=""/css/pfsense-theme.css"" rel=""stylesheet"" />
</head>
<body>
    <div class=""container mt-5"">
        <div class=""alert alert-warning"">
            <h4>Page Not Found</h4>
            <p>The page at <code>{route}</code> could not be loaded.</p>
            <p>Tried: <code>{indexPath}</code> and <code>{configPath}</code></p>
            <p class=""text-muted"">This page may not be fully implemented yet.</p>
        </div>
    </div>
</body>
</html>");
    }
});

// Package page route: /p/{package}/{module}/{page}
app.MapGet("/p/{package}/{module}/{page}", async (HttpContext context, string package, string module, string page) =>
{
    var route = $"/p/{package.ToLowerInvariant()}/{module.ToLowerInvariant()}/{page.ToLowerInvariant()}";
    
    // Convert package name to folder name: monolith-network
    var packageFolder = package.ToLowerInvariant();
    var pagesPath = $"/opt/monolith-firewall/packages/{packageFolder}/Pages";
    var moduleFolder = FindModuleFolder(pagesPath, module);
    
    // Map module/page to file path
    // Example: dhcp/config -> Pages/Dhcp/Config.cshtml
    var moduleCap = moduleFolder ?? (char.ToUpper(module[0]) + module.Substring(1));
    var pageCap = char.ToUpper(page[0]) + page.Substring(1);
    var filePath = $"/opt/monolith-firewall/packages/{packageFolder}/Pages/{moduleCap}/{pageCap}.cshtml";
    
    if (System.IO.File.Exists(filePath))
    {
        var content = await System.IO.File.ReadAllTextAsync(filePath);
        context.Response.ContentType = "text/html";
        context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
        context.Response.Headers.Append("Pragma", "no-cache");
        context.Response.Headers.Append("Expires", "0");
        await context.Response.WriteAsync(content);
    }
    else
    {
        context.Response.StatusCode = 404;
        await context.Response.WriteAsync($@"<!DOCTYPE html>
<html>
<head>
    <title>Page Not Found</title>
    <link href=""/css/bootstrap.min.css"" rel=""stylesheet"" />
    <link href=""/css/pfsense-theme.css"" rel=""stylesheet"" />
</head>
<body>
    <div class=""container mt-5"">
        <div class=""alert alert-warning"">
            <h4>Page Not Found</h4>
            <p>The page at <code>{route}</code> could not be loaded.</p>
            <p>Expected file: <code>{filePath}</code></p>
            <p class=""text-muted"">This page may not be fully implemented yet.</p>
        </div>
    </div>
</body>
</html>");
    }
});

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
        var sourcePath = root.TryGetProperty("sourcePath", out var pathEl) ? pathEl.GetString() : null;
        var overwrite = !root.TryGetProperty("overwrite", out var overwriteEl) || overwriteEl.GetBoolean();

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

            sourcePath = await DownloadPackageAsync(packageId, downloadUrl, context.RequestAborted);
        }

        var coreRequest = new
        {
            action = "packages.install",
            payload = new
            {
                packageId,
                sourcePath,
                overwrite
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

// Default route to index.html
app.MapGet("/", async context =>
{
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync("wwwroot/index.html");
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

static WebUiBindings LoadWebUiBindings(string path)
{
    try
    {
        if (!File.Exists(path))
        {
            return new WebUiBindings();
        }

        var json = File.ReadAllText(path);
        var config = JsonSerializer.Deserialize<WebUiBindings>(json);
        return config ?? new WebUiBindings();
    }
    catch
    {
        return new WebUiBindings();
    }
}

static List<IPAddress> ResolveBindingAddresses(WebUiBindings config)
{
    var addresses = new List<IPAddress>();
    if (config.Addresses == null || config.Addresses.Count == 0)
    {
        return addresses;
    }

    foreach (var entry in config.Addresses)
    {
        if (IPAddress.TryParse(entry, out var address))
        {
            addresses.Add(address);
        }
    }

    return addresses;
}

sealed class WebUiBindings
{
    public List<string> Addresses { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}
