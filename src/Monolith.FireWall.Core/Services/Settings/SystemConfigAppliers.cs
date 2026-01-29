using System.Text.Json;
using Monolith.FireWall.Common.Interfaces;
using Monolith.FireWall.Core.Models;
using Monolith.FireWall.Core.Services.Platform;
using Monolith.FireWall.Platform.Models;
using Monolith.FireWall.Platform.Validation;

namespace Monolith.FireWall.Core.Services.Settings;

/// <summary>
/// Config applier for hostname settings.
/// </summary>
public sealed class HostnameApplier : IConfigApplier
{
    private readonly ILogger _logger;
    private readonly PlatformCommandRunner _commandRunner;

    public string TargetType => "SystemConfig";
    public bool SupportsRollback => true;

    public HostnameApplier(ILogger logger, PlatformCommandRunner? commandRunner = null)
    {
        _logger = logger;
        _commandRunner = commandRunner ?? new PlatformCommandRunner();
    }

    public Task<ValidationResult> ValidateAsync(string targetKey, string? currentValue, string? newValue)
    {
        if (string.IsNullOrWhiteSpace(newValue))
        {
            return Task.FromResult(new ValidationResult
            {
                IsValid = false,
                Errors = new List<string> { "Hostname value is required" }
            });
        }

        try
        {
            var config = JsonSerializer.Deserialize<HostnameConfig>(newValue);
            if (config == null || string.IsNullOrWhiteSpace(config.Hostname))
            {
                return Task.FromResult(new ValidationResult
                {
                    IsValid = false,
                    Errors = new List<string> { "Invalid hostname configuration" }
                });
            }

            if (!PlatformValidators.IsValidHostname(config.Hostname))
            {
                return Task.FromResult(new ValidationResult
                {
                    IsValid = false,
                    Errors = new List<string> { "Invalid hostname format. Hostname must start with a letter, contain only letters, numbers, and hyphens, and not end with a hyphen." }
                });
            }

            return Task.FromResult(new ValidationResult { IsValid = true });
        }
        catch (JsonException ex)
        {
            return Task.FromResult(new ValidationResult
            {
                IsValid = false,
                Errors = new List<string> { $"Invalid JSON format: {ex.Message}" }
            });
        }
    }

