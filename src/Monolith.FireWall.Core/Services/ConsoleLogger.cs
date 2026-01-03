using Monolith.FireWall.Common.Interfaces;

namespace Monolith.FireWall.Core.Services;

public class ConsoleLogger : ILogger
{
    private readonly string _prefix;

    public ConsoleLogger(string prefix)
    {
        _prefix = prefix;
    }

    public void LogDebug(string message)
    {
        Console.WriteLine($"[DEBUG] [{_prefix}] {message}");
    }

    public void LogInformation(string message)
    {
        Console.WriteLine($"[INFO] [{_prefix}] {message}");
    }

    public void LogWarning(string message)
    {
        Console.WriteLine($"[WARN] [{_prefix}] {message}");
    }

    public void LogError(string message)
    {
        Console.WriteLine($"[ERROR] [{_prefix}] {message}");
    }

    public void LogError(Exception ex, string message)
    {
        Console.WriteLine($"[ERROR] [{_prefix}] {message}: {ex.Message}");
        Console.WriteLine(ex.StackTrace);
    }
}
