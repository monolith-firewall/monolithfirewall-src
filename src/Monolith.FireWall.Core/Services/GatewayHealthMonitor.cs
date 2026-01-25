using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Monolith.FireWall.Common.Services;
using Monolith.FireWall.Core.Models;
using Monolith.FireWall.Core.Services.Platform;
using Monolith.FireWall.Platform.Models;

namespace Monolith.FireWall.Core.Services;

/// <summary>
/// Background service that monitors gateway health using ICMP, TCP, or HTTP probes.
/// Triggers gateway group failover when health changes.
/// </summary>
public sealed class GatewayHealthMonitor : IDisposable
{
    private readonly GatewayStore _gatewayStore;
    private readonly GatewayHealthStore _healthStore;
    private readonly GatewayGroupManager? _groupManager;
    private readonly NetworkStateChangeStore _changeStore;
    private readonly PlatformCommandRunner _commandRunner;
    private readonly LoggingManager _loggingManager;

    private CancellationTokenSource? _cts;
    private Task? _monitorTask;
    private bool _isRunning;
    private bool _disposed;

    // Circular buffers for latency/packet loss averaging
    private readonly Dictionary<int, Queue<int>> _latencySamples = new();
    private readonly Dictionary<int, Queue<bool>> _successSamples = new();
    private readonly object _samplesLock = new();

    public GatewayHealthMonitor(
        GatewayStore gatewayStore,
        GatewayHealthStore healthStore,
        NetworkStateChangeStore changeStore,
        PlatformCommandRunner commandRunner,
        GatewayGroupManager? groupManager = null)
    {
        _gatewayStore = gatewayStore;
        _healthStore = healthStore;
        _changeStore = changeStore;
        _commandRunner = commandRunner;
        _groupManager = groupManager;
        _loggingManager = LoggingManager.Instance;
    }

