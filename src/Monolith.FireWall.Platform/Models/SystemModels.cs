namespace Monolith.FireWall.Platform.Models;

public sealed class HostnameInfo
{
    public string Hostname { get; set; } = string.Empty;
}

public sealed class SetHostnameRequest
{
    public string Hostname { get; set; } = string.Empty;
}

public sealed class FileReadRequest
{
    public string Path { get; set; } = string.Empty;
    public int? MaxBytes { get; set; }
}

public sealed class FileReadResponse
{
    public string Path { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public sealed class FileWriteRequest
{
    public string Path { get; set; } = string.Empty;
    public string? Content { get; set; }
    public bool CreateDirectories { get; set; }
}

public sealed class FileWriteResponse
{
    public string Path { get; set; } = string.Empty;
    public int BytesWritten { get; set; }
}
