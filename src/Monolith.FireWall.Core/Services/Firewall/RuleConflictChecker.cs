using System.Net;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Services.Firewall;

public sealed class RuleConflictChecker
{
    public FirewallRuleValidateResponse CheckConflicts(
        FirewallRuleValidateRequest newRule,
        IEnumerable<FirewallRuleViewExtended> existingRules,
        int? excludeRuleId = null)
    {
        var response = new FirewallRuleValidateResponse
        {
            IsValid = true,
            HasConflicts = false,
            CanProceed = true
        };

        if (string.IsNullOrWhiteSpace(newRule.Interface))
        {
            response.IsValid = false;
            response.CanProceed = false;
            response.Conflicts.Add(new RuleConflict
            {
                Type = "validation",
                Severity = "error",
                Message = "Interface is required",
                Resolution = "Select an interface for this rule"
            });
            return response;
        }

        var rulesToCheck = existingRules
            .Where(r => excludeRuleId == null || r.Id != excludeRuleId)
            .ToList();

        // Check for exact duplicate
        CheckExactDuplicate(newRule, rulesToCheck, response);

        // Check for system rule overlap
        CheckSystemRuleOverlap(newRule, rulesToCheck, response);

        // Check for conflicting actions
        CheckConflictingActions(newRule, rulesToCheck, response);

        // Check for network overlap
        CheckNetworkOverlap(newRule, rulesToCheck, response);

        // Check for port conflicts (informational)
        CheckPortConflicts(newRule, rulesToCheck, response);

        response.HasConflicts = response.Conflicts.Count > 0;
        response.CanProceed = !response.Conflicts.Any(c => c.Severity == "error");

        if (response.HasConflicts && response.CanProceed)
        {
            response.Suggestion = "Review the warnings above. You can still save this rule, but it may not behave as expected.";
        }

        return response;
    }

    private void CheckExactDuplicate(
        FirewallRuleValidateRequest newRule,
        List<FirewallRuleViewExtended> existingRules,
        FirewallRuleValidateResponse response)
    {
        foreach (var existing in existingRules)
        {
            if (!MatchesInterface(newRule.Interface, existing.Interface))
                continue;

            if (!MatchesDirection(newRule.Direction, existing.Direction))
                continue;

            // For exact duplicate, action must also match
            if (!MatchesAction(newRule.Action, existing.Action))
                continue;

            if (!MatchesProtocol(newRule.Protocol, existing.Protocol))
                continue;

            if (!MatchesEndpoint(newRule.SourceType, newRule.SourceValue, newRule.SourcePort,
                    existing.SourceType, existing.SourceValue, existing.SourcePort))
                continue;

            if (!MatchesEndpoint(newRule.DestinationType, newRule.DestinationValue, newRule.DestinationPort,
                    existing.DestinationType, existing.DestinationValue, existing.DestinationPort))
                continue;

            // Exact duplicate found (same match criteria AND same action)
            var ruleDesc = FormatRuleDescription(existing);
            response.Conflicts.Add(new RuleConflict
            {
                Type = "duplicate",
                Severity = "error",
                ConflictingRuleId = existing.Id > 0 ? existing.Id : null,
                ConflictingRuleDescription = ruleDesc,
                Message = "This rule is an exact duplicate of an existing rule",
                Resolution = existing.RuleType == "system"
                    ? "This functionality is already provided by a system rule. No user rule is needed."
                    : "Edit the existing rule instead of creating a duplicate."
            });
            return;
        }
    }

