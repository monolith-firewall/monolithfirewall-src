using System.Text.Json;
using Monolith.FireWall.Common.Services;
using Monolith.FireWall.Core.Models;
using Monolith.FireWall.Core.Services.Firewall;
using Monolith.FireWall.Core.Services.Platform;
using Monolith.FireWall.Platform.Models;

namespace Monolith.FireWall.Core.Services;

/// <summary>
/// Captures the initial network state on first boot/fresh install.
/// This creates the baseline for the operational state database.
/// </summary>
public sealed class InitialNetworkCaptureService
{
    private readonly InterfaceOperationalStateStore _operationalStateStore;
    private readonly InterfaceAssignmentStore _assignmentStore;
    private readonly GatewayStore _gatewayStore;
    private readonly GatewayHealthStore _healthStore;
    private readonly FirewallDynamicAliasStore _dynamicAliasStore;
    private readonly PlatformCommandRunner _commandRunner;
    private readonly LoggingManager _loggingManager;

    public InitialNetworkCaptureService(
        InterfaceOperationalStateStore operationalStateStore,
        InterfaceAssignmentStore assignmentStore,
        GatewayStore gatewayStore,
        GatewayHealthStore healthStore,
        FirewallDynamicAliasStore dynamicAliasStore,
        PlatformCommandRunner commandRunner)
    {
        _operationalStateStore = operationalStateStore;
        _assignmentStore = assignmentStore;
        _gatewayStore = gatewayStore;
        _healthStore = healthStore;
        _dynamicAliasStore = dynamicAliasStore;
        _commandRunner = commandRunner;
        _loggingManager = LoggingManager.Instance;
    }

