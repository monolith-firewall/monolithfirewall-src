using CL.SQLite.Models;
using System.Text.Json;

namespace Monolith.FireWall.Core.Models;

/// <summary>
/// Database entity for WebUI binding settings.
/// </summary>
[SQLiteTable("webui_settings")]
public sealed class WebUiSettingsEntity
{
    [SQLiteColumn(IsPrimaryKey = true, IsAutoIncrement = true)]
    public int Id { get; set; }

    [SQLiteColumn(ColumnName = "http_port", IsNotNull = true)]
    public int HttpPort { get; set; } = 80;

    [SQLiteColumn(ColumnName = "https_port", IsNotNull = true)]
    public int HttpsPort { get; set; } = 443;

    [SQLiteColumn(ColumnName = "binding_addresses", DataType = SQLiteDataType.TEXT)] // JSON array
    public string? BindingAddresses { get; set; } // JSON: ["192.168.1.1", "10.0.0.1"] or null/empty for all interfaces

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME, ColumnName = "updated_at", IsNotNull = true)]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// View model for WebUI settings.
/// </summary>
public sealed class WebUiSettingsView
{
    public int HttpPort { get; set; } = 80;
    public int HttpsPort { get; set; } = 443;
    public List<string> BindingAddresses { get; set; } = new(); // Empty = bind to all interfaces
    public bool BindToAllInterfaces { get; set; } = true; // If true, BindingAddresses is ignored
}

/// <summary>
/// Update request for WebUI settings.
/// </summary>
public sealed class WebUiSettingsUpdateRequest
{
    public int? HttpPort { get; set; }
    public int? HttpsPort { get; set; }
    public List<string>? BindingAddresses { get; set; }
    public bool? BindToAllInterfaces { get; set; }
}

/// <summary>
/// Result of WebUI settings update.
/// </summary>
public sealed class WebUiSettingsUpdateResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public bool RequiresRestart { get; set; }
    public WebUiSettingsView? Settings { get; set; }
}

/// <summary>
/// Result of WebUI service restart.
/// </summary>
public sealed class WebUiServiceRestartResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? ServiceStatus { get; set; }
}