    private void CheckSystemRuleOverlap(
        FirewallRuleValidateRequest newRule,
        List<FirewallRuleViewExtended> existingRules,
        FirewallRuleValidateResponse response)
    {
        var systemRules = existingRules.Where(r => r.RuleType == "system").ToList();

        foreach (var sysRule in systemRules)
        {
            if (!MatchesInterface(newRule.Interface, sysRule.Interface))
                continue;

            if (!MatchesDirection(newRule.Direction, sysRule.Direction))
                continue;

            // Check if actions match and rule has significant overlap
            if (MatchesAction(newRule.Action, sysRule.Action) &&
                MatchesProtocol(newRule.Protocol, sysRule.Protocol) &&
                IsSignificantOverlap(newRule, sysRule))
            {
                var ruleDesc = FormatRuleDescription(sysRule);
                response.Conflicts.Add(new RuleConflict
                {
                    Type = "system_overlap",
                    Severity = "warning",
                    ConflictingRuleId = null,
                    ConflictingRuleDescription = ruleDesc,
                    Message = "This rule duplicates functionality already provided by a system rule",
                    Resolution = "Remove this rule - the system rule already handles this traffic."
                });
            }
        }
    }

    private bool IsSignificantOverlap(FirewallRuleValidateRequest newRule, FirewallRuleViewExtended existing)
    {
        // For a significant overlap, we need source/dest AND port to meaningfully overlap
        // If the system rule has a specific port, new rule must match that port
        if (!string.IsNullOrWhiteSpace(existing.DestinationPort))
        {
            if (!ArePortsOverlapping(newRule.DestinationPort, existing.DestinationPort))
                return false;
        }

        // Check source overlap
        if (!IsEndpointOverlapping(
                newRule.SourceType, newRule.SourceValue, newRule.SourcePort,
                existing.SourceType, existing.SourceValue, existing.SourcePort))
            return false;

        // Check destination overlap
        if (!IsEndpointOverlapping(
                newRule.DestinationType, newRule.DestinationValue, newRule.DestinationPort,
                existing.DestinationType, existing.DestinationValue, existing.DestinationPort))
            return false;

        return true;
    }

    private void CheckConflictingActions(
        FirewallRuleValidateRequest newRule,
        List<FirewallRuleViewExtended> existingRules,
        FirewallRuleValidateResponse response)
    {
        foreach (var existing in existingRules)
        {
            if (!MatchesInterface(newRule.Interface, existing.Interface))
                continue;

            if (!MatchesDirection(newRule.Direction, existing.Direction))
                continue;

            // Check if same match criteria but different action
            if (!MatchesAction(newRule.Action, existing.Action) &&
                IsOverlapping(newRule, existing))
            {
                var ruleDesc = FormatRuleDescription(existing);
                var newAction = NormalizeAction(newRule.Action);
                var existingAction = existing.Action.ToLowerInvariant();

                response.Conflicts.Add(new RuleConflict
                {
                    Type = "action_conflict",
                    Severity = "warning",
                    ConflictingRuleId = existing.Id > 0 ? existing.Id : null,
                    ConflictingRuleDescription = ruleDesc,
                    Message = $"This rule has a different action ({newAction}) than an overlapping rule ({existingAction})",
                    Resolution = existing.RuleType == "system"
                        ? "Your rule will override the system rule for matching traffic. Verify this is intended."
                        : "The rule that matches first will determine the action. Check rule ordering."
                });
            }
        }
    }

    private void CheckNetworkOverlap(
        FirewallRuleValidateRequest newRule,
        List<FirewallRuleViewExtended> existingRules,
        FirewallRuleValidateResponse response)
    {
        var newSourceNet = ParseNetwork(newRule.SourceType, newRule.SourceValue);
        var newDestNet = ParseNetwork(newRule.DestinationType, newRule.DestinationValue);

        foreach (var existing in existingRules)
        {
            if (!MatchesInterface(newRule.Interface, existing.Interface))
                continue;

            if (!MatchesDirection(newRule.Direction, existing.Direction))
                continue;

            var existingSourceNet = ParseNetwork(existing.SourceType, existing.SourceValue);
            var existingDestNet = ParseNetwork(existing.DestinationType, existing.DestinationValue);

            // Check if new rule is more specific than existing (could be shadowed)
            bool sourceNarrower = IsNetworkNarrower(newSourceNet, existingSourceNet);
            bool destNarrower = IsNetworkNarrower(newDestNet, existingDestNet);

            if ((sourceNarrower || destNarrower) && !IsExactMatch(newRule, existing))
            {
                var ruleDesc = FormatRuleDescription(existing);
                response.Conflicts.Add(new RuleConflict
                {
                    Type = "ordering_issue",
                    Severity = "info",
                    ConflictingRuleId = existing.Id > 0 ? existing.Id : null,
                    ConflictingRuleDescription = ruleDesc,
                    Message = "This rule is more specific and may be shadowed by a broader existing rule",
                    Resolution = "Consider rule ordering - more specific rules should come before broader ones."
                });
            }
        }
    }

