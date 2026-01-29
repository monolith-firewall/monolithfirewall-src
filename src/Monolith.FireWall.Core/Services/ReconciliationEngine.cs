using Monolith.FireWall.Common.Interfaces;
using Monolith.FireWall.Common.Services;
using Monolith.FireWall.Core.Models;
using Monolith.FireWall.Core.Services.Platform;
using Monolith.FireWall.Platform.Models;

namespace Monolith.FireWall.Core.Services;

/// <summary>
/// Reconciliation engine that handles state drift between desired configuration
/// and operational reality. Implements auto-repair, notification, and manual
/// strategies based on the type of drift detected.
/// </summary>
public sealed class ReconciliationEngine : INetworkStateListener
{
    private readonly InterfaceOperationalStateStore _operationalStateStore;
    private readonly InterfaceAssignmentStore _assignmentStore;
    private readonly GatewayStore _gatewayStore;
    private readonly GatewayHealthStore _healthStore;
    private readonly NetworkStateChangeStore _changeStore;
    private readonly PlatformCommandRunner _commandRunner;
    private readonly LoggingManager _loggingManager;

    // Service references for triggering actions
    private readonly GatewayGroupManager? _gatewayGroupManager;

    // Configuration for reconciliation behavior
    private ReconciliationConfig _config = new();

    public ReconciliationEngine(
        InterfaceOperationalStateStore operationalStateStore,
        InterfaceAssignmentStore assignmentStore,
        GatewayStore gatewayStore,
        GatewayHealthStore healthStore,
        NetworkStateChangeStore changeStore,
        PlatformCommandRunner commandRunner,
        GatewayGroupManager? gatewayGroupManager = null)
    {
        _operationalStateStore = operationalStateStore;
        _assignmentStore = assignmentStore;
        _gatewayStore = gatewayStore;
        _healthStore = healthStore;
        _changeStore = changeStore;
        _commandRunner = commandRunner;
        _gatewayGroupManager = gatewayGroupManager;
        _loggingManager = LoggingManager.Instance;
    }

    /// <summary>
    /// Updates the reconciliation configuration.
    /// </summary>
    public void Configure(ReconciliationConfig config)
    {
        _config = config ?? new ReconciliationConfig();
    }

    // ========================================================================
    // INetworkStateListener Implementation (Common interface)
    // ========================================================================

    public async Task OnInterfaceStateChangedAsync(NetworkInterfaceChange change, CancellationToken cancellationToken)
    {
        switch (change.ChangeType)
        {
            case InterfaceChangeType.LinkUp:
                await HandleLinkUpAsync(change.InterfaceName, cancellationToken);
                break;

            case InterfaceChangeType.LinkDown:
                await HandleLinkDownAsync(change.InterfaceName, cancellationToken);
                break;

            case InterfaceChangeType.IpChanged:
            case InterfaceChangeType.IpAdded:
            case InterfaceChangeType.IpRemoved:
                await HandleIpChangeAsync(change, cancellationToken);
                break;

            case InterfaceChangeType.GatewayChanged:
                await HandleGatewayChangeAsync(change, cancellationToken);
                break;

            case InterfaceChangeType.InterfaceAdded:
                await HandleInterfaceAddedAsync(change.InterfaceName, cancellationToken);
                break;

            case InterfaceChangeType.InterfaceRemoved:
                await HandleInterfaceRemovedAsync(change.InterfaceName, cancellationToken);
                break;
        }
    }

    public async Task OnGatewayHealthChangedAsync(NetworkGatewayChange change, CancellationToken cancellationToken)
    {
        await _loggingManager.LogSystemAsync(
            "Network",
            change.NewStatus == "offline" ? "warning" : "info",
            "ReconciliationEngine",
            $"Gateway '{change.GatewayName}' health changed: {change.PreviousStatus} -> {change.NewStatus}",
            new Dictionary<string, object>
            {
                ["gatewayId"] = change.GatewayId,
                ["address"] = change.GatewayAddress,
                ["latencyMs"] = change.LatencyMs ?? 0,
                ["packetLoss"] = change.PacketLossPercent ?? 0
            });

        // Re-evaluate gateway groups when health changes
        if (_gatewayGroupManager != null)
        {
            await _gatewayGroupManager.EvaluateGroupsAsync(cancellationToken);
        }
    }

