using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Monolith.FireWall.Common.Interfaces;
using Monolith.FireWall.Common.Models;
using Monolith.FireWall.Core.Models;
using Monolith.FireWall.Core.Services;
using Monolith.FireWall.Core.Services.Firewall;
using Monolith.FireWall.Core.Transport.Handlers;

namespace Monolith.FireWall.Core.Transport;

public class UnixSocketListener
{
    private readonly ILogger _logger;
    private readonly ModuleRegistry _moduleRegistry;
    private readonly Services.Platform.PlatformExecutor _platformExecutor;
    private readonly PackageStateStore _packageStateStore;
    private readonly SetupManager _setupManager;
    private readonly PackageInstaller _packageInstaller;
    private readonly InterfaceAssignmentManager _interfaceAssignments;
    private readonly RoutingManager _routingManager;
    private readonly SystemTuneablesManager _tuneablesManager;
    private readonly MonitoringManager _monitoringManager;
    private readonly SystemSettingsManager _settingsManager;
    private readonly FirewallManager _firewallManager;
    private readonly StartupManager _startupManager;
    private readonly WebUiSettingsManager _webUiSettingsManager;
    private readonly WebUiServiceManager _webUiServiceManager;
    private readonly Services.BackupManager _backupManager;
    private readonly Services.Platform.PlatformCommandRunner _commandRunner;
    private readonly List<ICoreRequestHandler> _coreHandlers;
    private readonly string _socketPath;
    private readonly int _maxConcurrentConnections;
    private readonly CancellationTokenSource _cts;
    private Task? _listenerTask;
    private Socket? _listener;

    public UnixSocketListener(
        ILogger logger,
        ModuleRegistry moduleRegistry,
        Services.Platform.PlatformExecutor platformExecutor,
        PackageStateStore packageStateStore,
        PackageInstaller packageInstaller,
        InterfaceAssignmentManager interfaceAssignments,
        RoutingManager routingManager,
        SystemTuneablesManager tuneablesManager,
        MonitoringManager monitoringManager,
        SystemSettingsManager settingsManager,
        FirewallManager firewallManager,
        SetupManager setupManager,
        StartupManager startupManager,
        WebUiSettingsManager webUiSettingsManager,
        WebUiServiceManager webUiServiceManager,
        Services.BackupManager backupManager,
        Services.Platform.PlatformCommandRunner commandRunner,
        string socketPath = "/var/lib/monolith-firewall/run/monolith-core.sock",
        int maxConcurrentConnections = 20)
    {
        _logger = logger;
        _moduleRegistry = moduleRegistry;
        _platformExecutor = platformExecutor;
        _packageStateStore = packageStateStore;
        _setupManager = setupManager;
        _packageInstaller = packageInstaller;
        _interfaceAssignments = interfaceAssignments;
        _routingManager = routingManager;
        _tuneablesManager = tuneablesManager;
        _monitoringManager = monitoringManager;
        _settingsManager = settingsManager;
        _firewallManager = firewallManager;
        _startupManager = startupManager;
        _webUiSettingsManager = webUiSettingsManager;
        _webUiServiceManager = webUiServiceManager;
        _backupManager = backupManager;
        _commandRunner = commandRunner;
        _socketPath = socketPath;
        _maxConcurrentConnections = maxConcurrentConnections;
        _cts = new CancellationTokenSource();
        _coreHandlers = new List<ICoreRequestHandler>
        {
            new SystemMetadataHandler(),
            new InterfacesHandler(),
            new RoutingHandler(),
            new MonitoringHandler(),
            new SystemTuneablesHandler(),
            new SystemSettingsHandler(),
            new PackagesHandler(),
            new ModulesHandler(),
            new FirewallHandler(),
            new SetupHandler(_setupManager, _logger),
            new StartupHandler(),
            new WebUiSettingsHandler(),
            new BackupHandler(),
            new SystemCommandHandler(),
            new NetworkCardHandler()
        };
    }

