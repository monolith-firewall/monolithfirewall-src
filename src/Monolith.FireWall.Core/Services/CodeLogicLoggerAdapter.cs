using Monolith.FireWall.Common.Interfaces;

namespace Monolith.FireWall.Core.Services;

/// <summary>
/// Adapter to convert CodeLogic logger to MonolithFireWall logger
/// </summary>
public class CodeLogicLoggerAdapter : ILogger
{
    private readonly CodeLogic.Core.Logging.ILogger _logger;

    public CodeLogicLoggerAdapter(CodeLogic.Core.Logging.ILogger logger)
    {
        _logger = logger;
    }

    public void LogInformation(string message) => _logger.Info(message);
    public void LogWarning(string message) => _logger.Warning(message);
    public void LogError(Exception ex, string message) => _logger.Error(message, ex);
    public void LogError(string message) => _logger.Error(message);
    public void LogDebug(string message) => _logger.Debug(message);
}
