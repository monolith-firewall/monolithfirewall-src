using System.Text.Json.Serialization;

namespace Monolith.FireWall.Core.Models;

/// <summary>
/// Backup metadata stored alongside backup file
/// </summary>
public class BackupMetadata
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("databaseVersion")]
    public string? DatabaseVersion { get; set; }

    [JsonPropertyName("fileSize")]
    public long FileSize { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "local"; // "local" or "cloud"
}

/// <summary>
/// Backup information view model
/// </summary>
public class BackupInfo
{
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = "";

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "local";
}

/// <summary>
/// Request to create a backup
/// </summary>
public class BackupCreateRequest
{
    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

/// <summary>
/// Request to restore a backup
/// </summary>
public class BackupRestoreRequest
{
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = "";
}

/// <summary>
/// Request to delete a backup
/// </summary>
public class BackupDeleteRequest
{
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = "";
}

/// <summary>
/// Result of backup operation
/// </summary>
public class BackupResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("backup")]
    public BackupInfo? Backup { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

/// <summary>
/// Backup settings configuration
/// </summary>
public class BackupSettings
{
    [JsonPropertyName("namingPattern")]
    public string NamingPattern { get; set; } = "monolith-backup-{timestamp}";

    [JsonPropertyName("includeDatabase")]
    public bool IncludeDatabase { get; set; } = true;

    [JsonPropertyName("includeConfigFiles")]
    public bool IncludeConfigFiles { get; set; } = false;

    [JsonPropertyName("includeLogs")]
    public bool IncludeLogs { get; set; } = false;

    [JsonPropertyName("additionalLocations")]
    public List<BackupLocation> AdditionalLocations { get; set; } = new();

    [JsonPropertyName("autoBackupEnabled")]
    public bool AutoBackupEnabled { get; set; } = false;

    [JsonPropertyName("autoBackupInterval")]
    public int AutoBackupInterval { get; set; } = 24; // hours

    [JsonPropertyName("maxBackups")]
    public int MaxBackups { get; set; } = 10;
}

/// <summary>
/// Additional backup location
/// </summary>
public class BackupLocation
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

/// <summary>
/// Request to update backup settings
/// </summary>
public class BackupSettingsUpdateRequest
{
    [JsonPropertyName("settings")]
    public BackupSettings? Settings { get; set; }
}
