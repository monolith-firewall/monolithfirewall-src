using Monolith.FireWall.Core.Models;
using Monolith.FireWall.Core.Services.Platform;
using Monolith.FireWall.Platform.Models;
using Monolith.FireWall.Platform.Validation;

namespace Monolith.FireWall.Core.Services;

public sealed class SystemSettingsManager
{
    private readonly SystemSettingsStore _store;
    private readonly PlatformCommandRunner _commandRunner;

    public SystemSettingsManager(SystemSettingsStore store, PlatformCommandRunner? commandRunner = null)
    {
        _store = store;
        _commandRunner = commandRunner ?? new PlatformCommandRunner();
    }

    public async Task<SystemSettingsView> GetSettingsAsync()
    {
        var entity = await _store.GetAsync();
        var dns = ParseDnsServers(entity?.DnsServers);
        var ntp = ParseDnsServers(entity?.NtpServers); // Reuse same parsing logic

        return new SystemSettingsView
        {
            Hostname = entity?.Hostname ?? ReadFileTrim("/etc/hostname"),
            Domain = entity?.Domain,
            Timezone = entity?.Timezone ?? ReadFileTrim("/etc/timezone"),
            DnsServers = dns.Count > 0 ? dns : ReadResolvConf(),
            NtpServers = ntp.Count > 0 ? ntp : ReadNtpConf(),
            CurrentDateTime = DateTime.Now
        };
    }

    public async Task<(bool Success, string? Error)> UpdateSettingsAsync(SystemSettingsUpdateRequest request)
    {
        var hostname = request.Hostname?.Trim();
        if (!string.IsNullOrWhiteSpace(hostname) && !PlatformValidators.IsValidHostname(hostname))
        {
            return (false, "Invalid hostname");
        }

        List<string>? dnsServers = null;
        if (request.DnsServers != null)
        {
            dnsServers = request.DnsServers
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .ToList();

            if (dnsServers.Count > 0 && !PlatformValidators.AreValidDnsServers(dnsServers))
            {
                return (false, "Invalid DNS server address");
            }
        }

        // Validate NTP servers if provided
        List<string>? ntpServers = null;
        if (request.NtpServers != null)
        {
            ntpServers = request.NtpServers
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .ToList();

            // Basic validation: should be hostname or IP
            foreach (var ntp in ntpServers)
            {
                if (!PlatformValidators.IsValidHostname(ntp) && !PlatformValidators.IsValidIp(ntp))
                {
                    return (false, $"Invalid NTP server: {ntp}");
                }
            }
        }

        var existing = await _store.GetAsync();
        var entity = existing ?? new SystemSettingsEntity();
        
        // Update hostname in database and system
        if (!string.IsNullOrWhiteSpace(hostname) && hostname != entity.Hostname)
        {
            entity.Hostname = hostname;
            await ApplyHostnameAsync(hostname);
        }
        else if (!string.IsNullOrWhiteSpace(hostname))
        {
            entity.Hostname = hostname;
        }

        // Update timezone in database and system
        if (!string.IsNullOrWhiteSpace(request.Timezone) && request.Timezone != entity.Timezone)
        {
            entity.Timezone = request.Timezone.Trim();
            await ApplyTimezoneAsync(entity.Timezone);
        }
        else if (!string.IsNullOrWhiteSpace(request.Timezone))
        {
            entity.Timezone = request.Timezone.Trim();
        }

        // Update date/time if provided
        if (request.DateTime.HasValue)
        {
            await ApplyDateTimeAsync(request.DateTime.Value);
        }

        // Update NTP servers
        if (ntpServers != null)
        {
            entity.NtpServers = ntpServers.Count > 0 ? string.Join(',', ntpServers) : null;
            if (ntpServers.Count > 0)
            {
                await ApplyNtpServersAsync(ntpServers);
            }
        }

        entity.Domain = request.Domain?.Trim() ?? entity.Domain;
        if (dnsServers != null)
        {
            entity.DnsServers = dnsServers.Count > 0 ? string.Join(',', dnsServers) : null;
            if (dnsServers.Count > 0)
            {
                await ApplyDnsServersAsync(dnsServers);
            }
        }
        entity.UpdatedAt = DateTime.UtcNow;

        var saved = await _store.UpsertAsync(entity);
        return saved ? (true, null) : (false, "Failed to update settings");
    }

