using System.Text;
using System.Text.RegularExpressions;
using Monolith.FireWall.Common.Services;
using Monolith.FireWall.Core.Models;
using Monolith.FireWall.Core.Services.Platform;
using Monolith.FireWall.Platform.Models;

namespace Monolith.FireWall.Core.Services.Firewall;

public sealed class FirewallApplyManager
{
    private static readonly Regex AliasNameSanitizer = new("[^a-z0-9_]+", RegexOptions.Compiled);
    private readonly FirewallAliasManager _aliasManager;
    private readonly FirewallNatManager _natManager;
    private readonly FirewallNatSettingsManager _natSettingsManager;
    private readonly FirewallRulesManager _rulesManager;
    private readonly FirewallDefaultsManager _defaultsManager;
    private readonly InterfaceAssignmentStore _interfaceStore;
    private readonly PlatformCommandRunner _commandRunner;
    private readonly LoggingManager _loggingManager;

    public FirewallApplyManager(
        FirewallAliasManager aliasManager,
        FirewallNatManager natManager,
        FirewallNatSettingsManager natSettingsManager,
        FirewallRulesManager rulesManager,
        FirewallDefaultsManager defaultsManager,
        InterfaceAssignmentStore interfaceStore,
        PlatformCommandRunner commandRunner)
    {
        _aliasManager = aliasManager;
        _natManager = natManager;
        _natSettingsManager = natSettingsManager;
        _rulesManager = rulesManager;
        _defaultsManager = defaultsManager;
        _interfaceStore = interfaceStore;
        _commandRunner = commandRunner;
        _loggingManager = LoggingManager.Instance;
    }

    public async Task<FirewallApplyResult> ApplyAsync(CancellationToken cancellationToken)
    {
        var buildResult = await BuildConfigAsync(cancellationToken);
        if (!buildResult.Success)
        {
            return new FirewallApplyResult
            {
                Success = false,
                Error = buildResult.Error,
                Warnings = buildResult.Warnings
            };
        }

        var configPath = buildResult.ConfigPath;
        if (string.IsNullOrWhiteSpace(configPath))
        {
            return new FirewallApplyResult
            {
                Success = false,
                Error = "Config path was not generated",
                Warnings = buildResult.Warnings
            };
        }

        if (!_commandRunner.CommandExists("nft"))
        {
            return new FirewallApplyResult
            {
                Success = false,
                Error = "nft command not found",
                Warnings = buildResult.Warnings,
                ConfigPath = configPath
            };
        }

        var cleanupResult = await RemoveManagedTablesAsync(cancellationToken);
        if (!cleanupResult.Success)
        {
            return new FirewallApplyResult
            {
                Success = false,
                Error = cleanupResult.Error ?? "Failed to remove existing firewall tables",
                Warnings = buildResult.Warnings,
                ConfigPath = configPath
            };
        }

        var result = await _commandRunner.RunAsync(new PlatformCommand
        {
            FileName = "nft",
            Arguments = $"-f {configPath}",
            UseSudo = true,
            TimeoutMs = 15000
        }, cancellationToken);

        if (result.ExitCode != 0)
        {
            return new FirewallApplyResult
            {
                Success = false,
                Error = string.IsNullOrWhiteSpace(result.StdErr)
                    ? $"nft exited with code {result.ExitCode}"
                    : result.StdErr.Trim(),
                Warnings = buildResult.Warnings,
                ConfigPath = configPath
            };
        }

        await _loggingManager.LogSecurityAsync(
            "Firewall",
            "Info",
            "FirewallApply",
            "Applied firewall configuration",
            details: new Dictionary<string, object>
            {
                ["configPath"] = configPath
            });

        return new FirewallApplyResult
        {
            Success = true,
            ConfigPath = configPath,
            Warnings = buildResult.Warnings
        };
    }

