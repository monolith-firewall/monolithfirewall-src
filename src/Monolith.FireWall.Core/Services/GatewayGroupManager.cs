using System.Text.Json;
using Monolith.FireWall.Common.Services;
using Monolith.FireWall.Core.Models;
using Monolith.FireWall.Core.Services.Platform;
using Monolith.FireWall.Platform.Models;

namespace Monolith.FireWall.Core.Services;

/// <summary>
/// Manages gateway groups for multi-WAN failover, load balancing, and weighted routing.
/// </summary>
public sealed class GatewayGroupManager
{
    private readonly GatewayGroupStore _groupStore;
    private readonly GatewayStore _gatewayStore;
    private readonly GatewayHealthStore _healthStore;
    private readonly NetworkStateChangeStore _changeStore;
    private readonly PlatformCommandRunner _commandRunner;
    private readonly LoggingManager _loggingManager;

    // Track active tier per group for failover
    private readonly Dictionary<int, int> _activeTiers = new();
    private readonly object _activeTiersLock = new();

    public GatewayGroupManager(
        GatewayGroupStore groupStore,
        GatewayStore gatewayStore,
        GatewayHealthStore healthStore,
        NetworkStateChangeStore changeStore,
        PlatformCommandRunner commandRunner)
    {
        _groupStore = groupStore;
        _gatewayStore = gatewayStore;
        _healthStore = healthStore;
        _changeStore = changeStore;
        _commandRunner = commandRunner;
        _loggingManager = LoggingManager.Instance;
    }

    // ========================================================================
    // Gateway Group CRUD
    // ========================================================================

    public async Task<List<GatewayGroupView>> GetGroupsAsync(CancellationToken cancellationToken = default)
    {
        var groups = await _groupStore.GetGroupsAsync();
        var views = new List<GatewayGroupView>();

        foreach (var group in groups)
        {
            views.Add(await BuildGroupViewAsync(group, cancellationToken));
        }

        return views;
    }

    public async Task<GatewayGroupView?> GetGroupAsync(int id, CancellationToken cancellationToken = default)
    {
        var group = await _groupStore.GetGroupAsync(id);
        if (group == null)
        {
            return null;
        }

        return await BuildGroupViewAsync(group, cancellationToken);
    }

    public async Task<(bool Success, string? Error, GatewayGroupView? Group)> CreateGroupAsync(
        GatewayGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            return (false, "Request is required", null);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return (false, "Group name is required", null);
        }

        var existing = await _groupStore.GetGroupByNameAsync(request.Name);
        if (existing != null)
        {
            return (false, "A group with this name already exists", null);
        }

        if (!TryParseGroupMode(request.Mode, out var mode))
        {
            return (false, "Invalid group mode. Must be: failover, loadbalance, or weighted", null);
        }

        if (!TryParseTriggerLevel(request.TriggerLevel, out var trigger))
        {
            return (false, "Invalid trigger level. Must be: member_down, packet_loss, latency_high, or any", null);
        }

        if (request.Members == null || request.Members.Count == 0)
        {
            return (false, "At least one member gateway is required", null);
        }

        // Validate all gateway IDs exist
        foreach (var member in request.Members)
        {
            var gateway = await _gatewayStore.GetGatewayAsync(member.GatewayId);
            if (gateway == null)
            {
                return (false, $"Gateway with ID {member.GatewayId} not found", null);
            }
        }

