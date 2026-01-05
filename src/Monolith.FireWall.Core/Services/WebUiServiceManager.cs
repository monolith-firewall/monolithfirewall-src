using Monolith.FireWall.Common.Interfaces;
using Monolith.FireWall.Core.Models;
using Monolith.FireWall.Core.Services.Platform;
using Monolith.FireWall.Platform.Models;

namespace Monolith.FireWall.Core.Services;

/// <summary>
/// Manages WebUI service (monolith-firewall-webui.service).
/// </summary>
public sealed class WebUiServiceManager
{
    private readonly ILogger _logger;
    private readonly PlatformCommandRunner _commandRunner;

    public WebUiServiceManager(ILogger logger, PlatformCommandRunner commandRunner)
    {
        _logger = logger;
        _commandRunner = commandRunner;
    }

    /// <summary>
    /// Restart the WebUI service.
    /// </summary>
    public async Task<WebUiServiceRestartResult> RestartServiceAsync(CancellationToken cancellationToken = default)
    {
        var result = new WebUiServiceRestartResult
        {
            Success = false
        };

        try
        {
            _logger.LogInformation("Restarting monolith-firewall-webui.service...");

            // Reload systemd daemon first (in case service file changed)
            var reloadCommand = new PlatformCommand
            {
                FileName = "systemctl",
                Arguments = "daemon-reload",
                TimeoutMs = 5000
            };
            var reloadResult = await _commandRunner.RunAsync(reloadCommand, cancellationToken);

            if (reloadResult.ExitCode != 0)
            {
                _logger.LogWarning($"Failed to reload systemd daemon: {reloadResult.StdErr}");
                // Continue anyway - might not be critical
            }

            // Restart the service
            var restartCommand = new PlatformCommand
            {
                FileName = "systemctl",
                Arguments = "restart monolith-firewall-webui.service",
                TimeoutMs = 10000
            };
            var restartResult = await _commandRunner.RunAsync(restartCommand, cancellationToken);

            if (restartResult.ExitCode != 0)
            {
                result.Error = !string.IsNullOrEmpty(restartResult.StdErr) ? restartResult.StdErr : "Failed to restart WebUI service";
                _logger.LogError($"Failed to restart WebUI service: {result.Error}");
                return result;
            }

            // Check service status
            await Task.Delay(1000, cancellationToken); // Give it a moment to start

            var statusCommand = new PlatformCommand
            {
                FileName = "systemctl",
                Arguments = "is-active monolith-firewall-webui.service",
                TimeoutMs = 5000
            };
            var statusResult = await _commandRunner.RunAsync(statusCommand, cancellationToken);

            result.Success = true;
            result.ServiceStatus = !string.IsNullOrEmpty(statusResult.StdOut) ? statusResult.StdOut.Trim() : "unknown";
            _logger.LogInformation($"WebUI service restarted successfully. Status: {result.ServiceStatus}");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restarting WebUI service");
            result.Error = ex.Message;
            return result;
        }
    }

    /// <summary>
    /// Get WebUI service status.
    /// </summary>
    public async Task<string> GetServiceStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var command = new PlatformCommand
            {
                FileName = "systemctl",
                Arguments = "is-active monolith-firewall-webui.service",
                TimeoutMs = 5000
            };
            var result = await _commandRunner.RunAsync(command, cancellationToken);

            return !string.IsNullOrEmpty(result.StdOut) ? result.StdOut.Trim() : "unknown";
        }
        catch
        {
            return "unknown";
        }
    }
}