    private async Task<(bool Success, string? Error)> RemoveManagedTablesAsync(CancellationToken cancellationToken)
    {
        var tables = new[]
        {
            ("inet", "monolith_filter"),
            ("ip", "monolith_nat"),
            ("ip6", "monolith_nat")
        };

        foreach (var (family, name) in tables)
        {
            var listResult = await _commandRunner.RunAsync(new PlatformCommand
            {
                FileName = "nft",
                Arguments = $"list table {family} {name}",
                UseSudo = true,
                TimeoutMs = 5000
            }, cancellationToken);

            if (listResult.ExitCode != 0)
            {
                continue;
            }

            var deleteResult = await _commandRunner.RunAsync(new PlatformCommand
            {
                FileName = "nft",
                Arguments = $"delete table {family} {name}",
                UseSudo = true,
                TimeoutMs = 5000
            }, cancellationToken);

            if (deleteResult.ExitCode != 0)
            {
                return (false, deleteResult.StdErr?.Trim() ?? $"Failed to delete table {family} {name}");
            }
        }

        return (true, null);
    }

    public async Task<FirewallApplyResult> BuildConfigAsync(CancellationToken cancellationToken)
    {
        var aliases = await _aliasManager.ListAliasesAsync();
        var natRules = await _natManager.ListRulesAsync();
        var natSettings = await _natSettingsManager.GetAsync();
        var defaults = await _defaultsManager.GetAsync();
        var effectiveRules = await _rulesManager.GetEffectiveRulesAsync(defaults);
        var assignments = await _interfaceStore.GetAssignmentsAsync();

        var warnings = new List<string>();
        var builder = new StringBuilder();

        builder.AppendLine("# Generated by Monolith FireWall");
        builder.AppendLine($"# {DateTime.UtcNow:O}");
        builder.AppendLine("# Managed tables will be replaced by apply step");

        var ipv4Rules = natRules.Where(r => r.Enabled && (r.AddressFamily == "ipv4" || r.AddressFamily == "dual")).ToList();
        var ipv6Rules = natRules.Where(r => r.Enabled && (r.AddressFamily == "ipv6" || r.AddressFamily == "dual")).ToList();

        if (ipv4Rules.Count == 0 && ipv6Rules.Count == 0)
        {
            warnings.Add("No enabled NAT rules found");
        }

        if (ipv4Rules.Count > 0)
        {
            AppendNatTable(builder, "ip", ipv4Rules, aliases, natSettings, warnings);
        }

        if (ipv6Rules.Count > 0)
        {
            AppendNatTable(builder, "ip6", ipv6Rules, aliases, natSettings, warnings);
        }

        AppendFilterTable(builder, effectiveRules, defaults, assignments, aliases, warnings);

        var configPath = "/var/lib/monolith-firewall/firewall.nft";
        var dir = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await File.WriteAllTextAsync(configPath, builder.ToString(), cancellationToken);

        return new FirewallApplyResult
        {
            Success = true,
            ConfigPath = configPath,
            Warnings = warnings
        };
    }

    private void AppendNatTable(
        StringBuilder builder,
        string family,
        List<FirewallNatRuleView> rules,
        List<FirewallAliasView> aliases,
        FirewallNatSettingsView natSettings,
        List<string> warnings)
    {
        builder.AppendLine($"table {family} monolith_nat {{");

        var usedAliases = CollectUsedAliases(rules, aliases, family);
        foreach (var aliasSet in usedAliases.AddressSets)
        {
            builder.AppendLine($"  set {aliasSet.Name} {{");
            builder.AppendLine($"    type {aliasSet.Type};");
            builder.AppendLine("    elements = { " + string.Join(", ", aliasSet.Values) + " }");
            builder.AppendLine("  }");
        }

        foreach (var aliasSet in usedAliases.PortSets)
        {
            builder.AppendLine($"  set {aliasSet.Name} {{");
            builder.AppendLine($"    type {aliasSet.Type};");
            builder.AppendLine("    elements = { " + string.Join(", ", aliasSet.Values) + " }");
            builder.AppendLine("  }");
        }

        builder.AppendLine("  chain prerouting {");
        builder.AppendLine("    type nat hook prerouting priority -100; policy accept;");

        foreach (var rule in rules.OrderBy(r => r.RuleNumber))
        {
            var ruleLines = BuildNatRule(rule, usedAliases, family, warnings);
            foreach (var line in ruleLines)
            {
                builder.AppendLine("    " + line);
            }
        }

        builder.AppendLine("  }");
        builder.AppendLine("  chain output {");
        builder.AppendLine("    type nat hook output priority -100; policy accept;");
        builder.AppendLine("  }");
        builder.AppendLine("}");

        if (natSettings.ReflectionEnabled && natSettings.ReflectionMode == "nat")
        {
            warnings.Add("NAT reflection is enabled but not fully implemented in nftables output yet");
        }
    }

