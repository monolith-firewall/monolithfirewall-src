namespace Monolith.FireWall.Core.Models;

public sealed class PackageInstallRequest
{
    public string PackageId { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public bool Overwrite { get; set; } = true;
    public bool RestartServices { get; set; } = true;
}

public sealed class PackageUninstallRequest
{
    public string PackageId { get; set; } = string.Empty;
}

public sealed class ModuleStateRequest
{
    public string PackageId { get; set; } = string.Empty;
    public string ModuleId { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}
