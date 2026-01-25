using System.Text.Json;
using Monolith.FireWall.Common.Interfaces;
using Monolith.FireWall.Common.Services;
using Monolith.FireWall.Core.Models;
using Monolith.FireWall.Core.Services.Platform;
using Monolith.FireWall.Platform.Models;

namespace Monolith.FireWall.Core.Services;

/// <summary>
/// Background service that monitors physical network state via polling.
/// Detects link changes, IP changes, DHCP leases, and updates operational state.
/// </summary>
public sealed class NetworkStateMonitorService : IDisposable
{
    private readonly InterfaceOperationalStateStore _operationalStateStore;
    private readonly NetworkStateChangeStore _changeStore;
    private readonly PlatformCommandRunner _commandRunner;
    private readonly LoggingManager _loggingManager;

    private readonly List<INetworkStateListener> _listeners = new();
    private readonly object _listenersLock = new();

    private CancellationTokenSource? _cts;
    private Task? _monitorTask;
    private bool _isRunning;
    private bool _disposed;

    // Configuration
    private TimeSpan _pollInterval = TimeSpan.FromSeconds(10);
    private TimeSpan _dhcpLeaseCheckInterval = TimeSpan.FromSeconds(30);
    private DateTime _lastDhcpLeaseCheck = DateTime.MinValue;

    // State cache for change detection
    private readonly Dictionary<string, InterfaceStateSnapshot> _stateCache = new();
    private readonly object _cacheLock = new();

    public NetworkStateMonitorService(
        InterfaceOperationalStateStore operationalStateStore,
        NetworkStateChangeStore changeStore,
        PlatformCommandRunner commandRunner)
    {
        _operationalStateStore = operationalStateStore;
        _changeStore = changeStore;
        _commandRunner = commandRunner;
        _loggingManager = LoggingManager.Instance;
    }

    public bool IsRunning => _isRunning;

    public void SetPollInterval(TimeSpan interval)
    {
        if (interval < TimeSpan.FromSeconds(1))
        {
            interval = TimeSpan.FromSeconds(1);
        }
        _pollInterval = interval;
    }

    public void RegisterListener(INetworkStateListener listener)
    {
        lock (_listenersLock)
        {
            if (!_listeners.Contains(listener))
            {
                _listeners.Add(listener);
            }
        }
    }

    public void UnregisterListener(INetworkStateListener listener)
    {
        lock (_listenersLock)
        {
            _listeners.Remove(listener);
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning)
        {
            return;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _isRunning = true;

        // Initial state capture
        await CaptureInitialStateAsync(_cts.Token);

        // Start monitoring loop
        _monitorTask = MonitorLoopAsync(_cts.Token);

        await _loggingManager.LogSystemAsync(
            "Network",
            "info",
            "NetworkStateMonitor",
            "Network state monitoring started");
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
            "Network",
            "info",
            "NetworkStateMonitor",
            "Network state monitoring stopped");
    }

