using CodeLogic;
using CL.SQLite.Services;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Services.Settings;

/// <summary>
/// Store for system-wide configuration settings.
/// </summary>
public sealed class SystemConfigStore
{
    private CL.SQLite.SQLiteLibrary? _sqlite;
    private Repository<SystemConfigEntity>? _repository;

    public SystemConfigStore()
    {
        Initialize();
    }

    /// <summary>
    /// Gets a system configuration by key.
    /// </summary>
    public async Task<SystemConfigEntity?> GetAsync(string key)
    {
        if (_repository == null) return null;

        var result = await _repository.GetAllAsync();
        return result.IsSuccess
            ? result.Value?.FirstOrDefault(e => e.Key == key)
            : null;
    }

    /// <summary>
    /// Gets all system configurations.
    /// </summary>
    public async Task<List<SystemConfigEntity>> GetAllAsync()
    {
        if (_repository == null) return new List<SystemConfigEntity>();

        var result = await _repository.GetAllAsync();
        return result.IsSuccess ? result.Value?.ToList() ?? new List<SystemConfigEntity>() : new List<SystemConfigEntity>();
    }

    /// <summary>
    /// Gets configurations by category.
    /// </summary>
    public async Task<List<SystemConfigEntity>> GetByCategoryAsync(string category)
    {
        if (_repository == null) return new List<SystemConfigEntity>();

        var result = await _repository.GetAllAsync();
        return result.IsSuccess
            ? result.Value?.Where(e => e.Category == category).ToList() ?? new List<SystemConfigEntity>()
            : new List<SystemConfigEntity>();
    }

    /// <summary>
    /// Upserts a system configuration.
    /// </summary>
    public async Task<bool> UpsertAsync(SystemConfigEntity config)
    {
        if (_repository == null) return false;

        var existing = await GetAsync(config.Key);
        if (existing != null)
        {
            config.Id = existing.Id;
            config.UpdatedAt = DateTime.UtcNow;
            var update = await _repository.UpdateAsync(config);
            return update.IsSuccess;
        }

        config.UpdatedAt = DateTime.UtcNow;
        var insert = await _repository.InsertAsync(config);
        return insert.IsSuccess;
    }

    /// <summary>
    /// Deletes a system configuration by key.
    /// </summary>
    public async Task<bool> DeleteAsync(string key)
    {
        if (_repository == null) return false;

        var existing = await GetAsync(key);
        if (existing == null) return true;

        var result = await _repository.DeleteAsync(existing.Id);
        return result.IsSuccess;
    }

    private void Initialize()
    {
        try
        {
            var sqlite = CodeLogic.Libraries.Get<CL.SQLite.SQLiteLibrary>();
            if (sqlite == null) return;

            _sqlite = sqlite;
            _repository = sqlite.GetRepository<SystemConfigEntity>();
        }
        catch
        {
            _sqlite = null;
            _repository = null;
        }
    }
}

/// <summary>
/// Store for module-specific configuration settings.
/// </summary>
public sealed class ModuleConfigStore
{
    private CL.SQLite.SQLiteLibrary? _sqlite;
    private Repository<ModuleConfigEntity>? _repository;

    public ModuleConfigStore()
    {
        Initialize();
    }

    /// <summary>
    /// Gets a module configuration by module ID.
    /// </summary>
    public async Task<ModuleConfigEntity?> GetAsync(string moduleId)
    {
        if (_repository == null) return null;

        var result = await _repository.GetAllAsync();
        return result.IsSuccess
            ? result.Value?.FirstOrDefault(e => e.ModuleId == moduleId)
            : null;
    }

    /// <summary>
    /// Gets a module configuration by package and module ID.
    /// </summary>
    public async Task<ModuleConfigEntity?> GetAsync(string packageId, string moduleId)
    {
        if (_repository == null) return null;

        var result = await _repository.GetAllAsync();
        return result.IsSuccess
            ? result.Value?.FirstOrDefault(e => e.PackageId == packageId && e.ModuleId == moduleId)
            : null;
    }

