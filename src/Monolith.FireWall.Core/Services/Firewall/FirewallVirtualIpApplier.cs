using System.Text;
using Monolith.FireWall.Common.Services;
using Monolith.FireWall.Core.Models;
using Monolith.FireWall.Core.Services.Platform;
using Monolith.FireWall.Platform.Models;

namespace Monolith.FireWall.Core.Services.Firewall;

/// <summary>
/// Applies virtual IP configurations to the system
/// Supports IP aliasing, CARP (via ucarp), and proxy ARP
/// </summary>
public sealed class FirewallVirtualIpApplier
{
    private readonly PlatformCommandRunner _commandRunner;
    private readonly LoggingManager _loggingManager;
    private readonly InterfaceAssignmentStore _interfaceStore;

    public FirewallVirtualIpApplier(
        PlatformCommandRunner commandRunner,
        InterfaceAssignmentStore interfaceStore)
    {
        _commandRunner = commandRunner;
        _interfaceStore = interfaceStore;
        _loggingManager = LoggingManager.Instance;
    }

    /// <summary>
    /// Apply all virtual IPs to the system
    /// This should be called BEFORE firewall rules are applied
    /// </summary>
    public async Task<VirtualIpApplyResult> ApplyAsync(
        List<VirtualIpView> virtualIps,
        CancellationToken cancellationToken = default)
    {
        var result = new VirtualIpApplyResult
        {
            Success = true,
            AppliedCount = 0,
            SkippedCount = 0,
            Errors = new List<string>()
        };

        // Get all interface assignments for validation
        var assignments = await _interfaceStore.GetAssignmentsAsync();
        var validInterfaces = assignments.Select(a => a.InterfaceName).ToHashSet();

        // First, remove all existing virtual IPs (cleanup)
        await RemoveExistingVirtualIpsAsync(cancellationToken);

        // Apply each enabled virtual IP
        foreach (var vip in virtualIps.Where(v => v.Enabled).OrderBy(v => v.Id))
        {
            try
            {
                // Validate interface exists
                if (!validInterfaces.Contains(vip.Interface))
                {
                    result.Errors.Add($"VIP #{vip.Id}: Interface '{vip.Interface}' does not exist");
                    result.SkippedCount++;
                    continue;
                }

                // Validate IP address format
                if (string.IsNullOrWhiteSpace(vip.Address))
                {
                    result.Errors.Add($"VIP #{vip.Id}: Address is required");
                    result.SkippedCount++;
                    continue;
                }

                // Apply based on mode
                var (success, error) = vip.Mode.ToLowerInvariant() switch
                {
                    "ipalias" => await ApplyIpAliasAsync(vip, cancellationToken),
                    "carp" => await ApplyCarpAsync(vip, cancellationToken),
                    "proxyarp" => await ApplyProxyArpAsync(vip, cancellationToken),
                    _ => (false, $"Unknown VIP mode: {vip.Mode}")
                };

                if (success)
                {
                    result.AppliedCount++;
                    await _loggingManager.LogSecurityAsync(
                        "Firewall",
                        "Info",
                        "VirtualIp",
                        $"Applied virtual IP {vip.Address} on {vip.Interface} (mode: {vip.Mode})",
                        details: new Dictionary<string, object>
                        {
                            ["vipId"] = vip.Id,
                            ["interface"] = vip.Interface,
                            ["address"] = vip.Address,
                            ["mode"] = vip.Mode
                        });
                }
                else
                {
                    result.Errors.Add($"VIP #{vip.Id}: {error}");
                    result.SkippedCount++;
                    result.Success = false;
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"VIP #{vip.Id}: {ex.Message}");
                result.SkippedCount++;
                result.Success = false;
            }
        }

        if (result.Errors.Count > 0)
        {
            await _loggingManager.LogSecurityAsync(
                "Firewall",
                "Warning",
                "VirtualIp",
                $"Virtual IP application completed with {result.Errors.Count} error(s)",
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
    /// Apply a simple IP alias to an interface
    /// Uses 'ip addr add' command
    /// </summary>
    private async Task<(bool Success, string? Error)> ApplyIpAliasAsync(
        VirtualIpView vip,
        CancellationToken cancellationToken)
    {
        try
        {
            // Determine subnet mask (default to /32 for single IP if not specified)
            var subnet = string.IsNullOrWhiteSpace(vip.Subnet) ? "32" : vip.Subnet;

            // Add IP address to interface
            // Format: ip addr add <address>/<subnet> dev <interface>
            var command = new PlatformCommand
            {
                FileName = "ip",
                Arguments = $"addr add {vip.Address}/{subnet} dev {vip.Interface}",
                UseSudo = true,
                TimeoutMs = 5000
            };

            var result = await _commandRunner.RunAsync(command, cancellationToken);

            if (result.ExitCode != 0)
            {
                // Check if address already exists (not necessarily an error)
                if (result.StdErr?.Contains("RTNETLINK answers: File exists") == true)
                {
                    // Address already exists, consider this a success
                    return (true, null);
                }

                return (false, result.StdErr?.Trim() ?? $"ip addr add failed with exit code {result.ExitCode}");
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Apply CARP (Common Address Redundancy Protocol) using ucarp
    /// Requires ucarp package to be installed
    /// </summary>
    private async Task<(bool Success, string? Error)> ApplyCarpAsync(
        VirtualIpView vip,
        CancellationToken cancellationToken)
    {
        try
        {
            // Check if ucarp is installed
            if (!_commandRunner.CommandExists("ucarp"))
            {
                return (false, "ucarp is not installed. Install with: apt-get install ucarp");
            }

            // Parse CARP-specific fields
            var vhid = vip.VhId ?? 1;
            var password = vip.CarpPassword ?? "monolith";
            var advskew = vip.AdvSkew ?? 0;
            var subnet = string.IsNullOrWhiteSpace(vip.Subnet) ? "32" : vip.Subnet;

            // Create ucarp startup script
            var scriptPath = $"/etc/monolith-firewall/ucarp-{vip.Interface}-{vhid}.sh";
            var scriptDir = Path.GetDirectoryName(scriptPath);
            if (!string.IsNullOrEmpty(scriptDir))
            {
                Directory.CreateDirectory(scriptDir);
            }

            var upScript = new StringBuilder();
            upScript.AppendLine("#!/bin/sh");
            upScript.AppendLine($"# CARP Virtual IP - {vip.Address} on {vip.Interface}");
            upScript.AppendLine($"ip addr add {vip.Address}/{subnet} dev {vip.Interface}");

            await File.WriteAllTextAsync(scriptPath + ".up", upScript.ToString(), cancellationToken);

            var downScript = new StringBuilder();
            downScript.AppendLine("#!/bin/sh");
            downScript.AppendLine($"# CARP Virtual IP - {vip.Address} on {vip.Interface}");
            downScript.AppendLine($"ip addr del {vip.Address}/{subnet} dev {vip.Interface}");

            await File.WriteAllTextAsync(scriptPath + ".down", downScript.ToString(), cancellationToken);

            // Make scripts executable
            await _commandRunner.RunAsync(new PlatformCommand
            {
                FileName = "chmod",
                Arguments = $"+x {scriptPath}.up {scriptPath}.down",
                UseSudo = true,
                TimeoutMs = 2000
            }, cancellationToken);

            // Start ucarp daemon
            // Format: ucarp -i <interface> -v <vhid> -p <password> -a <address> -s <advskew> -u <upscript> -d <downscript> -B
            var ucarpCommand = new PlatformCommand
            {
                FileName = "ucarp",
                Arguments = $"-i {vip.Interface} -v {vhid} -p {password} -a {vip.Address} -s {advskew} -u {scriptPath}.up -d {scriptPath}.down -B",
                UseSudo = true,
                TimeoutMs = 5000
            };

            var result = await _commandRunner.RunAsync(ucarpCommand, cancellationToken);

            if (result.ExitCode != 0)
            {
                return (false, result.StdErr?.Trim() ?? $"ucarp failed with exit code {result.ExitCode}");
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Apply Proxy ARP for the virtual IP
    /// Makes the firewall respond to ARP requests for the IP
    /// </summary>
    private async Task<(bool Success, string? Error)> ApplyProxyArpAsync(
        VirtualIpView vip,
        CancellationToken cancellationToken)
    {
        try
        {
            // Add proxy ARP entry
            // Format: ip neigh add proxy <address> dev <interface>
            var command = new PlatformCommand
            {
                FileName = "ip",
                Arguments = $"neigh add proxy {vip.Address} dev {vip.Interface}",
                UseSudo = true,
                TimeoutMs = 5000
            };

            var result = await _commandRunner.RunAsync(command, cancellationToken);

            if (result.ExitCode != 0)
            {
                // Check if entry already exists
                if (result.StdErr?.Contains("RTNETLINK answers: File exists") == true)
                {
                    return (true, null);
                }

                return (false, result.StdErr?.Trim() ?? $"ip neigh add proxy failed with exit code {result.ExitCode}");
            }

            // Enable proxy ARP on the interface
            var proxyArpCommand = new PlatformCommand
            {
                FileName = "sysctl",
                Arguments = $"-w net.ipv4.conf.{vip.Interface}.proxy_arp=1",
                UseSudo = true,
                TimeoutMs = 2000
            };

            await _commandRunner.RunAsync(proxyArpCommand, cancellationToken);

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Remove all existing virtual IPs from all interfaces
    /// This ensures clean state before applying new configuration
    /// </summary>
    private async Task RemoveExistingVirtualIpsAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Kill all ucarp processes
            var killUcarp = new PlatformCommand
            {
                FileName = "pkill",
                Arguments = "-9 ucarp",
                UseSudo = true,
                TimeoutMs = 5000
            };
            await _commandRunner.RunAsync(killUcarp, cancellationToken);

            // Note: We don't remove IP aliases because we don't know which ones we added
            // The firewall will recreate all VIPs on next apply
            // TODO: Track applied VIPs in a state file for proper cleanup
        }
        catch
        {
            // Best effort cleanup - don't fail if nothing to clean
        }
    }

    /// <summary>
    /// Get status of applied virtual IPs
    /// </summary>
    public async Task<List<VirtualIpStatus>> GetStatusAsync(
        List<VirtualIpView> virtualIps,
        CancellationToken cancellationToken = default)
    {
        var statuses = new List<VirtualIpStatus>();

        foreach (var vip in virtualIps)
        {
            var status = new VirtualIpStatus
            {
                VipId = vip.Id,
                Address = vip.Address,
                Interface = vip.Interface,
                Mode = vip.Mode,
                IsApplied = false
            };

            try
            {
                if (vip.Mode.ToLowerInvariant() == "ipalias" || vip.Mode.ToLowerInvariant() == "carp")
                {
                    // Check if IP is assigned to interface
                    var command = new PlatformCommand
                    {
                        FileName = "ip",
                        Arguments = $"addr show dev {vip.Interface}",
                        UseSudo = false,
                        TimeoutMs = 5000
                    };

                    var result = await _commandRunner.RunAsync(command, cancellationToken);
                    status.IsApplied = result.StdOut?.Contains(vip.Address) == true;
                }
                else if (vip.Mode.ToLowerInvariant() == "proxyarp")
                {
                    // Check if proxy ARP entry exists
                    var command = new PlatformCommand
                    {
                        FileName = "ip",
                        Arguments = $"neigh show proxy | grep {vip.Address}",
                        UseSudo = true,
                        TimeoutMs = 5000
                    };

                    var result = await _commandRunner.RunAsync(command, cancellationToken);
                    status.IsApplied = !string.IsNullOrWhiteSpace(result.StdOut);
                }
            }
            catch
            {
                status.IsApplied = false;
            }

            statuses.Add(status);
        }

        return statuses;
    }
}

/// <summary>
/// Result of applying virtual IPs
/// </summary>
public sealed class VirtualIpApplyResult
{
    public bool Success { get; set; }
    public int AppliedCount { get; set; }
    public int SkippedCount { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Status of a virtual IP on the system
/// </summary>
public sealed class VirtualIpStatus
{
    public int VipId { get; set; }
    public string Address { get; set; } = string.Empty;
    public string Interface { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public bool IsApplied { get; set; }
}

