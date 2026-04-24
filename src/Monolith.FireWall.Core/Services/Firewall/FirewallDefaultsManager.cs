using CodeLogic;
using CL.SQLite.Services;
using Monolith.FireWall.Common.Services;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Services.Firewall;

public sealed class FirewallDefaultsManager
{
    private readonly LoggingManager _loggingManager;
    private Repository<FirewallDefaultsEntity>? _repository;

    public FirewallDefaultsManager()
    {
        _loggingManager = LoggingManager.Instance;
        Initialize();
    }

    public async Task<FirewallDefaultsView> GetAsync()
    {
        var entity = await GetEntityAsync();
        if (entity == null)
        {
            return new FirewallDefaultsView();
        }

        return new FirewallDefaultsView
        {
            LanDefaultAction = entity.LanDefaultAction,
            WanDefaultAction = entity.WanDefaultAction,
            OptDefaultAction = entity.OptDefaultAction,
            BlockReservedOnWan = entity.BlockReservedOnWan,
            AllowManagementWebUi = entity.AllowManagementWebUi,
            AllowSshAccess = entity.AllowSshAccess
        };
    }

    public async Task<(bool Success, string? Error, FirewallDefaultsView? Defaults)> UpdateAsync(FirewallDefaultsRequest request)
    {
        if (_repository == null)
        {
            return (false, "Defaults storage not available", null);
        }

        var lanAction = NormalizeAction(request.LanDefaultAction);
        var wanAction = NormalizeAction(request.WanDefaultAction);
        var optAction = NormalizeAction(request.OptDefaultAction);

        if (string.IsNullOrWhiteSpace(lanAction) || string.IsNullOrWhiteSpace(wanAction) || string.IsNullOrWhiteSpace(optAction))
        {
            return (false, "Default action is invalid", null);
        }

        var entity = await GetEntityAsync() ?? new FirewallDefaultsEntity();
        entity.LanDefaultAction = lanAction;
        entity.WanDefaultAction = wanAction;
        entity.OptDefaultAction = optAction;
        entity.BlockReservedOnWan = request.BlockReservedOnWan;
        entity.AllowManagementWebUi = request.AllowManagementWebUi;
        entity.AllowSshAccess = request.AllowSshAccess;
        entity.UpdatedAt = DateTime.UtcNow;

        if (entity.Id == 0)
        {
            var insert = await _repository.InsertAsync(entity);
            if (!insert.IsSuccess || insert.Value <= 0)
            {
                return (false, "Failed to save defaults", null);
            }

            entity.Id = (int)insert.Value;
        }
        else
        {
            var update = await _repository.UpdateAsync(entity);
            if (!update.IsSuccess)
            {
                return (false, "Failed to update defaults", null);
            }
        }

        await _loggingManager.LogSecurityAsync(
            "Firewall",
            "Info",
            "FirewallDefaults",
            "Updated firewall default actions",
            details: new Dictionary<string, object>
            {
                ["lanDefaultAction"] = entity.LanDefaultAction,
                ["wanDefaultAction"] = entity.WanDefaultAction,
                ["optDefaultAction"] = entity.OptDefaultAction
            });

        return (true, null, new FirewallDefaultsView
        {
            LanDefaultAction = entity.LanDefaultAction,
            WanDefaultAction = entity.WanDefaultAction,
            OptDefaultAction = entity.OptDefaultAction,
            BlockReservedOnWan = entity.BlockReservedOnWan,
            AllowManagementWebUi = entity.AllowManagementWebUi,
            AllowSshAccess = entity.AllowSshAccess
        });
    }

    private async Task<FirewallDefaultsEntity?> GetEntityAsync()
    {
        if (_repository == null)
        {
            return null;
        }

        var result = await _repository.GetAllAsync();
        if (!result.IsSuccess || result.Value == null)
        {
            return null;
        }

        return result.Value.FirstOrDefault();
    }

    private static string NormalizeAction(string? action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return string.Empty;
        }

        var normalized = action.Trim().ToLowerInvariant();
        return normalized switch
        {
            "pass" => "pass",
            "block" => "block",
            "reject" => "reject",
            _ => string.Empty
        };
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

            _repository = sqlite.GetRepository<FirewallDefaultsEntity>();
        }
        catch
        {
            _repository = null;
        }
    }
}