    /// <summary>
    /// Gets all module configurations.
    /// </summary>
    public async Task<List<ModuleConfigEntity>> GetAllAsync()
    {
        if (_repository == null) return new List<ModuleConfigEntity>();

        var result = await _repository.GetAllAsync();
        return result.IsSuccess ? result.Value?.ToList() ?? new List<ModuleConfigEntity>() : new List<ModuleConfigEntity>();
    }

    /// <summary>
    /// Gets all module configurations for a specific package.
    /// </summary>
    public async Task<List<ModuleConfigEntity>> GetByPackageAsync(string packageId)
    {
        if (_repository == null) return new List<ModuleConfigEntity>();

        var result = await _repository.GetAllAsync();
        return result.IsSuccess
            ? result.Value?.Where(e => e.PackageId == packageId).ToList() ?? new List<ModuleConfigEntity>()
            : new List<ModuleConfigEntity>();
    }

    /// <summary>
    /// Upserts a module configuration.
    /// </summary>
    public async Task<bool> UpsertAsync(ModuleConfigEntity config)
    {
        if (_repository == null) return false;

        var existing = await GetAsync(config.PackageId, config.ModuleId);
        if (existing != null)
        {
            config.Id = existing.Id;
            config.UpdatedAt = DateTime.UtcNow;
            var update = await _repository.UpdateAsync(config);
            return update.IsSuccess;
        }

        config.UpdatedAt = DateTime.UtcNow;
        var insert = await _repository.InsertAsync(config);
        return insert.IsSuccess;
    }

    /// <summary>
    /// Deletes a module configuration.
    /// </summary>
    public async Task<bool> DeleteAsync(string packageId, string moduleId)
    {
        if (_repository == null) return false;

        var existing = await GetAsync(packageId, moduleId);
        if (existing == null) return true;

        var result = await _repository.DeleteAsync(existing.Id);
        return result.IsSuccess;
    }

    private void Initialize()
    {
        try
        {
            var sqlite = CodeLogic.Libraries.Get<CL.SQLite.SQLiteLibrary>();
            if (sqlite == null) return;

            _sqlite = sqlite;
            _repository = sqlite.GetRepository<ModuleConfigEntity>();
        }
        catch
        {
            _sqlite = null;
            _repository = null;
        }
    }
}

/// <summary>
/// Store for pending configuration changes waiting to be applied.
/// </summary>
public sealed class PendingChangesStore
{
    private CL.SQLite.SQLiteLibrary? _sqlite;
    private Repository<PendingChangeEntity>? _repository;

    public PendingChangesStore()
    {
        Initialize();
    }

    /// <summary>
    /// Gets a pending change by ID.
    /// </summary>
    public async Task<PendingChangeEntity?> GetAsync(int id)
    {
        if (_repository == null) return null;

        var result = await _repository.GetByIdAsync(id);
        return result.IsSuccess ? result.Value : null;
    }

    /// <summary>
    /// Gets all pending changes.
    /// </summary>
    public async Task<List<PendingChangeEntity>> GetAllAsync()
    {
        if (_repository == null) return new List<PendingChangeEntity>();

        var result = await _repository.GetAllAsync();
        return result.IsSuccess ? result.Value?.ToList() ?? new List<PendingChangeEntity>() : new List<PendingChangeEntity>();
    }

    /// <summary>
    /// Gets all pending changes with a specific status.
    /// </summary>
    public async Task<List<PendingChangeEntity>> GetByStatusAsync(string status)
    {
        if (_repository == null) return new List<PendingChangeEntity>();

        var result = await _repository.GetAllAsync();
        return result.IsSuccess
            ? result.Value?.Where(e => e.Status == status).ToList() ?? new List<PendingChangeEntity>()
            : new List<PendingChangeEntity>();
    }

