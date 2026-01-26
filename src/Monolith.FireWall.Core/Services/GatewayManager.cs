using System.Text.Json;
using System.Linq;
using System.Text.RegularExpressions;
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
    private readonly InterfaceAssignmentStore? _interfaceStore;
    private readonly GatewayHealthStore? _healthStore;
    private HashSet<string>? _cachedDhcpInterfaces;
    private DateTime _dhcpCacheTime = DateTime.MinValue;
    private static readonly TimeSpan DhcpCacheTimeout = TimeSpan.FromMinutes(1);

    public GatewayManager(
        GatewayStore store,
        PlatformCommandRunner commandRunner,
        InterfaceAssignmentStore? interfaceStore = null,
        GatewayHealthStore? healthStore = null)
    {
        _store = store;
        _commandRunner = commandRunner;
        _interfaceStore = interfaceStore;
        _healthStore = healthStore;
        _loggingManager = LoggingManager.Instance;
    }

    public async Task<List<GatewayView>> GetGatewaysAsync(CancellationToken cancellationToken)
    {
        // Always sync dynamic gateways before returning to ensure we have the latest
        await SyncDynamicGatewaysAsync(cancellationToken);
        var entities = await _store.GetGatewaysAsync();

        // Get health data for all gateways if health store is available
        Dictionary<int, GatewayHealthEntity>? healthMap = null;
        if (_healthStore != null)
        {
            try
            {
                var healthData = await _healthStore.GetAllHealthAsync();
                healthMap = healthData.ToDictionary(h => h.GatewayId);
            }
            catch
            {
                // Continue without health data if unavailable
            }
        }

        return entities
            .Select(e => ToView(e, healthMap?.GetValueOrDefault(e.Id)))
            .OrderByDescending(g => g.IsDefault)
            .ThenBy(g => g.AddressFamily, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.Interface ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.Metric ?? int.MaxValue)
            .ToList();
    }

    /// <summary>
    /// Initialize gateways on first run by syncing dynamic gateways from the system.
    /// This is called during initial setup to import existing gateways.
    /// </summary>
    public async Task InitializeGatewaysAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SyncDynamicGatewaysAsync(cancellationToken);
            var gateways = await _store.GetGatewaysAsync();
            await _loggingManager.LogSystemAsync(
                "Routing",
                "info",
                "GatewayManager",
                $"Initialized {gateways.Count} gateway(s) on first run",
                new Dictionary<string, object>
                {
                    ["dynamicCount"] = gateways.Count(g => g.IsDynamic),
                    ["staticCount"] = gateways.Count(g => !g.IsDynamic),
                    ["totalCount"] = gateways.Count
                });
        }
        catch (Exception ex)
        {
            await _loggingManager.LogSystemAsync(
                "Routing",
                "error",
                "GatewayManager",
                $"Failed to initialize gateways: {ex.Message}",
                new Dictionary<string, object>
                {
                    ["error"] = ex.Message
                });
            throw;
        }
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
        // Get existing static gateways once for fallback detection
        var existingGateways = await _store.GetGatewaysAsync();
        var staticGatewayAddresses = existingGateways
            .Where(g => !g.IsDynamic)
            .Select(g => g.Address)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var gateways = new List<GatewayEntity>();
        gateways.AddRange(await ParseRoutesAsync("-j route show", "ipv4", cancellationToken, staticGatewayAddresses));
        gateways.AddRange(await ParseRoutesAsync("-6 -j route show", "ipv6", cancellationToken, staticGatewayAddresses));
        return gateways;
    }

    private async Task<List<GatewayEntity>> ParseRoutesAsync(string arguments, string family, CancellationToken cancellationToken, HashSet<string> staticGatewayAddresses)
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

        // Get list of interfaces with DHCP configured to help identify dynamic gateways
        var dhcpInterfaces = await GetDhcpInterfacesAsync(cancellationToken);

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

            // Check if this is a DHCP gateway:
            // 1. Protocol explicitly contains "dhcp" or "ra"
            // 2. Protocol is "dhcp" or "ra" 
            // 3. Protocol is "boot" (systemd-networkd)
            // 4. Protocol is "kernel" or "static" but interface has DHCP configured (common case)
            // 5. No protocol but interface has DHCP configured
            // 6. FALLBACK: If no static gateway exists for this address, treat as dynamic (common on fresh installs)
            //    This is the most important fallback for fresh installs where DHCP config might not be detected
            var isDhcp = false;
            var protocolName = "dhcp";

            // First check: Explicit DHCP protocol indicators
            if (!string.IsNullOrWhiteSpace(protocol))
            {
                var protocolLower = protocol.ToLowerInvariant();
                isDhcp = protocolLower.Contains("dhcp") || 
                         protocolLower.Contains("ra") ||
                         protocolLower == "boot";
                if (isDhcp)
                {
                    protocolName = protocol;
                }
            }

            // Second check: If interface has DHCP configured, treat it as dynamic regardless of protocol
            // This is reliable when we can detect DHCP config
            if (!isDhcp && !string.IsNullOrWhiteSpace(dev) && dhcpInterfaces.Contains(dev, StringComparer.OrdinalIgnoreCase))
            {
                isDhcp = true;
                protocolName = !string.IsNullOrWhiteSpace(protocol) ? protocol : "dhcp";
            }

            // Third check: FALLBACK - if protocol is "kernel" and no static gateway exists in DB,
            // treat as dynamic (common on fresh installs where DHCP config isn't detected)
            // This is the critical fallback that ensures gateways are imported on fresh installs
            if (!isDhcp && !string.IsNullOrWhiteSpace(protocol) && protocol.Equals("kernel", StringComparison.OrdinalIgnoreCase))
            {
                // If no static gateway exists for this address, assume this is dynamic (likely DHCP-assigned)
                // This handles the common case where DHCP assigns routes but they show as "kernel" protocol
                if (!staticGatewayAddresses.Contains(gateway))
                {
                    isDhcp = true;
                    protocolName = "dhcp";
                }
            }

            // Final fallback: If we still haven't detected it and there's no static gateway,
            // and it's a default route, treat as dynamic (safest assumption on fresh install)
            if (!isDhcp && !staticGatewayAddresses.Contains(gateway))
            {
                isDhcp = true;
                protocolName = !string.IsNullOrWhiteSpace(protocol) ? protocol : "dhcp";
            }

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
                Description = $"Dynamic gateway ({protocolName})"
            });
        }

        return result;
    }

    private async Task<HashSet<string>> GetDhcpInterfacesAsync(CancellationToken cancellationToken)
    {
        // Use cached result if recent
        if (_cachedDhcpInterfaces != null && DateTime.UtcNow - _dhcpCacheTime < DhcpCacheTimeout)
        {
            return _cachedDhcpInterfaces;
        }

        var dhcpInterfaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        try
        {
            // Method 1: Check interface assignments from database (if available)
            if (_interfaceStore != null)
            {
                try
                {
                    var assignments = await _interfaceStore.GetAssignmentsAsync();
                    foreach (var assignment in assignments)
                    {
                        // Check if assignment has DHCP configured
                        if (assignment.IpMode == InterfaceIpMode.Dhcp || assignment.Ipv6Mode == InterfaceIpMode.Dhcp)
                        {
                            dhcpInterfaces.Add(assignment.InterfaceName);
                        }
                    }
                }
                catch
                {
                    // Continue to file-based detection
                }
            }

            // Method 2: Check systemd-networkd configuration files
            var networkdDir = "/etc/systemd/network";
            if (Directory.Exists(networkdDir))
            {
                var networkFiles = Directory.GetFiles(networkdDir, "*.network", SearchOption.TopDirectoryOnly);
                foreach (var file in networkFiles)
                {
                    try
                    {
                        var content = await File.ReadAllTextAsync(file, cancellationToken);
                        if (content.Contains("[DHCP]", StringComparison.OrdinalIgnoreCase) ||
                            content.Contains("DHCP=yes", StringComparison.OrdinalIgnoreCase) ||
                            content.Contains("DHCP=ipv4", StringComparison.OrdinalIgnoreCase) ||
                            content.Contains("DHCP=ipv6", StringComparison.OrdinalIgnoreCase))
                        {
                            // Extract interface name from [Match] section or filename
                            var matchSection = Regex.Match(content, @"\[Match\]\s+Name\s*=\s*(\S+)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
                            if (matchSection.Success)
                            {
                                dhcpInterfaces.Add(matchSection.Groups[1].Value.Trim());
                            }
                            else
                            {
                                // Fallback: try to extract from filename (e.g., "10-eth0.network")
                                var fileName = Path.GetFileNameWithoutExtension(file);
                                var parts = fileName.Split('-');
                                if (parts.Length > 1)
                                {
                                    // Last part is often the interface name
                                    var potentialInterface = parts[parts.Length - 1];
                                    if (!string.IsNullOrWhiteSpace(potentialInterface))
                                    {
                                        dhcpInterfaces.Add(potentialInterface);
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Skip files that can't be read
                    }
                }
            }

            // Method 2.5: Check if interfaces are using DHCP via networkctl (systemd-networkd)
            // This is more reliable than config files since it shows actual runtime state
            if (_commandRunner.CommandExists("networkctl"))
            {
                try
                {
                    var networkctlCommand = new PlatformCommand
                    {
                        FileName = "networkctl",
                        Arguments = "list",
                        UseSudo = false,
                        TimeoutMs = 3000
                    };
                    var networkctlResult = await _commandRunner.RunAsync(networkctlCommand, cancellationToken);
                    if (networkctlResult.ExitCode == 0 && !string.IsNullOrWhiteSpace(networkctlResult.StdOut))
                    {
                        // Parse networkctl output to find interfaces with DHCP
                        var lines = networkctlResult.StdOut.Split('\n');
                        foreach (var line in lines)
                        {
                            if (line.Contains("routable", StringComparison.OrdinalIgnoreCase) || 
                                line.Contains("configured", StringComparison.OrdinalIgnoreCase))
                            {
                                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                                if (parts.Length >= 2)
                                {
                                    var ifaceName = parts[0].Trim();
                                    if (!string.IsNullOrWhiteSpace(ifaceName) && ifaceName != "IDX")
                                    {
                                        // Check if this interface has DHCP configured
                                        var statusCommand = new PlatformCommand
                                        {
                                            FileName = "networkctl",
                                            Arguments = $"status {ifaceName}",
                                            UseSudo = false,
                                            TimeoutMs = 2000
                                        };
                                        var statusResult = await _commandRunner.RunAsync(statusCommand, cancellationToken);
                                        if (statusResult.ExitCode == 0 && !string.IsNullOrWhiteSpace(statusResult.StdOut))
                                        {
                                            if (statusResult.StdOut.Contains("DHCP", StringComparison.OrdinalIgnoreCase) ||
                                                statusResult.StdOut.Contains("dhcp", StringComparison.OrdinalIgnoreCase))
                                            {
                                                dhcpInterfaces.Add(ifaceName);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Skip if networkctl fails
                }
            }

            // Method 3: Check /etc/network/interfaces
            var interfacesFile = "/etc/network/interfaces";
            if (File.Exists(interfacesFile))
            {
                try
                {
                    var content = await File.ReadAllTextAsync(interfacesFile, cancellationToken);
                    var lines = content.Split('\n');
                    string? currentInterface = null;
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("iface ", StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2)
                            {
                                currentInterface = parts[1];
                            }
                        }
                        else if (trimmed.StartsWith("dhcp", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(currentInterface))
                        {
                            dhcpInterfaces.Add(currentInterface);
                            currentInterface = null;
                        }
                        else if (trimmed.StartsWith("auto ", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("allow-", StringComparison.OrdinalIgnoreCase))
                        {
                            // Reset current interface when we hit a new section
                            currentInterface = null;
                        }
                    }
                }
                catch
                {
                    // Skip if file can't be read
                }
            }
        }
        catch
        {
            // If we can't check DHCP configs, continue without this information
        }

        // Cache the result
        _cachedDhcpInterfaces = dhcpInterfaces;
        _dhcpCacheTime = DateTime.UtcNow;

        return dhcpInterfaces;
    }

    private static GatewayView ToView(GatewayEntity entity, GatewayHealthEntity? health = null)
    {
        var view = new GatewayView
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

        // Add health information if available
        if (health != null)
        {
            view.HealthStatus = health.Status.ToString().ToLowerInvariant();
            view.LatencyMs = health.LatencyMs;
            view.PacketLossPercent = health.PacketLossPercent;
            view.LastHealthCheckAt = health.LastCheckAt;
        }

        return view;
    }
}
