using System.Text.Json;
using Monolith.FireWall.Common.Models;
using Monolith.FireWall.Core.Models;
using Monolith.FireWall.Core.Services.Platform;

namespace Monolith.FireWall.Core.Transport.Handlers;

/// <summary>
/// Handler for system settings using the new centralized ISettingsService.
/// Updates create pending changes that must be applied separately.
/// </summary>
public sealed class SystemSettingsHandler : ICoreRequestHandler
{
    private static readonly HashSet<string> Actions = new(StringComparer.OrdinalIgnoreCase)
    {
        "system.settings.get",
        "system.settings.update",
        "system.settings.timezones"
    };

    public bool CanHandle(string action) => Actions.Contains(action);

    public async Task<ApiResponse> HandleAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        if (context.ConfigService == null)
        {
            return new ApiResponse(false, null, "Configuration service not available");
        }

        var action = request.GetProperty("action").GetString() ?? string.Empty;

        switch (action)
        {
            case "system.settings.get":
                return await HandleGetSettingsAsync(context);

            case "system.settings.update":
                return await HandleUpdateSettingsAsync(context, request);

            case "system.settings.timezones":
                return await HandleGetTimezonesAsync(context);
        }

        return new ApiResponse(false, null, $"Unhandled action: {action}");
    }

    private static async Task<ApiResponse> HandleGetSettingsAsync(CoreRequestContext context)
    {
        // Read settings from the new system_configs table
        var hostname = await context.ConfigService!.GetSystemConfigAsync<HostnameConfig>(SystemConfigKeys.Hostname);
        var timezone = await context.ConfigService!.GetSystemConfigAsync<TimezoneConfig>(SystemConfigKeys.Timezone);
        var dns = await context.ConfigService!.GetSystemConfigAsync<DnsConfig>(SystemConfigKeys.Dns);
        var ntp = await context.ConfigService!.GetSystemConfigAsync<NtpConfig>(SystemConfigKeys.Ntp);

        // Build the view model
        var settings = new SystemSettingsView
        {
            Hostname = hostname?.Hostname ?? ReadFileTrim("/etc/hostname"),
            Domain = hostname?.Domain,
            Timezone = timezone?.Timezone ?? ReadFileTrim("/etc/timezone"),
            DnsServers = dns?.Servers ?? ReadResolvConf(),
            NtpServers = ntp?.Servers ?? GetDefaultNtpServers(),
            CurrentDateTime = DateTime.Now
        };

        return new ApiResponse(true, settings, null);
    }

    private static async Task<ApiResponse> HandleUpdateSettingsAsync(CoreRequestContext context, JsonElement request)
    {
        if (!CoreRequestParsing.TryGetPayload(request, out SystemSettingsUpdateRequest updateRequest, out var parseError))
        {
            return new ApiResponse(false, null, parseError);
        }

        string? changedBy = null;
        if (request.TryGetProperty("changedBy", out var changedByEl))
        {
            changedBy = changedByEl.GetString();
        }

        var pendingIds = new List<int>();
        var errors = new List<string>();

        // Update hostname if provided
        if (!string.IsNullOrWhiteSpace(updateRequest.Hostname))
        {
            var hostnameConfig = new HostnameConfig
            {
                Hostname = updateRequest.Hostname.Trim(),
                Domain = updateRequest.Domain?.Trim()
            };

            var result = await context.ConfigService!.SaveSystemConfigAsync(
                SystemConfigKeys.Hostname,
                hostnameConfig,
                changedBy,
                $"Update hostname to '{hostnameConfig.Hostname}'"
            );

            if (result.Success && result.PendingChangeId.HasValue)
            {
                pendingIds.Add(result.PendingChangeId.Value);
            }
            else if (!result.Success)
            {
                errors.Add($"Hostname: {result.ErrorMessage}");
            }
        }

        // Update timezone if provided
        if (!string.IsNullOrWhiteSpace(updateRequest.Timezone))
        {
            var timezoneConfig = new TimezoneConfig
            {
                Timezone = updateRequest.Timezone.Trim()
            };

            var result = await context.ConfigService!.SaveSystemConfigAsync(
                SystemConfigKeys.Timezone,
                timezoneConfig,
                changedBy,
                $"Update timezone to '{timezoneConfig.Timezone}'"
            );

            if (result.Success && result.PendingChangeId.HasValue)
            {
                pendingIds.Add(result.PendingChangeId.Value);
            }
            else if (!result.Success)
            {
                errors.Add($"Timezone: {result.ErrorMessage}");
            }
        }

        // Update DNS servers if provided
        if (updateRequest.DnsServers != null && updateRequest.DnsServers.Count > 0)
        {
            var dnsConfig = new DnsConfig
            {
                Servers = updateRequest.DnsServers
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim())
                    .ToList(),
                SearchDomains = new List<string>()
            };

            var result = await context.ConfigService!.SaveSystemConfigAsync(
                SystemConfigKeys.Dns,
                dnsConfig,
                changedBy,
                $"Update DNS servers to: {string.Join(", ", dnsConfig.Servers)}"
            );

            if (result.Success && result.PendingChangeId.HasValue)
            {
                pendingIds.Add(result.PendingChangeId.Value);
            }
            else if (!result.Success)
            {
                errors.Add($"DNS: {result.ErrorMessage}");
            }
        }

        // Update NTP servers if provided
        if (updateRequest.NtpServers != null && updateRequest.NtpServers.Count > 0)
        {
            var ntpConfig = new NtpConfig
            {
                Servers = updateRequest.NtpServers
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim())
                    .ToList(),
                Enabled = true
            };

            var result = await context.ConfigService!.SaveSystemConfigAsync(
                SystemConfigKeys.Ntp,
                ntpConfig,
                changedBy,
                $"Update NTP servers to: {string.Join(", ", ntpConfig.Servers)}"
            );

            if (result.Success && result.PendingChangeId.HasValue)
            {
                pendingIds.Add(result.PendingChangeId.Value);
            }
            else if (!result.Success)
            {
                errors.Add($"NTP: {result.ErrorMessage}");
            }
        }

        // Handle date/time update (this one applies immediately, not staged)
        if (updateRequest.DateTime.HasValue)
        {
            try
            {
                await ApplyDateTimeAsync(context.CommandRunner, updateRequest.DateTime.Value);
            }
            catch (Exception ex)
            {
                errors.Add($"DateTime: {ex.Message}");
            }
        }

        if (errors.Count > 0)
        {
            return new ApiResponse(false, new
            {
                pendingChangeIds = pendingIds,
                errors
            }, string.Join("; ", errors));
        }

        // Return success with pending change info
        var updatedSettings = await GetCurrentSettingsAsync(context);

        return new ApiResponse(true, new
        {
            settings = updatedSettings,
            staged = pendingIds.Count > 0,
            pendingChangeIds = pendingIds,
            message = pendingIds.Count > 0
                ? $"{pendingIds.Count} change(s) staged. Apply from Pending Changes to activate."
                : "No changes needed."
        }, null);
    }

    private static async Task<ApiResponse> HandleGetTimezonesAsync(CoreRequestContext context)
    {
        var timezones = await GetTimezonesAsync(context.CommandRunner);
        return new ApiResponse(true, new { timezones }, null);
    }

    #region Helper Methods

    private static async Task<SystemSettingsView> GetCurrentSettingsAsync(CoreRequestContext context)
    {
        var hostname = await context.ConfigService!.GetSystemConfigAsync<HostnameConfig>(SystemConfigKeys.Hostname);
        var timezone = await context.ConfigService!.GetSystemConfigAsync<TimezoneConfig>(SystemConfigKeys.Timezone);
        var dns = await context.ConfigService!.GetSystemConfigAsync<DnsConfig>(SystemConfigKeys.Dns);
        var ntp = await context.ConfigService!.GetSystemConfigAsync<NtpConfig>(SystemConfigKeys.Ntp);

        return new SystemSettingsView
        {
            Hostname = hostname?.Hostname ?? ReadFileTrim("/etc/hostname"),
            Domain = hostname?.Domain,
            Timezone = timezone?.Timezone ?? ReadFileTrim("/etc/timezone"),
            DnsServers = dns?.Servers ?? ReadResolvConf(),
            NtpServers = ntp?.Servers ?? GetDefaultNtpServers(),
            CurrentDateTime = DateTime.Now
        };
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
            if (!File.Exists("/etc/resolv.conf")) return servers;

            foreach (var line in File.ReadAllLines("/etc/resolv.conf"))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("nameserver ", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        servers.Add(parts[1]);
                    }
                }
            }
        }
        catch
        {
            // Ignore errors
        }
        return servers;
    }

    private static List<string> GetDefaultNtpServers()
    {
        return new List<string>
        {
            "0.debian.pool.ntp.org",
            "1.debian.pool.ntp.org",
            "2.debian.pool.ntp.org",
            "3.debian.pool.ntp.org"
        };
    }

    private static async Task ApplyDateTimeAsync(PlatformCommandRunner commandRunner, DateTime dateTime)
    {
        var dateTimeStr = dateTime.ToString("yyyy-MM-dd HH:mm:ss");
        var command = new PlatformCommand
        {
            FileName = "timedatectl",
            Arguments = $"set-time \"{dateTimeStr}\"",
            UseSudo = true
        };

        var result = await commandRunner.RunAsync(command, CancellationToken.None);
        if (result.ExitCode != 0)
        {
            // Fallback to date command
            var dateStr = dateTime.ToString("MMddHHmmyyyy.ss");
            var fallbackCommand = new PlatformCommand
            {
                FileName = "date",
                Arguments = dateStr,
                UseSudo = true
            };
            await commandRunner.RunAsync(fallbackCommand, CancellationToken.None);
        }
    }

    private static async Task<List<string>> GetTimezonesAsync(PlatformCommandRunner commandRunner)
    {
        var timezones = new List<string>();

        try
        {
            var command = new PlatformCommand
            {
                FileName = "timedatectl",
                Arguments = "list-timezones",
                UseSudo = false
            };

            var result = await commandRunner.RunAsync(command, CancellationToken.None);
            if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StdOut))
            {
                timezones.AddRange(result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                if (timezones.Count > 0) return timezones;
            }
        }
        catch
        {
            // Fall through to defaults
        }

        // Return common timezones as fallback
        return new List<string>
        {
            "UTC",
            "America/New_York",
            "America/Chicago",
            "America/Denver",
            "America/Los_Angeles",
            "Europe/London",
            "Europe/Paris",
            "Europe/Berlin",
            "Asia/Tokyo",
            "Asia/Shanghai",
            "Australia/Sydney"
        };
    }

    #endregion
}
