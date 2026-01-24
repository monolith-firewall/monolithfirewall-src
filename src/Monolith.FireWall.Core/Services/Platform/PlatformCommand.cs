namespace Monolith.FireWall.Core.Services.Platform;

public sealed class PlatformCommand
{
    public string FileName { get; init; } = string.Empty;
    public string Arguments { get; init; } = string.Empty;
    public bool UseSudo { get; init; }
    public int TimeoutMs { get; init; } = 5000;
    public Dictionary<string, string>? EnvironmentVariables { get; init; }
}
