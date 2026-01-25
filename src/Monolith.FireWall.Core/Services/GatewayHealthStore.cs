using CodeLogic;
using CL.SQLite.Services;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Services;

public sealed class GatewayHealthStore
{
    private CL.SQLite.SQLiteLibrary? _sqlite;
    private Repository<GatewayHealthEntity>? _healthRepository;
    private Repository<GatewayMonitorConfigEntity>? _configRepository;

    public GatewayHealthStore()
    {
        Initialize();
    }

    // ========================================================================
    // Gateway Health Status
    // ========================================================================

    public async Task<List<GatewayHealthEntity>> GetAllHealthAsync()
    {
        if (_healthRepository == null)
        {
            return new List<GatewayHealthEntity>();
        }

        var result = await _healthRepository.GetAllAsync();
        return result.IsSuccess && result.Data != null
            ? result.Data.ToList()
            : new List<GatewayHealthEntity>();
    }

    public async Task<GatewayHealthEntity?> GetHealthAsync(int gatewayId)
    {
        if (_sqlite == null)
        {
            return null;
        }

        var query = _sqlite.CreateQueryBuilder<GatewayHealthEntity>();
        var result = await query.Where(h => h.GatewayId == gatewayId).FirstOrDefaultAsync();
        return result.IsSuccess ? result.Data : null;
    }

    public async Task<List<GatewayHealthEntity>> GetByStatusAsync(GatewayHealthStatus status)
    {
        if (_sqlite == null)
        {
            return new List<GatewayHealthEntity>();
        }

        var query = _sqlite.CreateQueryBuilder<GatewayHealthEntity>();
        var result = await query.Where(h => h.Status == status).ExecuteAsync();
        return result.IsSuccess && result.Data != null
            ? result.Data.ToList()
            : new List<GatewayHealthEntity>();
    }

    public async Task<bool> UpsertHealthAsync(GatewayHealthEntity health)
    {
        if (_healthRepository == null)
        {
            return false;
        }

        var existing = await GetHealthAsync(health.GatewayId);
        if (existing != null)
        {
            health.Id = existing.Id;
            var update = await _healthRepository.UpdateAsync(health);
            return update.IsSuccess;
        }

        var insert = await _healthRepository.InsertAsync(health);
        return insert.IsSuccess;
    }

    public async Task<(bool StatusChanged, GatewayHealthStatus PreviousStatus)> UpdateHealthCheckAsync(
        int gatewayId,
        bool success,
        int? latencyMs,
        double? packetLossPercent,
        string? error = null)
    {
        var existing = await GetHealthAsync(gatewayId);
        var config = await GetMonitorConfigAsync(gatewayId);
        var failThreshold = config?.FailThreshold ?? 3;
        var recoverThreshold = config?.RecoverThreshold ?? 2;

        GatewayHealthEntity health;
        GatewayHealthStatus previousStatus;

        if (existing == null)
        {
            health = new GatewayHealthEntity
            {
                GatewayId = gatewayId,
                Status = GatewayHealthStatus.Unknown,
                LastCheckAt = DateTime.UtcNow
            };
            previousStatus = GatewayHealthStatus.Unknown;
        }
        else
        {
            health = existing;
            previousStatus = existing.Status;
        }

        health.LastCheckAt = DateTime.UtcNow;
        health.LatencyMs = latencyMs;
        health.PacketLossPercent = packetLossPercent;

        if (success)
        {
            health.ConsecutiveSuccesses++;
            health.ConsecutiveFailures = 0;
            health.LastSuccessAt = DateTime.UtcNow;

            // Transition to Online
            if (health.Status != GatewayHealthStatus.Online &&
                health.ConsecutiveSuccesses >= recoverThreshold)
            {
                health.Status = GatewayHealthStatus.Online;
                health.LastStateChangeAt = DateTime.UtcNow;
            }
        }
        else
        {
            health.ConsecutiveFailures++;
            health.ConsecutiveSuccesses = 0;
            health.LastFailureAt = DateTime.UtcNow;
            health.LastError = error;

            // Transition to Offline
            if (health.Status != GatewayHealthStatus.Offline &&
                health.ConsecutiveFailures >= failThreshold)
            {
                health.Status = GatewayHealthStatus.Offline;
                health.LastStateChangeAt = DateTime.UtcNow;
            }
        }

        // Check for degraded state based on packet loss or latency
        if (health.Status == GatewayHealthStatus.Online)
        {
            var isHighLatency = config?.LatencyThresholdMs != null && latencyMs > config.LatencyThresholdMs;
            var isHighPacketLoss = config?.PacketLossThreshold != null && packetLossPercent > config.PacketLossThreshold;

            if (isHighLatency || isHighPacketLoss)
            {
                health.Status = GatewayHealthStatus.Degraded;
                health.LastStateChangeAt = DateTime.UtcNow;
            }
        }

        await UpsertHealthAsync(health);

        return (health.Status != previousStatus, previousStatus);
    }