    private void CheckPortConflicts(
        FirewallRuleValidateRequest newRule,
        List<FirewallRuleViewExtended> existingRules,
        FirewallRuleValidateResponse response)
    {
        if (string.IsNullOrWhiteSpace(newRule.DestinationPort))
            return;

        var newPorts = ParsePorts(newRule.DestinationPort);
        if (newPorts.Count == 0)
            return;

        foreach (var existing in existingRules)
        {
            if (!MatchesInterface(newRule.Interface, existing.Interface))
                continue;

            if (!MatchesDirection(newRule.Direction, existing.Direction))
                continue;

            if (string.IsNullOrWhiteSpace(existing.DestinationPort))
                continue;

            var existingPorts = ParsePorts(existing.DestinationPort);
            var overlapping = newPorts.Intersect(existingPorts).ToList();

            if (overlapping.Count > 0 && !MatchesAction(newRule.Action, existing.Action))
            {
                var ruleDesc = FormatRuleDescription(existing);
                response.Conflicts.Add(new RuleConflict
                {
                    Type = "port_conflict",
                    Severity = "info",
                    ConflictingRuleId = existing.Id > 0 ? existing.Id : null,
                    ConflictingRuleDescription = ruleDesc,
                    Message = $"Port(s) {string.Join(", ", overlapping)} overlap with another rule using a different action",
                    Resolution = "Multiple rules on the same port with different actions may cause unexpected behavior."
                });
            }
        }
    }

