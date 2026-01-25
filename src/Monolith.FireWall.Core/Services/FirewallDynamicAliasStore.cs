using CodeLogic;
using CL.SQLite.Services;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Services;

public sealed class FirewallDynamicAliasStore
{
    private CL.SQLite.SQLiteLibrary? _sqlite;
    private Repository<FirewallDynamicAliasEntity>? _repository;

    public FirewallDynamicAliasStore()
    {
        Initialize();
    }

    public async Task<List<FirewallDynamicAliasEntity>> GetAllAsync()
    {
        if (_repository == null)
        {
            return new List<FirewallDynamicAliasEntity>();
        }

        var result = await _repository.GetAllAsync();
        return result.IsSuccess && result.Data != null
            ? result.Data.ToList()
            : new List<FirewallDynamicAliasEntity>();
    }

    public async Task<FirewallDynamicAliasEntity?> GetAsync(int id)
    {
        if (_sqlite == null)
        {
            return null;
        }

        var query = _sqlite.CreateQueryBuilder<FirewallDynamicAliasEntity>();
        var result = await query.Where(a => a.Id == id).FirstOrDefaultAsync();
        return result.IsSuccess ? result.Data : null;
    }

    public async Task<FirewallDynamicAliasEntity?> GetByNameAsync(string name)
    {
        if (_sqlite == null)
        {
            return null;
        }

        var query = _sqlite.CreateQueryBuilder<FirewallDynamicAliasEntity>();
        var result = await query.Where(a => a.Name == name).FirstOrDefaultAsync();
        return result.IsSuccess ? result.Data : null;
    }

    public async Task<List<FirewallDynamicAliasEntity>> GetByInterfaceAsync(string interfaceName)
    {
        if (_sqlite == null)
        {
            return new List<FirewallDynamicAliasEntity>();
        }

        var query = _sqlite.CreateQueryBuilder<FirewallDynamicAliasEntity>();
        var result = await query.Where(a => a.InterfaceName == interfaceName).ExecuteAsync();
        return result.IsSuccess && result.Data != null
            ? result.Data.ToList()
            : new List<FirewallDynamicAliasEntity>();
    }

    public async Task<List<FirewallDynamicAliasEntity>> GetByTypeAsync(DynamicAliasType aliasType)
    {
        if (_sqlite == null)
        {
            return new List<FirewallDynamicAliasEntity>();
        }

        var query = _sqlite.CreateQueryBuilder<FirewallDynamicAliasEntity>();
        var result = await query.Where(a => a.AliasType == aliasType).ExecuteAsync();
        return result.IsSuccess && result.Data != null
            ? result.Data.ToList()
            : new List<FirewallDynamicAliasEntity>();
    }

    public async Task<bool> InsertAsync(FirewallDynamicAliasEntity alias)
    {
        if (_repository == null)
        {
            return false;
        }

        alias.CreatedAt = DateTime.UtcNow;
        alias.UpdatedAt = DateTime.UtcNow;

        var result = await _repository.InsertAsync(alias);
        if (!result.IsSuccess)
        {
            return false;
        }

        alias.Id = Convert.ToInt32(result.Data);
        return true;
    }

    public async Task<bool> UpdateAsync(FirewallDynamicAliasEntity alias)
    {
        if (_repository == null)
        {
            return false;
        }

        alias.UpdatedAt = DateTime.UtcNow;
        var result = await _repository.UpdateAsync(alias);
        return result.IsSuccess;
    }

    public async Task<bool> UpsertAsync(FirewallDynamicAliasEntity alias)
    {
        if (_repository == null)
        {
            return false;
        }

        var existing = await GetByNameAsync(alias.Name);
        if (existing != null)
        {
            alias.Id = existing.Id;
            alias.CreatedAt = existing.CreatedAt;
            return await UpdateAsync(alias);
        }

        return await InsertAsync(alias);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        if (_repository == null)
        {
            return false;
        }

        var result = await _repository.DeleteAsync(id);
        return result.IsSuccess;
    }

    public async Task<bool> DeleteByNameAsync(string name)
    {
        var existing = await GetByNameAsync(name);
        if (existing == null)
        {
            return true;
        }

        return await DeleteAsync(existing.Id);
    }

    public async Task<bool> DeleteByInterfaceAsync(string interfaceName)
    {
        var aliases = await GetByInterfaceAsync(interfaceName);
        foreach (var alias in aliases)
        {
            await DeleteAsync(alias.Id);
        }
        return true;
    }

    public async Task EnsureStandardAliasesAsync(string interfaceName, string role)
    {
        var now = DateTime.UtcNow;
        var prefix = role.ToLowerInvariant();

        // Create interface IP alias
        var ipAlias = new FirewallDynamicAliasEntity
        {
            Name = $"{prefix}_ip",
            AliasType = DynamicAliasType.InterfaceIp,
            InterfaceName = interfaceName,
            AddressFamily = "ipv4",
            Description = $"Current IP address of {role} interface",
            CreatedAt = now,
            UpdatedAt = now
        };
        await UpsertAsync(ipAlias);

        // Create interface subnet alias
        var subnetAlias = new FirewallDynamicAliasEntity
        {
            Name = $"{prefix}_subnet",
            AliasType = DynamicAliasType.InterfaceSubnet,
            InterfaceName = interfaceName,
            AddressFamily = "ipv4",
            Description = $"Subnet of {role} interface",
            CreatedAt = now,
            UpdatedAt = now
        };
        await UpsertAsync(subnetAlias);

        // Create interface network alias
        var networkAlias = new FirewallDynamicAliasEntity
        {
            Name = $"{prefix}_network",
            AliasType = DynamicAliasType.InterfaceNetwork,
            InterfaceName = interfaceName,
            AddressFamily = "ipv4",
            Description = $"Network CIDR of {role} interface",
            CreatedAt = now,
            UpdatedAt = now
        };
        await UpsertAsync(networkAlias);
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
            _repository = sqlite.CreateRepository<FirewallDynamicAliasEntity>();
        }
        catch
        {
            _sqlite = null;
            _repository = null;
        }
    }
}