        var now = DateTime.UtcNow;
        var entity = new GatewayGroupEntity
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Mode = mode,
            TriggerLevel = trigger,
            Enabled = request.Enabled ?? true,
            PacketLossThreshold = request.PacketLossThreshold ?? 20,
            LatencyThresholdMs = request.LatencyThresholdMs ?? 500,
            CreatedAt = now,
            UpdatedAt = now
        };

        if (!await _groupStore.InsertGroupAsync(entity))
        {
            return (false, "Failed to create group", null);
        }

        // Add members
        var members = request.Members.Select(m => new GatewayGroupMemberEntity
        {
            GroupId = entity.Id,
            GatewayId = m.GatewayId,
            Tier = m.Tier ?? 1,
            Weight = m.Weight ?? 1,
            Priority = m.Priority ?? 0,
            CreatedAt = now
        }).ToList();

        await _groupStore.SetMembersAsync(entity.Id, members);

        // Initialize active tier to 1
        lock (_activeTiersLock)
        {
            _activeTiers[entity.Id] = 1;
        }

        await _loggingManager.LogSystemAsync(
            "Routing",
            "info",
            "GatewayGroupManager",
            $"Created gateway group '{entity.Name}'",
            new Dictionary<string, object>
            {
                ["groupId"] = entity.Id,
                ["mode"] = mode.ToString(),
                ["memberCount"] = members.Count
            });

        return (true, null, await BuildGroupViewAsync(entity, cancellationToken));
    }

    public async Task<(bool Success, string? Error, GatewayGroupView? Group)> UpdateGroupAsync(
        int id,
        GatewayGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _groupStore.GetGroupAsync(id);
        if (entity == null)
        {
            return (false, "Group not found", null);
        }

        if (!string.IsNullOrWhiteSpace(request.Name) && request.Name != entity.Name)
        {
            var existing = await _groupStore.GetGroupByNameAsync(request.Name);
            if (existing != null && existing.Id != id)
            {
                return (false, "A group with this name already exists", null);
            }
            entity.Name = request.Name.Trim();
        }

        if (request.Description != null)
        {
            entity.Description = request.Description.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Mode))
        {
            if (!TryParseGroupMode(request.Mode, out var mode))
            {
                return (false, "Invalid group mode", null);
            }
            entity.Mode = mode;
        }

        if (!string.IsNullOrWhiteSpace(request.TriggerLevel))
        {
            if (!TryParseTriggerLevel(request.TriggerLevel, out var trigger))
            {
                return (false, "Invalid trigger level", null);
            }
            entity.TriggerLevel = trigger;
        }

        if (request.Enabled.HasValue)
        {
            entity.Enabled = request.Enabled.Value;
        }

        if (request.PacketLossThreshold.HasValue)
        {
            entity.PacketLossThreshold = request.PacketLossThreshold.Value;
        }

        if (request.LatencyThresholdMs.HasValue)
        {
            entity.LatencyThresholdMs = request.LatencyThresholdMs.Value;
        }

        entity.UpdatedAt = DateTime.UtcNow;

        if (!await _groupStore.UpdateGroupAsync(entity))
        {
            return (false, "Failed to update group", null);
        }

        // Update members if provided
        if (request.Members != null)
        {
            foreach (var member in request.Members)
            {
                var gateway = await _gatewayStore.GetGatewayAsync(member.GatewayId);
                if (gateway == null)
                {
                    return (false, $"Gateway with ID {member.GatewayId} not found", null);
                }
            }

            var members = request.Members.Select(m => new GatewayGroupMemberEntity
            {
                GroupId = entity.Id,
                GatewayId = m.GatewayId,
                Tier = m.Tier ?? 1,
                Weight = m.Weight ?? 1,
                Priority = m.Priority ?? 0,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            await _groupStore.SetMembersAsync(entity.Id, members);
        }

        await _loggingManager.LogSystemAsync(
            "Routing",
            "info",
            "GatewayGroupManager",
            $"Updated gateway group '{entity.Name}'",
            new Dictionary<string, object>
            {
                ["groupId"] = entity.Id
            });

        return (true, null, await BuildGroupViewAsync(entity, cancellationToken));
    }

    public async Task<(bool Success, string? Error)> DeleteGroupAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _groupStore.GetGroupAsync(id);
        if (entity == null)
        {
            return (false, "Group not found");
        }

        // Remove routing rules first
        await RemoveGroupRoutingAsync(entity, cancellationToken);

        if (!await _groupStore.DeleteGroupAsync(id))
        {
            return (false, "Failed to delete group");
        }

        lock (_activeTiersLock)
        {
            _activeTiers.Remove(id);
        }

        await _loggingManager.LogSystemAsync(
            "Routing",
            "warning",
            "GatewayGroupManager",
            $"Deleted gateway group '{entity.Name}'",
            new Dictionary<string, object>
            {
                ["groupId"] = entity.Id
            });

        return (true, null);
    }

    // ========================================================================
    // Failover and Load Balance Logic
    // ========================================================================

    /// <summary>
    /// Evaluates gateway health and updates routing for all enabled groups.
    /// Called by GatewayHealthMonitor when health status changes.
    /// </summary>
    public async Task EvaluateGroupsAsync(CancellationToken cancellationToken = default)
    {
        var groups = await _groupStore.GetEnabledGroupsAsync();
        foreach (var group in groups)
        {
            await EvaluateGroupAsync(group.Id, cancellationToken);
        }
    }

    /// <summary>
    /// Evaluates a specific group and updates routing if needed.
    /// </summary>
    public async Task EvaluateGroupAsync(int groupId, CancellationToken cancellationToken = default)
    {
        var group = await _groupStore.GetGroupAsync(groupId);
        if (group == null || !group.Enabled)
        {
            return;
        }

        var members = await _groupStore.GetMembersByGroupAsync(groupId);
        if (members.Count == 0)
        {
            return;
        }

        // Get health status for all members
        var memberHealth = new Dictionary<int, GatewayHealthEntity?>();
        foreach (var member in members)
        {
            memberHealth[member.GatewayId] = await _healthStore.GetHealthAsync(member.GatewayId);
        }

        switch (group.Mode)
        {
            case GatewayGroupMode.Failover:
                await EvaluateFailoverAsync(group, members, memberHealth, cancellationToken);
                break;
            case GatewayGroupMode.LoadBalance:
                await EvaluateLoadBalanceAsync(group, members, memberHealth, cancellationToken);
                break;
            case GatewayGroupMode.Weighted:
                await EvaluateWeightedAsync(group, members, memberHealth, cancellationToken);
                break;
        }
    }

    private async Task EvaluateFailoverAsync(
        GatewayGroupEntity group,
        List<GatewayGroupMemberEntity> members,
        Dictionary<int, GatewayHealthEntity?> memberHealth,
        CancellationToken cancellationToken)
    {
        int currentActiveTier;
        lock (_activeTiersLock)
        {
            _activeTiers.TryGetValue(group.Id, out currentActiveTier);
            if (currentActiveTier == 0) currentActiveTier = 1;
        }

        // Group members by tier
        var tiers = members
            .GroupBy(m => m.Tier)
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.OrderBy(m => m.Priority).ToList());

        // Find the best tier with at least one healthy gateway
        var newActiveTier = 0;
        foreach (var tier in tiers.Keys.OrderBy(t => t))
        {
            var tierMembers = tiers[tier];
            var hasHealthyMember = tierMembers.Any(m =>
            {
                var health = memberHealth.GetValueOrDefault(m.GatewayId);
                return IsHealthy(health, group.TriggerLevel, group.PacketLossThreshold, group.LatencyThresholdMs);
            });

            if (hasHealthyMember)
            {
                newActiveTier = tier;
                break;
            }
        }

        // If no healthy tier, stay on current
        if (newActiveTier == 0)
        {
            newActiveTier = currentActiveTier;
        }

        // Check if failover needed
        if (newActiveTier != currentActiveTier)
        {
            // Perform failover
            var previousGateways = tiers.ContainsKey(currentActiveTier)
                ? tiers[currentActiveTier].Select(m => m.GatewayId).ToList()
                : new List<int>();
            var newGateways = tiers.ContainsKey(newActiveTier)
                ? tiers[newActiveTier].Select(m => m.GatewayId).ToList()
                : new List<int>();

            await ApplyFailoverRoutingAsync(group, tiers[newActiveTier], cancellationToken);

            lock (_activeTiersLock)
            {
                _activeTiers[group.Id] = newActiveTier;
            }

            // Log the failover
            var failoverEvent = new GatewayGroupFailoverEvent
            {
                GroupId = group.Id,
                GroupName = group.Name,
                PreviousTier = currentActiveTier,
                NewTier = newActiveTier,
                PreviousActiveGateways = previousGateways,
                NewActiveGateways = newGateways,
                Reason = currentActiveTier < newActiveTier ? "Primary tier unhealthy" : "Primary tier recovered",
                OccurredAt = DateTime.UtcNow
            };

            await _changeStore.LogChangeAsync(
                NetworkChangeType.GatewayGroupFailover,
                gatewayGroupId: group.Id,
                previousValue: new { Tier = currentActiveTier, Gateways = previousGateways },
                newValue: new { Tier = newActiveTier, Gateways = newGateways },
                resolution: ResolutionAction.AutoRepaired,
                resolutionDetails: failoverEvent.Reason);

            await _loggingManager.LogSystemAsync(
                "Routing",
                currentActiveTier < newActiveTier ? "warning" : "info",
                "GatewayGroupManager",
                $"Gateway group '{group.Name}' failed over from tier {currentActiveTier} to tier {newActiveTier}",
                new Dictionary<string, object>
                {
                    ["groupId"] = group.Id,
                    ["previousTier"] = currentActiveTier,
                    ["newTier"] = newActiveTier,
                    ["reason"] = failoverEvent.Reason
                });
        }
    }

    private async Task EvaluateLoadBalanceAsync(
        GatewayGroupEntity group,
        List<GatewayGroupMemberEntity> members,
        Dictionary<int, GatewayHealthEntity?> memberHealth,
        CancellationToken cancellationToken)
    {
        // Get healthy members
        var healthyMembers = members.Where(m =>
        {
            var health = memberHealth.GetValueOrDefault(m.GatewayId);
            return IsHealthy(health, group.TriggerLevel, group.PacketLossThreshold, group.LatencyThresholdMs);
        }).ToList();

        if (healthyMembers.Count == 0)
        {
            // No healthy members - keep existing routing
            return;
        }

        await ApplyLoadBalanceRoutingAsync(group, healthyMembers, cancellationToken);
    }

    private async Task EvaluateWeightedAsync(
        GatewayGroupEntity group,
        List<GatewayGroupMemberEntity> members,
        Dictionary<int, GatewayHealthEntity?> memberHealth,
        CancellationToken cancellationToken)
    {
        // Get healthy members with their weights
        var healthyMembers = members.Where(m =>
        {
            var health = memberHealth.GetValueOrDefault(m.GatewayId);
            return IsHealthy(health, group.TriggerLevel, group.PacketLossThreshold, group.LatencyThresholdMs);
        }).ToList();

        if (healthyMembers.Count == 0)
        {
            return;
        }

        await ApplyWeightedRoutingAsync(group, healthyMembers, cancellationToken);
    }

    private bool IsHealthy(
        GatewayHealthEntity? health,
        GatewayGroupTrigger trigger,
        int? packetLossThreshold,
        int? latencyThreshold)
    {
        if (health == null || health.Status == GatewayHealthStatus.Unknown)
        {
            return true; // Assume healthy if unknown
        }

        switch (trigger)
        {
            case GatewayGroupTrigger.MemberDown:
                return health.Status != GatewayHealthStatus.Offline;

            case GatewayGroupTrigger.PacketLoss:
                return health.Status != GatewayHealthStatus.Offline &&
                       (health.PacketLossPercent ?? 0) < (packetLossThreshold ?? 20);

            case GatewayGroupTrigger.LatencyHigh:
                return health.Status != GatewayHealthStatus.Offline &&
                       (health.LatencyMs ?? 0) < (latencyThreshold ?? 500);

            case GatewayGroupTrigger.Any:
                return health.Status == GatewayHealthStatus.Online;

            default:
                return health.Status != GatewayHealthStatus.Offline;
        }
    }

    // ========================================================================
    // Linux Routing Implementation
    // ========================================================================

    private async Task ApplyFailoverRoutingAsync(
        GatewayGroupEntity group,
        List<GatewayGroupMemberEntity> activeMembers,
        CancellationToken cancellationToken)
    {
        if (activeMembers.Count == 0)
        {
            return;
        }

        // For failover, use the first member (highest priority)
        var primary = activeMembers.First();
        var gateway = await _gatewayStore.GetGatewayAsync(primary.GatewayId);
        if (gateway == null)
        {
            return;
        }

        // Replace default route
        var familyFlag = gateway.AddressFamily.Equals("ipv6", StringComparison.OrdinalIgnoreCase) ? "-6 " : "";
        var args = $"{familyFlag}route replace default via {gateway.Address}";
        if (!string.IsNullOrWhiteSpace(gateway.Interface))
        {
            args += $" dev {gateway.Interface}";
        }
        args += $" metric 100";

        var command = new PlatformCommand
        {
            FileName = "ip",
            Arguments = args,
            UseSudo = true,
            TimeoutMs = 5000
        };

        await _commandRunner.RunAsync(command, cancellationToken);
    }

    private async Task ApplyLoadBalanceRoutingAsync(
        GatewayGroupEntity group,
        List<GatewayGroupMemberEntity> healthyMembers,
        CancellationToken cancellationToken)
    {
        if (healthyMembers.Count == 0)
        {
            return;
        }

        // Build multipath route with equal weights
        var nexthops = new List<string>();
        string? familyFlag = null;

        foreach (var member in healthyMembers)
        {
            var gateway = await _gatewayStore.GetGatewayAsync(member.GatewayId);
            if (gateway == null)
            {
                continue;
            }

            familyFlag ??= gateway.AddressFamily.Equals("ipv6", StringComparison.OrdinalIgnoreCase) ? "-6 " : "";

            var nexthop = $"nexthop via {gateway.Address}";
            if (!string.IsNullOrWhiteSpace(gateway.Interface))
            {
                nexthop += $" dev {gateway.Interface}";
            }
            nexthop += " weight 1";
            nexthops.Add(nexthop);
        }

        if (nexthops.Count == 0)
        {
            return;
        }

        var args = $"{familyFlag ?? ""}route replace default {string.Join(" ", nexthops)}";

        var command = new PlatformCommand
        {
            FileName = "ip",
            Arguments = args,
            UseSudo = true,
            TimeoutMs = 5000
        };

        await _commandRunner.RunAsync(command, cancellationToken);
    }

    private async Task ApplyWeightedRoutingAsync(
        GatewayGroupEntity group,
        List<GatewayGroupMemberEntity> healthyMembers,
        CancellationToken cancellationToken)
    {
        if (healthyMembers.Count == 0)
        {
            return;
        }

        // Build multipath route with specified weights
        var nexthops = new List<string>();
        string? familyFlag = null;

        foreach (var member in healthyMembers)
        {
            var gateway = await _gatewayStore.GetGatewayAsync(member.GatewayId);
            if (gateway == null)
            {
                continue;
            }

            familyFlag ??= gateway.AddressFamily.Equals("ipv6", StringComparison.OrdinalIgnoreCase) ? "-6 " : "";

            var nexthop = $"nexthop via {gateway.Address}";
            if (!string.IsNullOrWhiteSpace(gateway.Interface))
            {
                nexthop += $" dev {gateway.Interface}";
            }
            nexthop += $" weight {Math.Max(1, member.Weight)}";
            nexthops.Add(nexthop);
        }

        if (nexthops.Count == 0)
        {
            return;
        }

        var args = $"{familyFlag ?? ""}route replace default {string.Join(" ", nexthops)}";

        var command = new PlatformCommand
        {
            FileName = "ip",
            Arguments = args,
            UseSudo = true,
            TimeoutMs = 5000
        };

        await _commandRunner.RunAsync(command, cancellationToken);
    }

    private async Task RemoveGroupRoutingAsync(GatewayGroupEntity group, CancellationToken cancellationToken)
    {
        // When removing a group, we don't remove routes - they'll be managed by other groups or default gateway
        await Task.CompletedTask;
    }

    // ========================================================================
    // Helpers
    // ========================================================================

    private async Task<GatewayGroupView> BuildGroupViewAsync(GatewayGroupEntity entity, CancellationToken cancellationToken)
    {
        var members = await _groupStore.GetMembersByGroupAsync(entity.Id);
        var memberViews = new List<GatewayGroupMemberView>();

        foreach (var member in members)
        {
            var gateway = await _gatewayStore.GetGatewayAsync(member.GatewayId);
            var health = await _healthStore.GetHealthAsync(member.GatewayId);

            memberViews.Add(new GatewayGroupMemberView
            {
                Id = member.Id,
                GatewayId = member.GatewayId,
                GatewayName = gateway?.Name ?? "Unknown",
                GatewayAddress = gateway?.Address ?? string.Empty,
                Interface = gateway?.Interface,
                Tier = member.Tier,
                Weight = member.Weight,
                Priority = member.Priority,
                Health = health != null ? new GatewayHealthView
                {
                    GatewayId = health.GatewayId,
                    Status = health.Status.ToString().ToLowerInvariant(),
                    LatencyMs = health.LatencyMs,
                    PacketLossPercent = health.PacketLossPercent,
                    ConsecutiveFailures = health.ConsecutiveFailures,
                    ConsecutiveSuccesses = health.ConsecutiveSuccesses,
                    LastCheckAt = health.LastCheckAt,
                    LastStateChangeAt = health.LastStateChangeAt,
                    LastError = health.LastError
                } : null
            });
        }

        int activeTier;
        lock (_activeTiersLock)
        {
            _activeTiers.TryGetValue(entity.Id, out activeTier);
            if (activeTier == 0) activeTier = 1;
        }

        var healthyCount = memberViews.Count(m =>
            m.Health == null ||
            m.Health.Status == "online" ||
            m.Health.Status == "unknown");

        return new GatewayGroupView
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Mode = entity.Mode.ToString().ToLowerInvariant(),
            TriggerLevel = FormatTriggerLevel(entity.TriggerLevel),
            Enabled = entity.Enabled,
            PacketLossThreshold = entity.PacketLossThreshold,
            LatencyThresholdMs = entity.LatencyThresholdMs,
            Members = memberViews,
            CurrentStatus = new GatewayGroupStatusView
            {
                ActiveTier = activeTier,
                ActiveGatewayIds = memberViews
                    .Where(m => m.Tier == activeTier)
                    .Select(m => m.GatewayId)
                    .ToList(),
                HealthyMemberCount = healthyCount,
                TotalMemberCount = memberViews.Count
            },
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    private static bool TryParseGroupMode(string? mode, out GatewayGroupMode result)
    {
        result = GatewayGroupMode.Failover;
        if (string.IsNullOrWhiteSpace(mode))
        {
            return true;
        }

        return mode.ToLowerInvariant() switch
        {
            "failover" => (result = GatewayGroupMode.Failover) == GatewayGroupMode.Failover,
            "loadbalance" or "load_balance" or "load-balance" => (result = GatewayGroupMode.LoadBalance) == GatewayGroupMode.LoadBalance,
            "weighted" => (result = GatewayGroupMode.Weighted) == GatewayGroupMode.Weighted,
            _ => false
        };
    }

    private static bool TryParseTriggerLevel(string? trigger, out GatewayGroupTrigger result)
    {
        result = GatewayGroupTrigger.MemberDown;
        if (string.IsNullOrWhiteSpace(trigger))
        {
            return true;
        }

        return trigger.ToLowerInvariant() switch
        {
            "member_down" or "memberdown" => (result = GatewayGroupTrigger.MemberDown) == GatewayGroupTrigger.MemberDown,
            "packet_loss" or "packetloss" => (result = GatewayGroupTrigger.PacketLoss) == GatewayGroupTrigger.PacketLoss,
            "latency_high" or "latencyhigh" or "high_latency" => (result = GatewayGroupTrigger.LatencyHigh) == GatewayGroupTrigger.LatencyHigh,
            "any" => (result = GatewayGroupTrigger.Any) == GatewayGroupTrigger.Any,
            _ => false
        };
    }

    private static string FormatTriggerLevel(GatewayGroupTrigger trigger)
    {
        return trigger switch
        {
            GatewayGroupTrigger.MemberDown => "member_down",
            GatewayGroupTrigger.PacketLoss => "packet_loss",
            GatewayGroupTrigger.LatencyHigh => "latency_high",
            GatewayGroupTrigger.Any => "any",
            _ => "member_down"
        };
    }
}
