using System.Text.Json;
using CL.SQLite.Services;
using CodeLogic;
using Monolith.FireWall.Common.Services;
using System.Collections.Generic;

namespace Monolith.FireWall.WebUI.Features.Firewall.Aliases;

public class AliasesManager
{
    private CL.SQLite.Services.Repository<FirewallAliasEntity>? _repository;
    private CL.SQLite.Services.QueryBuilder<FirewallAliasEntity>? _queryBuilder;

    public AliasesManager()
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
                _repository = sqlite.CreateRepository<FirewallAliasEntity>();
                _queryBuilder = sqlite.CreateQueryBuilder<FirewallAliasEntity>();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize Aliases repository: {ex.Message}");
        }
    }

    public async Task<List<FirewallAlias>> ListAliasesAsync()
    {
        try
        {
            if (_repository == null || _queryBuilder == null)
            {
                return new List<FirewallAlias>();
            }

            var entitiesResult = await _queryBuilder.Select(e => e).ExecuteAsync();
            var entities = entitiesResult?.Data ?? new List<FirewallAliasEntity>();
            return entities.Select(e => new FirewallAlias
            {
                Id = e.Id,
                Name = e.Name,
                Type = e.Type,
                Description = e.Description,
                Content = JsonSerializer.Deserialize<string[]>(e.Content) ?? Array.Empty<string>(),
                Enabled = e.Enabled == 1,
                CreatedAt = e.CreatedAt,
                UpdatedAt = e.UpdatedAt
            }).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error listing aliases: {ex.Message}");
            return new List<FirewallAlias>();
        }
    }

    public async Task<FirewallAlias?> GetAliasAsync(int id)
    {
        try
        {
            if (_repository == null) return null;

            var getResult = await _repository.GetByIdAsync(id);
            if (getResult == null || !getResult.IsSuccess || getResult.Data == null) return null;
            var entity = getResult.Data;

            return new FirewallAlias
            {
                Id = entity.Id,
                Name = entity.Name,
                Type = entity.Type,
                Description = entity.Description,
                Content = JsonSerializer.Deserialize<string[]>(entity.Content) ?? Array.Empty<string>(),
                Enabled = entity.Enabled == 1,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting alias: {ex.Message}");
            return null;
        }
    }

    public async Task<FirewallAlias> CreateAliasAsync(FirewallAlias alias)
    {
        try
        {
            if (_repository == null)
            {
                throw new Exception("Repository not initialized");
            }

            var entity = new FirewallAliasEntity
            {
                Name = alias.Name,
                Type = alias.Type,
                Description = alias.Description,
                Content = JsonSerializer.Serialize(alias.Content),
                Enabled = alias.Enabled ? 1 : 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var insertResult = await _repository.InsertAsync(entity);
            if (insertResult != null && insertResult.IsSuccess && insertResult.Data > 0) {
                entity.Id = (int)insertResult.Data;
            }
            
            // Log the change
            await LoggingManager.Instance.LogMonolithAsync(
                "Changes",
                "Info",
                "firewall/aliases",
                $"Created firewall alias: {alias.Name} (Type: {alias.Type})",
                null,
                null,
                new Dictionary<string, object> { { "aliasId", entity.Id }, { "aliasName", alias.Name }, { "aliasType", alias.Type } }
            );
            
            return new FirewallAlias
            {
                Id = entity.Id,
                Name = entity.Name,
                Type = entity.Type,
                Description = entity.Description,
                Content = alias.Content,
                Enabled = entity.Enabled == 1,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating alias: {ex.Message}");
            throw;
        }
    }

    public async Task<FirewallAlias> UpdateAliasAsync(int id, FirewallAlias alias)
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
                throw new Exception($"Alias with ID {id} not found");
            }

            var entity = getResult.Data;
            entity.Name = alias.Name;
            entity.Type = alias.Type;
            entity.Description = alias.Description;
            entity.Content = JsonSerializer.Serialize(alias.Content);
            entity.Enabled = alias.Enabled ? 1 : 0;
            entity.UpdatedAt = DateTime.UtcNow;

            await _repository.UpdateAsync(entity);

            // Log the change
            await LoggingManager.Instance.LogMonolithAsync(
                "Changes",
                "Info",
                "firewall/aliases",
                $"Updated firewall alias: {alias.Name} (ID: {id})",
                null,
                null,
                new Dictionary<string, object> { { "aliasId", id }, { "aliasName", alias.Name }, { "aliasType", alias.Type } }
            );

            return new FirewallAlias
            {
                Id = entity.Id,
                Name = entity.Name,
                Type = entity.Type,
                Description = entity.Description,
                Content = alias.Content,
                Enabled = entity.Enabled == 1,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating alias: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> DeleteAliasAsync(int id)
    {
        try
        {
            if (_repository == null)
            {
                throw new Exception("Repository not initialized");
            }

            // Get alias name before deletion for logging
            var aliasToDelete = await GetAliasAsync(id);
            var aliasName = aliasToDelete?.Name ?? "Unknown";

            var deleteResult = await _repository.DeleteAsync(id);
            var success = (deleteResult != null && deleteResult.IsSuccess);
            
            // Log the change
            if (success)
            {
                await LoggingManager.Instance.LogMonolithAsync(
                    "Changes",
                    "Info",
                    "firewall/aliases",
                    $"Deleted firewall alias: {aliasName} (ID: {id})",
                    null,
                    null,
                    new Dictionary<string, object> { { "aliasId", id }, { "aliasName", aliasName } }
                );
            }
            
            return success;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting alias: {ex.Message}");
            throw;
        }
    }

    public bool ValidateAliasContent(string type, string[] content)
    {
        // Basic validation - can be enhanced
        if (content == null || content.Length == 0)
        {
            return false;
        }

        // TODO: Add type-specific validation (IP addresses, networks, ports, URLs)
        return true;
    }
}
