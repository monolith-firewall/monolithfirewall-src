using CL.SQLite.Services;
using CodeLogic;
using Monolith.FireWall.Common.Services;
using System.Collections.Generic;

namespace Monolith.FireWall.WebUI.Features.Firewall.VirtualIps;

public class VirtualIpsManager
{
    private CL.SQLite.Services.Repository<VirtualIpEntity>? _repository;
    private CL.SQLite.Services.QueryBuilder<VirtualIpEntity>? _queryBuilder;

    public VirtualIpsManager()
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
                _repository = sqlite.CreateRepository<VirtualIpEntity>();
                _queryBuilder = sqlite.CreateQueryBuilder<VirtualIpEntity>();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize Virtual IPs repository: {ex.Message}");
        }
    }

    public async Task<List<VirtualIp>> ListVirtualIpsAsync()
    {
        try
        {
            if (_repository == null || _queryBuilder == null)
            {
                return new List<VirtualIp>();
            }

            var entitiesResult = await _queryBuilder.Select(e => e).ExecuteAsync();
            var entities = entitiesResult?.Data ?? new List<VirtualIpEntity>();
            return entities.Select(EntityToVirtualIp).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error listing virtual IPs: {ex.Message}");
            return new List<VirtualIp>();
        }
    }

    public async Task<VirtualIp?> GetVirtualIpAsync(int id)
    {
        try
        {
            if (_repository == null) return null;

            var result = await _repository.GetByIdAsync(id);
            if (result == null || !result.IsSuccess || result.Data == null) return null;
            var entity = result.Data;
            return entity != null ? EntityToVirtualIp(entity) : null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting virtual IP: {ex.Message}");
            return null;
        }
    }

    public async Task<VirtualIp> CreateVirtualIpAsync(VirtualIp vip)
    {
        try
        {
            if (_repository == null)
            {
                throw new Exception("Repository not initialized");
            }

            var entity = VirtualIpToEntity(vip);
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
                "firewall/virtual-ips",
                $"Created virtual IP: {vip.Name} ({vip.Address}/{vip.SubnetBits})",
                null,
                null,
                new Dictionary<string, object> { { "vipId", entity.Id }, { "vipName", vip.Name }, { "vipAddress", vip.Address }, { "vipType", vip.Type } }
            );
            
            return EntityToVirtualIp(entity);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating virtual IP: {ex.Message}");
            throw;
        }
    }

    public async Task<VirtualIp> UpdateVirtualIpAsync(int id, VirtualIp vip)
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
                throw new Exception($"Virtual IP with ID {id} not found");
            }
            var existing = getResult.Data;

            var updated = VirtualIpToEntity(vip);
            updated.Id = id;
            updated.CreatedAt = existing.CreatedAt;
            updated.UpdatedAt = DateTime.UtcNow;

            await _repository.UpdateAsync(updated);
            
            // Log the change
            await LoggingManager.Instance.LogMonolithAsync(
                "Changes",
                "Info",
                "firewall/virtual-ips",
                $"Updated virtual IP: {vip.Name} ({vip.Address}/{vip.SubnetBits}) (ID: {id})",
                null,
                null,
                new Dictionary<string, object> { { "vipId", id }, { "vipName", vip.Name }, { "vipAddress", vip.Address }, { "vipType", vip.Type } }
            );
            
            return EntityToVirtualIp(updated);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating virtual IP: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> DeleteVirtualIpAsync(int id)
    {
        try
        {
            if (_repository == null)
            {
                throw new Exception("Repository not initialized");
            }

            // Get VIP info before deletion for logging
            var vipToDelete = await GetVirtualIpAsync(id);
            var vipName = vipToDelete?.Name ?? "Unknown";

            var deleteResult = await _repository.DeleteAsync(id);
            var success = (deleteResult != null && deleteResult.IsSuccess);
            
            // Log the change
            if (success)
            {
                await LoggingManager.Instance.LogMonolithAsync(
                    "Changes",
                    "Info",
                    "firewall/virtual-ips",
                    $"Deleted virtual IP: {vipName} (ID: {id})",
                    null,
                    null,
                    new Dictionary<string, object> { { "vipId", id }, { "vipName", vipName } }
                );
            }
            
            return success;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting virtual IP: {ex.Message}");
            throw;
        }
    }

    private VirtualIp EntityToVirtualIp(VirtualIpEntity entity)
    {
        return new VirtualIp
        {
            Id = entity.Id,
            Name = entity.Name,
            Type = entity.Type,
            Interface = entity.Interface,
            Address = entity.Address,
            SubnetBits = entity.SubnetBits,
            Description = entity.Description,
            Enabled = entity.Enabled == 1,
            Vhid = entity.Vhid,
            CarpPassword = entity.CarpPassword,
            Advskew = entity.Advskew,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    private VirtualIpEntity VirtualIpToEntity(VirtualIp vip)
    {
        return new VirtualIpEntity
        {
            Id = vip.Id,
            Name = vip.Name,
            Type = vip.Type,
            Interface = vip.Interface,
            Address = vip.Address,
            SubnetBits = vip.SubnetBits,
            Description = vip.Description,
            Enabled = vip.Enabled ? 1 : 0,
            Vhid = vip.Vhid,
            CarpPassword = vip.CarpPassword,
            Advskew = vip.Advskew
        };
    }
}