    public bool IsRunning => _isRunning;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning)
        {
            return;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _isRunning = true;

        // Initialize health for all gateways
        await InitializeHealthAsync(_cts.Token);

        // Start monitoring loop
        _monitorTask = MonitorLoopAsync(_cts.Token);

        await _loggingManager.LogSystemAsync(
            "Routing",
            "info",
            "GatewayHealthMonitor",
            "Gateway health monitoring started");
    }

    public async Task StopAsync()
    {
        if (!_isRunning)
        {
            return;
        }

        _isRunning = false;
        _cts?.Cancel();

        if (_monitorTask != null)
        {
            try
            {
                await _monitorTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch
            {
                // Ignore cancellation
            }
        }

        _cts?.Dispose();
        _cts = null;
        _monitorTask = null;

        await _loggingManager.LogSystemAsync(
            "Routing",
            "info",
            "GatewayHealthMonitor",
            "Gateway health monitoring stopped");
    }

    /// <summary>
    /// Performs a manual health check for a specific gateway.
    /// </summary>
    public async Task<GatewayHealthView?> CheckGatewayNowAsync(int gatewayId, CancellationToken cancellationToken = default)
    {
        var gateway = await _gatewayStore.GetGatewayAsync(gatewayId);
        if (gateway == null)
        {
            return null;
        }

        var config = await _healthStore.GetOrCreateDefaultConfigAsync(gatewayId);
        await PerformHealthCheckAsync(gateway, config, cancellationToken);

        var health = await _healthStore.GetHealthAsync(gatewayId);
        if (health == null)
        {
            return null;
        }

        return new GatewayHealthView
        {
            GatewayId = health.GatewayId,
            Status = health.Status.ToString().ToLowerInvariant(),
            LatencyMs = health.LatencyMs,
            PacketLossPercent = health.PacketLossPercent,
            ConsecutiveFailures = health.ConsecutiveFailures,
            ConsecutiveSuccesses = health.ConsecutiveSuccesses,
            LastCheckAt = health.LastCheckAt,
            LastStateChangeAt = health.LastStateChangeAt,
            LastError = health.LastError
        };
    }

    /// <summary>
    /// Gets current health status for all gateways.
    /// </summary>
    public async Task<List<GatewayHealthView>> GetAllHealthAsync()
    {
        var healthRecords = await _healthStore.GetAllHealthAsync();
        return healthRecords.Select(h => new GatewayHealthView
        {
            GatewayId = h.GatewayId,
            Status = h.Status.ToString().ToLowerInvariant(),
            LatencyMs = h.LatencyMs,
            PacketLossPercent = h.PacketLossPercent,
            ConsecutiveFailures = h.ConsecutiveFailures,
            ConsecutiveSuccesses = h.ConsecutiveSuccesses,
            LastCheckAt = h.LastCheckAt,
            LastStateChangeAt = h.LastStateChangeAt,
            LastError = h.LastError
        }).ToList();
    }

    private async Task InitializeHealthAsync(CancellationToken cancellationToken)
    {
        var gateways = await _gatewayStore.GetGatewaysAsync();
        foreach (var gateway in gateways)
        {
            var existing = await _healthStore.GetHealthAsync(gateway.Id);
            if (existing == null)
            {
                await _healthStore.UpsertHealthAsync(new GatewayHealthEntity
                {
                    GatewayId = gateway.Id,
                    Status = GatewayHealthStatus.Unknown
                });
            }

            // Ensure config exists
            await _healthStore.GetOrCreateDefaultConfigAsync(gateway.Id);
        }
    }

    private async Task MonitorLoopAsync(CancellationToken cancellationToken)
    {
        // Track next check time per gateway
        var nextCheckTime = new Dictionary<int, DateTime>();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                var configs = await _healthStore.GetEnabledConfigsAsync();

                foreach (var config in configs)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    // Check if it's time for this gateway
                    if (nextCheckTime.TryGetValue(config.GatewayId, out var scheduled) && scheduled > now)
                    {
                        continue;
                    }

                    var gateway = await _gatewayStore.GetGatewayAsync(config.GatewayId);
                    if (gateway == null)
                    {
                        continue;
                    }

                    // Perform health check
                    var (statusChanged, previousStatus) = await PerformHealthCheckAsync(gateway, config, cancellationToken);

                    // Notify group manager if status changed
                    if (statusChanged && _groupManager != null)
                    {
                        var health = await _healthStore.GetHealthAsync(gateway.Id);
                        if (health != null)
                        {
                            await OnHealthChangedAsync(gateway.Id, previousStatus, health.Status, cancellationToken);
                            await _groupManager.EvaluateGroupsAsync(cancellationToken);
                        }
                    }

                    // Schedule next check
                    nextCheckTime[config.GatewayId] = now.AddSeconds(config.IntervalSeconds);
                }

                // Sleep for a short interval before checking again
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                await _loggingManager.LogSystemAsync(
                    "Routing",
                    "error",
                    "GatewayHealthMonitor",
                    $"Error in health monitoring loop: {ex.Message}");
            }
        }
    }

    private async Task<(bool StatusChanged, GatewayHealthStatus PreviousStatus)> PerformHealthCheckAsync(
        GatewayEntity gateway,
        GatewayMonitorConfigEntity config,
        CancellationToken cancellationToken)
    {
        var target = string.IsNullOrWhiteSpace(config.MonitorTarget)
            ? gateway.Address
            : config.MonitorTarget;

        var (success, latencyMs, error) = config.MonitorType switch
        {
            GatewayMonitorType.Icmp => await PerformIcmpCheckAsync(target, config.TimeoutMs, cancellationToken),
            GatewayMonitorType.Tcp => await PerformTcpCheckAsync(target, config.MonitorPort ?? 80, config.TimeoutMs, cancellationToken),
            GatewayMonitorType.Http or GatewayMonitorType.HttpGet => await PerformHttpCheckAsync(target, config.MonitorPort, config.TimeoutMs, cancellationToken),
            _ => await PerformIcmpCheckAsync(target, config.TimeoutMs, cancellationToken)
        };

        // Update sample buffers
        UpdateSamples(gateway.Id, success, latencyMs, config.SampleCount);

        // Calculate averaged values
        var (avgLatency, packetLoss) = GetAveragedMetrics(gateway.Id);

        // Update health status
        return await _healthStore.UpdateHealthCheckAsync(
            gateway.Id,
            success,
            avgLatency,
            packetLoss,
            error);
    }

    private async Task<(bool Success, int? LatencyMs, string? Error)> PerformIcmpCheckAsync(
        string target,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        try
        {
            using var ping = new Ping();
            var stopwatch = Stopwatch.StartNew();
            var reply = await ping.SendPingAsync(target, timeoutMs);
            stopwatch.Stop();

            if (reply.Status == IPStatus.Success)
            {
                return (true, (int)reply.RoundtripTime, null);
            }

            return (false, null, $"Ping failed: {reply.Status}");
        }
        catch (PingException ex)
        {
            return (false, null, $"Ping exception: {ex.Message}");
        }
        catch (Exception ex)
        {
            return (false, null, $"Error: {ex.Message}");
        }
    }

    private async Task<(bool Success, int? LatencyMs, string? Error)> PerformTcpCheckAsync(
        string target,
        int port,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            var stopwatch = Stopwatch.StartNew();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeoutMs);

            await client.ConnectAsync(target, port, cts.Token);
            stopwatch.Stop();

            return (true, (int)stopwatch.ElapsedMilliseconds, null);
        }
        catch (OperationCanceledException)
        {
            return (false, null, "TCP connection timed out");
        }
        catch (SocketException ex)
        {
            return (false, null, $"TCP socket error: {ex.SocketErrorCode}");
        }
        catch (Exception ex)
        {
            return (false, null, $"TCP error: {ex.Message}");
        }
    }

    private async Task<(bool Success, int? LatencyMs, string? Error)> PerformHttpCheckAsync(
        string target,
        int? port,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        try
        {
            var url = target;
            if (!target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                var scheme = port == 443 ? "https" : "http";
                url = port.HasValue && port != 80 && port != 443
                    ? $"{scheme}://{target}:{port}/"
                    : $"{scheme}://{target}/";
            }

            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(timeoutMs)
            };

            var stopwatch = Stopwatch.StartNew();
            var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            stopwatch.Stop();

            // Consider 2xx and 3xx as success
            if ((int)response.StatusCode >= 200 && (int)response.StatusCode < 400)
            {
                return (true, (int)stopwatch.ElapsedMilliseconds, null);
            }

            return (false, null, $"HTTP status: {(int)response.StatusCode}");
        }
        catch (TaskCanceledException)
        {
            return (false, null, "HTTP request timed out");
        }
        catch (HttpRequestException ex)
        {
            return (false, null, $"HTTP error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return (false, null, $"Error: {ex.Message}");
        }
    }

    private void UpdateSamples(int gatewayId, bool success, int? latencyMs, int maxSamples)
    {
        lock (_samplesLock)
        {
            if (!_successSamples.ContainsKey(gatewayId))
            {
                _successSamples[gatewayId] = new Queue<bool>();
            }
            if (!_latencySamples.ContainsKey(gatewayId))
            {
                _latencySamples[gatewayId] = new Queue<int>();
            }

            var successQueue = _successSamples[gatewayId];
            var latencyQueue = _latencySamples[gatewayId];

            successQueue.Enqueue(success);
            while (successQueue.Count > maxSamples)
            {
                successQueue.Dequeue();
            }

            if (latencyMs.HasValue)
            {
                latencyQueue.Enqueue(latencyMs.Value);
                while (latencyQueue.Count > maxSamples)
                {
                    latencyQueue.Dequeue();
                }
            }
        }
    }

    private (int? AvgLatency, double PacketLoss) GetAveragedMetrics(int gatewayId)
    {
        lock (_samplesLock)
        {
            int? avgLatency = null;
            double packetLoss = 0;

            if (_latencySamples.TryGetValue(gatewayId, out var latencyQueue) && latencyQueue.Count > 0)
            {
                avgLatency = (int)latencyQueue.Average();
            }

            if (_successSamples.TryGetValue(gatewayId, out var successQueue) && successQueue.Count > 0)
            {
                var failures = successQueue.Count(s => !s);
                packetLoss = (double)failures / successQueue.Count * 100;
            }

            return (avgLatency, packetLoss);
        }
    }

    private async Task OnHealthChangedAsync(
        int gatewayId,
        GatewayHealthStatus previousStatus,
        GatewayHealthStatus newStatus,
        CancellationToken cancellationToken)
    {
        var gateway = await _gatewayStore.GetGatewayAsync(gatewayId);
        var health = await _healthStore.GetHealthAsync(gatewayId);

        await _changeStore.LogChangeAsync(
            NetworkChangeType.GatewayHealthChanged,
            gatewayId: gatewayId,
            previousValue: new { Status = previousStatus.ToString() },
            newValue: new
            {
                Status = newStatus.ToString(),
                LatencyMs = health?.LatencyMs,
                PacketLoss = health?.PacketLossPercent
            },
            resolution: ResolutionAction.Notified);

        var level = newStatus switch
        {
            GatewayHealthStatus.Offline => "error",
            GatewayHealthStatus.Degraded => "warning",
            GatewayHealthStatus.Online when previousStatus == GatewayHealthStatus.Offline => "info",
            _ => "info"
        };

        await _loggingManager.LogSystemAsync(
            "Routing",
            level,
            "GatewayHealthMonitor",
            $"Gateway '{gateway?.Name ?? gatewayId.ToString()}' health changed: {previousStatus} -> {newStatus}",
            new Dictionary<string, object>
            {
                ["gatewayId"] = gatewayId,
                ["previousStatus"] = previousStatus.ToString(),
                ["newStatus"] = newStatus.ToString(),
                ["latencyMs"] = health?.LatencyMs ?? 0,
                ["packetLoss"] = health?.PacketLossPercent ?? 0
            });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts?.Cancel();
        _cts?.Dispose();
    }
}
