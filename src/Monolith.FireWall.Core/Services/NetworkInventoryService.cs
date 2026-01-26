using System.Text.Json;
using Monolith.FireWall.Core.Services.Platform;
using Monolith.FireWall.Platform.Models;
using Monolith.FireWall.Platform.Validation;

namespace Monolith.FireWall.Core.Services;

public sealed class NetworkInventoryService
{
    private readonly PlatformCommandRunner _commandRunner;

    public NetworkInventoryService(PlatformCommandRunner commandRunner)
    {
        _commandRunner = commandRunner;
    }

    public Task<List<InterfaceInfo>> ListInterfacesAsync()
    {
        var interfaces = new List<InterfaceInfo>();
        var netPath = "/sys/class/net";
        if (!Directory.Exists(netPath))
        {
            return Task.FromResult(interfaces);
        }

        foreach (var dir in Directory.GetDirectories(netPath))
        {
            var iface = Path.GetFileName(dir);
            if (string.IsNullOrWhiteSpace(iface))
            {
                continue;
            }

            var operState = ReadFileTrim(Path.Combine(dir, "operstate"));
            var mac = ReadFileTrim(Path.Combine(dir, "address"));
            var mtuValue = ReadFileTrim(Path.Combine(dir, "mtu"));
            var speedValue = ReadFileTrim(Path.Combine(dir, "speed"));
            var duplex = ReadFileTrim(Path.Combine(dir, "duplex"));

            var mtu = int.TryParse(mtuValue, out var mtuParsed) ? mtuParsed : 0;
            var speed = int.TryParse(speedValue, out var speedParsed) ? speedParsed : (int?)null;
            var isUp = string.Equals(operState, "up", StringComparison.OrdinalIgnoreCase);

            interfaces.Add(new InterfaceInfo
            {
                Name = iface,
                MacAddress = mac,
                Mtu = mtu,
                OperState = operState,
                IsUp = isUp,
                SpeedMbps = speed,
                Duplex = string.IsNullOrWhiteSpace(duplex) ? null : duplex
            });
        }

        return Task.FromResult(interfaces);
    }

