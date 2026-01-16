using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Monolith.FireWall.Common.Services;
using Monolith.FireWall.Core.Models;
using Monolith.FireWall.Core.Services.Platform;

namespace Monolith.FireWall.Core.Services;

public sealed class MonitoringManager
{
    private readonly MonitoringStore _store;
    private readonly PlatformCommandRunner _commandRunner;
    private readonly RoutingManager _routingManager;
    private readonly LoggingManager _loggingManager;
    private Task? _loopTask;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Regex PingLatencyRegex = new(@"time=([0-9\.]+)\s*ms", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public MonitoringManager(MonitoringStore store, PlatformCommandRunner commandRunner, RoutingManager routingManager)
    {
        _store = store;
        _commandRunner = commandRunner;
        _routingManager = routingManager;
        _loggingManager = LoggingManager.Instance;
    }

    public void Start(CancellationToken cancellationToken)
    {
        if (_loopTask != null)
        {
            return;
        }

        _loopTask = Task.Run(() => RunLoopAsync(cancellationToken), cancellationToken);
    }

    public async Task<List<MonitorStatusView>> GetMonitorStatusAsync()
    {
        var definitions = await _store.GetDefinitionsAsync();
        var statuses = await _store.GetStatusesAsync();
        var statusMap = statuses.ToDictionary(s => s.MonitorKey, StringComparer.OrdinalIgnoreCase);

        return definitions
            .OrderBy(d => d.Name)
            .Select(def =>
            {
                statusMap.TryGetValue(def.Key, out var status);
                return new MonitorStatusView
                {
                    Key = def.Key,
                    Name = def.Name,
                    Type = def.Type,
                    Enabled = def.Enabled,
                    IntervalSeconds = def.IntervalSeconds,
                    Status = status?.Status ?? "unknown",
                    Message = status?.Message,
                    LastCheckAt = status?.LastCheckAt,
                    LastSuccessAt = status?.LastSuccessAt,
                    LastFailureAt = status?.LastFailureAt,
                    LastDurationMs = status?.LastDurationMs,
                    LastLatencyMs = status?.LastLatencyMs,
                    ConsecutiveFailures = status?.ConsecutiveFailures ?? 0
                };
            })
            .ToList();
    }

    public async Task<(bool Success, string? Error)> UpdateMonitorAsync(MonitorUpdateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Key))
        {
            return (false, "Monitor key is required");
        }

        var definition = await _store.GetDefinitionAsync(request.Key);
        if (definition == null)
        {
            return (false, "Monitor not found");
        }

        if (request.IntervalSeconds.HasValue && request.IntervalSeconds.Value < 5)
        {
            return (false, "Interval must be at least 5 seconds");
        }

        if (request.Enabled.HasValue)
        {
            definition.Enabled = request.Enabled.Value;
        }