    private void AppendFilterTable(
        StringBuilder builder,
        List<FirewallRuleView> rules,
        FirewallDefaultsView defaults,
        List<InterfaceAssignmentEntity> assignments,
        List<FirewallAliasView> aliases,
        List<string> warnings)
    {
        builder.AppendLine("table inet monolith_filter {");

        var aliasSets = CollectRuleAliases(rules, aliases);
        foreach (var aliasSet in aliasSets.AddressSets)
        {
            builder.AppendLine($"  set {aliasSet.Name} {{");
            builder.AppendLine($"    type {aliasSet.Type};");
            builder.AppendLine("    elements = { " + string.Join(", ", aliasSet.Values) + " }");
            builder.AppendLine("  }");
        }

        foreach (var portSet in aliasSets.PortSets)
        {
            builder.AppendLine($"  set {portSet.Name} {{");
            builder.AppendLine($"    type {portSet.Type};");
            builder.AppendLine("    elements = { " + string.Join(", ", portSet.Values) + " }");
            builder.AppendLine("  }");
        }

        foreach (var systemSet in aliasSets.SystemSets)
        {
            builder.AppendLine($"  set {systemSet.Name} {{");
            builder.AppendLine($"    type {systemSet.Type};");
            builder.AppendLine("    elements = { " + string.Join(", ", systemSet.Values) + " }");
            builder.AppendLine("  }");
        }

        builder.AppendLine("  chain input {");
        builder.AppendLine("    type filter hook input priority 0; policy drop;");

        foreach (var assignment in assignments)
        {
            builder.AppendLine($"    iifname \"{assignment.InterfaceName}\" jump input_{assignment.InterfaceName}");
        }

        builder.AppendLine("  }");

        builder.AppendLine("  chain forward {");
        builder.AppendLine("    type filter hook forward priority 0; policy drop;");
        foreach (var assignment in assignments)
        {
            builder.AppendLine($"    iifname \"{assignment.InterfaceName}\" jump forward_{assignment.InterfaceName}");
        }
        builder.AppendLine("  }");

        builder.AppendLine("  chain output {");
        builder.AppendLine("    type filter hook output priority 0; policy accept;");
        foreach (var rule in rules.Where(r => r.Direction == "out" && r.Enabled))
        {
            var lines = BuildFilterRule(rule, aliasSets, warnings);
            foreach (var line in lines)
            {
                builder.AppendLine("    " + line);
            }
        }
        builder.AppendLine("  }");

        foreach (var assignment in assignments)
        {
            builder.AppendLine($"  chain input_{assignment.InterfaceName} {{");
            var interfaceRules = rules
                .Where(r => r.Interface.Equals(assignment.InterfaceName, StringComparison.OrdinalIgnoreCase))
                .Where(r => r.Direction == "in" && r.Enabled)
                .OrderBy(r => r.IsSystem ? 0 : 1)
                .ThenBy(r => r.RuleNumber)
                .ToList();

            foreach (var rule in interfaceRules)
            {
                var lines = BuildFilterRule(rule, aliasSets, warnings);
                foreach (var line in lines)
                {
                    builder.AppendLine("    " + line);
                }
            }

            var defaultAction = GetDefaultAction(assignment.Role, defaults);
            builder.AppendLine($"    {BuildTerminalAction(defaultAction)}");
            builder.AppendLine("  }");

            builder.AppendLine($"  chain forward_{assignment.InterfaceName} {{");
            var forwardRules = rules
                .Where(r => r.Interface.Equals(assignment.InterfaceName, StringComparison.OrdinalIgnoreCase))
                .Where(r => r.Direction == "forward" && r.Enabled)
                .OrderBy(r => r.IsSystem ? 0 : 1)
                .ThenBy(r => r.RuleNumber)
                .ToList();

            foreach (var rule in forwardRules)
            {
                var lines = BuildFilterRule(rule, aliasSets, warnings);
                foreach (var line in lines)
                {
                    builder.AppendLine("    " + line);
                }
            }

            builder.AppendLine($"    {BuildTerminalAction(defaultAction)}");
            builder.AppendLine("  }");
        }

        builder.AppendLine("}");
    }

