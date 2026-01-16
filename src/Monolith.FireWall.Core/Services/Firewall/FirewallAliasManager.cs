using System.Text.RegularExpressions;
using Monolith.FireWall.Platform.Validation;
using CodeLogic;
using CL.SQLite.Services;
using Monolith.FireWall.Common.Services;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Services.Firewall;

public sealed class FirewallAliasManager
{
    private static readonly Regex NameRegex = new("^[A-Za-z0-9_-]+$", RegexOptions.Compiled);
    private readonly LoggingManager _loggingManager;
    private CL.SQLite.SQLiteLibrary? _sqlite;
    private Repository<FirewallAliasEntity>? _aliasRepository;
    private Repository<FirewallAliasEntryEntity>? _entryRepository;

    public FirewallAliasManager()
    {
        _loggingManager = LoggingManager.Instance;
        Initialize();
    }

    public async Task<List<FirewallAliasView>> ListAliasesAsync()
    {
        if (_aliasRepository == null || _entryRepository == null)
        {
            return new List<FirewallAliasView>();
        }

        var aliasResult = await _aliasRepository.GetAllAsync();
        var aliases = aliasResult.IsSuccess && aliasResult.Data != null
            ? aliasResult.Data.ToList()
            : new List<FirewallAliasEntity>();

        if (aliases.Count == 0)
        {
            return new List<FirewallAliasView>();
        }

        var entryResult = await _entryRepository.GetAllAsync();
        var entries = entryResult.IsSuccess && entryResult.Data != null
            ? entryResult.Data.ToList()
            : new List<FirewallAliasEntryEntity>();

        var entryLookup = entries
            .GroupBy(e => e.AliasId)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Value).ToList());

        return aliases
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .Select(a => BuildView(a, entryLookup.TryGetValue(a.Id, out var values) ? values : new List<string>()))
            .ToList();
    }

    public async Task<FirewallAliasView?> GetAliasAsync(int id)
    {
        var alias = await GetAliasEntityAsync(id);
        if (alias == null)
        {
            return null;
        }

        var entries = await GetAliasEntriesAsync(alias.Id);
        return BuildView(alias, entries);
    }

    public async Task<FirewallAliasView?> GetAliasByNameAsync(string name)
    {
        if (_sqlite == null || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var query = _sqlite.CreateQueryBuilder<FirewallAliasEntity>();
        var result = await query
            .Where(a => a.Name == name)
            .FirstOrDefaultAsync();

        if (!result.IsSuccess || result.Data == null)
        {
            return null;
        }

        var entries = await GetAliasEntriesAsync(result.Data.Id);
        return BuildView(result.Data, entries);
    }

    public async Task<(bool Success, string? Error, FirewallAliasView? Alias)> CreateAliasAsync(FirewallAliasRequest request)
    {
        if (_aliasRepository == null || _entryRepository == null)
        {
            return (false, "Alias storage not available", null);
        }

        var validation = ValidateRequest(request, isUpdate: false);
        if (!validation.Success)
        {
            return (false, validation.Error, null);
        }

        var exists = await GetAliasByNameAsync(request.Name!);
        if (exists != null)
        {
            return (false, $"Alias '{request.Name}' already exists", null);
        }

        var now = DateTime.UtcNow;
        var aliasEntity = new FirewallAliasEntity
        {
            Name = request.Name!.Trim(),
            Type = NormalizeType(request.Type),
            Description = request.Description?.Trim(),
            Enabled = request.Enabled,
            CreatedAt = now,
            UpdatedAt = now
        };

        var insertResult = await _aliasRepository.InsertAsync(aliasEntity);
        if (!insertResult.IsSuccess || insertResult.Data <= 0)
        {
            return (false, "Failed to create alias", null);
        }

        aliasEntity.Id = (int)insertResult.Data;
        await UpsertEntriesAsync(aliasEntity.Id, request.Content!);

        await _loggingManager.LogSecurityAsync(
            "Firewall",
            "Info",
            "FirewallAliases",
            $"Created firewall alias '{aliasEntity.Name}'",
            details: new Dictionary<string, object>
            {
                ["aliasId"] = aliasEntity.Id,
                ["aliasType"] = aliasEntity.Type
            });

        var view = BuildView(aliasEntity, request.Content!);
        return (true, null, view);
    }

    public async Task<(bool Success, string? Error, FirewallAliasView? Alias)> UpdateAliasAsync(int id, FirewallAliasRequest request)
    {
        if (_aliasRepository == null || _entryRepository == null)
        {
            return (false, "Alias storage not available", null);
        }

        var existing = await GetAliasEntityAsync(id);
        if (existing == null)
        {
            return (false, "Alias not found", null);
        }

        var validation = ValidateRequest(request, isUpdate: true);
        if (!validation.Success)
        {
            return (false, validation.Error, null);
        }

        var name = request.Name!.Trim();
        if (!name.Equals(existing.Name, StringComparison.OrdinalIgnoreCase))
        {
            var nameMatch = await GetAliasByNameAsync(name);
            if (nameMatch != null && nameMatch.Id != id)
            {
                return (false, $"Alias '{name}' already exists", null);
            }
        }

        existing.Name = name;
        existing.Type = NormalizeType(request.Type);
        existing.Description = request.Description?.Trim();
        existing.Enabled = request.Enabled;
        existing.UpdatedAt = DateTime.UtcNow;

        var updateResult = await _aliasRepository.UpdateAsync(existing);
        if (!updateResult.IsSuccess)
        {
            return (false, "Failed to update alias", null);
        }

        await UpsertEntriesAsync(existing.Id, request.Content!);

        await _loggingManager.LogSecurityAsync(
            "Firewall",
            "Info",
            "FirewallAliases",
            $"Updated firewall alias '{existing.Name}'",
            details: new Dictionary<string, object>
            {
                ["aliasId"] = existing.Id,
                ["aliasType"] = existing.Type
            });

        var view = BuildView(existing, request.Content!);
        return (true, null, view);
    }

    public async Task<bool> DeleteAliasAsync(int id)
    {
        if (_aliasRepository == null || _entryRepository == null)
        {
            return false;
        }

        var alias = await GetAliasEntityAsync(id);
        if (alias == null)
        {
            return true;
        }

        await DeleteEntriesAsync(id);
        var deleteResult = await _aliasRepository.DeleteAsync(id);
        if (!deleteResult.IsSuccess)
        {
            return false;
        }

        await _loggingManager.LogSecurityAsync(
            "Firewall",
            "Info",
            "FirewallAliases",
            $"Deleted firewall alias '{alias.Name}'",
            details: new Dictionary<string, object>
            {
                ["aliasId"] = alias.Id,
                ["aliasType"] = alias.Type
            });

        return true;
    }

    public async Task<List<string>> ResolveAliasAsync(string name)
    {
        if (_sqlite == null)
        {
            return new List<string>();
        }

        var query = _sqlite.CreateQueryBuilder<FirewallAliasEntity>();
        var result = await query
            .Where(a => a.Name == name)
            .FirstOrDefaultAsync();

        if (!result.IsSuccess || result.Data == null)
        {
            return new List<string>();
        }

        return await GetAliasEntriesAsync(result.Data.Id);
    }

    private static FirewallAliasView BuildView(FirewallAliasEntity entity, List<string> entries)
    {
        return new FirewallAliasView
        {
            Id = entity.Id,
            Name = entity.Name,
            Type = entity.Type,
            Description = entity.Description,
            Enabled = entity.Enabled,
            Content = entries
        };
    }

    private (bool Success, string? Error) ValidateRequest(FirewallAliasRequest? request, bool isUpdate)
    {
        if (request == null)
        {
            return (false, "Request is required");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return (false, "Alias name is required");
        }

        var name = request.Name.Trim();
        if (name.Length > 128)
        {
            return (false, "Alias name is too long");
        }

        if (!NameRegex.IsMatch(name))
        {
            return (false, "Alias name can only contain letters, numbers, hyphens, and underscores");
        }

        if (request.Content == null || request.Content.Count == 0)
        {
            return (false, "Alias content is required");
        }

        if (!ValidateAliasContent(request.Type, request.Content, out var error))
        {
            return (false, error);
        }

        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            var normalized = NormalizeType(request.Type);
            if (string.IsNullOrEmpty(normalized))
            {
                return (false, "Alias type is invalid");
            }
        }

        return (true, null);
    }

    private bool ValidateAliasContent(string? type, List<string> content, out string? error)
    {
        error = null;
        var normalized = NormalizeType(type);
        foreach (var entry in content)
        {
            var value = entry?.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                error = "Alias entries cannot be empty";
                return false;
            }

            switch (normalized)
            {
                case "host":
                    if (!PlatformValidators.IsValidIp(value) &&
                        !PlatformValidators.TryParseCidr(value, out _, out _))
                    {
                        error = $"Invalid host or network entry: {value}";
                        return false;
                    }
                    break;
                case "network":
                    if (!PlatformValidators.TryParseCidr(value, out _, out _))
                    {
                        error = $"Invalid network CIDR: {value}";
                        return false;
                    }
                    break;
                case "port":
                    if (!ValidatePortEntry(value))
                    {
                        error = $"Invalid port entry: {value}";
                        return false;
                    }
                    break;
            }
        }

        return true;
    }

    private bool ValidatePortEntry(string value)
    {
        // allow single port or range N-M
        if (int.TryParse(value, out var single))
        {
            return single >= 1 && single <= 65535;
        }

        var parts = value.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2
            && int.TryParse(parts[0], out var start)
            && int.TryParse(parts[1], out var end))
        {
            return start >= 1 && end <= 65535 && start <= end;
        }

        return false;
    }

    private static string NormalizeType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return "host";
        }

        var normalized = type.Trim().ToLowerInvariant();
        return normalized switch
        {
            "host" => "host",
            "network" => "network",
            "port" => "port",
            "url" => "url",
            _ => ""
        };
    }

    private async Task<FirewallAliasEntity?> GetAliasEntityAsync(int id)
    {
        if (_aliasRepository == null)
        {
            return null;
        }

        var result = await _aliasRepository.GetByIdAsync(id);
        return result.IsSuccess ? result.Data : null;
    }

    private async Task<List<string>> GetAliasEntriesAsync(int aliasId)
    {
        if (_sqlite == null)
        {
            return new List<string>();
        }

        var query = _sqlite.CreateQueryBuilder<FirewallAliasEntryEntity>();
        var result = await query
            .Where(e => e.AliasId == aliasId)
            .ExecuteAsync();

        if (!result.IsSuccess || result.Data == null)
        {
            return new List<string>();
        }

        return result.Data
            .OrderBy(e => e.Id)
            .Select(e => e.Value)
            .ToList();
    }

    private async Task UpsertEntriesAsync(int aliasId, List<string> entries)
    {
        if (_entryRepository == null)
        {
            return;
        }

        await DeleteEntriesAsync(aliasId);

        var now = DateTime.UtcNow;
        foreach (var entry in entries)
        {
            var value = entry.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var entryEntity = new FirewallAliasEntryEntity
            {
                AliasId = aliasId,
                Value = value,
                CreatedAt = now
            };

            await _entryRepository.InsertAsync(entryEntity);
        }
    }

    private async Task DeleteEntriesAsync(int aliasId)
    {
        if (_sqlite == null || _entryRepository == null)
        {
            return;
        }

        var query = _sqlite.CreateQueryBuilder<FirewallAliasEntryEntity>();
        var entries = await query
            .Where(e => e.AliasId == aliasId)
            .ExecuteAsync();

        if (!entries.IsSuccess || entries.Data == null)
        {
            return;
        }

        foreach (var entry in entries.Data)
        {
            await _entryRepository.DeleteAsync(entry.Id);
        }
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
            _aliasRepository = sqlite.CreateRepository<FirewallAliasEntity>();
            _entryRepository = sqlite.CreateRepository<FirewallAliasEntryEntity>();
        }
        catch
        {
            _sqlite = null;
            _aliasRepository = null;
            _entryRepository = null;
        }
    }
}
