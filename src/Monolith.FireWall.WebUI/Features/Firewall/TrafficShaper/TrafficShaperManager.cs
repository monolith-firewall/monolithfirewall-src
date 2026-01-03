using CL.SQLite.Services;
using CodeLogic;
using Monolith.FireWall.Common.Services;
using System.Collections.Generic;

namespace Monolith.FireWall.WebUI.Features.Firewall.TrafficShaper;

public class TrafficShaperManager
{
    private CL.SQLite.Services.Repository<TrafficShaperRuleEntity>? _repository;
    private CL.SQLite.Services.QueryBuilder<TrafficShaperRuleEntity>? _queryBuilder;

    public TrafficShaperManager()
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
                _repository = sqlite.CreateRepository<TrafficShaperRuleEntity>();
                _queryBuilder = sqlite.CreateQueryBuilder<TrafficShaperRuleEntity>();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize Traffic Shaper repository: {ex.Message}");
        }
    }

    public async Task<List<TrafficShaperRule>> ListRulesAsync()
    {
        try
        {
            if (_repository == null || _queryBuilder == null)
            {
                return new List<TrafficShaperRule>();
            }

            var entitiesResult = await _queryBuilder.Select(e => e).ExecuteAsync();
            var entities = entitiesResult?.Data ?? new List<TrafficShaperRuleEntity>();
            return entities.Select(EntityToRule).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error listing traffic shaper rules: {ex.Message}");
            return new List<TrafficShaperRule>();
        }
    }

    public async Task<TrafficShaperRule?> GetRuleAsync(int id)
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
            Console.WriteLine($"Error getting traffic shaper rule: {ex.Message}");
            return null;
        }
    }

    public async Task<TrafficShaperRule> CreateRuleAsync(TrafficShaperRule rule)
    {
        try
        {
            if (_repository == null)
            {
                throw new Exception("Repository not initialized");
            }

            var entity = RuleToEntity(rule);
            entity.CreatedAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;

            var insertResult = await _repository.InsertAsync(entity);
            if (insertResult != null && insertResult.IsSuccess && insertResult.Data > 0) {
                entity.Id = (int)insertResult.Data;
            }
            
            // Log the change
            await LoggingManager.Instance.LogMonolithAsync(
                "Changes",
                "Info",
                "firewall/traffic-shaper",
                $"Created traffic shaper rule: {rule.Name} (Interface: {rule.Interface})",
                null,
                null,
                new Dictionary<string, object> { { "ruleId", entity.Id }, { "ruleName", rule.Name }, { "interface", rule.Interface } }
            );
            
            return EntityToRule(entity);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating traffic shaper rule: {ex.Message}");
            throw;
        }
    }

    public async Task<TrafficShaperRule> UpdateRuleAsync(int id, TrafficShaperRule rule)
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
                throw new Exception($"Traffic shaper rule with ID {id} not found");
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
                "firewall/traffic-shaper",
                $"Updated traffic shaper rule: {rule.Name} (ID: {id})",
                null,
                null,
                new Dictionary<string, object> { { "ruleId", id }, { "ruleName", rule.Name }, { "interface", rule.Interface } }
            );
            
            return EntityToRule(updated);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating traffic shaper rule: {ex.Message}");
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
            var ruleName = ruleToDelete?.Name ?? "Unknown";

            var deleteResult = await _repository.DeleteAsync(id);
            var success = (deleteResult != null && deleteResult.IsSuccess);
            
            // Log the change
            if (success)
            {
                await LoggingManager.Instance.LogMonolithAsync(
                    "Changes",
                    "Info",
                    "firewall/traffic-shaper",
                    $"Deleted traffic shaper rule: {ruleName} (ID: {id})",
                    null,
                    null,
                    new Dictionary<string, object> { { "ruleId", id }, { "ruleName", ruleName } }
                );
            }
            
            return success;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting traffic shaper rule: {ex.Message}");
            throw;
        }
    }

    private TrafficShaperRule EntityToRule(TrafficShaperRuleEntity entity)
    {
        return new TrafficShaperRule
        {
            Id = entity.Id,
            Name = entity.Name,
            Interface = entity.Interface,
            BandwidthUp = entity.BandwidthUp,
            BandwidthDown = entity.BandwidthDown,
            Scheduler = entity.Scheduler,
            Description = entity.Description,
            Enabled = entity.Enabled == 1,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    private TrafficShaperRuleEntity RuleToEntity(TrafficShaperRule rule)
    {
        return new TrafficShaperRuleEntity
        {
            Id = rule.Id,
            Name = rule.Name,
            Interface = rule.Interface,
            BandwidthUp = rule.BandwidthUp,
            BandwidthDown = rule.BandwidthDown,
            Scheduler = rule.Scheduler,
            Description = rule.Description,
            Enabled = rule.Enabled ? 1 : 0
        };
    }
}
