using CodeLogic;
using CL.SQLite.Services;
using Monolith.FireWall.Common.Services;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Services.Firewall;

public sealed class FirewallNatSettingsManager
{
    private readonly LoggingManager _loggingManager;
    private CL.SQLite.SQLiteLibrary? _sqlite;
    private Repository<FirewallNatSettingsEntity>? _repository;

    public FirewallNatSettingsManager()
    {
        _loggingManager = LoggingManager.Instance;
        Initialize();
    }

    public async Task<FirewallNatSettingsView> GetAsync()
    {
        var entity = await GetEntityAsync();
        if (entity == null)
        {
            return new FirewallNatSettingsView
            {
                ReflectionEnabled = false,
                ReflectionMode = "proxy"
            };
        }

        return new FirewallNatSettingsView
        {
            ReflectionEnabled = entity.ReflectionEnabled,
            ReflectionMode = entity.ReflectionMode
        };
    }

    public async Task<(bool Success, string? Error, FirewallNatSettingsView? Settings)> UpdateAsync(FirewallNatSettingsRequest request)
    {
        if (_repository == null)
        {
            return (false, "NAT settings storage not available", null);
        }

        var mode = NormalizeReflectionMode(request.ReflectionMode);
        if (string.IsNullOrWhiteSpace(mode))
        {
            return (false, "Reflection mode is invalid", null);
        }

        var entity = await GetEntityAsync() ?? new FirewallNatSettingsEntity();
        entity.ReflectionEnabled = request.ReflectionEnabled;
        entity.ReflectionMode = mode;
        entity.UpdatedAt = DateTime.UtcNow;

        if (entity.Id == 0)
        {
            var insert = await _repository.InsertAsync(entity);
            if (!insert.IsSuccess || insert.Data <= 0)
            {
                return (false, "Failed to save NAT settings", null);
            }

            entity.Id = (int)insert.Data;
        }
        else
        {
            var update = await _repository.UpdateAsync(entity);
            if (!update.IsSuccess)
            {
                return (false, "Failed to update NAT settings", null);
            }
        }

        await _loggingManager.LogSecurityAsync(
            "Firewall",
            "Info",
            "FirewallNat",
            "Updated NAT reflection settings",
            details: new Dictionary<string, object>
            {
                ["reflectionEnabled"] = entity.ReflectionEnabled,
                ["reflectionMode"] = entity.ReflectionMode
            });

        return (true, null, new FirewallNatSettingsView
        {
            ReflectionEnabled = entity.ReflectionEnabled,
            ReflectionMode = entity.ReflectionMode
        });
    }

    private async Task<FirewallNatSettingsEntity?> GetEntityAsync()
    {
        if (_repository == null)
        {
            return null;
        }

        var result = await _repository.GetAllAsync();
        if (!result.IsSuccess || result.Data == null)
        {
            return null;
        }

        return result.Data.FirstOrDefault();
    }

    private static string NormalizeReflectionMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return "proxy";
        }

        var normalized = mode.Trim().ToLowerInvariant();
        return normalized switch
        {
            "proxy" => "proxy",
            "nat" => "nat",
            "disabled" => "disabled",
            "default" => "proxy",
            _ => "proxy"
        };
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
            _repository = sqlite.CreateRepository<FirewallNatSettingsEntity>();
        }
        catch
        {
            _sqlite = null;
            _repository = null;
        }
    }
}
