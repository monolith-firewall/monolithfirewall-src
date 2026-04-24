using CodeLogic;
using CL.SQLite.Services;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Services;

public sealed class SystemTuneablesStore
{
    private CL.SQLite.SQLiteLibrary? _sqlite;
    private Repository<SystemTuneableEntity>? _repository;

    public SystemTuneablesStore()
    {
        Initialize();
    }

    public async Task<List<SystemTuneableEntity>> GetAllAsync()
    {
        if (_repository == null)
        {
            return new List<SystemTuneableEntity>();
        }

        var result = await _repository.GetAllAsync();
        return result.IsSuccess && result.Value != null
            ? result.Value.ToList()
            : new List<SystemTuneableEntity>();
    }

    public async Task<SystemTuneableEntity?> GetByKeyAsync(string key)
    {
        if (_sqlite == null)
        {
            return null;
        }

        var query = _sqlite.GetQueryBuilder<SystemTuneableEntity>();
        var result = await query.Where(t => t.Key == key).FirstOrDefaultAsync();
        return result.IsSuccess ? result.Value : null;
    }

    public async Task<bool> UpsertAsync(SystemTuneableEntity entity)
    {
        if (_repository == null)
        {
            return false;
        }

        var existing = await GetByKeyAsync(entity.Key);
        if (existing != null)
        {
            entity.Id = existing.Id;
            var update = await _repository.UpdateAsync(entity);
            return update.IsSuccess;
        }

        var insert = await _repository.InsertAsync(entity);
        if (!insert.IsSuccess)
        {
            return false;
        }

        entity.Id = Convert.ToInt32(insert.Value);
        return true;
    }

    public async Task<bool> UpdateAppliedAsync(string key, DateTime appliedAt)
    {
        var existing = await GetByKeyAsync(key);
        if (existing == null)
        {
            return false;
        }

        existing.LastAppliedAt = appliedAt;
        var update = await _repository!.UpdateAsync(existing);
        return update.IsSuccess;
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
            _repository = sqlite.GetRepository<SystemTuneableEntity>();
        }
        catch
        {
            _sqlite = null;
            _repository = null;
        }
    }
}
