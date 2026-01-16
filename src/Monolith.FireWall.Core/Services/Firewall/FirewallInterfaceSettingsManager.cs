using CodeLogic;
using CL.SQLite.Services;
using Monolith.FireWall.Common.Services;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Services.Firewall;

public sealed class FirewallInterfaceSettingsManager
{
    private readonly LoggingManager _loggingManager;
    private Repository<FirewallInterfaceSettingsEntity>? _repository;

    public FirewallInterfaceSettingsManager()
    {
        _loggingManager = LoggingManager.Instance;
        Initialize();
    }

    public async Task<List<FirewallInterfaceSettingsEntity>> GetAllAsync()
    {
        if (_repository == null) return new List<FirewallInterfaceSettingsEntity>();
        var result = await _repository.GetAllAsync();
        return result.IsSuccess && result.Data != null ? result.Data.ToList() : new List<FirewallInterfaceSettingsEntity>();
    }

    public async Task<FirewallInterfaceSettingsEntity?> GetByInterfaceAsync(string interfaceName)
    {
        if (_repository == null) return null;
        var result = await _repository.GetAllAsync();
        return result.Data?.FirstOrDefault(s => string.Equals(s.InterfaceName, interfaceName, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> UpdateSettingsAsync(FirewallInterfaceSettingsEntity settings)
    {
        if (_repository == null) return false;
        
        settings.UpdatedAt = DateTime.UtcNow;
        var existing = await GetByInterfaceAsync(settings.InterfaceName);
        
        if (existing == null)
        {
            var result = await _repository.InsertAsync(settings);
            return result.IsSuccess;
        }
        else
        {
            settings.Id = existing.Id;
            var result = await _repository.UpdateAsync(settings);
            return result.IsSuccess;
        }
    }

    private void Initialize()
    {
        try
        {
            var sqlite = CodeLogic.Libs.Get<CL.SQLite.SQLiteLibrary>();
            if (sqlite != null)
            {
                _repository = sqlite.CreateRepository<FirewallInterfaceSettingsEntity>();
            }
        }
        catch
        {
            _repository = null;
        }
    }
}
