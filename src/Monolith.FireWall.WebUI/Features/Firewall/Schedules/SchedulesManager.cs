using System.Text.Json;
using CL.SQLite.Services;
using CodeLogic;
using Monolith.FireWall.Common.Services;
using System.Collections.Generic;

namespace Monolith.FireWall.WebUI.Features.Firewall.Schedules;

public class SchedulesManager
{
    private CL.SQLite.Services.Repository<FirewallScheduleEntity>? _repository;
    private CL.SQLite.Services.QueryBuilder<FirewallScheduleEntity>? _queryBuilder;

    public SchedulesManager()
    {
        InitializeRepository();
    }

    private void InitializeRepository()
    {
        try
        {
            var sqlite = CodeLogic.Libs.Get<CL.SQLite.SQLiteLibrary>();
            if (sqlite != null)
            {
                _repository = sqlite.CreateRepository<FirewallScheduleEntity>();
                _queryBuilder = sqlite.CreateQueryBuilder<FirewallScheduleEntity>();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize Schedules repository: {ex.Message}");
        }
    }

    public async Task<List<FirewallSchedule>> ListSchedulesAsync()
    {
        try
        {
            if (_repository == null || _queryBuilder == null)
            {
                return new List<FirewallSchedule>();
            }

            var entitiesResult = await _queryBuilder.Select(e => e).ExecuteAsync();
            var entities = entitiesResult?.Data ?? new List<FirewallScheduleEntity>();
            return entities.Select(EntityToSchedule).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error listing schedules: {ex.Message}");
            return new List<FirewallSchedule>();
        }
    }

    public async Task<FirewallSchedule?> GetScheduleAsync(int id)
    {
        try
        {
            if (_repository == null) return null;

            var result = await _repository.GetByIdAsync(id);
            if (result == null || !result.IsSuccess || result.Data == null) return null;
            var entity = result.Data;
            return entity != null ? EntityToSchedule(entity) : null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting schedule: {ex.Message}");
            return null;
        }
    }

    public async Task<FirewallSchedule> CreateScheduleAsync(FirewallSchedule schedule)
    {
        try
        {
            if (_repository == null)
            {
                throw new Exception("Repository not initialized");
            }

            var entity = ScheduleToEntity(schedule);
            entity.CreatedAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;

            var insertResult = await _repository.InsertAsync(entity);
            if (insertResult != null && insertResult.IsSuccess && insertResult.Data > 0) {
                entity.Id = (int)insertResult.Data;
            }
            
            // Log the change
            await LoggingManager.Instance.LogMonolithAsync(
                "Changes",
                "Info",
                "firewall/schedules",
                $"Created firewall schedule: {schedule.Name}",
                null,
                null,
                new Dictionary<string, object> { { "scheduleId", entity.Id }, { "scheduleName", schedule.Name } }
            );
            
            return EntityToSchedule(entity);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating schedule: {ex.Message}");
            throw;
        }
    }

    public async Task<FirewallSchedule> UpdateScheduleAsync(int id, FirewallSchedule schedule)
    {
        try
        {
            if (_repository == null)
            {
                throw new Exception("Repository not initialized");
            }

            var getResult = await _repository.GetByIdAsync(id);
            if (getResult == null || !getResult.IsSuccess || getResult.Data == null)
            {
                throw new Exception($"Schedule with ID {id} not found");
            }
            var existing = getResult.Data;

            var updated = ScheduleToEntity(schedule);
            updated.Id = id;
            updated.CreatedAt = existing.CreatedAt;
            updated.UpdatedAt = DateTime.UtcNow;

            await _repository.UpdateAsync(updated);
            
            // Log the change
            await LoggingManager.Instance.LogMonolithAsync(
                "Changes",
                "Info",
                "firewall/schedules",
                $"Updated firewall schedule: {schedule.Name} (ID: {id})",
                null,
                null,
                new Dictionary<string, object> { { "scheduleId", id }, { "scheduleName", schedule.Name } }
            );
            
            return EntityToSchedule(updated);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating schedule: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> DeleteScheduleAsync(int id)
    {
        try
        {
            if (_repository == null)
            {
                throw new Exception("Repository not initialized");
            }

            // Get schedule info before deletion for logging
            var scheduleToDelete = await GetScheduleAsync(id);
            var scheduleName = scheduleToDelete?.Name ?? "Unknown";

            var deleteResult = await _repository.DeleteAsync(id);
            var success = (deleteResult != null && deleteResult.IsSuccess);
            
            // Log the change
            if (success)
            {
                await LoggingManager.Instance.LogMonolithAsync(
                    "Changes",
                    "Info",
                    "firewall/schedules",
                    $"Deleted firewall schedule: {scheduleName} (ID: {id})",
                    null,
                    null,
                    new Dictionary<string, object> { { "scheduleId", id }, { "scheduleName", scheduleName } }
                );
            }
            
            return success;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting schedule: {ex.Message}");
            throw;
        }
    }

    private FirewallSchedule EntityToSchedule(FirewallScheduleEntity entity)
    {
        return new FirewallSchedule
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            TimeRanges = JsonSerializer.Deserialize<List<ScheduleTimeRange>>(entity.TimeRanges) ?? new(),
            Enabled = entity.Enabled,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    private FirewallScheduleEntity ScheduleToEntity(FirewallSchedule schedule)
    {
        return new FirewallScheduleEntity
        {
            Id = schedule.Id,
            Name = schedule.Name,
            Description = schedule.Description,
            TimeRanges = JsonSerializer.Serialize(schedule.TimeRanges),
            Enabled = schedule.Enabled
        };
    }
}
