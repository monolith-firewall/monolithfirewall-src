using CodeLogic;
using CL.SQLite.Services;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Services;

public sealed class NetworkStateChangeStore
{
    private CL.SQLite.SQLiteLibrary? _sqlite;
    private Repository<NetworkStateChangeEntity>? _repository;

    public NetworkStateChangeStore()
    {
        Initialize();
    }

    public async Task<List<NetworkStateChangeEntity>> GetAllAsync(int limit = 100)
    {
        if (_repository == null)
        {
            return new List<NetworkStateChangeEntity>();
        }

        var result = await _repository.GetAllAsync();
        return result.IsSuccess && result.Data != null
            ? result.Data.OrderByDescending(c => c.OccurredAt).Take(limit).ToList()
            : new List<NetworkStateChangeEntity>();
    }

    public async Task<NetworkStateChangeEntity?> GetAsync(long id)
    {
        if (_sqlite == null)
        {
            return null;
        }

        var query = _sqlite.CreateQueryBuilder<NetworkStateChangeEntity>();
        var result = await query.Where(c => c.Id == id).FirstOrDefaultAsync();
        return result.IsSuccess ? result.Data : null;
    }

    public async Task<List<NetworkStateChangeEntity>> GetByTypeAsync(NetworkChangeType changeType, int limit = 50)
    {
        if (_sqlite == null)
        {
            return new List<NetworkStateChangeEntity>();
        }

        var query = _sqlite.CreateQueryBuilder<NetworkStateChangeEntity>();
        var result = await query.Where(c => c.ChangeType == changeType).ExecuteAsync();
        return result.IsSuccess && result.Data != null
            ? result.Data.OrderByDescending(c => c.OccurredAt).Take(limit).ToList()
            : new List<NetworkStateChangeEntity>();
    }

    public async Task<List<NetworkStateChangeEntity>> GetByInterfaceAsync(string interfaceName, int limit = 50)
    {
        if (_sqlite == null)
        {
            return new List<NetworkStateChangeEntity>();
        }

        var query = _sqlite.CreateQueryBuilder<NetworkStateChangeEntity>();
        var result = await query.Where(c => c.InterfaceName == interfaceName).ExecuteAsync();
        return result.IsSuccess && result.Data != null
            ? result.Data.OrderByDescending(c => c.OccurredAt).Take(limit).ToList()
            : new List<NetworkStateChangeEntity>();
    }

    public async Task<List<NetworkStateChangeEntity>> GetByGatewayAsync(int gatewayId, int limit = 50)
    {
        if (_sqlite == null)
        {
            return new List<NetworkStateChangeEntity>();
        }

        var query = _sqlite.CreateQueryBuilder<NetworkStateChangeEntity>();
        var result = await query.Where(c => c.GatewayId == gatewayId).ExecuteAsync();
        return result.IsSuccess && result.Data != null
            ? result.Data.OrderByDescending(c => c.OccurredAt).Take(limit).ToList()
            : new List<NetworkStateChangeEntity>();
    }

    public async Task<List<NetworkStateChangeEntity>> GetUnresolvedAsync()
    {
        if (_sqlite == null)
        {
            return new List<NetworkStateChangeEntity>();
        }

        var query = _sqlite.CreateQueryBuilder<NetworkStateChangeEntity>();
        var result = await query.Where(c => c.ResolvedAt == null).ExecuteAsync();
        return result.IsSuccess && result.Data != null
            ? result.Data.OrderByDescending(c => c.OccurredAt).ToList()
            : new List<NetworkStateChangeEntity>();
    }

    public async Task<List<NetworkStateChangeEntity>> GetRecentAsync(TimeSpan window)
    {
        if (_sqlite == null)
        {
            return new List<NetworkStateChangeEntity>();
        }

        var cutoff = DateTime.UtcNow - window;
        var query = _sqlite.CreateQueryBuilder<NetworkStateChangeEntity>();
        var result = await query.Where(c => c.OccurredAt >= cutoff).ExecuteAsync();
        return result.IsSuccess && result.Data != null
            ? result.Data.OrderByDescending(c => c.OccurredAt).ToList()
            : new List<NetworkStateChangeEntity>();
    }

    public async Task<bool> InsertAsync(NetworkStateChangeEntity change)
    {
        if (_repository == null)
        {
            return false;
        }

        change.OccurredAt = DateTime.UtcNow;
        var result = await _repository.InsertAsync(change);
        if (!result.IsSuccess)
        {
            return false;
        }

        change.Id = Convert.ToInt64(result.Data);
        return true;
    }

    public async Task<long> LogChangeAsync(
        NetworkChangeType changeType,
        string? interfaceName = null,
        int? gatewayId = null,
        int? gatewayGroupId = null,
        object? previousValue = null,
        object? newValue = null,
        ResolutionAction resolution = ResolutionAction.None,
        string? resolutionDetails = null)
    {
        var change = new NetworkStateChangeEntity
        {
            ChangeType = changeType,
            InterfaceName = interfaceName,
            GatewayId = gatewayId,
            GatewayGroupId = gatewayGroupId,
            PreviousValueJson = previousValue != null
                ? System.Text.Json.JsonSerializer.Serialize(previousValue)
                : null,
            NewValueJson = newValue != null
                ? System.Text.Json.JsonSerializer.Serialize(newValue)
                : null,
            ResolutionAction = resolution,
            ResolutionDetails = resolutionDetails,
            OccurredAt = DateTime.UtcNow,
            ResolvedAt = resolution != ResolutionAction.None && resolution != ResolutionAction.ManualRequired
                ? DateTime.UtcNow
                : null
        };

        await InsertAsync(change);
        return change.Id;
    }

    public async Task<bool> ResolveAsync(
        long changeId,
        ResolutionAction resolution,
        string? details = null)
    {
        var change = await GetAsync(changeId);
        if (change == null)
        {
            return false;
        }

        change.ResolutionAction = resolution;
        change.ResolutionDetails = details;
        change.ResolvedAt = DateTime.UtcNow;

        return await UpdateAsync(change);
    }

    public async Task<bool> UpdateAsync(NetworkStateChangeEntity change)
    {
        if (_repository == null)
        {
            return false;
        }

        var result = await _repository.UpdateAsync(change);
        return result.IsSuccess;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        if (_repository == null)
        {
            return false;
        }

        var result = await _repository.DeleteAsync(id);
        return result.IsSuccess;
    }

    public async Task<int> PruneOldEntriesAsync(TimeSpan maxAge)
    {
        if (_sqlite == null)
        {
            return 0;
        }

        var cutoff = DateTime.UtcNow - maxAge;
        var all = await GetAllAsync(int.MaxValue);
        var toDelete = all.Where(c => c.OccurredAt < cutoff && c.ResolvedAt != null).ToList();

        foreach (var change in toDelete)
        {
            await DeleteAsync(change.Id);
        }

        return toDelete.Count;
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
            _repository = sqlite.CreateRepository<NetworkStateChangeEntity>();
        }
        catch
        {
            _sqlite = null;
            _repository = null;
        }
    }
}
