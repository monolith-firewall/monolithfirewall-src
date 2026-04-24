using CodeLogic;
using CL.SQLite.Services;
using Monolith.FireWall.Common.Services;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Services;

public sealed class PackageStateStore
{
    private readonly LoggingManager _loggingManager;
    private CL.SQLite.SQLiteLibrary? _sqlite;
    private Repository<PackageInstallationEntity>? _packageRepository;
    private Repository<ModuleStateEntity>? _moduleRepository;

    public PackageStateStore()
    {
        _loggingManager = LoggingManager.Instance;
        InitializeRepositories();
    }

    public async Task<PackageInstallationEntity?> GetPackageAsync(string packageId)
    {
        if (_sqlite == null)
        {
            return null;
        }

        var query = _sqlite.GetQueryBuilder<PackageInstallationEntity>();
        var result = await query
            .Where(p => p.PackageId == packageId)
            .FirstOrDefaultAsync();

        return result.IsSuccess ? result.Value : null;
    }

    public async Task<List<PackageInstallationEntity>> GetPackagesAsync()
    {
        if (_packageRepository == null)
        {
            return new List<PackageInstallationEntity>();
        }

        var result = await _packageRepository.GetAllAsync();
        return result.IsSuccess && result.Value != null
            ? result.Value.ToList()
            : new List<PackageInstallationEntity>();
    }

    public async Task<Dictionary<string, ModuleStateEntity>> GetModuleStatesAsync(string packageId)
    {
        var states = new Dictionary<string, ModuleStateEntity>(StringComparer.OrdinalIgnoreCase);
        if (_sqlite == null)
        {
            return states;
        }

        var query = _sqlite.GetQueryBuilder<ModuleStateEntity>();
        var result = await query
            .Where(m => m.PackageId == packageId)
            .ToListAsync();

        if (!result.IsSuccess || result.Value == null)
        {
            return states;
        }

        foreach (var state in result.Value)
        {
            if (!string.IsNullOrWhiteSpace(state.ModuleId))
            {
                states[state.ModuleId] = state;
            }
        }

        return states;
    }

    public async Task<ModuleStateEntity?> GetModuleStateAsync(string packageId, string moduleId)
    {
        if (_sqlite == null)
        {
            return null;
        }

        var query = _sqlite.GetQueryBuilder<ModuleStateEntity>();
        var result = await query
            .Where(m => m.PackageId == packageId && m.ModuleId == moduleId)
            .FirstOrDefaultAsync();

        return result.IsSuccess ? result.Value : null;
    }

    public async Task<bool> SetPackageInstalledAsync(string packageId, string version, string source, bool log = true)
    {
        if (_packageRepository == null)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        var existing = await GetPackageAsync(packageId);
        if (existing != null)
        {
            existing.Version = version;
            existing.Source = source;
            existing.UpdatedAt = now;
            var update = await _packageRepository.UpdateAsync(existing);
            if (!update.IsSuccess)
            {
                return false;
            }
        }
        else
        {
            var entity = new PackageInstallationEntity
            {
                PackageId = packageId,
                Version = version,
                Source = source,
                InstalledAt = now,
                UpdatedAt = now
            };
            var insert = await _packageRepository.InsertAsync(entity);
            if (!insert.IsSuccess)
            {
                return false;
            }
        }

        if (log)
        {
            await _loggingManager.LogMonolithAsync(
                "Package",
                "info",
                "PackageStateStore",
                $"Package '{packageId}' installed",
                null,
                null,
                new Dictionary<string, object>
                {
                    ["packageId"] = packageId,
                    ["version"] = version,
                    ["source"] = source
                });
        }

        return true;
    }

    public async Task<bool> RemovePackageAsync(string packageId, bool log = true)
    {
        if (_packageRepository == null)
        {
            return false;
        }

        var existing = await GetPackageAsync(packageId);
        if (existing == null)
        {
            return true;
        }

        var delete = await _packageRepository.DeleteAsync(existing.Id);
        if (!delete.IsSuccess)
        {
            return false;
        }

        if (log)
        {
            await _loggingManager.LogMonolithAsync(
                "Package",
                "warning",
                "PackageStateStore",
                $"Package '{packageId}' removed",
                null,
                null,
                new Dictionary<string, object>
                {
                    ["packageId"] = packageId
                });
        }

        return true;
    }

    public async Task ClearModuleStatesAsync(string packageId)
    {
        if (_moduleRepository == null)
        {
            return;
        }

        await _sqlite!.GetQueryBuilder<ModuleStateEntity>()
            .Where(m => m.PackageId == packageId)
            .DeleteAsync();
    }

    public async Task<bool> SetModuleEnabledAsync(string packageId, string moduleId, bool enabled)
    {
        if (_moduleRepository == null)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        var existing = await GetModuleStateAsync(packageId, moduleId);
        if (existing != null)
        {
            existing.Enabled = enabled;
            existing.UpdatedAt = now;
            var update = await _moduleRepository.UpdateAsync(existing);
            if (!update.IsSuccess)
            {
                return false;
            }
        }
        else
        {
            var entity = new ModuleStateEntity
            {
                PackageId = packageId,
                ModuleId = moduleId,
                Enabled = enabled,
                UpdatedAt = now
            };
            var insert = await _moduleRepository.InsertAsync(entity);
            if (!insert.IsSuccess)
            {
                return false;
            }
        }

        await _loggingManager.LogMonolithAsync(
            "Module",
            "info",
            "PackageStateStore",
            $"Module '{packageId}/{moduleId}' {(enabled ? "enabled" : "disabled")}",
            null,
            null,
            new Dictionary<string, object>
            {
                ["packageId"] = packageId,
                ["moduleId"] = moduleId,
                ["enabled"] = enabled
            });

        return true;
    }

    public async Task<bool> IsModuleEnabledAsync(string packageId, string moduleId)
    {
        var state = await GetModuleStateAsync(packageId, moduleId);
        return state?.Enabled ?? true;
    }

    private void InitializeRepositories()
    {
        try
        {
            var sqlite = CodeLogic.Libraries.Get<CL.SQLite.SQLiteLibrary>();
            if (sqlite == null)
            {
                return;
            }

            _sqlite = sqlite;
            _packageRepository = sqlite.GetRepository<PackageInstallationEntity>();
            _moduleRepository = sqlite.GetRepository<ModuleStateEntity>();
        }
        catch
        {
            _sqlite = null;
            _packageRepository = null;
            _moduleRepository = null;
        }
    }
}
