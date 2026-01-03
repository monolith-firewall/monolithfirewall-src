using CodeLogic;
using CL.SQLite.Services;
using Monolith.FireWall.Common.Services;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Services.Firewall;

public sealed class FirewallNatManager
{
    private readonly LoggingManager _loggingManager;
    private CL.SQLite.SQLiteLibrary? _sqlite;
    private Repository<FirewallNatRuleEntity>? _repository;

    public FirewallNatManager()
    {
        _loggingManager = LoggingManager.Instance;
        Initialize();
    }

    public async Task<List<FirewallNatRuleView>> ListRulesAsync()
    {
        if (_repository == null)
        {
            return new List<FirewallNatRuleView>();
        }

        var result = await _repository.GetAllAsync();
        var rules = result.IsSuccess && result.Data != null
            ? result.Data.ToList()
            : new List<FirewallNatRuleEntity>();

        return rules
            .OrderBy(r => r.RuleNumber)
            .Select(BuildView)
            .ToList();
    }

    public async Task<FirewallNatRuleView?> GetRuleAsync(int id)
    {
        var entity = await GetEntityAsync(id);
        return entity == null ? null : BuildView(entity);
    }

    public async Task<(bool Success, string? Error, FirewallNatRuleView? Rule)> CreateRuleAsync(FirewallNatRuleRequest request)
    {
        if (_repository == null)
        {
            return (false, "NAT storage not available", null);
        }

        var validation = ValidateRequest(request);
        if (!validation.Success)
        {
            return (false, validation.Error, null);
        }

        var maxRuleNumber = await GetMaxRuleNumberAsync();

        var now = DateTime.UtcNow;
        var entity = new FirewallNatRuleEntity
        {
            RuleNumber = maxRuleNumber + 1,
            Type = NormalizeNatType(request.Type),
            Interface = request.Interface!.Trim(),
            AddressFamily = NormalizeAddressFamily(request.AddressFamily),
            Protocol = NormalizeProtocol(request.Protocol),
            SourceType = NormalizeAddressType(request.SourceType),
            SourceValue = NormalizeValue(request.SourceValue),
            SourcePort = NormalizeValue(request.SourcePort),
            DestinationType = NormalizeAddressType(request.DestinationType),
            DestinationValue = NormalizeValue(request.DestinationValue),
            DestinationPort = NormalizeValue(request.DestinationPort),
            RedirectTargetIp = NormalizeValue(request.RedirectTargetIp),
            RedirectTargetPort = NormalizeValue(request.RedirectTargetPort),
            ReflectionMode = NormalizeReflectionMode(request.ReflectionMode),
            Enabled = request.Enabled,
            Description = request.Description?.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        var insert = await _repository.InsertAsync(entity);
        if (!insert.IsSuccess || insert.Data <= 0)
        {
            return (false, "Failed to create NAT rule", null);
        }

        entity.Id = (int)insert.Data;

        await _loggingManager.LogSecurityAsync(
            "Firewall",
            "Info",
            "FirewallNat",
            $"Created NAT rule #{entity.RuleNumber}",
            details: new Dictionary<string, object>
            {
                ["ruleId"] = entity.Id,
                ["interface"] = entity.Interface
            });

        return (true, null, BuildView(entity));
    }

    public async Task<(bool Success, string? Error, FirewallNatRuleView? Rule)> UpdateRuleAsync(int id, FirewallNatRuleRequest request)
    {
        if (_repository == null)
        {
            return (false, "NAT storage not available", null);
        }

        var entity = await GetEntityAsync(id);
        if (entity == null)
        {
            return (false, "NAT rule not found", null);
        }

        var validation = ValidateRequest(request);
        if (!validation.Success)
        {
            return (false, validation.Error, null);
        }

        entity.Type = NormalizeNatType(request.Type);
        entity.Interface = request.Interface!.Trim();
        entity.AddressFamily = NormalizeAddressFamily(request.AddressFamily);
        entity.Protocol = NormalizeProtocol(request.Protocol);
        entity.SourceType = NormalizeAddressType(request.SourceType);
        entity.SourceValue = NormalizeValue(request.SourceValue);
        entity.SourcePort = NormalizeValue(request.SourcePort);
        entity.DestinationType = NormalizeAddressType(request.DestinationType);
        entity.DestinationValue = NormalizeValue(request.DestinationValue);
        entity.DestinationPort = NormalizeValue(request.DestinationPort);
        entity.RedirectTargetIp = NormalizeValue(request.RedirectTargetIp);
        entity.RedirectTargetPort = NormalizeValue(request.RedirectTargetPort);
        entity.ReflectionMode = NormalizeReflectionMode(request.ReflectionMode);
        entity.Enabled = request.Enabled;
        entity.Description = request.Description?.Trim();
        entity.UpdatedAt = DateTime.UtcNow;

        var update = await _repository.UpdateAsync(entity);
        if (!update.IsSuccess)
        {
            return (false, "Failed to update NAT rule", null);
        }

        await _loggingManager.LogSecurityAsync(
            "Firewall",
            "Info",
            "FirewallNat",
            $"Updated NAT rule #{entity.RuleNumber}",
            details: new Dictionary<string, object>
            {
                ["ruleId"] = entity.Id,
                ["interface"] = entity.Interface
            });

        return (true, null, BuildView(entity));
    }

    public async Task<bool> DeleteRuleAsync(int id)
    {
        if (_repository == null)
        {
            return false;
        }

        var entity = await GetEntityAsync(id);
        if (entity == null)
        {
            return true;
        }

        var delete = await _repository.DeleteAsync(id);
        if (!delete.IsSuccess)
        {
            return false;
        }

        await ReindexRulesAsync();

        await _loggingManager.LogSecurityAsync(
            "Firewall",
            "Info",
            "FirewallNat",
            $"Deleted NAT rule #{entity.RuleNumber}",
            details: new Dictionary<string, object>
            {
                ["ruleId"] = entity.Id
            });

        return true;
    }

    public async Task<bool> ReorderRulesAsync(int[] ruleIds)
    {
        if (_repository == null)
        {
            return false;
        }

        var ruleLookup = (await ListRulesAsync())
            .ToDictionary(r => r.Id);

        var ordered = ruleIds
            .Where(ruleLookup.ContainsKey)
            .ToList();

        if (ordered.Count == 0)
        {
            return false;
        }

        var position = 1;
        foreach (var ruleId in ordered)
        {
            var entity = await GetEntityAsync(ruleId);
            if (entity == null)
            {
                continue;
            }

            entity.RuleNumber = position++;
            entity.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(entity);
        }

        return true;
    }

    private static FirewallNatRuleView BuildView(FirewallNatRuleEntity entity)
    {
        return new FirewallNatRuleView
        {
            Id = entity.Id,
            RuleNumber = entity.RuleNumber,
            Type = entity.Type,
            Interface = entity.Interface,
            AddressFamily = entity.AddressFamily,
            Protocol = entity.Protocol,
            SourceType = entity.SourceType,
            SourceValue = entity.SourceValue,
            SourcePort = entity.SourcePort,
            DestinationType = entity.DestinationType,
            DestinationValue = entity.DestinationValue,
            DestinationPort = entity.DestinationPort,
            RedirectTargetIp = entity.RedirectTargetIp,
            RedirectTargetPort = entity.RedirectTargetPort,
            ReflectionMode = entity.ReflectionMode,
            Enabled = entity.Enabled,
            Description = entity.Description,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    private (bool Success, string? Error) ValidateRequest(FirewallNatRuleRequest? request)
    {
        if (request == null)
        {
            return (false, "Request is required");
        }

        if (string.IsNullOrWhiteSpace(request.Interface))
        {
            return (false, "Interface is required");
        }

        if (string.IsNullOrWhiteSpace(request.Protocol))
        {
            return (false, "Protocol is required");
        }

        if (string.IsNullOrWhiteSpace(request.AddressFamily))
        {
            return (false, "Address family is required");
        }

        return (true, null);
    }

    private async Task<int> GetMaxRuleNumberAsync()
    {
        if (_sqlite == null)
        {
            return 0;
        }

        var query = _sqlite.CreateQueryBuilder<FirewallNatRuleEntity>();
        var result = await query
            .OrderByDescending(r => r.RuleNumber)
            .FirstOrDefaultAsync();

        return result.IsSuccess && result.Data != null ? result.Data.RuleNumber : 0;
    }

    private async Task<FirewallNatRuleEntity?> GetEntityAsync(int id)
    {
        if (_repository == null)
        {
            return null;
        }

        var result = await _repository.GetByIdAsync(id);
        return result.IsSuccess ? result.Data : null;
    }

    private async Task ReindexRulesAsync()
    {
        if (_repository == null)
        {
            return;
        }

        var rules = await ListRulesAsync();
        var position = 1;

        foreach (var rule in rules)
        {
            var entity = await GetEntityAsync(rule.Id);
            if (entity == null)
            {
                continue;
            }

            entity.RuleNumber = position++;
            entity.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(entity);
        }
    }

    private static string NormalizeNatType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return "port_forward";
        }

        var normalized = type.Trim().ToLowerInvariant();
        return normalized switch
        {
            "port_forward" => "port_forward",
            "one_to_one" => "one_to_one",
            "outbound" => "outbound",
            _ => "port_forward"
        };
    }

    private static string NormalizeAddressFamily(string? family)
    {
        if (string.IsNullOrWhiteSpace(family))
        {
            return "ipv4";
        }

        var normalized = family.Trim().ToLowerInvariant();
        return normalized switch
        {
            "ipv4" => "ipv4",
            "ipv6" => "ipv6",
            "dual" => "dual",
            _ => "ipv4"
        };
    }

    private static string NormalizeProtocol(string? protocol)
    {
        if (string.IsNullOrWhiteSpace(protocol))
        {
            return "tcp";
        }

        var normalized = protocol.Trim().ToLowerInvariant();
        return normalized switch
        {
            "tcp" => "tcp",
            "udp" => "udp",
            "tcp/udp" => "tcp/udp",
            "icmp" => "icmp",
            "any" => "any",
            _ => "tcp"
        };
    }

    private static string NormalizeAddressType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return "any";
        }

        var normalized = type.Trim().ToLowerInvariant();
        return normalized switch
        {
            "any" => "any",
            "single" => "single",
            "network" => "network",
            "alias" => "alias",
            _ => "any"
        };
    }

    private static string NormalizeReflectionMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return "default";
        }

        var normalized = mode.Trim().ToLowerInvariant();
        return normalized switch
        {
            "default" => "default",
            "proxy" => "proxy",
            "nat" => "nat",
            "disabled" => "disabled",
            _ => "default"
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

            _sqlite = sqlite;
            _repository = sqlite.CreateRepository<FirewallNatRuleEntity>();
        }
        catch
        {
            _sqlite = null;
            _repository = null;
        }
    }
}