    /// <summary>
    /// Gets pending changes for a specific target key.
    /// </summary>
    public async Task<List<PendingChangeEntity>> GetByTargetAsync(string targetKey)
    {
        if (_repository == null) return new List<PendingChangeEntity>();

        var result = await _repository.GetAllAsync();
        return result.IsSuccess
            ? result.Value?.Where(e => e.TargetKey == targetKey && e.Status == "Pending").ToList() ?? new List<PendingChangeEntity>()
            : new List<PendingChangeEntity>();
    }

    /// <summary>
    /// Gets pending changes for a category.
    /// </summary>
    public async Task<List<PendingChangeEntity>> GetByCategoryAsync(string category)
    {
        if (_repository == null) return new List<PendingChangeEntity>();

        var result = await _repository.GetAllAsync();
        return result.IsSuccess
            ? result.Value?.Where(e => e.TargetCategory == category && e.Status == "Pending").ToList() ?? new List<PendingChangeEntity>()
            : new List<PendingChangeEntity>();
    }

    /// <summary>
    /// Gets count of pending changes.
    /// </summary>
    public async Task<int> GetPendingCountAsync()
    {
        var pending = await GetByStatusAsync("Pending");
        return pending.Count;
    }

    /// <summary>
    /// Adds a new pending change.
    /// </summary>
    public async Task<PendingChangeEntity?> AddAsync(PendingChangeEntity change)
    {
        if (_repository == null) return null;

        change.CreatedAt = DateTime.UtcNow;
        change.Status = "Pending";
        var result = await _repository.InsertAsync(change);
        return result.IsSuccess ? change : null;
    }

    /// <summary>
    /// Updates a pending change.
    /// </summary>
    public async Task<bool> UpdateAsync(PendingChangeEntity change)
    {
        if (_repository == null) return false;

        var result = await _repository.UpdateAsync(change);
        return result.IsSuccess;
    }

    /// <summary>
    /// Marks a change as applied.
    /// </summary>
    public async Task<bool> MarkAppliedAsync(int id, string? appliedBy = null)
    {
        if (_repository == null) return false;

        var change = await GetAsync(id);
        if (change == null) return false;

        change.Status = "Applied";
        change.AppliedAt = DateTime.UtcNow;
        change.AppliedBy = appliedBy;
        return await UpdateAsync(change);
    }

    /// <summary>
    /// Marks a change as failed.
    /// </summary>
    public async Task<bool> MarkFailedAsync(int id, string? error = null)
    {
        if (_repository == null) return false;

        var change = await GetAsync(id);
        if (change == null) return false;

        change.Status = "Failed";
        change.AppliedAt = DateTime.UtcNow;
        change.ErrorMessage = error;
        return await UpdateAsync(change);
    }

    /// <summary>
    /// Deletes a pending change.
    /// </summary>
    public async Task<bool> DeleteAsync(int id)
    {
        if (_repository == null) return false;

        var result = await _repository.DeleteAsync(id);
        return result.IsSuccess;
    }

    /// <summary>
    /// Deletes all pending changes for a target.
    /// </summary>
    public async Task<int> DeleteByTargetAsync(string targetKey)
    {
        if (_repository == null) return 0;

        var changes = await GetByTargetAsync(targetKey);
        int deleted = 0;
        foreach (var change in changes)
        {
            if (await DeleteAsync(change.Id))
                deleted++;
        }
        return deleted;
    }

    /// <summary>
    /// Clears all pending changes (discards them).
    /// </summary>
    public async Task<int> ClearAllPendingAsync()
    {
        var pending = await GetByStatusAsync("Pending");
        int deleted = 0;
        foreach (var change in pending)
        {
            if (await DeleteAsync(change.Id))
                deleted++;
        }
        return deleted;
    }

    private void Initialize()
    {
        try
        {
            var sqlite = CodeLogic.Libraries.Get<CL.SQLite.SQLiteLibrary>();
            if (sqlite == null) return;

            _sqlite = sqlite;
            _repository = sqlite.GetRepository<PendingChangeEntity>();
        }
        catch
        {
            _sqlite = null;
            _repository = null;
        }
    }
}