    private static string GetDefaultAction(InterfaceRole role, FirewallDefaultsView defaults)
    {
        return role switch
        {
            InterfaceRole.Lan => defaults.LanDefaultAction,
            InterfaceRole.Wan => defaults.WanDefaultAction,
            InterfaceRole.Opt => defaults.OptDefaultAction,
            _ => "block"
        };
    }

    private static string BuildTerminalAction(string action)
    {
        return action switch
        {
            "pass" => "accept",
            "reject" => "reject with icmpx type admin-prohibited",
            _ => "drop"
        };
    }

    private List<string> BuildFilterRule(FirewallRuleView rule, RuleAliasSetCollection aliasSets, List<string> warnings)
    {
        var lines = new List<string>();

        var baseMatch = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(rule.Interface))
        {
            if (rule.Direction == "out")
            {
                baseMatch.Append($"oifname \"{rule.Interface}\"");
            }
            else if (rule.Direction == "forward")
            {
                baseMatch.Append($"iifname \"{rule.Interface}\"");
            }
        }

        var addressQualifier = rule.AddressFamily == "ipv6" ? "ip6" : "ip";

        var sourceMatch = BuildRuleAddressMatch(addressQualifier, "saddr", rule.SourceType, rule.SourceValue, aliasSets, warnings, rule);
        if (!string.IsNullOrWhiteSpace(sourceMatch))
        {
            AppendMatch(baseMatch, sourceMatch);
        }

        var destinationMatch = BuildRuleAddressMatch(addressQualifier, "daddr", rule.DestinationType, rule.DestinationValue, aliasSets, warnings, rule);
        if (!string.IsNullOrWhiteSpace(destinationMatch))
        {
            AppendMatch(baseMatch, destinationMatch);
        }

        var protocols = ExpandProtocols(rule.Protocol);
        foreach (var protocol in protocols)
        {
            var match = new StringBuilder(baseMatch.ToString());
            if (protocol != "any")
            {
                AppendMatch(match, protocol);
            }

            var sourcePortMatch = BuildPortExpression(protocol, rule.SourcePort, aliasSets, isSource: true, warnings: warnings, rule: rule);
            if (!string.IsNullOrWhiteSpace(sourcePortMatch))
            {
                AppendMatch(match, sourcePortMatch);
            }

            var destinationPortMatch = BuildPortExpression(protocol, rule.DestinationPort, aliasSets, isSource: false, warnings: warnings, rule: rule);
            if (!string.IsNullOrWhiteSpace(destinationPortMatch))
            {
                AppendMatch(match, destinationPortMatch);
            }

            if (rule.LogEnabled)
            {
                lines.Add($"{match} log prefix \"MF rule\"");
            }

            lines.Add($"{match} {MapAction(rule.Action)}");
        }

