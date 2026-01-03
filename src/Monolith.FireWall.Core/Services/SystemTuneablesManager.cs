using System.Globalization;
using System.IO;
using Monolith.FireWall.Common.Services;
using Monolith.FireWall.Core.Models;
using Monolith.FireWall.Core.Services.Platform;
using Monolith.FireWall.Platform.Models;

namespace Monolith.FireWall.Core.Services;

public sealed class SystemTuneablesManager
{
    private static readonly string[] SysctlCandidates = { "/usr/sbin/sysctl", "/sbin/sysctl", "sysctl" };
    private readonly SystemTuneablesStore _store;
    private readonly PlatformCommandRunner _commandRunner;
    private readonly LoggingManager _loggingManager;

    public SystemTuneablesManager(SystemTuneablesStore store, PlatformCommandRunner commandRunner)
    {
        _store = store;
        _commandRunner = commandRunner;
        _loggingManager = LoggingManager.Instance;
    }

    public async Task<List<TuneableView>> GetTuneablesAsync(CancellationToken cancellationToken)
    {
        var stored = await _store.GetAllAsync();
        var storedMap = stored.ToDictionary(s => s.Key, StringComparer.OrdinalIgnoreCase);
        var results = new List<TuneableView>();

        foreach (var def in GetCatalog())
        {
            var current = await GetCurrentValueAsync(def.Key, cancellationToken);
            var desired = storedMap.TryGetValue(def.Key, out var storedValue)
                ? storedValue.Value
                : current ?? def.DefaultValue;

            results.Add(new TuneableView
            {
                Key = def.Key,
                Label = def.Label,
                Description = def.Description,
                Category = def.Category,
                Type = def.Type,
                DefaultValue = def.DefaultValue,
                CurrentValue = current,
                DesiredValue = desired,
                Featured = def.Featured,
                Options = def.Options
            });
        }

        return results;
    }

