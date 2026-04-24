using CodeLogic;
using CL.SQLite.Services;
using CL.SQLite.Models;
using Monolith.FireWall.Common.Services;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Services.Firewall;

/// <summary>
/// Core manager for traffic shaping rules - provides access from Core service
/// </summary>
public sealed class TrafficShaperManager
{
    private readonly LoggingManager _loggingManager;
    private CL.SQLite.SQLiteLibrary? _sqlite;
    private Repository<TrafficShaperRuleEntity>? _repository;

    public TrafficShaperManager()
    {
        _loggingManager = LoggingManager.Instance;
        Initialize();
    }

    public async Task<List<TrafficShaperRuleView>> ListRulesAsync()
    {
        if (_repository == null)
        {
            return new List<TrafficShaperRuleView>();
        }

        var result = await _repository.GetAllAsync();
        var entities = result.IsSuccess && result.Data != null
            ? result.Data.ToList()
            : new List<TrafficShaperRuleEntity>();

        return entities
            .Select(EntityToView)
            .ToList();
    }

    public async Task<TrafficShaperRuleView?> GetRuleAsync(int id)
    {
        var entity = await GetEntityAsync(id);
        return entity == null ? null : EntityToView(entity);
    }

    public async Task<(bool Success, string? Error, TrafficShaperRuleView? Rule)> CreateRuleAsync(FirewallTrafficShaperRequest request)
    {
        if (_repository == null)
        {
            return (false, "Traffic shaper storage not available", null);
        }

        var validation = ValidateRequest(request);
        if (!validation.Success)
        {
            return (false, validation.Error, null);
        }

        var now = DateTime.UtcNow;
        var entity = new TrafficShaperRuleEntity
        {
            Name = request.Name?.Trim() ?? string.Empty,
            Interface = request.Interface!.Trim(),
            BandwidthUp = request.BandwidthUp,
            BandwidthDown = request.BandwidthDown,
            Scheduler = NormalizeScheduler(request.Scheduler),
            Description = request.Description?.Trim() ?? string.Empty,
            Enabled = request.Enabled ? 1 : 0,
            CreatedAt = now,
            UpdatedAt = now
        };

        var insert = await _repository.InsertAsync(entity);
        if (!insert.IsSuccess || insert.Data <= 0)
        {
            return (false, "Failed to create traffic shaper rule", null);
        }

        entity.Id = (int)insert.Data;

        await _loggingManager.LogSecurityAsync(
            "Firewall",
            "Info",
            "TrafficShaper",
            $"Created traffic shaper rule '{entity.Name}'",
            details: new Dictionary<string, object>
            {
                ["ruleId"] = entity.Id,
                ["interface"] = entity.Interface,
                ["bandwidthUp"] = entity.BandwidthUp,
                ["bandwidthDown"] = entity.BandwidthDown
            });

        return (true, null, EntityToView(entity));
    }

    public async Task<(bool Success, string? Error, TrafficShaperRuleView? Rule)> UpdateRuleAsync(int id, FirewallTrafficShaperRequest request)
    {
        if (_repository == null)
        {
            return (false, "Traffic shaper storage not available", null);
        }

        var entity = await GetEntityAsync(id);
        if (entity == null)
        {
            return (false, "Traffic shaper rule not found", null);
        }

        var validation = ValidateRequest(request);
        if (!validation.Success)
        {
            return (false, validation.Error, null);
        }

        entity.Name = request.Name?.Trim() ?? string.Empty;
        entity.Interface = request.Interface!.Trim();
        entity.BandwidthUp = request.BandwidthUp;
        entity.BandwidthDown = request.BandwidthDown;
        entity.Scheduler = NormalizeScheduler(request.Scheduler);
        entity.Description = request.Description?.Trim() ?? string.Empty;
        entity.Enabled = request.Enabled ? 1 : 0;
        entity.UpdatedAt = DateTime.UtcNow;

        var update = await _repository.UpdateAsync(entity);
        if (!update.IsSuccess)
        {
            return (false, "Failed to update traffic shaper rule", null);
        }

        await _loggingManager.LogSecurityAsync(
            "Firewall",
            "Info",
            "TrafficShaper",
            $"Updated traffic shaper rule '{entity.Name}'",
            details: new Dictionary<string, object>
            {
                ["ruleId"] = entity.Id,
                ["interface"] = entity.Interface,
                ["bandwidthUp"] = entity.BandwidthUp,
                ["bandwidthDown"] = entity.BandwidthDown
            });

        return (true, null, EntityToView(entity));
    }

