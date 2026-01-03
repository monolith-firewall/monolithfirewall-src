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
