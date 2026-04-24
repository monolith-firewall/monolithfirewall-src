using CodeLogic;
using CL.SQLite.Services;
using CL.SQLite.Models;
using Monolith.FireWall.Common.Services;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Services.Firewall;

/// <summary>
/// Core manager for virtual IPs - provides access from Core service
/// </summary>
public sealed class FirewallVirtualIpManager
{
    private readonly LoggingManager _loggingManager;
    private CL.SQLite.SQLiteLibrary? _sqlite;
    private Repository<VirtualIpEntity>? _repository;

    public FirewallVirtualIpManager()
    {
        _loggingManager = LoggingManager.Instance;
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

    public async Task<VirtualIpView?> GetVirtualIpAsync(int id)
    {
        var entity = await GetEntityAsync(id);
        return entity == null ? null : EntityToView(entity);
    }

    public async Task<(bool Success, string? Error, VirtualIpView? VirtualIp)> CreateVirtualIpAsync(FirewallVirtualIpRequest request)
    {
        if (_repository == null)
        {
            return (false, "Virtual IP storage not available", null);
        }

        var validation = ValidateRequest(request);
        if (!validation.Success)
        {
            return (false, validation.Error, null);
        }

        var now = DateTime.UtcNow;
        var entity = new VirtualIpEntity
        {
            Name = request.Name?.Trim() ?? string.Empty,
            Type = NormalizeType(request.Type),
            Interface = request.Interface!.Trim(),
            Address = request.Address!.Trim(),
            SubnetBits = request.SubnetBits,
            Description = request.Description?.Trim() ?? string.Empty,
            Enabled = request.Enabled ? 1 : 0,
            Vhid = request.Vhid,
            CarpPassword = request.CarpPassword?.Trim(),
            Advskew = request.Advskew?.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        var insert = await _repository.InsertAsync(entity);
        if (!insert.IsSuccess || insert.Data <= 0)
        {
            return (false, "Failed to create virtual IP", null);
        }

        entity.Id = (int)insert.Data;

        await _loggingManager.LogSecurityAsync(
            "Firewall",
            "Info",
            "FirewallVirtualIp",
            $"Created virtual IP '{entity.Name}' ({entity.Type})",
            details: new Dictionary<string, object>
            {
                ["virtualIpId"] = entity.Id,
                ["interface"] = entity.Interface,
                ["address"] = entity.Address
            });

        return (true, null, EntityToView(entity));
    }

    public async Task<(bool Success, string? Error, VirtualIpView? VirtualIp)> UpdateVirtualIpAsync(int id, FirewallVirtualIpRequest request)
    {
        if (_repository == null)
        {
            return (false, "Virtual IP storage not available", null);
        }

        var entity = await GetEntityAsync(id);
        if (entity == null)
        {
            return (false, "Virtual IP not found", null);
        }

        var validation = ValidateRequest(request);
        if (!validation.Success)
        {
            return (false, validation.Error, null);
        }

        entity.Name = request.Name?.Trim() ?? string.Empty;
        entity.Type = NormalizeType(request.Type);
        entity.Interface = request.Interface!.Trim();
        entity.Address = request.Address!.Trim();
        entity.SubnetBits = request.SubnetBits;
        entity.Description = request.Description?.Trim() ?? string.Empty;
        entity.Enabled = request.Enabled ? 1 : 0;
        entity.Vhid = request.Vhid;
        entity.CarpPassword = request.CarpPassword?.Trim();
        entity.Advskew = request.Advskew?.Trim();
        entity.UpdatedAt = DateTime.UtcNow;

        var update = await _repository.UpdateAsync(entity);
        if (!update.IsSuccess)
        {
            return (false, "Failed to update virtual IP", null);
        }

        await _loggingManager.LogSecurityAsync(
            "Firewall",
            "Info",
            "FirewallVirtualIp",
            $"Updated virtual IP '{entity.Name}' ({entity.Type})",
            details: new Dictionary<string, object>
            {
                ["virtualIpId"] = entity.Id,
                ["interface"] = entity.Interface,
                ["address"] = entity.Address
            });

        return (true, null, EntityToView(entity));
    }

    public async Task<bool> DeleteVirtualIpAsync(int id)
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
            "FirewallVirtualIp",
            $"Deleted virtual IP '{entity.Name}' ({entity.Type})",
            details: new Dictionary<string, object>
            {
                ["virtualIpId"] = entity.Id
            });

        return true;
    }

    private static VirtualIpView EntityToView(VirtualIpEntity entity)
    {
        return new VirtualIpView
        {
            Id = entity.Id,
            Name = entity.Name,
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

    private (bool Success, string? Error) ValidateRequest(FirewallVirtualIpRequest? request)
    {
        if (request == null)
        {
            return (false, "Request is required");
        }

        if (string.IsNullOrWhiteSpace(request.Interface))
        {
            return (false, "Interface is required");
        }

        if (string.IsNullOrWhiteSpace(request.Address))
        {
            return (false, "Address is required");
        }

        if (request.SubnetBits < 0 || request.SubnetBits > 128)
        {
            return (false, "Subnet bits must be between 0 and 128");
        }

        var type = NormalizeType(request.Type);
        if (type == "carp")
        {
            if (request.Vhid == null || request.Vhid < 1 || request.Vhid > 255)
            {
                return (false, "CARP VHID must be between 1 and 255");
            }

            if (string.IsNullOrWhiteSpace(request.CarpPassword))
            {
                return (false, "CARP password is required for CARP type");
            }
        }

        return (true, null);
    }

    private static string NormalizeType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return "ipalias";
        }

        var normalized = type.Trim().ToLowerInvariant();
        return normalized switch
        {
            "ipalias" => "ipalias",
            "carp" => "carp",
            "proxyarp" => "proxyarp",
            _ => "ipalias"
        };
    }

    private async Task<VirtualIpEntity?> GetEntityAsync(int id)
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
[SQLiteTable("firewall_virtual_ips")]
public sealed class VirtualIpEntity
{
    [SQLiteColumn(IsPrimaryKey = true, IsAutoIncrement = true)]
    public int Id { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 128)]
    public string Name { get; set; } = string.Empty;

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 32)]
    public string Type { get; set; } = "ipalias";

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 64)]
    public string Interface { get; set; } = string.Empty;

    [SQLiteColumn(IsNotNull = true, DataType = SQLiteDataType.TEXT, Size = 64)]
    public string Address { get; set; } = string.Empty;

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public int SubnetBits { get; set; } = 24;

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 256)]
    public string Description { get; set; } = string.Empty;

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER, DefaultValue = "1")]
    public int Enabled { get; set; } = 1;

    [SQLiteColumn(DataType = SQLiteDataType.INTEGER)]
    public int? Vhid { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 64)]
    public string? CarpPassword { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, Size = 16)]
    public string? Advskew { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime CreatedAt { get; set; }

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME)]
    public DateTime UpdatedAt { get; set; }
}
