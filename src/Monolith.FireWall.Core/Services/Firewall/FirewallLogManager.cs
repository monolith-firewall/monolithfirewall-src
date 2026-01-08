using System.Text.RegularExpressions;
using Monolith.FireWall.Core.Services.Platform;
using Monolith.FireWall.Platform.Models;

namespace Monolith.FireWall.Core.Services.Firewall;

/// <summary>
/// Manages firewall log viewing and parsing
/// Parses kernel logs from nftables to provide structured firewall log entries
/// </summary>
public sealed class FirewallLogManager
{
    private readonly PlatformCommandRunner _commandRunner;
    private static readonly Regex FilterRuleLogPattern = new(@"MF\[(\d+),([^,]+),([^,]+),([^\]]+)\]", RegexOptions.Compiled);
    private static readonly Regex NatRuleLogPattern = new(@"MF-NAT\[(\d+),([^,]+),([^,]+),([^\]]+)\]", RegexOptions.Compiled);

    public FirewallLogManager(PlatformCommandRunner? commandRunner = null)
    {
        _commandRunner = commandRunner ?? new PlatformCommandRunner();
    }

    /// <summary>
    /// Get firewall logs from kernel log (journalctl or /var/log/kern.log)
    /// </summary>
    public async Task<FirewallLogQueryResult> GetLogsAsync(
        FirewallLogQuery query,
        CancellationToken cancellationToken = default)
    {
        var result = new FirewallLogQueryResult
        {
            Success = true,
            Logs = new List<FirewallLogEntry>(),
            TotalCount = 0
        };

        try
        {
            // Use journalctl to get kernel logs with nftables entries
            // journalctl -k searches kernel messages only
            var args = "-k --no-pager --output=short-iso";

            // Add time filters
            if (query.Since.HasValue)
            {
                args += $" --since \"{query.Since.Value:yyyy-MM-dd HH:mm:ss}\"";
            }
            if (query.Until.HasValue)
            {
                args += $" --until \"{query.Until.Value:yyyy-MM-dd HH:mm:ss}\"";
            }

            // Limit lines
            var lines = query.Limit ?? 1000;
            args += $" -n {lines}";

            // Get reverse chronological order (newest first)
            args += " --reverse";

            var command = new PlatformCommand
            {
                FileName = "journalctl",
                Arguments = args,
                UseSudo = false,
                TimeoutMs = 30000
            };

            var cmdResult = await _commandRunner.RunAsync(command, cancellationToken);

            if (cmdResult.ExitCode != 0)
            {
                result.Success = false;
                result.Error = "Failed to retrieve logs from journalctl";
                return result;
            }

            if (string.IsNullOrWhiteSpace(cmdResult.StdOut))
            {
                return result;
            }

            // Parse log entries
            var logLines = cmdResult.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var entries = new List<FirewallLogEntry>();

            foreach (var line in logLines)
            {
                // Look for our MF or MF-NAT log prefixes
                if (line.Contains("MF[") || line.Contains("MF-NAT["))
                {
                    var entry = ParseLogEntry(line);
                    if (entry != null)
                    {
                        // Apply filters
                        if (query.RuleId.HasValue && entry.RuleId != query.RuleId.Value)
                            continue;

                        if (!string.IsNullOrWhiteSpace(query.Interface) &&
                            !string.Equals(entry.Interface, query.Interface, StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (!string.IsNullOrWhiteSpace(query.Action) &&
                            !string.Equals(entry.Action, query.Action, StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (!string.IsNullOrWhiteSpace(query.SourceIp) &&
                            !entry.SourceIp?.Contains(query.SourceIp, StringComparison.OrdinalIgnoreCase) == true)
                            continue;

                        if (!string.IsNullOrWhiteSpace(query.DestinationIp) &&
                            !entry.DestinationIp?.Contains(query.DestinationIp, StringComparison.OrdinalIgnoreCase) == true)
                            continue;

                        entries.Add(entry);
                    }
                }
            }

            result.Logs = entries;
            result.TotalCount = entries.Count;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = $"Error retrieving firewall logs: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Parse a single kernel log line containing MF or MF-NAT prefix
    /// </summary>
    private FirewallLogEntry? ParseLogEntry(string line)
    {
        try
        {
            // Example filter rule log:
            // 2026-01-08T12:34:56+0000 firewall kernel: MF[5,wan,in,block] IN=eth0 OUT= SRC=1.2.3.4 DST=192.168.1.1 PROTO=TCP SPT=54321 DPT=22

            // Example NAT rule log:
            // 2026-01-08T12:34:56+0000 firewall kernel: MF-NAT[3,wan,port_forward,192.168.1.100:80] IN=eth0 OUT= SRC=1.2.3.4 DST=10.0.0.1 PROTO=TCP SPT=54321 DPT=80

            var entry = new FirewallLogEntry
            {
                RawLine = line
            };

            // Extract timestamp (ISO 8601 format at beginning)
            var timestampMatch = Regex.Match(line, @"^(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}[+-]\d{4})");
            if (timestampMatch.Success)
            {
                if (DateTime.TryParse(timestampMatch.Groups[1].Value, out var timestamp))
                {
                    entry.Timestamp = timestamp;
                }
            }

            // Check if this is a filter rule or NAT rule
            var filterMatch = FilterRuleLogPattern.Match(line);
            var natMatch = NatRuleLogPattern.Match(line);

            if (filterMatch.Success)
            {
                entry.RuleType = "filter";
                entry.RuleId = int.Parse(filterMatch.Groups[1].Value);
                entry.Interface = filterMatch.Groups[2].Value;
                entry.Direction = filterMatch.Groups[3].Value;
                entry.Action = filterMatch.Groups[4].Value;
            }
            else if (natMatch.Success)
            {
                entry.RuleType = "nat";
                entry.RuleId = int.Parse(natMatch.Groups[1].Value);
                entry.Interface = natMatch.Groups[2].Value;
                entry.NatType = natMatch.Groups[3].Value;
                entry.NatTarget = natMatch.Groups[4].Value;
            }
            else
            {
                return null; // Not our log format
            }

            // Parse common nftables/iptables log fields
            // IN=interface, OUT=interface, SRC=ip, DST=ip, PROTO=protocol, SPT=port, DPT=port

            var inMatch = Regex.Match(line, @"IN=(\S+)");
            if (inMatch.Success && !string.IsNullOrWhiteSpace(inMatch.Groups[1].Value))
            {
                entry.InInterface = inMatch.Groups[1].Value;
            }

            var outMatch = Regex.Match(line, @"OUT=(\S+)");
            if (outMatch.Success && !string.IsNullOrWhiteSpace(outMatch.Groups[1].Value))
            {
                entry.OutInterface = outMatch.Groups[1].Value;
            }

            var srcMatch = Regex.Match(line, @"SRC=(\S+)");
            if (srcMatch.Success)
            {
                entry.SourceIp = srcMatch.Groups[1].Value;
            }

            var dstMatch = Regex.Match(line, @"DST=(\S+)");
            if (dstMatch.Success)
            {
                entry.DestinationIp = dstMatch.Groups[1].Value;
            }

            var protoMatch = Regex.Match(line, @"PROTO=(\S+)");
            if (protoMatch.Success)
            {
                entry.Protocol = protoMatch.Groups[1].Value;
            }

            var sptMatch = Regex.Match(line, @"SPT=(\d+)");
            if (sptMatch.Success)
            {
                entry.SourcePort = int.Parse(sptMatch.Groups[1].Value);
            }

            var dptMatch = Regex.Match(line, @"DPT=(\d+)");
            if (dptMatch.Success)
            {
                entry.DestinationPort = int.Parse(dptMatch.Groups[1].Value);
            }

            return entry;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Query parameters for firewall logs
/// </summary>
public sealed class FirewallLogQuery
{
    public DateTime? Since { get; set; }
    public DateTime? Until { get; set; }
    public int? Limit { get; set; } = 1000;
    public int? RuleId { get; set; }
    public string? Interface { get; set; }
    public string? Action { get; set; }
    public string? SourceIp { get; set; }
    public string? DestinationIp { get; set; }
}

/// <summary>
/// Result of a firewall log query
/// </summary>
public sealed class FirewallLogQueryResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<FirewallLogEntry> Logs { get; set; } = new();
    public int TotalCount { get; set; }
}

/// <summary>
/// Parsed firewall log entry
/// </summary>
public sealed class FirewallLogEntry
{
    public DateTime Timestamp { get; set; }
    public string RuleType { get; set; } = string.Empty; // "filter" or "nat"
    public int RuleId { get; set; }
    public string? Interface { get; set; }
    public string? Direction { get; set; } // For filter rules
    public string? Action { get; set; } // For filter rules
    public string? NatType { get; set; } // For NAT rules
    public string? NatTarget { get; set; } // For NAT rules
    public string? InInterface { get; set; }
    public string? OutInterface { get; set; }
    public string? SourceIp { get; set; }
    public string? DestinationIp { get; set; }
    public string? Protocol { get; set; }
    public int? SourcePort { get; set; }
    public int? DestinationPort { get; set; }
    public string RawLine { get; set; } = string.Empty;
}
