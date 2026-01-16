using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Monolith.FireWall.Common.Interfaces;
using Monolith.FireWall.Common.Models;
using Monolith.FireWall.Core.Services;

namespace Monolith.FireWall.Core.Transport;

public class NamedPipeListener
{
    private readonly ILogger _logger;
    private readonly ModuleRegistry _moduleRegistry;
    private readonly Services.Platform.PlatformExecutor _platformExecutor;
    private readonly PackageStateStore _packageStateStore;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _cts;
    private Task? _listenerTask;

    public NamedPipeListener(
        ILogger logger,
        ModuleRegistry moduleRegistry,
        Services.Platform.PlatformExecutor platformExecutor,
        PackageStateStore packageStateStore,
        string pipeName = "monolith-core")
    {
        _logger = logger;
        _moduleRegistry = moduleRegistry;
        _platformExecutor = platformExecutor;
        _packageStateStore = packageStateStore;
        _pipeName = pipeName;
        _cts = new CancellationTokenSource();
    }

    public void Start()
    {
        Console.WriteLine($"[PIPE] Starting named pipe listener on '{_pipeName}'");
        _logger.LogInformation($"Starting named pipe listener on '{_pipeName}'");
        _listenerTask = Task.Run(() => ListenAsync(_cts.Token));
        Console.WriteLine($"[PIPE] Listener task started");
    }

