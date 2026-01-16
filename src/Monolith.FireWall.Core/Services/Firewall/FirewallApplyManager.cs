using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using Monolith.FireWall.Platform.Validation;
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
    private readonly FirewallInterfaceSettingsManager _interfaceSettingsManager;
    private readonly FirewallRulesManager _rulesManager;
    private readonly FirewallDefaultsManager _defaultsManager;
    private readonly InterfaceAssignmentStore _interfaceStore;
    private readonly FirewallVirtualIpManager _virtualIpManager;
    private readonly FirewallVirtualIpApplier _virtualIpApplier;
    private readonly FirewallScheduleManager _scheduleManager;
    private readonly TrafficShaperManager _trafficShaperManager;
    private readonly TrafficShaperApplier _trafficShaperApplier;
    private readonly PlatformCommandRunner _commandRunner;
    private readonly LoggingManager _loggingManager;

    public FirewallApplyManager(
        FirewallAliasManager aliasManager,
        FirewallNatManager natManager,
        FirewallNatSettingsManager natSettingsManager,
        FirewallRulesManager rulesManager,
        FirewallDefaultsManager defaultsManager,
        FirewallInterfaceSettingsManager interfaceSettingsManager,
        InterfaceAssignmentStore interfaceStore,
        PlatformCommandRunner commandRunner)
    {
        _aliasManager = aliasManager;
        _natManager = natManager;
        _natSettingsManager = natSettingsManager;
        _rulesManager = rulesManager;
        _defaultsManager = defaultsManager;
        _interfaceSettingsManager = interfaceSettingsManager;
        _interfaceStore = interfaceStore;
        _commandRunner = commandRunner;
        _loggingManager = LoggingManager.Instance;
        _virtualIpManager = new FirewallVirtualIpManager();
        _virtualIpApplier = new FirewallVirtualIpApplier(commandRunner, interfaceStore);
        _scheduleManager = new FirewallScheduleManager();
        _trafficShaperManager = new TrafficShaperManager();
        _trafficShaperApplier = new TrafficShaperApplier(commandRunner, interfaceStore);
    }

    public async Task<FirewallApplyResult> ApplyAsync(CancellationToken cancellationToken)
    {
        // STEP 1: Apply Virtual IPs BEFORE firewall rules
        // Virtual IPs must be configured on interfaces before firewall can reference them
        var virtualIps = await _virtualIpManager.ListVirtualIpsAsync();
        if (virtualIps.Count > 0)
        {
            var vipResult = await _virtualIpApplier.ApplyAsync(virtualIps, cancellationToken);
            if (!vipResult.Success)
            {
                await _loggingManager.LogSecurityAsync(
                    "Firewall",
                    "Warning",
                    "VirtualIp",
                    $"Virtual IP application had {vipResult.Errors.Count} error(s), continuing with firewall apply",
                    details: new Dictionary<string, object>
                    {
                        ["appliedCount"] = vipResult.AppliedCount,
                        ["skippedCount"] = vipResult.SkippedCount,
                        ["errors"] = string.Join("; ", vipResult.Errors)
                    });
            }
        }

        // STEP 2: Build firewall configuration
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

        // Write config to /etc/nftables.conf for persistence
        // This ensures rules survive reboots and are in the standard location
        var systemConfigPath = "/etc/nftables.conf";
        
        // Copy the generated config to /etc/nftables.conf using sudo
        var copyResult = await _commandRunner.RunAsync(new PlatformCommand
        {
            FileName = "cp",
            Arguments = $"\"{configPath}\" \"{systemConfigPath}\"",
            UseSudo = true,
            TimeoutMs = 5000
        }, cancellationToken);
        
        if (copyResult.ExitCode != 0)
        {
            return new FirewallApplyResult
            {
                Success = false,
                Error = $"Failed to copy config to {systemConfigPath}: {copyResult.StdErr?.Trim() ?? "Unknown error"}",
                Warnings = buildResult.Warnings,
                ConfigPath = configPath
            };
        }

        // Delete existing tables before applying new configuration
        // This is necessary because nft -f will fail if tables already exist
        var cleanupResult = await RemoveManagedTablesAsync(cancellationToken);
        if (!cleanupResult.Success)
        {
            return new FirewallApplyResult
            {
                Success = false,
                Error = cleanupResult.Error ?? "Failed to remove existing firewall tables",
                Warnings = buildResult.Warnings,
                ConfigPath = systemConfigPath
            };
        }

        // Apply the new firewall configuration from /etc/nftables.conf
        // Use -f to load from file - this will create tables and chains atomically
        var result = await _commandRunner.RunAsync(new PlatformCommand
        {
            FileName = "nft",
            Arguments = $"-f \"{systemConfigPath}\"",
            UseSudo = true,
            TimeoutMs = 30000  // Increased timeout to ensure apply completes
        }, cancellationToken);

        if (result.ExitCode != 0)
        {
            var errorMessage = string.IsNullOrWhiteSpace(result.StdErr)
                ? $"nft exited with code {result.ExitCode}"
                : result.StdErr.Trim();
            
            // Include stdout in error if available (nft sometimes puts errors there)
            if (!string.IsNullOrWhiteSpace(result.StdOut))
            {
                errorMessage += $"\nOutput: {result.StdOut.Trim()}";
            }

            return new FirewallApplyResult
            {
                Success = false,
                Error = errorMessage,
                Warnings = buildResult.Warnings,
                ConfigPath = systemConfigPath
            };
        }

        // Verify the apply was successful by checking if tables exist
        var verifyResult = await _commandRunner.RunAsync(new PlatformCommand
        {
            FileName = "nft",
            Arguments = "list table inet monolith_filter",
            UseSudo = true,
            TimeoutMs = 5000
        }, cancellationToken);

        if (verifyResult.ExitCode != 0)
        {
            var verifyError = string.IsNullOrWhiteSpace(verifyResult.StdErr)
                ? $"nft list exited with code {verifyResult.ExitCode}"
                : verifyResult.StdErr.Trim();
            
            await _loggingManager.LogSecurityAsync(
                "Firewall",
                "Error",
                "FirewallApply",
                "Firewall configuration verification failed",
                details: new Dictionary<string, object>
                {
                    ["configPath"] = systemConfigPath,
                    ["backupPath"] = configPath,
                    ["verifyError"] = verifyError,
                    ["nftStdout"] = verifyResult.StdOut ?? string.Empty
                });

            return new FirewallApplyResult
            {
                Success = false,
                Error = $"Firewall configuration was not applied - tables do not exist after apply. Verification error: {verifyError}",
                Warnings = buildResult.Warnings,
                ConfigPath = systemConfigPath
            };
        }

        await _loggingManager.LogSecurityAsync(
            "Firewall",
            "Info",
            "FirewallApply",
            "Applied firewall configuration successfully",
            details: new Dictionary<string, object>
            {
                ["configPath"] = systemConfigPath,
                ["backupPath"] = configPath,
                ["verifyOutput"] = verifyResult.StdOut ?? string.Empty
            });

        // STEP 3: Apply Traffic Shaping AFTER firewall rules
        // Traffic shaping is applied to interfaces and is independent of nftables
        var trafficShaperRules = await _trafficShaperManager.ListRulesAsync();
        if (trafficShaperRules.Count > 0)
        {
            var tsResult = await _trafficShaperApplier.ApplyAsync(trafficShaperRules, cancellationToken);
            if (!tsResult.Success)
            {
                await _loggingManager.LogSecurityAsync(
                    "Firewall",
                    "Warning",
                    "TrafficShaper",
                    $"Traffic shaping had {tsResult.Errors.Count} error(s), firewall rules still active",
                    details: new Dictionary<string, object>
                    {
                        ["appliedCount"] = tsResult.AppliedCount,
                        ["skippedCount"] = tsResult.SkippedCount,
                        ["errors"] = string.Join("; ", tsResult.Errors)
                    });
                // Don't fail the entire apply if traffic shaping fails
                // Firewall rules are more critical than QoS
            }
        }

        return new FirewallApplyResult
        {
            Success = true,
            ConfigPath = systemConfigPath,  // Return the system config path where rules are actually applied
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
            // Check if table exists
            var listResult = await _commandRunner.RunAsync(new PlatformCommand
            {
                FileName = "nft",
                Arguments = $"list table {family} {name}",
                UseSudo = true,
                TimeoutMs = 5000
            }, cancellationToken);

            if (listResult.ExitCode != 0)
            {
                // Table doesn't exist, skip
                continue;
            }

            // Delete the table - this is necessary for nft -f to work
            // The gap is minimized by applying new rules immediately after
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
        var interfaceSettings = await _interfaceSettingsManager.GetAllAsync();
        var effectiveRules = await _rulesManager.GetEffectiveRulesAsync(defaults);
        var assignments = await _interfaceStore.GetAssignmentsAsync();

        var warnings = new List<string>();
        var builder = new StringBuilder();

        builder.AppendLine("# Generated by Monolith FireWall");
        builder.AppendLine($"# {DateTime.UtcNow:O}");
        builder.AppendLine("# Managed tables will be replaced by apply step");

        // Filter NAT rules based on schedules
        var activeNatRules = await FilterRulesByScheduleAsync(natRules);
        var ipv4Rules = activeNatRules.Where(r => r.Enabled && (r.AddressFamily == "ipv4" || r.AddressFamily == "dual")).ToList();
        var ipv6Rules = activeNatRules.Where(r => r.Enabled && (r.AddressFamily == "ipv6" || r.AddressFamily == "dual")).ToList();

        // Filter effective filter rules based on schedules
        var activeFilterRules = await FilterRulesByScheduleAsync(effectiveRules);
        effectiveRules = activeFilterRules;

        // Check if we have WAN interfaces - if so, we need NAT table for masquerade even without NAT rules
        var hasWanInterfaces = assignments.Any(a => a.Role == InterfaceRole.Wan);
        
        // Always create IPv4 NAT table if we have WAN interfaces (for masquerade) or NAT rules
        if (ipv4Rules.Count > 0 || hasWanInterfaces)
        {
            AppendNatTable(builder, "ip", ipv4Rules, aliases, natSettings, assignments, warnings);
        }
        else if (ipv4Rules.Count == 0 && ipv6Rules.Count == 0)
        {
            warnings.Add("No enabled NAT rules found and no WAN interfaces configured");
        }

        // Always create IPv6 NAT table if we have WAN interfaces (for masquerade) or NAT rules
        if (ipv6Rules.Count > 0 || hasWanInterfaces)
        {
            AppendNatTable(builder, "ip6", ipv6Rules, aliases, natSettings, assignments, warnings);
        }

        AppendFilterTable(builder, effectiveRules, defaults, interfaceSettings, assignments, aliases, warnings);

        // Write to backup location first (we'll copy to /etc/nftables.conf in ApplyAsync)
        var backupConfigPath = "/var/lib/monolith-firewall/firewall.nft";
        
        // Ensure backup directory exists
        var backupDir = Path.GetDirectoryName(backupConfigPath);
        if (!string.IsNullOrWhiteSpace(backupDir))
        {
            Directory.CreateDirectory(backupDir);
        }

        var configContent = builder.ToString();
        
        // Write to backup location (this will be copied to /etc/nftables.conf during apply)
        await File.WriteAllTextAsync(backupConfigPath, configContent, cancellationToken);

        return new FirewallApplyResult
        {
            Success = true,
            ConfigPath = backupConfigPath,  // Return backup path (will be copied to /etc/nftables.conf in ApplyAsync)
            Warnings = warnings
        };
    }

    private void AppendNatTable(
        StringBuilder builder,
        string family,
        List<FirewallNatRuleView> rules,
        List<FirewallAliasView> aliases,
        FirewallNatSettingsView natSettings,
        List<InterfaceAssignmentEntity> assignments,
        List<string> warnings)
    {
        builder.AppendLine($"table {family} monolith_nat {{");

        var usedAliases = CollectUsedAliases(rules, aliases, family);
        foreach (var aliasSet in usedAliases.AddressSets)
        {
            builder.AppendLine($"  set {aliasSet.Name} {{");
            builder.AppendLine($"    type {aliasSet.Type};");
            // Check if set contains CIDR ranges (contains '/')
            var hasCidr = aliasSet.Values.Any(v => v.Contains('/'));
            if (hasCidr)
            {
                builder.AppendLine("    flags interval;");
            }
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
        
        // Postrouting chain for masquerade (enables NAT for outbound traffic)
        builder.AppendLine("  chain postrouting {");
        builder.AppendLine("    type nat hook postrouting priority 100; policy accept;");

        // Get WAN and LAN interfaces for this address family
        var wanInterfaces = assignments
            .Where(a => a.Role == InterfaceRole.Wan)
            .Select(a => a.InterfaceName)
            .ToList();
        var lanInterfaces = assignments
            .Where(a => a.Role == InterfaceRole.Lan)
            .Select(a => a.InterfaceName)
            .ToList();

        // Standard WAN masquerade rules
        if (wanInterfaces.Count > 0)
        {
            foreach (var wanInterface in wanInterfaces)
            {
                // Masquerade all outbound traffic on WAN interface
                // This allows LAN devices to access internet through WAN
                builder.AppendLine($"    oifname \"{wanInterface}\" masquerade comment \"Auto: WAN masquerade on {wanInterface}\"");
            }
        }

        // NAT Reflection (Hairpin NAT) rules
        // These allow LAN clients to access port forwards using the WAN IP
        if (lanInterfaces.Count > 0 && (natSettings.ReflectionEnabled || rules.Any(r => r.ReflectionMode == "nat")))
        {
            AppendReflectionRules(builder, rules, lanInterfaces, usedAliases, family, natSettings, warnings);
        }

        builder.AppendLine("  }");
        builder.AppendLine("}");
    }

    private void AppendReflectionRules(
        StringBuilder builder,
        List<FirewallNatRuleView> rules,
        List<string> lanInterfaces,
        AliasSetCollection aliasSets,
        string family,
        FirewallNatSettingsView natSettings,
        List<string> warnings)
    {
        builder.AppendLine();
        builder.AppendLine("    # NAT Reflection (Hairpin NAT) rules");
        builder.AppendLine("    # Allow LAN clients to access port forwards via WAN IP");

        foreach (var rule in rules.OrderBy(r => r.RuleNumber))
        {
            // Determine if reflection is enabled for this rule
            var reflectionEnabled = rule.ReflectionMode == "nat" ||
                                   (rule.ReflectionMode == "default" && natSettings.ReflectionEnabled && natSettings.ReflectionMode == "nat");

            if (!reflectionEnabled)
            {
                continue;
            }

            // Only port forwards support reflection
            if (rule.Type != "port_forward")
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(rule.RedirectTargetIp))
            {
                warnings.Add($"NAT rule #{rule.RuleNumber}: Reflection enabled but no redirect target");
                continue;
            }

            var reflectionRules = BuildReflectionRule(rule, lanInterfaces, aliasSets, family, warnings);
            foreach (var line in reflectionRules)
            {
                builder.AppendLine("    " + line);
            }
        }
    }

    private List<string> BuildReflectionRule(
        FirewallNatRuleView rule,
        List<string> lanInterfaces,
        AliasSetCollection aliasSets,
        string family,
        List<string> warnings)
    {
        var lines = new List<string>();
        var addressQualifier = family == "ip6" ? "ip6" : "ip";

        // For each LAN interface, create a reflection rule
        foreach (var lanInterface in lanInterfaces)
        {
            var match = new StringBuilder();

            // Match traffic coming from AND going to the same LAN interface (hairpin)
            match.Append($"iifname \"{lanInterface}\" oifname \"{lanInterface}\"");

            // Match the target IP (after DNAT has been applied in prerouting)
            match.Append($" {addressQualifier} daddr {rule.RedirectTargetIp}");

            // Match protocol if specified
            var protocols = ExpandProtocols(rule.Protocol);
            foreach (var protocol in protocols)
            {
                var protocolMatch = new StringBuilder(match.ToString());

                if (protocol != "any")
                {
                    protocolMatch.Append($" {protocol}");
                }

                // Match destination port (the redirected port)
                if (!string.IsNullOrWhiteSpace(rule.RedirectTargetPort) && protocol != "any" && protocol != "icmp")
                {
                    protocolMatch.Append($" dport {rule.RedirectTargetPort}");
                }
                else if (!string.IsNullOrWhiteSpace(rule.DestinationPort) && protocol != "any" && protocol != "icmp")
                {
                    // If no redirect port specified, use original destination port
                    var portMatch = BuildPortMatch(protocol, rule.DestinationPort, aliasSets, warnings, rule.RuleNumber);
                    if (!string.IsNullOrWhiteSpace(portMatch))
                    {
                        protocolMatch.Append($" {portMatch}");
                    }
                }

                // Apply masquerade (SNAT) so return traffic goes through router
                var comment = !string.IsNullOrWhiteSpace(rule.Description)
                    ? $"Reflection: {rule.Description.Replace("\"", "")}"
                    : $"Reflection: Rule #{rule.RuleNumber}";

                protocolMatch.Append($" masquerade comment \"{comment}\"");

                lines.Add(protocolMatch.ToString());
            }
        }

        return lines;
    }

    private void AppendFilterTable(
        StringBuilder builder,
        List<FirewallRuleView> rules,
        FirewallDefaultsView defaults,
        List<FirewallInterfaceSettingsEntity> interfaceSettings,
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
            // Check if set contains CIDR ranges (contains '/')
            var hasCidr = aliasSet.Values.Any(v => v.Contains('/'));
            if (hasCidr)
            {
                builder.AppendLine("    flags interval;");
            }
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

        // Always ensure system sets are present if we need them for blocking
        var systemSets = aliasSets.SystemSets.ToList();
        if (!systemSets.Any(s => s.Name == "sys_rfc1918_v4"))
        {
            systemSets.Add(new RuleAliasSetDefinition
            {
                Name = "sys_rfc1918_v4",
                Type = "ipv4_addr",
                Values = new List<string> { "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16" },
                Family = "ip"
            });
        }
        if (!systemSets.Any(s => s.Name == "sys_reserved_v4"))
        {
            systemSets.Add(new RuleAliasSetDefinition
            {
                Name = "sys_reserved_v4",
                Type = "ipv4_addr",
                Values = new List<string> { "0.0.0.0/8", "100.64.0.0/10", "127.0.0.0/8", "169.254.0.0/16", "192.0.0.0/24", "192.0.2.0/24", "198.18.0.0/15", "198.51.100.0/24", "203.0.113.0/24", "224.0.0.0/4", "240.0.0.0/4" },
                Family = "ip"
            });
        }
        if (!systemSets.Any(s => s.Name == "sys_rfc4193_v6"))
        {
            systemSets.Add(new RuleAliasSetDefinition
            {
                Name = "sys_rfc4193_v6",
                Type = "ipv6_addr",
                Values = new List<string> { "fc00::/7" },
                Family = "ip6"
            });
        }
        if (!systemSets.Any(s => s.Name == "sys_reserved_v6"))
        {
            systemSets.Add(new RuleAliasSetDefinition
            {
                Name = "sys_reserved_v6",
                Type = "ipv6_addr",
                Values = new List<string> { "::/128", "::1/128", "::ffff:0:0/96", "100::/64", "2001:db8::/32", "2001:10::/28", "fe80::/10", "ff00::/8" },
                Family = "ip6"
            });
        }

        foreach (var systemSet in systemSets)
        {
            builder.AppendLine($"  set {systemSet.Name} {{");
            builder.AppendLine($"    type {systemSet.Type};");
            builder.AppendLine("    flags interval;");
            builder.AppendLine("    elements = { " + string.Join(", ", systemSet.Values) + " }");
            builder.AppendLine("  }");
        }

        builder.AppendLine("  chain input {");
        builder.AppendLine("    type filter hook input priority 0; policy drop;");

        // Connection tracking for input chain
        builder.AppendLine();
        builder.AppendLine("    # Connection tracking");
        builder.AppendLine("    ct state invalid drop comment \"Drop invalid packets\"");
        builder.AppendLine("    ct state established,related accept comment \"Allow established/related connections\"");
        builder.AppendLine();

        // Allow loopback
        builder.AppendLine("    iif \"lo\" accept comment \"Allow loopback\"");
        builder.AppendLine();

        foreach (var assignment in assignments)
        {
            builder.AppendLine($"    iifname \"{assignment.InterfaceName}\" jump input_{assignment.InterfaceName}");
        }

        builder.AppendLine("  }");

        builder.AppendLine("  chain forward {");
        builder.AppendLine("    type filter hook forward priority 0; policy drop;");

        // Connection tracking - stateful firewall (CRITICAL SECURITY)
        builder.AppendLine();
        builder.AppendLine("    # Connection tracking - stateful firewall");
        builder.AppendLine("    ct state invalid drop comment \"Drop invalid packets\"");
        builder.AppendLine("    ct state established,related accept comment \"Allow established/related connections\"");
        builder.AppendLine();

        // Automatic forward rules for WAN↔LAN routing
        // Get interface roles
        var wanInterfaces = assignments
            .Where(a => a.Role == InterfaceRole.Wan)
            .Select(a => a.InterfaceName)
            .ToList();
        var lanInterfaces = assignments
            .Where(a => a.Role == InterfaceRole.Lan)
            .Select(a => a.InterfaceName)
            .ToList();

        // Allow NEW connections from LAN to WAN (outbound internet access)
        // Established/related connections are handled by ct state rules above
        if (lanInterfaces.Count > 0 && wanInterfaces.Count > 0)
        {
            foreach (var lan in lanInterfaces)
            {
                foreach (var wan in wanInterfaces)
                {
                    builder.AppendLine($"    iifname \"{lan}\" oifname \"{wan}\" ct state new accept comment \"Auto: LAN to WAN (new outbound connections)\"");
                }
            }
        }

        // Allow forwarding between LAN interfaces (for internal routing)
        if (lanInterfaces.Count > 1)
        {
            for (int i = 0; i < lanInterfaces.Count; i++)
            {
                for (int j = i + 1; j < lanInterfaces.Count; j++)
                {
                    var lan1 = lanInterfaces[i];
                    var lan2 = lanInterfaces[j];
                    builder.AppendLine($"    iifname \"{lan1}\" oifname \"{lan2}\" accept comment \"Auto: LAN to LAN routing\"");
                    builder.AppendLine($"    iifname \"{lan2}\" oifname \"{lan1}\" accept comment \"Auto: LAN to LAN routing\"");
                }
            }
        }

        // Continue with interface-specific forward chains (for custom user rules)
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
            var settings = interfaceSettings.FirstOrDefault(s => s.InterfaceName.Equals(assignment.InterfaceName, StringComparison.OrdinalIgnoreCase));
            
            // Determine effective settings (specific > default > fallback)
            var defaultAction = settings?.DefaultAction ?? GetDefaultAction(assignment.Role, defaults);
            var blockReserved = settings?.BlockReserved ?? (assignment.Role == InterfaceRole.Wan && defaults.BlockReservedOnWan);
            var blockBogon = settings?.BlockBogon ?? false;

            builder.AppendLine($"  chain input_{assignment.InterfaceName} {{");
            
            // Block Reserved (RFC1918) networks on WAN
            if (blockReserved)
            {
                builder.AppendLine("    ip saddr @sys_rfc1918_v4 drop comment \"Block private networks (RFC 1918)\"");
                builder.AppendLine("    ip saddr @sys_reserved_v4 drop comment \"Block reserved networks\"");
                builder.AppendLine("    ip6 saddr @sys_rfc4193_v6 drop comment \"Block ULA networks (RFC 4193)\"");
                builder.AppendLine("    ip6 saddr @sys_reserved_v6 drop comment \"Block reserved IPv6 networks\"");
            }

            // Block Bogon networks (not fully implemented list yet, re-using reserved for now as placeholder)
            if (blockBogon)
            {
                // Placeholder for bogon blocking
            }

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
        var family = (rule.AddressFamily ?? "ipv4").ToLowerInvariant();
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

        var addressQualifier = family == "ipv6" ? "ip6" : "ip";

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
                // Enhanced log prefix with rule metadata for easier analysis
                // Format: MF[rule_id,interface,direction,action]
                var logPrefix = $"MF[{rule.Id},{rule.Interface},{rule.Direction},{rule.Action}] ";
                lines.Add($"{match} log prefix \"{logPrefix}\"");
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

        // Enforce address family alignment for direct addresses/networks
        if (type == "address")
        {
            var family = (rule.AddressFamily ?? "ipv4").ToLowerInvariant();
            var addressFamily = PlatformValidators.GetAddressFamily(value);
            if (addressFamily != null && addressFamily != family && family != "inet")
            {
                warnings.Add($"Rule on {rule.Interface}: {direction} {value} is {addressFamily} but rule family is {family}");
                return string.Empty;
            }
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

        // Don't include protocol here - it's already added in BuildFilterRule
        if (aliasSets.TryGetPortSet(value, out var setName))
        {
            return $"{(isSource ? "sport" : "dport")} @{setName}";
        }

        var portExpression = value.Contains(',')
            ? "{" + value + "}"
            : value;

        return $"{(isSource ? "sport" : "dport")} {portExpression}";
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

            // Add logging if enabled
            if (rule.LogEnabled)
            {
                // Enhanced log prefix with NAT rule metadata
                // Format: MF-NAT[rule_id,interface,type,target]
                var logPrefix = $"MF-NAT[{rule.Id},{rule.Interface},{rule.Type},{rule.RedirectTargetIp}";
                if (!string.IsNullOrWhiteSpace(rule.RedirectTargetPort))
                {
                    logPrefix += $":{rule.RedirectTargetPort}";
                }
                logPrefix += "] ";

                var logLine = new StringBuilder();
                logLine.Append(match);
                logLine.Append(" log prefix \"").Append(logPrefix).Append("\"");
                lines.Add(logLine.ToString());
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

        // Don't include protocol here - it's already added in BuildNatRule
        if (aliasSets.TryGetPortSet(value, out var setName))
        {
            return $"dport @{setName}";
        }

        return $"dport {value}";
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

        // Don't include protocol here - it's already added in BuildNatRule
        if (aliasSets.TryGetPortSet(value, out var setName))
        {
            return $"sport @{setName}";
        }

        return $"sport {value}";
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

    /// <summary>
    /// Filter firewall filter rules based on their schedule (if any)
    /// Rules without schedules or with active schedules are included
    /// </summary>
    private async Task<List<FirewallRuleView>> FilterRulesByScheduleAsync(List<FirewallRuleView> rules)
    {
        var result = new List<FirewallRuleView>();
        var now = DateTime.Now;

        foreach (var rule in rules)
        {
            // If rule has no schedule, always include it
            if (!rule.ScheduleId.HasValue)
            {
                result.Add(rule);
                continue;
            }

            // Check if the schedule is currently active
            var isActive = await _scheduleManager.IsScheduleActiveAsync(rule.ScheduleId.Value, now);
            if (isActive)
            {
                result.Add(rule);
            }
            // If schedule is not active, rule is filtered out (not added to result)
        }

        return result;
    }

    /// <summary>
    /// Filter NAT rules based on their schedule (if any)
    /// Rules without schedules or with active schedules are included
    /// </summary>
    private async Task<List<FirewallNatRuleView>> FilterRulesByScheduleAsync(List<FirewallNatRuleView> rules)
    {
        var result = new List<FirewallNatRuleView>();
        var now = DateTime.Now;

        foreach (var rule in rules)
        {
            // If rule has no schedule, always include it
            if (!rule.ScheduleId.HasValue)
            {
                result.Add(rule);
                continue;
            }

            // Check if the schedule is currently active
            var isActive = await _scheduleManager.IsScheduleActiveAsync(rule.ScheduleId.Value, now);
            if (isActive)
            {
                result.Add(rule);
            }
            // If schedule is not active, rule is filtered out (not added to result)
        }

        return result;
    }
}

public sealed class FirewallApplyResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? ConfigPath { get; set; }
    public List<string> Warnings { get; set; } = new();
}
