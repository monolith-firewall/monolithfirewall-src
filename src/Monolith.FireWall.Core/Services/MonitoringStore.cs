using CodeLogic;
using CL.SQLite.Services;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Services;

public sealed class MonitoringStore
{
    private CL.SQLite.SQLiteLibrary? _sqlite;
    private Repository<MonitorDefinitionEntity>? _definitions;
    private Repository<MonitorStatusEntity>? _statuses;
    private Repository<SystemNotificationEntity>? _notifications;

    public MonitoringStore()
    {
        Initialize();
    }

    public async Task<List<MonitorDefinitionEntity>> GetDefinitionsAsync()
    {
        if (_definitions == null)
        {
            return new List<MonitorDefinitionEntity>();
        }

        var result = await _definitions.GetAllAsync();
        return result.IsSuccess && result.Data != null
            ? result.Data.ToList()
            : new List<MonitorDefinitionEntity>();
    }

    public async Task<MonitorDefinitionEntity?> GetDefinitionAsync(string key)
    {
        if (_sqlite == null)
        {
            return null;
        }

        var query = _sqlite.CreateQueryBuilder<MonitorDefinitionEntity>();
        var result = await query.Where(d => d.Key == key).FirstOrDefaultAsync();
        return result.IsSuccess ? result.Data : null;
    }

    public async Task<bool> UpsertDefinitionAsync(MonitorDefinitionEntity definition)
    {
        if (_definitions == null)
        {
            return false;
        }

        var existing = await GetDefinitionAsync(definition.Key);
        if (existing != null)
        {
            definition.Id = existing.Id;
            var update = await _definitions.UpdateAsync(definition);
            return update.IsSuccess;
        }

        var insert = await _definitions.InsertAsync(definition);
        return insert.IsSuccess;
    }

    public async Task<List<MonitorStatusEntity>> GetStatusesAsync()
    {
        if (_statuses == null)
        {
            return new List<MonitorStatusEntity>();
        }

        var result = await _statuses.GetAllAsync();
        return result.IsSuccess && result.Data != null
            ? result.Data.ToList()
            : new List<MonitorStatusEntity>();
    }

    public async Task<MonitorStatusEntity?> GetStatusAsync(string key)
    {
        if (_sqlite == null)
        {
            return null;
        }

        var query = _sqlite.CreateQueryBuilder<MonitorStatusEntity>();
        var result = await query.Where(s => s.MonitorKey == key).FirstOrDefaultAsync();
        return result.IsSuccess ? result.Data : null;
    }

    public async Task<bool> UpsertStatusAsync(MonitorStatusEntity status)
    {
        if (_statuses == null)
        {
            return false;
        }

        var existing = await GetStatusAsync(status.MonitorKey);
        if (existing != null)
        {
            status.Id = existing.Id;
            var update = await _statuses.UpdateAsync(status);
            return update.IsSuccess;
        }

        var insert = await _statuses.InsertAsync(status);
        return insert.IsSuccess;
    }

    public async Task<int> InsertNotificationAsync(SystemNotificationEntity notification)
    {
        if (_notifications == null)
        {
            return 0;
        }

        var result = await _notifications.InsertAsync(notification);
        if (!result.IsSuccess || result.Data == null)
        {
            return 0;
        }

        return result.Data > int.MaxValue ? int.MaxValue : (int)result.Data;
    }

    public async Task<List<SystemNotificationEntity>> GetNotificationsAsync(int limit, bool unreadOnly)
    {
        if (_sqlite == null)
        {
            return new List<SystemNotificationEntity>();
        }

        var query = _sqlite.CreateQueryBuilder<SystemNotificationEntity>().Select(n => n);
        if (unreadOnly)
        {
            query = query.Where(n => n.ReadAt == null);
        }

        var result = await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .ExecuteAsync();

        return result.IsSuccess && result.Data != null
            ? result.Data.ToList()
            : new List<SystemNotificationEntity>();
    }

    public async Task<int> GetUnreadCountAsync()
    {
        if (_sqlite == null)
        {
            return 0;
        }

        var query = _sqlite.CreateQueryBuilder<SystemNotificationEntity>().Select(n => n);
        var result = await query.Where(n => n.ReadAt == null).ExecuteAsync();
        return result.IsSuccess && result.Data != null ? result.Data.Count() : 0;
    }

    public async Task<bool> MarkNotificationsReadAsync(IEnumerable<int> ids)
    {
        if (_notifications == null)
        {
            return false;
        }

        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
        {
            return true;
        }

        var now = DateTime.UtcNow;
        var ok = true;
        foreach (var id in idList)
        {
            var entityResult = await _notifications.GetByIdAsync(id);
            if (!entityResult.IsSuccess || entityResult.Data == null)
            {
                ok = false;
                continue;
            }

            var notification = entityResult.Data;
            if (notification.ReadAt.HasValue)
            {
                continue;
            }

            notification.ReadAt = now;
            var update = await _notifications.UpdateAsync(notification);
            ok = ok && update.IsSuccess;
        }

        return ok;
    }

    public async Task<bool> MarkAllReadAsync()
    {
        if (_notifications == null || _sqlite == null)
        {
            return false;
        }

        var query = _sqlite.CreateQueryBuilder<SystemNotificationEntity>();
        var result = await query.Where(n => n.ReadAt == null).ExecuteAsync();
        if (!result.IsSuccess || result.Data == null)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        var ok = true;
        foreach (var notification in result.Data)
        {
            notification.ReadAt = now;
            var update = await _notifications.UpdateAsync(notification);
            ok = ok && update.IsSuccess;
        }

        return ok;
    }

    public async Task<bool> DeleteNotificationsAsync(IEnumerable<int> ids)
    {
        if (_notifications == null)
        {
            return false;
        }

        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
        {
            return true;
        }

        var ok = true;
        foreach (var id in idList)
        {
            var deleteResult = await _notifications.DeleteAsync(id);
            ok = ok && deleteResult.IsSuccess;
        }

        return ok;
    }

    public async Task<bool> DeleteAllAsync()
    {
        if (_notifications == null || _sqlite == null)
        {
            return false;
        }

        var query = _sqlite.CreateQueryBuilder<SystemNotificationEntity>();
        var result = await query.ExecuteAsync();
        if (!result.IsSuccess || result.Data == null)
        {
            return false;
        }

        var ok = true;
        foreach (var notification in result.Data)
        {
            var deleteResult = await _notifications.DeleteAsync(notification.Id);
            ok = ok && deleteResult.IsSuccess;
        }

        return ok;
    }

    public async Task<bool> DeleteAllReadAsync()
    {
        if (_notifications == null || _sqlite == null)
        {
            return false;
        }

        var query = _sqlite.CreateQueryBuilder<SystemNotificationEntity>();
        var result = await query.Where(n => n.ReadAt != null).ExecuteAsync();
        if (!result.IsSuccess || result.Data == null)
        {
            return false;
        }

        var ok = true;
        foreach (var notification in result.Data)
        {
            var deleteResult = await _notifications.DeleteAsync(notification.Id);
            ok = ok && deleteResult.IsSuccess;
        }

        return ok;
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
            _definitions = sqlite.CreateRepository<MonitorDefinitionEntity>();
            _statuses = sqlite.CreateRepository<MonitorStatusEntity>();
            _notifications = sqlite.CreateRepository<SystemNotificationEntity>();
        }
        catch
        {
            _sqlite = null;
            _definitions = null;
            _statuses = null;
            _notifications = null;
        }
    }
}
