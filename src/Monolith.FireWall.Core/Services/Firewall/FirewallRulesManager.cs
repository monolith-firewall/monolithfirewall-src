using CodeLogic;
using CL.SQLite.Services;
using Monolith.FireWall.Common.Services;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Services.Firewall;

public sealed class FirewallRulesManager
{
    private readonly LoggingManager _loggingManager;
    private Repository<FirewallRuleEntity>? _repository;
    private readonly InterfaceAssignmentStore _interfaceStore;

    public FirewallRulesManager(InterfaceAssignmentStore interfaceStore)
    {
        _loggingManager = LoggingManager.Instance;
        _interfaceStore = interfaceStore;
        Initialize();
    }

    public async Task<List<FirewallRuleView>> ListRulesAsync()
    {
        var rules = await ListUserRulesAsync();
        return rules.OrderBy(r => r.Interface, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.RuleNumber)
            .ToList();
    }

    public async Task<FirewallRuleView?> GetRuleAsync(int id)
    {
        if (_repository == null)
        {
            return null;
        }

        var result = await _repository.GetByIdAsync(id);
        if (!result.IsSuccess || result.Data == null)
        {
            return null;
        }

        return BuildView(result.Data, isSystem: false, systemTag: null);
    }

    public async Task<List<FirewallRuleView>> GetEffectiveRulesAsync(FirewallDefaultsView defaults)
    {
        var assignments = await _interfaceStore.GetAssignmentsAsync();
        var userRules = await ListUserRulesAsync();

        var grouped = userRules
            .GroupBy(r => r.Interface, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.RuleNumber).ToList(), StringComparer.OrdinalIgnoreCase);

        var effective = new List<FirewallRuleView>();

        foreach (var assignment in assignments)
        {
            var systemRules = BuildSystemRules(assignment, defaults);
            effective.AddRange(systemRules);

            if (grouped.TryGetValue(assignment.InterfaceName, out var rules))
            {
                effective.AddRange(rules);
            }
        }

