namespace Monolith.FireWall.Platform.Models;

public sealed class InterfaceInfo
{
    public string Name { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public int Mtu { get; set; }
    public string OperState { get; set; } = string.Empty;
    public bool IsUp { get; set; }
}

public sealed class AddressInfo
{
    public string Interface { get; set; } = string.Empty;
    public string Family { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int PrefixLength { get; set; }
}

public sealed class RouteInfo
{
    public string Destination { get; set; } = string.Empty;
    public string? Gateway { get; set; }
    public string? Interface { get; set; }
    public string? Protocol { get; set; }
    public string? Scope { get; set; }
}

public sealed class ResolverInfo
{
    public string Source { get; set; } = string.Empty;
    public string[] Servers { get; set; } = Array.Empty<string>();
}

public sealed class InterfaceRequest
{
    public string? Interface { get; set; }
}

public sealed class SetInterfaceStateRequest
{
    public string Interface { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
}

public sealed class AddressRequest
{
    public string Interface { get; set; } = string.Empty;
    public string AddressCidr { get; set; } = string.Empty;
}

public sealed class RouteRequest
{
    public string Destination { get; set; } = string.Empty;
    public string? Gateway { get; set; }
    public string? Interface { get; set; }
}

public sealed class DnsResolversRequest
{
    public string? Interface { get; set; }
    public string[] Servers { get; set; } = Array.Empty<string>();
}