    public void Stop()
    {
        _logger.LogInformation("Stopping named pipe listener");
        _cts.Cancel();
        _listenerTask?.Wait();
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                NamedPipeServerStream? server = null;
                try
                {
                    server = new NamedPipeServerStream(
                        _pipeName,
                        PipeDirection.InOut,
                        20,  // Allow up to 20 concurrent connections (for multiple browser requests)
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous
                    );
                    
                    // Fix permissions on Linux - allow www-data to connect
                    // On Linux, .NET creates the pipe file when NamedPipeServerStream is constructed
                    // We need to set permissions immediately after creation
                    if (OperatingSystem.IsLinux())
                    {
                        var pipePath = $"/tmp/CoreFxPipe_{_pipeName}";
                        // Wait a moment for the file to be created, then set permissions
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(200); // Give .NET time to create the file
                            for (int i = 0; i < 10; i++)
                            {
                                if (System.IO.File.Exists(pipePath))
                                {
                                    try
                                    {
                                        // Set permissions to 666 so www-data can connect
                                        System.IO.File.SetUnixFileMode(pipePath, 
                                            System.IO.UnixFileMode.UserRead | System.IO.UnixFileMode.UserWrite |
                                            System.IO.UnixFileMode.GroupRead | System.IO.UnixFileMode.GroupWrite |
                                            System.IO.UnixFileMode.OtherRead | System.IO.UnixFileMode.OtherWrite);
                                        Console.WriteLine($"[PIPE] ✓ Set permissions 666 on {pipePath}");
                                        _logger.LogInformation($"Set pipe permissions to 666 for {pipePath}");
                                        break;
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"[PIPE] ✗ Failed to set permissions on {pipePath}: {ex.Message}");
                                        _logger.LogError($"Failed to set pipe permissions: {ex.Message}");
                                    }
                                }
                                await Task.Delay(100);
                            }
                        });
                    }

                    Console.WriteLine($"[PIPE] Created server stream, waiting for connection on '{_pipeName}'...");
                    _logger.LogInformation($"Waiting for connection on '{_pipeName}'...");
                    await server.WaitForConnectionAsync(cancellationToken);
                    Console.WriteLine("[PIPE] Client connected!");
                    _logger.LogInformation("Client connected");

                    // Capture server reference for background task
                    var serverForTask = server;
                    server = null; // Prevent disposal in finally block
                    
                    // Handle client in background task to allow concurrent connections
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await HandleClientAsync(serverForTask, cancellationToken);
                        }
                        finally
                        {
                            serverForTask?.Dispose();
                        }
                    }, cancellationToken);
                }
                finally
                {
                    // Only dispose if not handled in background task
                    if (server != null)
                    {
                        server.Dispose();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PIPE] Error in named pipe listener: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine($"[PIPE] Stack trace: {ex.StackTrace}");
                _logger.LogError(ex, "Error in named pipe listener");
                await Task.Delay(1000, cancellationToken);
            }
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream server, CancellationToken cancellationToken)
    {
        try
        {
            var buffer = new byte[4096];
            var bytesRead = await server.ReadAsync(buffer, cancellationToken);
            var requestJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            _logger.LogInformation($"Received request: {requestJson}");

            // Try to route to a module
            ApiResponse response;
            try
            {
                var request = JsonSerializer.Deserialize<JsonElement>(requestJson);

                if (request.TryGetProperty("packageId", out var packageIdEl) &&
                    request.TryGetProperty("moduleId", out var moduleIdEl) &&
                    request.TryGetProperty("action", out var actionEl))
                {
                    var packageId = packageIdEl.GetString() ?? "";
                    var moduleId = moduleIdEl.GetString() ?? "";
                    var action = actionEl.GetString() ?? "";

                    // Route to module
                    var route = _moduleRegistry.GetRoute(packageId, moduleId, action);
                    if (route != null)
                    {
                        // Extract query parameters if present
                        var query = new Dictionary<string, string>();
                        if (request.TryGetProperty("query", out var queryEl) && queryEl.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var prop in queryEl.EnumerateObject())
                            {
                                query[prop.Name] = prop.Value.GetString() ?? "";
                            }
                        }

                        // Extract body if present
                        string? body = null;
                        if (request.TryGetProperty("body", out var bodyEl))
                        {
                            body = bodyEl.GetString();
                        }

                        // Create dummy user context for now (will be real in later phases)
                        var user = new UserContext(1, "admin", new[] { "admin" }, new[] { "*" });
                        var apiRequest = new ApiRequest(
                            packageId,
                            moduleId,
                            action,
                            query,
                            body,
                            user
                        );

                        response = await route.Handler(apiRequest);
                    }
                    else
                    {
                        response = new ApiResponse(false, null, $"Route not found: {packageId}.{moduleId}.{action}");
                    }
                }
                else if (request.TryGetProperty("action", out var coreActionEl))
                {
                    var coreAction = coreActionEl.GetString() ?? "";
                    
                    if (coreAction.StartsWith("platform.", StringComparison.OrdinalIgnoreCase))
                    {
                        var platformRequest = Monolith.FireWall.Platform.Models.PlatformRequest.FromJsonElement(request);
                        platformRequest.Action = coreAction;
                        var platformResponse = await _platformExecutor.HandleAsync(platformRequest, cancellationToken);
                        response = new ApiResponse(platformResponse.Success, platformResponse, platformResponse.Error?.Message);
                    }
                    else
                    {
                        // Core system actions
                        switch (coreAction)
                        {
                            case "packages.list":
                                var installed = await _packageStateStore.GetPackagesAsync();
                                response = new ApiResponse(true, installed, null);
                                break;

                            case "get-menus":
                                var menusWithPackage = _moduleRegistry.GetAllModules()
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
                                response = new ApiResponse(true, menusWithPackage, null);
                                break;

                            case "get-all-pages":
                                var allPages = _moduleRegistry.GetAllPages().ToList();
                                response = new ApiResponse(true, new { pages = allPages }, null);
                                break;

                            case "get-pages":
                                var pages = _moduleRegistry.GetAllModules()
                                    .SelectMany(m => m.Module.GetPages())
                                    .ToList();
                                response = new ApiResponse(true, pages, null);
                                break;

                            case "get-widgets":
                                var widgets = _moduleRegistry.GetAllModules()
                                    .SelectMany(m => m.Module.GetWidgets())
                                    .ToList();
                                response = new ApiResponse(true, widgets, null);
                                break;

                            case "get-packages":
                                var packages = _moduleRegistry.GetAllPackages()
                                    .Select(p => {
                                        // Views are embedded in main assembly - get main assembly location
                                        string? viewsAssemblyPath = null;
                                        if (p.MainAssembly != null)
                                        {
                                            try
                                            {
                                                viewsAssemblyPath = p.MainAssembly.Location;
                                                if (string.IsNullOrEmpty(viewsAssemblyPath))
                                                {
                                                    var codeBase = p.MainAssembly.CodeBase;
                                                    if (!string.IsNullOrEmpty(codeBase))
                                                    {
                                                        var uri = new Uri(codeBase);
                                                        viewsAssemblyPath = uri.LocalPath;
                                                    }
                                                }
                                            }
                                            catch
                                            {
                                                // Location may not be available
                                            }
                                        }

                                        return new {
                                            id = p.Definition.Id,
                                            name = p.Definition.Name,
                                            version = p.Definition.Version,
                                            hasRazorViews = p.HasRazorViews,
                                            viewsAssemblyPath = viewsAssemblyPath,
                                            viewsAssemblyName = p.MainAssembly?.FullName, // Views are in main assembly
                                            packageDirectory = p.PackageDirectory,
                                        modules = p.Definition.GetModules().Select(m => new {
                                            id = m.Id,
                                            name = m.Name,
                                            requiredPermissions = m.GetRequiredPermissions().Select(r => r.Id).ToList(),
                                            systemPermissions = m.GetSystemPermissions().Select(sp => new {
                                                type = sp.Type.ToString(),
                                                resource = sp.Resource,
                                                justification = sp.Justification
                                            }).ToList()
                                        }).ToList()
                                    };
                                })
                                .ToList();
                                response = new ApiResponse(true, packages, null);
                                break;

                            default:
                                // Status request or other simple request
                                response = new ApiResponse(true, new {
                                    status = "ok",
                                    message = "Core is running",
                                    packages = _moduleRegistry.GetAllPackages().Count(),
                                    modules = _moduleRegistry.GetAllModules().Count()
                                }, null);
                                break;
                        }
                    }
                }
                else
                {
                    // Status request or other simple request
                    response = new ApiResponse(true, new {
                        status = "ok",
                        message = "Core is running",
                        packages = _moduleRegistry.GetAllPackages().Count(),
                        modules = _moduleRegistry.GetAllModules().Count()
                    }, null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing request");
                response = new ApiResponse(false, null, $"Error: {ex.Message}");
            }

            var responseJson = JsonSerializer.Serialize(response);
            var responseBytes = Encoding.UTF8.GetBytes(responseJson);

            await server.WriteAsync(responseBytes, cancellationToken);
            await server.FlushAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling client");
        }
    }
}
