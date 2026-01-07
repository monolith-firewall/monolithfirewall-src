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

    [SQLiteColumn(DataType = SQLiteDataType.TEXT)]
    public string? NtpServers { get; set; } // Comma-separated NTP server list

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime UpdatedAt { get; set; }
}

public sealed class SystemSettingsView
{
    public string? Hostname { get; set; }
    public string? Domain { get; set; }
    public string? Timezone { get; set; }
    public List<string> DnsServers { get; set; } = new();
    public List<string> NtpServers { get; set; } = new();
    public DateTime? CurrentDateTime { get; set; } // Current system date/time
}

public sealed class SystemSettingsUpdateRequest
{
    public string? Hostname { get; set; }
    public string? Domain { get; set; }
    public string? Timezone { get; set; }
    public List<string>? DnsServers { get; set; }
    public List<string>? NtpServers { get; set; }
    public DateTime? DateTime { get; set; } // Optional: set system date/time
}

/// <summary>
/// Result of system settings application during startup.
/// </summary>
public sealed class SystemSettingsResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public bool HostnameApplied { get; set; }
    public bool TimezoneApplied { get; set; }
    public bool DnsApplied { get; set; }
    public bool NtpApplied { get; set; }
}

/// <summary>
/// Result of interface configuration generation during startup.
/// </summary>
public sealed class InterfaceConfigResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int GeneratedCount { get; set; }
    public bool Applied { get; set; }
}

/// <summary>
/// Result of firewall rule application during startup.
/// </summary>
public sealed class FirewallStartupResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int RulesApplied { get; set; }
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Result of system tuneables application during startup.
/// </summary>
public sealed class TuneablesStartupResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int AppliedCount { get; set; }
    public int TotalCount { get; set; }
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Result of system initialization during startup.
/// </summary>
public sealed class StartupResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? Duration { get; set; }
    public SystemSettingsResult SystemSettings { get; set; } = new();
    public TuneablesStartupResult Tuneables { get; set; } = new();
    public InterfaceConfigResult Interfaces { get; set; } = new();
    public Services.ModuleConfigGenerationSummary Modules { get; set; } = new();
    public Services.ServiceManagementResult Services { get; set; } = new();
    public FirewallStartupResult Firewall { get; set; } = new();
}
