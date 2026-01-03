using CodeLogic;
using CL.SQLite.Services;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Services;

public sealed class SystemSettingsStore
{
    private CL.SQLite.SQLiteLibrary? _sqlite;
    private Repository<SystemSettingsEntity>? _repository;

    public SystemSettingsStore()
    {
        Initialize();
    }

    public async Task<SystemSettingsEntity?> GetAsync()
    {
        if (_repository == null)
        {
            return null;
        }

        var result = await _repository.GetAllAsync(1);
        return result.IsSuccess ? result.Data?.FirstOrDefault() : null;
    }

    public async Task<bool> UpsertAsync(SystemSettingsEntity settings)
    {
        if (_repository == null)
        {
            return false;
        }

        var existing = await GetAsync();
        if (existing != null)
        {
            settings.Id = existing.Id;
            var update = await _repository.UpdateAsync(settings);
            return update.IsSuccess;
        }

        var insert = await _repository.InsertAsync(settings);
        return insert.IsSuccess;
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
            _repository = sqlite.CreateRepository<SystemSettingsEntity>();
        }
        catch
        {
            _sqlite = null;
            _repository = null;
        }
    }
}
