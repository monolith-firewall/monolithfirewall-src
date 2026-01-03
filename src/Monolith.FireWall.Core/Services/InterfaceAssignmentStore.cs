using CodeLogic;
using CL.SQLite.Services;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Services;

public sealed class InterfaceAssignmentStore
{
    private CL.SQLite.SQLiteLibrary? _sqlite;
    private Repository<InterfaceAssignmentEntity>? _repository;

    public InterfaceAssignmentStore()
    {
        Initialize();
    }

    public async Task<List<InterfaceAssignmentEntity>> GetAssignmentsAsync()
    {
        if (_repository == null)
        {
            return new List<InterfaceAssignmentEntity>();
        }

        var result = await _repository.GetAllAsync();
        return result.IsSuccess && result.Data != null
            ? result.Data.ToList()
            : new List<InterfaceAssignmentEntity>();
    }

    public async Task<InterfaceAssignmentEntity?> GetAssignmentAsync(string iface)
    {
        if (_sqlite == null)
        {
            return null;
        }

        var query = _sqlite.CreateQueryBuilder<InterfaceAssignmentEntity>();
        var result = await query
            .Where(a => a.InterfaceName == iface)
            .FirstOrDefaultAsync();

        return result.IsSuccess ? result.Data : null;
    }

    public async Task<bool> UpsertAsync(InterfaceAssignmentEntity assignment)
    {
        if (_repository == null)
        {
            return false;
        }

        var existing = await GetAssignmentAsync(assignment.InterfaceName);
        if (existing != null)
        {
            assignment.Id = existing.Id;
            var update = await _repository.UpdateAsync(assignment);
            return update.IsSuccess;
        }

        var insert = await _repository.InsertAsync(assignment);
        return insert.IsSuccess;
    }

    public async Task<bool> DeleteAsync(string iface)
    {
        if (_repository == null)
        {
            return false;
        }

        var existing = await GetAssignmentAsync(iface);
        if (existing == null)
        {
            return true;
        }

        var delete = await _repository.DeleteAsync(existing.Id);
        return delete.IsSuccess;
    }

    public async Task<bool> UpdateAppliedAsync(string iface, DateTime appliedAt)
    {
        var existing = await GetAssignmentAsync(iface);
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
            var sqlite = CodeLogic.Libs.Get<CL.SQLite.SQLiteLibrary>();
            if (sqlite == null)
            {
                return;
            }

            _sqlite = sqlite;
            _repository = sqlite.CreateRepository<InterfaceAssignmentEntity>();
        }
        catch
        {
            _sqlite = null;
            _repository = null;
        }
    }
}
