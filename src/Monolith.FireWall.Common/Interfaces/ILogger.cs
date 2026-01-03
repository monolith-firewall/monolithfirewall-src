namespace Monolith.FireWall.Common.Interfaces;

/// <summary>
/// Simple logger interface.
/// </summary>
public interface ILogger
{
    void LogDebug(string message);
    void LogInformation(string message);
    void LogWarning(string message);
    void LogError(string message);
    void LogError(Exception ex, string message);
}
