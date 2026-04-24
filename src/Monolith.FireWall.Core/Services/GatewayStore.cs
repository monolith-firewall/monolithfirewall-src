using CodeLogic;
using CL.SQLite.Services;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Services;

public sealed class GatewayStore
{
    private CL.SQLite.SQLiteLibrary? _sqlite;
    private Repository<GatewayEntity>? _repository;

    public GatewayStore()
    {
        Initialize();
    }

    public async Task<List<GatewayEntity>> GetGatewaysAsync()
    {
        if (_repository == null)
        {
            return new List<GatewayEntity>();
        }

        var result = await _repository.GetAllAsync();
        return result.IsSuccess && result.Value != null
            ? result.Value.ToList()
            : new List<GatewayEntity>();
    }

    public async Task<GatewayEntity?> GetGatewayAsync(int id)
    {
        if (_sqlite == null)
        {
            return null;
        }

        var query = _sqlite.GetQueryBuilder<GatewayEntity>();
        var result = await query.Where(g => g.Id == id).FirstOrDefaultAsync();
        return result.IsSuccess ? result.Value : null;
    }

    public async Task<GatewayEntity?> GetByAddressAsync(string address)
    {
        if (_sqlite == null)
        {
            return null;
        }

        var query = _sqlite.GetQueryBuilder<GatewayEntity>();
        var result = await query.Where(g => g.Address == address).FirstOrDefaultAsync();
        return result.IsSuccess ? result.Value : null;
    }

    public async Task<bool> InsertAsync(GatewayEntity gateway)
    {
        if (_repository == null)
        {
            return false;
        }

        var result = await _repository.InsertAsync(gateway);
        if (!result.IsSuccess)
        {
            return false;
        }

        gateway.Id = Convert.ToInt32(result.Value);
        return true;
    }

    public async Task<bool> UpdateAsync(GatewayEntity gateway)
    {
        if (_repository == null)
        {
            return false;
        }

        var result = await _repository.UpdateAsync(gateway);
        return result.IsSuccess;
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

    private void Initialize()
    {
        try
        {
            var sqlite = CodeLogic.Libraries.Get<CL.SQLite.SQLiteLibrary>();
            if (sqlite == null)
            {
                return;
            }

            _sqlite = sqlite;
            _repository = sqlite.GetRepository<GatewayEntity>();
        }
        catch
        {
            _sqlite = null;
            _repository = null;
        }
    }
}