        return lines;
    }

    private static void AppendMatch(StringBuilder builder, string token)
    {
        if (builder.Length > 0)
        {
            builder.Append(' ');
        }
        builder.Append(token);
    }

    private static string MapAction(string action)
    {
        return action switch
        {
            "pass" => "accept",
            "reject" => "reject with icmpx type admin-prohibited",
            _ => "drop"
        };
    }

    private static string BuildRuleAddressMatch(
        string qualifier,
        string direction,
        string type,
        string? value,
        RuleAliasSetCollection aliasSets,
        List<string> warnings,
        FirewallRuleView rule)
    {
        if (type == "any")
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            warnings.Add($"Rule on {rule.Interface}: {direction} value is required");
            return string.Empty;
        }

        if (type == "alias")
        {
            if (!aliasSets.TryGetAddressSet(value, qualifier, out var setName))
            {
                warnings.Add($"Rule on {rule.Interface}: alias '{value}' not found");
                return string.Empty;
            }

            return $"{qualifier} {direction} @{setName}";
        }

        if (type == "system")
        {
            if (!aliasSets.TryGetSystemSet(value, qualifier, out var systemSet))
            {
                warnings.Add($"Rule on {rule.Interface}: system set '{value}' not found");
                return string.Empty;
            }

            return $"{qualifier} {direction} @{systemSet}";
        }

        return $"{qualifier} {direction} {value}";
    }

    private static string BuildPortExpression(
        string protocol,
        string? value,
        RuleAliasSetCollection aliasSets,
        bool isSource,
        List<string> warnings,
        FirewallRuleView rule)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (protocol == "any" || protocol == "icmp")
        {
            return string.Empty;
        }

        if (aliasSets.TryGetPortSet(value, out var setName))
        {
            return $"{protocol} {(isSource ? "sport" : "dport")} @{setName}";
        }

        var portExpression = value.Contains(',')
            ? "{" + value + "}"
            : value;

        return $"{protocol} {(isSource ? "sport" : "dport")} {portExpression}";
    }

    private RuleAliasSetCollection CollectRuleAliases(List<FirewallRuleView> rules, List<FirewallAliasView> aliases)
    {
        var aliasLookup = aliases.ToDictionary(a => a.Name, StringComparer.OrdinalIgnoreCase);
        var addressSets = new List<RuleAliasSetDefinition>();
        var portSets = new List<RuleAliasSetDefinition>();
        var systemSets = new List<RuleAliasSetDefinition>();

        var requiresRfc1918 = rules.Any(r => r.SourceType == "system" && string.Equals(r.SourceValue, "rfc1918", StringComparison.OrdinalIgnoreCase));
        var requiresReserved = rules.Any(r => r.SourceType == "system" && string.Equals(r.SourceValue, "iana_reserved", StringComparison.OrdinalIgnoreCase));
        var requiresRfc4193 = rules.Any(r => r.SourceType == "system" && string.Equals(r.SourceValue, "rfc4193", StringComparison.OrdinalIgnoreCase));
        var requiresReservedV6 = rules.Any(r => r.SourceType == "system" && string.Equals(r.SourceValue, "iana_reserved_v6", StringComparison.OrdinalIgnoreCase));

        if (requiresRfc1918)
        {
            systemSets.Add(new RuleAliasSetDefinition
            {
                Name = "sys_rfc1918_v4",
                Type = "ipv4_addr",
                Values = new List<string> { "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16" },
                Family = "ip"
            });
        }

        if (requiresReserved)
        {
            systemSets.Add(new RuleAliasSetDefinition
            {
                Name = "sys_reserved_v4",
                Type = "ipv4_addr",
                Values = new List<string>
                {
                    "0.0.0.0/8",
                    "100.64.0.0/10",
                    "127.0.0.0/8",
                    "169.254.0.0/16",
                    "192.0.0.0/24",
                    "192.0.2.0/24",
                    "198.18.0.0/15",
                    "198.51.100.0/24",
                    "203.0.113.0/24",
                    "224.0.0.0/4",
                    "240.0.0.0/4"
                },
                Family = "ip"
            });
        }

        if (requiresRfc4193)
        {
            systemSets.Add(new RuleAliasSetDefinition
            {
                Name = "sys_rfc4193_v6",
                Type = "ipv6_addr",
                Values = new List<string> { "fc00::/7" },
                Family = "ip6"
            });
        }

        if (requiresReservedV6)
        {
            systemSets.Add(new RuleAliasSetDefinition
            {
                Name = "sys_reserved_v6",
                Type = "ipv6_addr",
                Values = new List<string>
                {
                    "::/128",
                    "::1/128",
                    "::ffff:0:0/96",
                    "100::/64",
                    "2001:db8::/32",
                    "2001:10::/28",
                    "fe80::/10",
                    "ff00::/8"
                },
                Family = "ip6"
            });
        }

        foreach (var rule in rules)
        {
            CollectRuleAlias(rule.SourceType, rule.SourceValue, rule.AddressFamily, aliasLookup, addressSets);
            CollectRuleAlias(rule.DestinationType, rule.DestinationValue, rule.AddressFamily, aliasLookup, addressSets);
            CollectPortAlias(rule.SourcePort, aliasLookup, portSets);
            CollectPortAlias(rule.DestinationPort, aliasLookup, portSets);
        }

        return new RuleAliasSetCollection(addressSets, portSets, systemSets);
    }

    private void CollectRuleAlias(
        string type,
        string? value,
        string family,
        Dictionary<string, FirewallAliasView> aliasLookup,
        List<RuleAliasSetDefinition> addressSets)
    {
        if (type != "alias" || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!aliasLookup.TryGetValue(value.Trim(), out var alias))
        {
            return;
        }

        var familyKey = family == "ipv6" ? "ip6" : "ip";
        if (family == "dual")
        {
            AddAddressSet(alias, "ip", addressSets);
            AddAddressSet(alias, "ip6", addressSets);
            return;
        }

        AddAddressSet(alias, familyKey, addressSets);
    }

    private void AddAddressSet(FirewallAliasView alias, string family, List<RuleAliasSetDefinition> addressSets)
    {
        var suffix = family == "ip6" ? "_v6" : "_v4";
        var setName = "alias_" + AliasNameSanitizer.Replace(alias.Name.ToLowerInvariant(), "_") + suffix;
        if (addressSets.Any(s => s.Name == setName))
        {
            return;
        }

        addressSets.Add(new RuleAliasSetDefinition
        {
            Name = setName,
            Type = family == "ip6" ? "ipv6_addr" : "ipv4_addr",
            Values = alias.Content,
            Family = family
        });
    }

    private void CollectPortAlias(string? value, Dictionary<string, FirewallAliasView> aliasLookup, List<RuleAliasSetDefinition> portSets)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!aliasLookup.TryGetValue(value.Trim(), out var alias) || alias.Type != "port")
        {
            return;
        }

        var setName = "alias_" + AliasNameSanitizer.Replace(alias.Name.ToLowerInvariant(), "_") + "_ports";
        if (portSets.Any(s => s.Name == setName))
        {
            return;
        }

        portSets.Add(new RuleAliasSetDefinition
        {
            Name = setName,
            Type = "inet_service",
            Values = alias.Content,
            Family = "ports"
        });
    }

    private sealed record RuleAliasSetDefinition
    {
        public string Name { get; init; } = string.Empty;
        public string Type { get; init; } = string.Empty;
        public List<string> Values { get; init; } = new();
        public string Family { get; init; } = string.Empty;
    }

    private sealed class RuleAliasSetCollection
    {
        public RuleAliasSetCollection(
            List<RuleAliasSetDefinition> addressSets,
            List<RuleAliasSetDefinition> portSets,
            List<RuleAliasSetDefinition> systemSets)
        {
            AddressSets = addressSets;
            PortSets = portSets;
            SystemSets = systemSets;
        }

        public List<RuleAliasSetDefinition> AddressSets { get; }
        public List<RuleAliasSetDefinition> PortSets { get; }
        public List<RuleAliasSetDefinition> SystemSets { get; }

        public bool TryGetAddressSet(string name, string qualifier, out string setName)
        {
            var suffix = qualifier == "ip6" ? "_v6" : "_v4";
            var target = "alias_" + AliasNameSanitizer.Replace(name.ToLowerInvariant(), "_") + suffix;
            var entry = AddressSets.FirstOrDefault(s => s.Name.Equals(target, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
            {
                setName = string.Empty;
                return false;
            }

            setName = entry.Name;
            return true;
        }

        public bool TryGetPortSet(string name, out string setName)
        {
            var target = "alias_" + AliasNameSanitizer.Replace(name.ToLowerInvariant(), "_") + "_ports";
            var entry = PortSets.FirstOrDefault(s => s.Name.Equals(target, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
            {
                setName = string.Empty;
                return false;
            }

            setName = entry.Name;
            return true;
        }

        public bool TryGetSystemSet(string name, string qualifier, out string setName)
        {
            var target = name.Equals("rfc1918", StringComparison.OrdinalIgnoreCase)
                ? "sys_rfc1918_v4"
                : name.Equals("iana_reserved", StringComparison.OrdinalIgnoreCase)
                    ? "sys_reserved_v4"
                    : name.Equals("rfc4193", StringComparison.OrdinalIgnoreCase)
                        ? "sys_rfc4193_v6"
                        : name.Equals("iana_reserved_v6", StringComparison.OrdinalIgnoreCase)
                            ? "sys_reserved_v6"
                            : string.Empty;

            if (string.IsNullOrWhiteSpace(target))
            {
                setName = string.Empty;
                return false;
            }

            var entry = SystemSets.FirstOrDefault(s => s.Name.Equals(target, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
            {
                setName = string.Empty;
                return false;
            }

            if (qualifier == "ip6" && target.EndsWith("_v4", StringComparison.OrdinalIgnoreCase))
            {
                setName = string.Empty;
                return false;
            }

            setName = entry.Name;
            return true;
        }
    }

    private List<string> BuildNatRule(
        FirewallNatRuleView rule,
        AliasSetCollection aliasSets,
        string family,
        List<string> warnings)
    {
        var lines = new List<string>();

        if (string.IsNullOrWhiteSpace(rule.Interface))
        {
            warnings.Add($"Skipping NAT rule #{rule.RuleNumber} (missing interface)");
            return lines;
        }

        if (string.IsNullOrWhiteSpace(rule.RedirectTargetIp))
        {
            warnings.Add($"Skipping NAT rule #{rule.RuleNumber} (missing redirect target)");
            return lines;
        }

        var baseMatch = new StringBuilder();
        baseMatch.Append($"iifname \"{rule.Interface}\"");

        var addressQualifier = family == "ip6" ? "ip6" : "ip";

        var sourceMatch = BuildAddressMatch(addressQualifier, "saddr", rule.SourceType, rule.SourceValue, aliasSets, warnings, rule.RuleNumber);
        if (!string.IsNullOrWhiteSpace(sourceMatch))
        {
            baseMatch.Append(' ').Append(sourceMatch);
        }

        var destinationMatch = BuildAddressMatch(addressQualifier, "daddr", rule.DestinationType, rule.DestinationValue, aliasSets, warnings, rule.RuleNumber);
        if (!string.IsNullOrWhiteSpace(destinationMatch))
        {
            baseMatch.Append(' ').Append(destinationMatch);
        }

        var protocols = ExpandProtocols(rule.Protocol);
        foreach (var protocol in protocols)
        {
            var match = new StringBuilder(baseMatch.ToString());
            if (protocol != "any")
            {
                match.Append($" {protocol}");
            }

            var sourcePortMatch = BuildSourcePortMatch(protocol, rule.SourcePort, aliasSets, warnings, rule.RuleNumber);
            if (!string.IsNullOrWhiteSpace(sourcePortMatch))
            {
                match.Append(' ').Append(sourcePortMatch);
            }

            var destinationPortMatch = BuildPortMatch(protocol, rule.DestinationPort, aliasSets, warnings, rule.RuleNumber);
            if (!string.IsNullOrWhiteSpace(destinationPortMatch))
            {
                match.Append(' ').Append(destinationPortMatch);
            }

            var redirect = new StringBuilder();
            redirect.Append(match);
            redirect.Append(" dnat to ");
            redirect.Append(rule.RedirectTargetIp);
            if (!string.IsNullOrWhiteSpace(rule.RedirectTargetPort))
            {
                redirect.Append(':').Append(rule.RedirectTargetPort);
            }

            if (!string.IsNullOrWhiteSpace(rule.Description))
            {
                redirect.Append(" comment \"").Append(rule.Description.Replace("\"", "")).Append("\"");
            }

            lines.Add(redirect.ToString());
        }

        return lines;
    }

    private static string BuildAddressMatch(
        string qualifier,
        string direction,
        string type,
        string? value,
        AliasSetCollection aliasSets,
        List<string> warnings,
        int ruleNumber)
    {
        if (type == "any")
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            warnings.Add($"NAT rule #{ruleNumber}: {direction} value is required");
            return string.Empty;
        }

        if (type == "alias")
        {
            if (!aliasSets.TryGetAddressSet(value, out var setName))
            {
                warnings.Add($"NAT rule #{ruleNumber}: alias '{value}' not found");
                return string.Empty;
            }

            return $"{qualifier} {direction} @{setName}";
        }

        return $"{qualifier} {direction} {value}";
    }

    private static string BuildPortMatch(
        string protocol,
        string? value,
        AliasSetCollection aliasSets,
        List<string> warnings,
        int ruleNumber)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (protocol == "any" || protocol == "icmp")
        {
            return string.Empty;
        }

        if (aliasSets.TryGetPortSet(value, out var setName))
        {
            return $"{protocol} dport @{setName}";
        }

        return $"{protocol} dport {value}";
    }

    private static string BuildSourcePortMatch(
        string protocol,
        string? value,
        AliasSetCollection aliasSets,
        List<string> warnings,
        int ruleNumber)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (protocol == "any" || protocol == "icmp")
        {
            return string.Empty;
        }

        if (aliasSets.TryGetPortSet(value, out var setName))
        {
            return $"{protocol} sport @{setName}";
        }

        return $"{protocol} sport {value}";
    }

    private static List<string> ExpandProtocols(string protocol)
    {
        if (string.IsNullOrWhiteSpace(protocol) || protocol == "any")
        {
            return new List<string> { "any" };
        }

        if (protocol == "tcp/udp")
        {
            return new List<string> { "tcp", "udp" };
        }

        return new List<string> { protocol };
    }

    private AliasSetCollection CollectUsedAliases(
        List<FirewallNatRuleView> rules,
        List<FirewallAliasView> aliases,
        string family)
    {
        var aliasLookup = aliases.ToDictionary(a => a.Name, StringComparer.OrdinalIgnoreCase);
        var addressSets = new List<AliasSetDefinition>();
        var portSets = new List<AliasSetDefinition>();

        foreach (var rule in rules)
        {
            CollectAlias(rule.SourceType, rule.SourceValue, aliasLookup, addressSets, portSets, family);
            CollectAlias(rule.DestinationType, rule.DestinationValue, aliasLookup, addressSets, portSets, family);
            CollectAlias("port", rule.SourcePort, aliasLookup, addressSets, portSets, family);
            CollectAlias("port", rule.DestinationPort, aliasLookup, addressSets, portSets, family);
        }

        return new AliasSetCollection(addressSets, portSets);
    }

    private void CollectAlias(
        string? type,
        string? value,
        Dictionary<string, FirewallAliasView> aliasLookup,
        List<AliasSetDefinition> addressSets,
        List<AliasSetDefinition> portSets,
        string family)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!aliasLookup.TryGetValue(value.Trim(), out var alias))
        {
            return;
        }

        var setName = "alias_" + AliasNameSanitizer.Replace(alias.Name.ToLowerInvariant(), "_");

        if (alias.Type == "port" || type == "port")
        {
            if (portSets.Any(s => s.Name == setName))
            {
                return;
            }

            portSets.Add(new AliasSetDefinition
            {
                Name = setName,
                Type = "inet_service",
                Values = alias.Content
            });
            return;
        }

        var addressType = family == "ip6" ? "ipv6_addr" : "ipv4_addr";
        if (addressSets.Any(s => s.Name == setName))
        {
            return;
        }

        addressSets.Add(new AliasSetDefinition
        {
            Name = setName,
            Type = addressType,
            Values = alias.Content
        });
    }

    private sealed record AliasSetDefinition
    {
        public string Name { get; init; } = string.Empty;
        public string Type { get; init; } = string.Empty;
        public List<string> Values { get; init; } = new();
    }

    private sealed class AliasSetCollection
    {
        public AliasSetCollection(List<AliasSetDefinition> addressSets, List<AliasSetDefinition> portSets)
        {
            AddressSets = addressSets;
            PortSets = portSets;
        }

        public List<AliasSetDefinition> AddressSets { get; }
        public List<AliasSetDefinition> PortSets { get; }

        public bool TryGetAddressSet(string name, out string setName)
        {
            var entry = AddressSets.FirstOrDefault(s => s.Name.Equals("alias_" + AliasNameSanitizer.Replace(name.ToLowerInvariant(), "_"), StringComparison.OrdinalIgnoreCase));
            if (entry == null)
            {
                setName = string.Empty;
                return false;
            }

            setName = entry.Name;
            return true;
        }

        public bool TryGetPortSet(string name, out string setName)
        {
            var entry = PortSets.FirstOrDefault(s => s.Name.Equals("alias_" + AliasNameSanitizer.Replace(name.ToLowerInvariant(), "_"), StringComparison.OrdinalIgnoreCase));
            if (entry == null)
            {
                setName = string.Empty;
                return false;
            }

            setName = entry.Name;
            return true;
        }
    }
}

public sealed class FirewallApplyResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? ConfigPath { get; set; }
    public List<string> Warnings { get; set; } = new();
}
