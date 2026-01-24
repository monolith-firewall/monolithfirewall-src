using System.Text.Json;
using System.Linq;
using Monolith.FireWall.Common.Services;
using Monolith.FireWall.Core.Models;
using Monolith.FireWall.Core.Services.Platform;
using Monolith.FireWall.Platform.Validation;

namespace Monolith.FireWall.Core.Services;

public sealed class GatewayManager
{
    private readonly GatewayStore _store;
    private readonly PlatformCommandRunner _commandRunner;
    private readonly LoggingManager _loggingManager;

    public GatewayManager(GatewayStore store, PlatformCommandRunner commandRunner)
    {
        _store = store;
        _commandRunner = commandRunner;
        _loggingManager = LoggingManager.Instance;
    }

    public async Task<List<GatewayView>> GetGatewaysAsync(CancellationToken cancellationToken)
    {
        await SyncDynamicGatewaysAsync(cancellationToken);
        var entities = await _store.GetGatewaysAsync();
        return entities
            .Select(ToView)
            .OrderByDescending(g => g.IsDefault)
            .ThenBy(g => g.AddressFamily, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.Interface ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.Metric ?? int.MaxValue)
            .ToList();
    }

    public async Task<(bool Success, string? Error, GatewayView? Gateway)> CreateStaticGatewayAsync(
        GatewayRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return (false, "Request is required", null);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return (false, "Gateway name is required", null);
        }

        if (string.IsNullOrWhiteSpace(request.Address))
        {
            return (false, "Gateway address is required", null);
        }

        var family = PlatformValidators.GetAddressFamily(request.Address);
        if (string.IsNullOrWhiteSpace(family))
        {
            return (false, "Invalid gateway address", null);
        }

        if (!string.IsNullOrWhiteSpace(request.Interface) &&
            !PlatformValidators.IsValidInterfaceName(request.Interface))
        {
            return (false, "Invalid interface name", null);
        }

        if (request.Metric.HasValue && request.Metric.Value < 0)
        {
            return (false, "Metric must be zero or positive", null);
        }

        var existing = await _store.GetByAddressAsync(request.Address);
        if (existing != null && !existing.IsDynamic)
        {
            return (false, "Gateway already exists", null);
        }

        var now = DateTime.UtcNow;
        var entity = new GatewayEntity
        {
            Name = request.Name.Trim(),
            Address = request.Address.Trim(),
            AddressFamily = family!,
            Interface = string.IsNullOrWhiteSpace(request.Interface) ? null : request.Interface.Trim(),
            Metric = request.Metric,
            IsDefault = request.IsDefault ?? false,
            IsDynamic = false,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
            LastSeenAt = now
        };

        var routeResult = await EnsureDefaultRouteAsync(entity, cancellationToken);
        if (!routeResult.Success)
        {
            return (false, routeResult.Error, null);
        }

        var saved = await _store.InsertAsync(entity);
        if (!saved)
        {
            return (false, "Failed to save gateway", null);
        }

        await _loggingManager.LogSystemAsync(
            "Routing",
            "info",
            "GatewayManager",
            $"Created static gateway {entity.Name}",
            new Dictionary<string, object>
            {
                ["address"] = entity.Address,
                ["family"] = entity.AddressFamily,
                ["interface"] = entity.Interface ?? string.Empty,
                ["metric"] = entity.Metric ?? 0,
                ["default"] = entity.IsDefault
            });

