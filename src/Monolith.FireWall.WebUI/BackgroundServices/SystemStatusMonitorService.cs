using System.Text.Json;
using Monolith.FireWall.WebUI.Hubs;
using Monolith.FireWall.WebUI.Hubs.Events;
using Monolith.FireWall.WebUI.Services;

namespace Monolith.FireWall.WebUI.BackgroundServices;

/// <summary>
/// Background service that monitors system status and broadcasts changes via SignalR.
/// Polls the Core service at configurable intervals and notifies connected clients
/// only when actual changes occur.
/// </summary>
public sealed class SystemStatusMonitorService : BackgroundService
{
    private readonly ILogger<SystemStatusMonitorService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly SystemEventsNotifier _notifier;

    // Poll intervals
    private static readonly TimeSpan InterfaceInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan GatewayInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan SystemInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ServiceInterval = TimeSpan.FromSeconds(30);

    // Previous state for change detection
    private Dictionary<string, InterfaceStatusEvent> _previousInterfaceStatus = new();
    private Dictionary<int, GatewayStatusEvent> _previousGatewayStatus = new();
    private SystemMetricsEvent? _previousSystemMetrics;
    private Dictionary<string, ServiceStatusEvent> _previousServiceStatus = new();

    public SystemStatusMonitorService(
        ILogger<SystemStatusMonitorService> logger,
        IServiceProvider serviceProvider,
        SystemEventsNotifier notifier)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _notifier = notifier;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("System status monitor service started");

        // Run parallel polling tasks
        var interfaceTask = PollInterfaceStatusAsync(stoppingToken);
        var gatewayTask = PollGatewayStatusAsync(stoppingToken);
        var systemTask = PollSystemMetricsAsync(stoppingToken);
        var serviceTask = PollServiceStatusAsync(stoppingToken);

        await Task.WhenAll(interfaceTask, gatewayTask, systemTask, serviceTask);

