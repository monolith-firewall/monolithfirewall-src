using CL.SQLite.Models;

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

[SQLiteTable("firewall_aliases")]
[SQLiteIndex(new[] { "name" }, IsUnique = true, Name = "idx_firewall_aliases_name")]
public class FirewallAliasEntity
{
    [SQLiteColumn(IsPrimaryKey = true, IsAutoIncrement = true)]
    public int Id { get; set; }

    [SQLiteColumn(IsNotNull = true, IsUnique = true, Size = 100)]
    public string Name { get; set; } = "";

    [SQLiteColumn(IsNotNull = true, Size = 50)]
    public string Type { get; set; } = "host";

    [SQLiteColumn(Size = 500)]
    public string Description { get; set; } = "";

    [SQLiteColumn(IsNotNull = true)]
    public string Content { get; set; } = "[]"; // JSON array

    [SQLiteColumn(IsNotNull = true)]
    public int Enabled { get; set; } = 1;

    [SQLiteColumn(IsNotNull = true)]
    public DateTime CreatedAt { get; set; }

    [SQLiteColumn(IsNotNull = true)]
    public DateTime UpdatedAt { get; set; }
}
