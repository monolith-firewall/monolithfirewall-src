using CodeLogic;
using CL.SQLite.Services;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Services;

public sealed class RoutingStore
{
    private CL.SQLite.SQLiteLibrary? _sqlite;
    private Repository<StaticRouteEntity>? _repository;

    public RoutingStore()
    {
        Initialize();
    }

    public async Task<List<StaticRouteEntity>> GetRoutesAsync()
    {
        if (_repository == null)
        {
            return new List<StaticRouteEntity>();
        }

        var result = await _repository.GetAllAsync();
        return result.IsSuccess && result.Data != null
            ? result.Data.ToList()
            : new List<StaticRouteEntity>();
    }

    public async Task<StaticRouteEntity?> GetRouteAsync(int id)
    {
        if (_sqlite == null)
        {
            return null;
        }

        var query = _sqlite.CreateQueryBuilder<StaticRouteEntity>();
        var result = await query.Where(r => r.Id == id).FirstOrDefaultAsync();
        return result.IsSuccess ? result.Data : null;
    }

    public async Task<bool> InsertAsync(StaticRouteEntity route)
    {
        if (_repository == null)
        {
            return false;
        }

        var result = await _repository.InsertAsync(route);
        if (!result.IsSuccess)
        {
            return false;
        }

        route.Id = Convert.ToInt32(result.Data);

        return true;
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
            var sqlite = CodeLogic.Libs.Get<CL.SQLite.SQLiteLibrary>();
            if (sqlite == null)
            {
                return;
            }

            _sqlite = sqlite;
            _repository = sqlite.CreateRepository<StaticRouteEntity>();
        }
        catch
        {
            _sqlite = null;
            _repository = null;
        }
    }
}
