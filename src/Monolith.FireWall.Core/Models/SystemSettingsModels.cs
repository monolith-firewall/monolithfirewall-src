using CL.SQLite.Models;

namespace Monolith.FireWall.Core.Models;

[SQLiteTable("system_settings")]
public sealed class SystemSettingsEntity
{
    [SQLiteColumn(IsPrimaryKey = true, IsAutoIncrement = true)]
    public int Id { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 128)]
    public string? Hostname { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 128)]
    public string? Domain { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 64)]
    public string? Timezone { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.TEXT)]
    public string? DnsServers { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime UpdatedAt { get; set; }
}

public sealed class SystemSettingsView
{
    public string? Hostname { get; set; }
    public string? Domain { get; set; }
    public string? Timezone { get; set; }
    public List<string> DnsServers { get; set; } = new();
}

public sealed class SystemSettingsUpdateRequest
{
    public string? Hostname { get; set; }
    public string? Domain { get; set; }
    public string? Timezone { get; set; }
    public List<string>? DnsServers { get; set; }
}
