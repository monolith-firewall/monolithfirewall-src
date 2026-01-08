using CodeLogic;
using CL.SQLite.Services;

namespace Monolith.FireWall.Core.Services.Firewall;

/// <summary>
/// Core manager for virtual IPs - provides access from Core service
/// </summary>
public sealed class FirewallVirtualIpManager
{
    private CL.SQLite.SQLiteLibrary? _sqlite;
    private Repository<VirtualIpEntity>? _repository;

    public FirewallVirtualIpManager()
    {
        Initialize();
    }

    public async Task<List<VirtualIpView>> ListVirtualIpsAsync()
    {
        if (_repository == null)
        {
            return new List<VirtualIpView>();
        }

        var result = await _repository.GetAllAsync();
        var entities = result.IsSuccess && result.Data != null
            ? result.Data.ToList()
            : new List<VirtualIpEntity>();

        return entities
            .Select(EntityToView)
            .ToList();
    }

    private static VirtualIpView EntityToView(VirtualIpEntity entity)
    {
        return new VirtualIpView
        {
            Id = entity.Id,
            Interface = entity.Interface,
            Address = entity.Address,
            Subnet = entity.SubnetBits.ToString(),
            Mode = entity.Type.ToLowerInvariant(),
            VhId = entity.Vhid,
            CarpPassword = entity.CarpPassword,
            AdvSkew = ParseAdvSkew(entity.Advskew),
            Description = entity.Description,
            Enabled = entity.Enabled == 1
        };
    }

    private static int? ParseAdvSkew(string? advskew)
    {
        if (string.IsNullOrWhiteSpace(advskew))
        {
            return null;
        }

        return int.TryParse(advskew, out var value) ? value : null;
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
            _repository = sqlite.CreateRepository<VirtualIpEntity>();
        }
        catch
        {
            _sqlite = null;
            _repository = null;
        }
    }
}

/// <summary>
/// Virtual IP entity (matches WebUI entity)
/// </summary>
public sealed class VirtualIpEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "ipalias";
    public string Interface { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int SubnetBits { get; set; } = 24;
    public string Description { get; set; } = string.Empty;
    public int Enabled { get; set; } = 1;
    public int? Vhid { get; set; }
    public string? CarpPassword { get; set; }
    public string? Advskew { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
