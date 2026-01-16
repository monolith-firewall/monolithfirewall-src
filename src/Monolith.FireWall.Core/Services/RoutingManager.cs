using System.Text.Json;
using Monolith.FireWall.Common.Services;
using Monolith.FireWall.Core.Models;
using Monolith.FireWall.Core.Services.Platform;
using Monolith.FireWall.Platform.Models;
using Monolith.FireWall.Platform.Validation;

namespace Monolith.FireWall.Core.Services;

public sealed class RoutingManager
{
    private readonly RoutingStore _store;
    private readonly GatewayManager _gatewayManager;
    private readonly PlatformCommandRunner _commandRunner;
    private readonly NetworkInventoryService _inventory;
    private readonly LoggingManager _loggingManager;

    public RoutingManager(
        RoutingStore store,
        GatewayManager gatewayManager,
        PlatformCommandRunner commandRunner,
        NetworkInventoryService inventory)
    {
        _store = store;
        _gatewayManager = gatewayManager;
        _commandRunner = commandRunner;
        _inventory = inventory;
        _loggingManager = LoggingManager.Instance;
    }

    public async Task<List<GatewayView>> GetGatewaysAsync(CancellationToken cancellationToken)
    {
        return await _gatewayManager.GetGatewaysAsync(cancellationToken);
    }

    public Task<(bool Success, string? Error, GatewayView? Gateway)> CreateGatewayAsync(
        GatewayRequest request,
        CancellationToken cancellationToken)
    {
        return _gatewayManager.CreateStaticGatewayAsync(request, cancellationToken);
    }

    public Task<(bool Success, string? Error)> DeleteGatewayAsync(int id, CancellationToken cancellationToken)
    {
        return _gatewayManager.DeleteGatewayAsync(id, cancellationToken);
    }

    public Task SyncGatewaysAsync(CancellationToken cancellationToken)
    {
        return _gatewayManager.SyncDynamicGatewaysAsync(cancellationToken);
    }

    public async Task<List<StaticRouteView>> GetStaticRoutesAsync(CancellationToken cancellationToken)
    {
        var stored = await _store.GetRoutesAsync();
        var systemRoutes = await ListSystemRoutesAsync(cancellationToken);
        var activeSet = new HashSet<string>(systemRoutes.Select(BuildRouteKey), StringComparer.OrdinalIgnoreCase);

        return stored.Select(route => new StaticRouteView
        {
            Id = route.Id,
            Destination = route.DestinationCidr,
            Gateway = route.Gateway,
            Interface = route.Interface,
            Metric = route.Metric,
            Description = route.Description,
            AddressFamily = route.AddressFamily,
            Active = activeSet.Contains(BuildRouteKey(route.DestinationCidr, route.Gateway, route.Interface))
        }).ToList();
    }