    public async Task<bool> DeleteHealthAsync(int gatewayId)
    {
        if (_healthRepository == null)
        {
            return false;
        }

        var existing = await GetHealthAsync(gatewayId);
        if (existing == null)
        {
            return true;
        }

        var result = await _healthRepository.DeleteAsync(existing.Id);
        return result.IsSuccess;
    }

    // ========================================================================
    // Gateway Monitor Configurations
    // ========================================================================

    public async Task<List<GatewayMonitorConfigEntity>> GetAllConfigsAsync()
    {
        if (_configRepository == null)
        {
            return new List<GatewayMonitorConfigEntity>();
        }

        var result = await _configRepository.GetAllAsync();
        return result.IsSuccess && result.Data != null
            ? result.Data.ToList()
            : new List<GatewayMonitorConfigEntity>();
    }

    public async Task<GatewayMonitorConfigEntity?> GetMonitorConfigAsync(int gatewayId)
    {
        if (_sqlite == null)
        {
            return null;
        }

        var query = _sqlite.CreateQueryBuilder<GatewayMonitorConfigEntity>();
        var result = await query.Where(c => c.GatewayId == gatewayId).FirstOrDefaultAsync();
        return result.IsSuccess ? result.Data : null;
    }

    public async Task<List<GatewayMonitorConfigEntity>> GetEnabledConfigsAsync()
    {
        if (_sqlite == null)
        {
            return new List<GatewayMonitorConfigEntity>();
        }

        var query = _sqlite.CreateQueryBuilder<GatewayMonitorConfigEntity>();
        var result = await query.Where(c => c.Enabled == true).ExecuteAsync();
        return result.IsSuccess && result.Data != null
            ? result.Data.ToList()
            : new List<GatewayMonitorConfigEntity>();
    }

    public async Task<bool> UpsertMonitorConfigAsync(GatewayMonitorConfigEntity config)
    {
        if (_configRepository == null)
        {
            return false;
        }

        var existing = await GetMonitorConfigAsync(config.GatewayId);
        if (existing != null)
        {
            config.Id = existing.Id;
            var update = await _configRepository.UpdateAsync(config);
            return update.IsSuccess;
        }

        var insert = await _configRepository.InsertAsync(config);
        if (!insert.IsSuccess)
        {
            return false;
        }

        config.Id = Convert.ToInt32(insert.Data);
        return true;
    }

    public async Task<bool> DeleteMonitorConfigAsync(int gatewayId)
    {
        if (_configRepository == null)
        {
            return false;
        }

        var existing = await GetMonitorConfigAsync(gatewayId);
        if (existing == null)
        {
            return true;
        }

        var result = await _configRepository.DeleteAsync(existing.Id);
        return result.IsSuccess;
    }

    public async Task<GatewayMonitorConfigEntity> GetOrCreateDefaultConfigAsync(int gatewayId)
    {
        var existing = await GetMonitorConfigAsync(gatewayId);
        if (existing != null)
        {
            return existing;
        }

        var defaultConfig = new GatewayMonitorConfigEntity
        {
            GatewayId = gatewayId,
            MonitorType = GatewayMonitorType.Icmp,
            IntervalSeconds = 10,
            TimeoutMs = 1000,
            FailThreshold = 3,
            RecoverThreshold = 2,
            SampleCount = 10,
            Enabled = true,
            UpdatedAt = DateTime.UtcNow
        };

        await UpsertMonitorConfigAsync(defaultConfig);
        return defaultConfig;
    }

    // ========================================================================
    // Extended Config (for threshold lookups in UpdateHealthCheckAsync)
    // ========================================================================

    private sealed class ExtendedConfig
    {
        public int? LatencyThresholdMs { get; set; }
        public int? PacketLossThreshold { get; set; }
    }

    private async Task<ExtendedConfig?> GetExtendedConfigAsync(int gatewayId)
    {
        var config = await GetMonitorConfigAsync(gatewayId);
        if (config == null) return null;

        // These thresholds would come from gateway group settings
        // For now, return defaults that won't trigger degraded state
        return new ExtendedConfig
        {
            LatencyThresholdMs = 500,
            PacketLossThreshold = 20
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
            _healthRepository = sqlite.CreateRepository<GatewayHealthEntity>();
            _configRepository = sqlite.CreateRepository<GatewayMonitorConfigEntity>();
        }
        catch
        {
            _sqlite = null;
            _healthRepository = null;
            _configRepository = null;
        }
    }
}
