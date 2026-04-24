using System.Text;
using Monolith.FireWall.Common.Services;
using Monolith.FireWall.Core.Models;
using Monolith.FireWall.Core.Services.Platform;
using Monolith.FireWall.Platform.Models;

namespace Monolith.FireWall.Core.Services.Firewall;

/// <summary>
/// Applies traffic shaping rules to network interfaces using Linux tc (traffic control)
/// Supports HTB (Hierarchical Token Bucket), FQ_CoDel, and other queuing disciplines
/// </summary>
public sealed class TrafficShaperApplier
{
    private readonly PlatformCommandRunner _commandRunner;
    private readonly InterfaceAssignmentStore _interfaceStore;
    private readonly LoggingManager _loggingManager;

    public TrafficShaperApplier(
        PlatformCommandRunner commandRunner,
        InterfaceAssignmentStore interfaceStore)
    {
        _commandRunner = commandRunner;
        _interfaceStore = interfaceStore;
        _loggingManager = LoggingManager.Instance;
    }

    /// <summary>
    /// Apply traffic shaping rules to all interfaces
    /// </summary>
    public async Task<TrafficShaperApplyResult> ApplyAsync(
        List<TrafficShaperRuleView> rules,
        CancellationToken cancellationToken = default)
    {
        var result = new TrafficShaperApplyResult
        {
            Success = true,
            AppliedCount = 0,
            SkippedCount = 0,
            Errors = new List<string>()
        };

        // Get all interface assignments for validation
        var assignments = await _interfaceStore.GetAssignmentsAsync();
        var validInterfaces = assignments.Select(a => a.InterfaceName).ToHashSet();

        // First, remove all existing traffic control rules (cleanup)
        await RemoveExistingTrafficControlAsync(validInterfaces, cancellationToken);

        // Apply each enabled traffic shaping rule
        foreach (var rule in rules.Where(r => r.Enabled).OrderBy(r => r.Id))
        {
            try
            {
                // Validate interface exists
                if (!validInterfaces.Contains(rule.Interface))
                {
                    result.Errors.Add($"Traffic Shaper #{rule.Id}: Interface '{rule.Interface}' does not exist");
                    result.SkippedCount++;
                    continue;
                }

                // Validate bandwidth values
                if (rule.BandwidthUp <= 0 && rule.BandwidthDown <= 0)
                {
                    result.Errors.Add($"Traffic Shaper #{rule.Id}: At least one bandwidth limit must be set");
                    result.SkippedCount++;
                    continue;
                }

                // Apply traffic shaping
                var (success, error) = await ApplyTrafficShapingAsync(rule, cancellationToken);

                if (success)
                {
                    result.AppliedCount++;
                    await _loggingManager.LogSecurityAsync(
                        "Firewall",
                        "Info",
                        "TrafficShaper",
                        $"Applied traffic shaping on {rule.Interface}: {rule.Name}",
                        details: new Dictionary<string, object>
                        {
                            ["ruleId"] = rule.Id,
                            ["interface"] = rule.Interface,
                            ["bandwidthUp"] = rule.BandwidthUp,
                            ["bandwidthDown"] = rule.BandwidthDown,
                            ["scheduler"] = rule.Scheduler
                        });
                }
                else
                {
                    result.Errors.Add($"Traffic Shaper #{rule.Id}: {error}");
                    result.SkippedCount++;
                    result.Success = false;
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Traffic Shaper #{rule.Id}: {ex.Message}");
                result.SkippedCount++;
                result.Success = false;
            }
        }

        if (result.Errors.Count > 0)
        {
            await _loggingManager.LogSecurityAsync(
                "Firewall",
                "Warning",
                "TrafficShaper",
                $"Traffic shaping completed with {result.Errors.Count} error(s)",
                details: new Dictionary<string, object>
                {
                    ["appliedCount"] = result.AppliedCount,
                    ["skippedCount"] = result.SkippedCount,
                    ["errors"] = string.Join("; ", result.Errors)
                });
        }

        return result;
    }

    /// <summary>
    /// Apply traffic shaping to a single interface
    /// Uses HTB (Hierarchical Token Bucket) with specified scheduler
    /// </summary>
    private async Task<(bool Success, string? Error)> ApplyTrafficShapingAsync(
        TrafficShaperRuleView rule,
        CancellationToken cancellationToken)
    {
        try
        {
            var errors = new List<string>();

            // Apply egress (upload) shaping
            if (rule.BandwidthUp > 0)
            {
                var (success, error) = await ApplyEgressShapingAsync(
                    rule.Interface,
                    rule.BandwidthUp,
                    rule.Scheduler,
                    cancellationToken);

                if (!success)
                {
                    errors.Add($"Egress: {error}");
                }
            }

            // Apply ingress (download) shaping
            // Note: Ingress shaping is more complex and requires IFB (Intermediate Functional Block) device
            if (rule.BandwidthDown > 0)
            {
                var (success, error) = await ApplyIngressShapingAsync(
                    rule.Interface,
                    rule.BandwidthDown,
                    rule.Scheduler,
                    cancellationToken);

                if (!success)
                {
                    errors.Add($"Ingress: {error}");
                }
            }

            if (errors.Count > 0)
            {
                return (false, string.Join(", ", errors));
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Apply egress (upload) traffic shaping using HTB qdisc
    /// </summary>
    private async Task<(bool Success, string? Error)> ApplyEgressShapingAsync(
        string iface,
        int bandwidthKbps,
        string scheduler,
        CancellationToken cancellationToken)
    {
        try
        {
            // Convert Kbps to bits/sec for tc
            var rateBps = bandwidthKbps * 1000;

            // Create root HTB qdisc
            // HTB allows hierarchical bandwidth control
            var rootQdisc = new PlatformCommand
            {
                FileName = "tc",
                Arguments = $"qdisc add dev {iface} root handle 1: htb default 10",
                UseSudo = true,
                TimeoutMs = 5000
            };

            var result = await _commandRunner.RunAsync(rootQdisc, cancellationToken);
            if (result.ExitCode != 0 && !result.StdErr?.Contains("File exists") == true)
            {
                return (false, $"Failed to create root qdisc: {result.StdErr}");
            }

            // Create HTB class with bandwidth limit
            var htbClass = new PlatformCommand
            {
                FileName = "tc",
                Arguments = $"class add dev {iface} parent 1: classid 1:10 htb rate {rateBps}",
                UseSudo = true,
                TimeoutMs = 5000
            };

            result = await _commandRunner.RunAsync(htbClass, cancellationToken);
            if (result.ExitCode != 0 && !result.StdErr?.Contains("File exists") == true)
            {
                return (false, $"Failed to create HTB class: {result.StdErr}");
            }

            // Add leaf qdisc based on scheduler
            var leafQdisc = GetLeafQdiscCommand(iface, scheduler, "1:10");
            result = await _commandRunner.RunAsync(leafQdisc, cancellationToken);
            if (result.ExitCode != 0 && !result.StdErr?.Contains("File exists") == true)
            {
                return (false, $"Failed to add {scheduler} qdisc: {result.StdErr}");
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Apply ingress (download) traffic shaping using IFB device redirection
    /// Ingress shaping requires redirecting traffic to an IFB device and shaping there
    /// </summary>
    private async Task<(bool Success, string? Error)> ApplyIngressShapingAsync(
        string iface,
        int bandwidthKbps,
        string scheduler,
        CancellationToken cancellationToken)
    {
        try
        {
            // Convert Kbps to bits/sec for tc
            var rateBps = bandwidthKbps * 1000;

            // Use IFB (Intermediate Functional Block) device for ingress shaping
            // First, ensure ifb module is loaded
            var modprobe = new PlatformCommand
            {
                FileName = "modprobe",
                Arguments = "ifb numifbs=1",
                UseSudo = true,
                TimeoutMs = 5000
            };
            await _commandRunner.RunAsync(modprobe, cancellationToken);

            // Bring up ifb0 device
            var ifbUp = new PlatformCommand
            {
                FileName = "ip",
                Arguments = "link set dev ifb0 up",
                UseSudo = true,
                TimeoutMs = 5000
            };
            await _commandRunner.RunAsync(ifbUp, cancellationToken);

            // Create ingress qdisc on the interface
            var ingressQdisc = new PlatformCommand
            {
                FileName = "tc",
                Arguments = $"qdisc add dev {iface} ingress",
                UseSudo = true,
                TimeoutMs = 5000
            };

            var result = await _commandRunner.RunAsync(ingressQdisc, cancellationToken);
            if (result.ExitCode != 0 && !result.StdErr?.Contains("File exists") == true)
            {
                return (false, $"Failed to create ingress qdisc: {result.StdErr}");
            }

            // Redirect ingress traffic to ifb0
            var redirect = new PlatformCommand
            {
                FileName = "tc",
                Arguments = $"filter add dev {iface} parent ffff: protocol all u32 match u32 0 0 action mirred egress redirect dev ifb0",
                UseSudo = true,
                TimeoutMs = 5000
            };

            result = await _commandRunner.RunAsync(redirect, cancellationToken);
            if (result.ExitCode != 0 && !result.StdErr?.Contains("File exists") == true)
            {
                return (false, $"Failed to redirect ingress: {result.StdErr}");
            }

            // Apply egress shaping on ifb0 (which is actually ingress for the real interface)
            var ifbRoot = new PlatformCommand
            {
                FileName = "tc",
                Arguments = "qdisc add dev ifb0 root handle 1: htb default 10",
                UseSudo = true,
                TimeoutMs = 5000
            };

            result = await _commandRunner.RunAsync(ifbRoot, cancellationToken);
            if (result.ExitCode != 0 && !result.StdErr?.Contains("File exists") == true)
            {
                return (false, $"Failed to create ifb0 root qdisc: {result.StdErr}");
            }

            var ifbClass = new PlatformCommand
            {
                FileName = "tc",
                Arguments = $"class add dev ifb0 parent 1: classid 1:10 htb rate {rateBps}",
                UseSudo = true,
                TimeoutMs = 5000
            };

            result = await _commandRunner.RunAsync(ifbClass, cancellationToken);
            if (result.ExitCode != 0 && !result.StdErr?.Contains("File exists") == true)
            {
                return (false, $"Failed to create ifb0 HTB class: {result.StdErr}");
            }

            // Add leaf qdisc to ifb0
            var ifbLeaf = GetLeafQdiscCommand("ifb0", scheduler, "1:10");
            result = await _commandRunner.RunAsync(ifbLeaf, cancellationToken);
            if (result.ExitCode != 0 && !result.StdErr?.Contains("File exists") == true)
            {
                return (false, $"Failed to add ifb0 {scheduler} qdisc: {result.StdErr}");
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Get the appropriate leaf qdisc command based on scheduler type
    /// </summary>
    private PlatformCommand GetLeafQdiscCommand(string iface, string scheduler, string parent)
    {
        var normalizedScheduler = scheduler.ToLowerInvariant();

        var arguments = normalizedScheduler switch
        {
            "fq_codel" => $"qdisc add dev {iface} parent {parent} fq_codel",
            "sfq" => $"qdisc add dev {iface} parent {parent} sfq perturb 10",
            "pfifo" => $"qdisc add dev {iface} parent {parent} pfifo limit 1000",
            "bfifo" => $"qdisc add dev {iface} parent {parent} bfifo limit 1000000",
            _ => $"qdisc add dev {iface} parent {parent} fq_codel" // Default to fq_codel
        };

        return new PlatformCommand
        {
            FileName = "tc",
            Arguments = arguments,
            UseSudo = true,
            TimeoutMs = 5000
        };
    }

    /// <summary>
    /// Remove all existing traffic control rules from all interfaces
    /// </summary>
    private async Task RemoveExistingTrafficControlAsync(
        HashSet<string> interfaces,
        CancellationToken cancellationToken)
    {
        try
        {
            foreach (var iface in interfaces)
            {
                // Remove root qdisc (this removes all child qdiscs and classes)
                var removeRoot = new PlatformCommand
                {
                    FileName = "tc",
                    Arguments = $"qdisc del dev {iface} root",
                    UseSudo = true,
                    TimeoutMs = 5000
                };
                await _commandRunner.RunAsync(removeRoot, cancellationToken);

                // Remove ingress qdisc
                var removeIngress = new PlatformCommand
                {
                    FileName = "tc",
                    Arguments = $"qdisc del dev {iface} ingress",
                    UseSudo = true,
                    TimeoutMs = 5000
                };
                await _commandRunner.RunAsync(removeIngress, cancellationToken);
            }

            // Clean up ifb0 if it exists
            var removeIfb = new PlatformCommand
            {
                FileName = "tc",
                Arguments = "qdisc del dev ifb0 root",
                UseSudo = true,
                TimeoutMs = 5000
            };
            await _commandRunner.RunAsync(removeIfb, cancellationToken);
        }
        catch
        {
            // Best effort cleanup - don't fail if nothing to clean
        }
    }

    /// <summary>
    /// Get current traffic shaping status for an interface
    /// </summary>
    public async Task<TrafficShaperStatus?> GetStatusAsync(
        string iface,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var command = new PlatformCommand
            {
                FileName = "tc",
                Arguments = $"qdisc show dev {iface}",
                UseSudo = false,
                TimeoutMs = 5000
            };

            var result = await _commandRunner.RunAsync(command, cancellationToken);

            if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StdOut))
            {
                return null;
            }

            return new TrafficShaperStatus
            {
                Interface = iface,
                IsActive = result.StdOut.Contains("htb") || result.StdOut.Contains("fq_codel"),
                Details = result.StdOut
            };
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Result of applying traffic shaping rules
/// </summary>
public sealed class TrafficShaperApplyResult
{
    public bool Success { get; set; }
    public int AppliedCount { get; set; }
    public int SkippedCount { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Status of traffic shaping on an interface
/// </summary>
public sealed class TrafficShaperStatus
{
    public string Interface { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string Details { get; set; } = string.Empty;
}