        return effective;
    }

    public async Task<(bool Success, string? Error, FirewallRuleView? Rule)> CreateRuleAsync(FirewallRuleRequest request)
    {
        if (_repository == null)
        {
            return (false, "Rule storage not available", null);
        }

        var validation = ValidateRequest(request);
        if (!validation.Success)
        {
            return (false, validation.Error, null);
        }

        var interfaceName = request.Interface!.Trim();
        if (!await IsInterfaceAssignedAsync(interfaceName))
        {
            return (false, $"Interface '{interfaceName}' is not assigned", null);
        }
        var maxRuleNumber = await GetMaxRuleNumberAsync(interfaceName);
        var now = DateTime.UtcNow;

        var entity = new FirewallRuleEntity
        {
            Interface = interfaceName,
            RuleNumber = maxRuleNumber + 1,
            Direction = NormalizeDirection(request.Direction),
            Action = NormalizeAction(request.Action),
            AddressFamily = NormalizeAddressFamily(request.AddressFamily),
            Protocol = NormalizeProtocol(request.Protocol),
            SourceType = NormalizeAddressType(request.SourceType),
            SourceValue = NormalizeValue(request.SourceValue),
            SourcePort = NormalizeValue(request.SourcePort),
            DestinationType = NormalizeAddressType(request.DestinationType),
            DestinationValue = NormalizeValue(request.DestinationValue),
            DestinationPort = NormalizeValue(request.DestinationPort),
            Gateway = NormalizeValue(request.Gateway),
            LogEnabled = request.LogEnabled,
            IsManaged = false,
            ManagedSourceType = null,
            ManagedSourceId = null,
            Enabled = request.Enabled,
            Description = request.Description?.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        var insert = await _repository.InsertAsync(entity);
        if (!insert.IsSuccess || insert.Data <= 0)
        {
            return (false, "Failed to create rule", null);
        }

        entity.Id = (int)insert.Data;

        await _loggingManager.LogSecurityAsync(
            "Firewall",
            "Info",
            "FirewallRules",
            $"Created firewall rule #{entity.RuleNumber} on {entity.Interface}",
            details: new Dictionary<string, object>
            {
                ["ruleId"] = entity.Id,
                ["interface"] = entity.Interface
            });

        return (true, null, BuildView(entity, isSystem: false, systemTag: null));
    }

    public async Task<(bool Success, string? Error, FirewallRuleView? Rule)> UpdateRuleAsync(int id, FirewallRuleRequest request)
    {
        if (_repository == null)
        {
            return (false, "Rule storage not available", null);
        }

        var existingResult = await _repository.GetByIdAsync(id);
        if (!existingResult.IsSuccess || existingResult.Data == null)
        {
            return (false, "Rule not found", null);
        }

        var validation = ValidateRequest(request);
        if (!validation.Success)
        {
            return (false, validation.Error, null);
        }

        var interfaceName = request.Interface!.Trim();
        if (!await IsInterfaceAssignedAsync(interfaceName))
        {
            return (false, $"Interface '{interfaceName}' is not assigned", null);
        }

        var existing = existingResult.Data;
        existing.Interface = interfaceName;
        existing.Direction = NormalizeDirection(request.Direction);
        existing.Action = NormalizeAction(request.Action);
        existing.AddressFamily = NormalizeAddressFamily(request.AddressFamily);
        existing.Protocol = NormalizeProtocol(request.Protocol);
        existing.SourceType = NormalizeAddressType(request.SourceType);
        existing.SourceValue = NormalizeValue(request.SourceValue);
        existing.SourcePort = NormalizeValue(request.SourcePort);
        existing.DestinationType = NormalizeAddressType(request.DestinationType);
        existing.DestinationValue = NormalizeValue(request.DestinationValue);
        existing.DestinationPort = NormalizeValue(request.DestinationPort);
        existing.Gateway = NormalizeValue(request.Gateway);
        existing.LogEnabled = request.LogEnabled;
        // Preserve managed metadata for package-owned rules.
        var existingManaged = existing.IsManaged;
        var existingSourceType = existing.ManagedSourceType;
        var existingSourceId = existing.ManagedSourceId;
        existing.Enabled = request.Enabled;
        existing.Description = request.Description?.Trim();
        existing.UpdatedAt = DateTime.UtcNow;
        existing.IsManaged = existingManaged;
        existing.ManagedSourceType = existingSourceType;
        existing.ManagedSourceId = existingSourceId;

        var update = await _repository.UpdateAsync(existing);
        if (!update.IsSuccess)
        {
            return (false, "Failed to update rule", null);
        }

        await _loggingManager.LogSecurityAsync(
            "Firewall",
            "Info",
            "FirewallRules",
            $"Updated firewall rule #{existing.RuleNumber} on {existing.Interface}",
            details: new Dictionary<string, object>
            {
                ["ruleId"] = existing.Id,
                ["interface"] = existing.Interface
            });

        return (true, null, BuildView(existing, isSystem: false, systemTag: null));
    }

    public async Task<bool> DeleteRuleAsync(int id)
    {
        if (_repository == null)
        {
            return false;
        }

        var existingResult = await _repository.GetByIdAsync(id);
        if (!existingResult.IsSuccess || existingResult.Data == null)
        {
            return true;
        }

        var entity = existingResult.Data;
        var delete = await _repository.DeleteAsync(id);
        if (!delete.IsSuccess)
        {
            return false;
        }

        await ReindexRulesAsync(entity.Interface);

        await _loggingManager.LogSecurityAsync(
            "Firewall",
            "Info",
            "FirewallRules",
            $"Deleted firewall rule #{entity.RuleNumber} on {entity.Interface}",
            details: new Dictionary<string, object>
            {
                ["ruleId"] = entity.Id,
                ["interface"] = entity.Interface
            });

        return true;
    }

    public async Task<bool> ReorderRulesAsync(string interfaceName, List<int> ruleIds)
    {
        if (_repository == null)
        {
            return false;
        }

        var orderedIds = ruleIds.Where(id => id > 0).ToList();
        if (orderedIds.Count == 0)
        {
            return false;
        }

        var position = 1;
        foreach (var ruleId in orderedIds)
        {
            var result = await _repository.GetByIdAsync(ruleId);
            if (!result.IsSuccess || result.Data == null)
            {
                continue;
            }

            var entity = result.Data;
            if (!entity.Interface.Equals(interfaceName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            entity.RuleNumber = position++;
            entity.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(entity);
        }

        return true;
    }

    public async Task<(bool Success, string? Error, FirewallRuleView? Rule)> UpsertManagedRuleAsync(FirewallManagedRuleRequest request)
    {
        if (_repository == null)
        {
            return (false, "Rule storage not available", null);
        }

        if (string.IsNullOrWhiteSpace(request.PackageId))
        {
            return (false, "Package ID is required", null);
        }

        if (string.IsNullOrWhiteSpace(request.Interface))
        {
            return (false, "Interface is required", null);
        }

        var key = $"{request.PackageId}:{request.ModuleId ?? "default"}";

        var allResult = await _repository.GetAllAsync();
        var existing = allResult.IsSuccess && allResult.Data != null
            ? allResult.Data.FirstOrDefault(r => r.IsManaged && r.ManagedSourceId == key)
            : null;

        if (existing != null)
        {
            existing.Interface = request.Interface!.Trim();
            existing.Direction = NormalizeDirection(request.Direction);
            existing.Action = NormalizeAction(request.Action);
            existing.AddressFamily = NormalizeAddressFamily(request.AddressFamily);
            existing.Protocol = NormalizeProtocol(request.Protocol);
            existing.SourceType = NormalizeAddressType(request.SourceType);
            existing.SourceValue = NormalizeValue(request.SourceValue);
            existing.SourcePort = NormalizeValue(request.SourcePort);
            existing.DestinationType = NormalizeAddressType(request.DestinationType);
            existing.DestinationValue = NormalizeValue(request.DestinationValue);
            existing.DestinationPort = NormalizeValue(request.DestinationPort);
            existing.Gateway = NormalizeValue(request.Gateway);
            existing.LogEnabled = request.LogEnabled;
            existing.Enabled = request.Enabled;
            existing.Description = request.Description?.Trim();
            existing.UpdatedAt = DateTime.UtcNow;

            var update = await _repository.UpdateAsync(existing);
            if (!update.IsSuccess)
            {
                return (false, "Failed to update managed rule", null);
            }

            return (true, null, BuildView(existing, isSystem: false, systemTag: null));
        }

        var maxRuleNumber = await GetMaxRuleNumberAsync(request.Interface!.Trim());
        var now = DateTime.UtcNow;
        var entity = new FirewallRuleEntity
        {
            Interface = request.Interface!.Trim(),
            RuleNumber = maxRuleNumber + 1,
            Direction = NormalizeDirection(request.Direction),
            Action = NormalizeAction(request.Action),
            AddressFamily = NormalizeAddressFamily(request.AddressFamily),
            Protocol = NormalizeProtocol(request.Protocol),
            SourceType = NormalizeAddressType(request.SourceType),
            SourceValue = NormalizeValue(request.SourceValue),
            SourcePort = NormalizeValue(request.SourcePort),
            DestinationType = NormalizeAddressType(request.DestinationType),
            DestinationValue = NormalizeValue(request.DestinationValue),
            DestinationPort = NormalizeValue(request.DestinationPort),
            Gateway = NormalizeValue(request.Gateway),
            LogEnabled = request.LogEnabled,
            IsManaged = true,
            ManagedSourceType = "package",
            ManagedSourceId = key,
            Enabled = request.Enabled,
            Description = request.Description?.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        var insert = await _repository.InsertAsync(entity);
        if (!insert.IsSuccess || insert.Data <= 0)
        {
            return (false, "Failed to create managed rule", null);
        }

        entity.Id = (int)insert.Data;
        return (true, null, BuildView(entity, isSystem: false, systemTag: null));
    }

    private async Task<List<FirewallRuleView>> ListUserRulesAsync()
    {
        if (_repository == null)
        {
            return new List<FirewallRuleView>();
        }

        var result = await _repository.GetAllAsync();
        var rules = result.IsSuccess && result.Data != null
            ? result.Data.ToList()
            : new List<FirewallRuleEntity>();

        return rules.Select(r => BuildView(r, isSystem: false, systemTag: null)).ToList();
    }

    private static FirewallRuleView BuildView(FirewallRuleEntity entity, bool isSystem, string? systemTag)
    {
        return new FirewallRuleView
        {
            Id = entity.Id,
            RuleNumber = entity.RuleNumber,
            Interface = entity.Interface,
            Direction = entity.Direction,
            Action = entity.Action,
            AddressFamily = entity.AddressFamily,
            Protocol = entity.Protocol,
            SourceType = entity.SourceType,
            SourceValue = entity.SourceValue,
            SourcePort = entity.SourcePort,
            DestinationType = entity.DestinationType,
            DestinationValue = entity.DestinationValue,
            DestinationPort = entity.DestinationPort,
            Gateway = entity.Gateway,
            LogEnabled = entity.LogEnabled,
            Enabled = entity.Enabled,
            ScheduleId = entity.ScheduleId,
            Description = entity.Description,
            IsSystem = isSystem,
            SystemTag = systemTag,
            IsManaged = entity.IsManaged,
            ManagedBy = string.IsNullOrWhiteSpace(entity.ManagedSourceId) ? null : entity.ManagedSourceId
        };
    }

    private static FirewallRuleView BuildSystemRule(
        string interfaceName,
        string direction,
        string action,
        string addressFamily,
        string protocol,
        string sourceType,
        string? sourceValue,
        string? sourcePort,
        string destinationType,
        string? destinationValue,
        string? destinationPort,
        string description,
        string systemTag)
    {
        return new FirewallRuleView
        {
            Id = 0,
            RuleNumber = 0,
            Interface = interfaceName,
            Direction = direction,
            Action = action,
            AddressFamily = addressFamily,
            Protocol = protocol,
            SourceType = sourceType,
            SourceValue = sourceValue,
            SourcePort = sourcePort,
            DestinationType = destinationType,
            DestinationValue = destinationValue,
            DestinationPort = destinationPort,
            LogEnabled = false,
            Enabled = true,
            Description = description,
            IsSystem = true,
            SystemTag = systemTag
        };
    }

    private static List<FirewallRuleView> BuildSystemRules(InterfaceAssignmentEntity assignment, FirewallDefaultsView defaults)
    {
        var rules = new List<FirewallRuleView>();
        var interfaceName = assignment.InterfaceName;

        if (assignment.Role == InterfaceRole.Wan && defaults.BlockReservedOnWan)
        {
            rules.Add(BuildSystemRule(
                interfaceName,
                "in",
                "block",
                "ipv4",
                "any",
                "system",
                "rfc1918",
                null,
                "any",
                null,
                null,
                "Block RFC1918 networks",
                "rfc1918"));

            rules.Add(BuildSystemRule(
                interfaceName,
                "in",
                "block",
                "ipv4",
                "any",
                "system",
                "iana_reserved",
                null,
                "any",
                null,
                null,
                "Block reserved networks",
                "iana_reserved"));

            rules.Add(BuildSystemRule(
                interfaceName,
                "in",
                "block",
                "ipv6",
                "any",
                "system",
                "rfc4193",
                null,
                "any",
                null,
                null,
                "Block RFC4193 ULA networks",
                "rfc4193"));

            rules.Add(BuildSystemRule(
                interfaceName,
                "in",
                "block",
                "ipv6",
                "any",
                "system",
                "iana_reserved_v6",
                null,
                "any",
                null,
                null,
                "Block reserved IPv6 networks",
                "iana_reserved_v6"));
        }

        if (assignment.IsManagement && defaults.AllowManagementWebUi)
        {
            rules.Add(BuildSystemRule(
                interfaceName,
                "in",
                "pass",
                "ipv4",
                "tcp",
                "any",
                null,
                null,
                "any",
                null,
                "80,443",
                "Allow WebUI management",
                "webui"));
        }

        return rules;
    }

    private async Task<int> GetMaxRuleNumberAsync(string interfaceName)
    {
        if (_repository == null)
        {
            return 0;
        }

        var result = await _repository.GetAllAsync();
        if (!result.IsSuccess || result.Data == null)
        {
            return 0;
        }

        var maxRule = result.Data
            .Where(r => r.Interface.Equals(interfaceName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.RuleNumber)
            .FirstOrDefault();

        return maxRule?.RuleNumber ?? 0;
    }

    private async Task ReindexRulesAsync(string interfaceName)
    {
        if (_repository == null)
        {
            return;
        }

        var rules = await ListUserRulesAsync();
        var interfaceRules = rules
            .Where(r => r.Interface.Equals(interfaceName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.RuleNumber)
            .ToList();

        var position = 1;
        foreach (var rule in interfaceRules)
        {
            var result = await _repository.GetByIdAsync(rule.Id);
            if (!result.IsSuccess || result.Data == null)
            {
                continue;
            }

            var entity = result.Data;
            entity.RuleNumber = position++;
            entity.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(entity);
        }
    }

    private (bool Success, string? Error) ValidateRequest(FirewallRuleRequest? request)
    {
        if (request == null)
        {
            return (false, "Request is required");
        }

        if (string.IsNullOrWhiteSpace(request.Interface))
        {
            return (false, "Interface is required");
        }

        if (string.IsNullOrWhiteSpace(request.Direction))
        {
            return (false, "Direction is required");
        }

        var direction = NormalizeDirection(request.Direction);
        if (direction != "in" && direction != "out" && direction != "forward")
        {
            return (false, "Invalid direction");
        }

        if (string.IsNullOrWhiteSpace(request.Action))
        {
            return (false, "Action is required");
        }

        var action = NormalizeAction(request.Action);
        if (action != "pass" && action != "block" && action != "reject")
        {
            return (false, "Invalid action");
        }

        var family = NormalizeAddressFamily(request.AddressFamily);
        if (family != "ipv4" && family != "ipv6" && family != "dual")
        {
            return (false, "Invalid address family");
        }

        var protocol = NormalizeProtocol(request.Protocol);
        if (protocol != "any" && protocol != "tcp" && protocol != "udp" && protocol != "tcp/udp" && protocol != "icmp")
        {
            return (false, "Invalid protocol");
        }

        var sourceType = NormalizeAddressType(request.SourceType);
        if (sourceType != "any" && sourceType != "single" && sourceType != "network" && sourceType != "alias" && sourceType != "system")
        {
            return (false, "Invalid source type");
        }

        if (sourceType != "any" && string.IsNullOrWhiteSpace(request.SourceValue))
        {
            return (false, "Source value is required");
        }

        var destinationType = NormalizeAddressType(request.DestinationType);
        if (destinationType != "any" && destinationType != "single" && destinationType != "network" && destinationType != "alias" && destinationType != "system")
        {
            return (false, "Invalid destination type");
        }

        if (destinationType != "any" && string.IsNullOrWhiteSpace(request.DestinationValue))
        {
            return (false, "Destination value is required");
        }

        return (true, null);
    }

    private static string NormalizeDirection(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "in";
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
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
        {
            return "pass";
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "pass" => "pass",
            "block" => "block",
            "reject" => "reject",
            _ => "pass"
        };
    }

    private static string NormalizeAddressFamily(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "ipv4";
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "ipv4" => "ipv4",
            "ipv6" => "ipv6",
            "dual" => "dual",
            _ => "ipv4"
        };
    }

    private static string NormalizeProtocol(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "any";
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
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
        {
            return "any";
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "any" => "any",
            "single" => "single",
            "network" => "network",
            "alias" => "alias",
            "system" => "system",
            _ => "any"
        };
    }

    private static string? NormalizeValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private void Initialize()
    {
        try
        {
            var sqlite = CodeLogic.Libs.Get<CL.SQLite.SQLiteLibrary>();
            if (sqlite == null)
            {
                return;
            }

            _repository = sqlite.CreateRepository<FirewallRuleEntity>();
        }
        catch
        {
            _repository = null;
        }
    }

    private async Task<bool> IsInterfaceAssignedAsync(string interfaceName)
    {
        var assignments = await _interfaceStore.GetAssignmentsAsync();
        return assignments.Any(a => a.InterfaceName.Equals(interfaceName, StringComparison.OrdinalIgnoreCase));
    }
}
