using System.Text.Json;
using Monolith.FireWall.Common.Models;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Transport.Handlers;

/// <summary>
/// Handler for interface operational state operations.
/// </summary>
public sealed class OperationalStateHandler : ICoreRequestHandler
{
    private static readonly HashSet<string> Actions = new(StringComparer.OrdinalIgnoreCase)
    {
        "operational.interfaces.list",
        "operational.interfaces.get",
        "operational.interfaces.refresh"
    };

    public bool CanHandle(string action) => Actions.Contains(action);

    public async Task<ApiResponse> HandleAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        if (context.OperationalStateStore == null)
        {
            return new ApiResponse(false, null, "Operational state store not available");
        }

        var action = request.GetProperty("action").GetString() ?? string.Empty;

        switch (action)
        {
            case "operational.interfaces.list":
                return await HandleListAsync(context, cancellationToken);

            case "operational.interfaces.get":
                return await HandleGetAsync(context, request, cancellationToken);

            case "operational.interfaces.refresh":
                return await HandleRefreshAsync(context, request, cancellationToken);
        }

        return new ApiResponse(false, null, $"Unhandled action: {action}");
    }

    private static async Task<ApiResponse> HandleListAsync(CoreRequestContext context, CancellationToken cancellationToken)
    {
        var states = await context.OperationalStateStore!.GetAllAsync();
        var views = states.Select(ToView).ToList();
        return new ApiResponse(true, views, null);
    }

    private static async Task<ApiResponse> HandleGetAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        if (!CoreRequestParsing.TryGetPayload(request, out InterfaceNameRequest nameRequest, out var error))
        {
            return new ApiResponse(false, null, error);
        }

        var state = await context.OperationalStateStore!.GetAsync(nameRequest.InterfaceName);
        if (state == null)
        {
            return new ApiResponse(false, null, "Interface not found");
        }

        return new ApiResponse(true, ToView(state), null);
    }

    private static async Task<ApiResponse> HandleRefreshAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        // Trigger a refresh of operational state for an interface
        // This re-reads the current state from the system
        if (!CoreRequestParsing.TryGetPayload(request, out InterfaceNameRequest nameRequest, out var error))
        {
            return new ApiResponse(false, null, error);
        }

        // Get current state from network inventory
        var networkInventory = new Services.NetworkInventoryService(context.CommandRunner);
        var addresses = await networkInventory.ListAddressesAsync(nameRequest.InterfaceName, cancellationToken);

        // Update operational state
        var state = await context.OperationalStateStore!.GetAsync(nameRequest.InterfaceName)
            ?? new InterfaceOperationalStateEntity { InterfaceName = nameRequest.InterfaceName };

        // Find IPv4 address if available
        var ipv4Addr = addresses.FirstOrDefault(a => a.Family == "inet");
        if (ipv4Addr != null)
        {
            state.CurrentIpv4Address = ipv4Addr.Address;
            state.CurrentIpv4Prefix = ipv4Addr.PrefixLength;
        }

        // Find IPv6 address if available
        var ipv6Addr = addresses.FirstOrDefault(a => a.Family == "inet6" && !a.Address.StartsWith("fe80:"));
        if (ipv6Addr != null)
        {
            state.CurrentIpv6Address = ipv6Addr.Address;
            state.CurrentIpv6Prefix = ipv6Addr.PrefixLength;
        }

        // If we found any addresses, the interface exists
        if (addresses.Count > 0)
        {
            state.LinkState = LinkState.Up;
            state.HealthStatus = InterfaceHealthStatus.Healthy;
        }
        else
        {
            // Interface might exist but have no addresses
            state.LinkState = LinkState.Unknown;
            state.HealthStatus = InterfaceHealthStatus.Unknown;
        }

        state.LastSeenAt = DateTime.UtcNow;
        state.LastIpChangeAt = DateTime.UtcNow;

        await context.OperationalStateStore.UpsertAsync(state);

        return new ApiResponse(true, ToView(state), null);
    }

    private static InterfaceOperationalStateView ToView(InterfaceOperationalStateEntity entity)
    {
        return new InterfaceOperationalStateView
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
            DhcpServerAddress = entity.DhcpServerAddress,
            DhcpLeaseObtained = entity.DhcpLeaseObtained,
            DhcpLeaseExpires = entity.DhcpLeaseExpires,
            DhcpGateway = entity.DhcpGateway,
            HealthStatus = entity.HealthStatus.ToString().ToLowerInvariant(),
            LastSeenAt = entity.LastSeenAt,
            LastLinkChangeAt = entity.LastLinkChangeAt,
            LastIpChangeAt = entity.LastIpChangeAt,
            TrafficStats = entity.RxBytes.HasValue ? new TrafficStatsView
            {
                RxBytes = entity.RxBytes ?? 0,
                TxBytes = entity.TxBytes ?? 0,
                RxPackets = entity.RxPackets ?? 0,
                TxPackets = entity.TxPackets ?? 0,
                RxErrors = entity.RxErrors ?? 0,
                TxErrors = entity.TxErrors ?? 0
            } : null
        };
    }
}

/// <summary>
/// Request with interface name.
/// </summary>
public sealed class InterfaceNameRequest
{
    public string InterfaceName { get; set; } = string.Empty;
}
