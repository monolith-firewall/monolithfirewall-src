using CodeLogic;
using CL.SQLite.Services;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Services;

/// <summary>
/// Store for setup state - manages fresh install detection
/// </summary>
public class SetupStateStore
{
    private CL.SQLite.SQLiteLibrary? _sqlite;
    private Repository<SetupStateEntity>? _repository;

    public SetupStateStore()
    {
        InitializeRepository();
    }

    private void InitializeRepository()
    {
        try
        {
            var sqlite = CodeLogic.Libs.Get<CL.SQLite.SQLiteLibrary>();
            if (sqlite == null)
            {
                return;
            }

            _sqlite = sqlite;
            _repository = sqlite.CreateRepository<SetupStateEntity>();
        }
        catch
        {
            _sqlite = null;
            _repository = null;
        }
    }

    /// <summary>
    /// Get current setup state (singleton record with ID 1)
    /// </summary>
    public async Task<SetupStateEntity?> GetSetupStateAsync()
    {
        if (_sqlite == null || _repository == null)
            return null;

        try
        {
            var query = _sqlite.CreateQueryBuilder<SetupStateEntity>();
            var result = await query
                .Where(s => s.Id == 1)
                .FirstOrDefaultAsync();

            return result.IsSuccess ? result.Data : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Initialize fresh install marker on first startup
    /// </summary>
    public async Task InitializeFreshInstallAsync()
    {
        if (_repository == null)
            return;

        try
        {
            // Check if already exists
            var existing = await GetSetupStateAsync();
            if (existing != null)
                return; // Already initialized

            var state = new SetupStateEntity
            {
                Id = 1,
                IsFreshInstall = true,
                SetupCompleted = false,
                FirstRunAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            
            try
            {
                var result = await _repository.InsertAsync(state);
                if (!result.IsSuccess)
                {
                    // Might already exist, ignore
                }
            }
            catch
            {
                // Might already exist, ignore
            }
        }
        catch
        {
            // Ignore - might already exist
        }
    }

    /// <summary>
    /// Mark setup as completed
    /// </summary>
    public async Task MarkSetupCompletedAsync()
    {
        if (_repository == null)
            return;

        try
        {
            var state = await GetSetupStateAsync();
            if (state != null)
            {
                state.IsFreshInstall = false;
                state.SetupCompleted = true;
                state.SetupCompletedAt = DateTime.UtcNow;
                state.UpdatedAt = DateTime.UtcNow;
                var result = await _repository.UpdateAsync(state);
                if (!result.IsSuccess)
                {
                    // Ignore update errors
                }
            }
            else
            {
                // Create if doesn't exist
                state = new SetupStateEntity
                {
                    Id = 1,
                    IsFreshInstall = false,
                    SetupCompleted = true,
                    SetupCompletedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                var result = await _repository.InsertAsync(state);
                if (!result.IsSuccess)
                {
                    // Ignore insert errors
                }
            }
        }
        catch
        {
            // Ignore errors
        }
    }

    /// <summary>
    /// Mark setup as skipped
    /// </summary>
    public async Task MarkSetupSkippedAsync()
    {
        if (_repository == null)
            return;

        try
        {
            var state = await GetSetupStateAsync();
            if (state != null)
            {
                state.IsFreshInstall = false;
                state.SetupCompleted = true; // Skipped counts as "completed"
                state.SetupCompletedAt = DateTime.UtcNow;
                state.UpdatedAt = DateTime.UtcNow;
                var result = await _repository.UpdateAsync(state);
                if (!result.IsSuccess)
                {
                    // Ignore update errors
                }
            }
            else
            {
                // Create if doesn't exist
                state = new SetupStateEntity
                {
                    Id = 1,
                    IsFreshInstall = false,
                    SetupCompleted = true,
                    SetupCompletedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                var result = await _repository.InsertAsync(state);
                if (!result.IsSuccess)
                {
                    // Ignore insert errors
                }
            }
        }
        catch
        {
            // Ignore errors
        }
    }
}