    /// <summary>
    /// Performs a manual poll of all interface states and returns changes detected.
    /// </summary>
    public async Task<List<NetworkStateChangeEntity>> PollNowAsync(CancellationToken cancellationToken = default)
    {
        return await PollInterfacesAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the current operational state for an interface from cache.
    /// </summary>
    public InterfaceOperationalStateView? GetCachedState(string interfaceName)
    {
        lock (_cacheLock)
        {
            if (_stateCache.TryGetValue(interfaceName, out var snapshot))
            {
                return BuildView(snapshot);
            }
        }
        return null;
    }

    /// <summary>
    /// Gets all cached operational states.
    /// </summary>
    public List<InterfaceOperationalStateView> GetAllCachedStates()
    {
        lock (_cacheLock)
        {
            return _stateCache.Values.Select(BuildView).ToList();
        }
    }

    private async Task CaptureInitialStateAsync(CancellationToken cancellationToken)
    {
        var interfaces = await GetInterfacesAsync(cancellationToken);
        var addresses = await GetAddressesAsync(cancellationToken);
        var routes = await GetRoutesAsync(cancellationToken);

        var now = DateTime.UtcNow;

        foreach (var iface in interfaces)
        {
            var ifaceAddresses = addresses.Where(a => a.Interface == iface.Name).ToList();
            var ipv4 = ifaceAddresses.FirstOrDefault(a => a.Family == "inet");
            var ipv6 = ifaceAddresses.FirstOrDefault(a => a.Family == "inet6" && !a.Address.StartsWith("fe80:"));

            var entity = new InterfaceOperationalStateEntity
            {
                InterfaceName = iface.Name,
                LinkState = ParseLinkState(iface.OperState),
                MacAddress = iface.MacAddress,
                Mtu = iface.Mtu,
                CurrentIpv4Address = ipv4?.Address,
                CurrentIpv4Prefix = ipv4?.PrefixLength,
                CurrentIpv6Address = ipv6?.Address,
                CurrentIpv6Prefix = ipv6?.PrefixLength,
                HealthStatus = iface.IsUp ? InterfaceHealthStatus.Healthy : InterfaceHealthStatus.Down,
                LastSeenAt = now
            };

            // Get speed/duplex from ethtool
            var linkInfo = await GetLinkInfoAsync(iface.Name, cancellationToken);
            if (linkInfo != null)
            {
                entity.SpeedMbps = linkInfo.SpeedMbps;
                entity.Duplex = linkInfo.Duplex;
            }

            // Get traffic stats
            var stats = await GetTrafficStatsAsync(iface.Name, cancellationToken);
            if (stats != null)
            {
                entity.RxBytes = stats.RxBytes;
                entity.TxBytes = stats.TxBytes;
                entity.RxPackets = stats.RxPackets;
                entity.TxPackets = stats.TxPackets;
                entity.RxErrors = stats.RxErrors;
                entity.TxErrors = stats.TxErrors;
            }

            await _operationalStateStore.UpsertAsync(entity);

            // Cache state for change detection
            lock (_cacheLock)
            {
                _stateCache[iface.Name] = CreateSnapshot(entity);
            }
        }
    }

    private async Task MonitorLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_pollInterval, cancellationToken);
                await PollInterfacesAsync(cancellationToken);

