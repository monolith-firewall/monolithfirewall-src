namespace Monolith.FireWall.WebUI.Features.Firewall.VirtualIps;

public class VirtualIp
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "ipalias"; // ipalias, carp, proxyarp, other
    public string Interface { get; set; } = "";
    public string Address { get; set; } = "";
    public int SubnetBits { get; set; } = 24;
    public string Description { get; set; } = "";
    public bool Enabled { get; set; } = true;
    // CARP-specific fields
    public int? Vhid { get; set; }
    public string? CarpPassword { get; set; }
    public string? Advskew { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class VirtualIpEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "ipalias";
    public string Interface { get; set; } = "";
    public string Address { get; set; } = "";
    public int SubnetBits { get; set; } = 24;
    public string Description { get; set; } = "";
    public int Enabled { get; set; } = 1;
    public int? Vhid { get; set; }
    public string? CarpPassword { get; set; }
    public string? Advskew { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