        if (request.IntervalSeconds.HasValue)
        {
            definition.IntervalSeconds = request.IntervalSeconds.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.ConfigJson))
        {
            definition.ConfigJson = request.ConfigJson;
        }

        definition.UpdatedAt = DateTime.UtcNow;
        var saved = await _store.UpsertDefinitionAsync(definition);
        return saved ? (true, null) : (false, "Failed to update monitor");
    }

    public async Task<NotificationSummaryView> GetNotificationsAsync(NotificationQuery query)
    {
        var limit = query.Limit < 1 ? 20 : Math.Min(query.Limit, 100);
        var notifications = await _store.GetNotificationsAsync(limit, query.UnreadOnly);
        var unreadCount = await _store.GetUnreadCountAsync();

        return new NotificationSummaryView
        {
            Notifications = notifications.Select(ToView).ToList(),
            UnreadCount = unreadCount
        };
    }

    public async Task<(bool Success, string? Error)> MarkNotificationsReadAsync(NotificationReadRequest request)
    {
        if (request.All)
        {
            var ok = await _store.MarkAllReadAsync();
            return ok ? (true, null) : (false, "Failed to mark notifications read");
        }

        var okIds = await _store.MarkNotificationsReadAsync(request.Ids ?? new List<int>());
        return okIds ? (true, null) : (false, "Failed to mark notifications read");
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        await EnsureDefaultsAsync();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var definitions = await _store.GetDefinitionsAsync();
                var statuses = await _store.GetStatusesAsync();
                var statusMap = statuses.ToDictionary(s => s.MonitorKey, StringComparer.OrdinalIgnoreCase);

                var now = DateTime.UtcNow;
                var nextRuns = new List<DateTime>();

                foreach (var definition in definitions.Where(d => d.Enabled))
                {
                    statusMap.TryGetValue(definition.Key, out var status);
                    var lastCheck = status?.LastCheckAt;
                    var interval = TimeSpan.FromSeconds(Math.Max(5, definition.IntervalSeconds));
                    var due = !lastCheck.HasValue || now - lastCheck.Value >= interval;

                    var nextBase = lastCheck ?? now;
                    if (due)
                    {
                        await RunMonitorAsync(definition, status, cancellationToken);
                        now = DateTime.UtcNow;
                        nextBase = now;
                    }

                    var nextRun = nextBase.Add(interval);
                    nextRuns.Add(nextRun);
                }

                var delay = ComputeDelay(nextRuns, now);
                await Task.Delay(delay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                await _loggingManager.LogSystemAsync(
                    "Monitoring",
                    "error",
                    "MonitoringManager",
                    "Monitor loop error",
                    new Dictionary<string, object> { ["error"] = ex.Message });
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
    }

    private async Task EnsureDefaultsAsync()
    {
        var defaults = new List<MonitorDefinitionEntity>
        {
            new()
            {
                Key = "gateway",
                Name = "Gateway Reachability",
                Type = "gateway",
                IntervalSeconds = 15,
                Enabled = true,
                ConfigJson = JsonSerializer.Serialize(new GatewayMonitorConfig
                {
                    UseGateway = true,
                    TimeoutMs = 1000
                }),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Key = "system-health",
                Name = "System Health",
                Type = "system",
                IntervalSeconds = 60,
                Enabled = true,
                ConfigJson = JsonSerializer.Serialize(new SystemHealthMonitorConfig
                {
                    CpuWarn = 80,
                    CpuCrit = 95,
                    MemWarn = 80,
                    MemCrit = 95,
                    DiskWarn = 85,
                    DiskCrit = 95
                }),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Key = "services",
                Name = "Core Services",
                Type = "services",
                IntervalSeconds = 60,
                Enabled = true,
                ConfigJson = JsonSerializer.Serialize(new ServiceMonitorConfig
                {
                    RequireWebUi = true
                }),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Key = "gateway-sync",
                Name = "Gateway Sync",
                Type = "gateway-sync",
                IntervalSeconds = 60,
                Enabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        foreach (var def in defaults)
        {
            var existing = await _store.GetDefinitionAsync(def.Key);
            if (existing == null)
            {
                await _store.UpsertDefinitionAsync(def);
            }
        }
    }

    private async Task RunMonitorAsync(
        MonitorDefinitionEntity definition,
        MonitorStatusEntity? previous,
        CancellationToken cancellationToken)
    {
        var started = DateTime.UtcNow;
        var result = await ExecuteMonitorAsync(definition, cancellationToken);
        var durationMs = (int)Math.Max(0, (DateTime.UtcNow - started).TotalMilliseconds);

        var status = previous ?? new MonitorStatusEntity
        {
            MonitorKey = definition.Key
        };

        status.Status = result.Status;
        status.Message = result.Message;
        status.LastCheckAt = started;
        status.LastDurationMs = durationMs;
        status.LastLatencyMs = result.LatencyMs;
        status.UpdatedAt = DateTime.UtcNow;

        if (result.Status == "ok")
        {
            status.LastSuccessAt = started;
            status.ConsecutiveFailures = 0;
        }
        else
        {
            status.LastFailureAt = started;
            status.ConsecutiveFailures = previous?.ConsecutiveFailures + 1 ?? 1;
        }

        await _store.UpsertStatusAsync(status);
        await HandleStatusChangeAsync(definition, previous, status, result);
    }

    private async Task<MonitorExecutionResult> ExecuteMonitorAsync(
        MonitorDefinitionEntity definition,
        CancellationToken cancellationToken)
    {
        return definition.Type switch
        {
            "gateway" => await RunGatewayMonitorAsync(definition, cancellationToken),
            "system" => await RunSystemMonitorAsync(definition, cancellationToken),
            "services" => await RunServiceMonitorAsync(definition, cancellationToken),
            "gateway-sync" => await RunGatewaySyncMonitorAsync(definition, cancellationToken),
            _ => new MonitorExecutionResult
            {
                Status = "unknown",
                Message = $"Unknown monitor type '{definition.Type}'"
            }
        };
    }

    private async Task<MonitorExecutionResult> RunGatewayMonitorAsync(
        MonitorDefinitionEntity definition,
        CancellationToken cancellationToken)
    {
        var config = DeserializeConfig(definition.ConfigJson, new GatewayMonitorConfig
        {
            UseGateway = true,
            TimeoutMs = 1000
        });

        var target = config.Target?.Trim();
        if (config.UseGateway || string.IsNullOrWhiteSpace(target))
        {
            var gateways = await _routingManager.GetGatewaysAsync(cancellationToken);
            target = gateways.FirstOrDefault()?.Address;
        }

        if (string.IsNullOrWhiteSpace(target))
        {
            return new MonitorExecutionResult
            {
                Status = "warning",
                Message = "Gateway not detected"
            };
        }

        if (!_commandRunner.CommandExists("ping"))
        {
            return new MonitorExecutionResult
            {
                Status = "warning",
                Message = "Ping utility not available"
            };
        }

        var waitSeconds = Math.Max(1, (config.TimeoutMs ?? 1000) / 1000);
        var command = new PlatformCommand
        {
            FileName = "ping",
            Arguments = $"-c 1 -W {waitSeconds} {target}",
            UseSudo = false,
            TimeoutMs = Math.Max(2000, (config.TimeoutMs ?? 1000) + 1000)
        };

        var result = await _commandRunner.RunAsync(command, cancellationToken);
        if (result.ExitCode != 0)
        {
            return new MonitorExecutionResult
            {
                Status = "error",
                Message = $"Gateway {target} unreachable"
            };
        }

        var latency = ParseLatency(result.StdOut);
        return new MonitorExecutionResult
        {
            Status = "ok",
            Message = latency.HasValue
                ? $"Gateway {target} reachable ({latency.Value} ms)"
                : $"Gateway {target} reachable",
            LatencyMs = latency
        };
    }

    private Task<MonitorExecutionResult> RunSystemMonitorAsync(
        MonitorDefinitionEntity definition,
        CancellationToken cancellationToken)
    {
        var config = DeserializeConfig(definition.ConfigJson, new SystemHealthMonitorConfig
        {
            CpuWarn = 80,
            CpuCrit = 95,
            MemWarn = 80,
            MemCrit = 95,
            DiskWarn = 85,
            DiskCrit = 95
        });

        var cpu = GetCpuUsagePercent();
        var mem = GetMemoryUsagePercent();
        var disk = GetDiskUsagePercent();

        var status = "ok";
        if (cpu >= config.CpuCrit || mem >= config.MemCrit || disk >= config.DiskCrit)
        {
            status = "error";
        }
        else if (cpu >= config.CpuWarn || mem >= config.MemWarn || disk >= config.DiskWarn)
        {
            status = "warning";
        }

        var message = $"CPU {cpu:F0}% • RAM {mem:F0}% • Disk {disk:F0}%";
        return Task.FromResult(new MonitorExecutionResult
        {
            Status = status,
            Message = message
        });
    }

    private Task<MonitorExecutionResult> RunServiceMonitorAsync(
        MonitorDefinitionEntity definition,
        CancellationToken cancellationToken)
    {
        var config = DeserializeConfig(definition.ConfigJson, new ServiceMonitorConfig
        {
            RequireWebUi = true
        });

        var webUiOk = true;
        if (config.RequireWebUi)
        {
            webUiOk = Process.GetProcessesByName("Monolith.FireWall.WebUI").Length > 0;
        }

        if (!webUiOk)
        {
            return Task.FromResult(new MonitorExecutionResult
            {
                Status = "error",
                Message = "Web UI service not running"
            });
        }

        return Task.FromResult(new MonitorExecutionResult
        {
            Status = "ok",
            Message = "Core services running"
        });
    }

    private async Task<MonitorExecutionResult> RunGatewaySyncMonitorAsync(
        MonitorDefinitionEntity definition,
        CancellationToken cancellationToken)
    {
        try
        {
            await _routingManager.SyncGatewaysAsync(cancellationToken);
            var gateways = await _routingManager.GetGatewaysAsync(cancellationToken);
            if (gateways.Count == 0)
            {
                return new MonitorExecutionResult
                {
                    Status = "warning",
                    Message = "No gateways detected after sync"
                };
            }

            return new MonitorExecutionResult
            {
                Status = "ok",
                Message = $"Synced gateways ({gateways.Count} found)"
            };
        }
        catch (Exception ex)
        {
            return new MonitorExecutionResult
            {
                Status = "error",
                Message = $"Gateway sync failed: {ex.Message}"
            };
        }
    }

    private async Task HandleStatusChangeAsync(
        MonitorDefinitionEntity definition,
        MonitorStatusEntity? previous,
        MonitorStatusEntity current,
        MonitorExecutionResult result)
    {
        var previousStatus = previous?.Status ?? "unknown";
        if (string.Equals(previousStatus, current.Status, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (previous == null && current.Status == "ok")
        {
            return;
        }

        var title = current.Status == "ok"
            ? $"{definition.Name} recovered"
            : $"{definition.Name} {current.Status}";
        var severity = current.Status switch
        {
            "ok" => "info",
            "warning" => "warning",
            "error" => "error",
            _ => "info"
        };

        var notification = new SystemNotificationEntity
        {
            Type = "monitor",
            Severity = severity,
            Title = title,
            Message = current.Message,
            MonitorKey = definition.Key,
            CreatedAt = DateTime.UtcNow
        };

        await _store.InsertNotificationAsync(notification);
        await _loggingManager.LogSystemAsync(
            "Monitoring",
            severity,
            "MonitoringManager",
            title,
            new Dictionary<string, object>
            {
                ["monitorKey"] = definition.Key,
                ["status"] = current.Status,
                ["message"] = current.Message ?? string.Empty
            });
    }

    private static NotificationView ToView(SystemNotificationEntity entity)
    {
        return new NotificationView
        {
            Id = entity.Id,
            Type = entity.Type,
            Severity = entity.Severity,
            Title = entity.Title,
            Message = entity.Message,
            MonitorKey = entity.MonitorKey,
            CreatedAt = entity.CreatedAt,
            ReadAt = entity.ReadAt
        };
    }

    private static GatewayMonitorConfig DeserializeConfig(string? json, GatewayMonitorConfig fallback)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return fallback;
        }

        try
        {
            var config = JsonSerializer.Deserialize<GatewayMonitorConfig>(json, JsonOptions);
            return config ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static SystemHealthMonitorConfig DeserializeConfig(string? json, SystemHealthMonitorConfig fallback)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return fallback;
        }

        try
        {
            var config = JsonSerializer.Deserialize<SystemHealthMonitorConfig>(json, JsonOptions);
            return config ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static ServiceMonitorConfig DeserializeConfig(string? json, ServiceMonitorConfig fallback)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return fallback;
        }

        try
        {
            var config = JsonSerializer.Deserialize<ServiceMonitorConfig>(json, JsonOptions);
            return config ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static int? ParseLatency(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        var match = PingLatencyRegex.Match(output);
        if (!match.Success)
        {
            return null;
        }

        if (double.TryParse(match.Groups[1].Value, out var value))
        {
            return (int)Math.Round(value);
        }

        return null;
    }

    private static double GetCpuUsagePercent()
    {
        try
        {
            var first = ReadCpuStat();
            Thread.Sleep(120);
            var second = ReadCpuStat();

            var idleDelta = second.Idle - first.Idle;
            var totalDelta = second.Total - first.Total;
            if (totalDelta <= 0)
            {
                return 0;
            }

            return Math.Max(0, Math.Min(100, 100.0 * (totalDelta - idleDelta) / totalDelta));
        }
        catch
        {
            return 0;
        }
    }

    private static double GetMemoryUsagePercent()
    {
        try
        {
            var memInfo = File.ReadAllLines("/proc/meminfo");
            double total = 0;
            double available = 0;

            foreach (var line in memInfo)
            {
                if (line.StartsWith("MemTotal:", StringComparison.OrdinalIgnoreCase))
                {
                    total = ParseMemValue(line);
                }
                else if (line.StartsWith("MemAvailable:", StringComparison.OrdinalIgnoreCase))
                {
                    available = ParseMemValue(line);
                }
            }

            if (total <= 0)
            {
                return 0;
            }

            var used = total - available;
            return Math.Max(0, Math.Min(100, (used / total) * 100));
        }
        catch
        {
            return 0;
        }
    }

    private static double GetDiskUsagePercent()
    {
        try
        {
            var drives = DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed);
            var max = 0.0;
            foreach (var drive in drives)
            {
                var total = drive.TotalSize;
                if (total <= 0)
                {
                    continue;
                }

                var used = total - drive.AvailableFreeSpace;
                var percent = (double)used / total * 100;
                if (percent > max)
                {
                    max = percent;
                }
            }

            return Math.Max(0, Math.Min(100, max));
        }
        catch
        {
            return 0;
        }
    }

    private static CpuStat ReadCpuStat()
    {
        var line = File.ReadLines("/proc/stat").FirstOrDefault(l => l.StartsWith("cpu "));
        if (line == null)
        {
            return new CpuStat();
        }

        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5)
        {
            return new CpuStat();
        }

        var values = parts.Skip(1)
            .Select(p => long.TryParse(p, out var v) ? v : 0)
            .ToArray();

        var idle = values.Length > 3 ? values[3] : 0;
        var iowait = values.Length > 4 ? values[4] : 0;
        var total = values.Sum();
        return new CpuStat
        {
            Idle = idle + iowait,
            Total = total
        };
    }

    private static double ParseMemValue(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return 0;
        }

        return double.TryParse(parts[1], out var value) ? value : 0;
    }

    private static TimeSpan ComputeDelay(List<DateTime> nextRuns, DateTime now)
    {
        if (nextRuns.Count == 0)
        {
            return TimeSpan.FromSeconds(30);
        }

        var next = nextRuns.Min();
        var delay = next - now;
        if (delay < TimeSpan.FromSeconds(1))
        {
            return TimeSpan.FromSeconds(1);
        }

        if (delay > TimeSpan.FromSeconds(30))
        {
            return TimeSpan.FromSeconds(30);
        }

        return delay;
    }

    private sealed class MonitorExecutionResult
    {
        public string Status { get; set; } = "unknown";
        public string? Message { get; set; }
        public int? LatencyMs { get; set; }
    }

    private sealed class GatewayMonitorConfig
    {
        public bool UseGateway { get; set; }
        public string? Target { get; set; }
        public int? TimeoutMs { get; set; }
    }

    private sealed class SystemHealthMonitorConfig
    {
        public int CpuWarn { get; set; }
        public int CpuCrit { get; set; }
        public int MemWarn { get; set; }
        public int MemCrit { get; set; }
        public int DiskWarn { get; set; }
        public int DiskCrit { get; set; }
    }

    private sealed class ServiceMonitorConfig
    {
        public bool RequireWebUi { get; set; }
    }

    private readonly struct CpuStat
    {
        public long Idle { get; init; }
        public long Total { get; init; }
    }
}
