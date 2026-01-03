using CL.SQLite.Services;
using CodeLogic;
using Monolith.FireWall.Common.Services;
using System.Collections.Generic;

namespace Monolith.FireWall.WebUI.Features.Firewall.Nat;

public class NatManager
{
    private CL.SQLite.Services.Repository<NatRuleEntity>? _repository;
    private CL.SQLite.Services.QueryBuilder<NatRuleEntity>? _queryBuilder;

    public NatManager()
    {
        InitializeRepository();
    }

    private void InitializeRepository()
    {
        try
        {
            var sqlite = CodeLogic.Libs.Get<CL.SQLite.SQLiteLibrary>();
            if (sqlite != null)
            {
                _repository = sqlite.CreateRepository<NatRuleEntity>();
                _queryBuilder = sqlite.CreateQueryBuilder<NatRuleEntity>();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize NAT repository: {ex.Message}");
        }
    }

    public async Task<List<NatRule>> ListRulesAsync()
    {
        try
        {
            if (_repository == null || _queryBuilder == null)
            {
                return new List<NatRule>();
            }

            var entitiesResult = await _queryBuilder.Select(e => e).OrderBy(e => e.RuleNumber).ExecuteAsync();
            var entities = entitiesResult?.Data ?? new List<NatRuleEntity>();
            return entities.Select(e => EntityToRule(e)).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error listing NAT rules: {ex.Message}");
            return new List<NatRule>();
        }
    }

    public async Task<NatRule?> GetRuleAsync(int id)
    {
        try
        {
            if (_repository == null) return null;

            var result = await _repository.GetByIdAsync(id);
            if (result == null || !result.IsSuccess || result.Data == null) return null;
            var entity = result.Data;
            return entity != null ? EntityToRule(entity) : null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting NAT rule: {ex.Message}");
            return null;
        }
    }

    public async Task<NatRule> CreateRuleAsync(NatRule rule)
    {
        try
        {
            if (_repository == null)
            {
                throw new Exception("Repository not initialized");
            }

            // Get next rule number if not specified
            if (rule.RuleNumber == 0)
            {
                var maxRuleResult = await _queryBuilder?.Select(e => e).OrderByDescending(e => e.RuleNumber).Take(1).ExecuteAsync();
                var maxRule = maxRuleResult?.Data?.FirstOrDefault();
                rule.RuleNumber = (maxRule?.RuleNumber ?? 0) + 1;
            }

            var entity = RuleToEntity(rule);
            entity.CreatedAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;

            var insertResult = await _repository.InsertAsync(entity);
            if (insertResult != null && insertResult.IsSuccess && insertResult.Data > 0)
            {
                entity.Id = (int)insertResult.Data;
            }
            
            // Log the change
            await LoggingManager.Instance.LogMonolithAsync(
                "Changes",
                "Info",
                "firewall/nat",
                $"Created NAT rule: {rule.Description ?? $"Rule #{rule.RuleNumber}"} (ID: {entity.Id})",
                null,
                null,
                new Dictionary<string, object> { { "ruleId", entity.Id }, { "ruleNumber", rule.RuleNumber }, { "description", rule.Description ?? "" } }
            );
            
            return EntityToRule(entity);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating NAT rule: {ex.Message}");
            throw;
        }
    }

    public async Task<NatRule> UpdateRuleAsync(int id, NatRule rule)
    {
        try
        {
            if (_repository == null)
            {
                throw new Exception("Repository not initialized");
            }

            var getResult = await _repository.GetByIdAsync(id);
            if (getResult == null || !getResult.IsSuccess || getResult.Data == null)
            {
                throw new Exception($"NAT rule with ID {id} not found");
            }
            var existing = getResult.Data;

            var updated = RuleToEntity(rule);
            updated.Id = id;
            updated.CreatedAt = existing.CreatedAt;
            updated.UpdatedAt = DateTime.UtcNow;

            await _repository.UpdateAsync(updated);
            
            // Log the change
            await LoggingManager.Instance.LogMonolithAsync(
                "Changes",
                "Info",
                "firewall/nat",
                $"Updated NAT rule: {rule.Description ?? $"Rule #{rule.RuleNumber}"} (ID: {id})",
                null,
                null,
                new Dictionary<string, object> { { "ruleId", id }, { "ruleNumber", rule.RuleNumber }, { "description", rule.Description ?? "" } }
            );
            
            return EntityToRule(updated);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating NAT rule: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> DeleteRuleAsync(int id)
    {
        try
        {
            if (_repository == null)
            {
                throw new Exception("Repository not initialized");
            }

            // Get rule info before deletion for logging
            var ruleToDelete = await GetRuleAsync(id);
            var ruleDesc = ruleToDelete?.Description ?? $"Rule #{ruleToDelete?.RuleNumber ?? 0}";

            var deleteResult = await _repository.DeleteAsync(id);
            var success = (deleteResult != null && deleteResult.IsSuccess);
            
            // Log the change
            if (success)
            {
                await LoggingManager.Instance.LogMonolithAsync(
                    "Changes",
                    "Info",
                    "firewall/nat",
                    $"Deleted NAT rule: {ruleDesc} (ID: {id})",
                    null,
                    null,
                    new Dictionary<string, object> { { "ruleId", id }, { "ruleNumber", ruleToDelete?.RuleNumber ?? 0 } }
                );
            }
            
            return success;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting NAT rule: {ex.Message}");
            throw;
        }
    }

    public async Task ReorderRulesAsync(int[] ruleIds)
    {
        try
        {
            if (_repository == null)
            {
                throw new Exception("Repository not initialized");
            }

            for (int i = 0; i < ruleIds.Length; i++)
            {
                var getResult = await _repository.GetByIdAsync(ruleIds[i]);
                if (getResult != null && getResult.IsSuccess && getResult.Data != null)
                {
                    var entity = getResult.Data;
                    entity.RuleNumber = i + 1;
                    entity.UpdatedAt = DateTime.UtcNow;
                    await _repository.UpdateAsync(entity);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reordering NAT rules: {ex.Message}");
            throw;
        }
    }

    private NatRule EntityToRule(NatRuleEntity entity)
    {
        return new NatRule
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
            Description = entity.Description,
            Enabled = entity.Enabled == 1,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    private NatRuleEntity RuleToEntity(NatRule rule)
    {
        return new NatRuleEntity
        {
            Id = rule.Id,
            RuleNumber = rule.RuleNumber,
            Type = rule.Type,
            Interface = rule.Interface,
            AddressFamily = rule.AddressFamily,
            Protocol = rule.Protocol,
            SourceType = rule.SourceType,
            SourceValue = rule.SourceValue,
            SourcePort = rule.SourcePort,
            DestinationType = rule.DestinationType,
            DestinationValue = rule.DestinationValue,
            DestinationPort = rule.DestinationPort,
            RedirectTargetIp = rule.RedirectTargetIp,
            RedirectTargetPort = rule.RedirectTargetPort,
            ReflectionMode = rule.ReflectionMode,
            Description = rule.Description,
            Enabled = rule.Enabled ? 1 : 0
        };
    }
}
