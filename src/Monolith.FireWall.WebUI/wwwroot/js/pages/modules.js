// Module Manager Page
// Ensure Monolith.Core exists for API calls
if (!window.Monolith) window.Monolith = {};
if (!Monolith.Core) {
    Monolith.Core = {
        call: async function(action, payload) {
            try {
                var requestBody = { action: action };
                if (payload && Object.keys(payload).length > 0) {
                    requestBody.payload = payload;
                }
                var response = await Monolith.API.post('/api/core', requestBody);
                return {
                    success: response.success || response.Success || false,
                    data: response.data || response.Data || null,
                    error: response.error || response.Error || null
                };
            } catch (error) {
                console.error('Core API error:', error);
                return { success: false, data: null, error: error.message };
            }
        }
    };
}

var Modules = {
    modules: [],
    services: [],
    filtered: [],
    _signalRSubscribed: false,

    init: function() {
        console.log('Initializing Module Manager...');
        this.render();
        this.loadData();
        this._subscribeToSignalR();
    },

    destroy: function() {
        this._unsubscribeFromSignalR();
    },

    _subscribeToSignalR: function() {
        if (this._signalRSubscribed || !Monolith.SignalR) return;

        const self = this;
        Monolith.SignalR.subscribe('services', function(event, data) {
            if (event === 'ServiceStatusChanged') {
                self._handleServiceStatusChanged(data);
            }
        });

        this._signalRSubscribed = true;
        console.log('Modules: SignalR subscribed');
    },

    _unsubscribeFromSignalR: function() {
        if (!this._signalRSubscribed || !Monolith.SignalR) return;

        Monolith.SignalR.unsubscribe('services');
        this._signalRSubscribed = false;
        console.log('Modules: SignalR unsubscribed');
    },

    _handleServiceStatusChanged: function(data) {
        const moduleId = data.moduleId;
        if (!moduleId) return;

        const service = this.services.find(s => s.moduleId === moduleId);
        if (service) {
            service.isRunning = data.status === 'running';
            service.activeState = data.status;
        }

        const card = $(`.module-card[data-module-id="${moduleId}"]`);
        if (!card.length) return;

        const statusEl = card.find('.module-service-status');
        if (statusEl.length) {
            statusEl.html(this._formatServiceStatusBadge(data.status === 'running', data.status));
            card.addClass('border-primary');
            setTimeout(() => card.removeClass('border-primary'), 1000);
        }

        console.log(`Module service status updated: ${moduleId} - ${data.status}`);
    },

    render: function() {
        const container = $('#modules-container');

        if (Monolith.PageHeader && typeof Monolith.PageHeader.render === 'function') {
            Monolith.PageHeader.render({
                title: "Module Manager",
                icon: "fa-puzzle-piece",
                description: "Enable, disable, and manage module services",
                container: container,
                prepend: true
            });
        }

        container.append(`
            <style>
                .module-card {
                    transition: border-color 0.3s ease, box-shadow 0.3s ease;
                }
                .module-card:hover {
                    box-shadow: 0 4px 12px rgba(0,0,0,0.1);
                }
                .module-card .module-header {
                    border-bottom: 1px solid rgba(0,0,0,0.08);
                }
                .module-card .service-section {
                    background: rgba(0,0,0,0.02);
                    border-radius: 8px;
                }
                .module-card .binding-tag {
                    font-family: monospace;
                    font-size: 0.8rem;
                    background: #f8f9fa;
                    border: 1px solid #dee2e6;
                    border-radius: 4px;
                    padding: 2px 8px;
                    display: inline-block;
                    margin: 2px;
                }
                .module-card .permission-tag {
                    font-size: 0.75rem;
                    background: #fff3cd;
                    border: 1px solid #ffc107;
                    color: #856404;
                    border-radius: 4px;
                    padding: 2px 6px;
                    display: inline-block;
                    margin: 2px;
                }
                .service-controls .btn {
                    padding: 0.375rem 0.75rem;
                }
                .module-toggle-wrapper {
                    min-width: 80px;
                }
            </style>

            <div class="container-fluid p-4 modules-shell">
                <div class="d-flex justify-content-between align-items-center mb-4">
                    <div class="d-flex gap-3 flex-grow-1" style="max-width: 600px;">
                        <div class="input-group">
                            <span class="input-group-text">
                                <i class="fa fa-search"></i>
                            </span>
                            <input type="text" class="form-control" id="module-search" placeholder="Search modules...">
                        </div>
                        <select class="form-select" id="module-package-filter" style="max-width: 200px;">
                            <option value="">All Packages</option>
                        </select>
                    </div>
                    <div class="d-flex align-items-center gap-3">
                        <span class="text-muted" id="modules-count">0 modules</span>
                        <button class="btn btn-outline-primary" id="modules-refresh">
                            <i class="fa fa-refresh me-1"></i> Refresh
                        </button>
                    </div>
                </div>

                <div id="modules-grid" class="row g-4">
                    <div class="col-12 text-center text-muted py-5">
                        <div class="spinner-border text-primary mb-3" role="status"></div>
                        <div>Loading modules...</div>
                    </div>
                </div>
            </div>

            <!-- Service Logs Modal -->
            <div class="modal fade" id="service-logs-modal" tabindex="-1">
                <div class="modal-dialog modal-xl">
                    <div class="modal-content">
                        <div class="modal-header">
                            <h5 class="modal-title">Service Logs</h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                        </div>
                        <div class="modal-body">
                            <div class="d-flex gap-2 mb-3">
                                <select class="form-select form-select-sm" id="logs-priority-filter" style="width: auto;">
                                    <option value="">All Priorities</option>
                                    <option value="err">Error</option>
                                    <option value="warning">Warning</option>
                                    <option value="info">Info</option>
                                    <option value="debug">Debug</option>
                                </select>
                                <select class="form-select form-select-sm" id="logs-limit" style="width: auto;">
                                    <option value="50">Last 50</option>
                                    <option value="100" selected>Last 100</option>
                                    <option value="200">Last 200</option>
                                    <option value="500">Last 500</option>
                                </select>
                                <button class="btn btn-sm btn-outline-primary" id="logs-refresh-btn">
                                    <i class="fa fa-refresh"></i> Refresh
                                </button>
                            </div>
                            <div id="service-logs-content" style="max-height: 60vh; overflow-y: auto;">
                                <div class="text-center text-muted py-4">Select a service to view logs</div>
                            </div>
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Close</button>
                        </div>
                    </div>
                </div>
            </div>

            <!-- Confirmation Modal -->
            <div class="modal fade" id="service-confirm-modal" tabindex="-1">
                <div class="modal-dialog">
                    <div class="modal-content">
                        <div class="modal-header">
                            <h5 class="modal-title" id="confirm-modal-title">Confirm Action</h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                        </div>
                        <div class="modal-body" id="confirm-modal-body">
                            Are you sure?
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
                            <button type="button" class="btn btn-danger" id="confirm-modal-action">Confirm</button>
                        </div>
                    </div>
                </div>
            </div>
        `);

        $('#modules-refresh').off('click').on('click', () => this.loadData());
        $('#module-search').off('input').on('input', () => this.applyFilters());
        $('#module-package-filter').off('change').on('change', () => this.applyFilters());
    },

    loadData: async function() {
        try {
            const [modulesResponse, servicesResponse] = await Promise.all([
                Monolith.API.get('/core?action=get-modules'),
                Monolith.Core.call('module.services.list')
            ]);

            if (modulesResponse.Success || modulesResponse.success) {
                this.modules = modulesResponse.Data || modulesResponse.data || [];
            } else {
                this.renderError('Failed to load modules');
                return;
            }

            if (servicesResponse.success) {
                this.services = servicesResponse.data || [];
            } else {
                this.services = [];
            }

            this.populatePackageFilter();
            this.applyFilters();
        } catch (error) {
            console.error('Error loading data:', error);
            this.renderError('Failed to load modules');
        }
    },

    populatePackageFilter: function() {
        const select = $('#module-package-filter');
        const packages = [...new Set(this.modules.map(m => m.packageName || m.packageId))].sort();
        select.find('option:not(:first)').remove();
        packages.forEach(pkg => {
            select.append(`<option value="${pkg}">${pkg}</option>`);
        });
    },

    applyFilters: function() {
        const query = ($('#module-search').val() || '').toString().toLowerCase();
        const packageFilter = $('#module-package-filter').val();

        this.filtered = this.modules.filter(module => {
            const name = (module.name || module.id || '').toLowerCase();
            const pkg = (module.packageName || module.packageId || '').toLowerCase();
            const matchesQuery = !query || name.includes(query) || pkg.includes(query);
            const matchesPackage = !packageFilter || (module.packageName || module.packageId) === packageFilter;
            return matchesQuery && matchesPackage;
        });

        this.renderModules();
    },

    _getModuleServices: function(moduleId) {
        return this.services.filter(s => s.moduleId === moduleId);
    },

    _formatServiceStatusBadge: function(isRunning, activeState) {
        if (activeState === 'failed') {
            return '<span class="badge bg-danger"><i class="fa fa-times-circle me-1"></i>Failed</span>';
        } else if (activeState === 'activating') {
            return '<span class="badge bg-warning text-dark"><i class="fa fa-spinner fa-spin me-1"></i>Starting</span>';
        } else if (isRunning) {
            return '<span class="badge bg-success"><i class="fa fa-check-circle me-1"></i>Running</span>';
        } else {
            return '<span class="badge bg-secondary"><i class="fa fa-stop-circle me-1"></i>Stopped</span>';
        }
    },

    _formatBindings: function(bindings) {
        if (!bindings || bindings.length === 0) {
            return '<span class="text-muted">No bindings configured</span>';
        }

        return bindings.map(b => {
            const port = b.port ? `:${b.port}` : '';
            const protocol = b.protocol ? `/${b.protocol}` : '';
            const iface = b.interface || '*';
            const ip = b.ip || '*';
            return `<span class="binding-tag">${iface} ${ip}${port}${protocol}</span>`;
        }).join('');
    },

    _formatPermissions: function(systemPermissions) {
        if (!systemPermissions || systemPermissions.length === 0) {
            return '<span class="text-muted small">No special permissions required</span>';
        }

        return systemPermissions.map(p => {
            const type = (p.type || '').replace('File', '').replace('Network', '').replace('Command', '');
            const icon = p.type?.includes('File') ? 'fa-file' :
                        p.type?.includes('Network') ? 'fa-globe' :
                        p.type?.includes('Command') ? 'fa-terminal' : 'fa-lock';
            return `<span class="permission-tag"><i class="fa ${icon} me-1"></i>${p.resource}</span>`;
        }).join('');
    },

    renderModules: function() {
        const grid = $('#modules-grid');
        const countEl = $('#modules-count');
        countEl.text(`${this.filtered.length} module${this.filtered.length === 1 ? '' : 's'}`);

        if (this.filtered.length === 0) {
            grid.html(`
                <div class="col-12 text-center text-muted py-5">
                    <i class="fa fa-puzzle-piece fa-3x mb-3 opacity-50"></i>
                    <div>No modules found</div>
                </div>
            `);
            return;
        }

        let html = '';
        this.filtered.forEach(module => {
            const moduleServices = this._getModuleServices(module.id);
            const hasServices = moduleServices.length > 0;
            const service = hasServices ? moduleServices[0] : null;

            html += `
                <div class="col-12 col-lg-6 col-xl-4">
                    <div class="card module-card h-100" data-module-id="${module.id}" data-package-id="${module.packageId}">
                        <!-- Module Header -->
                        <div class="card-header module-header bg-transparent py-3">
                            <div class="d-flex justify-content-between align-items-start">
                                <div class="flex-grow-1">
                                    <h5 class="mb-1 fw-semibold">${module.name || module.id}</h5>
                                    <div class="d-flex align-items-center gap-2">
                                        <span class="badge bg-light text-dark border">${module.packageName || module.packageId}</span>
                                        <code class="text-muted small">${module.id}</code>
                                    </div>
                                </div>
                                <div class="module-toggle-wrapper text-end">
                                    <div class="form-check form-switch">
                                        <input class="form-check-input module-toggle" type="checkbox" role="switch"
                                            data-package="${module.packageId}"
                                            data-module="${module.id}"
                                            ${module.enabled ? 'checked' : ''}
                                            id="toggle-${module.id}">
                                        <label class="form-check-label small fw-medium ${module.enabled ? 'text-success' : 'text-muted'}"
                                            for="toggle-${module.id}">
                                            ${module.enabled ? 'Enabled' : 'Disabled'}
                                        </label>
                                    </div>
                                </div>
                            </div>
                        </div>

                        <div class="card-body">
                            ${hasServices ? `
                                <!-- Service Section -->
                                <div class="service-section p-3 mb-3">
                                    <div class="d-flex justify-content-between align-items-center mb-2">
                                        <div>
                                            <div class="fw-medium mb-1">${service.name}</div>
                                            <code class="text-muted small">${service.systemdUnit}</code>
                                        </div>
                                        <div class="module-service-status">
                                            ${this._formatServiceStatusBadge(service.isRunning, service.activeState)}
                                        </div>
                                    </div>

                                    <div class="d-flex gap-2 mt-3 service-controls">
                                        ${service.isRunning ? `
                                            <button class="btn btn-sm btn-outline-warning service-restart-btn flex-grow-1"
                                                data-unit="${service.systemdUnit}" data-name="${service.name}">
                                                <i class="fa fa-refresh me-1"></i> Restart
                                            </button>
                                            <button class="btn btn-sm btn-outline-danger service-stop-btn flex-grow-1"
                                                data-unit="${service.systemdUnit}" data-name="${service.name}">
                                                <i class="fa fa-stop me-1"></i> Stop
                                            </button>
                                        ` : `
                                            <button class="btn btn-sm btn-outline-success service-start-btn flex-grow-1"
                                                data-unit="${service.systemdUnit}" data-name="${service.name}">
                                                <i class="fa fa-play me-1"></i> Start
                                            </button>
                                        `}
                                        <button class="btn btn-sm btn-outline-secondary service-logs-btn"
                                            data-unit="${service.systemdUnit}" data-name="${service.name}"
                                            title="View Logs">
                                            <i class="fa fa-file-text-o"></i>
                                        </button>
                                    </div>
                                </div>

                                <!-- Bindings -->
                                <div class="mb-3">
                                    <div class="text-muted small mb-2">
                                        <i class="fa fa-plug me-1"></i> Bindings
                                    </div>
                                    <div class="bindings-list">
                                        ${this._formatBindings(service.bindings)}
                                    </div>
                                </div>
                            ` : `
                                <div class="text-muted text-center py-3 mb-3">
                                    <i class="fa fa-info-circle me-1"></i>
                                    No services associated with this module
                                </div>
                            `}

                            <!-- Permissions -->
                            <div>
                                <div class="text-muted small mb-2">
                                    <i class="fa fa-shield me-1"></i> System Permissions
                                </div>
                                <div class="permissions-list">
                                    ${this._formatPermissions(module.systemPermissions)}
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            `;
        });

        grid.html(html);
        this.bindHandlers();
    },

    bindHandlers: function() {
        const self = this;

        $('.module-toggle').off('change').on('change', async function(e) {
            const toggle = $(this);
            const packageId = toggle.data('package');
            const moduleId = toggle.data('module');
            const enabled = toggle.is(':checked');
            const label = toggle.next('label');

            toggle.prop('disabled', true);
            try {
                const response = await Monolith.API.post('/modules/state', {
                    packageId,
                    moduleId,
                    enabled
                });

                if (!(response.Success || response.success)) {
                    throw new Error(response.Error || response.error || 'Update failed');
                }

                const module = self.modules.find(m => m.packageId === packageId && m.id === moduleId);
                if (module) {
                    module.enabled = enabled;
                }

                label.text(enabled ? 'Enabled' : 'Disabled')
                    .removeClass('text-success text-muted')
                    .addClass(enabled ? 'text-success' : 'text-muted');

                const data = response.Data || response.data;
                if (data && data.serviceResults && data.serviceResults.length > 0) {
                    const failed = data.serviceResults.filter(r => !r.success);
                    if (failed.length > 0) {
                        Monolith.UI.toast(`Module ${enabled ? 'enabled' : 'disabled'}, but some services failed`, 'warning');
                    } else {
                        Monolith.UI.toast(`Module ${enabled ? 'enabled' : 'disabled'} and services ${enabled ? 'started' : 'stopped'}`, 'success');
                    }
                } else {
                    Monolith.UI.toast(`Module ${enabled ? 'enabled' : 'disabled'}`, 'success');
                }

                await self.loadData();
            } catch (error) {
                console.error('Error updating module state:', error);
                Monolith.UI.toast('Failed to update module state', 'error');
                toggle.prop('checked', !enabled);
            } finally {
                toggle.prop('disabled', false);
            }
        });

        $('.service-start-btn').off('click').on('click', function() {
            const unit = $(this).data('unit');
            const name = $(this).data('name');
            self._showConfirmModal(
                'Start Service',
                `Are you sure you want to start <strong>${name}</strong>?`,
                'btn-success',
                'Start Service',
                () => self._startService(unit, name)
            );
        });

        $('.service-stop-btn').off('click').on('click', function() {
            const unit = $(this).data('unit');
            const name = $(this).data('name');
            self._showConfirmModal(
                'Stop Service',
                `Are you sure you want to stop <strong>${name}</strong>?<br><br><small class="text-muted">This may disconnect clients using this service until it is restarted.</small>`,
                'btn-danger',
                'Stop Service',
                () => self._stopService(unit, name)
            );
        });

        $('.service-restart-btn').off('click').on('click', function() {
            const unit = $(this).data('unit');
            const name = $(this).data('name');
            self._showConfirmModal(
                'Restart Service',
                `Are you sure you want to restart <strong>${name}</strong>?<br><br><small class="text-muted">This may briefly interrupt clients using this service.</small>`,
                'btn-warning',
                'Restart Service',
                () => self._restartService(unit, name)
            );
        });

        $('.service-logs-btn').off('click').on('click', function() {
            const unit = $(this).data('unit');
            const name = $(this).data('name');
            self._showLogsModal(unit, name);
        });
    },

    _showConfirmModal: function(title, message, btnClass, btnText, onConfirm) {
        $('#confirm-modal-title').text(title);
        $('#confirm-modal-body').html(message);
        $('#confirm-modal-action')
            .removeClass('btn-danger btn-warning btn-success btn-primary')
            .addClass(btnClass)
            .text(btnText)
            .off('click')
            .on('click', async function() {
                $(this).prop('disabled', true);
                try {
                    await onConfirm();
                } finally {
                    $(this).prop('disabled', false);
                    bootstrap.Modal.getInstance($('#service-confirm-modal')[0])?.hide();
                }
            });

        new bootstrap.Modal($('#service-confirm-modal')[0]).show();
    },

    _startService: async function(unit, name) {
        try {
            const response = await Monolith.Core.call('module.services.start', { systemdUnit: unit });
            if (response.success) {
                Monolith.UI.toast(`${name} started successfully`, 'success');
                await this.loadData();
            } else {
                Monolith.UI.toast(`Failed to start ${name}: ${response.error}`, 'error');
            }
        } catch (error) {
            Monolith.UI.toast(`Failed to start ${name}: ${error.message}`, 'error');
        }
    },

    _stopService: async function(unit, name) {
        try {
            const response = await Monolith.Core.call('module.services.stop', { systemdUnit: unit });
            if (response.success) {
                Monolith.UI.toast(`${name} stopped successfully`, 'success');
                await this.loadData();
            } else {
                Monolith.UI.toast(`Failed to stop ${name}: ${response.error}`, 'error');
            }
        } catch (error) {
            Monolith.UI.toast(`Failed to stop ${name}: ${error.message}`, 'error');
        }
    },

    _restartService: async function(unit, name) {
        try {
            const response = await Monolith.Core.call('module.services.restart', { systemdUnit: unit });
            if (response.success) {
                Monolith.UI.toast(`${name} restarted successfully`, 'success');
                await this.loadData();
            } else {
                Monolith.UI.toast(`Failed to restart ${name}: ${response.error}`, 'error');
            }
        } catch (error) {
            Monolith.UI.toast(`Failed to restart ${name}: ${error.message}`, 'error');
        }
    },

    _currentLogsUnit: null,
    _currentLogsName: null,

    _showLogsModal: function(unit, name) {
        this._currentLogsUnit = unit;
        this._currentLogsName = name;

        $('#service-logs-modal .modal-title').text(`Service Logs: ${name}`);
        $('#service-logs-content').html('<div class="text-center py-4"><div class="spinner-border text-primary" role="status"></div></div>');

        const self = this;
        $('#logs-refresh-btn').off('click').on('click', () => self._loadLogs());
        $('#logs-priority-filter').off('change').on('change', () => self._loadLogs());
        $('#logs-limit').off('change').on('change', () => self._loadLogs());

        new bootstrap.Modal($('#service-logs-modal')[0]).show();
        this._loadLogs();
    },

    _loadLogs: async function() {
        if (!this._currentLogsUnit) return;

        const priority = $('#logs-priority-filter').val();
        const limit = parseInt($('#logs-limit').val()) || 100;

        try {
            const response = await Monolith.Core.call('module.services.logs', {
                systemdUnit: this._currentLogsUnit,
                limit: limit,
                priority: priority || undefined
            });

            if (!response.success) {
                $('#service-logs-content').html(`<div class="alert alert-danger">${response.error || 'Failed to load logs'}</div>`);
                return;
            }

            const logs = response.data?.logs || [];
            if (logs.length === 0) {
                $('#service-logs-content').html('<div class="text-center text-muted py-4">No logs found</div>');
                return;
            }

            let html = '<div class="log-entries font-monospace small">';
            logs.forEach(log => {
                const priorityClass = this._getPriorityClass(log.priority);
                const timestamp = new Date(log.timestamp).toLocaleString();
                html += `
                    <div class="log-entry p-2 border-bottom ${priorityClass}">
                        <span class="text-muted">${timestamp}</span>
                        <span class="badge ${this._getPriorityBadgeClass(log.priority)} ms-2">${log.priority}</span>
                        ${log.identifier ? `<span class="text-info ms-2">[${log.identifier}]</span>` : ''}
                        <div class="log-message mt-1">${this._escapeHtml(log.message)}</div>
                    </div>
                `;
            });
            html += '</div>';

            $('#service-logs-content').html(html);
        } catch (error) {
            $('#service-logs-content').html(`<div class="alert alert-danger">${error.message}</div>`);
        }
    },

    _getPriorityClass: function(priority) {
        switch (priority) {
            case 'emerg':
            case 'alert':
            case 'crit':
            case 'err':
                return 'bg-danger bg-opacity-10';
            case 'warning':
                return 'bg-warning bg-opacity-10';
            default:
                return '';
        }
    },

    _getPriorityBadgeClass: function(priority) {
        switch (priority) {
            case 'emerg':
            case 'alert':
            case 'crit':
            case 'err':
                return 'bg-danger';
            case 'warning':
                return 'bg-warning text-dark';
            case 'notice':
            case 'info':
                return 'bg-info text-dark';
            case 'debug':
                return 'bg-secondary';
            default:
                return 'bg-light text-dark';
        }
    },

    _escapeHtml: function(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    },

    renderError: function(message) {
        $('#modules-grid').html(`
            <div class="col-12 text-center text-danger py-5">
                <i class="fa fa-exclamation-triangle fa-3x mb-3"></i>
                <div>${message}</div>
            </div>
        `);
    }
};

if (typeof Monolith !== 'undefined') {
    Monolith.Pages = Monolith.Pages || {};
    Monolith.Pages.Modules = Modules;
}