        return (true, null, ToView(entity));
    }

    public async Task<(bool Success, string? Error)> DeleteGatewayAsync(int id, CancellationToken cancellationToken)
    {
        var gateway = await _store.GetGatewayAsync(id);
        if (gateway == null)
        {
            return (false, "Gateway not found");
        }

        if (gateway.IsDynamic)
        {
            return (false, "Dynamic gateways are managed automatically");
        }

        if (gateway.IsDefault)
        {
            var removeResult = await RemoveDefaultRouteAsync(gateway, cancellationToken);
            if (!removeResult.Success)
            {
                return removeResult;
            }
        }

        var deleted = await _store.DeleteAsync(id);
        if (!deleted)
        {
            return (false, "Failed to delete gateway");
        }

        await _loggingManager.LogSystemAsync(
            "Routing",
            "warning",
            "GatewayManager",
            $"Deleted gateway {gateway.Name}",
            new Dictionary<string, object>
            {
                ["address"] = gateway.Address,
                ["family"] = gateway.AddressFamily,
                ["interface"] = gateway.Interface ?? string.Empty
            });

        return (true, null);
    }

    public async Task SyncDynamicGatewaysAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var dynamicGateways = await DiscoverDynamicGatewaysAsync(cancellationToken);
        var existing = await _store.GetGatewaysAsync();
        var existingByAddress = existing.ToDictionary(g => g.Address, StringComparer.OrdinalIgnoreCase);

        foreach (var dyn in dynamicGateways)
        {
            if (existingByAddress.TryGetValue(dyn.Address, out var current))
            {
                if (!current.IsDynamic)
                {
                    continue; // keep static
                }

                current.Interface = dyn.Interface;
                current.Metric = dyn.Metric;
                current.Description = dyn.Description;
                current.IsDefault = dyn.IsDefault;
                current.UpdatedAt = now;
                current.LastSeenAt = now;
                await _store.UpdateAsync(current);
            }
            else
            {
                dyn.CreatedAt = now;
                dyn.UpdatedAt = now;
                dyn.LastSeenAt = now;
                await _store.InsertAsync(dyn);
            }
        }

        var staleCutoff = now.AddMinutes(-2);
        foreach (var gateway in existing.Where(g => g.IsDynamic && g.LastSeenAt.HasValue && g.LastSeenAt.Value < staleCutoff))
        {
            await _store.DeleteAsync(gateway.Id);
        }
    }

    private async Task<(bool Success, string? Error)> EnsureDefaultRouteAsync(GatewayEntity gateway, CancellationToken cancellationToken)
    {
        if (!gateway.IsDefault)
        {
            return (true, null);
        }

        var familyFlag = gateway.AddressFamily.Equals("ipv6", StringComparison.OrdinalIgnoreCase) ? "-6 " : string.Empty;
        var args = $"{familyFlag}route add default via {gateway.Address}";
        if (!string.IsNullOrWhiteSpace(gateway.Interface))
        {
            args += $" dev {gateway.Interface}";
        }

        if (gateway.Metric.HasValue)
        {
            args += $" metric {gateway.Metric.Value}";
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
            var errorText = (result.StdErr ?? string.Empty).Trim();
            if (result.ExitCode != 2 && !errorText.Contains("File exists", StringComparison.OrdinalIgnoreCase))
            {
                var error = string.IsNullOrWhiteSpace(errorText) ? "Failed to add gateway route" : errorText;
                return (false, error);
            }
        }

        return (true, null);
    }

    private async Task<(bool Success, string? Error)> RemoveDefaultRouteAsync(GatewayEntity gateway, CancellationToken cancellationToken)
    {
        var familyFlag = gateway.AddressFamily.Equals("ipv6", StringComparison.OrdinalIgnoreCase) ? "-6 " : string.Empty;
        var args = $"{familyFlag}route del default via {gateway.Address}";
        if (!string.IsNullOrWhiteSpace(gateway.Interface))
        {
            args += $" dev {gateway.Interface}";
        }

        if (gateway.Metric.HasValue)
        {
            args += $" metric {gateway.Metric.Value}";
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
            var errorText = (result.StdErr ?? string.Empty).Trim();
            if (!errorText.Contains("Cannot find", StringComparison.OrdinalIgnoreCase))
            {
                var error = string.IsNullOrWhiteSpace(errorText) ? "Failed to remove gateway route" : errorText;
                return (false, error);
            }
        }

        return (true, null);
    }

    private async Task<List<GatewayEntity>> DiscoverDynamicGatewaysAsync(CancellationToken cancellationToken)
    {
        var gateways = new List<GatewayEntity>();
        gateways.AddRange(await ParseRoutesAsync("-j route show", "ipv4", cancellationToken));
        gateways.AddRange(await ParseRoutesAsync("-6 -j route show", "ipv6", cancellationToken));
        return gateways;
    }

    private async Task<List<GatewayEntity>> ParseRoutesAsync(string arguments, string family, CancellationToken cancellationToken)
    {
        var result = new List<GatewayEntity>();
        if (!_commandRunner.CommandExists("ip"))
        {
            return result;
        }

        var command = new PlatformCommand
        {
            FileName = "ip",
            Arguments = arguments,
            UseSudo = false,
            TimeoutMs = 5000
        };

        var response = await _commandRunner.RunAsync(command, cancellationToken);
        if (response.ExitCode != 0 || string.IsNullOrWhiteSpace(response.StdOut))
        {
            return result;
        }

        using var doc = JsonDocument.Parse(response.StdOut);
        foreach (var route in doc.RootElement.EnumerateArray())
        {
            var destination = route.TryGetProperty("dst", out var dstEl) ? dstEl.GetString() : null;
            var gateway = route.TryGetProperty("gateway", out var gwEl) ? gwEl.GetString() : null;
            var dev = route.TryGetProperty("dev", out var devEl) ? devEl.GetString() : null;
            var protocol = route.TryGetProperty("protocol", out var protoEl) ? protoEl.GetString() : null;
            var metric = route.TryGetProperty("metric", out var metricEl) && metricEl.ValueKind == JsonValueKind.Number
                ? metricEl.GetInt32()
                : (int?)null;

            var isDefault = string.IsNullOrWhiteSpace(destination)
                            || string.Equals(destination, "default", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(destination, "0.0.0.0/0", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(destination, "::/0", StringComparison.OrdinalIgnoreCase);

            if (!isDefault || string.IsNullOrWhiteSpace(gateway))
            {
                continue;
            }

            var isDhcp = !string.IsNullOrWhiteSpace(protocol) &&
                         (protocol.Contains("dhcp", StringComparison.OrdinalIgnoreCase) ||
                          protocol.Contains("ra", StringComparison.OrdinalIgnoreCase));

            if (!isDhcp)
            {
                continue;
            }

            result.Add(new GatewayEntity
            {
                Name = $"Dynamic ({dev ?? "unknown"})",
                Address = gateway,
                AddressFamily = family,
                Interface = dev,
                Metric = metric,
                IsDefault = true,
                IsDynamic = true,
                Description = $"Dynamic gateway ({protocol ?? "dhcp"})"
            });
        }

        return result;
    }

    private static GatewayView ToView(GatewayEntity entity)
    {
        return new GatewayView
        {
            Id = entity.Id,
            Name = entity.Name,
            Address = entity.Address,
            AddressFamily = entity.AddressFamily,
            Interface = entity.Interface,
            Metric = entity.Metric,
            IsDefault = entity.IsDefault,
            IsDynamic = entity.IsDynamic,
            Description = entity.Description,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            LastSeenAt = entity.LastSeenAt,
            Source = entity.IsDynamic ? "dynamic" : "static" // Always "static" or "dynamic", never "kernel"
        };
    }
}