    public async Task<ApplyResult> ApplyAsync(string targetKey, string? previousValue, string? newValue)
    {
        if (string.IsNullOrWhiteSpace(newValue))
        {
            return new ApplyResult { Success = false, Error = "No hostname value provided" };
        }

        try
        {
            var config = JsonSerializer.Deserialize<HostnameConfig>(newValue);
            if (config == null || string.IsNullOrWhiteSpace(config.Hostname))
            {
                return new ApplyResult { Success = false, Error = "Invalid hostname configuration" };
            }

            var hostname = config.Hostname.Trim();

            // Update /etc/hostname
            await File.WriteAllTextAsync("/etc/hostname", hostname + "\n");

            // Set hostname using hostnamectl
            var command = new PlatformCommand
            {
                FileName = "hostnamectl",
                Arguments = $"set-hostname {hostname}",
                UseSudo = true
            };
            var result = await _commandRunner.RunAsync(command, CancellationToken.None);

            if (result.ExitCode != 0)
            {
                // Fallback: try hostname command
                var fallbackCommand = new PlatformCommand
                {
                    FileName = "hostname",
                    Arguments = hostname,
                    UseSudo = true
                };
                await _commandRunner.RunAsync(fallbackCommand, CancellationToken.None);
            }

            _logger.LogInformation($"Applied hostname: {hostname}");
            return new ApplyResult { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to apply hostname: {ex.Message}");
            return new ApplyResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<ApplyResult> RollbackAsync(string targetKey, string? currentValue, string? previousValue)
    {
        if (string.IsNullOrWhiteSpace(previousValue))
        {
            return new ApplyResult { Success = false, Error = "No previous hostname value to rollback to" };
        }

        return await ApplyAsync(targetKey, currentValue, previousValue);
    }
}

/// <summary>
/// Config applier for timezone settings.
/// </summary>
public sealed class TimezoneApplier : IConfigApplier
{
    private readonly ILogger _logger;
    private readonly PlatformCommandRunner _commandRunner;

    public string TargetType => "SystemConfig";
    public bool SupportsRollback => true;

    public TimezoneApplier(ILogger logger, PlatformCommandRunner? commandRunner = null)
    {
        _logger = logger;
        _commandRunner = commandRunner ?? new PlatformCommandRunner();
    }

    public Task<ValidationResult> ValidateAsync(string targetKey, string? currentValue, string? newValue)
    {
        if (string.IsNullOrWhiteSpace(newValue))
        {
            return Task.FromResult(new ValidationResult
            {
                IsValid = false,
                Errors = new List<string> { "Timezone value is required" }
            });
        }

        try
        {
            var config = JsonSerializer.Deserialize<TimezoneConfig>(newValue);
            if (config == null || string.IsNullOrWhiteSpace(config.Timezone))
            {
                return Task.FromResult(new ValidationResult
                {
                    IsValid = false,
                    Errors = new List<string> { "Invalid timezone configuration" }
                });
            }

            // Check if timezone exists in zoneinfo
            var zonePath = $"/usr/share/zoneinfo/{config.Timezone}";
            if (!File.Exists(zonePath))
            {
                return Task.FromResult(new ValidationResult
                {
                    IsValid = false,
                    Errors = new List<string> { $"Unknown timezone: {config.Timezone}" }
                });
            }

            return Task.FromResult(new ValidationResult { IsValid = true });
        }
        catch (JsonException ex)
        {
            return Task.FromResult(new ValidationResult
            {
                IsValid = false,
                Errors = new List<string> { $"Invalid JSON format: {ex.Message}" }
            });
        }
    }

    public async Task<ApplyResult> ApplyAsync(string targetKey, string? previousValue, string? newValue)
    {
        if (string.IsNullOrWhiteSpace(newValue))
        {
            return new ApplyResult { Success = false, Error = "No timezone value provided" };
        }

        try
        {
            var config = JsonSerializer.Deserialize<TimezoneConfig>(newValue);
            if (config == null || string.IsNullOrWhiteSpace(config.Timezone))
            {
                return new ApplyResult { Success = false, Error = "Invalid timezone configuration" };
            }

            var timezone = config.Timezone.Trim();

            // Update /etc/timezone
            await File.WriteAllTextAsync("/etc/timezone", timezone + "\n");

            // Set timezone using timedatectl
            var command = new PlatformCommand
            {
                FileName = "timedatectl",
                Arguments = $"set-timezone {timezone}",
                UseSudo = true
            };
            var result = await _commandRunner.RunAsync(command, CancellationToken.None);

            if (result.ExitCode != 0)
            {
                // Fallback: create symlink
                var zoneInfoPath = $"/usr/share/zoneinfo/{timezone}";
                if (File.Exists(zoneInfoPath))
                {
                    if (File.Exists("/etc/localtime"))
                    {
                        File.Delete("/etc/localtime");
                    }
                    File.CreateSymbolicLink("/etc/localtime", zoneInfoPath);
                }
            }

            _logger.LogInformation($"Applied timezone: {timezone}");
            return new ApplyResult { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to apply timezone: {ex.Message}");
            return new ApplyResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<ApplyResult> RollbackAsync(string targetKey, string? currentValue, string? previousValue)
    {
        if (string.IsNullOrWhiteSpace(previousValue))
        {
            return new ApplyResult { Success = false, Error = "No previous timezone value to rollback to" };
        }

        return await ApplyAsync(targetKey, currentValue, previousValue);
    }
}

/// <summary>
/// Config applier for DNS server settings.
/// </summary>
public sealed class DnsApplier : IConfigApplier
{
    private readonly ILogger _logger;
    private readonly PlatformCommandRunner _commandRunner;

    public string TargetType => "SystemConfig";
    public bool SupportsRollback => true;

    public DnsApplier(ILogger logger, PlatformCommandRunner? commandRunner = null)
    {
        _logger = logger;
        _commandRunner = commandRunner ?? new PlatformCommandRunner();
    }

    public Task<ValidationResult> ValidateAsync(string targetKey, string? currentValue, string? newValue)
    {
        if (string.IsNullOrWhiteSpace(newValue))
        {
            return Task.FromResult(new ValidationResult
            {
                IsValid = false,
                Errors = new List<string> { "DNS configuration is required" }
            });
        }

        try
        {
            var config = JsonSerializer.Deserialize<DnsConfig>(newValue);
            if (config == null)
            {
                return Task.FromResult(new ValidationResult
                {
                    IsValid = false,
                    Errors = new List<string> { "Invalid DNS configuration" }
                });
            }

            if (config.Servers == null || config.Servers.Count == 0)
            {
                return Task.FromResult(new ValidationResult
                {
                    IsValid = false,
                    Errors = new List<string> { "At least one DNS server is required" }
                });
            }

            var errors = new List<string>();
            foreach (var server in config.Servers)
            {
                if (!PlatformValidators.IsValidIp(server))
                {
                    errors.Add($"Invalid DNS server IP address: {server}");
                }
            }

            if (errors.Count > 0)
            {
                return Task.FromResult(new ValidationResult
                {
                    IsValid = false,
                    Errors = errors
                });
            }

            return Task.FromResult(new ValidationResult { IsValid = true });
        }
        catch (JsonException ex)
        {
            return Task.FromResult(new ValidationResult
            {
                IsValid = false,
                Errors = new List<string> { $"Invalid JSON format: {ex.Message}" }
            });
        }
    }

    public async Task<ApplyResult> ApplyAsync(string targetKey, string? previousValue, string? newValue)
    {
        if (string.IsNullOrWhiteSpace(newValue))
        {
            return new ApplyResult { Success = false, Error = "No DNS configuration provided" };
        }

        try
        {
            var config = JsonSerializer.Deserialize<DnsConfig>(newValue);
            if (config?.Servers == null || config.Servers.Count == 0)
            {
                return new ApplyResult { Success = false, Error = "Invalid DNS configuration" };
            }

            // Check if systemd-resolved is available and active
            var systemdResolvedExists = File.Exists("/run/systemd/resolve/stub-resolv.conf") ||
                                        File.Exists("/etc/systemd/resolved.conf");

            if (systemdResolvedExists)
            {
                await ApplyDnsViaSystemdResolvedAsync(config.Servers);
            }
            else
            {
                await ApplyDnsViaResolvConfAsync(config.Servers);
            }

            _logger.LogInformation($"Applied DNS servers: {string.Join(", ", config.Servers)}");
            return new ApplyResult { Success = true, RequiresRestart = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to apply DNS servers: {ex.Message}");
            return new ApplyResult { Success = false, Error = ex.Message };
        }
    }

    private async Task ApplyDnsViaSystemdResolvedAsync(List<string> dnsServers)
    {
        var configPath = "/etc/systemd/resolved.conf";
        var lines = new List<string>();

        if (File.Exists(configPath))
        {
            var existingLines = await File.ReadAllLinesAsync(configPath);
            bool inResolveSection = false;

            foreach (var line in existingLines)
            {
                var trimmed = line.Trim();

                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    inResolveSection = trimmed == "[Resolve]";
                    lines.Add(line);
                    continue;
                }

                if (inResolveSection &&
                    (trimmed.StartsWith("DNS=", StringComparison.OrdinalIgnoreCase) ||
                     trimmed.StartsWith("#DNS=", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                lines.Add(line);
            }
        }
        else
        {
            lines.Add("[Resolve]");
        }

        if (!lines.Any(l => l.Trim() == "[Resolve]"))
        {
            lines.Insert(0, "[Resolve]");
        }

        var resolveIndex = lines.FindIndex(l => l.Trim() == "[Resolve]");
        if (resolveIndex >= 0)
        {
            lines.Insert(resolveIndex + 1, $"DNS={string.Join(" ", dnsServers)}");
        }

        await File.WriteAllLinesAsync(configPath, lines);

        var restartCommand = new PlatformCommand
        {
            FileName = "systemctl",
            Arguments = "restart systemd-resolved.service",
            UseSudo = true,
            TimeoutMs = 10000
        };
        await _commandRunner.RunAsync(restartCommand, CancellationToken.None);
    }

    private async Task ApplyDnsViaResolvConfAsync(List<string> dnsServers)
    {
        var lines = new List<string>();

        if (File.Exists("/etc/resolv.conf"))
        {
            var existingLines = await File.ReadAllLinesAsync("/etc/resolv.conf");
            foreach (var line in existingLines)
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("nameserver ", StringComparison.OrdinalIgnoreCase))
                {
                    lines.Add(line);
                }
            }
        }

        foreach (var dns in dnsServers)
        {
            lines.Add($"nameserver {dns}");
        }

        await File.WriteAllLinesAsync("/etc/resolv.conf", lines);
    }

    public async Task<ApplyResult> RollbackAsync(string targetKey, string? currentValue, string? previousValue)
    {
        if (string.IsNullOrWhiteSpace(previousValue))
        {
            return new ApplyResult { Success = false, Error = "No previous DNS configuration to rollback to" };
        }

        return await ApplyAsync(targetKey, currentValue, previousValue);
    }
}

/// <summary>
/// Config applier for NTP server settings.
/// </summary>
public sealed class NtpApplier : IConfigApplier
{
    private readonly ILogger _logger;
    private readonly PlatformCommandRunner _commandRunner;

    public string TargetType => "SystemConfig";
    public bool SupportsRollback => true;

    public NtpApplier(ILogger logger, PlatformCommandRunner? commandRunner = null)
    {
        _logger = logger;
        _commandRunner = commandRunner ?? new PlatformCommandRunner();
    }

    public Task<ValidationResult> ValidateAsync(string targetKey, string? currentValue, string? newValue)
    {
        if (string.IsNullOrWhiteSpace(newValue))
        {
            return Task.FromResult(new ValidationResult
            {
                IsValid = false,
                Errors = new List<string> { "NTP configuration is required" }
            });
        }

        try
        {
            var config = JsonSerializer.Deserialize<NtpConfig>(newValue);
            if (config == null)
            {
                return Task.FromResult(new ValidationResult
                {
                    IsValid = false,
                    Errors = new List<string> { "Invalid NTP configuration" }
                });
            }

            if (config.Servers == null || config.Servers.Count == 0)
            {
                return Task.FromResult(new ValidationResult
                {
                    IsValid = false,
                    Errors = new List<string> { "At least one NTP server is required" }
                });
            }

            var errors = new List<string>();
            foreach (var server in config.Servers)
            {
                if (!PlatformValidators.IsValidHostname(server) && !PlatformValidators.IsValidIp(server))
                {
                    errors.Add($"Invalid NTP server: {server}");
                }
            }

            if (errors.Count > 0)
            {
                return Task.FromResult(new ValidationResult
                {
                    IsValid = false,
                    Errors = errors
                });
            }

            return Task.FromResult(new ValidationResult { IsValid = true });
        }
        catch (JsonException ex)
        {
            return Task.FromResult(new ValidationResult
            {
                IsValid = false,
                Errors = new List<string> { $"Invalid JSON format: {ex.Message}" }
            });
        }
    }

    public async Task<ApplyResult> ApplyAsync(string targetKey, string? previousValue, string? newValue)
    {
        if (string.IsNullOrWhiteSpace(newValue))
        {
            return new ApplyResult { Success = false, Error = "No NTP configuration provided" };
        }

        try
        {
            var config = JsonSerializer.Deserialize<NtpConfig>(newValue);
            if (config?.Servers == null || config.Servers.Count == 0)
            {
                return new ApplyResult { Success = false, Error = "Invalid NTP configuration" };
            }

            // Update systemd-timesyncd configuration
            var configPath = "/etc/systemd/timesyncd.conf";
            var lines = new List<string>();

            if (File.Exists(configPath))
            {
                var existingLines = await File.ReadAllLinesAsync(configPath);
                bool hasTimeSection = false;

                foreach (var line in existingLines)
                {
                    var trimmed = line.Trim();
                    if (trimmed == "[Time]")
                    {
                        hasTimeSection = true;
                    }

                    if (!trimmed.StartsWith("NTP=", StringComparison.OrdinalIgnoreCase) &&
                        !trimmed.StartsWith("#NTP=", StringComparison.OrdinalIgnoreCase))
                    {
                        lines.Add(line);
                    }
                }

                if (!hasTimeSection)
                {
                    lines.Insert(0, "[Time]");
                }
            }
            else
            {
                lines.Add("[Time]");
            }

            // Find the [Time] section and add NTP servers after it
            var timeIndex = lines.FindIndex(l => l.Trim() == "[Time]");
            if (timeIndex >= 0)
            {
                lines.Insert(timeIndex + 1, $"NTP={string.Join(" ", config.Servers)}");
            }
            else
            {
                lines.Add($"NTP={string.Join(" ", config.Servers)}");
            }

            await File.WriteAllLinesAsync(configPath, lines);

            // Restart systemd-timesyncd
            var restartCommand = new PlatformCommand
            {
                FileName = "systemctl",
                Arguments = "restart systemd-timesyncd.service",
                UseSudo = true
            };
            await _commandRunner.RunAsync(restartCommand, CancellationToken.None);

            _logger.LogInformation($"Applied NTP servers: {string.Join(", ", config.Servers)}");
            return new ApplyResult { Success = true, RequiresRestart = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to apply NTP servers: {ex.Message}");
            return new ApplyResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<ApplyResult> RollbackAsync(string targetKey, string? currentValue, string? previousValue)
    {
        if (string.IsNullOrWhiteSpace(previousValue))
        {
            return new ApplyResult { Success = false, Error = "No previous NTP configuration to rollback to" };
        }

        return await ApplyAsync(targetKey, currentValue, previousValue);
    }
}

/// <summary>
/// Config applier for IP forwarding settings.
/// </summary>
public sealed class IpForwardingApplier : IConfigApplier
{
    private readonly ILogger _logger;
    private readonly PlatformCommandRunner _commandRunner;

    public string TargetType => "SystemConfig";
    public bool SupportsRollback => true;

    public IpForwardingApplier(ILogger logger, PlatformCommandRunner? commandRunner = null)
    {
        _logger = logger;
        _commandRunner = commandRunner ?? new PlatformCommandRunner();
    }

    public Task<ValidationResult> ValidateAsync(string targetKey, string? currentValue, string? newValue)
    {
        if (string.IsNullOrWhiteSpace(newValue))
        {
            return Task.FromResult(new ValidationResult
            {
                IsValid = false,
                Errors = new List<string> { "IP forwarding configuration is required" }
            });
        }

        try
        {
            var config = JsonSerializer.Deserialize<IpForwardingConfig>(newValue);
            if (config == null)
            {
                return Task.FromResult(new ValidationResult
                {
                    IsValid = false,
                    Errors = new List<string> { "Invalid IP forwarding configuration" }
                });
            }

            return Task.FromResult(new ValidationResult { IsValid = true });
        }
        catch (JsonException ex)
        {
            return Task.FromResult(new ValidationResult
            {
                IsValid = false,
                Errors = new List<string> { $"Invalid JSON format: {ex.Message}" }
            });
        }
    }

    public async Task<ApplyResult> ApplyAsync(string targetKey, string? previousValue, string? newValue)
    {
        if (string.IsNullOrWhiteSpace(newValue))
        {
            return new ApplyResult { Success = false, Error = "No IP forwarding configuration provided" };
        }

        try
        {
            var config = JsonSerializer.Deserialize<IpForwardingConfig>(newValue);
            if (config == null)
            {
                return new ApplyResult { Success = false, Error = "Invalid IP forwarding configuration" };
            }

            // Apply IPv4 forwarding
            var ipv4Value = config.Ipv4 ? "1" : "0";
            await File.WriteAllTextAsync("/proc/sys/net/ipv4/ip_forward", ipv4Value);

            // Make it persistent via sysctl.conf
            await UpdateSysctlAsync("net.ipv4.ip_forward", ipv4Value);

            // Apply IPv6 forwarding
            var ipv6Value = config.Ipv6 ? "1" : "0";
            if (File.Exists("/proc/sys/net/ipv6/conf/all/forwarding"))
            {
                await File.WriteAllTextAsync("/proc/sys/net/ipv6/conf/all/forwarding", ipv6Value);
                await UpdateSysctlAsync("net.ipv6.conf.all.forwarding", ipv6Value);
            }

            _logger.LogInformation($"Applied IP forwarding: IPv4={config.Ipv4}, IPv6={config.Ipv6}");
            return new ApplyResult { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to apply IP forwarding: {ex.Message}");
            return new ApplyResult { Success = false, Error = ex.Message };
        }
    }

    private async Task UpdateSysctlAsync(string key, string value)
    {
        var sysctlPath = "/etc/sysctl.conf";
        var lines = new List<string>();
        var found = false;

        if (File.Exists(sysctlPath))
        {
            var existingLines = await File.ReadAllLinesAsync(sysctlPath);
            foreach (var line in existingLines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith($"{key}=") || trimmed.StartsWith($"#{key}="))
                {
                    lines.Add($"{key}={value}");
                    found = true;
                }
                else
                {
                    lines.Add(line);
                }
            }
        }

        if (!found)
        {
            lines.Add($"{key}={value}");
        }

        await File.WriteAllLinesAsync(sysctlPath, lines);
    }

    public async Task<ApplyResult> RollbackAsync(string targetKey, string? currentValue, string? previousValue)
    {
        if (string.IsNullOrWhiteSpace(previousValue))
        {
            return new ApplyResult { Success = false, Error = "No previous IP forwarding configuration to rollback to" };
        }

        return await ApplyAsync(targetKey, currentValue, previousValue);
    }
}

// Config models are defined in SettingsModels.cs:
// - HostnameConfig
// - TimezoneConfig
// - DnsConfig
// - NtpConfig
// SystemConfigKeys is also defined in SettingsModels.cs
