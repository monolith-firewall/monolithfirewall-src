using Monolith.FireWall.Common.Services;
using Monolith.FireWall.Core.Models;
using Monolith.FireWall.Core.Services.Platform;
using Monolith.FireWall.Platform.Validation;
using System.Text.Json;

namespace Monolith.FireWall.Core.Services;

public sealed class InterfaceAssignmentManager
{
    private readonly InterfaceAssignmentStore _store;
    private readonly NetworkInventoryService _inventory;
    private readonly InterfaceConfigManager _configManager;
    private readonly PlatformCommandRunner _commandRunner;
    private readonly SystemSettingsManager _settingsManager;
    private readonly SystemTuneablesManager? _tuneablesManager;
    private readonly LoggingManager _loggingManager;

    public InterfaceAssignmentManager(
        InterfaceAssignmentStore store,
        NetworkInventoryService inventory,
        InterfaceConfigManager configManager,
        PlatformCommandRunner commandRunner,
        SystemSettingsManager settingsManager,
        SystemTuneablesManager? tuneablesManager = null)
    {
        _store = store;
        _inventory = inventory;
        _configManager = configManager;
        _commandRunner = commandRunner;
        _settingsManager = settingsManager;
        _tuneablesManager = tuneablesManager;
        _loggingManager = LoggingManager.Instance;
    }

    public async Task<InterfaceAssignmentsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        var assignments = await _store.GetAssignmentsAsync();
        var interfaces = await _inventory.ListInterfacesAsync();
        var addresses = await _inventory.ListAddressesAsync(null, cancellationToken);