    /// <summary>
    /// Apply stored system settings from database to the system.
    /// Used during startup initialization.
    /// </summary>
    public async Task<SystemSettingsResult> ApplyStoredSettingsAsync(CancellationToken cancellationToken = default)
    {
        var result = new SystemSettingsResult();
        
        try
        {
            var entity = await _store.GetAsync();
            if (entity == null)
            {
                // No settings stored, nothing to apply
                result.Success = true;
                return result;
            }

            // Apply hostname if stored
            if (!string.IsNullOrWhiteSpace(entity.Hostname))
            {
                try
                {
                    await ApplyHostnameAsync(entity.Hostname);
                    result.HostnameApplied = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Warning: Failed to apply hostname: {ex.Message}");
                }
            }

            // Apply timezone if stored
            if (!string.IsNullOrWhiteSpace(entity.Timezone))
            {
                try
                {
                    await ApplyTimezoneAsync(entity.Timezone);
                    result.TimezoneApplied = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Warning: Failed to apply timezone: {ex.Message}");
                }
            }

            // Apply DNS servers if stored
            var dnsServers = ParseDnsServers(entity.DnsServers);
            if (dnsServers.Count > 0)
            {
                try
                {
                    await ApplyDnsServersAsync(dnsServers);
                    result.DnsApplied = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Warning: Failed to apply DNS servers: {ex.Message}");
                }
            }

            // Apply NTP servers if stored
            var ntpServers = ParseDnsServers(entity.NtpServers);
            if (ntpServers.Count > 0)
            {
                try
                {
                    await ApplyNtpServersAsync(ntpServers);
                    result.NtpApplied = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Warning: Failed to apply NTP servers: {ex.Message}");
                }
            }

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    public async Task<IReadOnlyList<string>> GetDnsServersAsync()
    {
        var entity = await _store.GetAsync();
        var servers = ParseDnsServers(entity?.DnsServers);
        return servers.Count > 0 ? servers : ReadResolvConf();
    }

    private static List<string> ParseDnsServers(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new List<string>();
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

    private static string ReadFileTrim(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path).Trim() : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static List<string> ReadResolvConf()
    {
        var servers = new List<string>();
        try
        {
            if (!File.Exists("/etc/resolv.conf"))
            {
                return servers;
            }

            foreach (var line in File.ReadAllLines("/etc/resolv.conf"))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("nameserver ", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && PlatformValidators.IsValidIp(parts[1]))
                    {
                        servers.Add(parts[1]);
                    }
                }
            }
        }
        catch
        {
            return servers;
        }

        return servers;
    }

    private static List<string> ReadNtpConf()
    {
        var servers = new List<string>();
        try
        {
            // Try systemd-timesyncd config
            if (File.Exists("/etc/systemd/timesyncd.conf"))
            {
                foreach (var line in File.ReadAllLines("/etc/systemd/timesyncd.conf"))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("NTP=", StringComparison.OrdinalIgnoreCase))
                    {
                        var ntpLine = trimmed.Substring(4).Trim();
                        if (!string.IsNullOrWhiteSpace(ntpLine))
                        {
                            servers.AddRange(ntpLine.Split(' ', StringSplitOptions.RemoveEmptyEntries));
                        }
                    }
                }
            }

            // Fallback to common defaults
            if (servers.Count == 0)
            {
                servers.AddRange(new[] { "0.debian.pool.ntp.org", "1.debian.pool.ntp.org", "2.debian.pool.ntp.org", "3.debian.pool.ntp.org" });
            }
        }
        catch
        {
            // Return defaults on error
            return new List<string> { "0.debian.pool.ntp.org", "1.debian.pool.ntp.org", "2.debian.pool.ntp.org", "3.debian.pool.ntp.org" };
        }

