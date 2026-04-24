using System.Text.Json;
using System.IO;
using Monolith.FireWall.Common.Models;
using Monolith.FireWall.Core.Models;
using Monolith.FireWall.Core.Services.Firewall;

namespace Monolith.FireWall.Core.Transport.Handlers;

public sealed class FirewallHandler : ICoreRequestHandler
{
    private static readonly HashSet<string> Actions = new(StringComparer.OrdinalIgnoreCase)
    {
        "firewall.aliases.list",
        "firewall.aliases.get",
        "firewall.aliases.create",
        "firewall.aliases.update",
        "firewall.aliases.delete",
        "firewall.aliases.resolve",
        "firewall.nat.list",
        "firewall.nat.get",
        "firewall.nat.create",
        "firewall.nat.update",
        "firewall.nat.delete",
        "firewall.nat.reorder",
        "firewall.nat.settings.get",
        "firewall.nat.settings.update",
        "firewall.rules.list",
        "firewall.rules.get",
        "firewall.rules.create",
        "firewall.rules.update",
        "firewall.rules.delete",
        "firewall.rules.reorder",
        "firewall.rules.managed.upsert",
        "firewall.rules.query",
        "firewall.rules.types",
        "firewall.rules.validate",
        "firewall.defaults.get",
        "firewall.defaults.update",
        "firewall.interface_settings.list",
        "firewall.interface_settings.get",
        "firewall.interface_settings.update",
        "firewall.states.list",
        "firewall.states.kill",
        "firewall.virtualips.list",
        "firewall.virtualips.get",
        "firewall.virtualips.create",
        "firewall.virtualips.update",
        "firewall.virtualips.delete",
        "firewall.trafficshaper.list",
        "firewall.trafficshaper.get",
        "firewall.trafficshaper.create",
        "firewall.trafficshaper.update",
        "firewall.trafficshaper.delete",
        "firewall.schedules.list",
        "firewall.schedules.get",
        "firewall.schedules.create",
        "firewall.schedules.update",
        "firewall.schedules.delete",
        "firewall.schedules.active",
        "firewall.status",
        "firewall.config",
        "firewall.pending",
        "firewall.apply",
        "firewall.discard",
        "firewall.preview"
    };

    private readonly RuleConflictChecker _conflictChecker = new();

    public bool CanHandle(string action) => Actions.Contains(action);

    public async Task<ApiResponse> HandleAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        var action = request.GetProperty("action").GetString() ?? string.Empty;