    public async Task<TuneableApplyResult> ApplyAsync(TuneableApplyRequest request, CancellationToken cancellationToken)
    {
        if (request?.Items == null || request.Items.Count == 0)
        {
            return new TuneableApplyResult
            {
                Success = false,
                Error = "No tuneables provided"
            };
        }

        var results = new List<TuneableApplyItemResult>();
        var definitions = GetCatalog().ToDictionary(d => d.Key, StringComparer.OrdinalIgnoreCase);
        var sysctl = ResolveSysctlBinary();

        if (string.IsNullOrWhiteSpace(sysctl))
        {
            return new TuneableApplyResult
            {
                Success = false,
                Error = "sysctl command not available"
            };
        }

        foreach (var item in request.Items)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Key))
            {
                continue;
            }

            if (!definitions.TryGetValue(item.Key, out var def))
            {
                results.Add(new TuneableApplyItemResult
                {
                    Key = item.Key,
                    Success = false,
                    Error = "Unknown tuneable"
                });
                continue;
            }

            if (!TryNormalizeValue(def, item.Value, out var normalized, out var error))
            {
                results.Add(new TuneableApplyItemResult
                {
                    Key = def.Key,
                    Success = false,
                    Error = error
                });
                continue;
            }

            var now = DateTime.UtcNow;
            var existing = await _store.GetByKeyAsync(def.Key);
            var entity = existing ?? new SystemTuneableEntity { Key = def.Key };
            entity.Value = normalized;
            entity.UpdatedAt = now;

            var applyResult = await RunSysctlSetAsync(sysctl, def.Key, normalized, cancellationToken);
            if (applyResult.Success)
            {
                entity.LastAppliedAt = now;
            }

            await _store.UpsertAsync(entity);

            if (applyResult.Success)
            {
                await _store.UpdateAppliedAsync(def.Key, now);
            }

            var currentValue = applyResult.Success
                ? await GetCurrentValueAsync(def.Key, cancellationToken)
                : null;

            results.Add(new TuneableApplyItemResult
            {
                Key = def.Key,
                Success = applyResult.Success,
                Error = applyResult.Error,
                AppliedValue = normalized,
                CurrentValue = currentValue
            });

            await _loggingManager.LogSystemAsync(
                "Network",
                applyResult.Success ? "info" : "warning",
                "SystemTuneablesManager",
                $"{(applyResult.Success ? "Applied" : "Failed")} tuneable {def.Key}",
                new Dictionary<string, object>
                {
                    ["key"] = def.Key,
                    ["value"] = normalized,
                    ["success"] = applyResult.Success,
                    ["error"] = applyResult.Error ?? string.Empty
                });
        }

        return new TuneableApplyResult
        {
            Success = results.All(r => r.Success),
            Results = results,
            Error = results.All(r => r.Success) ? null : "One or more tuneables failed to apply"
        };
    }

    public async Task<TuneableApplyResult> SaveAsync(TuneableApplyRequest request, CancellationToken cancellationToken)
    {
        if (request?.Items == null || request.Items.Count == 0)
        {
            return new TuneableApplyResult
            {
                Success = false,
                Error = "No tuneables provided"
            };
        }

        var results = new List<TuneableApplyItemResult>();
        var definitions = GetCatalog().ToDictionary(d => d.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var item in request.Items)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Key))
            {
                continue;
            }

            if (!definitions.TryGetValue(item.Key, out var def))
            {
                results.Add(new TuneableApplyItemResult
                {
                    Key = item.Key,
                    Success = false,
                    Error = "Unknown tuneable"
                });
                continue;
            }

            if (!TryNormalizeValue(def, item.Value, out var normalized, out var error))
            {
                results.Add(new TuneableApplyItemResult
                {
                    Key = def.Key,
                    Success = false,
                    Error = error
                });
                continue;
            }

            var now = DateTime.UtcNow;
            var existing = await _store.GetByKeyAsync(def.Key);
            var entity = existing ?? new SystemTuneableEntity { Key = def.Key };
            entity.Value = normalized;
            entity.UpdatedAt = now;

            await _store.UpsertAsync(entity);

            results.Add(new TuneableApplyItemResult
            {
                Key = def.Key,
                Success = true,
                AppliedValue = normalized
            });

            await _loggingManager.LogSystemAsync(
                "Network",
                "info",
                "SystemTuneablesManager",
                $"Saved tuneable {def.Key}",
                new Dictionary<string, object>
                {
                    ["key"] = def.Key,
                    ["value"] = normalized
                });
        }

        return new TuneableApplyResult
        {
            Success = results.All(r => r.Success),
            Results = results,
            Error = results.All(r => r.Success) ? null : "One or more tuneables failed to save"
        };
    }

    private async Task<string?> GetCurrentValueAsync(string key, CancellationToken cancellationToken)
    {
        var sysctl = ResolveSysctlBinary();
        if (string.IsNullOrWhiteSpace(sysctl))
        {
            return null;
        }

        var command = new PlatformCommand
        {
            FileName = sysctl,
            Arguments = $"-n {key}",
            UseSudo = false,
            TimeoutMs = 3000
        };

        var result = await _commandRunner.RunAsync(command, cancellationToken);
        if (result.ExitCode != 0)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(result.StdOut)
            ? null
            : result.StdOut.Trim();
    }

    private async Task<(bool Success, string? Error)> RunSysctlSetAsync(string sysctl, string key, string value, CancellationToken cancellationToken)
    {
        var command = new PlatformCommand
        {
            FileName = sysctl,
            Arguments = $"-w {key}={value}",
            UseSudo = true,
            TimeoutMs = 5000
        };

        var result = await _commandRunner.RunAsync(command, cancellationToken);
        if (result.ExitCode == 0)
        {
            return (true, null);
        }

        var error = string.IsNullOrWhiteSpace(result.StdErr)
            ? result.StdOut
            : result.StdErr;

        return (false, string.IsNullOrWhiteSpace(error) ? "sysctl failed" : error.Trim());
    }

    private string? ResolveSysctlBinary()
    {
        foreach (var candidate in SysctlCandidates)
        {
            if (candidate == "sysctl")
            {
                if (_commandRunner.CommandExists(candidate))
                {
                    return candidate;
                }
                continue;
            }

            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool TryNormalizeValue(TuneableDefinition def, string? rawValue, out string normalized, out string error)
    {
        normalized = string.Empty;
        error = string.Empty;

        if (rawValue == null)
        {
            error = "Value is required";
            return false;
        }

        var trimmed = rawValue.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            error = "Value is required";
            return false;
        }

        switch (def.Type)
        {
            case "bool":
                if (TryNormalizeBool(trimmed, out normalized))
                {
                    return true;
                }
                error = "Expected a boolean value";
                return false;
            case "int":
                if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intVal))
                {
                    normalized = intVal.ToString(CultureInfo.InvariantCulture);
                    return true;
                }
                error = "Expected an integer value";
                return false;
            case "select":
                if (def.Options == null || def.Options.Count == 0)
                {
                    error = "No options available";
                    return false;
                }
                if (def.Options.Any(opt => string.Equals(opt.Value, trimmed, StringComparison.OrdinalIgnoreCase)))
                {
                    normalized = trimmed;
                    return true;
                }
                error = "Value not allowed";
                return false;
            case "string":
            default:
                normalized = trimmed;
                return true;
        }
    }

    private static bool TryNormalizeBool(string value, out string normalized)
    {
        normalized = string.Empty;
        var lowered = value.ToLowerInvariant();
        if (lowered is "1" or "true" or "yes" or "on" or "enable" or "enabled")
        {
            normalized = "1";
            return true;
        }

        if (lowered is "0" or "false" or "no" or "off" or "disable" or "disabled")
        {
            normalized = "0";
            return true;
        }

        return false;
    }

    private static List<TuneableDefinition> GetCatalog()
    {
        return new List<TuneableDefinition>
        {
            new()
            {
                Key = "net.ipv4.ip_forward",
                Label = "IPv4 forwarding",
                Description = "Allow routing IPv4 packets between interfaces.",
                Category = "Routing",
                Type = "bool",
                DefaultValue = "0",
                Featured = true
            },
            new()
            {
                Key = "net.ipv6.conf.all.forwarding",
                Label = "IPv6 forwarding",
                Description = "Allow routing IPv6 packets between interfaces.",
                Category = "Routing",
                Type = "bool",
                DefaultValue = "0",
                Featured = true
            },
            new()
            {
                Key = "net.ipv4.conf.all.rp_filter",
                Label = "Reverse path filtering",
                Description = "Drop packets with source addresses that do not match routing table expectations.",
                Category = "Security",
                Type = "select",
                DefaultValue = "1",
                Featured = true,
                Options = new List<TuneableOption>
                {
                    new() { Value = "0", Label = "Disabled" },
                    new() { Value = "1", Label = "Strict" },
                    new() { Value = "2", Label = "Loose" }
                }
            },
            new()
            {
                Key = "net.ipv4.tcp_syncookies",
                Label = "TCP SYN cookies",
                Description = "Enable SYN cookies to mitigate SYN flood attacks.",
                Category = "Security",
                Type = "bool",
                DefaultValue = "1",
                Featured = true
            },
            new()
            {
                Key = "net.ipv4.conf.all.accept_redirects",
                Label = "Accept ICMP redirects",
                Description = "Allow ICMP redirects to change routing decisions.",
                Category = "Security",
                Type = "bool",
                DefaultValue = "0",
                Featured = true
            },
            new()
            {
                Key = "net.ipv4.conf.all.send_redirects",
                Label = "Send ICMP redirects",
                Description = "Allow this system to send ICMP redirects.",
                Category = "Security",
                Type = "bool",
                DefaultValue = "0",
                Featured = false
            },
            new()
            {
                Key = "net.ipv4.conf.all.log_martians",
                Label = "Log martian packets",
                Description = "Log packets with impossible source addresses.",
                Category = "Security",
                Type = "bool",
                DefaultValue = "0",
                Featured = true
            },
            new()
            {
                Key = "net.ipv4.conf.all.accept_source_route",
                Label = "Accept source routed packets",
                Description = "Allow packets to dictate the route they take through the network.",
                Category = "Security",
                Type = "bool",
                DefaultValue = "0",
                Featured = false
            },
            new()
            {
                Key = "net.ipv4.icmp_echo_ignore_broadcasts",
                Label = "Ignore broadcast pings",
                Description = "Ignore ICMP echo requests to broadcast addresses.",
                Category = "ICMP",
                Type = "bool",
                DefaultValue = "1",
                Featured = false
            },
            new()
            {
                Key = "net.ipv4.icmp_ignore_bogus_error_responses",
                Label = "Ignore bogus ICMP errors",
                Description = "Ignore malformed ICMP error responses.",
                Category = "ICMP",
                Type = "bool",
                DefaultValue = "1",
                Featured = false
            },
            new()
            {
                Key = "net.core.somaxconn",
                Label = "Max listen backlog",
                Description = "Maximum number of queued connection requests.",
                Category = "Performance",
                Type = "int",
                DefaultValue = "1024",
                Featured = false
            },
            new()
            {
                Key = "net.core.netdev_max_backlog",
                Label = "Netdev backlog",
                Description = "Maximum packets queued on the input side when the interface receives packets faster than they can be processed.",
                Category = "Performance",
                Type = "int",
                DefaultValue = "1000",
                Featured = false
            },
            new()
            {
                Key = "net.ipv4.tcp_max_syn_backlog",
                Label = "TCP SYN backlog",
                Description = "Maximum number of queued SYN requests.",
                Category = "Performance",
                Type = "int",
                DefaultValue = "2048",
                Featured = false
            },
            new()
            {
                Key = "net.ipv4.tcp_fin_timeout",
                Label = "TCP FIN timeout",
                Description = "Time in seconds to wait for a FIN to close a socket.",
                Category = "Performance",
                Type = "int",
                DefaultValue = "60",
                Featured = false
            },
            new()
            {
                Key = "net.ipv4.tcp_keepalive_time",
                Label = "TCP keepalive time",
                Description = "Idle time in seconds before TCP sends keepalive probes.",
                Category = "Performance",
                Type = "int",
                DefaultValue = "7200",
                Featured = false
            },
            new()
            {
                Key = "net.ipv4.tcp_keepalive_intvl",
                Label = "TCP keepalive interval",
                Description = "Seconds between keepalive probes.",
                Category = "Performance",
                Type = "int",
                DefaultValue = "75",
                Featured = false
            },
            new()
            {
                Key = "net.ipv4.tcp_keepalive_probes",
                Label = "TCP keepalive probes",
                Description = "Number of keepalive probes before declaring the peer dead.",
                Category = "Performance",
                Type = "int",
                DefaultValue = "9",
                Featured = false
            },
            new()
            {
                Key = "net.bridge.bridge-nf-call-iptables",
                Label = "Bridge IPv4 firewalling",
                Description = "Pass bridged IPv4 packets through iptables.",
                Category = "Bridge",
                Type = "bool",
                DefaultValue = "1",
                Featured = false
            },
            new()
            {
                Key = "net.bridge.bridge-nf-call-ip6tables",
                Label = "Bridge IPv6 firewalling",
                Description = "Pass bridged IPv6 packets through ip6tables.",
                Category = "Bridge",
                Type = "bool",
                DefaultValue = "1",
                Featured = false
            }
        };
    }
}
