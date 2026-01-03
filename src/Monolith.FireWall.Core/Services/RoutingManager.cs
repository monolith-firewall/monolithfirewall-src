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
    private readonly PlatformCommandRunner _commandRunner;
    private readonly NetworkInventoryService _inventory;
    private readonly LoggingManager _loggingManager;

    public RoutingManager(
        RoutingStore store,
        PlatformCommandRunner commandRunner,
        NetworkInventoryService inventory)
    {
        _store = store;
        _commandRunner = commandRunner;
        _inventory = inventory;
        _loggingManager = LoggingManager.Instance;
    }

    public async Task<List<GatewayView>> GetGatewaysAsync(CancellationToken cancellationToken)
    {
        var routes = await ListSystemRoutesAsync(cancellationToken);
        var gateways = new List<GatewayView>();

        foreach (var route in routes)
        {
            if (!route.IsDefault)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(route.Gateway))
            {
                continue;
            }

            gateways.Add(new GatewayView
            {
                Name = $"Default ({route.Interface ?? "unknown"})",
                Address = route.Gateway ?? string.Empty,
                Interface = route.Interface ?? string.Empty,
                Source = ResolveGatewaySource(route.Protocol),
                Metric = route.Metric,
                IsDefault = true
            });
        }

        return gateways;
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
        if (!PlatformValidators.TryParseCidr(destination, out _, out _))
        {
            return (false, "Invalid destination CIDR", null);
        }

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

        var addResult = await RunRouteCommandAsync("add", destination, request.Gateway, request.Interface, request.Metric, cancellationToken);
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

        var deleteResult = await RunRouteCommandAsync("del", route.DestinationCidr, route.Gateway, route.Interface, route.Metric, cancellationToken);
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

    private async Task<List<RouteEntry>> ListSystemRoutesAsync(CancellationToken cancellationToken)
    {
        var routes = new List<RouteEntry>();
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
                Metric = metric
            });
        }

        return routes;
    }

    private async Task<(bool Success, string? Error)> RunRouteCommandAsync(
        string verb,
        string destination,
        string? gateway,
        string? iface,
        int? metric,
        CancellationToken cancellationToken)
    {
        var args = $"route {verb} {destination}";
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

    private static string ResolveGatewaySource(string? protocol)
    {
        if (string.IsNullOrWhiteSpace(protocol))
        {
            return "static";
        }

        return protocol.Contains("dhcp", StringComparison.OrdinalIgnoreCase) ? "dhcp" : protocol;
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

    private sealed class RouteEntry
    {
        public string Destination { get; set; } = string.Empty;
        public string? Gateway { get; set; }
        public string? Interface { get; set; }
        public string? Protocol { get; set; }
        public int? Metric { get; set; }
        public bool IsDefault =>
            string.Equals(Destination, "default", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Destination, "0.0.0.0/0", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Destination, "::/0", StringComparison.OrdinalIgnoreCase);
    }
}
