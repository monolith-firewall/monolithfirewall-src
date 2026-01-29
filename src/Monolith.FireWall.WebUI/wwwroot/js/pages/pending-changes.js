/**
 * Pending Changes Page
 * Full-page view for managing pending configuration changes
 */

var Monolith = window.Monolith || {};

Monolith.PendingChangesPage = {
    pollInterval: null,
    pollIntervalMs: 10000, // Poll every 10 seconds
    lastCount: 0,
    changes: [],
    history: [],

    init: function () {
        this.render();
        this.loadPendingChanges();
        this.loadHistory();
        this.bindEvents();
        this.startPolling();
    },

    render: function () {
        var html = `
            <div class="page-header">
                <div class="page-header-content">
                    <nav aria-label="breadcrumb">
                        <ol class="breadcrumb">
                            <li class="breadcrumb-item"><a href="/dashboard">Dashboard</a></li>
                            <li class="breadcrumb-item"><a href="/system/settings">System</a></li>
                            <li class="breadcrumb-item active" aria-current="page">Pending Changes</li>
                        </ol>
                    </nav>
                    <h1 class="page-title"><i class="fa-solid fa-clock-rotate-left me-2"></i>Pending Changes</h1>
                </div>
            </div>

            <div class="container-fluid">
                <!-- Summary Stats -->
                <div class="row mb-4">
                    <div class="col-md-3">
                        <div class="card bg-warning text-dark">
                            <div class="card-body text-center">
                                <h3 id="pending-count" class="mb-0">0</h3>
                                <small>Pending Changes</small>
                            </div>
                        </div>
                    </div>
                    <div class="col-md-3">
                        <div class="card bg-info text-white">
                            <div class="card-body text-center">
                                <h3 id="restart-count" class="mb-0">0</h3>
                                <small>Require Restart</small>
                            </div>
                        </div>
                    </div>
                    <div class="col-md-3">
                        <div class="card bg-danger text-white">
                            <div class="card-body text-center">
                                <h3 id="reboot-count" class="mb-0">0</h3>
                                <small>Require Reboot</small>
                            </div>
                        </div>
                    </div>
                    <div class="col-md-3">
                        <div class="card">
                            <div class="card-body d-flex align-items-center justify-content-center gap-2">
                                <button class="btn btn-success" id="btn-apply-all" disabled>
                                    <i class="fa-solid fa-check me-1"></i>Apply All
                                </button>
                                <button class="btn btn-outline-danger" id="btn-discard-all" disabled>
                                    <i class="fa-solid fa-trash me-1"></i>Discard All
                                </button>
                            </div>
                        </div>
                    </div>
                </div>

                <!-- Tabs -->
                <ul class="nav nav-tabs mb-3" id="changes-tabs" role="tablist">
                    <li class="nav-item" role="presentation">
                        <button class="nav-link active" id="pending-tab" data-bs-toggle="tab" data-bs-target="#pending-pane" type="button" role="tab">
                            <i class="fa-solid fa-clock me-1"></i>Pending
                            <span class="badge bg-warning text-dark ms-1" id="pending-badge">0</span>
                        </button>
                    </li>
                    <li class="nav-item" role="presentation">
                        <button class="nav-link" id="history-tab" data-bs-toggle="tab" data-bs-target="#history-pane" type="button" role="tab">
                            <i class="fa-solid fa-history me-1"></i>History
                        </button>
                    </li>
                </ul>

                <div class="tab-content" id="changes-tab-content">
                    <!-- Pending Changes Tab -->
                    <div class="tab-pane fade show active" id="pending-pane" role="tabpanel">
                        <div class="card">
                            <div class="card-header d-flex justify-content-between align-items-center">
                                <span><i class="fa-solid fa-list me-2"></i>Pending Changes</span>
                                <button class="btn btn-sm btn-outline-secondary" id="btn-refresh-pending">
                                    <i class="fa-solid fa-sync-alt me-1"></i>Refresh
                                </button>
                            </div>
                            <div class="card-body p-0">
                                <div id="pending-changes-list" class="pending-changes-full-list">
                                    <div class="text-center py-5 text-muted">
                                        <i class="fa-solid fa-spinner fa-spin fa-2x mb-3"></i>
                                        <p>Loading pending changes...</p>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>

                    <!-- History Tab -->
                    <div class="tab-pane fade" id="history-pane" role="tabpanel">
                        <div class="card">
                            <div class="card-header d-flex justify-content-between align-items-center">
                                <span><i class="fa-solid fa-history me-2"></i>Change History</span>
                                <button class="btn btn-sm btn-outline-secondary" id="btn-refresh-history">
                                    <i class="fa-solid fa-sync-alt me-1"></i>Refresh
                                </button>
                            </div>
                            <div class="card-body p-0">
                                <div class="table-responsive">
                                    <table class="table table-hover mb-0" id="history-table">
                                        <thead>
                                            <tr>
                                                <th>Date/Time</th>
                                                <th>Type</th>
                                                <th>Key</th>
                                                <th>Action</th>
                                                <th>Changed By</th>
                                                <th>Source</th>
                                            </tr>
                                        </thead>
                                        <tbody id="history-tbody">
                                            <tr>
                                                <td colspan="6" class="text-center py-4 text-muted">
                                                    <i class="fa-solid fa-spinner fa-spin"></i> Loading history...
                                                </td>
                                            </tr>
                                        </tbody>
                                    </table>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>

            <!-- Validation Modal -->
            <div class="modal fade" id="validation-modal" tabindex="-1">
                <div class="modal-dialog">
                    <div class="modal-content">
                        <div class="modal-header">
                            <h5 class="modal-title"><i class="fa-solid fa-check-double me-2"></i>Validation Results</h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                        </div>
                        <div class="modal-body" id="validation-body">
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Close</button>
                            <button type="button" class="btn btn-success" id="btn-apply-after-validate" style="display:none;">
                                Apply Anyway
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        `;

        $('#pending-changes-container').html(html);
    },

    bindEvents: function () {
        var self = this;

        $('#btn-apply-all').off('click').on('click', function () {
            self.applyAll();
        });

        $('#btn-discard-all').off('click').on('click', function () {
            self.discardAll();
        });

        $('#btn-refresh-pending').off('click').on('click', function () {
            self.loadPendingChanges();
        });

        $('#btn-refresh-history').off('click').on('click', function () {
            self.loadHistory();
        });

        $(document).off('click', '.btn-apply-single').on('click', '.btn-apply-single', function () {
            var id = $(this).data('id');
            self.applySingle(id);
        });

        $(document).off('click', '.btn-discard-single').on('click', '.btn-discard-single', function () {
            var id = $(this).data('id');
            self.discardSingle(id);
        });

        $(document).off('click', '.btn-view-change').on('click', '.btn-view-change', function () {
            var id = $(this).data('id');
            self.viewChangeDetails(id);
        });
    },

    startPolling: function () {
        var self = this;
        if (this.pollInterval) clearInterval(this.pollInterval);
        this.pollInterval = setInterval(function () {
            self.loadPendingChanges(true);
        }, this.pollIntervalMs);
    },

    stopPolling: function () {
        if (this.pollInterval) {
            clearInterval(this.pollInterval);
            this.pollInterval = null;
        }
    },

    loadPendingChanges: async function (silent) {
        var self = this;

        if (!silent) {
            $('#pending-changes-list').html(`
                <div class="text-center py-5 text-muted">
                    <i class="fa-solid fa-spinner fa-spin fa-2x mb-3"></i>
                    <p>Loading pending changes...</p>
                </div>
            `);
        }

        try {
            var response = await Monolith.API.post('/api/core', {
                action: 'config.pending.list'
            });

            if (response.Success && response.Data) {
                self.changes = response.Data.changes || [];
                self.renderPendingChanges();
                self.updateStats();
            } else {
                self.renderError('Failed to load pending changes');
            }
        } catch (error) {
            console.error('Failed to load pending changes:', error);
            if (!silent) {
                self.renderError('Failed to load pending changes: ' + error.message);
            }
        }
    },

    loadHistory: async function () {
        var self = this;

        try {
            var response = await Monolith.API.post('/api/core', {
                action: 'config.history.list',
                limit: 100
            });

            if (response.Success && response.Data) {
                self.history = response.Data.history || [];
                self.renderHistory();
            }
        } catch (error) {
            console.error('Failed to load history:', error);
            $('#history-tbody').html(`
                <tr>
                    <td colspan="6" class="text-center text-danger py-4">
                        <i class="fa-solid fa-exclamation-triangle me-2"></i>
                        Failed to load history
                    </td>
                </tr>
            `);
        }
    },

    renderPendingChanges: function () {
        var self = this;
        var changes = this.changes;
        var container = $('#pending-changes-list');

        if (!changes || changes.length === 0) {
            container.html(`
                <div class="text-center py-5 text-muted">
                    <i class="fa-solid fa-check-circle fa-3x mb-3 text-success"></i>
                    <h5>No Pending Changes</h5>
                    <p class="mb-0">All configuration changes have been applied.</p>
                </div>
            `);
            return;
        }

        // Group by category
        var grouped = this.groupByCategory(changes);

        var html = '<div class="accordion" id="pending-accordion">';
        var index = 0;

        for (var category in grouped) {
            var categoryChanges = grouped[category];
            var categoryId = 'category-' + index;

            html += `
                <div class="accordion-item">
                    <h2 class="accordion-header">
                        <button class="accordion-button" type="button" data-bs-toggle="collapse" data-bs-target="#${categoryId}">
                            ${this.getCategoryIcon(category)}
                            <span class="ms-2">${category}</span>
                            <span class="badge bg-warning text-dark ms-2">${categoryChanges.length}</span>
                        </button>
                    </h2>
                    <div id="${categoryId}" class="accordion-collapse collapse show">
                        <div class="accordion-body p-0">
                            <div class="list-group list-group-flush">
            `;

            categoryChanges.forEach(function (change) {
                // Handle both PascalCase (C#) and camelCase (JS) property names
                var description = change.Description || change.description || 'Configuration change';
                var targetKey = change.TargetKey || change.targetKey || '';
                var createdAt = change.CreatedAt || change.createdAt;
                var createdBy = change.CreatedBy || change.createdBy;
                var requiresRestart = change.RequiresRestart || change.requiresRestart;
                var requiresReboot = change.RequiresReboot || change.requiresReboot;
                var changeId = change.Id || change.id;

                var badges = '';
                if (requiresRestart) {
                    badges += '<span class="badge bg-info ms-1" title="Requires service restart"><i class="fa-solid fa-sync"></i></span>';
                }
                if (requiresReboot) {
                    badges += '<span class="badge bg-danger ms-1" title="Requires system reboot"><i class="fa-solid fa-power-off"></i></span>';
                }

                html += `
                    <div class="list-group-item list-group-item-action d-flex justify-content-between align-items-center">
                        <div class="pending-change-detail">
                            <div class="d-flex align-items-center">
                                <strong>${self.escapeHtml(description)}</strong>
                                ${badges}
                            </div>
                            <small class="text-muted">
                                <span class="me-3"><i class="fa-solid fa-key me-1"></i>${self.escapeHtml(targetKey)}</span>
                                <span class="me-3"><i class="fa-solid fa-clock me-1"></i>${self.formatTime(createdAt)}</span>
                                ${createdBy ? '<span><i class="fa-solid fa-user me-1"></i>' + self.escapeHtml(createdBy) + '</span>' : ''}
                            </small>
                        </div>
                        <div class="btn-group">
                            <button class="btn btn-sm btn-outline-primary btn-view-change" data-id="${changeId}" title="View Details">
                                <i class="fa-solid fa-eye"></i>
                            </button>
                            <button class="btn btn-sm btn-success btn-apply-single" data-id="${changeId}" title="Apply">
                                <i class="fa-solid fa-check"></i>
                            </button>
                            <button class="btn btn-sm btn-outline-danger btn-discard-single" data-id="${changeId}" title="Discard">
                                <i class="fa-solid fa-times"></i>
                            </button>
                        </div>
                    </div>
                `;
            });

            html += `
                            </div>
                        </div>
                    </div>
                </div>
            `;
            index++;
        }

        html += '</div>';
        container.html(html);
    },

    renderHistory: function () {
        var self = this;
        var history = this.history;
        var tbody = $('#history-tbody');

        if (!history || history.length === 0) {
            tbody.html(`
                <tr>
                    <td colspan="6" class="text-center py-4 text-muted">
                        <i class="fa-solid fa-inbox me-2"></i>No history records found
                    </td>
                </tr>
            `);
            return;
        }

        var html = '';
        history.forEach(function (entry) {
            // Handle both PascalCase (C#) and camelCase (JS) property names
            var action = entry.Action || entry.action || '';
            var changedAt = entry.ChangedAt || entry.changedAt;
            var configType = entry.ConfigType || entry.configType || '';
            var configKey = entry.ConfigKey || entry.configKey || '';
            var changedBy = entry.ChangedBy || entry.changedBy;
            var changeSource = entry.ChangeSource || entry.changeSource || 'webui';

            var actionBadge = self.getActionBadge(action);

            html += `
                <tr>
                    <td><small>${self.formatDateTime(changedAt)}</small></td>
                    <td><span class="badge bg-secondary">${self.escapeHtml(configType)}</span></td>
                    <td><code>${self.escapeHtml(configKey)}</code></td>
                    <td>${actionBadge}</td>
                    <td>${changedBy ? self.escapeHtml(changedBy) : '<span class="text-muted">-</span>'}</td>
                    <td><span class="badge bg-outline-secondary border">${self.escapeHtml(changeSource)}</span></td>
                </tr>
            `;
        });

        tbody.html(html);
    },

    updateStats: function () {
        var changes = this.changes;
        var pendingCount = changes.length;
        // Handle both PascalCase and camelCase
        var restartCount = changes.filter(function (c) { return c.RequiresRestart || c.requiresRestart; }).length;
        var rebootCount = changes.filter(function (c) { return c.RequiresReboot || c.requiresReboot; }).length;

        $('#pending-count').text(pendingCount);
        $('#pending-badge').text(pendingCount);
        $('#restart-count').text(restartCount);
        $('#reboot-count').text(rebootCount);

        // Enable/disable buttons
        var hasChanges = pendingCount > 0;
        $('#btn-apply-all').prop('disabled', !hasChanges);
        $('#btn-discard-all').prop('disabled', !hasChanges);

        // Update navbar indicator if Monolith.PendingChanges exists
        if (window.Monolith && Monolith.PendingChanges) {
            Monolith.PendingChanges.updateBadge(pendingCount);
        }
    },

    applyAll: async function () {
        if (!confirm('Apply all pending changes? This may affect system connectivity.')) {
            return;
        }

        var self = this;
        Monolith.UI.showLoading('#pending-changes-list');

        try {
            var response = await Monolith.API.post('/api/core', {
                action: 'config.apply-all',
                appliedBy: Monolith.Auth ? Monolith.Auth.getUsername() : 'admin'
            });

            if (response.Success && response.Data) {
                var data = response.Data;
                Monolith.UI.toast(
                    'Applied ' + (data.AppliedCount || data.appliedCount || 0) + ' changes successfully' +
                    (data.FailedCount || data.failedCount ? ', ' + (data.FailedCount || data.failedCount) + ' failed' : ''),
                    data.FailedCount || data.failedCount ? 'warning' : 'success'
                );

                if (data.RequiresRestart || data.requiresRestart) {
                    self.showRestartPrompt();
                } else if (data.RequiresReboot || data.requiresReboot) {
                    self.showRebootPrompt();
                }

                self.loadPendingChanges();
                self.loadHistory();
            } else {
                Monolith.UI.toast(response.Error || 'Failed to apply changes', 'error');
                self.loadPendingChanges();
            }
        } catch (error) {
            Monolith.UI.toast('Failed to apply changes: ' + error.message, 'error');
            self.loadPendingChanges();
        }
    },

    applySingle: async function (id) {
        var self = this;

        try {
            var response = await Monolith.API.post('/api/core', {
                action: 'config.apply',
                changeId: id,
                appliedBy: Monolith.Auth ? Monolith.Auth.getUsername() : 'admin'
            });

            if (response.Success) {
                Monolith.UI.toast('Change applied successfully', 'success');
                self.loadPendingChanges();
                self.loadHistory();
            } else {
                Monolith.UI.toast(response.Error || 'Failed to apply change', 'error');
            }
        } catch (error) {
            Monolith.UI.toast('Failed to apply change: ' + error.message, 'error');
        }
    },

    discardAll: async function () {
        if (!confirm('Discard all pending changes? This cannot be undone.')) {
            return;
        }

        var self = this;

        try {
            var response = await Monolith.API.post('/api/core', {
                action: 'config.pending.discard-all'
            });

            if (response.Success) {
                Monolith.UI.toast('All changes discarded', 'info');
                self.loadPendingChanges();
            } else {
                Monolith.UI.toast(response.Error || 'Failed to discard changes', 'error');
            }
        } catch (error) {
            Monolith.UI.toast('Failed to discard changes: ' + error.message, 'error');
        }
    },

    discardSingle: async function (id) {
        var self = this;

        try {
            var response = await Monolith.API.post('/api/core', {
                action: 'config.pending.discard',
                changeId: id
            });

            if (response.Success) {
                Monolith.UI.toast('Change discarded', 'info');
                self.loadPendingChanges();
            } else {
                Monolith.UI.toast(response.Error || 'Failed to discard change', 'error');
            }
        } catch (error) {
            Monolith.UI.toast('Failed to discard change: ' + error.message, 'error');
        }
    },

    viewChangeDetails: function (id) {
        var change = this.changes.find(function (c) { return (c.Id || c.id) === id; });
        if (!change) return;

        // Handle both PascalCase (C#) and camelCase (JS) property names
        var description = change.Description || change.description || 'Configuration change';
        var targetKey = change.TargetKey || change.targetKey || '';
        var changeType = change.ChangeType || change.changeType || '';
        var createdAt = change.CreatedAt || change.createdAt;
        var createdBy = change.CreatedBy || change.createdBy;
        var requiresRestart = change.RequiresRestart || change.requiresRestart;
        var requiresReboot = change.RequiresReboot || change.requiresReboot;

        var modal = new bootstrap.Modal(document.getElementById('validation-modal'));
        var body = `
            <div class="mb-3">
                <strong>Description:</strong> ${this.escapeHtml(description)}
            </div>
            <div class="mb-3">
                <strong>Target:</strong> <code>${this.escapeHtml(targetKey)}</code>
            </div>
            <div class="mb-3">
                <strong>Type:</strong> ${this.escapeHtml(changeType)}
            </div>
            <div class="mb-3">
                <strong>Created:</strong> ${this.formatDateTime(createdAt)}
                ${createdBy ? ' by ' + this.escapeHtml(createdBy) : ''}
            </div>
            ${requiresRestart ? '<div class="alert alert-info py-2"><i class="fa-solid fa-sync me-2"></i>Requires service restart</div>' : ''}
            ${requiresReboot ? '<div class="alert alert-danger py-2"><i class="fa-solid fa-power-off me-2"></i>Requires system reboot</div>' : ''}
        `;

        $('#validation-body').html(body);
        $('#validation-modal .modal-title').html('<i class="fa-solid fa-info-circle me-2"></i>Change Details');
        $('#btn-apply-after-validate').hide();
        modal.show();
    },

    showRestartPrompt: function () {
        Monolith.UI.confirm(
            'Some changes require a service restart to take effect. Restart now?',
            async function () {
                try {
                    await Monolith.API.post('/api/core', { action: 'system.restart-services' });
                    Monolith.UI.toast('Services restarting...', 'info');
                } catch (error) {
                    Monolith.UI.toast('Failed to restart services', 'error');
                }
            }
        );
    },

    showRebootPrompt: function () {
        Monolith.UI.confirm(
            'Some changes require a system reboot to take effect. Reboot now?',
            async function () {
                try {
                    await Monolith.API.post('/api/core', { action: 'system.reboot' });
                    Monolith.UI.toast('System rebooting...', 'warning');
                } catch (error) {
                    Monolith.UI.toast('Failed to reboot system', 'error');
                }
            }
        );
    },

    groupByCategory: function (changes) {
        var self = this;
        return changes.reduce(function (groups, change) {
            // Handle both PascalCase (C#) and camelCase (JS) property names
            var category = change.TargetCategory || change.targetCategory || 'Other';
            // Capitalize first letter for display
            category = self.formatCategoryName(category);
            if (!groups[category]) groups[category] = [];
            groups[category].push(change);
            return groups;
        }, {});
    },

    formatCategoryName: function (category) {
        if (!category) return 'Other';
        // Map common category values to display names
        var categoryMap = {
            'system': 'System',
            'network': 'Network',
            'webui': 'Web UI',
            'firewall': 'Firewall',
            'modules': 'Modules',
            'other': 'Other'
        };
        var lower = category.toLowerCase();
        return categoryMap[lower] || category.charAt(0).toUpperCase() + category.slice(1);
    },

    getCategoryIcon: function (category) {
        var icons = {
            'network': '<i class="fa-solid fa-network-wired"></i>',
            'Network': '<i class="fa-solid fa-network-wired"></i>',
            'system': '<i class="fa-solid fa-server"></i>',
            'System': '<i class="fa-solid fa-server"></i>',
            'firewall': '<i class="fa-solid fa-shield-halved"></i>',
            'Firewall': '<i class="fa-solid fa-shield-halved"></i>',
            'webui': '<i class="fa-solid fa-desktop"></i>',
            'WebUI': '<i class="fa-solid fa-desktop"></i>',
            'modules': '<i class="fa-solid fa-puzzle-piece"></i>',
            'Modules': '<i class="fa-solid fa-puzzle-piece"></i>'
        };
        return icons[category] || '<i class="fa-solid fa-cog"></i>';
    },

    getActionBadge: function (action) {
        var badges = {
            'created': '<span class="badge bg-success">Created</span>',
            'updated': '<span class="badge bg-primary">Updated</span>',
            'deleted': '<span class="badge bg-danger">Deleted</span>',
            'applied': '<span class="badge bg-success">Applied</span>',
            'failed': '<span class="badge bg-danger">Failed</span>',
            'rolled_back': '<span class="badge bg-warning text-dark">Rolled Back</span>'
        };
        return badges[action] || '<span class="badge bg-secondary">' + action + '</span>';
    },

    escapeHtml: function (text) {
        if (!text) return '';
        var div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    },

    formatTime: function (timestamp) {
        if (!timestamp) return '';
        var date = new Date(timestamp);
        var now = new Date();
        var diff = now - date;

        if (diff < 60000) return 'Just now';
        if (diff < 3600000) return Math.floor(diff / 60000) + ' min ago';
        if (diff < 86400000) return Math.floor(diff / 3600000) + ' hours ago';
        return date.toLocaleDateString();
    },

    formatDateTime: function (timestamp) {
        if (!timestamp) return '';
        var date = new Date(timestamp);
        return date.toLocaleString();
    },

    renderError: function (message) {
        $('#pending-changes-list').html(`
            <div class="text-center py-5 text-danger">
                <i class="fa-solid fa-exclamation-triangle fa-3x mb-3"></i>
                <h5>Error</h5>
                <p class="mb-0">${this.escapeHtml(message)}</p>
            </div>
        `);
    },

    destroy: function () {
        this.stopPolling();
    }
};

// Register with CMS Router - expose as expected module names
window.Monolith = window.Monolith || {};
window.Monolith.Pages = window.Monolith.Pages || {};
window.Monolith.Pages.PendingChanges = Monolith.PendingChangesPage;
window.PendingChanges = Monolith.PendingChangesPage;

// Initialize on page load (fallback for non-CMS pages)
$(document).ready(function () {
    // Only auto-init if CMS router is not present
    if ($('#pending-changes-container').length && !window.CmsRouter) {
        Monolith.PendingChangesPage.init();
    }
});

// Cleanup on page unload
$(window).on('beforeunload', function () {
    if (Monolith.PendingChangesPage) {
        Monolith.PendingChangesPage.destroy();
    }
});