    public void Start()
    {
        Console.WriteLine($"[SOCKET] Starting Unix socket listener on '{_socketPath}'");
        _logger.LogInformation($"Starting Unix socket listener on '{_socketPath}'");
        _listenerTask = Task.Run(() => ListenAsync(_cts.Token));
        Console.WriteLine("[SOCKET] Listener task started");
    }

    public void Stop()
    {
        _logger.LogInformation("Stopping Unix socket listener");
        _cts.Cancel();
        try
        {
            _listener?.Close();
        }
        catch
        {
            // Best effort on shutdown.
        }
        _listenerTask?.Wait();
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        try
        {
            PrepareSocketPath();

            _listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            _listener.Bind(new UnixDomainSocketEndPoint(_socketPath));
            _listener.Listen(_maxConcurrentConnections);

            TrySetSocketPermissions();

            Console.WriteLine($"[SOCKET] Waiting for connections on '{_socketPath}'...");
            _logger.LogInformation($"Waiting for connections on '{_socketPath}'...");

            while (!cancellationToken.IsCancellationRequested)
            {
                Socket client;
                try
                {
                    client = await _listener.AcceptAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await HandleClientAsync(client, cancellationToken);
                    }
                    finally
                    {
                        try
                        {
                            client.Dispose();
                        }
                        catch
                        {
                            // Ignore disposal errors.
                        }
                    }
                }, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SOCKET] Error in Unix socket listener: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"[SOCKET] Stack trace: {ex.StackTrace}");
            _logger.LogError(ex, "Error in Unix socket listener");
        }
    }

    private void PrepareSocketPath()
    {
        var dir = Path.GetDirectoryName(_socketPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        if (File.Exists(_socketPath))
        {
            File.Delete(_socketPath);
        }
    }

    private void TrySetSocketPermissions()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(_socketPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite |
                UnixFileMode.GroupRead | UnixFileMode.GroupWrite |
                UnixFileMode.OtherRead | UnixFileMode.OtherWrite);
            Console.WriteLine($"[SOCKET] ✓ Set permissions 666 on {_socketPath}");
            _logger.LogInformation($"Set socket permissions to 666 for {_socketPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SOCKET] ✗ Failed to set permissions on {_socketPath}: {ex.Message}");
            _logger.LogError($"Failed to set socket permissions: {ex.Message}");
        }
    }

    private async Task HandleClientAsync(Socket client, CancellationToken cancellationToken)
    {
        try
        {
            using var stream = new NetworkStream(client, ownsSocket: false);
            var buffer = new byte[4096];
            var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
            var requestJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            _logger.LogInformation($"Received request: {requestJson}");

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

                    var route = _moduleRegistry.GetRoute(packageId, moduleId, action);
                    if (route != null)
                    {
                        var query = new Dictionary<string, string>();
                        if (request.TryGetProperty("query", out var queryEl) && queryEl.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var prop in queryEl.EnumerateObject())
                            {
                                query[prop.Name] = prop.Value.GetString() ?? "";
                            }
                        }

                        string? body = null;
                        if (request.TryGetProperty("body", out var bodyEl))
                        {
                            body = bodyEl.GetString();
                        }

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
                        var handler = _coreHandlers.FirstOrDefault(h => h.CanHandle(coreAction));
                        if (handler != null)
                        {
                            var context = new CoreRequestContext(
                                _logger,
                                _moduleRegistry,
                                _packageStateStore,
                                _packageInstaller,
                                _interfaceAssignments,
                                _routingManager,
                                _tuneablesManager,
                                _monitoringManager,
                                _settingsManager,
                                _firewallManager,
                                _startupManager,
                                _webUiSettingsManager,
                                _webUiServiceManager,
                                _backupManager,
                                _commandRunner);
                            response = await handler.HandleAsync(context, request, cancellationToken);
                        }
                        else
                        {
                            response = new ApiResponse(true, new
                            {
                                status = "ok",
                                message = "Core is running",
                                packages = _moduleRegistry.GetAllPackages().Count(),
                                modules = _moduleRegistry.GetAllModules().Count()
                            }, null);
                        }
                    }
                }
                else
                {
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

            await stream.WriteAsync(responseBytes, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling client");
        }
    }

}