/// <summary>
/// Store for configuration change history (audit trail).
/// </summary>
public sealed class ConfigHistoryStore
{
    private CL.SQLite.SQLiteLibrary? _sqlite;
    private Repository<ConfigHistoryEntity>? _repository;

    public ConfigHistoryStore()
    {
        Initialize();
    }

    /// <summary>
    /// Gets a history entry by ID.
    /// </summary>
    public async Task<ConfigHistoryEntity?> GetAsync(int id)
    {
        if (_repository == null) return null;

        var result = await _repository.GetByIdAsync(id);
        return result.IsSuccess ? result.Value : null;
    }

    /// <summary>
    /// Gets recent history entries.
    /// </summary>
    public async Task<List<ConfigHistoryEntity>> GetRecentAsync(int limit = 50)
    {
        if (_repository == null) return new List<ConfigHistoryEntity>();

        var result = await _repository.GetAllAsync(limit * 2); // Get more to sort
        var entries = result.IsSuccess ? result.Value?.ToList() ?? new List<ConfigHistoryEntity>() : new List<ConfigHistoryEntity>();

        // Sort by timestamp descending (most recent first) and take limit
        return entries.OrderByDescending(e => e.ChangedAt).Take(limit).ToList();
    }

    /// <summary>
    /// Gets history for a specific config key.
    /// </summary>
    public async Task<List<ConfigHistoryEntity>> GetByConfigKeyAsync(string configType, string configKey, int limit = 50)
    {
        if (_repository == null) return new List<ConfigHistoryEntity>();

        var result = await _repository.GetAllAsync();
        var entries = result.IsSuccess
            ? result.Value?.Where(e => e.ConfigType == configType && e.ConfigKey == configKey).ToList() ?? new List<ConfigHistoryEntity>()
            : new List<ConfigHistoryEntity>();
        return entries.OrderByDescending(e => e.ChangedAt).Take(limit).ToList();
    }

    /// <summary>
    /// Adds a history entry.
    /// </summary>
    public async Task<ConfigHistoryEntity?> AddAsync(ConfigHistoryEntity entry)
    {
        if (_repository == null) return null;

        entry.ChangedAt = DateTime.UtcNow;
        var result = await _repository.InsertAsync(entry);
        return result.IsSuccess ? entry : null;
    }

    /// <summary>
    /// Creates a history entry from a pending change.
    /// </summary>
    public async Task<ConfigHistoryEntity?> RecordFromChangeAsync(PendingChangeEntity change, bool success, string? error = null)
    {
        var entry = new ConfigHistoryEntity
        {
            ConfigType = change.ChangeType,
            ConfigKey = change.TargetKey,
            Action = success ? "applied" : "failed",
            OldValueJson = change.PreviousJson,
            NewValueJson = change.PendingJson,
            ChangedBy = change.CreatedBy,
            ChangedAt = DateTime.UtcNow,
            ChangeSource = "webui"
        };
        return await AddAsync(entry);
    }

    /// <summary>
    /// Prunes old history entries (keeps last N entries).
    /// </summary>
    public async Task<int> PruneOldEntriesAsync(int keepCount = 1000)
    {
        if (_repository == null) return 0;

        var all = await _repository.GetAllAsync(keepCount * 2);
        if (!all.IsSuccess || all.Value == null) return 0;

        var entries = all.Value.OrderByDescending(e => e.ChangedAt).ToList();
        if (entries.Count <= keepCount) return 0;

        var toDelete = entries.Skip(keepCount).ToList();
        int deleted = 0;
        foreach (var entry in toDelete)
        {
            var result = await _repository.DeleteAsync(entry.Id);
            if (result.IsSuccess) deleted++;
        }
        return deleted;
    }

    private void Initialize()
    {
        try
        {
            var sqlite = CodeLogic.Libraries.Get<CL.SQLite.SQLiteLibrary>();
            if (sqlite == null) return;

            _sqlite = sqlite;
            _repository = sqlite.GetRepository<ConfigHistoryEntity>();
        }
        catch
        {
            _sqlite = null;
            _repository = null;
        }
    }
}