        var ifaceMap = interfaces.ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase);
        var addressMap = addresses
            .GroupBy(a => a.Interface, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var views = assignments.Select(a => BuildAssignmentView(a, ifaceMap, addressMap)).ToList();
        var assignedIfaces = new HashSet<string>(assignments.Select(a => a.InterfaceName), StringComparer.OrdinalIgnoreCase);

        // Read unmanaged interfaces (only those not already assigned)
        var unmanagedStanzas = await _configManager.ReadUnmanagedInterfacesAsync(cancellationToken);
        var unmanagedViews = new List<InterfaceAssignmentView>();
        
        foreach (var stanza in unmanagedStanzas)
        {
            // Skip if already assigned (managed)
            if (assignedIfaces.Contains(stanza.Interface))
            {
                continue;
            }
            
            if (ifaceMap.TryGetValue(stanza.Interface, out var iface))
            {
                var view = BuildUnmanagedView(stanza, iface, addressMap);
                unmanagedViews.Add(view);
                assignedIfaces.Add(stanza.Interface); // Exclude from unassigned
            }
        }

        // Combine managed and unmanaged views
        var allAssigned = views.Where(v => v.Type == "physical").Concat(unmanagedViews).ToList();

        var unassigned = interfaces
            .Where(i => !assignedIfaces.Contains(i.Name))
            .Where(i => IsPhysicalInterface(i.Name))
            .Select(i =>
            {
                var ipDisplay = ResolvePrimaryAddress(addressMap, i.Name);
                return new InterfaceInventoryView
                {
                    Interface = i.Name,
                    MacAddress = i.MacAddress,
                    Status = i.IsUp ? "up" : "down",
                    IpAddress = ipDisplay
                };
            })
            .OrderBy(i => i.Interface, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new InterfaceAssignmentsSnapshot
        {
            Assigned = allAssigned,
            Vlans = views.Where(v => v.Type == "vlan").ToList(),
            Bridges = views.Where(v => v.Type == "bridge").ToList(),
            Unassigned = unassigned,
            ManagedFile = _configManager.ManagedPath
        };
    }

    public async Task<(bool Success, string? Error, InterfaceAssignmentEntity? Assignment)> SaveAssignmentAsync(
        InterfaceAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return (false, "Request is required", null);
        }

        if (string.IsNullOrWhiteSpace(request.Type))
        {
            return (false, "Assignment type is required", null);
        }

        var type = ParseType(request.Type);
        if (type == null)
        {
            return (false, "Invalid assignment type", null);
        }

        var inventory = await _inventory.ListInterfacesAsync();
        var inventorySet = new HashSet<string>(inventory.Select(i => i.Name), StringComparer.OrdinalIgnoreCase);

        var ifaceName = request.Interface?.Trim();
        var parentInterface = request.ParentInterface?.Trim();
        var vlanId = request.VlanId;

        if (type == InterfaceAssignmentType.Physical)
        {
            if (string.IsNullOrWhiteSpace(ifaceName))
            {
                return (false, "Interface is required", null);
            }

            if (!PlatformValidators.IsValidInterfaceName(ifaceName))
            {
                return (false, "Invalid interface name", null);
            }

            if (!inventorySet.Contains(ifaceName))
            {
                return (false, "Interface not found", null);
            }
        }
        else if (type == InterfaceAssignmentType.Vlan)
        {
            if (string.IsNullOrWhiteSpace(parentInterface))
            {
                return (false, "Parent interface is required", null);
            }

            if (!PlatformValidators.IsValidInterfaceName(parentInterface))
            {
                return (false, "Invalid parent interface", null);
            }

            if (!inventorySet.Contains(parentInterface))
            {
                return (false, "Parent interface not found", null);
            }

            if (!vlanId.HasValue || vlanId < 1 || vlanId > 4094)
            {
                return (false, "VLAN ID must be between 1 and 4094", null);
            }

            ifaceName = $"{parentInterface}.{vlanId.Value}";
        }
        else if (type == InterfaceAssignmentType.Bridge)
        {
            if (string.IsNullOrWhiteSpace(ifaceName))
            {
                return (false, "Bridge name is required", null);
            }

            if (!PlatformValidators.IsValidInterfaceName(ifaceName))
            {
                return (false, "Invalid bridge name", null);
            }

            var ports = request.BridgePorts?.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).ToList() ?? new List<string>();
            if (ports.Count == 0)
            {
                return (false, "Bridge ports are required", null);
            }

            foreach (var port in ports)
            {
                if (!PlatformValidators.IsValidInterfaceName(port))
                {
                    return (false, $"Invalid bridge port: {port}", null);
                }

                if (!inventorySet.Contains(port))
                {
                    return (false, $"Bridge port not found: {port}", null);
                }
            }
        }

        if (string.IsNullOrWhiteSpace(ifaceName))
        {
            return (false, "Interface name could not be resolved", null);
        }

        var ipMode = ParseIpMode(request.IpMode);
        if (type == InterfaceAssignmentType.Physical && ipMode == InterfaceIpMode.None)
        {
            ipMode = InterfaceIpMode.Dhcp;
        }
        var (address, prefix, ipError) = ParseIp(request);
        if (!string.IsNullOrWhiteSpace(ipError))
        {
            return (false, ipError, null);
        }

        if (ipMode == InterfaceIpMode.Static && string.IsNullOrWhiteSpace(address))
        {
            return (false, "Static IP requires an address and prefix", null);
        }

        if (ipMode != InterfaceIpMode.Static)
        {
            address = null;
            prefix = null;
        }
        else if (!string.IsNullOrWhiteSpace(request.Gateway) && !PlatformValidators.IsValidIp(request.Gateway))
        {
            return (false, "Invalid gateway address", null);
        }

        var ipv6Mode = ParseIpMode(request.Ipv6Mode);
        var (ipv6Address, ipv6Prefix, ipv6Error) = ParseIpv6(request);
        if (!string.IsNullOrWhiteSpace(ipv6Error))
        {
            return (false, ipv6Error, null);
        }

        if (ipv6Mode == InterfaceIpMode.Static && string.IsNullOrWhiteSpace(ipv6Address))
        {
            return (false, "Static IPv6 requires an address and prefix", null);
        }

        if (ipv6Mode != InterfaceIpMode.Static)
        {
            ipv6Address = null;
            ipv6Prefix = null;
        }
        else if (!string.IsNullOrWhiteSpace(request.Ipv6Gateway) && !PlatformValidators.IsValidIpv6(request.Ipv6Gateway))
        {
            return (false, "Invalid IPv6 gateway address", null);
        }

        var existingAssignment = await _store.GetAssignmentAsync(ifaceName);
        if (existingAssignment != null && existingAssignment.Type != type.Value)
        {
            return (false, "Interface already assigned with a different type", null);
        }

        var assignment = existingAssignment ?? new InterfaceAssignmentEntity
        {
            InterfaceName = ifaceName
        };

        assignment.Type = type.Value;
        var defaultName = ifaceName;
        if (type == InterfaceAssignmentType.Vlan && vlanId.HasValue)
        {
            defaultName = $"VLAN {vlanId.Value}";
        }
        else if (type == InterfaceAssignmentType.Bridge)
        {
            defaultName = $"Bridge {ifaceName}";
        }

        assignment.Name = string.IsNullOrWhiteSpace(request.Name) ? defaultName : request.Name.Trim();
        assignment.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        assignment.IpMode = ipMode;
        assignment.Ipv6Mode = ipv6Mode;
        assignment.Role = ResolveRole(request.Role, existingAssignment?.Role);
        assignment.IsManagement = request.IsManagement ?? existingAssignment?.IsManagement ?? false;
        assignment.IpAddress = address;
        assignment.PrefixLength = prefix;
        assignment.Gateway = ipMode == InterfaceIpMode.Static && !string.IsNullOrWhiteSpace(request.Gateway)
            ? request.Gateway.Trim()
            : null;
        assignment.Ipv6Address = ipv6Address;
        assignment.Ipv6PrefixLength = ipv6Prefix;
        assignment.Ipv6Gateway = ipv6Mode == InterfaceIpMode.Static && !string.IsNullOrWhiteSpace(request.Ipv6Gateway)
            ? request.Ipv6Gateway.Trim()
            : null;
        assignment.Ipv6AcceptRa = request.Ipv6AcceptRa ?? existingAssignment?.Ipv6AcceptRa ?? false;
        assignment.Ipv6Autoconf = request.Ipv6Autoconf ?? existingAssignment?.Ipv6Autoconf ?? false;
        assignment.ParentInterface = parentInterface;
        assignment.VlanId = vlanId;
        assignment.BridgePorts = request.BridgePorts != null
            ? string.Join(',', request.BridgePorts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()))
            : null;
        assignment.BridgeStp = request.BridgeStp ?? false;
        assignment.BridgeForwardDelay = request.BridgeForwardDelay;
        assignment.UpdatedAt = DateTime.UtcNow;

        // Check if this interface exists in unmanaged interfaces and remove it from its file
        var unmanagedStanzas = await _configManager.ReadUnmanagedInterfacesAsync(cancellationToken);
        var existingStanza = unmanagedStanzas.FirstOrDefault(s => 
            string.Equals(s.Interface, ifaceName, StringComparison.OrdinalIgnoreCase));
        
        if (existingStanza != null && !string.IsNullOrWhiteSpace(existingStanza.FilePath))
        {
            // Remove the interface from its original file
            var removeResult = await _configManager.RemoveInterfaceFromFileAsync(existingStanza.FilePath, ifaceName, cancellationToken);
            if (!removeResult.Success)
            {
                await _loggingManager.LogSystemAsync(
                    "Network",
                    "warning",
                    "InterfaceAssignmentManager",
                    $"Assigned interface {ifaceName} but failed to remove from original file: {existingStanza.FilePath}",
                    new Dictionary<string, object>
                    {
                        ["interface"] = ifaceName,
                        ["file"] = existingStanza.FilePath,
                        ["error"] = removeResult.Error ?? "Unknown error"
                    });
            }
        }

        var saved = await _store.UpsertAsync(assignment);
        if (!saved)
        {
            return (false, "Failed to save assignment", null);
        }

        // Auto-enable IP forwarding if WAN and LAN interfaces are configured
        await EnsureIpForwardingEnabledAsync(cancellationToken);

        await _loggingManager.LogSystemAsync(
            "Network",
            "info",
            "InterfaceAssignmentManager",
            $"Saved interface assignment {assignment.InterfaceName}",
            new Dictionary<string, object>
            {
                ["interface"] = assignment.InterfaceName,
                ["type"] = assignment.Type.ToString(),
                ["role"] = assignment.Role.ToString()
            });

        return (true, null, assignment);
    }

    public async Task<(bool Success, string? Error)> DeleteAssignmentAsync(string iface, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(iface))
        {
            return (false, "Interface is required");
        }

        var assignment = await _store.GetAssignmentAsync(iface);
        if (assignment == null)
        {
            return (true, null);
        }

        var exportResult = await _configManager.ExportAssignmentToUnmanagedAsync(assignment, cancellationToken);
        if (!exportResult.Success)
        {
            return (false, exportResult.Error ?? "Failed to export assignment");
        }

        var deleted = await _store.DeleteAsync(iface);
        if (!deleted)
        {
            return (false, "Failed to delete assignment");
        }

        await _loggingManager.LogSystemAsync(
            "Network",
            "warning",
            "InterfaceAssignmentManager",
            $"Removed interface assignment {iface}",
            new Dictionary<string, object>
            {
                ["interface"] = iface,
                ["exportedTo"] = _configManager.UnmanagedPath,
                ["backup"] = exportResult.BackupFile ?? string.Empty
            });

        return (true, null);
    }

    /// <summary>
    /// Delete an unmanaged interface (remove from unmanaged file).
    /// </summary>
    public async Task<(bool Success, string? Error)> DeleteUnmanagedInterfaceAsync(string iface, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(iface))
        {
            return (false, "Interface is required");
        }

        var unmanagedStanzas = await _configManager.ReadUnmanagedInterfacesAsync(cancellationToken);
        var targetStanza = unmanagedStanzas.FirstOrDefault(s => 
            string.Equals(s.Interface, iface, StringComparison.OrdinalIgnoreCase));

        if (targetStanza == null)
        {
            return (false, "Interface not found in unmanaged interfaces");
        }

        // Remove the unmanaged block from the file
        var unmanagedPath = _configManager.UnmanagedPath;
        if (!File.Exists(unmanagedPath))
        {
            return (true, null); // Already removed
        }

        try
        {
            var lines = (await File.ReadAllLinesAsync(unmanagedPath, cancellationToken)).ToList();
            var updated = RemoveUnmanagedBlock(lines, iface, out var removed);

            if (!removed)
            {
                return (false, "Interface block not found in unmanaged file");
            }

            // Write back the file (or delete if empty)
            if (updated.All(string.IsNullOrWhiteSpace) || updated.Count == 0)
            {
                File.Delete(unmanagedPath);
            }
            else
            {
                await File.WriteAllLinesAsync(unmanagedPath, updated, cancellationToken);
            }

            await _loggingManager.LogSystemAsync(
                "Network",
                "info",
                "InterfaceAssignmentManager",
                $"Deleted unmanaged interface {iface}",
                new Dictionary<string, object>
                {
                    ["interface"] = iface
                });

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"Failed to delete unmanaged interface: {ex.Message}");
        }
    }

    /// <summary>
    /// Assign an unmanaged interface (read config and create assignment).
    /// </summary>
    public async Task<(bool Success, string? Error, InterfaceAssignmentEntity? Assignment)> AssignUnmanagedInterfaceAsync(
        string iface,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(iface))
        {
            return (false, "Interface is required", null);
        }

        // Check if already assigned
        var existing = await _store.GetAssignmentAsync(iface);
        if (existing != null)
        {
            return (false, "Interface is already assigned", null);
        }

        // Read unmanaged interfaces
        var unmanagedStanzas = await _configManager.ReadUnmanagedInterfacesAsync(cancellationToken);
        var stanza = unmanagedStanzas.FirstOrDefault(s => 
            string.Equals(s.Interface, iface, StringComparison.OrdinalIgnoreCase));

        if (stanza == null)
        {
            return (false, "Interface not found in unmanaged interfaces", null);
        }

        // Convert stanza to assignment
        var assignment = new InterfaceAssignmentEntity
        {
            InterfaceName = stanza.Interface,
            Name = stanza.Interface,
            Type = InterfaceAssignmentType.Physical,
            Description = "Imported from unmanaged configuration",
            UpdatedAt = DateTime.UtcNow
        };

        // Parse IP configuration
        if (stanza.Method == "static")
        {
            assignment.IpMode = InterfaceIpMode.Static;
            if (stanza.Options.TryGetValue("address", out var address))
            {
                assignment.IpAddress = address;
            }
            if (stanza.Options.TryGetValue("netmask", out var netmask))
            {
                assignment.PrefixLength = NetmaskToPrefixLength(netmask);
            }
            if (stanza.Options.TryGetValue("gateway", out var gateway))
            {
                assignment.Gateway = gateway;
            }
        }
        else if (stanza.Method == "dhcp")
        {
            assignment.IpMode = InterfaceIpMode.Dhcp;
        }
        else
        {
            assignment.IpMode = InterfaceIpMode.None;
        }

        // Save assignment
        var saved = await _store.UpsertAsync(assignment);
        if (!saved)
        {
            return (false, "Failed to save assignment", null);
        }

        // Remove from unmanaged file
        var deleteResult = await DeleteUnmanagedInterfaceAsync(iface, cancellationToken);
        if (!deleteResult.Success)
        {
            // Log warning but don't fail - assignment is saved
            await _loggingManager.LogSystemAsync(
                "Network",
                "warning",
                "InterfaceAssignmentManager",
                $"Assigned interface {iface} but failed to remove from unmanaged file",
                new Dictionary<string, object>
                {
                    ["interface"] = iface,
                    ["error"] = deleteResult.Error ?? "Unknown error"
                });
        }

        await _loggingManager.LogSystemAsync(
            "Network",
            "info",
            "InterfaceAssignmentManager",
            $"Assigned unmanaged interface {iface}",
            new Dictionary<string, object>
            {
                ["interface"] = iface,
                ["type"] = assignment.Type.ToString(),
                ["ipMode"] = assignment.IpMode.ToString()
            });

        return (true, null, assignment);
    }

    private static List<string> RemoveUnmanagedBlock(List<string> lines, string iface, out bool removed)
    {
        removed = false;
        var begin = $"# BEGIN MONOLITH UNMANAGED {iface}";
        var end = $"# END MONOLITH UNMANAGED {iface}";
        var output = new List<string>(lines.Count);
        var inBlock = false;

        foreach (var line in lines)
        {
            if (!inBlock && line.Trim().Equals(begin, StringComparison.OrdinalIgnoreCase))
            {
                inBlock = true;
                removed = true;
                continue;
            }

            if (inBlock)
            {
                if (line.Trim().Equals(end, StringComparison.OrdinalIgnoreCase))
                {
                    inBlock = false;
                }
                continue;
            }

            output.Add(line);
        }

        return output;
    }

    public async Task<InterfaceConfigCheckResult> CheckConfigAsync(CancellationToken cancellationToken)
    {
        var assignments = await _store.GetAssignmentsAsync();
        return await _configManager.CheckAsync(assignments, cancellationToken);
    }

    public async Task<InterfaceApplyResult> ApplyConfigAsync(CancellationToken cancellationToken)
    {
        var assignments = await _store.GetAssignmentsAsync();
        var dnsServers = await _settingsManager.GetDnsServersAsync();
        var applyResult = await _configManager.ApplyAsync(assignments, dnsServers, cancellationToken);
        if (applyResult.Success)
        {
            var appliedAt = DateTime.UtcNow;
            foreach (var assignment in assignments)
            {
                await _store.UpdateAppliedAsync(assignment.InterfaceName, appliedAt);
            }

            await UpdateWebUiBindingsAsync(assignments, cancellationToken);

            await _loggingManager.LogSystemAsync(
                "Network",
                "info",
                "InterfaceAssignmentManager",
                "Applied interface configuration",
                new Dictionary<string, object>
                {
                    ["count"] = assignments.Count,
                    ["managedFile"] = applyResult.ManagedFile
                });
        }

        return applyResult;
    }

    public async Task<InterfaceApplyNowResult> ApplyNowAsync(CancellationToken cancellationToken)
    {
        var commandPath = ResolveApplyCommand();
        if (string.IsNullOrWhiteSpace(commandPath))
        {
            return new InterfaceApplyNowResult
            {
                Success = false,
                Message = "ifreload is not available. Install ifupdown2 to apply interfaces."
            };
        }

        var command = new PlatformCommand
        {
            FileName = commandPath,
            Arguments = "-a",
            UseSudo = true,
            TimeoutMs = 20000
        };

        var result = await _commandRunner.RunAsync(command, cancellationToken);
        if (IsIfupdownBusy(result))
        {
            try
            {
                await Task.Delay(1500, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellation here; follow the original result.
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                result = await _commandRunner.RunAsync(command, cancellationToken);
            }
        }
        var success = !result.TimedOut && result.ExitCode == 0;
        var message = success
            ? "Interfaces reloaded via ifreload"
            : BuildApplyNowError(result);

        if (success)
        {
            await _loggingManager.LogSystemAsync(
                "Network",
                "info",
                "InterfaceAssignmentManager",
                "Applied interfaces with ifreload",
                new Dictionary<string, object>
                {
                    ["command"] = $"{command.FileName} {command.Arguments}".Trim(),
                    ["durationMs"] = result.DurationMs
                });
        }

        return new InterfaceApplyNowResult
        {
            Success = success,
            Message = message,
            Command = $"{command.FileName} {command.Arguments}".Trim(),
            ExitCode = result.ExitCode,
            TimedOut = result.TimedOut,
            StdOut = string.IsNullOrWhiteSpace(result.StdOut) ? null : result.StdOut.Trim(),
            StdErr = string.IsNullOrWhiteSpace(result.StdErr) ? null : result.StdErr.Trim()
        };
    }

    public async Task<InterfaceApplyResult> FixConfigAsync(CancellationToken cancellationToken)
    {
        var assignments = await _store.GetAssignmentsAsync();
        var (removedStanzas, _) = await _configManager.RemoveConflictsAsync(assignments, cancellationToken);
        var (changed, backup) = await _configManager.EnsureIncludeAsync(cancellationToken);
        var movedBackups = await _configManager.MoveLegacyBackupsAsync(cancellationToken);
        var applyResult = await ApplyConfigAsync(cancellationToken);
        if (applyResult.Success)
        {
            if (changed)
            {
                applyResult.BackupFile = backup;
            }

            if (changed || removedStanzas > 0 || movedBackups > 0)
            {
                var parts = new List<string>();
                if (removedStanzas > 0)
                {
                    parts.Add($"Removed {removedStanzas} conflicting stanza(s)");
                }

                if (changed)
                {
                    parts.Add("Fixed includes");
                }

                if (movedBackups > 0)
                {
                    parts.Add($"Moved {movedBackups} backup file(s)");
                }

                applyResult.Message = $"{string.Join(" and ", parts)} and wrote managed configuration";
            }
        }

        return applyResult;
    }

    private static InterfaceAssignmentView BuildAssignmentView(
        InterfaceAssignmentEntity assignment,
        IReadOnlyDictionary<string, Monolith.FireWall.Platform.Models.InterfaceInfo> ifaceMap,
        IReadOnlyDictionary<string, List<Monolith.FireWall.Platform.Models.AddressInfo>> addressMap)
    {
        var status = "unknown";
        if (ifaceMap.TryGetValue(assignment.InterfaceName, out var info))
        {
            status = info.IsUp ? "up" : "down";
        }

        var liveIpv4 = ResolveAddress(addressMap, assignment.InterfaceName, "inet");
        var liveIpv6 = ResolveAddress(addressMap, assignment.InterfaceName, "inet6");
        var ip = liveIpv4 ?? liveIpv6;

        var ports = assignment.BridgePorts != null
            ? assignment.BridgePorts.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
            : null;

        return new InterfaceAssignmentView
        {
            Interface = assignment.InterfaceName,
            Name = assignment.Name,
            Type = assignment.Type.ToString().ToLowerInvariant(),
            Description = assignment.Description,
            Status = status,
            IpAddress = ip,
            Managed = true,
            IsUnmanaged = false,
            SourceFile = "/etc/network/interfaces.d/monolith",
            IpMode = assignment.IpMode,
            Ipv6Mode = assignment.Ipv6Mode,
            Role = assignment.Role,
            IsManagement = assignment.IsManagement,
            ConfigAddress = assignment.IpAddress,
            ConfigPrefixLength = assignment.PrefixLength,
            Gateway = assignment.Gateway,
            Ipv6Address = assignment.Ipv6Address,
            Ipv6PrefixLength = assignment.Ipv6PrefixLength,
            Ipv6Gateway = assignment.Ipv6Gateway,
            Ipv6AcceptRa = assignment.Ipv6AcceptRa,
            Ipv6Autoconf = assignment.Ipv6Autoconf,
            ParentInterface = assignment.ParentInterface,
            VlanId = assignment.VlanId,
            BridgePorts = ports,
            BridgeStp = assignment.BridgeStp,
            BridgeForwardDelay = assignment.BridgeForwardDelay
        };
    }

    private InterfaceAssignmentView BuildUnmanagedView(
        InterfaceConfigManager.InterfaceStanza stanza,
        Monolith.FireWall.Platform.Models.InterfaceInfo iface,
        IReadOnlyDictionary<string, List<Monolith.FireWall.Platform.Models.AddressInfo>> addressMap)
    {
        var status = iface.IsUp ? "up" : "down";
        var liveIpv4 = ResolveAddress(addressMap, stanza.Interface, "inet");
        var liveIpv6 = ResolveAddress(addressMap, stanza.Interface, "inet6");
        var ip = liveIpv4 ?? liveIpv6;

        // Parse configuration from stanza options
        var ipMode = InterfaceIpMode.None;
        string? configAddress = null;
        int? configPrefixLength = null;
        string? gateway = null;

        if (stanza.Method == "static" && stanza.Options.TryGetValue("address", out var address))
        {
            ipMode = InterfaceIpMode.Static;
            configAddress = address;
            
            if (stanza.Options.TryGetValue("netmask", out var netmask))
            {
                configPrefixLength = NetmaskToPrefixLength(netmask);
            }
            
            if (stanza.Options.TryGetValue("gateway", out var gw))
            {
                gateway = gw;
            }
        }
        else if (stanza.Method == "dhcp")
        {
            ipMode = InterfaceIpMode.Dhcp;
        }

        return new InterfaceAssignmentView
        {
            Interface = stanza.Interface,
            Name = stanza.Interface,
            Type = "physical",
            Description = "Unmanaged interface",
            Status = status,
            IpAddress = ip,
            Managed = false,
            IsUnmanaged = true,
            SourceFile = _configManager.UnmanagedPath,
            IpMode = ipMode,
            Ipv6Mode = InterfaceIpMode.None,
            Role = InterfaceRole.Unknown,
            IsManagement = false,
            ConfigAddress = configAddress,
            ConfigPrefixLength = configPrefixLength,
            Gateway = gateway
        };
    }

    private static int? NetmaskToPrefixLength(string netmask)
    {
        if (string.IsNullOrWhiteSpace(netmask))
        {
            return null;
        }

        var parts = netmask.Split('.');
        if (parts.Length != 4)
        {
            return null;
        }

        if (!int.TryParse(parts[0], out var b0) ||
            !int.TryParse(parts[1], out var b1) ||
            !int.TryParse(parts[2], out var b2) ||
            !int.TryParse(parts[3], out var b3))
        {
            return null;
        }

        var mask = (uint)((b0 << 24) | (b1 << 16) | (b2 << 8) | b3);
        var prefix = 0;
        while (mask != 0 && (mask & 0x80000000) != 0)
        {
            prefix++;
            mask <<= 1;
        }

        return prefix;
    }

    private static InterfaceAssignmentType? ParseType(string value)
    {
        if (Enum.TryParse<InterfaceAssignmentType>(value, true, out var parsed))
        {
            return parsed;
        }

        return value.ToLowerInvariant() switch
        {
            "physical" => InterfaceAssignmentType.Physical,
            "vlan" => InterfaceAssignmentType.Vlan,
            "bridge" => InterfaceAssignmentType.Bridge,
            _ => null
        };
    }

    private static InterfaceIpMode ParseIpMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return InterfaceIpMode.None;
        }

        if (Enum.TryParse<InterfaceIpMode>(value, true, out var parsed))
        {
            return parsed;
        }

        return value.ToLowerInvariant() switch
        {
            "dhcp" => InterfaceIpMode.Dhcp,
            "static" => InterfaceIpMode.Static,
            "manual" => InterfaceIpMode.None,
            "none" => InterfaceIpMode.None,
            _ => InterfaceIpMode.None
        };
    }

    private static InterfaceRole ResolveRole(string? value, InterfaceRole? existing)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return existing ?? InterfaceRole.Opt;
        }

        if (Enum.TryParse<InterfaceRole>(value, true, out var parsed))
        {
            return parsed;
        }

        return value.ToLowerInvariant() switch
        {
            "lan" => InterfaceRole.Lan,
            "wan" => InterfaceRole.Wan,
            "opt" => InterfaceRole.Opt,
            _ => existing ?? InterfaceRole.Opt
        };
    }

    private static string? ResolveApplyCommand()
    {
        var candidates = new[]
        {
            "/usr/sbin/ifreload",
            "/sbin/ifreload"
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string BuildApplyNowError(Monolith.FireWall.Platform.Models.PlatformCommandResult result)
    {
        if (result.TimedOut)
        {
            return "Interface reload timed out";
        }

        if (IsIfupdownBusy(result))
        {
            return "ifupdown2 is already running. Try again in a few seconds.";
        }

        var error = string.IsNullOrWhiteSpace(result.StdErr) ? "Interface reload failed" : result.StdErr.Trim();
        return $"{error} (exit {result.ExitCode})";
    }

    private static bool IsIfupdownBusy(Monolith.FireWall.Platform.Models.PlatformCommandResult result)
    {
        if (result.ExitCode == 89)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(result.StdErr)
               && result.StdErr.Contains("already running", StringComparison.OrdinalIgnoreCase);
    }

    private async Task UpdateWebUiBindingsAsync(
        IReadOnlyCollection<InterfaceAssignmentEntity> assignments,
        CancellationToken cancellationToken)
    {
        const string bindingsPath = "/etc/monolith-firewall/webui-bindings.json";
        var managementInterfaces = assignments
            .Where(a => a.IsManagement)
            .Select(a => a.InterfaceName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (managementInterfaces.Count == 0)
        {
            if (File.Exists(bindingsPath))
            {
                File.Delete(bindingsPath);
            }
            return;
        }

        var addresses = await _inventory.ListAddressesAsync(null, cancellationToken);
        var managementAddresses = addresses
            .Where(a => managementInterfaces.Contains(a.Interface, StringComparer.OrdinalIgnoreCase))
            .Where(a => string.Equals(a.Family, "inet", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(a.Family, "inet6", StringComparison.OrdinalIgnoreCase))
            .Select(a => a.Address)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (managementAddresses.Count == 0)
        {
            if (File.Exists(bindingsPath))
            {
                File.Delete(bindingsPath);
            }
            return;
        }

        var payload = new WebUiBindings
        {
            Addresses = managementAddresses,
            GeneratedAt = DateTime.UtcNow
        };

        var directory = Path.GetDirectoryName(bindingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(bindingsPath, json, cancellationToken);
    }

    private sealed class WebUiBindings
    {
        public List<string> Addresses { get; set; } = new();
        public DateTime GeneratedAt { get; set; }
    }

    private static (string? Address, int? Prefix, string? Error) ParseIp(InterfaceAssignmentRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.AddressCidr))
        {
            if (!PlatformValidators.TryParseCidr(request.AddressCidr, out var addr, out var prefix))
            {
                return (null, null, "Invalid address CIDR");
            }

            return (addr.ToString(), prefix, null);
        }

        if (!string.IsNullOrWhiteSpace(request.Address))
        {
            if (!PlatformValidators.IsValidIp(request.Address))
            {
                return (null, null, "Invalid IP address");
            }

            if (!request.PrefixLength.HasValue)
            {
                return (null, null, "Prefix length is required for static IP");
            }

            return (request.Address, request.PrefixLength, null);
        }

        return (null, null, null);
    }

    private static (string? Address, int? Prefix, string? Error) ParseIpv6(InterfaceAssignmentRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Ipv6Address))
        {
            if (!PlatformValidators.IsValidIpv6(request.Ipv6Address))
            {
                return (null, null, "Invalid IPv6 address");
            }

            if (!request.Ipv6PrefixLength.HasValue || request.Ipv6PrefixLength.Value < 0 || request.Ipv6PrefixLength.Value > 128)
            {
                return (null, null, "IPv6 prefix length must be between 0 and 128");
            }

            return (request.Ipv6Address.Trim(), request.Ipv6PrefixLength.Value, null);
        }

        return (null, null, null);
    }

    private static string? ResolvePrimaryAddress(IReadOnlyDictionary<string, List<Monolith.FireWall.Platform.Models.AddressInfo>> addressMap, string iface)
    {
        if (!addressMap.TryGetValue(iface, out var list) || list.Count == 0)
        {
            return null;
        }

        var ipv4 = list.FirstOrDefault(a => string.Equals(a.Family, "inet", StringComparison.OrdinalIgnoreCase));
        if (ipv4 != null)
        {
            return $"{ipv4.Address}/{ipv4.PrefixLength}";
        }

        var ipv6 = list.FirstOrDefault(a => string.Equals(a.Family, "inet6", StringComparison.OrdinalIgnoreCase));
        return ipv6 != null ? $"{ipv6.Address}/{ipv6.PrefixLength}" : null;
    }

    private static string? ResolveAddress(IReadOnlyDictionary<string, List<Monolith.FireWall.Platform.Models.AddressInfo>> addressMap, string iface, string family)
    {
        if (!addressMap.TryGetValue(iface, out var list))
        {
            return null;
        }

        var match = list.FirstOrDefault(a => string.Equals(a.Family, family, StringComparison.OrdinalIgnoreCase));
        return match != null ? $"{match.Address}/{match.PrefixLength}" : null;
    }

    private static bool IsPhysicalInterface(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (string.Equals(name, "lo", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (name.Contains('.', StringComparison.Ordinal))
        {
            return false;
        }

        if (name.StartsWith("br", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("vlan", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("veth", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Automatically enables IP forwarding if both WAN and LAN interfaces are configured.
    /// This is required for routing between networks.
    /// </summary>
    private async Task EnsureIpForwardingEnabledAsync(CancellationToken cancellationToken)
    {
        if (_tuneablesManager == null)
        {
            return; // Tuneables manager not available
        }

        try
        {
            var assignments = await _store.GetAssignmentsAsync();
            var hasWan = assignments.Any(a => a.Role == InterfaceRole.Wan);
            var hasLan = assignments.Any(a => a.Role == InterfaceRole.Lan);

            if (hasWan && hasLan)
            {
                // Check current IP forwarding status
                var currentValue = await _tuneablesManager.GetTuneablesAsync(cancellationToken);
                var ipForward = currentValue.FirstOrDefault(t => t.Key == "net.ipv4.ip_forward");
                
                if (ipForward != null && ipForward.CurrentValue != "1")
                {
                    // Enable IPv4 forwarding
                    var applyRequest = new TuneableApplyRequest
                    {
                        Items = new List<TuneableUpdate>
                        {
                            new TuneableUpdate { Key = "net.ipv4.ip_forward", Value = "1" }
                        }
                    };

                    var result = await _tuneablesManager.ApplyAsync(applyRequest, cancellationToken);
                    if (result.Success)
                    {
                        await _loggingManager.LogSystemAsync(
                            "Network",
                            "info",
                            "InterfaceAssignmentManager",
                            "Auto-enabled IPv4 forwarding for WAN/LAN routing",
                            new Dictionary<string, object>
                            {
                                ["wanInterfaces"] = assignments.Where(a => a.Role == InterfaceRole.Wan).Select(a => a.InterfaceName).ToList(),
                                ["lanInterfaces"] = assignments.Where(a => a.Role == InterfaceRole.Lan).Select(a => a.InterfaceName).ToList()
                            });
                    }
                    else
                    {
                        await _loggingManager.LogSystemAsync(
                            "Network",
                            "warning",
                            "InterfaceAssignmentManager",
                            "Failed to auto-enable IPv4 forwarding",
                            new Dictionary<string, object>
                            {
                                ["error"] = result.Error ?? "Unknown error"
                            });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Log but don't fail the interface assignment
            await _loggingManager.LogSystemAsync(
                "Network",
                "error",
                "InterfaceAssignmentManager",
                "Error checking/enabling IP forwarding",
                new Dictionary<string, object>
                {
                    ["error"] = ex.Message
                });
        }
    }
}
