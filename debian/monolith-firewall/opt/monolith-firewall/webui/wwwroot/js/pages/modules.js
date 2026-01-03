// Module Manager Page
var Modules = {
    modules: [],
    filtered: [],

    init: function() {
        console.log('Initializing Module Manager...');
        this.render();
        this.loadModules();
    },

    render: function() {
        const container = $('#modules-container');
        container.html(`
            <div class="container-fluid modules-shell">
                <div class="modules-hero">
                    <div>
                        <h1 class="mb-1">Module Manager</h1>
                        <p class="text-muted mb-0">Enable, disable, and audit module permissions.</p>
                    </div>
                    <div class="modules-actions">
                        <button class="btn btn-outline-primary" id="modules-refresh">
                            <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-1">
                                <path fill-rule="evenodd" d="M8 3a5 5 0 1 0 4.546 2.914.5.5 0 0 1 .908-.417A6 6 0 1 1 8 2v1z"/>
                                <path d="M8 4.466V.534a.25.25 0 0 1 .41-.192l2.36 1.966c.12.1.12.284 0 .384L8.41 4.658A.25.25 0 0 1 8 4.466z"/>
                            </svg>
                            Refresh
                        </button>
                    </div>
                </div>

                <div class="modules-toolbar">
                    <div class="input-group">
                        <span class="input-group-text">
                            <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16">
                                <path d="M11.742 10.344a6.5 6.5 0 1 0-1.397 1.398h-.001c.03.04.062.078.098.115l3.85 3.85a1 1 0 0 0 1.415-1.414l-3.85-3.85a1.007 1.007 0 0 0-.115-.1zM12 6.5a5.5 5.5 0 1 1-11 0 5.5 5.5 0 0 1 11 0z"/>
                            </svg>
                        </span>
                        <input type="text" class="form-control" id="module-search" placeholder="Search modules or packages">
                    </div>
                    <select class="form-select" id="module-package-filter">
                        <option value="">All Packages</option>
                    </select>
                </div>

                <div class="modules-table card">
                    <div class="card-header d-flex justify-content-between align-items-center">
                        <span>Modules</span>
                        <span class="text-muted small" id="modules-count">0 modules</span>
                    </div>
                    <div class="table-responsive">
                        <table class="table table-hover align-middle">
                            <thead>
                                <tr>
                                    <th>Module</th>
                                    <th>Package</th>
                                    <th>Permissions</th>
                                    <th>Status</th>
                                    <th class="text-end">Action</th>
                                </tr>
                            </thead>
                            <tbody id="modules-table-body">
                                <tr>
                                    <td colspan="5" class="text-center text-muted py-4">Loading modules...</td>
                                </tr>
                            </tbody>
                        </table>
                    </div>
                </div>
            </div>
        `);

        $('#modules-refresh').on('click', () => this.loadModules());
        $('#module-search').on('input', () => this.applyFilters());
        $('#module-package-filter').on('change', () => this.applyFilters());
    },

    loadModules: async function() {
        try {
            const response = await Monolith.API.get('/core?action=get-modules');
            if (response.Success || response.success) {
                this.modules = response.Data || response.data || [];
                this.populatePackageFilter();
                this.applyFilters();
                return;
            }

            this.renderError('Failed to load modules');
        } catch (error) {
            console.error('Error loading modules:', error);
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

    renderModules: function() {
        const tbody = $('#modules-table-body');
        const countEl = $('#modules-count');
        countEl.text(`${this.filtered.length} module${this.filtered.length === 1 ? '' : 's'}`);

        if (this.filtered.length === 0) {
            tbody.html('<tr><td colspan="5" class="text-center text-muted py-4">No modules found</td></tr>');
            return;
        }

        let html = '';
        this.filtered.forEach(module => {
            const permissions = this.formatPermissions(module.systemPermissions || []);
            const statusBadge = module.enabled
                ? '<span class="badge bg-success">Enabled</span>'
                : '<span class="badge bg-secondary">Disabled</span>';

            html += `
                <tr>
                    <td>
                        <div class="fw-semibold">${module.name || module.id}</div>
                        <div class="text-muted small">${module.id}</div>
                    </td>
                    <td>${module.packageName || module.packageId}</td>
                    <td>${permissions}</td>
                    <td>${statusBadge}</td>
                    <td class="text-end">
                        <div class="form-check form-switch justify-content-end d-inline-flex align-items-center gap-2">
                            <input class="form-check-input module-toggle" type="checkbox"
                                data-package="${module.packageId}"
                                data-module="${module.id}"
                                ${module.enabled ? 'checked' : ''}>
                            <label class="form-check-label small">${module.enabled ? 'On' : 'Off'}</label>
                        </div>
                    </td>
                </tr>
            `;
        });

        tbody.html(html);
        this.bindToggleHandlers();
    },

    bindToggleHandlers: function() {
        $('.module-toggle').off('change').on('change', async (e) => {
            const toggle = $(e.currentTarget);
            const packageId = toggle.data('package');
            const moduleId = toggle.data('module');
            const enabled = toggle.is(':checked');

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

                const module = this.modules.find(m => m.packageId === packageId && m.id === moduleId);
                if (module) {
                    module.enabled = enabled;
                }

                Monolith.UI.toast(`Module ${enabled ? 'enabled' : 'disabled'}`, 'success');
                this.applyFilters();
            } catch (error) {
                console.error('Error updating module state:', error);
                Monolith.UI.toast('Failed to update module state', 'error');
                toggle.prop('checked', !enabled);
            } finally {
                toggle.prop('disabled', false);
            }
        });
    },

    formatPermissions: function(systemPermissions) {
        if (!systemPermissions || systemPermissions.length === 0) {
            return '<span class="text-muted small">No system access</span>';
        }

        const entries = systemPermissions.map(p => {
            const type = (p.type || '').replace('File', 'File ').replace('Network', 'Network ');
            return `<span class="badge bg-light text-dark border">${type}: ${p.resource}</span>`;
        });

        return `<div class="d-flex flex-wrap gap-1">${entries.join('')}</div>`;
    },

    renderError: function(message) {
        $('#modules-table-body').html(`<tr><td colspan="5" class="text-center text-danger py-4">${message}</td></tr>`);
    }
};

if (typeof Monolith !== 'undefined') {
    Monolith.Pages = Monolith.Pages || {};
    Monolith.Pages.Modules = Modules;
}
