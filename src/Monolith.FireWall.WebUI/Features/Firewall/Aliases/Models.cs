namespace Monolith.FireWall.WebUI.Features.Firewall.Aliases;

public class FirewallAlias
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "host"; // host, network, port, url
    public string Description { get; set; } = "";
    public string[] Content { get; set; } = Array.Empty<string>();
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class FirewallAliasEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "host";
    public string Description { get; set; } = "";
    public string Content { get; set; } = "[]"; // JSON array
    public int Enabled { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