    public async Task OnLinkStateChangedAsync(NetworkLinkChange change, CancellationToken cancellationToken)
    {
        // Delegate to the appropriate handler
        if (change.IsUp)
        {
            await HandleLinkUpAsync(change.InterfaceName, cancellationToken);
        }
        else
        {
            await HandleLinkDownAsync(change.InterfaceName, cancellationToken);
        }
    }

    // ========================================================================
    // Event Handlers
    // ========================================================================

    private async Task HandleLinkUpAsync(string interfaceName, CancellationToken cancellationToken)
    {
        var assignment = await _assignmentStore.GetAssignmentAsync(interfaceName);
        if (assignment == null)
        {
            // Unmanaged interface - just notify
            await NotifyAsync(
                $"Unmanaged interface '{interfaceName}' link restored",
                "info",
                interfaceName);
            return;
        }

        // Check if this interface has DHCP - IP should restore automatically
        if (assignment.IpMode == InterfaceIpMode.Dhcp)
        {
            await NotifyAsync(
                $"DHCP interface '{assignment.Name}' link restored - waiting for IP",
                "info",
                interfaceName);
            return;
        }

        // Static interface - verify IP is still configured
        if (assignment.IpMode == InterfaceIpMode.Static)
        {
            var opState = await _operationalStateStore.GetAsync(interfaceName);
            if (opState?.CurrentIpv4Address != assignment.IpAddress)
            {
                // Static IP missing after link restore - auto-repair if configured
                if (_config.AutoRepairStaticIp)
                {
                    await AutoRepairStaticIpAsync(assignment, cancellationToken);
                }
                else
                {
                    await NotifyAsync(
                        $"Static interface '{assignment.Name}' link restored but IP mismatch - manual intervention required",
                        "warning",
                        interfaceName);
                }
            }
            else
            {
                await NotifyAsync(
                    $"Interface '{assignment.Name}' link restored with correct IP",
                    "info",
                    interfaceName);
            }
        }

        // Re-evaluate gateway groups
        if (_gatewayGroupManager != null)
        {
            await _gatewayGroupManager.EvaluateGroupsAsync(cancellationToken);
        }
    }

    private async Task HandleLinkDownAsync(string interfaceName, CancellationToken cancellationToken)
    {
        var assignment = await _assignmentStore.GetAssignmentAsync(interfaceName);

        // Determine criticality
        var isCritical = assignment?.Role == InterfaceRole.Wan ||
                         assignment?.IsManagement == true;

        var severity = isCritical ? "error" : "warning";
        var message = assignment != null
            ? $"Interface '{assignment.Name}' ({assignment.Role}) link down"
            : $"Interface '{interfaceName}' link down";

        await NotifyAsync(message, severity, interfaceName);

        // Update change log with appropriate resolution
        var resolution = isCritical
            ? ResolutionAction.ManualRequired
            : ResolutionAction.Notified;

        await _changeStore.LogChangeAsync(
            NetworkChangeType.LinkDown,
            interfaceName: interfaceName,
            previousValue: new { LinkState = "up" },
            newValue: new { LinkState = "down" },
            resolution: resolution,
            resolutionDetails: isCritical ? "Critical interface down" : "Non-critical interface down");

        // Re-evaluate gateway groups
        if (_gatewayGroupManager != null)
        {
            await _gatewayGroupManager.EvaluateGroupsAsync(cancellationToken);
        }
    }

