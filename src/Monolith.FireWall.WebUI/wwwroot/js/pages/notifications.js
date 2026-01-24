/**
 * Notifications Management Page
 */

Monolith.Pages.Notifications = {
    notifications: [],
    filteredNotifications: [],
    filters: {
        status: 'all',
        severity: 'all',
        limit: 20
    },

    init: async function() {
        await this.loadNotifications();
        this.attachEventHandlers();
    },

    loadNotifications: async function() {
        try {
            $('#notifications-loading').removeClass('d-none');
            $('#notifications-empty').addClass('d-none');
            $('#notifications-table-container').addClass('d-none');

            const limit = this.filters.limit || 100;
            const unreadOnly = this.filters.status === 'unread';
            
            const response = await Monolith.API.get(`/monitoring/notifications?limit=${limit}&unreadOnly=${unreadOnly}`);
            const data = response.Data || response.data || {};
            this.notifications = data.Notifications || data.notifications || [];
            
            this.applyFilters();
            this.render();
        } catch (error) {
            console.error('Error loading notifications:', error);
            Monolith.UI.toast('Failed to load notifications', 'error');
            $('#notifications-loading').addClass('d-none');
            $('#notifications-empty').removeClass('d-none');
        }
    },

    applyFilters: function() {
        let filtered = [...this.notifications];

        // Filter by status
        if (this.filters.status === 'unread') {
            filtered = filtered.filter(n => !(n.ReadAt || n.readAt));
        } else if (this.filters.status === 'read') {
            filtered = filtered.filter(n => !!(n.ReadAt || n.readAt));
        }

        // Filter by severity
        if (this.filters.severity !== 'all') {
            filtered = filtered.filter(n => {
                const severity = (n.Severity || n.severity || 'info').toLowerCase();
                return severity === this.filters.severity.toLowerCase();
            });
        }

        this.filteredNotifications = filtered;
    },

    render: function() {
        $('#notifications-loading').addClass('d-none');

        if (this.filteredNotifications.length === 0) {
            $('#notifications-empty').removeClass('d-none');
            $('#notifications-table-container').addClass('d-none');
            return;
        }

        $('#notifications-empty').addClass('d-none');
        $('#notifications-table-container').removeClass('d-none');

        const tbody = $('#notifications-tbody');
        tbody.empty();

        this.filteredNotifications.forEach(n => {
            const id = n.Id || n.id;
            const severity = (n.Severity || n.severity || 'info').toLowerCase();
            const title = n.Title || n.title || 'Notification';
            const message = n.Message || n.message || '';
            const created = n.CreatedAt || n.createdAt;
            const createdText = created ? new Date(created).toLocaleString() : '';
            const readAt = n.ReadAt || n.readAt;
            const isRead = !!readAt;

            const badgeClass = severity === 'error' ? 'bg-danger' : severity === 'warning' ? 'bg-warning' : 'bg-info';
            const statusBadge = isRead 
                ? '<span class="badge bg-secondary">Read</span>'
                : '<span class="badge bg-primary">Unread</span>';

            const row = `
                <tr class="${isRead ? '' : 'table-active'}">
                    <td>
                        <input type="checkbox" class="form-check-input notification-checkbox" data-id="${id}">
                    </td>
                    <td>
                        <span class="badge ${badgeClass} text-uppercase">${severity}</span>
                    </td>
                    <td>
                        <strong>${title}</strong>
                    </td>
                    <td>
                        <span class="text-muted">${message || 'No message'}</span>
                    </td>
                    <td>
                        <small class="text-muted">${createdText}</small>
                    </td>
                    <td>
                        ${statusBadge}
                    </td>
                    <td>
                        <div class="btn-group btn-group-sm">
                            ${!isRead ? `
                                <button class="btn btn-outline-primary btn-mark-read" data-id="${id}" title="Mark as read">
                                    <i class="fa-solid fa-check"></i>
                                </button>
                            ` : ''}
                            <button class="btn btn-outline-danger btn-delete" data-id="${id}" title="Delete">
                                <i class="fa-solid fa-trash"></i>
                            </button>
                        </div>
                    </td>
                </tr>
            `;
            tbody.append(row);
        });
    },

    attachEventHandlers: function() {
        // Filter controls
        $('#filter-status, #filter-severity, #filter-limit').on('change', () => {
            this.filters.status = $('#filter-status').val();
            this.filters.severity = $('#filter-severity').val();
            this.filters.limit = parseInt($('#filter-limit').val());
        });

        $('#btn-apply-filters').on('click', async () => {
            await this.loadNotifications();
        });

        // Select all checkbox
        $('#select-all').on('change', function() {
            $('.notification-checkbox').prop('checked', $(this).prop('checked'));
        });

        // Mark as read (individual)
        $(document).on('click', '.btn-mark-read', async (e) => {
            const id = $(e.currentTarget).data('id');
            await this.markAsRead([id]);
        });

        // Delete (individual)
        $(document).on('click', '.btn-delete', async (e) => {
            const id = $(e.currentTarget).data('id');
            if (confirm('Are you sure you want to delete this notification?')) {
                await this.deleteNotifications([id]);
            }
        });

        // Mark all read
        $('#btn-mark-all-read').on('click', async () => {
            await this.markAllAsRead();
        });

        // Delete all read
        $('#btn-delete-all-read').on('click', async () => {
            if (confirm('Are you sure you want to delete all read notifications? This action cannot be undone.')) {
                await this.deleteAllRead();
            }
        });

        // Delete all
        $('#btn-delete-all').on('click', async () => {
            if (confirm('Are you sure you want to delete ALL notifications? This action cannot be undone.')) {
                await this.deleteAll();
            }
        });
    },

    markAsRead: async function(ids) {
        try {
            const response = await Monolith.API.post('/monitoring/notifications/read', { ids: ids });
            if (response.Success || response.success) {
                Monolith.UI.toast('Notification marked as read', 'success');
                await this.loadNotifications();
                // Also refresh the dropdown menu if it exists
                if (typeof loadNotifications === 'function') {
                    loadNotifications();
                }
            } else {
                throw new Error(response.Error || response.error || 'Failed to mark notification as read');
            }
        } catch (error) {
            console.error('Error marking notification as read:', error);
            Monolith.UI.toast('Failed to mark notification as read', 'error');
        }
    },

    markAllAsRead: async function() {
        try {
            const btn = $('#btn-mark-all-read');
            const originalText = btn.html();
            btn.prop('disabled', true).html('<i class="fa-solid fa-spinner fa-spin me-2"></i>Marking...');

            const response = await Monolith.API.post('/monitoring/notifications/read', { all: true });
            if (response.Success || response.success) {
                Monolith.UI.toast('All notifications marked as read', 'success');
                await this.loadNotifications();
                // Also refresh the dropdown menu if it exists
                if (typeof loadNotifications === 'function') {
                    loadNotifications();
                }
            } else {
                throw new Error(response.Error || response.error || 'Failed to mark notifications as read');
            }
        } catch (error) {
            console.error('Error marking all notifications as read:', error);
            Monolith.UI.toast('Failed to mark all notifications as read', 'error');
        } finally {
            $('#btn-mark-all-read').prop('disabled', false).html('<i class="fa-solid fa-check-double me-2"></i>Mark All Read');
        }
    },

    deleteNotifications: async function(ids) {
        try {
            const payload = ids.length === 1 
                ? { ids: ids }
                : { ids: ids };
            const response = await Monolith.API.post('/monitoring/notifications/delete', payload);
            if (response.Success || response.success) {
                Monolith.UI.toast(ids.length === 1 ? 'Notification deleted' : 'Notifications deleted', 'success');
                await this.loadNotifications();
                // Also refresh the dropdown menu if it exists
                if (typeof loadNotifications === 'function') {
                    loadNotifications();
                }
            } else {
                throw new Error(response.Error || response.error || 'Failed to delete notification');
            }
        } catch (error) {
            console.error('Error deleting notification:', error);
            Monolith.UI.toast('Failed to delete notification', 'error');
        }
    },

    deleteAllRead: async function() {
        try {
            const btn = $('#btn-delete-all-read');
            const originalText = btn.html();
            btn.prop('disabled', true).html('<i class="fa-solid fa-spinner fa-spin me-2"></i>Deleting...');

            const response = await Monolith.API.post('/monitoring/notifications/delete', { readOnly: true });
            if (response.Success || response.success) {
                Monolith.UI.toast('All read notifications deleted', 'success');
                await this.loadNotifications();
                // Also refresh the dropdown menu if it exists
                if (typeof loadNotifications === 'function') {
                    loadNotifications();
                }
            } else {
                throw new Error(response.Error || response.error || 'Failed to delete read notifications');
            }
        } catch (error) {
            console.error('Error deleting read notifications:', error);
            Monolith.UI.toast('Failed to delete read notifications', 'error');
        } finally {
            $('#btn-delete-all-read').prop('disabled', false).html('<i class="fa-solid fa-trash me-2"></i>Delete All Read');
        }
    },

    deleteAll: async function() {
        try {
            const btn = $('#btn-delete-all');
            const originalText = btn.html();
            btn.prop('disabled', true).html('<i class="fa-solid fa-spinner fa-spin me-2"></i>Deleting...');

            const response = await Monolith.API.post('/monitoring/notifications/delete', { all: true });
            if (response.Success || response.success) {
                Monolith.UI.toast('All notifications deleted', 'success');
                await this.loadNotifications();
                // Also refresh the dropdown menu if it exists
                if (typeof loadNotifications === 'function') {
                    loadNotifications();
                }
            } else {
                throw new Error(response.Error || response.error || 'Failed to delete notifications');
            }
        } catch (error) {
            console.error('Error deleting all notifications:', error);
            Monolith.UI.toast('Failed to delete all notifications', 'error');
        } finally {
            $('#btn-delete-all').prop('disabled', false).html('<i class="fa-solid fa-trash-can me-2"></i>Delete All');
        }
    }
};

// Initialize when page loads
function initializePage() {
    if (Monolith.Pages && Monolith.Pages.Notifications) {
        Monolith.Pages.Notifications.init();
    }
}

// Export for route system
if (typeof Monolith !== 'undefined' && Monolith.Pages) {
    Monolith.Pages.Notifications.init = Monolith.Pages.Notifications.init || initializePage;
}

// Auto-initialize on DOM ready
$(document).ready(function() {
    // Check if we're on the notifications page
    if (window.location.pathname === '/notifications' || $('#notifications-page-container').length > 0) {
        initializePage();
    }
});