        return servers;
    }

    /// <summary>
    /// Apply hostname to system
    /// </summary>
    private async Task ApplyHostnameAsync(string hostname)
    {
        try
        {
            // Update /etc/hostname
            await File.WriteAllTextAsync("/etc/hostname", hostname + "\n");

            // Set hostname using hostnamectl
            var command = new PlatformCommand
            {
                FileName = "hostnamectl",
                Arguments = string.Join(" ", new[] { "set-hostname", hostname }),
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
        }
        catch (Exception ex)
        {
            // Log but don't fail - hostname update is best effort
            System.Diagnostics.Debug.WriteLine($"Warning: Failed to apply hostname: {ex.Message}");
        }
    }

    /// <summary>
    /// Apply timezone to system
    /// </summary>
    private async Task ApplyTimezoneAsync(string timezone)
    {
        try
        {
            // Update /etc/timezone
            await File.WriteAllTextAsync("/etc/timezone", timezone + "\n");

            // Set timezone using timedatectl
            var command = new PlatformCommand
            {
                FileName = "timedatectl",
                Arguments = string.Join(" ", new[] { "set-timezone", timezone }),
                UseSudo = true
            };
            var result = await _commandRunner.RunAsync(command, CancellationToken.None);
            if (result.ExitCode != 0)
            {
                // Fallback: create symlink
                var zoneInfoPath = $"/usr/share/zoneinfo/{timezone}";
                if (File.Exists(zoneInfoPath))
                {
                    File.Delete("/etc/localtime");
                    File.CreateSymbolicLink("/etc/localtime", zoneInfoPath);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Warning: Failed to apply timezone: {ex.Message}");
        }
    }

    /// <summary>
    /// Apply date/time to system
    /// </summary>
    private async Task ApplyDateTimeAsync(DateTime dateTime)
    {
        try
        {
            // Format: YYYY-MM-DD HH:MM:SS
            var dateTimeStr = dateTime.ToString("yyyy-MM-dd HH:mm:ss");
            var command = new PlatformCommand
            {
                FileName = "timedatectl",
                Arguments = string.Join(" ", new[] { "set-time", dateTimeStr }),
                UseSudo = true
            };
            var result = await _commandRunner.RunAsync(command, CancellationToken.None);
            if (result.ExitCode != 0)
            {
                // Fallback: use date command
                var dateStr = dateTime.ToString("MMddHHmmyyyy.ss");
                var fallbackCommand = new PlatformCommand
                {
                    FileName = "date",
                    Arguments = dateStr,
                    UseSudo = true
                };
                await _commandRunner.RunAsync(fallbackCommand, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Warning: Failed to apply date/time: {ex.Message}");
        }
    }

    /// <summary>
    /// Apply DNS servers to system
    /// </summary>
    private async Task ApplyDnsServersAsync(List<string> dnsServers)
    {
        try
        {
            // Check if systemd-resolved is available and active
            var systemdResolvedExists = File.Exists("/run/systemd/resolve/stub-resolv.conf") ||
                                        File.Exists("/etc/systemd/resolved.conf");

            if (systemdResolvedExists)
            {
                // Use systemd-resolved
                await ApplyDnsViaSystemdResolvedAsync(dnsServers);
            }
            else
            {
                // Fallback to direct /etc/resolv.conf modification
                await ApplyDnsViaResolvConfAsync(dnsServers);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Warning: Failed to apply DNS servers: {ex.Message}");
        }
    }

    /// <summary>
    /// Apply DNS servers via systemd-resolved
    /// </summary>
    private async Task ApplyDnsViaSystemdResolvedAsync(List<string> dnsServers)
    {
        try
        {
            var configPath = "/etc/systemd/resolved.conf";
            var lines = new List<string>();

            // Read existing config
            if (File.Exists(configPath))
            {
                var existingLines = await File.ReadAllLinesAsync(configPath);
                bool inResolveSection = false;

                foreach (var line in existingLines)
                {
                    var trimmed = line.Trim();

                    // Track if we're in the [Resolve] section
                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        inResolveSection = trimmed == "[Resolve]";
                        lines.Add(line);
                        continue;
                    }

                    // Skip existing DNS= lines in [Resolve] section
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
                // Create new config file
                lines.Add("[Resolve]");
            }

            // Ensure we have a [Resolve] section
            if (!lines.Any(l => l.Trim() == "[Resolve]"))
            {
                lines.Insert(0, "[Resolve]");
            }

            // Add DNS servers after [Resolve] section
            var resolveIndex = lines.FindIndex(l => l.Trim() == "[Resolve]");
            if (resolveIndex >= 0)
            {
                lines.Insert(resolveIndex + 1, $"DNS={string.Join(" ", dnsServers)}");
            }

            await File.WriteAllLinesAsync(configPath, lines);

            // Restart systemd-resolved
            var restartCommand = new PlatformCommand
            {
                FileName = "systemctl",
                Arguments = "restart systemd-resolved.service",
                UseSudo = true,
                TimeoutMs = 10000
            };
            await _commandRunner.RunAsync(restartCommand, CancellationToken.None);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Warning: Failed to apply DNS via systemd-resolved: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Apply DNS servers via direct /etc/resolv.conf modification
    /// </summary>
    private async Task ApplyDnsViaResolvConfAsync(List<string> dnsServers)
    {
        try
        {
            var lines = new List<string>();

            // Read existing resolv.conf and preserve non-nameserver lines
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

            // Add new nameserver entries
            foreach (var dns in dnsServers)
            {
                lines.Add($"nameserver {dns}");
            }

            await File.WriteAllLinesAsync("/etc/resolv.conf", lines);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Warning: Failed to apply DNS via resolv.conf: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Apply NTP servers to system
    /// </summary>
    private async Task ApplyNtpServersAsync(List<string> ntpServers)
    {
        try
        {
            // Update systemd-timesyncd configuration
            var configPath = "/etc/systemd/timesyncd.conf";
            var lines = new List<string>();

            if (File.Exists(configPath))
            {
                var existingLines = await File.ReadAllLinesAsync(configPath);
                foreach (var line in existingLines)
                {
                    if (!line.Trim().StartsWith("NTP=", StringComparison.OrdinalIgnoreCase) &&
                        !line.Trim().StartsWith("#NTP=", StringComparison.OrdinalIgnoreCase))
                    {
                        lines.Add(line);
                    }
                }
            }

            // Add NTP servers
            lines.Add($"NTP={string.Join(" ", ntpServers)}");

            await File.WriteAllLinesAsync(configPath, lines);

            // Reload systemd-timesyncd
            var restartCommand = new PlatformCommand
            {
                FileName = "systemctl",
                Arguments = string.Join(" ", new[] { "restart", "systemd-timesyncd.service" }),
                UseSudo = true
            };
            await _commandRunner.RunAsync(restartCommand, CancellationToken.None);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Warning: Failed to apply NTP servers: {ex.Message}");
        }
    }

    /// <summary>
    /// Get list of available timezones
    /// </summary>
    public async Task<List<string>> GetTimezonesAsync()
    {
        var timezones = new List<string>();

        try
        {
            // Try timedatectl first (preferred method)
            var command = new PlatformCommand
            {
                FileName = "timedatectl",
                Arguments = "list-timezones",
                UseSudo = false
            };
            var result = await _commandRunner.RunAsync(command, CancellationToken.None);
            if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StdOut))
            {
                timezones.AddRange(result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                if (timezones.Count > 0)
                {
                    return timezones;
                }
            }
        }
        catch
        {
            // Fall back to reading zoneinfo directory
        }

        // Fallback: read from /usr/share/zoneinfo
        try
        {
            timezones = ReadTimezonesFromZoneinfo("/usr/share/zoneinfo", "");
        }
        catch
        {
            // Return common timezones as last resort
            return new List<string>
            {
                "UTC", "America/New_York", "America/Chicago", "America/Denver", "America/Los_Angeles",
                "Europe/London", "Europe/Paris", "Europe/Berlin", "Asia/Tokyo", "Asia/Shanghai"
            };
        }

        return timezones;
    }

    private List<string> ReadTimezonesFromZoneinfo(string basePath, string prefix)
    {
        var timezones = new List<string>();

        try
        {
            if (!Directory.Exists(basePath))
            {
                return timezones;
            }

            foreach (var entry in Directory.GetFileSystemEntries(basePath))
            {
                var name = Path.GetFileName(entry);
                var fullPath = Path.Combine(basePath, name);
                var timezoneName = string.IsNullOrEmpty(prefix) ? name : $"{prefix}/{name}";

                if (File.Exists(fullPath))
                {
                    // It's a timezone file
                    timezones.Add(timezoneName);
                }
                else if (Directory.Exists(fullPath))
                {
                    // It's a directory, recurse
                    timezones.AddRange(ReadTimezonesFromZoneinfo(fullPath, timezoneName));
                }
            }
        }
        catch
        {
            // Ignore errors
        }

        return timezones.OrderBy(t => t).ToList();
    }
}
