using CodeLogic;
using CL.SQLite.Services;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Services;

public sealed class InterfaceOperationalStateStore
{
    private CL.SQLite.SQLiteLibrary? _sqlite;
    private Repository<InterfaceOperationalStateEntity>? _repository;

    public InterfaceOperationalStateStore()
    {
        Initialize();
    }

    public async Task<List<InterfaceOperationalStateEntity>> GetAllAsync()
    {
        if (_repository == null)
        {
            return new List<InterfaceOperationalStateEntity>();
        }

        var result = await _repository.GetAllAsync();
        return result.IsSuccess && result.Data != null
            ? result.Data.ToList()
            : new List<InterfaceOperationalStateEntity>();
    }

    public async Task<InterfaceOperationalStateEntity?> GetAsync(string interfaceName)
    {
        if (_sqlite == null)
        {
            return null;
        }

        var query = _sqlite.CreateQueryBuilder<InterfaceOperationalStateEntity>();
        var result = await query
            .Where(s => s.InterfaceName == interfaceName)
            .FirstOrDefaultAsync();

        return result.IsSuccess ? result.Data : null;
    }

    public async Task<List<InterfaceOperationalStateEntity>> GetByLinkStateAsync(LinkState state)
    {
        if (_sqlite == null)
        {
            return new List<InterfaceOperationalStateEntity>();
        }

        var query = _sqlite.CreateQueryBuilder<InterfaceOperationalStateEntity>();
        var result = await query
            .Where(s => s.LinkState == state)
            .ExecuteAsync();

        return result.IsSuccess && result.Data != null
            ? result.Data.ToList()
            : new List<InterfaceOperationalStateEntity>();
    }

    public async Task<List<InterfaceOperationalStateEntity>> GetByHealthStatusAsync(InterfaceHealthStatus status)
    {
        if (_sqlite == null)
        {
            return new List<InterfaceOperationalStateEntity>();
        }

        var query = _sqlite.CreateQueryBuilder<InterfaceOperationalStateEntity>();
        var result = await query
            .Where(s => s.HealthStatus == status)
            .ExecuteAsync();

        return result.IsSuccess && result.Data != null
            ? result.Data.ToList()
            : new List<InterfaceOperationalStateEntity>();
    }

    public async Task<bool> UpsertAsync(InterfaceOperationalStateEntity state)
    {
        if (_repository == null)
        {
            return false;
        }

        var existing = await GetAsync(state.InterfaceName);
        if (existing != null)
        {
            state.Id = existing.Id;
            var update = await _repository.UpdateAsync(state);
            return update.IsSuccess;
        }

        var insert = await _repository.InsertAsync(state);
        return insert.IsSuccess;
    }

    public async Task<bool> UpdateLinkStateAsync(string interfaceName, LinkState linkState, DateTime? linkChangeAt = null)
    {
        var existing = await GetAsync(interfaceName);
        if (existing == null)
        {
            return false;
        }

        existing.LinkState = linkState;
        existing.LastSeenAt = DateTime.UtcNow;
        if (linkChangeAt.HasValue)
        {
            existing.LastLinkChangeAt = linkChangeAt.Value;
        }

        if (linkState == LinkState.Down)
        {
            existing.HealthStatus = InterfaceHealthStatus.Down;
        }
        else if (linkState == LinkState.Up && existing.HealthStatus == InterfaceHealthStatus.Down)
        {
            existing.HealthStatus = InterfaceHealthStatus.Healthy;
        }

        var update = await _repository!.UpdateAsync(existing);
        return update.IsSuccess;
    }

    public async Task<bool> UpdateIpAddressAsync(
        string interfaceName,
        string? ipv4Address,
        int? ipv4Prefix,
        DateTime? ipChangeAt = null)
    {
        var existing = await GetAsync(interfaceName);
        if (existing == null)
        {
            return false;
        }

        existing.CurrentIpv4Address = ipv4Address;
        existing.CurrentIpv4Prefix = ipv4Prefix;
        existing.LastSeenAt = DateTime.UtcNow;
        if (ipChangeAt.HasValue)
        {
            existing.LastIpChangeAt = ipChangeAt.Value;
        }

        var update = await _repository!.UpdateAsync(existing);
        return update.IsSuccess;
    }

    public async Task<bool> UpdateDhcpInfoAsync(
        string interfaceName,
        string? serverAddress,
        string? gateway,
        DateTime? leaseObtained,
        DateTime? leaseExpires,
        List<string>? dnsServers)
    {
        var existing = await GetAsync(interfaceName);
        if (existing == null)
        {
            return false;
        }

        existing.DhcpServerAddress = serverAddress;
        existing.DhcpGateway = gateway;
        existing.DhcpLeaseObtained = leaseObtained;
        existing.DhcpLeaseExpires = leaseExpires;
        existing.DhcpDnsServersJson = dnsServers != null
            ? System.Text.Json.JsonSerializer.Serialize(dnsServers)
            : null;
        existing.LastSeenAt = DateTime.UtcNow;

        var update = await _repository!.UpdateAsync(existing);
        return update.IsSuccess;
    }

    public async Task<bool> UpdateTrafficStatsAsync(
        string interfaceName,
        long rxBytes, long txBytes,
        long rxPackets, long txPackets,
        long rxErrors, long txErrors)
    {
        var existing = await GetAsync(interfaceName);
        if (existing == null)
        {
            return false;
        }

        existing.RxBytes = rxBytes;
        existing.TxBytes = txBytes;
        existing.RxPackets = rxPackets;
        existing.TxPackets = txPackets;
        existing.RxErrors = rxErrors;
        existing.TxErrors = txErrors;
        existing.LastSeenAt = DateTime.UtcNow;

        var update = await _repository!.UpdateAsync(existing);
        return update.IsSuccess;
    }

    public async Task<bool> DeleteAsync(string interfaceName)
    {
        if (_repository == null)
        {
            return false;
        }

        var existing = await GetAsync(interfaceName);
        if (existing == null)
        {
            return true;
        }

        var result = await _repository.DeleteAsync(existing.Id);
        return result.IsSuccess;
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
            _repository = sqlite.CreateRepository<InterfaceOperationalStateEntity>();
        }
        catch
        {
            _sqlite = null;
            _repository = null;
        }
    }
}