                // Check DHCP leases less frequently
                if (DateTime.UtcNow - _lastDhcpLeaseCheck > _dhcpLeaseCheckInterval)
                {
                    await CheckDhcpLeasesAsync(cancellationToken);
                    _lastDhcpLeaseCheck = DateTime.UtcNow;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                await _loggingManager.LogSystemAsync(
                    "Network",
                    "error",
                    "NetworkStateMonitor",
                    $"Error in monitoring loop: {ex.Message}");
            }
        }
    }

    private async Task<List<NetworkStateChangeEntity>> PollInterfacesAsync(CancellationToken cancellationToken)
    {
        var changes = new List<NetworkStateChangeEntity>();
        var interfaces = await GetInterfacesAsync(cancellationToken);
        var addresses = await GetAddressesAsync(cancellationToken);
        var now = DateTime.UtcNow;

        // Detect new and changed interfaces
        var currentNames = new HashSet<string>();
        foreach (var iface in interfaces)
        {
            currentNames.Add(iface.Name);
            var ifaceAddresses = addresses.Where(a => a.Interface == iface.Name).ToList();
            var ipv4 = ifaceAddresses.FirstOrDefault(a => a.Family == "inet");
            var ipv6 = ifaceAddresses.FirstOrDefault(a => a.Family == "inet6" && !a.Address.StartsWith("fe80:"));

            InterfaceStateSnapshot? previousSnapshot;
            lock (_cacheLock)
            {
                _stateCache.TryGetValue(iface.Name, out previousSnapshot);
            }

            var currentState = ParseLinkState(iface.OperState);
            var currentIp = ipv4?.Address;
            var currentPrefix = ipv4?.PrefixLength;

            // New interface detected
            if (previousSnapshot == null)
            {
                var change = new NetworkStateChangeEntity
                {
                    ChangeType = NetworkChangeType.InterfaceAdded,
                    InterfaceName = iface.Name,
                    NewValueJson = JsonSerializer.Serialize(new { Interface = iface.Name, Mac = iface.MacAddress }),
                    OccurredAt = now,
                    ResolutionAction = ResolutionAction.Notified
                };
                await _changeStore.InsertAsync(change);
                changes.Add(change);
                await NotifyInterfaceAddedAsync(iface.Name, cancellationToken);
            }
            else
            {
                // Link state change
                if (previousSnapshot.LinkState != currentState)
                {
                    var changeType = currentState == LinkState.Up ? NetworkChangeType.LinkUp : NetworkChangeType.LinkDown;
                    var change = new NetworkStateChangeEntity
                    {
                        ChangeType = changeType,
                        InterfaceName = iface.Name,
                        PreviousValueJson = JsonSerializer.Serialize(new { LinkState = previousSnapshot.LinkState.ToString() }),
                        NewValueJson = JsonSerializer.Serialize(new { LinkState = currentState.ToString() }),
                        OccurredAt = now,
                        ResolutionAction = ResolutionAction.Notified
                    };
                    await _changeStore.InsertAsync(change);
                    changes.Add(change);
                    await NotifyLinkStateChangedAsync(iface.Name, previousSnapshot.LinkState, currentState, cancellationToken);
                }

                // IP address change
                if (previousSnapshot.Ipv4Address != currentIp || previousSnapshot.Ipv4Prefix != currentPrefix)
                {
                    NetworkChangeType changeType;
                    if (string.IsNullOrEmpty(previousSnapshot.Ipv4Address) && !string.IsNullOrEmpty(currentIp))
                    {
                        changeType = NetworkChangeType.IpAdded;
                    }
                    else if (!string.IsNullOrEmpty(previousSnapshot.Ipv4Address) && string.IsNullOrEmpty(currentIp))
                    {
                        changeType = NetworkChangeType.IpRemoved;
                    }
                    else
                    {
                        changeType = NetworkChangeType.IpChanged;
                    }

                    var change = new NetworkStateChangeEntity
                    {
                        ChangeType = changeType,
                        InterfaceName = iface.Name,
                        PreviousValueJson = JsonSerializer.Serialize(new { Address = previousSnapshot.Ipv4Address, Prefix = previousSnapshot.Ipv4Prefix }),
                        NewValueJson = JsonSerializer.Serialize(new { Address = currentIp, Prefix = currentPrefix }),
                        OccurredAt = now,
                        ResolutionAction = ResolutionAction.Notified
                    };
                    await _changeStore.InsertAsync(change);
                    changes.Add(change);
                    await NotifyIpChangedAsync(iface.Name, previousSnapshot.Ipv4Address, currentIp, cancellationToken);
                }
            }

            // Update database and cache
            var entity = await _operationalStateStore.GetAsync(iface.Name) ?? new InterfaceOperationalStateEntity { InterfaceName = iface.Name };

            var linkStateChanged = entity.LinkState != currentState;
            var ipChanged = entity.CurrentIpv4Address != currentIp || entity.CurrentIpv4Prefix != currentPrefix;

            entity.LinkState = currentState;
            entity.MacAddress = iface.MacAddress;
            entity.Mtu = iface.Mtu;
            entity.CurrentIpv4Address = currentIp;
            entity.CurrentIpv4Prefix = currentPrefix;
            entity.CurrentIpv6Address = ipv6?.Address;
            entity.CurrentIpv6Prefix = ipv6?.PrefixLength;
            entity.HealthStatus = iface.IsUp ? InterfaceHealthStatus.Healthy : InterfaceHealthStatus.Down;
            entity.LastSeenAt = now;

            if (linkStateChanged)
            {
                entity.LastLinkChangeAt = now;
            }
            if (ipChanged)
            {
                entity.LastIpChangeAt = now;
            }

            // Update traffic stats
            var stats = await GetTrafficStatsAsync(iface.Name, cancellationToken);
            if (stats != null)
            {
                entity.RxBytes = stats.RxBytes;
                entity.TxBytes = stats.TxBytes;
                entity.RxPackets = stats.RxPackets;
                entity.TxPackets = stats.TxPackets;
                entity.RxErrors = stats.RxErrors;
                entity.TxErrors = stats.TxErrors;
            }

            await _operationalStateStore.UpsertAsync(entity);

            lock (_cacheLock)
            {
                _stateCache[iface.Name] = CreateSnapshot(entity);
            }
        }

        // Detect removed interfaces
        List<string> removedInterfaces;
        lock (_cacheLock)
        {
            removedInterfaces = _stateCache.Keys.Except(currentNames).ToList();
        }

        foreach (var removedName in removedInterfaces)
        {
            var change = new NetworkStateChangeEntity
            {
                ChangeType = NetworkChangeType.InterfaceRemoved,
                InterfaceName = removedName,
                OccurredAt = now,
                ResolutionAction = ResolutionAction.ManualRequired
            };
            await _changeStore.InsertAsync(change);
            changes.Add(change);
            await NotifyInterfaceRemovedAsync(removedName, cancellationToken);

            lock (_cacheLock)
            {
                _stateCache.Remove(removedName);
            }
        }

        return changes;
    }

    private async Task CheckDhcpLeasesAsync(CancellationToken cancellationToken)
    {
        // Parse dhclient lease files to get DHCP information
        var leaseDir = "/var/lib/dhcp";
        if (!Directory.Exists(leaseDir))
        {
            return;
        }

        try
        {
            foreach (var leaseFile in Directory.GetFiles(leaseDir, "dhclient*.leases"))
            {
                var content = await File.ReadAllTextAsync(leaseFile, cancellationToken);
                var leaseInfo = ParseDhcpLease(content);

                if (leaseInfo == null || string.IsNullOrEmpty(leaseInfo.Interface))
                {
                    continue;
                }

                var entity = await _operationalStateStore.GetAsync(leaseInfo.Interface);
                if (entity == null)
                {
                    continue;
                }

                var previousGateway = entity.DhcpGateway;
                var gatewayChanged = previousGateway != leaseInfo.Gateway;

                entity.DhcpServerAddress = leaseInfo.ServerAddress;
                entity.DhcpGateway = leaseInfo.Gateway;
                entity.DhcpLeaseObtained = leaseInfo.LeaseObtained;
                entity.DhcpLeaseExpires = leaseInfo.LeaseExpires;
                entity.DhcpDnsServersJson = leaseInfo.DnsServers != null
                    ? JsonSerializer.Serialize(leaseInfo.DnsServers)
                    : null;

                await _operationalStateStore.UpsertAsync(entity);

                // Log gateway change from DHCP
                if (gatewayChanged && !string.IsNullOrEmpty(leaseInfo.Gateway))
                {
                    var change = new NetworkStateChangeEntity
                    {
                        ChangeType = NetworkChangeType.GatewayChanged,
                        InterfaceName = leaseInfo.Interface,
                        PreviousValueJson = JsonSerializer.Serialize(new { Gateway = previousGateway }),
                        NewValueJson = JsonSerializer.Serialize(new { Gateway = leaseInfo.Gateway }),
                        OccurredAt = DateTime.UtcNow,
                        ResolutionAction = ResolutionAction.AutoRepaired
                    };
                    await _changeStore.InsertAsync(change);
                    await NotifyGatewayChangedAsync(leaseInfo.Interface, previousGateway, leaseInfo.Gateway, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            await _loggingManager.LogSystemAsync(
                "Network",
                "warning",
                "NetworkStateMonitor",
                $"Error checking DHCP leases: {ex.Message}");
        }
    }

    // ========================================================================
    // Platform data gathering
    // ========================================================================

    private async Task<List<InterfaceInfo>> GetInterfacesAsync(CancellationToken cancellationToken)
    {
        var interfaces = new List<InterfaceInfo>();
        var netPath = "/sys/class/net";
        if (!Directory.Exists(netPath))
        {
            return interfaces;
        }

        await Task.Yield(); // Make async-friendly

        foreach (var dir in Directory.GetDirectories(netPath))
        {
            var iface = Path.GetFileName(dir);
            if (string.IsNullOrWhiteSpace(iface) || iface == "lo")
            {
                continue;
            }

            var operState = ReadFileTrim(Path.Combine(dir, "operstate"));
            var mac = ReadFileTrim(Path.Combine(dir, "address"));
            var mtuValue = ReadFileTrim(Path.Combine(dir, "mtu"));
            var mtu = int.TryParse(mtuValue, out var parsed) ? parsed : 0;
            var isUp = string.Equals(operState, "up", StringComparison.OrdinalIgnoreCase);

            interfaces.Add(new InterfaceInfo
            {
                Name = iface,
                MacAddress = mac,
                Mtu = mtu,
                OperState = operState,
                IsUp = isUp
            });
        }

        return interfaces;
    }

    private async Task<List<AddressInfo>> GetAddressesAsync(CancellationToken cancellationToken)
    {
        var addresses = new List<AddressInfo>();
        if (!_commandRunner.CommandExists("ip"))
        {
            return addresses;
        }

        var command = new PlatformCommand
        {
            FileName = "ip",
            Arguments = "-j addr show",
            UseSudo = false,
            TimeoutMs = 5000
        };

        var result = await _commandRunner.RunAsync(command, cancellationToken);
        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StdOut))
        {
            return addresses;
        }

        try
        {
            using var doc = JsonDocument.Parse(result.StdOut);
            foreach (var ifaceJson in doc.RootElement.EnumerateArray())
            {
                var ifname = ifaceJson.GetProperty("ifname").GetString() ?? string.Empty;
                if (!ifaceJson.TryGetProperty("addr_info", out var addrInfo) || addrInfo.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var addr in addrInfo.EnumerateArray())
                {
                    var local = addr.GetProperty("local").GetString() ?? string.Empty;
                    var family = addr.GetProperty("family").GetString() ?? string.Empty;
                    var prefix = addr.TryGetProperty("prefixlen", out var prefixEl) ? prefixEl.GetInt32() : 0;

                    addresses.Add(new AddressInfo
                    {
                        Interface = ifname,
                        Family = family,
                        Address = local,
                        PrefixLength = prefix
                    });
                }
            }
        }
        catch
        {
            // JSON parse error
        }

        return addresses;
    }

    private async Task<List<RouteInfo>> GetRoutesAsync(CancellationToken cancellationToken)
    {
        var routes = new List<RouteInfo>();
        if (!_commandRunner.CommandExists("ip"))
        {
            return routes;
        }

        var command = new PlatformCommand
        {
            FileName = "ip",
            Arguments = "-j route show",
            UseSudo = false,
            TimeoutMs = 5000
        };

        var result = await _commandRunner.RunAsync(command, cancellationToken);
        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StdOut))
        {
            return routes;
        }

        try
        {
            using var doc = JsonDocument.Parse(result.StdOut);
            foreach (var routeJson in doc.RootElement.EnumerateArray())
            {
                var dst = routeJson.TryGetProperty("dst", out var dstEl) ? dstEl.GetString() : "default";
                var gateway = routeJson.TryGetProperty("gateway", out var gwEl) ? gwEl.GetString() : null;
                var dev = routeJson.TryGetProperty("dev", out var devEl) ? devEl.GetString() : null;
                var protocol = routeJson.TryGetProperty("protocol", out var protoEl) ? protoEl.GetString() : null;

                routes.Add(new RouteInfo
                {
                    Destination = dst ?? "default",
                    Gateway = gateway,
                    Interface = dev,
                    Protocol = protocol
                });
            }
        }
        catch
        {
            // JSON parse error
        }

        return routes;
    }

    private async Task<LinkInfo?> GetLinkInfoAsync(string interfaceName, CancellationToken cancellationToken)
    {
        if (!_commandRunner.CommandExists("ethtool"))
        {
            return null;
        }

        var command = new PlatformCommand
        {
            FileName = "ethtool",
            Arguments = interfaceName,
            UseSudo = false,
            TimeoutMs = 5000
        };

        var result = await _commandRunner.RunAsync(command, cancellationToken);
        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StdOut))
        {
            return null;
        }

        var info = new LinkInfo();

        foreach (var line in result.StdOut.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("Speed:"))
            {
                var speedStr = trimmed.Substring(6).Trim();
                if (speedStr.EndsWith("Mb/s") && int.TryParse(speedStr.Replace("Mb/s", ""), out var speed))
                {
                    info.SpeedMbps = speed;
                }
            }
            else if (trimmed.StartsWith("Duplex:"))
            {
                info.Duplex = trimmed.Substring(7).Trim().ToLowerInvariant();
            }
        }

        return info;
    }

    private async Task<TrafficStats?> GetTrafficStatsAsync(string interfaceName, CancellationToken cancellationToken)
    {
        await Task.Yield();

        var basePath = $"/sys/class/net/{interfaceName}/statistics";
        if (!Directory.Exists(basePath))
        {
            return null;
        }

        return new TrafficStats
        {
            RxBytes = ReadLongFile(Path.Combine(basePath, "rx_bytes")),
            TxBytes = ReadLongFile(Path.Combine(basePath, "tx_bytes")),
            RxPackets = ReadLongFile(Path.Combine(basePath, "rx_packets")),
            TxPackets = ReadLongFile(Path.Combine(basePath, "tx_packets")),
            RxErrors = ReadLongFile(Path.Combine(basePath, "rx_errors")),
            TxErrors = ReadLongFile(Path.Combine(basePath, "tx_errors"))
        };
    }

    // ========================================================================
    // Listener notifications
    // ========================================================================

    private async Task NotifyInterfaceAddedAsync(string interfaceName, CancellationToken cancellationToken)
    {
        var change = new InterfaceStateChange
        {
            InterfaceName = interfaceName,
            ChangeType = NetworkChangeType.InterfaceAdded,
            OccurredAt = DateTime.UtcNow
        };

        INetworkStateListener[] listeners;
        lock (_listenersLock)
        {
            listeners = _listeners.ToArray();
        }

        foreach (var listener in listeners)
        {
            try
            {
                await listener.OnInterfaceStateChangedAsync(change, cancellationToken);
            }
            catch
            {
                // Don't let listener errors stop notifications
            }
        }
    }

    private async Task NotifyInterfaceRemovedAsync(string interfaceName, CancellationToken cancellationToken)
    {
        var change = new InterfaceStateChange
        {
            InterfaceName = interfaceName,
            ChangeType = NetworkChangeType.InterfaceRemoved,
            OccurredAt = DateTime.UtcNow
        };

        INetworkStateListener[] listeners;
        lock (_listenersLock)
        {
            listeners = _listeners.ToArray();
        }

        foreach (var listener in listeners)
        {
            try
            {
                await listener.OnInterfaceStateChangedAsync(change, cancellationToken);
            }
            catch
            {
                // Don't let listener errors stop notifications
            }
        }
    }

    private async Task NotifyLinkStateChangedAsync(string interfaceName, LinkState previous, LinkState current, CancellationToken cancellationToken)
    {
        var change = new InterfaceStateChange
        {
            InterfaceName = interfaceName,
            ChangeType = current == LinkState.Up ? NetworkChangeType.LinkUp : NetworkChangeType.LinkDown,
            PreviousLinkState = previous,
            NewLinkState = current,
            OccurredAt = DateTime.UtcNow
        };

        INetworkStateListener[] listeners;
        lock (_listenersLock)
        {
            listeners = _listeners.ToArray();
        }

        foreach (var listener in listeners)
        {
            try
            {
                await listener.OnInterfaceStateChangedAsync(change, cancellationToken);
            }
            catch
            {
                // Don't let listener errors stop notifications
            }
        }
    }

    private async Task NotifyIpChangedAsync(string interfaceName, string? previousIp, string? newIp, CancellationToken cancellationToken)
    {
        var change = new InterfaceStateChange
        {
            InterfaceName = interfaceName,
            ChangeType = NetworkChangeType.IpChanged,
            PreviousIpAddress = previousIp,
            NewIpAddress = newIp,
            OccurredAt = DateTime.UtcNow
        };

        INetworkStateListener[] listeners;
        lock (_listenersLock)
        {
            listeners = _listeners.ToArray();
        }

        foreach (var listener in listeners)
        {
            try
            {
                await listener.OnInterfaceStateChangedAsync(change, cancellationToken);
            }
            catch
            {
                // Don't let listener errors stop notifications
            }
        }
    }

    private async Task NotifyGatewayChangedAsync(string interfaceName, string? previousGateway, string? newGateway, CancellationToken cancellationToken)
    {
        var change = new InterfaceStateChange
        {
            InterfaceName = interfaceName,
            ChangeType = NetworkChangeType.GatewayChanged,
            PreviousGateway = previousGateway,
            NewGateway = newGateway,
            OccurredAt = DateTime.UtcNow
        };

        INetworkStateListener[] listeners;
        lock (_listenersLock)
        {
            listeners = _listeners.ToArray();
        }

        foreach (var listener in listeners)
        {
            try
            {
                await listener.OnInterfaceStateChangedAsync(change, cancellationToken);
            }
            catch
            {
                // Don't let listener errors stop notifications
            }
        }
    }

    // ========================================================================
    // Helpers
    // ========================================================================

    private static LinkState ParseLinkState(string operState)
    {
        return operState.ToLowerInvariant() switch
        {
            "up" => LinkState.Up,
            "down" => LinkState.Down,
            "dormant" => LinkState.Dormant,
            _ => LinkState.Unknown
        };
    }

    private static string ReadFileTrim(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path).Trim() : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static long ReadLongFile(string path)
    {
        var content = ReadFileTrim(path);
        return long.TryParse(content, out var value) ? value : 0;
    }

    private static InterfaceStateSnapshot CreateSnapshot(InterfaceOperationalStateEntity entity)
    {
        return new InterfaceStateSnapshot
        {
            InterfaceName = entity.InterfaceName,
            LinkState = entity.LinkState,
            MacAddress = entity.MacAddress,
            Ipv4Address = entity.CurrentIpv4Address,
            Ipv4Prefix = entity.CurrentIpv4Prefix,
            Ipv6Address = entity.CurrentIpv6Address,
            Ipv6Prefix = entity.CurrentIpv6Prefix,
            DhcpGateway = entity.DhcpGateway,
            CapturedAt = DateTime.UtcNow
        };
    }

    private static InterfaceOperationalStateView BuildView(InterfaceStateSnapshot snapshot)
    {
        return new InterfaceOperationalStateView
        {
            InterfaceName = snapshot.InterfaceName,
            LinkState = snapshot.LinkState.ToString().ToLowerInvariant(),
            MacAddress = snapshot.MacAddress,
            CurrentIpv4Address = snapshot.Ipv4Address,
            CurrentIpv4Prefix = snapshot.Ipv4Prefix,
            CurrentIpv6Address = snapshot.Ipv6Address,
            CurrentIpv6Prefix = snapshot.Ipv6Prefix,
            DhcpGateway = snapshot.DhcpGateway,
            LastSeenAt = snapshot.CapturedAt
        };
    }

    private DhcpLeaseInfo? ParseDhcpLease(string content)
    {
        // Parse ISC dhclient lease format
        // lease {
        //   interface "eth0";
        //   fixed-address 192.168.1.100;
        //   option dhcp-server-identifier 192.168.1.1;
        //   option routers 192.168.1.1;
        //   option domain-name-servers 8.8.8.8, 8.8.4.4;
        //   renew 2 2024/01/01 00:00:00;
        //   rebind 2 2024/01/01 12:00:00;
        //   expire 2 2024/01/02 00:00:00;
        // }

        var lines = content.Split('\n');
        DhcpLeaseInfo? currentLease = null;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("lease {"))
            {
                currentLease = new DhcpLeaseInfo();
            }
            else if (trimmed == "}" && currentLease != null)
            {
                // Return the most recent lease
                return currentLease;
            }
            else if (currentLease != null)
            {
                if (trimmed.StartsWith("interface "))
                {
                    currentLease.Interface = ExtractQuotedValue(trimmed);
                }
                else if (trimmed.StartsWith("fixed-address "))
                {
                    currentLease.Address = ExtractValue(trimmed, "fixed-address ");
                }
                else if (trimmed.StartsWith("option dhcp-server-identifier "))
                {
                    currentLease.ServerAddress = ExtractValue(trimmed, "option dhcp-server-identifier ");
                }
                else if (trimmed.StartsWith("option routers "))
                {
                    currentLease.Gateway = ExtractValue(trimmed, "option routers ")?.Split(',')[0].Trim();
                }
                else if (trimmed.StartsWith("option domain-name-servers "))
                {
                    var dnsValue = ExtractValue(trimmed, "option domain-name-servers ");
                    if (!string.IsNullOrEmpty(dnsValue))
                    {
                        currentLease.DnsServers = dnsValue.Split(',').Select(s => s.Trim()).ToList();
                    }
                }
                else if (trimmed.StartsWith("expire "))
                {
                    currentLease.LeaseExpires = ParseDhcpDateTime(trimmed.Substring(7));
                }
                else if (trimmed.StartsWith("renew "))
                {
                    currentLease.LeaseObtained = ParseDhcpDateTime(trimmed.Substring(6));
                }
            }
        }

        return currentLease;
    }

    private static string? ExtractQuotedValue(string line)
    {
        var start = line.IndexOf('"');
        var end = line.LastIndexOf('"');
        if (start >= 0 && end > start)
        {
            return line.Substring(start + 1, end - start - 1);
        }
        return null;
    }

    private static string? ExtractValue(string line, string prefix)
    {
        if (!line.StartsWith(prefix)) return null;
        var value = line.Substring(prefix.Length).TrimEnd(';').Trim();
        return value;
    }

    private static DateTime? ParseDhcpDateTime(string value)
    {
        // Format: "2 2024/01/01 00:00:00;" (day-of-week date time)
        var parts = value.TrimEnd(';').Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3)
        {
            if (DateTime.TryParse($"{parts[1]} {parts[2]}", out var dt))
            {
                return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            }
        }
        return null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts?.Cancel();
        _cts?.Dispose();
    }

    // ========================================================================
    // Internal types
    // ========================================================================

    private sealed class InterfaceStateSnapshot
    {
        public string InterfaceName { get; set; } = string.Empty;
        public LinkState LinkState { get; set; }
        public string? MacAddress { get; set; }
        public string? Ipv4Address { get; set; }
        public int? Ipv4Prefix { get; set; }
        public string? Ipv6Address { get; set; }
        public int? Ipv6Prefix { get; set; }
        public string? DhcpGateway { get; set; }
        public DateTime CapturedAt { get; set; }
    }

    private sealed class LinkInfo
    {
        public int? SpeedMbps { get; set; }
        public string? Duplex { get; set; }
    }

    private sealed class TrafficStats
    {
        public long RxBytes { get; set; }
        public long TxBytes { get; set; }
        public long RxPackets { get; set; }
        public long TxPackets { get; set; }
        public long RxErrors { get; set; }
        public long TxErrors { get; set; }
    }

    private sealed class DhcpLeaseInfo
    {
        public string? Interface { get; set; }
        public string? Address { get; set; }
        public string? ServerAddress { get; set; }
        public string? Gateway { get; set; }
        public List<string>? DnsServers { get; set; }
        public DateTime? LeaseObtained { get; set; }
        public DateTime? LeaseExpires { get; set; }
    }
}

// ========================================================================
// Listener interfaces
// ========================================================================

/// <summary>
/// Interface for components that want to be notified of network state changes.
/// </summary>
public interface INetworkStateListener
{
    Task OnInterfaceStateChangedAsync(InterfaceStateChange change, CancellationToken cancellationToken);
}

/// <summary>
/// Represents a network interface state change event.
/// </summary>
public sealed class InterfaceStateChange
{
    public string InterfaceName { get; set; } = string.Empty;
    public NetworkChangeType ChangeType { get; set; }
    public LinkState? PreviousLinkState { get; set; }
    public LinkState? NewLinkState { get; set; }
    public string? PreviousIpAddress { get; set; }
    public string? NewIpAddress { get; set; }
    public string? PreviousGateway { get; set; }
    public string? NewGateway { get; set; }
    public DateTime OccurredAt { get; set; }
}
