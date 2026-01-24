using Microsoft.AspNetCore.Mvc;
using Monolith.FireWall.WebUI.Features.SystemLogs;
using Monolith.FireWall.WebUI.Features.Users.Services;
using Monolith.FireWall.WebUI.Middleware;
using Monolith.FireWall.WebUI.Services;
using System.Globalization;
using System.Text.Json;
using Monolith.FireWall.Common.Models;
using IOFile = System.IO.File;

namespace Monolith.FireWall.WebUI.Features.Dashboard;

[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly UserService _userService;
    private readonly CoreApiClient _coreClient;
    private readonly SystemLogsManager _logsManager;
    private readonly PackageDiscoveryService _packageDiscovery;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        UserService userService, 
        CoreApiClient coreClient, 
        SystemLogsManager logsManager,
        PackageDiscoveryService packageDiscovery,
        ILogger<DashboardController> logger)
    {
        _userService = userService;
        _coreClient = coreClient;
        _logsManager = logsManager;
        _packageDiscovery = packageDiscovery;
        _logger = logger;
    }

    [HttpGet("widgets")]
    public async Task<IActionResult> GetAvailableWidgets()
    {
        try
        {
            // Get widgets from Core (includes system + package widgets)
            var coreRequest = new
            {
                action = "get-widgets"
            };
            var requestJson = JsonSerializer.Serialize(coreRequest);
            var responseJson = await _coreClient.SendRequestAsync(requestJson);
            var coreResponse = JsonSerializer.Deserialize<JsonElement>(responseJson);

            // If Core returns widgets, use them (Core uses PascalCase ApiResponse)
            var isSuccess =
                (coreResponse.TryGetProperty("success", out var successProp) && successProp.ValueKind == JsonValueKind.True) ||
                (coreResponse.TryGetProperty("Success", out var successProp2) && successProp2.ValueKind == JsonValueKind.True);

            if (isSuccess)
            {
                if (coreResponse.TryGetProperty("data", out var dataProp) || coreResponse.TryGetProperty("Data", out dataProp))
                {
                    // Merge system widgets + package widgets from Core
                    var systemWidgets = new[]
                    {
                        new {
                            id = "system.info",
                            title = "System Information",
                            package = "system",
                            module = "dashboard",
                            description = "Live CPU, Memory, and Disk trends",
                            icon = "cpu",
                            defaultWidth = 4,
                            defaultHeight = 2,
                            refreshInterval = 1,
                            requiredPermissions = new[] { "system.dashboard.read" }
                        },
                        new {
                            id = "system.details",
                            title = "System Details",
                            package = "system",
                            module = "dashboard",
                            description = "Hardware, OS, DNS, and conntrack info",
                            icon = "info",
                            defaultWidth = 4,
                            defaultHeight = 3,
                            refreshInterval = 5,
                            requiredPermissions = new[] { "system.dashboard.read" }
                        },
                        new {
                            id = "system.network",
                            title = "Network Interfaces",
                            package = "system",
                            module = "dashboard",
                            description = "Network interface status",
                            icon = "network",
                            defaultWidth = 4,
                            defaultHeight = 2,
                            refreshInterval = 10,
                            requiredPermissions = new[] { "system.dashboard.read" }
                        },
                        new {
                            id = "system.traffic",
                            title = "Traffic graphs",
                            package = "system",
                            module = "dashboard",
                            description = "Throughput for Monolith-managed interfaces",
                            icon = "activity",
                            defaultWidth = 4,
                            defaultHeight = 3,
                            refreshInterval = 1,
                            requiredPermissions = new[] { "system.dashboard.read" }
                        },
                        new {
                            id = "system.activity",
                            title = "Recent Activity",
                            package = "system",
                            module = "dashboard",
                            description = "System logs and events",
                            icon = "activity",
                            defaultWidth = 4,
                            defaultHeight = 3,
                            refreshInterval = 30,
                            requiredPermissions = new[] { "system.dashboard.read" }
                        }
                    };

                    // Always include system widgets first
                    var merged = new List<object>(systemWidgets);

                    // Add package widgets from Core
                    if (dataProp.ValueKind == JsonValueKind.Array && dataProp.GetArrayLength() > 0)
                    {
                        foreach (var widgetEl in dataProp.EnumerateArray())
                        {
                            merged.Add(widgetEl);
                        }
                    }

                    return Ok(new { success = true, data = merged });
                }
            }

            // Fallback to system widgets only if Core fails
            var widgets = new[]
            {
                    new {
                        id = "system.info",
                        title = "System Information",
                    package = "system",
                    module = "dashboard",
                    description = "Live CPU, Memory, and Disk trends",
                    icon = "cpu",
                    defaultWidth = 4,
                    defaultHeight = 2,
                    refreshInterval = 1,
                    requiredPermissions = new[] { "system.dashboard.read" }
                },
                new {
                    id = "system.details",
                    title = "System Details",
                    package = "system",
                    module = "dashboard",
                    description = "Hardware, OS, DNS, and conntrack info",
                    icon = "info",
                    defaultWidth = 4,
                    defaultHeight = 3,
                    refreshInterval = 30,
                    requiredPermissions = new[] { "system.dashboard.read" }
                },
                new {
                    id = "system.network",
                    title = "Network Interfaces",
                    package = "system",
                    module = "dashboard",
                    description = "Network interface status",
                    icon = "network",
                    defaultWidth = 4,
                    defaultHeight = 2,
                    refreshInterval = 2,
                    requiredPermissions = new[] { "system.dashboard.read" }
                },
                new {
                    id = "system.traffic",
                    title = "Traffic graphs",
                    package = "system",
                    module = "dashboard",
                    description = "Throughput for Monolith-managed interfaces",
                    icon = "activity",
                    defaultWidth = 4,
                    defaultHeight = 3,
                    refreshInterval = 1,
                    requiredPermissions = new[] { "system.dashboard.read" }
                },
                new {
                    id = "system.activity",
                    title = "Recent Activity",
                    package = "system",
                    module = "dashboard",
                    description = "System logs and events",
                    icon = "activity",
                    defaultWidth = 4,
                    defaultHeight = 3,
                    refreshInterval = 30,
                    requiredPermissions = new[] { "system.dashboard.read" }
                }
            };

            return Ok(new { success = true, data = widgets });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting widgets from Core, using fallback");
            // Return system widgets as fallback
            var widgets = new[]
            {
                new {
                    id = "system.info",
                    title = "System Information",
                    package = "system",
                    module = "dashboard",
                    description = "Live CPU, Memory, and Disk trends",
                    icon = "cpu",
                    defaultWidth = 4,
                    defaultHeight = 2,
                    refreshInterval = 1,
                    requiredPermissions = new[] { "system.dashboard.read" }
                },
                new {
                    id = "system.details",
                    title = "System Details",
                    package = "system",
                    module = "dashboard",
                    description = "Hardware, OS, DNS, and conntrack info",
                    icon = "info",
                    defaultWidth = 4,
                    defaultHeight = 3,
                    refreshInterval = 5,
                    requiredPermissions = new[] { "system.dashboard.read" }
                },
                new {
                    id = "system.network",
                    title = "Network Interfaces",
                    package = "system",
                    module = "dashboard",
                    description = "Network interface status",
                    icon = "network",
                    defaultWidth = 4,
                    defaultHeight = 2,
                    refreshInterval = 2,
                    requiredPermissions = new[] { "system.dashboard.read" }
                },
                new {
                    id = "system.traffic",
                    title = "Traffic graphs",
                    package = "system",
                    module = "dashboard",
                    description = "Throughput for Monolith-managed interfaces",
                    icon = "activity",
                    defaultWidth = 4,
                    defaultHeight = 3,
                    refreshInterval = 1,
                    requiredPermissions = new[] { "system.dashboard.read" }
                },
                new {
                    id = "system.activity",
                    title = "Recent Activity",
                    package = "system",
                    module = "dashboard",
                    description = "System logs and events",
                    icon = "activity",
                    defaultWidth = 4,
                    defaultHeight = 3,
                    refreshInterval = 30,
                    requiredPermissions = new[] { "system.dashboard.read" }
                }
            };
            return Ok(new { success = true, data = widgets });
        }
    }

    [HttpGet("layout")]
    public async Task<IActionResult> GetLayout()
    {
        var user = AuthenticationMiddleware.GetUser(HttpContext);
        if (user == null)
            return Unauthorized(new { success = false, error = "Not authenticated" });

        var userEntity = await _userService.GetUserByIdAsync(user.UserId);
        if (userEntity == null)
            return NotFound(new { success = false, error = "User not found" });

        // Parse dashboard layout or return default
        if (string.IsNullOrEmpty(userEntity.DashboardLayoutJson))
        {
            var defaultLayout = new
            {
                widgets = new[]
                {
                    new { id = "system.info", order = 1, width = 4, height = 2, visible = true },
                    new { id = "system.details", order = 2, width = 4, height = 3, visible = true },
                    new { id = "system.network", order = 3, width = 4, height = 2, visible = true },
                    new { id = "system.traffic", order = 4, width = 4, height = 3, visible = true },
                    new { id = "system.activity", order = 5, width = 4, height = 3, visible = true }
                }
            };
            return Ok(new { success = true, data = defaultLayout });
        }

        try
        {
            var layout = JsonSerializer.Deserialize<object>(userEntity.DashboardLayoutJson);
            return Ok(new { success = true, data = layout });
        }
        catch
        {
            return Ok(new { success = true, data = new { widgets = Array.Empty<object>() } });
        }
    }

    [HttpPost("layout")]
    public async Task<IActionResult> SaveLayout([FromBody] JsonElement layoutData)
    {
        var user = AuthenticationMiddleware.GetUser(HttpContext);
        if (user == null)
            return Unauthorized(new { success = false, error = "Not authenticated" });

        var userEntity = await _userService.GetUserByIdAsync(user.UserId);
        if (userEntity == null)
            return NotFound(new { success = false, error = "User not found" });

        // Check if this is a reset (empty widgets array)
        if (layoutData.TryGetProperty("widgets", out var widgetsProp) && 
            widgetsProp.ValueKind == System.Text.Json.JsonValueKind.Array && 
            widgetsProp.GetArrayLength() == 0)
        {
            // Reset to default by clearing the JSON
            userEntity.DashboardLayoutJson = null;
        }
        else
        {
            userEntity.DashboardLayoutJson = layoutData.GetRawText();
        }
        
        await _userService.UpdateUserAsync(userEntity);

        return Ok(new { success = true, message = "Layout saved successfully" });
    }

    [HttpGet("widget/{id}/data")]
    public async Task<IActionResult> GetWidgetData(string id)
    {
        try
        {
            // Handle system widgets
            if (id.StartsWith("system."))
            {
                return id switch
                {
                    "system.info" => Ok(new { success = true, data = await GetSystemInfo() }),
                    "system.details" => Ok(new { success = true, data = await GetSystemDetails() }),
                    "system.network" => Ok(new { success = true, data = await GetNetworkInfo() }),
                    "system.traffic" => Ok(new { success = true, data = await GetTrafficInfo() }),
                    "system.activity" => Ok(new { success = true, data = await GetActivity() }),
                    _ => NotFound(new { success = false, error = "Widget not found" })
                };
            }

            // Handle package widgets (e.g., "network.dhcp.status")
            if (id.Contains(".") && !id.StartsWith("system."))
            {
                var parts = id.Split('.');
                if (parts.Length >= 3)
                {
                    var packagePrefix = parts[0]; // "network"
                    var module = parts[1]; // "dhcp"
                    var widgetAction = parts[2]; // "status"
                    
                    // Find the package that provides this module dynamically
                    var packageId = await _packageDiscovery.FindPackageByModuleAsync(packagePrefix);
                    if (string.IsNullOrEmpty(packageId))
                    {
                        // Try alternative: find package by module ID directly
                        packageId = await _packageDiscovery.FindPackageByModuleAsync(module);
                    }
                    
                    if (string.IsNullOrEmpty(packageId))
                    {
                        _logger.LogWarning("No package found for widget {WidgetId}", id);
                        return NotFound(new { success = false, error = $"No package provides module '{packagePrefix}' or '{module}'" });
                    }

                    // Try the new package API format first: /api/packages/{package}/modules/{module}/{action}
                    // This uses the standard package module action format
                    try
                    {
                        var packageApiRequest = new
                        {
                            packageId = packageId,
                            moduleId = module,
                            action = $"get-widget-data",
                            body = JsonSerializer.Serialize(new { widgetId = id, action = widgetAction })
                        };
                        var packageApiJson = JsonSerializer.Serialize(packageApiRequest);
                        var packageApiResponse = await _coreClient.SendRequestAsync(packageApiJson);
                        var packageApiElement = JsonSerializer.Deserialize<JsonElement>(packageApiResponse);

                        if (TryGetPropertyIgnoreCase(packageApiElement, "success", out var pkgSuccess) && pkgSuccess.GetBoolean())
                        {
                            if (TryGetPropertyIgnoreCase(packageApiElement, "data", out var pkgData))
                            {
                                return Ok(new { success = true, data = pkgData });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Package API request failed for widget {WidgetId}, trying fallback", id);
                    }

                    // Fallback: Try direct Core API format
                    try
                    {
                        var coreRequest = new
                        {
                            action = "packages.get-widget-data",
                            payload = new
                            {
                                packageId = packageId,
                                moduleId = module,
                                widgetId = id,
                                action = widgetAction
                            }
                        };
                        var requestJson = JsonSerializer.Serialize(coreRequest);
                        var responseJson = await _coreClient.SendRequestAsync(requestJson);
                        var coreResponse = JsonSerializer.Deserialize<JsonElement>(responseJson);

                        if (TryGetPropertyIgnoreCase(coreResponse, "success", out var successProp) && successProp.GetBoolean())
                        {
                            if (TryGetPropertyIgnoreCase(coreResponse, "data", out var dataProp))
                            {
                                return Ok(new { success = true, data = dataProp });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Core API request failed for widget {WidgetId}, trying fallback", id);
                    }

                    // No hardcoded fallbacks - all widgets must come from packages
                    _logger.LogWarning("Widget {WidgetId} not found in package {PackageId}", id, packageId);
                }
            }

            return NotFound(new { success = false, error = "Widget not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting widget data for {WidgetId}", id);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    private static readonly object CpuLock = new();
    private static long _lastCpuTotal;
    private static long _lastCpuIdle;

    private async Task<object> GetSystemInfo()
    {
        var cpuUsage = GetCpuUsagePercent();
        var (memUsed, memTotal, memPercent) = GetMemoryInfo();
        var (diskUsed, diskTotal, diskPercent) = GetDiskInfo();
        var uptime = GetUptimeString();

        return new
        {
            cpu = new { usage = Math.Round(cpuUsage, 1), cores = Environment.ProcessorCount },
            memory = new { used = memUsed, total = memTotal, percent = memPercent },
            disk = new { used = diskUsed, total = diskTotal, percent = diskPercent },
            uptime = uptime,
            timestamp = DateTime.UtcNow.ToString("O")
        };
    }

    private async Task<object> GetSystemDetails()
    {
        var settings = await GetSystemSettingsAsync();
        var dnsServers = settings?.DnsServers?.Count > 0 ? settings.DnsServers : ReadResolvConf();

        var hostname = settings?.Hostname ?? ReadFileTrim("/etc/hostname");
        var domain = settings?.Domain ?? string.Empty;
        var timezone = settings?.Timezone ?? ReadFileTrim("/etc/timezone");

        var osName = ReadOsReleaseValue("PRETTY_NAME") ?? ReadOsReleaseValue("NAME") ?? "Linux";
        var kernel = ReadFileTrim("/proc/sys/kernel/osrelease");
        var uptime = GetUptimeString();

        var user = AuthenticationMiddleware.GetUser(HttpContext);
        var cpuModel = ReadCpuModel();

        var conntrack = GetConntrackStats();
        var lastUpdate = ReadLastUpdate();

        return new
        {
            system = new
            {
                hostname = hostname,
                domain = domain,
                os = osName,
                kernel = kernel,
                timezone = timezone,
                uptime = uptime
            },
            hardware = new
            {
                vendor = ReadDmiValue("sys_vendor"),
                model = ReadDmiValue("product_name"),
                version = ReadDmiValue("product_version"),
                cpu = cpuModel,
                cores = Environment.ProcessorCount
            },
            bios = new
            {
                vendor = ReadDmiValue("bios_vendor"),
                version = ReadDmiValue("bios_version"),
                date = ReadDmiValue("bios_date")
            },
            user = new
            {
                name = user?.Username ?? "unknown"
            },
            dnsServers = dnsServers,
            time = new
            {
                local = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                utc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            },
            conntrack = new
            {
                count = conntrack.Count,
                max = conntrack.Max
            },
            lastUpdate = lastUpdate
        };
    }

    private async Task<object> GetNetworkInfo()
    {
        var interfaces = await GetManagedInterfacesAsync();
        var payload = interfaces.Select(iface =>
        {
            var stats = ReadInterfaceStats(iface.Name);
            var rxLossPercent = stats.RxPackets > 0 ? (stats.RxDropped * 100.0 / stats.RxPackets) : 0.0;
            var txLossPercent = stats.TxPackets > 0 ? (stats.TxDropped * 100.0 / stats.TxPackets) : 0.0;
            return new
            {
                name = iface.Name,
                status = iface.Status,
                ip = iface.IpAddress ?? "-",
                rxBytes = stats.RxBytes,
                txBytes = stats.TxBytes,
                rxLossPercent = Math.Round(rxLossPercent, 2),
                txLossPercent = Math.Round(txLossPercent, 2)
            };
        }).ToList();

        return new { interfaces = payload };
    }

    private async Task<object> GetTrafficInfo()
    {
        var interfaces = await GetManagedInterfacesAsync();
        long totalRx = 0;
        long totalTx = 0;
        long totalRxPackets = 0;
        long totalTxPackets = 0;
        long totalRxDropped = 0;
        long totalTxDropped = 0;
        var rows = new List<object>();

        foreach (var iface in interfaces)
        {
            var stats = ReadInterfaceStats(iface.Name);
            totalRx += stats.RxBytes;
            totalTx += stats.TxBytes;
            totalRxPackets += stats.RxPackets;
            totalTxPackets += stats.TxPackets;
            totalRxDropped += stats.RxDropped;
            totalTxDropped += stats.TxDropped;
            
            var rxLossPercent = stats.RxPackets > 0 ? (stats.RxDropped * 100.0 / stats.RxPackets) : 0.0;
            var txLossPercent = stats.TxPackets > 0 ? (stats.TxDropped * 100.0 / stats.TxPackets) : 0.0;
            
            rows.Add(new
            {
                name = iface.Name,
                status = iface.Status,
                rxBytes = stats.RxBytes,
                txBytes = stats.TxBytes,
                rxPackets = stats.RxPackets,
                txPackets = stats.TxPackets,
                rxDropped = stats.RxDropped,
                txDropped = stats.TxDropped,
                rxLossPercent = Math.Round(rxLossPercent, 2),
                txLossPercent = Math.Round(txLossPercent, 2)
            });
        }

        var totalRxLossPercent = totalRxPackets > 0 ? (totalRxDropped * 100.0 / totalRxPackets) : 0.0;
        var totalTxLossPercent = totalTxPackets > 0 ? (totalTxDropped * 100.0 / totalTxPackets) : 0.0;

        return new
        {
            interfaces = rows,
            totalRxBytes = totalRx,
            totalTxBytes = totalTx,
            totalRxPackets = totalRxPackets,
            totalTxPackets = totalTxPackets,
            totalRxDropped = totalRxDropped,
            totalTxDropped = totalTxDropped,
            totalRxLossPercent = Math.Round(totalRxLossPercent, 2),
            totalTxLossPercent = Math.Round(totalTxLossPercent, 2),
            sampledAt = DateTime.UtcNow.ToString("O")
        };
    }

    private async Task<object> GetActivity()
    {
        try
        {
            var query = new LogQueryParams
            {
                Limit = 6,
                Offset = 0
            };

            var result = await _logsManager.QueryMonolithLogsAsync(query);
            return new
            {
                logs = result.Logs.Select(log => new
                {
                    time = log.Timestamp.ToString("HH:mm:ss"),
                    type = log.Level.ToLowerInvariant(),
                    message = log.Message,
                    source = log.Source,
                    category = log.Category
                }).ToArray()
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load activity logs for dashboard");
            return new { logs = Array.Empty<object>() };
        }
    }

    private async Task<object> GetDhcpStatus()
    {
        try
        {
            // Try to get DHCP status from Core
            var coreRequest = new
            {
                package = "monolith-network",
                module = "dhcp",
                action = "list-leases"
            };
            var requestJson = JsonSerializer.Serialize(coreRequest);
            var responseJson = await _coreClient.SendRequestAsync(requestJson);
            var coreResponse = JsonSerializer.Deserialize<JsonElement>(responseJson);

            int activeLeases = 0;
            if (coreResponse.TryGetProperty("success", out var successProp) && successProp.GetBoolean())
            {
                if (coreResponse.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    activeLeases = dataProp.GetArrayLength();
                }
            }

            return new
            {
                enabled = true,
                activeLeases = activeLeases,
                status = "Running",
                poolSize = 100,
                leases = new[]
                {
                    new { ip = "192.168.1.100", mac = "aa:bb:cc:dd:ee:ff", hostname = "laptop", expires = "2h 30m" },
                    new { ip = "192.168.1.101", mac = "11:22:33:44:55:66", hostname = "phone", expires = "1h 15m" }
                }
            };
        }
        catch
        {
            // Fallback data
            return new
            {
                enabled = true,
                activeLeases = 2,
                status = "Running",
                poolSize = 100,
                leases = new[]
                {
                    new { ip = "192.168.1.100", mac = "aa:bb:cc:dd:ee:ff", hostname = "laptop", expires = "2h 30m" },
                    new { ip = "192.168.1.101", mac = "11:22:33:44:55:66", hostname = "phone", expires = "1h 15m" }
                }
            };
        }
    }

    private async Task<List<InterfaceSnapshot>> GetManagedInterfacesAsync()
    {
        var data = await GetCoreDataAsync(new { action = "interfaces.assignments.list" });
        if (data == null || data.Value.ValueKind != JsonValueKind.Object)
        {
            return new List<InterfaceSnapshot>();
        }

        var interfaces = new List<InterfaceSnapshot>();
        foreach (var section in new[] { "Assigned", "Vlans", "Bridges" })
        {
            foreach (var item in GetArrayProperty(data.Value, section))
            {
                var name = GetStringProperty(item, "Interface");
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var status = GetStringProperty(item, "Status") ?? "unknown";
                var ip = GetStringProperty(item, "IpAddress");
                interfaces.Add(new InterfaceSnapshot(name, status, ip));
            }
        }

        return interfaces;
    }

    private async Task<SystemSettingsSnapshot?> GetSystemSettingsAsync()
    {
        var data = await GetCoreDataAsync(new { action = "system.settings.get" });
        if (data == null || data.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var hostname = GetStringProperty(data.Value, "Hostname");
        var domain = GetStringProperty(data.Value, "Domain");
        var timezone = GetStringProperty(data.Value, "Timezone");
        var dnsServers = new List<string>();

        if (TryGetPropertyIgnoreCase(data.Value, "DnsServers", out var dnsProp) && dnsProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in dnsProp.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var value = item.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        dnsServers.Add(value);
                    }
                }
            }
        }

        return new SystemSettingsSnapshot(hostname, domain, timezone, dnsServers);
    }

    private async Task<JsonElement?> GetCoreDataAsync(object coreRequest)
    {
        try
        {
            var requestJson = JsonSerializer.Serialize(coreRequest);
            var responseJson = await _coreClient.SendRequestAsync(requestJson);
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            if (!IsCoreSuccess(root))
            {
                return null;
            }

            return TryGetPropertyIgnoreCase(root, "data", out var data) ? data.Clone() : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Core request failed: {Request}", coreRequest);
            return null;
        }
    }

    private static bool IsCoreSuccess(JsonElement root)
    {
        if (TryGetPropertyIgnoreCase(root, "success", out var successProp))
        {
            return successProp.ValueKind == JsonValueKind.True;
        }

        return false;
    }

    private static IEnumerable<JsonElement> GetArrayProperty(JsonElement element, string name)
    {
        if (TryGetPropertyIgnoreCase(element, name, out var value) && value.ValueKind == JsonValueKind.Array)
        {
            return value.EnumerateArray();
        }

        return Array.Empty<JsonElement>();
    }

    private static string? GetStringProperty(JsonElement element, string name)
    {
        if (TryGetPropertyIgnoreCase(element, name, out var value))
        {
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                _ => null
            };
        }

        return null;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static double GetCpuUsagePercent()
    {
        try
        {
            var line = IOFile.ReadLines("/proc/stat").FirstOrDefault();
            if (string.IsNullOrWhiteSpace(line))
            {
                return 0;
            }

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5)
            {
                return 0;
            }

            var values = parts.Skip(1)
                .Select(p => long.TryParse(p, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0)
                .ToArray();

            var idle = values.Length > 3 ? values[3] : 0;
            var iowait = values.Length > 4 ? values[4] : 0;
            var idleTotal = idle + iowait;
            var total = values.Sum();

            lock (CpuLock)
            {
                if (_lastCpuTotal == 0)
                {
                    _lastCpuTotal = total;
                    _lastCpuIdle = idleTotal;
                    return 0;
                }

                var totalDiff = total - _lastCpuTotal;
                var idleDiff = idleTotal - _lastCpuIdle;
                _lastCpuTotal = total;
                _lastCpuIdle = idleTotal;

                if (totalDiff <= 0)
                {
                    return 0;
                }

                var usage = (1.0 - idleDiff / (double)totalDiff) * 100.0;
                return Math.Clamp(usage, 0, 100);
            }
        }
        catch
        {
            return 0;
        }
    }

    private static (long UsedMb, long TotalMb, int Percent) GetMemoryInfo()
    {
        try
        {
            long totalKb = 0;
            long availableKb = 0;

            foreach (var line in IOFile.ReadLines("/proc/meminfo"))
            {
                if (line.StartsWith("MemTotal:", StringComparison.OrdinalIgnoreCase))
                {
                    totalKb = ParseMeminfoValue(line);
                }
                else if (line.StartsWith("MemAvailable:", StringComparison.OrdinalIgnoreCase))
                {
                    availableKb = ParseMeminfoValue(line);
                }
            }

            if (totalKb <= 0)
            {
                return (0, 0, 0);
            }

            var usedKb = Math.Max(0, totalKb - availableKb);
            var totalMb = totalKb / 1024;
            var usedMb = usedKb / 1024;
            var percent = (int)Math.Round(usedKb * 100d / totalKb);
            return (usedMb, totalMb, Math.Clamp(percent, 0, 100));
        }
        catch
        {
            return (0, 0, 0);
        }
    }

    private static long ParseMeminfoValue(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        return 0;
    }

    private static (long UsedMb, long TotalMb, int Percent) GetDiskInfo()
    {
        try
        {
            var drive = new DriveInfo("/");
            var total = drive.TotalSize;
            var free = drive.AvailableFreeSpace;
            var used = Math.Max(0, total - free);
            var totalMb = total / 1024 / 1024;
            var usedMb = used / 1024 / 1024;
            var percent = total > 0 ? (int)Math.Round(used * 100d / total) : 0;
            return (usedMb, totalMb, Math.Clamp(percent, 0, 100));
        }
        catch
        {
            return (0, 0, 0);
        }
    }

    private static string GetUptimeString()
    {
        try
        {
            var contents = IOFile.ReadAllText("/proc/uptime").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (contents.Length > 0 && double.TryParse(contents[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
            {
                var uptime = TimeSpan.FromSeconds(seconds);
                return $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m";
            }
        }
        catch
        {
            // ignore
        }

        var fallback = TimeSpan.FromSeconds(Environment.TickCount64 / 1000d);
        return $"{fallback.Days}d {fallback.Hours}h {fallback.Minutes}m";
    }

    private static string ReadFileTrim(string path)
    {
        try
        {
            return IOFile.Exists(path) ? IOFile.ReadAllText(path).Trim() : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ReadOsReleaseValue(string key)
    {
        try
        {
            if (!IOFile.Exists("/etc/os-release"))
            {
                return string.Empty;
            }

            foreach (var line in IOFile.ReadLines("/etc/os-release"))
            {
                if (line.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
                {
                    return line.Substring(key.Length + 1).Trim().Trim('"');
                }
            }
        }
        catch
        {
            return string.Empty;
        }

        return string.Empty;
    }

    private static string ReadDmiValue(string name)
    {
        var path = $"/sys/class/dmi/id/{name}";
        return ReadFileTrim(path);
    }

    private static string ReadCpuModel()
    {
        try
        {
            foreach (var line in IOFile.ReadLines("/proc/cpuinfo"))
            {
                if (line.StartsWith("model name", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        return parts[1].Trim();
                    }
                }
            }
        }
        catch
        {
            return string.Empty;
        }

        return string.Empty;
    }

    private static (long Count, long Max) GetConntrackStats()
    {
        var count = ReadLongFromFile("/proc/sys/net/netfilter/nf_conntrack_count");
        var max = ReadLongFromFile("/proc/sys/net/netfilter/nf_conntrack_max");
        return (count, max);
    }

    private static string? ReadLastUpdate()
    {
        try
        {
            var path = "/var/log/apt/history.log";
            if (!IOFile.Exists(path))
            {
                return null;
            }

            string? lastStart = null;
            foreach (var line in IOFile.ReadLines(path))
            {
                if (line.StartsWith("Start-Date:", StringComparison.OrdinalIgnoreCase))
                {
                    lastStart = line.Substring("Start-Date:".Length).Trim();
                }
            }

            return lastStart;
        }
        catch
        {
            return null;
        }
    }

    private static List<string> ReadResolvConf()
    {
        var servers = new List<string>();
        try
        {
            if (!IOFile.Exists("/etc/resolv.conf"))
            {
                return servers;
            }

            foreach (var line in IOFile.ReadLines("/etc/resolv.conf"))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("nameserver ", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        servers.Add(parts[1]);
                    }
                }
            }
        }
        catch
        {
            return servers;
        }

        return servers;
    }

    private static InterfaceStats ReadInterfaceStats(string name)
    {
        var basePath = $"/sys/class/net/{name}/statistics";
        var rx = ReadLongFromFile(Path.Combine(basePath, "rx_bytes"));
        var tx = ReadLongFromFile(Path.Combine(basePath, "tx_bytes"));
        var rxPackets = ReadLongFromFile(Path.Combine(basePath, "rx_packets"));
        var txPackets = ReadLongFromFile(Path.Combine(basePath, "tx_packets"));
        var rxDropped = ReadLongFromFile(Path.Combine(basePath, "rx_dropped"));
        var txDropped = ReadLongFromFile(Path.Combine(basePath, "tx_dropped"));
        return new InterfaceStats(rx, tx, rxPackets, txPackets, rxDropped, txDropped);
    }

    private static long ReadLongFromFile(string path)
    {
        try
        {
            if (!IOFile.Exists(path))
            {
                return 0;
            }

            var content = IOFile.ReadAllText(path).Trim();
            return long.TryParse(content, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
        }
        catch
        {
            return 0;
        }
    }

    private sealed record InterfaceSnapshot(string Name, string Status, string? IpAddress);
    private sealed record InterfaceStats(long RxBytes, long TxBytes, long RxPackets, long TxPackets, long RxDropped, long TxDropped);
    private sealed record SystemSettingsSnapshot(string? Hostname, string? Domain, string? Timezone, List<string> DnsServers);
}