        switch (action)
        {
            case "firewall.aliases.list":
                var aliases = await context.FirewallManager.Aliases.ListAliasesAsync();
                return new ApiResponse(true, aliases, null);

            case "firewall.aliases.get":
                if (!CoreRequestParsing.TryGetPayload(request, out FirewallIdRequest aliasIdRequest, out var aliasIdError))
                {
                    return new ApiResponse(false, null, aliasIdError);
                }

                var alias = await context.FirewallManager.Aliases.GetAliasAsync(aliasIdRequest.Id);
                return alias == null
                    ? new ApiResponse(false, null, "Alias not found")
                    : new ApiResponse(true, alias, null);

            case "firewall.aliases.resolve":
                if (!CoreRequestParsing.TryGetPayload(request, out FirewallAliasResolveRequest aliasResolveRequest, out var aliasResolveError))
                {
                    return new ApiResponse(false, null, aliasResolveError);
                }

                if (string.IsNullOrWhiteSpace(aliasResolveRequest.Name))
                {
                    return new ApiResponse(false, null, "Alias name is required");
                }

                var aliasEntries = await context.FirewallManager.Aliases.ResolveAliasAsync(aliasResolveRequest.Name.Trim());
                return new ApiResponse(true, aliasEntries, null);

            case "firewall.aliases.create":
                if (!CoreRequestParsing.TryGetPayload(request, out FirewallAliasRequest aliasRequest, out var aliasError))
                {
                    return new ApiResponse(false, null, aliasError);
                }

                var createResult = await context.FirewallManager.Aliases.CreateAliasAsync(aliasRequest);
                return createResult.Success
                    ? new ApiResponse(true, createResult.Alias, null)
                    : new ApiResponse(false, null, createResult.Error ?? "Failed to create alias");

            case "firewall.aliases.update":
                if (!CoreRequestParsing.TryGetPayload(request, out FirewallAliasUpdateRequest aliasUpdateRequest, out var aliasUpdateError))
                {
                    return new ApiResponse(false, null, aliasUpdateError);
                }

                var aliasUpdate = await context.FirewallManager.Aliases.UpdateAliasAsync(aliasUpdateRequest.Id, aliasUpdateRequest);
                return aliasUpdate.Success
                    ? new ApiResponse(true, aliasUpdate.Alias, null)
                    : new ApiResponse(false, null, aliasUpdate.Error ?? "Failed to update alias");

            case "firewall.aliases.delete":
                if (!CoreRequestParsing.TryGetPayload(request, out FirewallIdRequest aliasDeleteRequest, out var aliasDeleteError))
                {
                    return new ApiResponse(false, null, aliasDeleteError);
                }

                var aliasDeleted = await context.FirewallManager.Aliases.DeleteAliasAsync(aliasDeleteRequest.Id);
                return aliasDeleted
                    ? new ApiResponse(true, new { id = aliasDeleteRequest.Id }, null)
                    : new ApiResponse(false, null, "Failed to delete alias");

            case "firewall.nat.list":
                var rules = await context.FirewallManager.Nat.ListRulesAsync();
                return new ApiResponse(true, rules, null);

            case "firewall.nat.get":
                if (!CoreRequestParsing.TryGetPayload(request, out FirewallIdRequest natGetRequest, out var natGetError))
                {
                    return new ApiResponse(false, null, natGetError);
                }

                var rule = await context.FirewallManager.Nat.GetRuleAsync(natGetRequest.Id);
                return rule == null
                    ? new ApiResponse(false, null, "NAT rule not found")
                    : new ApiResponse(true, rule, null);

            case "firewall.nat.create":
                if (!CoreRequestParsing.TryGetPayload(request, out FirewallNatRuleRequest natRequest, out var natError))
                {
                    return new ApiResponse(false, null, natError);
                }

                var natCreate = await context.FirewallManager.Nat.CreateRuleAsync(natRequest);
                return natCreate.Success
                    ? new ApiResponse(true, natCreate.Rule, null)
                    : new ApiResponse(false, null, natCreate.Error ?? "Failed to create NAT rule");

            case "firewall.nat.update":
                if (!CoreRequestParsing.TryGetPayload(request, out FirewallNatRuleUpdateRequest natUpdateRequest, out var natUpdateError))
                {
                    return new ApiResponse(false, null, natUpdateError);
                }

                var natUpdate = await context.FirewallManager.Nat.UpdateRuleAsync(natUpdateRequest.Id, natUpdateRequest);
                return natUpdate.Success
                    ? new ApiResponse(true, natUpdate.Rule, null)
                    : new ApiResponse(false, null, natUpdate.Error ?? "Failed to update NAT rule");

            case "firewall.nat.delete":
                if (!CoreRequestParsing.TryGetPayload(request, out FirewallIdRequest natDeleteRequest, out var natDeleteError))
                {
                    return new ApiResponse(false, null, natDeleteError);
                }

                var natDeleted = await context.FirewallManager.Nat.DeleteRuleAsync(natDeleteRequest.Id);
                return natDeleted
                    ? new ApiResponse(true, new { id = natDeleteRequest.Id }, null)
                    : new ApiResponse(false, null, "Failed to delete NAT rule");

            case "firewall.nat.reorder":
                if (!CoreRequestParsing.TryGetPayload(request, out FirewallNatReorderRequest natReorderRequest, out var natReorderError))
                {
                    return new ApiResponse(false, null, natReorderError);
                }

                var reorder = await context.FirewallManager.Nat.ReorderRulesAsync(natReorderRequest.RuleIds.ToArray());
                return reorder
                    ? new ApiResponse(true, new { success = true }, null)
                    : new ApiResponse(false, null, "Failed to reorder NAT rules");

            case "firewall.nat.settings.get":
                var settings = await context.FirewallManager.NatSettings.GetAsync();
                return new ApiResponse(true, settings, null);

            case "firewall.nat.settings.update":
                if (!CoreRequestParsing.TryGetPayload(request, out FirewallNatSettingsRequest natSettingsRequest, out var natSettingsError))
                {
                    return new ApiResponse(false, null, natSettingsError);
                }

                var settingsUpdate = await context.FirewallManager.NatSettings.UpdateAsync(natSettingsRequest);
                return settingsUpdate.Success
                    ? new ApiResponse(true, settingsUpdate.Settings, null)
                    : new ApiResponse(false, null, settingsUpdate.Error ?? "Failed to update NAT settings");

            case "firewall.rules.list":
                var defaults = await context.FirewallManager.Defaults.GetAsync();
                var ruleList = await context.FirewallManager.Rules.GetEffectiveRulesAsync(defaults);
                return new ApiResponse(true, ruleList, null);

            case "firewall.rules.get":
                if (!CoreRequestParsing.TryGetPayload(request, out FirewallIdRequest ruleIdRequest, out var ruleIdError))
                {
                    return new ApiResponse(false, null, ruleIdError);
                }

                var ruleEntry = await context.FirewallManager.Rules.GetRuleAsync(ruleIdRequest.Id);
                return ruleEntry == null
                    ? new ApiResponse(false, null, "Rule not found")
                    : new ApiResponse(true, ruleEntry, null);

            case "firewall.rules.create":
                if (!CoreRequestParsing.TryGetPayload(request, out FirewallRuleRequest ruleRequest, out var ruleError))
                {
                    return new ApiResponse(false, null, ruleError);
                }

                var ruleCreate = await context.FirewallManager.Rules.CreateRuleAsync(ruleRequest);
                return ruleCreate.Success
                    ? new ApiResponse(true, ruleCreate.Rule, null)
                    : new ApiResponse(false, null, ruleCreate.Error ?? "Failed to create rule");

            case "firewall.rules.update":
                if (!CoreRequestParsing.TryGetPayload(request, out FirewallRuleUpdateRequest ruleUpdateRequest, out var ruleUpdateError))
                {
                    return new ApiResponse(false, null, ruleUpdateError);
                }

                var ruleUpdate = await context.FirewallManager.Rules.UpdateRuleAsync(ruleUpdateRequest.Id, ruleUpdateRequest);
                return ruleUpdate.Success
                    ? new ApiResponse(true, ruleUpdate.Rule, null)
                    : new ApiResponse(false, null, ruleUpdate.Error ?? "Failed to update rule");

            case "firewall.rules.delete":
                if (!CoreRequestParsing.TryGetPayload(request, out FirewallIdRequest ruleDeleteRequest, out var ruleDeleteError))
                {
                    return new ApiResponse(false, null, ruleDeleteError);
                }

                var ruleDeleted = await context.FirewallManager.Rules.DeleteRuleAsync(ruleDeleteRequest.Id);
                return ruleDeleted
                    ? new ApiResponse(true, new { id = ruleDeleteRequest.Id }, null)
                    : new ApiResponse(false, null, "Failed to delete rule");

            case "firewall.rules.reorder":
                if (!CoreRequestParsing.TryGetPayload(request, out FirewallRuleReorderRequest reorderRequest, out var reorderError))
                {
                    return new ApiResponse(false, null, reorderError);
                }

                if (string.IsNullOrWhiteSpace(reorderRequest.Interface))
                {
                    return new ApiResponse(false, null, "Interface is required for reorder");
                }

                var reorderResult = await context.FirewallManager.Rules.ReorderRulesAsync(reorderRequest.Interface.Trim(), reorderRequest.RuleIds);
                return reorderResult
                    ? new ApiResponse(true, new { success = true }, null)
                    : new ApiResponse(false, null, "Failed to reorder rules");

            case "firewall.rules.managed.upsert":
                if (!CoreRequestParsing.TryGetPayload(request, out FirewallManagedRuleRequest managedRequest, out var managedError))
                {
                    return new ApiResponse(false, null, managedError);
                }

                var managedResult = await context.FirewallManager.Rules.UpsertManagedRuleAsync(managedRequest);
                return managedResult.Success
                    ? new ApiResponse(true, managedResult.Rule, null)
                    : new ApiResponse(false, null, managedResult.Error ?? "Failed to update managed rule");

            case "firewall.rules.query":
                FirewallRuleQueryRequest queryRequest;
                if (request.TryGetProperty("payload", out var queryPayload))
                {
                    if (!CoreRequestParsing.TryGetPayload(request, out queryRequest, out var queryError))
                    {
                        queryRequest = new FirewallRuleQueryRequest();
                    }
                }
                else
                {
                    queryRequest = new FirewallRuleQueryRequest();
                }

                var queryDefaults = await context.FirewallManager.Defaults.GetAsync();
                var queryResponse = await context.FirewallManager.Rules.QueryRulesAsync(queryRequest, queryDefaults);
                return new ApiResponse(true, queryResponse, null);

            case "firewall.rules.types":
                var typesDefaults = await context.FirewallManager.Defaults.GetAsync();
                var typesResponse = await context.FirewallManager.Rules.GetRuleTypesAsync(typesDefaults);
                return new ApiResponse(true, typesResponse, null);

            case "firewall.rules.validate":
                if (!CoreRequestParsing.TryGetPayload(request, out FirewallRuleValidateRequest validateRequest, out var validateError))
                {
                    return new ApiResponse(false, null, validateError);
                }

                var validateDefaults = await context.FirewallManager.Defaults.GetAsync();
                var allRulesExtended = await context.FirewallManager.Rules.GetAllRulesExtendedAsync(validateDefaults);
                var validateResponse = _conflictChecker.CheckConflicts(validateRequest, allRulesExtended, validateRequest.ExcludeRuleId);
                return new ApiResponse(true, validateResponse, null);

            case "firewall.defaults.get":
                var defaultsView = await context.FirewallManager.Defaults.GetAsync();
                return new ApiResponse(true, defaultsView, null);

            case "firewall.defaults.update":
                if (!CoreRequestParsing.TryGetPayload(request, out FirewallDefaultsRequest defaultsRequest, out var defaultsError))
                {
                    return new ApiResponse(false, null, defaultsError);
                }

                var defaultsUpdate = await context.FirewallManager.Defaults.UpdateAsync(defaultsRequest);
                return defaultsUpdate.Success
                    ? new ApiResponse(true, defaultsUpdate.Defaults, null)
                    : new ApiResponse(false, null, defaultsUpdate.Error ?? "Failed to update defaults");

            case "firewall.interface_settings.list":
                var ifaceSettingsList = await context.FirewallManager.InterfaceSettings.GetAllAsync();
                return new ApiResponse(true, ifaceSettingsList, null);

            case "firewall.interface_settings.get":
                if (!CoreRequestParsing.TryGetPayload(request, out FirewallInterfaceSettingsRequest getSettingsRequest, out var getSettingsError))
                {
                    return new ApiResponse(false, null, getSettingsError);
                }

                if (string.IsNullOrWhiteSpace(getSettingsRequest.InterfaceName))
                {
                    return new ApiResponse(false, null, "Interface name is required");
                }

                var ifaceSettings = await context.FirewallManager.InterfaceSettings.GetByInterfaceAsync(getSettingsRequest.InterfaceName);
                return new ApiResponse(true, ifaceSettings, null);

            case "firewall.interface_settings.update":
                if (!CoreRequestParsing.TryGetPayload(request, out FirewallInterfaceSettingsEntity updateSettingsRequest, out var updateSettingsError))
                {
                    return new ApiResponse(false, null, updateSettingsError);
                }

                if (string.IsNullOrWhiteSpace(updateSettingsRequest.InterfaceName))
                {
                    return new ApiResponse(false, null, "Interface name is required");
                }

                var updateSettingsResult = await context.FirewallManager.InterfaceSettings.UpdateSettingsAsync(updateSettingsRequest);
                return updateSettingsResult
                    ? new ApiResponse(true, new { success = true }, null)
                    : new ApiResponse(false, null, "Failed to update interface settings");

            // Virtual IPs
            case "firewall.virtualips.list":
                var virtualIps = await context.FirewallManager.VirtualIps.ListVirtualIpsAsync();
                return new ApiResponse(true, virtualIps, null);

            case "firewall.virtualips.get":
                if (!CoreRequestParsing.TryGetPayload(request, out FirewallIdRequest vipGetRequest, out var vipGetError))
                {
                    return new ApiResponse(false, null, vipGetError);
                }

                var vip = await context.FirewallManager.VirtualIps.GetVirtualIpAsync(vipGetRequest.Id);
                return vip == null
                    ? new ApiResponse(false, null, "Virtual IP not found")
                    : new ApiResponse(true, vip, null);

            case "firewall.virtualips.create":
                if (!CoreRequestParsing.TryGetPayload(request, out FirewallVirtualIpRequest vipCreateRequest, out var vipCreateError))
                {
                    return new ApiResponse(false, null, vipCreateError);
                }

                var vipCreate = await context.FirewallManager.VirtualIps.CreateVirtualIpAsync(vipCreateRequest);
                return vipCreate.Success
                    ? new ApiResponse(true, vipCreate.VirtualIp, null)
                    : new ApiResponse(false, null, vipCreate.Error ?? "Failed to create virtual IP");

            case "firewall.virtualips.update":
                if (!CoreRequestParsing.TryGetPayload(request, out FirewallVirtualIpUpdateRequest vipUpdateRequest, out var vipUpdateError))
                {
                    return new ApiResponse(false, null, vipUpdateError);
                }

                var vipUpdate = await context.FirewallManager.VirtualIps.UpdateVirtualIpAsync(vipUpdateRequest.Id, vipUpdateRequest);
                return vipUpdate.Success
                    ? new ApiResponse(true, vipUpdate.VirtualIp, null)
                    : new ApiResponse(false, null, vipUpdate.Error ?? "Failed to update virtual IP");

            case "firewall.virtualips.delete":
                if (!CoreRequestParsing.TryGetPayload(request, out FirewallIdRequest vipDeleteRequest, out var vipDeleteError))
                {
                    return new ApiResponse(false, null, vipDeleteError);
                }

                var vipDeleted = await context.FirewallManager.VirtualIps.DeleteVirtualIpAsync(vipDeleteRequest.Id);
                return vipDeleted
                    ? new ApiResponse(true, new { id = vipDeleteRequest.Id }, null)
                    : new ApiResponse(false, null, "Failed to delete virtual IP");

            // Traffic Shaper
            case "firewall.trafficshaper.list":
                var shaperRules = await context.FirewallManager.TrafficShaper.ListRulesAsync();
                return new ApiResponse(true, shaperRules, null);

            case "firewall.trafficshaper.get":
                if (!CoreRequestParsing.TryGetPayload(request, out FirewallIdRequest shaperGetRequest, out var shaperGetError))
                {
                    return new ApiResponse(false, null, shaperGetError);
                }

                var shaperRule = await context.FirewallManager.TrafficShaper.GetRuleAsync(shaperGetRequest.Id);
                return shaperRule == null
                    ? new ApiResponse(false, null, "Traffic shaper rule not found")
                    : new ApiResponse(true, shaperRule, null);

            case "firewall.trafficshaper.create":
                if (!CoreRequestParsing.TryGetPayload(request, out FirewallTrafficShaperRequest shaperCreateRequest, out var shaperCreateError))
                {
                    return new ApiResponse(false, null, shaperCreateError);
                }

                var shaperCreate = await context.FirewallManager.TrafficShaper.CreateRuleAsync(shaperCreateRequest);
                return shaperCreate.Success
                    ? new ApiResponse(true, shaperCreate.Rule, null)
                    : new ApiResponse(false, null, shaperCreate.Error ?? "Failed to create traffic shaper rule");

            case "firewall.trafficshaper.update":
                if (!CoreRequestParsing.TryGetPayload(request, out FirewallTrafficShaperUpdateRequest shaperUpdateRequest, out var shaperUpdateError))
                {
                    return new ApiResponse(false, null, shaperUpdateError);
                }

                var shaperUpdate = await context.FirewallManager.TrafficShaper.UpdateRuleAsync(shaperUpdateRequest.Id, shaperUpdateRequest);
                return shaperUpdate.Success
                    ? new ApiResponse(true, shaperUpdate.Rule, null)
                    : new ApiResponse(false, null, shaperUpdate.Error ?? "Failed to update traffic shaper rule");

            case "firewall.trafficshaper.delete":
                if (!CoreRequestParsing.TryGetPayload(request, out FirewallIdRequest shaperDeleteRequest, out var shaperDeleteError))
                {
                    return new ApiResponse(false, null, shaperDeleteError);
                }

                var shaperDeleted = await context.FirewallManager.TrafficShaper.DeleteRuleAsync(shaperDeleteRequest.Id);
                return shaperDeleted
                    ? new ApiResponse(true, new { id = shaperDeleteRequest.Id }, null)
                    : new ApiResponse(false, null, "Failed to delete traffic shaper rule");

            // Schedules
            case "firewall.schedules.list":
                var schedules = await context.FirewallManager.Schedules.ListSchedulesAsync();
                return new ApiResponse(true, schedules, null);

            case "firewall.schedules.get":
                if (!CoreRequestParsing.TryGetPayload(request, out FirewallIdRequest scheduleGetRequest, out var scheduleGetError))
                {
                    return new ApiResponse(false, null, scheduleGetError);
                }

                var schedule = await context.FirewallManager.Schedules.GetScheduleViewAsync(scheduleGetRequest.Id);
                return schedule == null
                    ? new ApiResponse(false, null, "Schedule not found")
                    : new ApiResponse(true, schedule, null);

            case "firewall.schedules.create":
                if (!CoreRequestParsing.TryGetPayload(request, out FirewallScheduleRequest scheduleCreateRequest, out var scheduleCreateError))
                {
                    return new ApiResponse(false, null, scheduleCreateError);
                }

                var scheduleCreate = await context.FirewallManager.Schedules.CreateScheduleAsync(scheduleCreateRequest);
                return scheduleCreate.Success
                    ? new ApiResponse(true, scheduleCreate.Schedule, null)
                    : new ApiResponse(false, null, scheduleCreate.Error ?? "Failed to create schedule");

            case "firewall.schedules.update":
                if (!CoreRequestParsing.TryGetPayload(request, out FirewallScheduleUpdateRequest scheduleUpdateRequest, out var scheduleUpdateError))
                {
                    return new ApiResponse(false, null, scheduleUpdateError);
                }

                var scheduleUpdate = await context.FirewallManager.Schedules.UpdateScheduleAsync(scheduleUpdateRequest.Id, scheduleUpdateRequest);
                return scheduleUpdate.Success
                    ? new ApiResponse(true, scheduleUpdate.Schedule, null)
                    : new ApiResponse(false, null, scheduleUpdate.Error ?? "Failed to update schedule");

            case "firewall.schedules.delete":
                if (!CoreRequestParsing.TryGetPayload(request, out FirewallIdRequest scheduleDeleteRequest, out var scheduleDeleteError))
                {
                    return new ApiResponse(false, null, scheduleDeleteError);
                }

                var scheduleDeleted = await context.FirewallManager.Schedules.DeleteScheduleAsync(scheduleDeleteRequest.Id);
                return scheduleDeleted
                    ? new ApiResponse(true, new { id = scheduleDeleteRequest.Id }, null)
                    : new ApiResponse(false, null, "Failed to delete schedule");

            case "firewall.schedules.active":
                if (!CoreRequestParsing.TryGetPayload(request, out FirewallIdRequest scheduleActiveRequest, out var scheduleActiveError))
                {
                    return new ApiResponse(false, null, scheduleActiveError);
                }

                var isActive = await context.FirewallManager.Schedules.IsScheduleActiveAsync(scheduleActiveRequest.Id);
                return new ApiResponse(true, new { id = scheduleActiveRequest.Id, active = isActive }, null);

            case "firewall.status":
                var aliasCount = (await context.FirewallManager.Aliases.ListAliasesAsync()).Count;
                var ruleCount = (await context.FirewallManager.Nat.ListRulesAsync()).Count;
                var filterCount = (await context.FirewallManager.Rules.ListRulesAsync()).Count;
                return new ApiResponse(true, new
                {
                    isActive = true,
                    aliases = aliasCount,
                    natRules = ruleCount,
                    filterRules = filterCount,
                    pendingChanges = 0
                }, null);

            case "firewall.config":
                return new ApiResponse(true, new
                {
                    enabled = true,
                    defaultAction = "deny",
                    logLevel = "info"
                }, null);

            case "firewall.pending":
                return new ApiResponse(true, new { count = 0 }, null);

            case "firewall.apply":
                var applyResult = await context.FirewallManager.ApplyManager.ApplyAsync(cancellationToken);
                return applyResult.Success
                    ? new ApiResponse(true, applyResult, null)
                    : new ApiResponse(false, applyResult, applyResult.Error ?? "Failed to apply firewall configuration");

            case "firewall.discard":
                return new ApiResponse(true, new { success = true }, null);

            case "firewall.preview":
                var buildResult = await context.FirewallManager.ApplyManager.BuildConfigAsync(cancellationToken);
                if (!buildResult.Success)
                {
                    return new ApiResponse(false, buildResult, buildResult.Error ?? "Failed to build firewall preview");
                }

                var configPath = buildResult.ConfigPath ?? string.Empty;
                if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
                {
                    return new ApiResponse(false, null, "Firewall preview file not found");
                }

                var configContent = await File.ReadAllTextAsync(configPath, cancellationToken);
                return new ApiResponse(true, new
                {
                    config = configContent,
                    configPath,
                    warnings = buildResult.Warnings
                }, null);

            case "firewall.states.list":
                FirewallStatesListRequest statesRequest;
                if (request.TryGetProperty("payload", out var payload))
                {
                    if (!CoreRequestParsing.TryGetPayload(request, out statesRequest, out var statesError))
                    {
                        statesRequest = new FirewallStatesListRequest();
                    }
                }
                else
                {
                    statesRequest = new FirewallStatesListRequest();
                }

                var statesResponse = await context.FirewallManager.States.ListStatesAsync(statesRequest, cancellationToken);
                return new ApiResponse(true, statesResponse, null);

            case "firewall.states.kill":
                if (!CoreRequestParsing.TryGetPayload(request, out FirewallStateKillRequest killRequest, out var killError))
                {
                    return new ApiResponse(false, null, killError);
                }

                if (string.IsNullOrWhiteSpace(killRequest.Id))
                {
                    return new ApiResponse(false, null, "State ID is required");
                }

                var killed = await context.FirewallManager.States.KillStateAsync(killRequest.Id, cancellationToken);
                return killed
                    ? new ApiResponse(true, new { killed = true }, null)
                    : new ApiResponse(false, null, "Failed to kill connection state");
        }

        return new ApiResponse(false, null, $"Unhandled action: {action}");
    }
}