    private async Task HandleIpChangeAsync(NetworkInterfaceChange change, CancellationToken cancellationToken)
    {
        var assignment = await _assignmentStore.GetAssignmentAsync(change.InterfaceName);

        // Map Common enum to Core enum for change logging
        var coreChangeType = change.ChangeType switch
        {
            InterfaceChangeType.IpAdded => NetworkChangeType.IpAdded,
            InterfaceChangeType.IpRemoved => NetworkChangeType.IpRemoved,
            InterfaceChangeType.IpChanged => NetworkChangeType.IpChanged,
            _ => NetworkChangeType.IpChanged
        };

        // DHCP interface - IP changes are expected, auto-repair by updating operational state
        if (assignment?.IpMode == InterfaceIpMode.Dhcp)
        {
            await _changeStore.LogChangeAsync(
                coreChangeType,
                interfaceName: change.InterfaceName,
                previousValue: new { Address = change.PreviousIpAddress },
                newValue: new { Address = change.NewIpAddress },
                resolution: ResolutionAction.AutoRepaired,
                resolutionDetails: "DHCP IP change - operational state updated");

            await NotifyAsync(
                $"DHCP interface '{assignment.Name}' IP changed: {change.PreviousIpAddress ?? "(none)"} -> {change.NewIpAddress ?? "(none)"}",
                "info",
                change.InterfaceName);

            return;
        }

        // Static interface - unexpected IP change
        if (assignment?.IpMode == InterfaceIpMode.Static)
        {
            if (change.ChangeType == InterfaceChangeType.IpRemoved ||
                change.NewIpAddress != assignment.IpAddress)
            {
                if (_config.AutoRepairStaticIp)
                {
                    await AutoRepairStaticIpAsync(assignment, cancellationToken);
                }
                else
                {
                    await NotifyAsync(
                        $"Static interface '{assignment.Name}' lost configured IP - manual intervention required",
                        "error",
                        change.InterfaceName);

                    await _changeStore.LogChangeAsync(
                        coreChangeType,
                        interfaceName: change.InterfaceName,
                        previousValue: new { Address = change.PreviousIpAddress },
                        newValue: new { Address = change.NewIpAddress },
                        resolution: ResolutionAction.ManualRequired,
                        resolutionDetails: "Static IP lost - manual repair needed");
                }
            }
        }
    }

    private async Task HandleGatewayChangeAsync(NetworkInterfaceChange change, CancellationToken cancellationToken)
    {
        var assignment = await _assignmentStore.GetAssignmentAsync(change.InterfaceName);

        // DHCP gateway change - auto-repair by syncing gateways
        if (assignment?.IpMode == InterfaceIpMode.Dhcp)
        {
            await _changeStore.LogChangeAsync(
                NetworkChangeType.GatewayChanged,
                interfaceName: change.InterfaceName,
                previousValue: new { Gateway = change.PreviousGateway },
                newValue: new { Gateway = change.NewGateway },
                resolution: ResolutionAction.AutoRepaired,
                resolutionDetails: "DHCP gateway change - gateway sync triggered");

            await NotifyAsync(
                $"DHCP interface '{assignment.Name}' gateway changed: {change.PreviousGateway ?? "(none)"} -> {change.NewGateway ?? "(none)"}",
                "info",
                change.InterfaceName);

            // Re-evaluate gateway groups with new gateway
            if (_gatewayGroupManager != null)
            {
                await _gatewayGroupManager.EvaluateGroupsAsync(cancellationToken);
            }

            return;
        }

        // Static gateway change is unexpected
        await NotifyAsync(
            $"Unexpected gateway change on '{change.InterfaceName}': {change.PreviousGateway} -> {change.NewGateway}",
            "warning",
            change.InterfaceName);
    }

    private async Task HandleInterfaceAddedAsync(string interfaceName, CancellationToken cancellationToken)
    {
        // Check if we have an assignment waiting for this interface
        var assignment = await _assignmentStore.GetAssignmentAsync(interfaceName);
        if (assignment != null)
        {
            await NotifyAsync(
                $"Configured interface '{assignment.Name}' appeared - may need to apply configuration",
                "info",
                interfaceName);
        }
        else
        {
            await NotifyAsync(
                $"New unassigned interface detected: '{interfaceName}'",
                "info",
                interfaceName);
        }
    }

