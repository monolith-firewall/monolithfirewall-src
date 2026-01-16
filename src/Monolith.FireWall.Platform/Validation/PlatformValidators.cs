using System.Net;
using System.Text.RegularExpressions;

namespace Monolith.FireWall.Platform.Validation;

public static class PlatformValidators
{
    private static readonly Regex InterfaceRegex = new("^[a-zA-Z0-9._-]+$", RegexOptions.Compiled);
    private static readonly Regex HostnameRegex = new("^[a-zA-Z0-9][a-zA-Z0-9.-]{0,62}$", RegexOptions.Compiled);

    public static bool IsValidInterfaceName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return InterfaceRegex.IsMatch(name);
    }

    public static bool IsValidHostname(string hostname)
    {
        if (string.IsNullOrWhiteSpace(hostname))
        {
            return false;
        }

        return HostnameRegex.IsMatch(hostname);
    }

    public static bool IsValidIpv4(string value)
    {
        if (!IPAddress.TryParse(value, out var ip))
        {
            return false;
        }

        return ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
    }

    public static bool IsValidIpv6(string value)
    {
        if (!IPAddress.TryParse(value, out var ip))
        {
            return false;
        }

        return ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6;
    }

    public static string? GetAddressFamily(string value)
    {
        if (!IPAddress.TryParse(value, out var ip))
        {
            return null;
        }

        return ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
            ? "ipv4"
            : ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
                ? "ipv6"
                : null;
    }

    public static bool TryParseCidr(string cidr, out IPAddress address, out int prefixLength)
    {
        address = IPAddress.None;
        prefixLength = -1;

        if (string.IsNullOrWhiteSpace(cidr))
        {
            return false;
        }

        var parts = cidr.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        if (!IPAddress.TryParse(parts[0], out address))
        {
            return false;
        }

        if (!int.TryParse(parts[1], out prefixLength))
        {
            return false;
        }

        var maxPrefix = address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
        if (prefixLength < 0 || prefixLength > maxPrefix)
        {
            return false;
        }

        return true;
    }

    public static bool TryParseCidrV4(string cidr, out IPAddress address, out int prefixLength)
    {
        if (!TryParseCidr(cidr, out address, out prefixLength))
        {
            return false;
        }

        if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return false;
        }

        return prefixLength >= 0 && prefixLength <= 32;
    }

    public static bool TryParseCidrV6(string cidr, out IPAddress address, out int prefixLength)
    {
        if (!TryParseCidr(cidr, out address, out prefixLength))
        {
            return false;
        }

        if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            return false;
        }

        return prefixLength >= 0 && prefixLength <= 128;
    }

    public static bool IsValidIp(string value)
    {
        return IPAddress.TryParse(value, out _);
    }

    public static bool AreValidDnsServers(IEnumerable<string> servers)
    {
        foreach (var server in servers)
        {
            if (!IsValidIp(server))
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsValidAbsolutePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (path.IndexOf('\0') >= 0)
        {
            return false;
        }

        if (!Path.IsPathRooted(path))
        {
            return false;
        }

        var normalized = path.Replace('\\', '/');
        if (normalized.Contains("/../", StringComparison.Ordinal) || normalized.EndsWith("/..", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }
}