    public async Task<bool> DeleteRuleAsync(int id)
    {
        if (_repository == null)
        {
            return false;
        }

        var entity = await GetEntityAsync(id);
        if (entity == null)
        {
            return true;
        }

        var delete = await _repository.DeleteAsync(id);
        if (!delete.IsSuccess)
        {
            return false;
        }

        await _loggingManager.LogSecurityAsync(
            "Firewall",
            "Info",
            "TrafficShaper",
            $"Deleted traffic shaper rule '{entity.Name}'",
            details: new Dictionary<string, object>
            {
                ["ruleId"] = entity.Id
            });

        return true;
    }

    private static TrafficShaperRuleView EntityToView(TrafficShaperRuleEntity entity)
    {
        return new TrafficShaperRuleView
        {
            Id = entity.Id,
            Name = entity.Name,
            Interface = entity.Interface,
            BandwidthUp = entity.BandwidthUp,
            BandwidthDown = entity.BandwidthDown,
            Scheduler = entity.Scheduler,
            Description = entity.Description,
            Enabled = entity.Enabled == 1
        };
    }

    private (bool Success, string? Error) ValidateRequest(FirewallTrafficShaperRequest? request)
    {
        if (request == null)
        {
            return (false, "Request is required");
        }

        if (string.IsNullOrWhiteSpace(request.Interface))
        {
            return (false, "Interface is required");
        }

        if (request.BandwidthUp < 0)
        {
            return (false, "Bandwidth up must be a positive value");
        }

        if (request.BandwidthDown < 0)
        {
            return (false, "Bandwidth down must be a positive value");
        }

        return (true, null);
    }

    private static string NormalizeScheduler(string? scheduler)
    {
        if (string.IsNullOrWhiteSpace(scheduler))
        {
            return "fq_codel";
        }

        var normalized = scheduler.Trim().ToLowerInvariant();
        return normalized switch
        {
            "fq_codel" => "fq_codel",
            "fifo" => "fifo",
            "fairq" => "fairq",
            "hfsc" => "hfsc",
            "cbq" => "cbq",
            "priq" => "priq",
            _ => "fq_codel"
        };
    }

    private async Task<TrafficShaperRuleEntity?> GetEntityAsync(int id)
    {
        if (_repository == null)
        {
            return null;
        }

        var result = await _repository.GetByIdAsync(id);
        return result.IsSuccess ? result.Data : null;
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
            _repository = sqlite.CreateRepository<TrafficShaperRuleEntity>();
        }
        catch
        {
            _sqlite = null;
            _repository = null;
        }
    }
}

/// <summary>
/// Traffic shaper rule entity (matches WebUI entity)
/// </summary>
[SQLiteTable("firewall_traffic_shaper")]
public sealed class TrafficShaperRuleEntity
{
    [SQLiteColumn(IsPrimaryKey = true, IsAutoIncrement = true)]
    public int Id { get; set; }

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 128)]
    public string Name { get; set; } = string.Empty;

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 64)]
    public string Interface { get; set; } = string.Empty;

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public int BandwidthUp { get; set; } = 1000; // Kbps

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public int BandwidthDown { get; set; } = 1000; // Kbps

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 32)]
    public string Scheduler { get; set; } = "fq_codel";

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 256)]
    public string Description { get; set; } = string.Empty;

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER, DefaultValue = "1")]
    public int Enabled { get; set; } = 1;

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime CreatedAt { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime UpdatedAt { get; set; }
}