    /// <summary>
    /// Captures the complete network baseline on first boot.
    /// This should be called early in the startup sequence before the setup wizard.
    /// </summary>
    public async Task<NetworkCaptureResult> CaptureNetworkBaselineAsync(CancellationToken cancellationToken = default)
    {
        var result = new NetworkCaptureResult();
        var now = DateTime.UtcNow;

        try
        {
            await _loggingManager.LogSystemAsync(
                "Network",
                "info",
                "InitialCapture",
                "Starting network baseline capture...");

            // Step 1: Enumerate all network interfaces
            var interfaces = await EnumerateInterfacesAsync(cancellationToken);
            result.InterfacesDiscovered = interfaces.Count;

            await _loggingManager.LogSystemAsync(
                "Network",
                "info",
                "InitialCapture",
                $"Discovered {interfaces.Count} network interfaces");

            // Step 2: Get IP addresses for all interfaces
            var addresses = await GetAllAddressesAsync(cancellationToken);

            // Step 3: Get routing information
            var routes = await GetRoutesAsync(cancellationToken);
            var defaultGateways = routes.Where(r =>
                r.Destination == "default" ||
                r.Destination == "0.0.0.0/0" ||
                r.Destination == "::/0").ToList();

            result.GatewaysDiscovered = defaultGateways.Count;

            // Step 4: Get DNS resolvers
            var dnsServers = await GetDnsServersAsync(cancellationToken);
            result.DnsServersDiscovered = dnsServers.Count;

            // Step 5: Populate operational state for each interface
            foreach (var iface in interfaces)
            {
                if (ShouldSkipInterface(iface.Name))
                {
                    continue;
                }

                var ifaceAddresses = addresses.Where(a => a.Interface == iface.Name).ToList();
                var ipv4Addr = ifaceAddresses.FirstOrDefault(a => a.Family == "inet");
                var ipv6Addr = ifaceAddresses.FirstOrDefault(a =>
                    a.Family == "inet6" && !a.Address.StartsWith("fe80:"));

                var linkInfo = await GetLinkInfoAsync(iface.Name, cancellationToken);
                var stats = await GetTrafficStatsAsync(iface.Name, cancellationToken);

                var opState = new InterfaceOperationalStateEntity
                {
                    InterfaceName = iface.Name,
                    LinkState = ParseLinkState(iface.OperState),
                    MacAddress = iface.MacAddress,
                    SpeedMbps = linkInfo?.SpeedMbps,
                    Duplex = linkInfo?.Duplex,
                    Mtu = iface.Mtu,
                    CurrentIpv4Address = ipv4Addr?.Address,
                    CurrentIpv4Prefix = ipv4Addr?.PrefixLength,
                    CurrentIpv6Address = ipv6Addr?.Address,
                    CurrentIpv6Prefix = ipv6Addr?.PrefixLength,
                    HealthStatus = iface.IsUp ? InterfaceHealthStatus.Healthy : InterfaceHealthStatus.Down,
                    LastSeenAt = now,
                    RxBytes = stats?.RxBytes,
                    TxBytes = stats?.TxBytes,
                    RxPackets = stats?.RxPackets,
                    TxPackets = stats?.TxPackets,
                    RxErrors = stats?.RxErrors,
                    TxErrors = stats?.TxErrors
                };

                // Check if this interface has a default gateway (likely DHCP WAN)
                var ifaceGateway = defaultGateways.FirstOrDefault(g => g.Interface == iface.Name);
                if (ifaceGateway != null)
                {
                    opState.DhcpGateway = ifaceGateway.Gateway;
                }

                await _operationalStateStore.UpsertAsync(opState);
            }

            // Step 6: Import gateways
            foreach (var route in defaultGateways)
            {
                if (string.IsNullOrWhiteSpace(route.Gateway))
                {
                    continue;
                }

                var existing = await _gatewayStore.GetByAddressAsync(route.Gateway);
                if (existing != null)
                {
                    continue;
                }

                var family = route.Gateway.Contains(':') ? "ipv6" : "ipv4";
                var gateway = new GatewayEntity
                {
                    Name = $"Dynamic ({route.Interface ?? "unknown"})",
                    Address = route.Gateway,
                    AddressFamily = family,
                    Interface = route.Interface,
                    IsDefault = true,
                    IsDynamic = true,
                    Description = "Discovered on first boot",
                    CreatedAt = now,
                    UpdatedAt = now,
                    LastSeenAt = now
                };

                await _gatewayStore.InsertAsync(gateway);

                // Initialize health record
                await _healthStore.UpsertHealthAsync(new GatewayHealthEntity
                {
                    GatewayId = gateway.Id,
                    Status = GatewayHealthStatus.Unknown
                });
            }

            // Step 7: Suggest interface assignments based on heuristics
            result.SuggestedAssignments = await SuggestInterfaceAssignmentsAsync(interfaces, addresses, defaultGateways);

            result.Success = true;
            await _loggingManager.LogSystemAsync(
                "Network",
                "info",
                "InitialCapture",
                $"Network baseline capture complete: {result.InterfacesDiscovered} interfaces, {result.GatewaysDiscovered} gateways, {result.DnsServersDiscovered} DNS servers");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            await _loggingManager.LogSystemAsync(
                "Network",
                "error",
                "InitialCapture",
                $"Network baseline capture failed: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Creates interface assignments from suggested roles.
    /// Called by the setup wizard when user confirms assignments.
    /// </summary>
    public async Task ApplySuggestedAssignmentsAsync(
        List<SuggestedInterfaceAssignment> assignments,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        foreach (var suggestion in assignments.Where(a => a.Role != InterfaceRole.Unknown))
        {
            var existing = await _assignmentStore.GetAssignmentAsync(suggestion.InterfaceName);
            if (existing != null)
            {
                // Update existing
                existing.Role = suggestion.Role;
                existing.Name = suggestion.SuggestedName;
                existing.IpMode = suggestion.SuggestedIpMode;
                existing.IpAddress = suggestion.CurrentIpAddress;
                existing.PrefixLength = suggestion.CurrentPrefix;
                existing.Gateway = suggestion.CurrentGateway;
                existing.UpdatedAt = now;
                await _assignmentStore.UpsertAsync(existing);
            }
            else
            {
                // Create new
                var entity = new InterfaceAssignmentEntity
                {
                    InterfaceName = suggestion.InterfaceName,
                    Name = suggestion.SuggestedName,
                    Type = InterfaceAssignmentType.Physical,
                    Role = suggestion.Role,
                    IpMode = suggestion.SuggestedIpMode,
                    IpAddress = suggestion.CurrentIpAddress,
                    PrefixLength = suggestion.CurrentPrefix,
                    Gateway = suggestion.CurrentGateway,
                    IsManagement = suggestion.Role == InterfaceRole.Lan,
                    UpdatedAt = now
                };
                await _assignmentStore.UpsertAsync(entity);
            }

            // Create dynamic aliases for the interface
            var roleName = suggestion.Role switch
            {
                InterfaceRole.Wan => "wan",
                InterfaceRole.Lan => "lan",
                InterfaceRole.Opt => $"opt{suggestion.InterfaceName}",
                _ => null
            };

            if (!string.IsNullOrEmpty(roleName))
            {
                await _dynamicAliasStore.EnsureStandardAliasesAsync(suggestion.InterfaceName, roleName);
            }
        }

        await _loggingManager.LogSystemAsync(
            "Network",
            "info",
            "InitialCapture",
            $"Applied {assignments.Count(a => a.Role != InterfaceRole.Unknown)} interface assignments");
    }

    // ========================================================================
    // Heuristics for Suggesting Interface Roles
    // ========================================================================

    private async Task<List<SuggestedInterfaceAssignment>> SuggestInterfaceAssignmentsAsync(
        List<InterfaceInfo> interfaces,
        List<AddressInfo> addresses,
        List<RouteInfo> defaultGateways)
    {
        var suggestions = new List<SuggestedInterfaceAssignment>();

        foreach (var iface in interfaces.Where(i => !ShouldSkipInterface(i.Name)))
        {
            var ifaceAddresses = addresses.Where(a => a.Interface == iface.Name).ToList();
            var ipv4Addr = ifaceAddresses.FirstOrDefault(a => a.Family == "inet");
            var hasGateway = defaultGateways.Any(g => g.Interface == iface.Name);

            var suggestion = new SuggestedInterfaceAssignment
            {
                InterfaceName = iface.Name,
                MacAddress = iface.MacAddress,
                LinkUp = iface.IsUp,
                CurrentIpAddress = ipv4Addr?.Address,
                CurrentPrefix = ipv4Addr?.PrefixLength
            };

            // Heuristics for role detection
            if (hasGateway && !string.IsNullOrEmpty(ipv4Addr?.Address))
            {
                // Has default gateway - likely WAN
                var gateway = defaultGateways.First(g => g.Interface == iface.Name);
                suggestion.Role = InterfaceRole.Wan;
                suggestion.SuggestedName = "WAN";
                suggestion.SuggestedIpMode = InterfaceIpMode.Dhcp;
                suggestion.CurrentGateway = gateway.Gateway;
                suggestion.Confidence = "high";
                suggestion.Reason = "Interface has default gateway route";
            }
            else if (!string.IsNullOrEmpty(ipv4Addr?.Address) && IsPrivateIp(ipv4Addr.Address))
            {
                // Has private IP without gateway - likely LAN
                suggestion.Role = InterfaceRole.Lan;
                suggestion.SuggestedName = "LAN";
                suggestion.SuggestedIpMode = InterfaceIpMode.Static;
                suggestion.Confidence = "medium";
                suggestion.Reason = "Interface has private IP without gateway";
            }
            else if (iface.IsUp && string.IsNullOrEmpty(ipv4Addr?.Address))
            {
                // Link up but no IP - could be anything, suggest unconfigured
                suggestion.Role = InterfaceRole.Unknown;
                suggestion.SuggestedName = iface.Name.ToUpperInvariant();
                suggestion.SuggestedIpMode = InterfaceIpMode.None;
                suggestion.Confidence = "low";
                suggestion.Reason = "Interface is up but has no IP address";
            }
            else if (!iface.IsUp)
            {
                // Link down
                suggestion.Role = InterfaceRole.Unknown;
                suggestion.SuggestedName = iface.Name.ToUpperInvariant();
                suggestion.SuggestedIpMode = InterfaceIpMode.None;
                suggestion.Confidence = "none";
                suggestion.Reason = "Interface link is down";
            }
            else
            {
                suggestion.Role = InterfaceRole.Opt;
                suggestion.SuggestedName = $"OPT ({iface.Name})";
                suggestion.SuggestedIpMode = InterfaceIpMode.None;
                suggestion.Confidence = "low";
                suggestion.Reason = "Could not determine role";
            }

            suggestions.Add(suggestion);
        }

        // If we have exactly one WAN suggestion and one or more others,
        // promote the first non-WAN with private IP to LAN
        var wanCount = suggestions.Count(s => s.Role == InterfaceRole.Wan);
        var lanCount = suggestions.Count(s => s.Role == InterfaceRole.Lan);

        if (wanCount == 1 && lanCount == 0)
        {
            var candidate = suggestions
                .Where(s => s.Role != InterfaceRole.Wan && s.LinkUp && IsPrivateIp(s.CurrentIpAddress))
                .FirstOrDefault();

            if (candidate != null)
            {
                candidate.Role = InterfaceRole.Lan;
                candidate.SuggestedName = "LAN";
                candidate.SuggestedIpMode = InterfaceIpMode.Static;
                candidate.Confidence = "medium";
                candidate.Reason = "Selected as LAN (other interface is WAN)";
            }
        }

        return suggestions;
    }

    // ========================================================================
    // Platform Data Gathering
    // ========================================================================

    private async Task<List<InterfaceInfo>> EnumerateInterfacesAsync(CancellationToken cancellationToken)
    {
        var interfaces = new List<InterfaceInfo>();
        var netPath = "/sys/class/net";

        if (!Directory.Exists(netPath))
        {
            return interfaces;
        }

        await Task.Yield();

        foreach (var dir in Directory.GetDirectories(netPath))
        {
            var name = Path.GetFileName(dir);
            if (string.IsNullOrWhiteSpace(name))
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
                Name = name,
                MacAddress = mac,
                Mtu = mtu,
                OperState = operState,
                IsUp = isUp
            });
        }

        return interfaces;
    }

    private async Task<List<AddressInfo>> GetAllAddressesAsync(CancellationToken cancellationToken)
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
                if (!ifaceJson.TryGetProperty("addr_info", out var addrInfo) ||
                    addrInfo.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var addr in addrInfo.EnumerateArray())
                {
                    var local = addr.GetProperty("local").GetString() ?? string.Empty;
                    var family = addr.GetProperty("family").GetString() ?? string.Empty;
                    var prefix = addr.TryGetProperty("prefixlen", out var prefixEl)
                        ? prefixEl.GetInt32()
                        : 0;

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

        // Get IPv4 routes
        var ipv4Command = new PlatformCommand
        {
            FileName = "ip",
            Arguments = "-j route show",
            UseSudo = false,
            TimeoutMs = 5000
        };

        var ipv4Result = await _commandRunner.RunAsync(ipv4Command, cancellationToken);
        if (ipv4Result.ExitCode == 0 && !string.IsNullOrWhiteSpace(ipv4Result.StdOut))
        {
            routes.AddRange(ParseRoutesJson(ipv4Result.StdOut));
        }

        // Get IPv6 routes
        var ipv6Command = new PlatformCommand
        {
            FileName = "ip",
            Arguments = "-6 -j route show",
            UseSudo = false,
            TimeoutMs = 5000
        };

        var ipv6Result = await _commandRunner.RunAsync(ipv6Command, cancellationToken);
        if (ipv6Result.ExitCode == 0 && !string.IsNullOrWhiteSpace(ipv6Result.StdOut))
        {
            routes.AddRange(ParseRoutesJson(ipv6Result.StdOut));
        }

        return routes;
    }

    private List<RouteInfo> ParseRoutesJson(string json)
    {
        var routes = new List<RouteInfo>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var routeJson in doc.RootElement.EnumerateArray())
            {
                var dst = routeJson.TryGetProperty("dst", out var dstEl)
                    ? dstEl.GetString()
                    : "default";
                var gateway = routeJson.TryGetProperty("gateway", out var gwEl)
                    ? gwEl.GetString()
                    : null;
                var dev = routeJson.TryGetProperty("dev", out var devEl)
                    ? devEl.GetString()
                    : null;
                var protocol = routeJson.TryGetProperty("protocol", out var protoEl)
                    ? protoEl.GetString()
                    : null;

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

    private async Task<List<string>> GetDnsServersAsync(CancellationToken cancellationToken)
    {
        var servers = new List<string>();

        // Check /etc/resolv.conf
        var resolvConf = "/etc/resolv.conf";
        if (File.Exists(resolvConf))
        {
            try
            {
                var content = await File.ReadAllTextAsync(resolvConf, cancellationToken);
                foreach (var line in content.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("nameserver ", StringComparison.OrdinalIgnoreCase))
                    {
                        var server = trimmed.Substring(11).Trim();
                        if (!string.IsNullOrWhiteSpace(server) && !servers.Contains(server))
                        {
                            servers.Add(server);
                        }
                    }
                }
            }
            catch
            {
                // File read error
            }
        }

        return servers;
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
                if (speedStr.EndsWith("Mb/s") &&
                    int.TryParse(speedStr.Replace("Mb/s", ""), out var speed))
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
    // Helpers
    // ========================================================================

    private static bool ShouldSkipInterface(string name)
    {
        // Skip loopback, virtual, and container interfaces
        return name == "lo" ||
               name.StartsWith("veth") ||
               name.StartsWith("docker") ||
               name.StartsWith("br-") ||
               name.StartsWith("virbr");
    }

    private static bool IsPrivateIp(string? address)
    {
        if (string.IsNullOrEmpty(address))
        {
            return false;
        }

        // Check RFC1918 private ranges
        return address.StartsWith("10.") ||
               address.StartsWith("172.16.") ||
               address.StartsWith("172.17.") ||
               address.StartsWith("172.18.") ||
               address.StartsWith("172.19.") ||
               address.StartsWith("172.20.") ||
               address.StartsWith("172.21.") ||
               address.StartsWith("172.22.") ||
               address.StartsWith("172.23.") ||
               address.StartsWith("172.24.") ||
               address.StartsWith("172.25.") ||
               address.StartsWith("172.26.") ||
               address.StartsWith("172.27.") ||
               address.StartsWith("172.28.") ||
               address.StartsWith("172.29.") ||
               address.StartsWith("172.30.") ||
               address.StartsWith("172.31.") ||
               address.StartsWith("192.168.");
    }

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

    // ========================================================================
    // Internal Types
    // ========================================================================

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
}

/// <summary>
/// Result of the network baseline capture operation.
/// </summary>
public sealed class NetworkCaptureResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int InterfacesDiscovered { get; set; }
    public int GatewaysDiscovered { get; set; }
    public int DnsServersDiscovered { get; set; }
    public List<SuggestedInterfaceAssignment> SuggestedAssignments { get; set; } = new();
}

/// <summary>
/// A suggested interface role assignment based on heuristics.
/// </summary>
public sealed class SuggestedInterfaceAssignment
{
    public string InterfaceName { get; set; } = string.Empty;
    public string? MacAddress { get; set; }
    public bool LinkUp { get; set; }
    public string? CurrentIpAddress { get; set; }
    public int? CurrentPrefix { get; set; }
    public string? CurrentGateway { get; set; }
    public InterfaceRole Role { get; set; }
    public string SuggestedName { get; set; } = string.Empty;
    public InterfaceIpMode SuggestedIpMode { get; set; }
    public string Confidence { get; set; } = "low";
    public string Reason { get; set; } = string.Empty;
}
