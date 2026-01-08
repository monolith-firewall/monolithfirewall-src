using CodeLogic;
using CL.SQLite.Services;

namespace Monolith.FireWall.Core.Services.Firewall;

/// <summary>
/// Core manager for traffic shaping rules - provides access from Core service
/// </summary>
public sealed class TrafficShaperManager
{
    private CL.SQLite.SQLiteLibrary? _sqlite;
    private Repository<TrafficShaperRuleEntity>? _repository;

    public TrafficShaperManager()
    {
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
public sealed class TrafficShaperRuleEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Interface { get; set; } = string.Empty;
    public int BandwidthUp { get; set; } = 1000; // Kbps
    public int BandwidthDown { get; set; } = 1000; // Kbps
    public string Scheduler { get; set; } = "fq_codel";
    public string Description { get; set; } = string.Empty;
    public int Enabled { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
