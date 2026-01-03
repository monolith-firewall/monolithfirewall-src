using Monolith.FireWall.Core.Models;
using Monolith.FireWall.Platform.Validation;

namespace Monolith.FireWall.Core.Services;

public sealed class SystemSettingsManager
{
    private readonly SystemSettingsStore _store;

    public SystemSettingsManager(SystemSettingsStore store)
    {
        _store = store;
    }

    public async Task<SystemSettingsView> GetSettingsAsync()
    {
        var entity = await _store.GetAsync();
        var dns = ParseDnsServers(entity?.DnsServers);

        return new SystemSettingsView
        {
            Hostname = entity?.Hostname ?? ReadFileTrim("/etc/hostname"),
            Domain = entity?.Domain,
            Timezone = entity?.Timezone ?? ReadFileTrim("/etc/timezone"),
            DnsServers = dns.Count > 0 ? dns : ReadResolvConf()
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

        var existing = await _store.GetAsync();
        var entity = existing ?? new SystemSettingsEntity();
        entity.Hostname = hostname ?? entity.Hostname;
        entity.Domain = request.Domain?.Trim() ?? entity.Domain;
        entity.Timezone = request.Timezone?.Trim() ?? entity.Timezone;
        if (dnsServers != null)
        {
            entity.DnsServers = dnsServers.Count > 0 ? string.Join(',', dnsServers) : null;
        }
        entity.UpdatedAt = DateTime.UtcNow;

        var saved = await _store.UpsertAsync(entity);
        return saved ? (true, null) : (false, "Failed to update settings");
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
}
