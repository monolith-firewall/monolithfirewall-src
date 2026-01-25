using Monolith.FireWall.Common.Interfaces;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Services;

/// <summary>
/// Provides access to network operational state for packages.
/// Implements INetworkStateProvider from the Common library.
/// </summary>
public sealed class NetworkStateProvider : INetworkStateProvider
{
    private readonly InterfaceOperationalStateStore _operationalStateStore;
    private readonly GatewayHealthStore _healthStore;
    private readonly GatewayStore _gatewayStore;
    private readonly NetworkStateMonitorService _networkStateMonitor;

    private readonly List<Action<InterfaceStateChangeEvent>> _interfaceChangeHandlers = new();
    private readonly List<Action<GatewayStateChangeEvent>> _gatewayChangeHandlers = new();
    private readonly object _handlersLock = new();

    public NetworkStateProvider(
        InterfaceOperationalStateStore operationalStateStore,
        GatewayHealthStore healthStore,
        GatewayStore gatewayStore,
        NetworkStateMonitorService networkStateMonitor)
    {
        _operationalStateStore = operationalStateStore;
        _healthStore = healthStore;
        _gatewayStore = gatewayStore;
        _networkStateMonitor = networkStateMonitor;
    }

    public async Task<InterfaceOperationalState?> GetInterfaceStateAsync(string interfaceName)
    {
        var entity = await _operationalStateStore.GetAsync(interfaceName);
        if (entity == null)
        {
            return null;
        }

        return MapInterfaceState(entity);
    }

    public async Task<IReadOnlyList<InterfaceOperationalState>> GetAllInterfaceStatesAsync()
    {
        var entities = await _operationalStateStore.GetAllAsync();
        return entities.Select(MapInterfaceState).ToList();
    }

    public async Task<GatewayHealth?> GetGatewayHealthAsync(int gatewayId)
    {
        var health = await _healthStore.GetHealthAsync(gatewayId);
        if (health == null)
        {
            return null;
        }

        var gateway = await _gatewayStore.GetGatewayAsync(gatewayId);
        return MapGatewayHealth(health, gateway);
    }

    public async Task<IReadOnlyList<GatewayHealth>> GetAllGatewayHealthAsync()
    {
        var healthRecords = await _healthStore.GetAllHealthAsync();
        var gateways = await _gatewayStore.GetGatewaysAsync();
        var gatewayLookup = gateways.ToDictionary(g => g.Id);

        return healthRecords.Select(h =>
        {
            gatewayLookup.TryGetValue(h.GatewayId, out var gateway);
            return MapGatewayHealth(h, gateway);
        }).ToList();
    }

    public IDisposable SubscribeToInterfaceChanges(Action<InterfaceStateChangeEvent> handler)
    {
        lock (_handlersLock)
        {
            _interfaceChangeHandlers.Add(handler);
        }

        return new Subscription(() =>
        {
            lock (_handlersLock)
            {
                _interfaceChangeHandlers.Remove(handler);
            }
        });
    }

    public IDisposable SubscribeToGatewayChanges(Action<GatewayStateChangeEvent> handler)
    {
        lock (_handlersLock)
        {
            _gatewayChangeHandlers.Add(handler);
        }

        return new Subscription(() =>
        {
            lock (_handlersLock)
            {
                _gatewayChangeHandlers.Remove(handler);
            }
        });
    }

    /// <summary>
    /// Called by NetworkStateMonitorService when interface state changes.
    /// </summary>
    internal void NotifyInterfaceStateChanged(InterfaceStateChangeEvent evt)
    {
        Action<InterfaceStateChangeEvent>[] handlers;
        lock (_handlersLock)
        {
            handlers = _interfaceChangeHandlers.ToArray();
        }

        foreach (var handler in handlers)
        {
            try
            {
                handler(evt);
            }
            catch
            {
                // Don't let handler errors affect other handlers
            }
        }
    }

    /// <summary>
    /// Called by GatewayHealthMonitor when gateway health changes.
    /// </summary>
    internal void NotifyGatewayStateChanged(GatewayStateChangeEvent evt)
    {
        Action<GatewayStateChangeEvent>[] handlers;
        lock (_handlersLock)
        {
            handlers = _gatewayChangeHandlers.ToArray();
        }

        foreach (var handler in handlers)
        {
            try
            {
                handler(evt);
            }
            catch
            {
                // Don't let handler errors affect other handlers
            }
        }
    }

    private static InterfaceOperationalState MapInterfaceState(InterfaceOperationalStateEntity entity)
    {
        return new InterfaceOperationalState
        {
            InterfaceName = entity.InterfaceName,
            LinkState = entity.LinkState.ToString().ToLowerInvariant(),
            MacAddress = entity.MacAddress,
            SpeedMbps = entity.SpeedMbps,
            Duplex = entity.Duplex,
            Mtu = entity.Mtu,
            CurrentIpv4Address = entity.CurrentIpv4Address,
            CurrentIpv4Prefix = entity.CurrentIpv4Prefix,
            CurrentIpv6Address = entity.CurrentIpv6Address,
            CurrentIpv6Prefix = entity.CurrentIpv6Prefix,
            DhcpGateway = entity.DhcpGateway,
            HealthStatus = entity.HealthStatus.ToString().ToLowerInvariant(),
            LastSeenAt = entity.LastSeenAt,
            LastLinkChangeAt = entity.LastLinkChangeAt,
            LastIpChangeAt = entity.LastIpChangeAt
        };
    }

    private static GatewayHealth MapGatewayHealth(GatewayHealthEntity health, GatewayEntity? gateway)
    {
        return new GatewayHealth
        {
            GatewayId = health.GatewayId,
            GatewayName = gateway?.Name ?? string.Empty,
            GatewayAddress = gateway?.Address ?? string.Empty,
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

    private sealed class Subscription : IDisposable
    {
        private readonly Action _onDispose;

        public Subscription(Action onDispose)
        {
            _onDispose = onDispose;
        }

        public void Dispose()
        {
            _onDispose();
        }
    }
}