    public async Task<List<AddressInfo>> ListAddressesAsync(string? iface = null, CancellationToken cancellationToken = default)
    {
        var addresses = new List<AddressInfo>();
        if (!_commandRunner.CommandExists("ip"))
        {
            return addresses;
        }

        string args = "-j addr show";
        if (!string.IsNullOrWhiteSpace(iface))
        {
            if (!PlatformValidators.IsValidInterfaceName(iface))
            {
                return addresses;
            }

            args = $"-j addr show dev {iface}";
        }

        var command = new PlatformCommand
        {
            FileName = "ip",
            Arguments = args,
            UseSudo = false,
            TimeoutMs = 5000
        };

        var result = await _commandRunner.RunAsync(command, cancellationToken);
        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StdOut))
        {
            return addresses;
        }

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

        return addresses;
    }

    /// <summary>
    /// Gets DHCP lease information for an interface.
    /// Checks both dhclient and systemd-networkd lease files.
    /// </summary>
    public Task<DhcpLeaseInfo?> GetDhcpLeaseInfoAsync(string interfaceName, CancellationToken cancellationToken = default)
    {
        // Try systemd-networkd lease first (more common on modern systems)
        var networkdLeasePath = $"/run/systemd/netif/leases";
        if (Directory.Exists(networkdLeasePath))
        {
            // Find the interface index
            var indexPath = $"/sys/class/net/{interfaceName}/ifindex";
            var ifindex = ReadFileTrim(indexPath);
            if (!string.IsNullOrWhiteSpace(ifindex))
            {
                var leasePath = Path.Combine(networkdLeasePath, ifindex);
                if (File.Exists(leasePath))
                {
                    var lease = ParseNetworkdLease(leasePath, interfaceName);
                    if (lease != null)
                    {
                        return Task.FromResult<DhcpLeaseInfo?>(lease);
                    }
                }
            }
        }

        // Try dhclient lease file
        var dhclientLeasePaths = new[]
        {
            $"/var/lib/dhcp/dhclient.{interfaceName}.leases",
            $"/var/lib/dhclient/dhclient.{interfaceName}.leases",
            "/var/lib/dhcp/dhclient.leases",
            "/var/lib/dhclient/dhclient.leases"
        };

        foreach (var path in dhclientLeasePaths)
        {
            if (File.Exists(path))
            {
                var lease = ParseDhclientLease(path, interfaceName);
                if (lease != null)
                {
                    return Task.FromResult<DhcpLeaseInfo?>(lease);
                }
            }
        }

        return Task.FromResult<DhcpLeaseInfo?>(null);
    }

    private DhcpLeaseInfo? ParseNetworkdLease(string path, string interfaceName)
    {
        try
        {
            var lines = File.ReadAllLines(path);
            var lease = new DhcpLeaseInfo { InterfaceName = interfaceName };

            foreach (var line in lines)
            {
                var parts = line.Split('=', 2);
                if (parts.Length != 2) continue;

                var key = parts[0].Trim();
                var value = parts[1].Trim();

                switch (key)
                {
                    case "ADDRESS":
                        lease.IpAddress = value;
                        break;
                    case "NETMASK":
                        lease.SubnetMask = value;
                        break;
                    case "ROUTER":
                        lease.Gateway = value.Split(' ').FirstOrDefault();
                        break;
                    case "DNS":
                        lease.DnsServers = value.Split(' ').Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                        break;
                    case "SERVER_ADDRESS":
                        lease.ServerAddress = value;
                        break;
                    case "T1":
                        if (long.TryParse(value, out var t1))
                            lease.RenewTime = DateTimeOffset.FromUnixTimeSeconds(t1).UtcDateTime;
                        break;
                    case "T2":
                        if (long.TryParse(value, out var t2))
                            lease.RebindTime = DateTimeOffset.FromUnixTimeSeconds(t2).UtcDateTime;
                        break;
                    case "LIFETIME":
                        if (int.TryParse(value, out var lifetime))
                            lease.LeaseTime = lifetime;
                        break;
                }
            }

            return string.IsNullOrWhiteSpace(lease.IpAddress) ? null : lease;
        }
        catch
        {
            return null;
        }
    }

    private DhcpLeaseInfo? ParseDhclientLease(string path, string interfaceName)
    {
        try
        {
            var content = File.ReadAllText(path);
            // Find the last lease block for this interface
            var leaseBlocks = content.Split(new[] { "lease {" }, StringSplitOptions.RemoveEmptyEntries);

            DhcpLeaseInfo? lastLease = null;

            foreach (var block in leaseBlocks)
            {
                if (!block.Contains($"interface \"{interfaceName}\"")) continue;

                var lease = new DhcpLeaseInfo { InterfaceName = interfaceName };
                var lines = block.Split('\n');

                foreach (var line in lines)
                {
                    var trimmed = line.Trim().TrimEnd(';');

                    if (trimmed.StartsWith("fixed-address"))
                        lease.IpAddress = trimmed.Replace("fixed-address", "").Trim();
                    else if (trimmed.StartsWith("option subnet-mask"))
                        lease.SubnetMask = trimmed.Replace("option subnet-mask", "").Trim();
                    else if (trimmed.StartsWith("option routers"))
                        lease.Gateway = trimmed.Replace("option routers", "").Trim().Split(',').FirstOrDefault()?.Trim();
                    else if (trimmed.StartsWith("option domain-name-servers"))
                        lease.DnsServers = trimmed.Replace("option domain-name-servers", "").Trim()
                            .Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                    else if (trimmed.StartsWith("option dhcp-server-identifier"))
                        lease.ServerAddress = trimmed.Replace("option dhcp-server-identifier", "").Trim();
                    else if (trimmed.StartsWith("option dhcp-lease-time"))
                    {
                        if (int.TryParse(trimmed.Replace("option dhcp-lease-time", "").Trim(), out var leaseTime))
                            lease.LeaseTime = leaseTime;
                    }
                    else if (trimmed.StartsWith("renew"))
                    {
                        if (TryParseDhclientDate(trimmed.Replace("renew", "").Trim(), out var renewTime))
                            lease.RenewTime = renewTime;
                    }
                    else if (trimmed.StartsWith("rebind"))
                    {
                        if (TryParseDhclientDate(trimmed.Replace("rebind", "").Trim(), out var rebindTime))
                            lease.RebindTime = rebindTime;
                    }
                    else if (trimmed.StartsWith("expire"))
                    {
                        if (TryParseDhclientDate(trimmed.Replace("expire", "").Trim(), out var expireTime))
                            lease.ExpireTime = expireTime;
                    }
                }

                if (!string.IsNullOrWhiteSpace(lease.IpAddress))
                    lastLease = lease;
            }

            return lastLease;
        }
        catch
        {
            return null;
        }
    }

    private static bool TryParseDhclientDate(string dateStr, out DateTime result)
    {
        result = DateTime.MinValue;
        try
        {
            // Format: "1 2024/01/15 10:30:45" (day-of-week year/month/day time)
            var parts = dateStr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3)
            {
                var datePart = parts[1];
                var timePart = parts[2];
                if (DateTime.TryParse($"{datePart} {timePart}", out result))
                {
                    result = DateTime.SpecifyKind(result, DateTimeKind.Utc);
                    return true;
                }
            }
        }
        catch { }
        return false;
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
}

/// <summary>
/// DHCP lease information for an interface.
/// </summary>
public sealed class DhcpLeaseInfo
{
    public string InterfaceName { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? SubnetMask { get; set; }
    public string? Gateway { get; set; }
    public List<string> DnsServers { get; set; } = new();
    public string? ServerAddress { get; set; }
    public int? LeaseTime { get; set; }
    public DateTime? RenewTime { get; set; }
    public DateTime? RebindTime { get; set; }
    public DateTime? ExpireTime { get; set; }
}