    private static bool MatchesInterface(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            return true;
        return a.Equals(b, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesDirection(string? a, string? b)
    {
        var normA = NormalizeDirection(a);
        var normB = NormalizeDirection(b);
        return normA == normB;
    }

    private static bool MatchesAction(string? a, string? b)
    {
        var normA = NormalizeAction(a);
        var normB = NormalizeAction(b);
        return normA == normB;
    }

    private static bool MatchesProtocol(string? a, string? b)
    {
        var normA = NormalizeProtocol(a);
        var normB = NormalizeProtocol(b);

        if (normA == "any" || normB == "any")
            return true;

        if (normA == "tcp/udp")
            return normB == "tcp" || normB == "udp" || normB == "tcp/udp";

        if (normB == "tcp/udp")
            return normA == "tcp" || normA == "udp";

        return normA == normB;
    }

    private static bool MatchesEndpoint(
        string? type1, string? value1, string? port1,
        string? type2, string? value2, string? port2)
    {
        var normType1 = NormalizeAddressType(type1);
        var normType2 = NormalizeAddressType(type2);

        // Any matches anything
        if (normType1 == "any" || normType2 == "any")
        {
            return MatchesPorts(port1, port2);
        }

        if (normType1 != normType2)
            return false;

        var normVal1 = (value1 ?? "").Trim().ToLowerInvariant();
        var normVal2 = (value2 ?? "").Trim().ToLowerInvariant();

        if (normVal1 != normVal2)
            return false;

        return MatchesPorts(port1, port2);
    }

    private static bool MatchesPorts(string? port1, string? port2)
    {
        var p1 = (port1 ?? "").Trim().ToLowerInvariant();
        var p2 = (port2 ?? "").Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(p1) || string.IsNullOrEmpty(p2))
            return true;

        return p1 == p2;
    }

    private bool IsOverlapping(FirewallRuleValidateRequest newRule, FirewallRuleViewExtended existing)
    {
        if (!MatchesProtocol(newRule.Protocol, existing.Protocol))
            return false;

        // Check source overlap
        if (!IsEndpointOverlapping(
                newRule.SourceType, newRule.SourceValue, newRule.SourcePort,
                existing.SourceType, existing.SourceValue, existing.SourcePort))
            return false;

        // Check destination overlap
        if (!IsEndpointOverlapping(
                newRule.DestinationType, newRule.DestinationValue, newRule.DestinationPort,
                existing.DestinationType, existing.DestinationValue, existing.DestinationPort))
            return false;

        return true;
    }

    private static bool IsEndpointOverlapping(
        string? type1, string? value1, string? port1,
        string? type2, string? value2, string? port2)
    {
        var normType1 = NormalizeAddressType(type1);
        var normType2 = NormalizeAddressType(type2);

        // Any always overlaps
        if (normType1 == "any" || normType2 == "any")
            return true;

        // System types have special overlap logic
        if (normType1 == "system" || normType2 == "system")
        {
            // Simplified: consider overlapping if same system set
            var val1 = (value1 ?? "").Trim().ToLowerInvariant();
            var val2 = (value2 ?? "").Trim().ToLowerInvariant();
            if (val1 == val2)
                return ArePortsOverlapping(port1, port2);
            return false;
        }

        // For network/single types, check IP overlap
        if ((normType1 == "network" || normType1 == "single") &&
            (normType2 == "network" || normType2 == "single"))
        {
            var net1 = ParseNetwork(normType1, value1);
            var net2 = ParseNetwork(normType2, value2);

            if (net1 != null && net2 != null)
            {
                if (NetworksOverlap(net1.Value, net2.Value))
                    return ArePortsOverlapping(port1, port2);
            }
        }

        // Alias overlap is complex - assume overlap for safety
        if (normType1 == "alias" || normType2 == "alias")
        {
            var val1 = (value1 ?? "").Trim().ToLowerInvariant();
            var val2 = (value2 ?? "").Trim().ToLowerInvariant();
            if (val1 == val2)
                return ArePortsOverlapping(port1, port2);
        }

        return false;
    }

    private static bool ArePortsOverlapping(string? port1, string? port2)
    {
        var p1 = (port1 ?? "").Trim();
        var p2 = (port2 ?? "").Trim();

        // Empty port means all ports
        if (string.IsNullOrEmpty(p1) || string.IsNullOrEmpty(p2))
            return true;

        var ports1 = ParsePorts(p1);
        var ports2 = ParsePorts(p2);

        return ports1.Intersect(ports2).Any();
    }

    private static HashSet<int> ParsePorts(string portSpec)
    {
        var ports = new HashSet<int>();

        if (string.IsNullOrWhiteSpace(portSpec))
            return ports;

        foreach (var part in portSpec.Split(','))
        {
            var trimmed = part.Trim();
            if (trimmed.Contains('-'))
            {
                var range = trimmed.Split('-');
                if (range.Length == 2 &&
                    int.TryParse(range[0].Trim(), out var start) &&
                    int.TryParse(range[1].Trim(), out var end))
                {
                    for (int i = start; i <= end && i <= 65535; i++)
                        ports.Add(i);
                }
            }
            else if (int.TryParse(trimmed, out var port))
            {
                ports.Add(port);
            }
        }

        return ports;
    }

    private static (IPAddress Address, int PrefixLength)? ParseNetwork(string? type, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normType = NormalizeAddressType(type);

        try
        {
            if (normType == "single")
            {
                if (IPAddress.TryParse(value.Trim(), out var ip))
                {
                    var prefix = ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
                    return (ip, prefix);
                }
            }
            else if (normType == "network")
            {
                var parts = value.Trim().Split('/');
                if (parts.Length == 2 &&
                    IPAddress.TryParse(parts[0], out var ip) &&
                    int.TryParse(parts[1], out var prefix))
                {
                    return (ip, prefix);
                }
                else if (parts.Length == 1 && IPAddress.TryParse(parts[0], out var singleIp))
                {
                    var defaultPrefix = singleIp.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
                    return (singleIp, defaultPrefix);
                }
            }
        }
        catch
        {
            // Ignore parse errors
        }

        return null;
    }

    private static bool NetworksOverlap((IPAddress Address, int PrefixLength) net1, (IPAddress Address, int PrefixLength) net2)
    {
        if (net1.Address.AddressFamily != net2.Address.AddressFamily)
            return false;

        var bytes1 = net1.Address.GetAddressBytes();
        var bytes2 = net2.Address.GetAddressBytes();
        var minPrefix = Math.Min(net1.PrefixLength, net2.PrefixLength);

        int fullBytes = minPrefix / 8;
        int remainingBits = minPrefix % 8;

        for (int i = 0; i < fullBytes; i++)
        {
            if (bytes1[i] != bytes2[i])
                return false;
        }

        if (remainingBits > 0 && fullBytes < bytes1.Length)
        {
            int mask = (byte)(0xFF << (8 - remainingBits));
            if ((bytes1[fullBytes] & mask) != (bytes2[fullBytes] & mask))
                return false;
        }

        return true;
    }

    private static bool IsNetworkNarrower(
        (IPAddress Address, int PrefixLength)? newNet,
        (IPAddress Address, int PrefixLength)? existingNet)
    {
        if (newNet == null || existingNet == null)
            return false;

        if (newNet.Value.Address.AddressFamily != existingNet.Value.Address.AddressFamily)
            return false;

        // Narrower means longer prefix (more specific)
        if (newNet.Value.PrefixLength <= existingNet.Value.PrefixLength)
            return false;

        // Check if new network is contained within existing
        return NetworksOverlap(newNet.Value, existingNet.Value);
    }

    private bool IsExactMatch(FirewallRuleValidateRequest newRule, FirewallRuleViewExtended existing)
    {
        return MatchesProtocol(newRule.Protocol, existing.Protocol) &&
               MatchesEndpoint(newRule.SourceType, newRule.SourceValue, newRule.SourcePort,
                   existing.SourceType, existing.SourceValue, existing.SourcePort) &&
               MatchesEndpoint(newRule.DestinationType, newRule.DestinationValue, newRule.DestinationPort,
                   existing.DestinationType, existing.DestinationValue, existing.DestinationPort);
    }

    private static string FormatRuleDescription(FirewallRuleViewExtended rule)
    {
        var typeLabel = rule.RuleType switch
        {
            "system" => "System",
            "managed" => "Managed",
            _ => "User"
        };

        if (!string.IsNullOrWhiteSpace(rule.Description))
            return $"{typeLabel}: {rule.Description}";

        var action = rule.Action.ToUpperInvariant();
        var protocol = rule.Protocol.ToUpperInvariant();
        var dest = string.IsNullOrWhiteSpace(rule.DestinationPort) ? "any port" : $"port {rule.DestinationPort}";

        return $"{typeLabel}: {action} {protocol} to {dest}";
    }

    private static string NormalizeDirection(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "in";
        return value.Trim().ToLowerInvariant() switch
        {
            "in" => "in",
            "out" => "out",
            "forward" => "forward",
            _ => "in"
        };
    }

    private static string NormalizeAction(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "pass";
        return value.Trim().ToLowerInvariant() switch
        {
            "pass" => "pass",
            "block" => "block",
            "reject" => "reject",
            _ => "pass"
        };
    }

    private static string NormalizeProtocol(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "any";
        return value.Trim().ToLowerInvariant() switch
        {
            "tcp" => "tcp",
            "udp" => "udp",
            "tcp/udp" => "tcp/udp",
            "icmp" => "icmp",
            "any" => "any",
            _ => "any"
        };
    }

    private static string NormalizeAddressType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "any";
        return value.Trim().ToLowerInvariant() switch
        {
            "any" => "any",
            "single" => "single",
            "network" => "network",
            "alias" => "alias",
            "system" => "system",
            _ => "any"
        };
    }
}