    public async Task<(bool Success, string? Error, StaticRouteView? Route)> AddRouteAsync(
        StaticRouteRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Destination))
        {
            return (false, "Destination is required", null);
        }

        var destination = NormalizeDestination(request.Destination);
        if (!PlatformValidators.TryParseCidr(destination, out var cidrAddress, out _))
        {
            return (false, "Invalid destination CIDR", null);
        }
        var addressFamily = cidrAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
            ? "ipv6"
            : "ipv4";

        if (string.IsNullOrWhiteSpace(request.Gateway) && string.IsNullOrWhiteSpace(request.Interface))
        {
            return (false, "Gateway or interface is required", null);
        }

        if (!string.IsNullOrWhiteSpace(request.Gateway) && !PlatformValidators.IsValidIp(request.Gateway))
        {
            return (false, "Invalid gateway address", null);
        }

        var interfaces = await _inventory.ListInterfacesAsync();
        if (!string.IsNullOrWhiteSpace(request.Interface))
        {
            if (!PlatformValidators.IsValidInterfaceName(request.Interface))
            {
                return (false, "Invalid interface name", null);
            }

            if (!interfaces.Any(i => string.Equals(i.Name, request.Interface, StringComparison.OrdinalIgnoreCase)))
            {
                return (false, "Interface not found", null);
            }
        }

        var existing = await _store.GetRoutesAsync();
        if (existing.Any(r =>
            string.Equals(r.DestinationCidr, destination, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(r.Gateway ?? string.Empty, request.Gateway ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(r.Interface ?? string.Empty, request.Interface ?? string.Empty, StringComparison.OrdinalIgnoreCase)))
        {
            return (false, "Route already exists", null);
        }

        var addResult = await RunRouteCommandAsync("add", destination, request.Gateway, request.Interface, request.Metric, addressFamily, cancellationToken);
        if (!addResult.Success)
        {
            return (false, addResult.Error, null);
        }

        var entity = new StaticRouteEntity
        {
            DestinationCidr = destination,
            Gateway = string.IsNullOrWhiteSpace(request.Gateway) ? null : request.Gateway,
            Interface = string.IsNullOrWhiteSpace(request.Interface) ? null : request.Interface,
            Metric = request.Metric,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            AddressFamily = addressFamily,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var saved = await _store.InsertAsync(entity);
        if (!saved)
        {
            return (false, "Failed to save route", null);
        }

        await _loggingManager.LogSystemAsync(
            "Routing",
            "info",
            "RoutingManager",
            $"Added static route {destination}",
            new Dictionary<string, object>
            {
                ["destination"] = destination,
                ["gateway"] = entity.Gateway ?? string.Empty,
                ["interface"] = entity.Interface ?? string.Empty
            });

        return (true, null, new StaticRouteView
        {
            Id = entity.Id,
            Destination = entity.DestinationCidr,
            Gateway = entity.Gateway,
            Interface = entity.Interface,
            Metric = entity.Metric,
            Description = entity.Description,
            Active = true
        });
    }

    public async Task<(bool Success, string? Error)> DeleteRouteAsync(int id, CancellationToken cancellationToken)
    {
        var route = await _store.GetRouteAsync(id);
        if (route == null)
        {
            return (false, "Route not found");
        }

        var deleteResult = await RunRouteCommandAsync("del", route.DestinationCidr, route.Gateway, route.Interface, route.Metric, route.AddressFamily, cancellationToken);
        if (!deleteResult.Success)
        {
            return (false, deleteResult.Error);
        }

        var removed = await _store.DeleteAsync(id);
        if (!removed)
        {
            return (false, "Failed to remove route");
        }

        await _loggingManager.LogSystemAsync(
            "Routing",
            "warning",
            "RoutingManager",
            $"Removed static route {route.DestinationCidr}",
            new Dictionary<string, object>
            {
                ["destination"] = route.DestinationCidr,
                ["gateway"] = route.Gateway ?? string.Empty,
                ["interface"] = route.Interface ?? string.Empty
            });

        return (true, null);
    }

    public async Task<RoutingStatusView> GetRoutingStatusAsync(CancellationToken cancellationToken)
    {
        var systemRoutes = await ListSystemRoutesAsync(cancellationToken);
        var routeViews = systemRoutes.Select(r => new RouteSummaryView
        {
            Destination = r.Destination,
            Gateway = r.Gateway,
            Interface = r.Interface,
            Protocol = r.Protocol,
            Metric = r.Metric,
            IsDefault = r.IsDefault,
            AddressFamily = r.AddressFamily
        }).ToList();

        var status = new RoutingStatusView
        {
            IpForwardingEnabled = await GetIpForwardingStatusAsync(cancellationToken),
            DefaultGateway = await GetDefaultGatewayAsync(cancellationToken),
            Routes = routeViews,
            NatMasqueradeEnabled = await CheckNatMasqueradeAsync(cancellationToken)
        };

        return status;
    }

    private async Task<bool> GetIpForwardingStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            var command = new PlatformCommand
            {
                FileName = "cat",
                Arguments = "/proc/sys/net/ipv4/ip_forward",
                UseSudo = false,
                TimeoutMs = 2000
            };

            var result = await _commandRunner.RunAsync(command, cancellationToken);
            if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StdOut))
            {
                return result.StdOut.Trim() == "1";
            }
        }
        catch
        {
            // Ignore errors
        }

        return false;
    }

    private async Task<GatewayView?> GetDefaultGatewayAsync(CancellationToken cancellationToken)
    {
        var gateways = await GetGatewaysAsync(cancellationToken);
        return gateways.FirstOrDefault(g => g.IsDefault);
    }

    private async Task<bool> CheckNatMasqueradeAsync(CancellationToken cancellationToken)
    {
        if (!_commandRunner.CommandExists("nft"))
        {
            return false;
        }

        try
        {
            var command = new PlatformCommand
            {
                FileName = "nft",
                Arguments = "list table ip monolith_nat",
                UseSudo = true,
                TimeoutMs = 3000
            };

            var result = await _commandRunner.RunAsync(command, cancellationToken);
            if (result.ExitCode != 0)
            {
                return false; // Table doesn't exist
            }

            // Check if output contains masquerade
            var output = result.StdOut ?? string.Empty;
            return output.Contains("masquerade", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private async Task<List<RouteEntry>> ListSystemRoutesAsync(CancellationToken cancellationToken)
    {
        var routes = new List<RouteEntry>();
        routes.AddRange(await ParseRoutesAsync("-j route show", "ipv4", cancellationToken));
        routes.AddRange(await ParseRoutesAsync("-6 -j route show", "ipv6", cancellationToken));
        return routes;
    }

    private async Task<(bool Success, string? Error)> RunRouteCommandAsync(
        string verb,
        string destination,
        string? gateway,
        string? iface,
        int? metric,
        string addressFamily,
        CancellationToken cancellationToken)
    {
        var familyFlag = string.Equals(addressFamily, "ipv6", StringComparison.OrdinalIgnoreCase) ? "-6 " : string.Empty;
        var args = $"{familyFlag}route {verb} {destination}";
        if (!string.IsNullOrWhiteSpace(gateway))
        {
            args += $" via {gateway}";
        }

        if (!string.IsNullOrWhiteSpace(iface))
        {
            args += $" dev {iface}";
        }

        if (metric.HasValue)
        {
            args += $" metric {metric.Value}";
        }

        var command = new PlatformCommand
        {
            FileName = "ip",
            Arguments = args,
            UseSudo = true,
            TimeoutMs = 5000
        };

        var result = await _commandRunner.RunAsync(command, cancellationToken);
        if (result.ExitCode != 0)
        {
            var error = string.IsNullOrWhiteSpace(result.StdErr) ? "Failed to update route" : result.StdErr.Trim();
            return (false, error);
        }

        return (true, null);
    }

    private static string NormalizeDestination(string destination)
    {
        if (string.Equals(destination.Trim(), "default", StringComparison.OrdinalIgnoreCase))
        {
            return "0.0.0.0/0";
        }

        return destination.Trim();
    }

    private static string BuildRouteKey(RouteEntry entry)
    {
        return BuildRouteKey(entry.Destination, entry.Gateway, entry.Interface);
    }

    private static string BuildRouteKey(string destination, string? gateway, string? iface)
    {
        return $"{NormalizeDestination(destination)}|{gateway ?? ""}|{iface ?? ""}";
    }

    private async Task<List<RouteEntry>> ParseRoutesAsync(string arguments, string family, CancellationToken cancellationToken)
    {
        var routes = new List<RouteEntry>();
        if (!_commandRunner.CommandExists("ip"))
        {
            return routes;
        }

        var command = new PlatformCommand
        {
            FileName = "ip",
            Arguments = arguments,
            UseSudo = false,
            TimeoutMs = 5000
        };

        var result = await _commandRunner.RunAsync(command, cancellationToken);
        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StdOut))
        {
            return routes;
        }

        using var doc = JsonDocument.Parse(result.StdOut);
        foreach (var route in doc.RootElement.EnumerateArray())
        {
            var destination = route.TryGetProperty("dst", out var dstEl) ? dstEl.GetString() ?? "default" : "default";
            var gateway = route.TryGetProperty("gateway", out var gwEl) ? gwEl.GetString() : null;
            var dev = route.TryGetProperty("dev", out var devEl) ? devEl.GetString() : null;
            var protocol = route.TryGetProperty("protocol", out var protoEl) ? protoEl.GetString() : null;
            var metric = route.TryGetProperty("metric", out var metricEl) && metricEl.ValueKind == JsonValueKind.Number
                ? metricEl.GetInt32()
                : (int?)null;

            routes.Add(new RouteEntry
            {
                Destination = destination,
                Gateway = gateway,
                Interface = dev,
                Protocol = protocol,
                Metric = metric,
                AddressFamily = family
            });
        }

        return routes;
    }

    private sealed class RouteEntry
    {
        public string Destination { get; set; } = string.Empty;
        public string? Gateway { get; set; }
        public string? Interface { get; set; }
        public string? Protocol { get; set; }
        public int? Metric { get; set; }
        public string AddressFamily { get; set; } = "ipv4";
        public bool IsDefault =>
            string.Equals(Destination, "default", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Destination, "0.0.0.0/0", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Destination, "::/0", StringComparison.OrdinalIgnoreCase);
    }
}
