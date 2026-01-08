using Monolith.FireWall.Core.Services.Platform;
using Monolith.FireWall.Platform.Models;

namespace Monolith.FireWall.Core.Services;

/// <summary>
/// Manages systemd services - start, stop, restart, enable, disable, and status checking
/// </summary>
public sealed class SystemdServiceManager
{
    private readonly PlatformCommandRunner _commandRunner;

    public SystemdServiceManager(PlatformCommandRunner? commandRunner = null)
    {
        _commandRunner = commandRunner ?? new PlatformCommandRunner();
    }

    /// <summary>
    /// Start a systemd service
    /// </summary>
    public async Task<ServiceOperationResult> StartServiceAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        var command = new PlatformCommand
        {
            FileName = "systemctl",
            Arguments = $"start {serviceName}",
            UseSudo = true,
            TimeoutMs = 30000
        };

        var result = await _commandRunner.RunAsync(command, cancellationToken);
        return new ServiceOperationResult
        {
            Success = result.ExitCode == 0,
            ServiceName = serviceName,
            Operation = "start",
            ErrorMessage = result.ExitCode != 0 ? result.StdErr : null
        };
    }

    /// <summary>
    /// Stop a systemd service
    /// </summary>
    public async Task<ServiceOperationResult> StopServiceAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        var command = new PlatformCommand
        {
            FileName = "systemctl",
            Arguments = $"stop {serviceName}",
            UseSudo = true,
            TimeoutMs = 30000
        };

        var result = await _commandRunner.RunAsync(command, cancellationToken);
        return new ServiceOperationResult
        {
            Success = result.ExitCode == 0,
            ServiceName = serviceName,
            Operation = "stop",
            ErrorMessage = result.ExitCode != 0 ? result.StdErr : null
        };
    }

    /// <summary>
    /// Restart a systemd service
    /// </summary>
    public async Task<ServiceOperationResult> RestartServiceAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        var command = new PlatformCommand
        {
            FileName = "systemctl",
            Arguments = $"restart {serviceName}",
            UseSudo = true,
            TimeoutMs = 30000
        };

        var result = await _commandRunner.RunAsync(command, cancellationToken);
        return new ServiceOperationResult
        {
            Success = result.ExitCode == 0,
            ServiceName = serviceName,
            Operation = "restart",
            ErrorMessage = result.ExitCode != 0 ? result.StdErr : null
        };
    }

    /// <summary>
    /// Reload a systemd service configuration without restarting
    /// </summary>
    public async Task<ServiceOperationResult> ReloadServiceAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        var command = new PlatformCommand
        {
            FileName = "systemctl",
            Arguments = $"reload {serviceName}",
            UseSudo = true,
            TimeoutMs = 30000
        };

        var result = await _commandRunner.RunAsync(command, cancellationToken);
        return new ServiceOperationResult
        {
            Success = result.ExitCode == 0,
            ServiceName = serviceName,
            Operation = "reload",
            ErrorMessage = result.ExitCode != 0 ? result.StdErr : null
        };
    }

    /// <summary>
    /// Enable a systemd service to start on boot
    /// </summary>
    public async Task<ServiceOperationResult> EnableServiceAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        var command = new PlatformCommand
        {
            FileName = "systemctl",
            Arguments = $"enable {serviceName}",
            UseSudo = true,
            TimeoutMs = 10000
        };

        var result = await _commandRunner.RunAsync(command, cancellationToken);
        return new ServiceOperationResult
        {
            Success = result.ExitCode == 0,
            ServiceName = serviceName,
            Operation = "enable",
            ErrorMessage = result.ExitCode != 0 ? result.StdErr : null
        };
    }

    /// <summary>
    /// Disable a systemd service from starting on boot
    /// </summary>
    public async Task<ServiceOperationResult> DisableServiceAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        var command = new PlatformCommand
        {
            FileName = "systemctl",
            Arguments = $"disable {serviceName}",
            UseSudo = true,
            TimeoutMs = 10000
        };

        var result = await _commandRunner.RunAsync(command, cancellationToken);
        return new ServiceOperationResult
        {
            Success = result.ExitCode == 0,
            ServiceName = serviceName,
            Operation = "disable",
            ErrorMessage = result.ExitCode != 0 ? result.StdErr : null
        };
    }

    /// <summary>
    /// Get the status of a systemd service
    /// </summary>
    public async Task<SystemdServiceStatus> GetServiceStatusAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        // Use 'systemctl is-active' for quick status check
        var activeCommand = new PlatformCommand
        {
            FileName = "systemctl",
            Arguments = $"is-active {serviceName}",
            UseSudo = false,
            TimeoutMs = 5000
        };

        var activeResult = await _commandRunner.RunAsync(activeCommand, cancellationToken);
        var activeState = activeResult.StdOut?.Trim() ?? "unknown";

        // Use 'systemctl is-enabled' to check boot status
        var enabledCommand = new PlatformCommand
        {
            FileName = "systemctl",
            Arguments = $"is-enabled {serviceName}",
            UseSudo = false,
            TimeoutMs = 5000
        };

        var enabledResult = await _commandRunner.RunAsync(enabledCommand, cancellationToken);
        var enabledState = enabledResult.StdOut?.Trim() ?? "unknown";

        return new SystemdServiceStatus
        {
            ServiceName = serviceName,
            ActiveState = ParseActiveState(activeState),
            EnabledState = ParseEnabledState(enabledState),
            RawActiveState = activeState,
            RawEnabledState = enabledState
        };
    }

    /// <summary>
    /// Check if a systemd service exists
    /// </summary>
    public async Task<bool> ServiceExistsAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        var command = new PlatformCommand
        {
            FileName = "systemctl",
            Arguments = $"list-unit-files {serviceName}",
            UseSudo = false,
            TimeoutMs = 5000
        };

        var result = await _commandRunner.RunAsync(command, cancellationToken);
        return result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StdOut) && result.StdOut.Contains(serviceName);
    }

    /// <summary>
    /// Reload systemd daemon configuration
    /// </summary>
    public async Task<ServiceOperationResult> DaemonReloadAsync(CancellationToken cancellationToken = default)
    {
        var command = new PlatformCommand
        {
            FileName = "systemctl",
            Arguments = "daemon-reload",
            UseSudo = true,
            TimeoutMs = 10000
        };

        var result = await _commandRunner.RunAsync(command, cancellationToken);
        return new ServiceOperationResult
        {
            Success = result.ExitCode == 0,
            ServiceName = "systemd",
            Operation = "daemon-reload",
            ErrorMessage = result.ExitCode != 0 ? result.StdErr : null
        };
    }

    private static ServiceActiveState ParseActiveState(string state)
    {
        return state.ToLowerInvariant() switch
        {
            "active" => ServiceActiveState.Active,
            "inactive" => ServiceActiveState.Inactive,
            "failed" => ServiceActiveState.Failed,
            "activating" => ServiceActiveState.Activating,
            "deactivating" => ServiceActiveState.Deactivating,
            _ => ServiceActiveState.Unknown
        };
    }

    private static ServiceEnabledState ParseEnabledState(string state)
    {
        return state.ToLowerInvariant() switch
        {
            "enabled" => ServiceEnabledState.Enabled,
            "disabled" => ServiceEnabledState.Disabled,
            "static" => ServiceEnabledState.Static,
            "masked" => ServiceEnabledState.Masked,
            _ => ServiceEnabledState.Unknown
        };
    }
}

/// <summary>
/// Result of a service operation (start, stop, restart, etc.)
/// </summary>
public sealed class ServiceOperationResult
{
    public bool Success { get; init; }
    public string ServiceName { get; init; } = string.Empty;
    public string Operation { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Status of a systemd service
/// </summary>
public sealed class SystemdServiceStatus
{
    public string ServiceName { get; init; } = string.Empty;
    public ServiceActiveState ActiveState { get; init; }
    public ServiceEnabledState EnabledState { get; init; }
    public string RawActiveState { get; init; } = string.Empty;
    public string RawEnabledState { get; init; } = string.Empty;

    public bool IsRunning => ActiveState == ServiceActiveState.Active;
    public bool IsEnabled => EnabledState == ServiceEnabledState.Enabled;
}

/// <summary>
/// Active state of a systemd service
/// </summary>
public enum ServiceActiveState
{
    Unknown,
    Active,
    Inactive,
    Failed,
    Activating,
    Deactivating
}

/// <summary>
/// Enabled state of a systemd service
/// </summary>
public enum ServiceEnabledState
{
    Unknown,
    Enabled,
    Disabled,
    Static,
    Masked
}
