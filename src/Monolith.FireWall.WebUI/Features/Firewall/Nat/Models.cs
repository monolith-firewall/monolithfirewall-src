namespace Monolith.FireWall.WebUI.Features.Firewall.Nat;

public class NatRule
{
    public int Id { get; set; }
    public int RuleNumber { get; set; }
    public string Type { get; set; } = "port_forward"; // port_forward, one_to_one, outbound
    public string Interface { get; set; } = "";
    public string AddressFamily { get; set; } = "ipv4"; // ipv4, ipv6, dual
    public string Protocol { get; set; } = "tcp"; // tcp, udp, tcp/udp, icmp, any
    public string SourceType { get; set; } = "any"; // any, single, network, alias
    public string? SourceValue { get; set; }
    public string? SourcePort { get; set; }
    public string DestinationType { get; set; } = "any"; // any, single, network, alias
    public string? DestinationValue { get; set; }
    public string? DestinationPort { get; set; }
    public string? RedirectTargetIp { get; set; }
    public string? RedirectTargetPort { get; set; }
    public string ReflectionMode { get; set; } = "default"; // default, proxy, nat, disabled
    public string Description { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class NatRuleEntity
{
    public int Id { get; set; }
    public int RuleNumber { get; set; }
    public string Type { get; set; } = "port_forward";
    public string Interface { get; set; } = "";
    public string AddressFamily { get; set; } = "ipv4";
    public string Protocol { get; set; } = "tcp";
    public string SourceType { get; set; } = "any";
    public string? SourceValue { get; set; }
    public string? SourcePort { get; set; }
    public string DestinationType { get; set; } = "any";
    public string? DestinationValue { get; set; }
    public string? DestinationPort { get; set; }
    public string? RedirectTargetIp { get; set; }
    public string? RedirectTargetPort { get; set; }
    public string ReflectionMode { get; set; } = "default";
    public string Description { get; set; } = "";
    public int Enabled { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
