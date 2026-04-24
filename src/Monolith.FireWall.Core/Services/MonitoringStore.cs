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
        return result.IsSuccess && result.Value != null
            ? result.Value.ToList()
            : new List<MonitorDefinitionEntity>();
    }

    public async Task<MonitorDefinitionEntity?> GetDefinitionAsync(string key)
    {
        if (_sqlite == null)
        {
            return null;
        }

        var query = _sqlite.GetQueryBuilder<MonitorDefinitionEntity>();
        var result = await query.Where(d => d.Key == key).FirstOrDefaultAsync();
        return result.IsSuccess ? result.Value : null;
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
        return result.IsSuccess && result.Value != null
            ? result.Value.ToList()
            : new List<MonitorStatusEntity>();
    }

    public async Task<MonitorStatusEntity?> GetStatusAsync(string key)
    {
        if (_sqlite == null)
        {
            return null;
        }

        var query = _sqlite.GetQueryBuilder<MonitorStatusEntity>();
        var result = await query.Where(s => s.MonitorKey == key).FirstOrDefaultAsync();
        return result.IsSuccess ? result.Value : null;
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
        if (!result.IsSuccess || result.Value == null)
        {
            return 0;
        }

        return result.Value > int.MaxValue ? int.MaxValue : (int)result.Value;
    }

    public async Task<List<SystemNotificationEntity>> GetNotificationsAsync(int limit, bool unreadOnly)
    {
        if (_sqlite == null)
        {
            return new List<SystemNotificationEntity>();
        }

        var query = _sqlite.GetQueryBuilder<SystemNotificationEntity>().Select(n => n);
        if (unreadOnly)
        {
            query = query.Where(n => n.ReadAt == null);
        }

        var result = await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .ToListAsync();

        return result.IsSuccess && result.Value != null
            ? result.Value.ToList()
            : new List<SystemNotificationEntity>();
    }

    public async Task<int> GetUnreadCountAsync()
    {
        if (_sqlite == null)
        {
            return 0;
        }

        var query = _sqlite.GetQueryBuilder<SystemNotificationEntity>().Select(n => n);
        var result = await query.Where(n => n.ReadAt == null).ToListAsync();
        return result.IsSuccess && result.Value != null ? result.Value.Count() : 0;
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
            if (!entityResult.IsSuccess || entityResult.Value == null)
            {
                ok = false;
                continue;
            }

            var notification = entityResult.Value;
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

        var query = _sqlite.GetQueryBuilder<SystemNotificationEntity>();
        var result = await query.Where(n => n.ReadAt == null).ToListAsync();
        if (!result.IsSuccess || result.Value == null)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        var ok = true;
        foreach (var notification in result.Value)
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

        var query = _sqlite.GetQueryBuilder<SystemNotificationEntity>();
        var result = await query.ToListAsync();
        if (!result.IsSuccess || result.Value == null)
        {
            return false;
        }

        var ok = true;
        foreach (var notification in result.Value)
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

        var query = _sqlite.GetQueryBuilder<SystemNotificationEntity>();
        var result = await query.Where(n => n.ReadAt != null).ToListAsync();
        if (!result.IsSuccess || result.Value == null)
        {
            return false;
        }

        var ok = true;
        foreach (var notification in result.Value)
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
            var sqlite = CodeLogic.Libraries.Get<CL.SQLite.SQLiteLibrary>();
            if (sqlite == null)
            {
                return;
            }

            _sqlite = sqlite;
            _definitions = sqlite.GetRepository<MonitorDefinitionEntity>();
            _statuses = sqlite.GetRepository<MonitorStatusEntity>();
            _notifications = sqlite.GetRepository<SystemNotificationEntity>();
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