        _logger.LogInformation("System status monitor service stopped");
    }

    private async Task PollInterfaceStatusAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await FetchAndBroadcastInterfaceStatusAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling interface status");
            }

            await Task.Delay(InterfaceInterval, stoppingToken);
        }
    }

    private async Task PollGatewayStatusAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await FetchAndBroadcastGatewayStatusAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling gateway status");
            }

            await Task.Delay(GatewayInterval, stoppingToken);
        }
    }

    private async Task PollSystemMetricsAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await FetchAndBroadcastSystemMetricsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling system metrics");
            }

            await Task.Delay(SystemInterval, stoppingToken);
        }
    }

    private async Task PollServiceStatusAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await FetchAndBroadcastServiceStatusAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling service status");
            }

            await Task.Delay(ServiceInterval, stoppingToken);
        }
    }

    private async Task FetchAndBroadcastInterfaceStatusAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var coreClient = scope.ServiceProvider.GetRequiredService<CoreApiClient>();

        try
        {
            // Request interface status from Core
            var request = new { action = "interfaces.status.list" };
            var responseJson = await coreClient.SendRequestAsync(JsonSerializer.Serialize(request));

            if (string.IsNullOrEmpty(responseJson))
            {
                return;
            }

            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("Success", out var successProp) || !successProp.GetBoolean())
            {
                return;
            }

            if (!root.TryGetProperty("Data", out var dataProp))
            {
                return;
            }

            var changedInterfaces = new List<InterfaceStatusEvent>();

            foreach (var item in dataProp.EnumerateArray())
            {
                var interfaceName = item.GetProperty("name").GetString() ?? "";
                var status = item.TryGetProperty("status", out var s) ? s.GetString() ?? "down" : "down";
                var ipAddress = item.TryGetProperty("ipAddress", out var ip) ? ip.GetString() : null;
                var ipv6Address = item.TryGetProperty("ipv6Address", out var ip6) ? ip6.GetString() : null;
                var rxBytes = item.TryGetProperty("rxBytes", out var rx) ? rx.GetInt64() : 0;
                var txBytes = item.TryGetProperty("txBytes", out var tx) ? tx.GetInt64() : 0;
                var macAddress = item.TryGetProperty("macAddress", out var mac) ? mac.GetString() : null;
                var linkSpeed = item.TryGetProperty("linkSpeedMbps", out var ls) ? ls.GetInt32() : (int?)null;

                var currentEvent = new InterfaceStatusEvent
                {
                    InterfaceName = interfaceName,
                    Status = status,
                    IpAddress = ipAddress,
                    Ipv6Address = ipv6Address,
                    RxBytes = rxBytes,
                    TxBytes = txBytes,
                    MacAddress = macAddress,
                    LinkSpeedMbps = linkSpeed,
                    Timestamp = DateTime.UtcNow
                };

                // Calculate bytes per second if we have previous data
                if (_previousInterfaceStatus.TryGetValue(interfaceName, out var prev))
                {
                    var timeDiff = (currentEvent.Timestamp - prev.Timestamp).TotalSeconds;
                    if (timeDiff > 0)
                    {
                        currentEvent = currentEvent with
                        {
                            RxBytesPerSec = (long)((rxBytes - prev.RxBytes) / timeDiff),
                            TxBytesPerSec = (long)((txBytes - prev.TxBytes) / timeDiff)
                        };
                    }

                    // Check if status changed
                    if (prev.Status != currentEvent.Status ||
                        prev.IpAddress != currentEvent.IpAddress ||
                        prev.Ipv6Address != currentEvent.Ipv6Address)
                    {
                        changedInterfaces.Add(currentEvent);
                    }
                }
                else
                {
                    // New interface
                    changedInterfaces.Add(currentEvent);
                }

                _previousInterfaceStatus[interfaceName] = currentEvent;
            }

            // Broadcast changes
            if (changedInterfaces.Count > 0)
            {
                if (changedInterfaces.Count == 1)
                {
                    await _notifier.NotifyInterfaceStatusChangedAsync(changedInterfaces[0]);
                }
                else
                {
                    await _notifier.NotifyInterfaceStatusBatchAsync(changedInterfaces);
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse interface status response");
        }
    }

    private async Task FetchAndBroadcastGatewayStatusAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var coreClient = scope.ServiceProvider.GetRequiredService<CoreApiClient>();

        try
        {
            // Request gateway status from Core
            var request = new { action = "routing.gateways.status" };
            var responseJson = await coreClient.SendRequestAsync(JsonSerializer.Serialize(request));

            if (string.IsNullOrEmpty(responseJson))
            {
                return;
            }

            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("Success", out var successProp) || !successProp.GetBoolean())
            {
                return;
            }

            if (!root.TryGetProperty("Data", out var dataProp))
            {
                return;
            }

            var changedGateways = new List<GatewayStatusEvent>();

            foreach (var item in dataProp.EnumerateArray())
            {
                var gatewayId = item.TryGetProperty("id", out var id) ? id.GetInt32() : 0;
                var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                var address = item.TryGetProperty("address", out var a) ? a.GetString() ?? "" : "";
                var status = item.TryGetProperty("status", out var s) ? s.GetString() ?? "offline" : "offline";
                var latencyMs = item.TryGetProperty("latencyMs", out var lat) ? lat.GetInt32() : (int?)null;
                var packetLoss = item.TryGetProperty("packetLossPercent", out var pl) ? pl.GetDouble() : 0;
                var iface = item.TryGetProperty("interface", out var i) ? i.GetString() : null;
                var isDefault = item.TryGetProperty("isDefault", out var d) && d.GetBoolean();

                var currentEvent = new GatewayStatusEvent
                {
                    GatewayId = gatewayId,
                    Name = name,
                    Address = address,
                    Status = status,
                    LatencyMs = latencyMs,
                    PacketLossPercent = packetLoss,
                    Interface = iface,
                    IsDefault = isDefault,
                    Timestamp = DateTime.UtcNow
                };

                // Check if status changed
                if (_previousGatewayStatus.TryGetValue(gatewayId, out var prev))
                {
                    if (prev.Status != currentEvent.Status ||
                        prev.LatencyMs != currentEvent.LatencyMs)
                    {
                        changedGateways.Add(currentEvent);
                    }
                }
                else
                {
                    // New gateway
                    changedGateways.Add(currentEvent);
                }

                _previousGatewayStatus[gatewayId] = currentEvent;
            }

            // Broadcast changes
            if (changedGateways.Count > 0)
            {
                if (changedGateways.Count == 1)
                {
                    await _notifier.NotifyGatewayStatusChangedAsync(changedGateways[0]);
                }
                else
                {
                    await _notifier.NotifyGatewayStatusBatchAsync(changedGateways);
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse gateway status response");
        }
    }

    private async Task FetchAndBroadcastSystemMetricsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var coreClient = scope.ServiceProvider.GetRequiredService<CoreApiClient>();

        try
        {
            // Request system metrics from Core
            var request = new { action = "monitoring.system.metrics" };
            var responseJson = await coreClient.SendRequestAsync(JsonSerializer.Serialize(request));

            if (string.IsNullOrEmpty(responseJson))
            {
                return;
            }

            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("Success", out var successProp) || !successProp.GetBoolean())
            {
                return;
            }

            if (!root.TryGetProperty("Data", out var dataProp))
            {
                return;
            }

            var cpuPercent = dataProp.TryGetProperty("cpuPercent", out var cpu) ? cpu.GetDouble() : 0;
            var memPercent = dataProp.TryGetProperty("memoryPercent", out var mem) ? mem.GetDouble() : 0;
            var memUsed = dataProp.TryGetProperty("memoryUsedBytes", out var memU) ? memU.GetInt64() : 0;
            var memTotal = dataProp.TryGetProperty("memoryTotalBytes", out var memT) ? memT.GetInt64() : 0;
            var diskPercent = dataProp.TryGetProperty("diskPercent", out var disk) ? disk.GetDouble() : 0;
            var diskUsed = dataProp.TryGetProperty("diskUsedBytes", out var diskU) ? diskU.GetInt64() : 0;
            var diskTotal = dataProp.TryGetProperty("diskTotalBytes", out var diskT) ? diskT.GetInt64() : 0;
            var uptime = dataProp.TryGetProperty("uptimeSeconds", out var up) ? up.GetInt64() : 0;
            var load1 = dataProp.TryGetProperty("loadAverage1", out var l1) ? l1.GetDouble() : 0;
            var load5 = dataProp.TryGetProperty("loadAverage5", out var l5) ? l5.GetDouble() : 0;
            var load15 = dataProp.TryGetProperty("loadAverage15", out var l15) ? l15.GetDouble() : 0;

            // Determine alert level
            string? alertLevel = null;
            if (cpuPercent > 90 || memPercent > 90 || diskPercent > 95)
            {
                alertLevel = "critical";
            }
            else if (cpuPercent > 75 || memPercent > 80 || diskPercent > 85)
            {
                alertLevel = "warning";
            }

            var currentEvent = new SystemMetricsEvent
            {
                CpuPercent = cpuPercent,
                MemoryPercent = memPercent,
                MemoryUsedBytes = memUsed,
                MemoryTotalBytes = memTotal,
                DiskPercent = diskPercent,
                DiskUsedBytes = diskUsed,
                DiskTotalBytes = diskTotal,
                UptimeSeconds = uptime,
                LoadAverage1 = load1,
                LoadAverage5 = load5,
                LoadAverage15 = load15,
                AlertLevel = alertLevel,
                Timestamp = DateTime.UtcNow
            };

            // Always broadcast system metrics (dashboard widgets need continuous updates)
            await _notifier.NotifySystemMetricsAsync(currentEvent);
            _previousSystemMetrics = currentEvent;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse system metrics response");
        }
    }

    private async Task FetchAndBroadcastServiceStatusAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var coreClient = scope.ServiceProvider.GetRequiredService<CoreApiClient>();

        try
        {
            // Request service status from Core
            var request = new { action = "services.status.list" };
            var responseJson = await coreClient.SendRequestAsync(JsonSerializer.Serialize(request));

            if (string.IsNullOrEmpty(responseJson))
            {
                return;
            }

            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("Success", out var successProp) || !successProp.GetBoolean())
            {
                return;
            }

            if (!root.TryGetProperty("Data", out var dataProp))
            {
                return;
            }

            var changedServices = new List<ServiceStatusEvent>();

            foreach (var item in dataProp.EnumerateArray())
            {
                var serviceName = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                var displayName = item.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? serviceName : serviceName;
                var status = item.TryGetProperty("status", out var s) ? s.GetString() ?? "stopped" : "stopped";
                var isEnabled = item.TryGetProperty("isEnabled", out var e) && e.GetBoolean();
                var pid = item.TryGetProperty("pid", out var p) ? p.GetInt32() : (int?)null;
                var memBytes = item.TryGetProperty("memoryBytes", out var m) ? m.GetInt64() : (long?)null;
                var moduleId = item.TryGetProperty("moduleId", out var mid) ? mid.GetString() : null;

                var currentEvent = new ServiceStatusEvent
                {
                    ServiceName = serviceName,
                    DisplayName = displayName,
                    Status = status,
                    IsEnabled = isEnabled,
                    Pid = pid,
                    MemoryBytes = memBytes,
                    ModuleId = moduleId,
                    Timestamp = DateTime.UtcNow
                };

                // Check if status changed
                if (_previousServiceStatus.TryGetValue(serviceName, out var prev))
                {
                    if (prev.Status != currentEvent.Status ||
                        prev.IsEnabled != currentEvent.IsEnabled)
                    {
                        changedServices.Add(currentEvent);
                    }
                }
                else
                {
                    // New service
                    changedServices.Add(currentEvent);
                }

                _previousServiceStatus[serviceName] = currentEvent;
            }

            // Broadcast changes
            if (changedServices.Count > 0)
            {
                if (changedServices.Count == 1)
                {
                    await _notifier.NotifyServiceStatusChangedAsync(changedServices[0]);
                }
                else
                {
                    await _notifier.NotifyServiceStatusBatchAsync(changedServices);
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse service status response");
        }
    }
}