    private async Task HandleInterfaceRemovedAsync(string interfaceName, CancellationToken cancellationToken)
    {
        var assignment = await _assignmentStore.GetAssignmentAsync(interfaceName);

        if (assignment != null)
        {
            // Configured interface disappeared - this is serious
            var severity = assignment.Role == InterfaceRole.Wan || assignment.IsManagement
                ? "error"
                : "warning";

            await NotifyAsync(
                $"Configured interface '{assignment.Name}' ({assignment.Role}) disappeared",
                severity,
                interfaceName);

            await _changeStore.LogChangeAsync(
                NetworkChangeType.InterfaceRemoved,
                interfaceName: interfaceName,
                previousValue: new { Name = assignment.Name, Role = assignment.Role.ToString() },
                resolution: ResolutionAction.ManualRequired,
                resolutionDetails: "Configured interface removed - check hardware");
        }
        else
        {
            await NotifyAsync(
                $"Unassigned interface '{interfaceName}' removed",
                "info",
                interfaceName);
        }

        // Re-evaluate gateway groups
        if (_gatewayGroupManager != null)
        {
            await _gatewayGroupManager.EvaluateGroupsAsync(cancellationToken);
        }
    }

    // ========================================================================
    // Auto-Repair Actions
    // ========================================================================

    private async Task AutoRepairStaticIpAsync(InterfaceAssignmentEntity assignment, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assignment.IpAddress) || !assignment.PrefixLength.HasValue)
        {
            await NotifyAsync(
                $"Cannot auto-repair '{assignment.Name}' - missing static IP configuration",
                "error",
                assignment.InterfaceName);
            return;
        }

        try
        {
            // Remove any existing addresses
            var flushCommand = new PlatformCommand
            {
                FileName = "ip",
                Arguments = $"addr flush dev {assignment.InterfaceName}",
                UseSudo = true,
                TimeoutMs = 5000
            };
            await _commandRunner.RunAsync(flushCommand, cancellationToken);

            // Add the configured static IP
            var addCommand = new PlatformCommand
            {
                FileName = "ip",
                Arguments = $"addr add {assignment.IpAddress}/{assignment.PrefixLength} dev {assignment.InterfaceName}",
                UseSudo = true,
                TimeoutMs = 5000
            };
            var result = await _commandRunner.RunAsync(addCommand, cancellationToken);

            if (result.ExitCode == 0)
            {
                await _changeStore.LogChangeAsync(
                    NetworkChangeType.IpChanged,
                    interfaceName: assignment.InterfaceName,
                    newValue: new { Address = assignment.IpAddress, Prefix = assignment.PrefixLength },
                    resolution: ResolutionAction.AutoRepaired,
                    resolutionDetails: "Static IP restored automatically");

                await NotifyAsync(
                    $"Auto-repaired static IP on '{assignment.Name}': {assignment.IpAddress}/{assignment.PrefixLength}",
                    "info",
                    assignment.InterfaceName);

                // Update operational state
                await _operationalStateStore.UpdateIpAddressAsync(
                    assignment.InterfaceName,
                    assignment.IpAddress,
                    assignment.PrefixLength,
                    DateTime.UtcNow);
            }
            else
            {
                await NotifyAsync(
                    $"Failed to auto-repair static IP on '{assignment.Name}': {result.StdErr}",
                    "error",
                    assignment.InterfaceName);
            }
        }
        catch (Exception ex)
        {
            await NotifyAsync(
                $"Error auto-repairing static IP on '{assignment.Name}': {ex.Message}",
                "error",
                assignment.InterfaceName);
        }
    }

    // ========================================================================
    // Manual Reconciliation
    // ========================================================================

    /// <summary>
    /// Performs a full reconciliation check, comparing desired state with operational state.
    /// Returns a list of discrepancies found.
    /// </summary>
    public async Task<List<ReconciliationIssue>> CheckReconciliationAsync(CancellationToken cancellationToken = default)
    {
        var issues = new List<ReconciliationIssue>();

        var assignments = await _assignmentStore.GetAssignmentsAsync();
        foreach (var assignment in assignments)
        {
            var opState = await _operationalStateStore.GetAsync(assignment.InterfaceName);

            // Check if interface exists
            if (opState == null)
            {
                issues.Add(new ReconciliationIssue
                {
                    Severity = "error",
                    InterfaceName = assignment.InterfaceName,
                    IssueType = "interface_missing",
                    Description = $"Configured interface '{assignment.Name}' not found in system",
                    SuggestedAction = "Check hardware connection or interface name"
                });
                continue;
            }

            // Check link state
            if (opState.LinkState == LinkState.Down)
            {
                issues.Add(new ReconciliationIssue
                {
                    Severity = assignment.Role == InterfaceRole.Wan ? "error" : "warning",
                    InterfaceName = assignment.InterfaceName,
                    IssueType = "link_down",
                    Description = $"Interface '{assignment.Name}' has no link",
                    SuggestedAction = "Check cable connection"
                });
            }

            // Check static IP match
            if (assignment.IpMode == InterfaceIpMode.Static)
            {
                if (opState.CurrentIpv4Address != assignment.IpAddress ||
                    opState.CurrentIpv4Prefix != assignment.PrefixLength)
                {
                    issues.Add(new ReconciliationIssue
                    {
                        Severity = "error",
                        InterfaceName = assignment.InterfaceName,
                        IssueType = "ip_mismatch",
                        Description = $"Interface '{assignment.Name}' has IP {opState.CurrentIpv4Address}/{opState.CurrentIpv4Prefix ?? 0} but configured for {assignment.IpAddress}/{assignment.PrefixLength}",
                        SuggestedAction = _config.AutoRepairStaticIp ? "Will auto-repair" : "Reapply interface configuration"
                    });
                }
            }

            // Check DHCP has IP
            if (assignment.IpMode == InterfaceIpMode.Dhcp &&
                opState.LinkState == LinkState.Up &&
                string.IsNullOrWhiteSpace(opState.CurrentIpv4Address))
            {
                issues.Add(new ReconciliationIssue
                {
                    Severity = "warning",
                    InterfaceName = assignment.InterfaceName,
                    IssueType = "dhcp_no_ip",
                    Description = $"DHCP interface '{assignment.Name}' has link but no IP address",
                    SuggestedAction = "Check DHCP server availability"
                });
            }
        }

        return issues;
    }

    /// <summary>
    /// Attempts to auto-repair all repairable issues.
    /// </summary>
    public async Task<int> RepairAllAsync(CancellationToken cancellationToken = default)
    {
        var repaired = 0;
        var issues = await CheckReconciliationAsync(cancellationToken);

        foreach (var issue in issues.Where(i => i.IssueType == "ip_mismatch"))
        {
            var assignment = await _assignmentStore.GetAssignmentAsync(issue.InterfaceName);
            if (assignment != null && assignment.IpMode == InterfaceIpMode.Static)
            {
                await AutoRepairStaticIpAsync(assignment, cancellationToken);
                repaired++;
            }
        }

        return repaired;
    }

    // ========================================================================
    // Helpers
    // ========================================================================

    private async Task NotifyAsync(string message, string level, string? interfaceName = null)
    {
        await _loggingManager.LogSystemAsync(
            "Network",
            level,
            "ReconciliationEngine",
            message,
            interfaceName != null ? new Dictionary<string, object> { ["interface"] = interfaceName } : null);
    }
}

/// <summary>
/// Configuration for reconciliation behavior.
/// </summary>
public sealed class ReconciliationConfig
{
    /// <summary>
    /// Automatically repair static IP addresses when they are lost.
    /// </summary>
    public bool AutoRepairStaticIp { get; set; } = true;

    /// <summary>
    /// Automatically trigger interface configuration when link is restored.
    /// </summary>
    public bool AutoRecoverOnLinkRestore { get; set; } = true;

    /// <summary>
    /// Automatically sync dynamic gateways when DHCP lease changes.
    /// </summary>
    public bool AutoSyncDynamicGateways { get; set; } = true;
}

/// <summary>
/// Represents a discrepancy between desired and operational state.
/// </summary>
public sealed class ReconciliationIssue
{
    public string Severity { get; set; } = "info";
    public string InterfaceName { get; set; } = string.Empty;
    public string IssueType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SuggestedAction { get; set; } = string.Empty;
}
