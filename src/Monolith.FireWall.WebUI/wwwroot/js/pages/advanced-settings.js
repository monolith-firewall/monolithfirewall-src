// Advanced Settings Page
var AdvancedSettings = {
    tuneables: [],
    originalValues: {},
    featuredKeys: new Set(),

    init: function() {
        console.log('Initializing Advanced Settings...');
        this.render();
        this.bindEvents();
        this.loadTuneables();
    },

    render: function() {
        const container = $('#advanced-settings-container');
        container.html(`
            <div class="container-fluid advanced-settings">
                <div class="d-flex flex-wrap justify-content-between align-items-center mb-3">
                    <div>
                        <h1 class="mb-1">Advanced Settings</h1>
                        <div class="text-muted small">Save settings to apply at next boot, or Save and Apply Now to apply immediately.</div>
                    </div>
                    <div class="mt-2 mt-md-0">
                        <button class="btn btn-outline-secondary btn-sm" id="advanced-refresh-btn">Refresh</button>
                    </div>
                </div>

                <ul class="nav nav-tabs" id="advanced-tabs" role="tablist">
                    <li class="nav-item" role="presentation">
                        <button class="nav-link active" id="advanced-network-tab" data-bs-toggle="tab" data-bs-target="#advanced-network" type="button" role="tab">
                            Network
                        </button>
                    </li>
                    <li class="nav-item" role="presentation">
                        <button class="nav-link" id="advanced-firewall-tab" data-bs-toggle="tab" data-bs-target="#advanced-firewall" type="button" role="tab">
                            Firewall
                        </button>
                    </li>
                    <li class="nav-item" role="presentation">
                        <button class="nav-link" id="advanced-tuneables-tab" data-bs-toggle="tab" data-bs-target="#advanced-tuneables" type="button" role="tab">
                            System Tuneables
                        </button>
                    </li>
                </ul>

                <div class="tab-content pt-4">
                    <div class="tab-pane fade show active" id="advanced-network" role="tabpanel">
                        <div class="card mb-4">
                            <div class="card-header d-flex justify-content-between align-items-center">
                                <h5 class="mb-0">Core Network Controls</h5>
                                <div class="btn-group btn-group-sm">
                                    <button class="btn btn-outline-primary" id="advanced-save-network">Save</button>
                                    <button class="btn btn-primary" id="advanced-apply-network">Save & Apply Now</button>
                                </div>
                            </div>
                            <div class="card-body" id="advanced-network-container">
                                <div class="text-center text-muted py-4">Loading tuneables...</div>
                            </div>
                        </div>
                    </div>

                    <div class="tab-pane fade" id="advanced-firewall" role="tabpanel">
                        <div class="card mb-4">
                            <div class="card-header d-flex justify-content-between align-items-center">
                                <h5 class="mb-0">Firewall Hardening</h5>
                                <div class="btn-group btn-group-sm">
                                    <button class="btn btn-outline-primary" id="advanced-save-firewall">Save</button>
                                    <button class="btn btn-primary" id="advanced-apply-firewall">Save & Apply Now</button>
                                </div>
                            </div>
                            <div class="card-body" id="advanced-firewall-container">
                                <div class="text-center text-muted py-4">Loading tuneables...</div>
                            </div>
                        </div>
                    </div>

                    <div class="tab-pane fade" id="advanced-tuneables" role="tabpanel">
                        <div class="d-flex flex-wrap justify-content-between align-items-center mb-3 gap-2">
                            <div class="input-group tuneable-search">
                                <span class="input-group-text">Search</span>
                                <input type="text" class="form-control" id="tuneables-search" placeholder="Filter by name, key, or description">
                            </div>
                            <div class="d-flex gap-2">
                                <button class="btn btn-outline-primary btn-sm" id="advanced-save-changes">Save Changes</button>
                                <button class="btn btn-primary btn-sm" id="advanced-apply-changes">Save & Apply Changes</button>
                                <button class="btn btn-outline-secondary btn-sm" id="advanced-apply-all">Save & Apply All</button>
                            </div>
                        </div>

                        <div class="card">
                            <div class="card-body p-0">
                                <div class="table-responsive">
                                    <table class="table table-hover mb-0 tuneables-table">
                                        <thead>
                                            <tr>
                                                <th>Setting</th>
                                                <th>Current</th>
                                                <th>Desired</th>
                                                <th>Category</th>
                                                <th></th>
                                            </tr>
                                        </thead>
                                        <tbody id="tuneables-table-body">
                                            <tr>
                                                <td colspan="5" class="text-center text-muted py-4">Loading tuneables...</td>
                                            </tr>
                                        </tbody>
                                    </table>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `);
    },

    bindEvents: function() {
        $(document).off('click', '#advanced-refresh-btn');
        $(document).on('click', '#advanced-refresh-btn', () => this.loadTuneables());

        $(document).off('click', '#advanced-save-network');
        $(document).on('click', '#advanced-save-network', () => this.saveFeatured('#advanced-network-container'));

        $(document).off('click', '#advanced-apply-network');
        $(document).on('click', '#advanced-apply-network', () => this.applyFeatured('#advanced-network-container'));

        $(document).off('click', '#advanced-save-firewall');
        $(document).on('click', '#advanced-save-firewall', () => this.saveFeatured('#advanced-firewall-container'));

        $(document).off('click', '#advanced-apply-firewall');
        $(document).on('click', '#advanced-apply-firewall', () => this.applyFeatured('#advanced-firewall-container'));

        $(document).off('click', '#advanced-save-changes');
        $(document).on('click', '#advanced-save-changes', () => this.saveChanges());

        $(document).off('click', '#advanced-apply-changes');
        $(document).on('click', '#advanced-apply-changes', () => this.applyChanges());

        $(document).off('click', '#advanced-apply-all');
        $(document).on('click', '#advanced-apply-all', () => this.applyAll());

        $(document).off('input', '#tuneables-search');
        $(document).on('input', '#tuneables-search', (e) => {
            this.renderTuneables((e.target.value || '').trim());
        });


        $(document).off('click', '.tuneable-save-btn');
        $(document).on('click', '.tuneable-save-btn', (e) => {
            const key = $(e.currentTarget).data('key');
            if (key) {
                this.saveSingle(key);
            }
        });

        $(document).off('click', '.tuneable-apply-btn');
        $(document).on('click', '.tuneable-apply-btn', (e) => {
            const key = $(e.currentTarget).data('key');
            if (key) {
                this.applySingle(key);
            }
        });
    },

    loadTuneables: async function() {
        try {
            const response = await Monolith.API.get('/system/tuneables');
            const data = response.Data || response.data || [];
            this.tuneables = Array.isArray(data) ? data : [];
            this.originalValues = {};
            this.tuneables.forEach(item => {
                const desired = item.DesiredValue || item.desiredValue || '';
                this.originalValues[item.Key || item.key] = desired.toString();
            });
            this.renderFeatured();
            this.renderTuneables($('#tuneables-search').val() || '');
        } catch (error) {
            console.error('Failed to load tuneables:', error);
            Monolith.UI.toast('Failed to load tuneables', 'error');
        }
    },

    renderFeatured: function() {
        const featured = this.tuneables.filter(t => t.Featured || t.featured);
        const network = featured.filter(t => this.isCategory(t, ['routing', 'performance', 'icmp', 'bridge']));
        const firewall = featured.filter(t => this.isCategory(t, ['security']));

        this.featuredKeys = new Set([
            ...network.map(t => t.Key || t.key),
            ...firewall.map(t => t.Key || t.key)
        ]);

        this.renderFeaturedSection('#advanced-network-container', network, 'No network tuneables configured.');
        this.renderFeaturedSection('#advanced-firewall-container', firewall, 'No firewall tuneables configured.');
    },

    renderFeaturedSection: function(selector, items, emptyMessage) {
        const container = $(selector);
        if (!container.length) {
            return;
        }

        if (!items || items.length === 0) {
            container.html(`<div class="text-muted">${emptyMessage}</div>`);
            return;
        }

        const rows = items.map(tuneable => {
            const key = tuneable.Key || tuneable.key;
            const label = tuneable.Label || tuneable.label || key;
            const description = tuneable.Description || tuneable.description || '';
            const input = this.buildInput(tuneable, `featured-${key}`, 'featured');
            return `
                <div class="tuneable-featured-row">
                    <div class="tuneable-featured-info">
                        <div class="fw-semibold">${label}</div>
                        <div class="text-muted small">${description}</div>
                        <div class="tuneable-key">${key}</div>
                    </div>
                    <div class="tuneable-featured-input">
                        ${input}
                    </div>
                </div>
            `;
        }).join('');

        container.html(`<div class="tuneable-featured-list">${rows}</div>`);
    },

    isCategory: function(tuneable, categories) {
        const category = (tuneable.Category || tuneable.category || '').toString().toLowerCase();
        return categories.includes(category);
    },

    renderTuneables: function(filterText) {
        const body = $('#tuneables-table-body');
        const filter = (filterText || '').toLowerCase();

        const filtered = this.tuneables.filter(item => {
            const key = item.Key || item.key;
            if (this.featuredKeys && this.featuredKeys.has(key)) {
                return false;
            }
            if (!filter) return true;
            const haystack = [
                item.Label || item.label,
                item.Key || item.key,
                item.Description || item.description,
                item.Category || item.category
            ].join(' ').toLowerCase();
            return haystack.includes(filter);
        });

        if (filtered.length === 0) {
            body.html('<tr><td colspan="5" class="text-center text-muted py-4">No tuneables match that filter.</td></tr>');
            return;
        }

        const rows = filtered.map(tuneable => {
            const key = tuneable.Key || tuneable.key;
            const label = tuneable.Label || tuneable.label || key;
            const description = tuneable.Description || tuneable.description || '';
            const category = tuneable.Category || tuneable.category || '';
            const current = tuneable.CurrentValue || tuneable.currentValue || 'n/a';
            const input = this.buildInput(tuneable, `tuneable-${key}`, 'table');

            return `
                <tr>
                    <td>
                        <div class="fw-semibold">${label}</div>
                        <div class="tuneable-key">${key}</div>
                        <div class="tuneable-description">${description}</div>
                    </td>
                    <td class="text-muted">${current}</td>
                    <td>${input}</td>
                    <td>${category}</td>
                    <td class="tuneable-actions text-end">
                        <div class="btn-group btn-group-sm">
                            <button class="btn btn-outline-primary tuneable-save-btn" data-key="${key}" title="Save for next boot">Save</button>
                            <button class="btn btn-primary tuneable-apply-btn" data-key="${key}" title="Save and apply now">Apply</button>
                        </div>
                    </td>
                </tr>
            `;
        }).join('');

        body.html(rows);
    },

    buildInput: function(tuneable, idPrefix, location) {
        const key = tuneable.Key || tuneable.key;
        const type = tuneable.Type || tuneable.type || 'string';
        const desired = tuneable.DesiredValue || tuneable.desiredValue || tuneable.DefaultValue || tuneable.defaultValue || '';
        const nameAttr = `data-key="${key}" data-type="${type}"`;
        const valueAttr = desired !== null && desired !== undefined ? desired : '';

        if (type === 'bool') {
            const checked = valueAttr.toString() === '1' || valueAttr.toString().toLowerCase() === 'true';
            const inputId = `${idPrefix}-toggle`;
            return `
                <div class="form-check form-switch">
                    <input class="form-check-input tuneable-input" type="checkbox" id="${inputId}" ${nameAttr} ${checked ? 'checked' : ''}>
                </div>
            `;
        }

        if (type === 'select') {
            const options = (tuneable.Options || tuneable.options || []).map(opt => {
                const optValue = opt.Value || opt.value;
                const optLabel = opt.Label || opt.label || optValue;
                const selected = optValue.toString() === valueAttr.toString() ? 'selected' : '';
                return `<option value="${optValue}" ${selected}>${optLabel}</option>`;
            }).join('');
            return `
                <select class="form-select form-select-sm tuneable-input tuneable-input-select" ${nameAttr}>
                    ${options}
                </select>
            `;
        }

        const inputType = type === 'int' ? 'number' : 'text';
        return `
            <input type="${inputType}" class="form-control form-control-sm tuneable-input" ${nameAttr} value="${valueAttr}">
        `;
    },

    collectItems: function(selector, onlyChanges) {
        const items = [];
        $(selector).each((_, el) => {
            const $el = $(el);
            const key = $el.data('key');
            const type = $el.data('type');
            let value;

            if (type === 'bool') {
                value = $el.is(':checked') ? '1' : '0';
            } else {
                value = $el.val();
            }

            if (key) {
                const normalized = value !== null && value !== undefined ? value.toString() : '';
                if (onlyChanges) {
                    const original = (this.originalValues[key] || '').toString();
                    if (original === normalized) {
                        return;
                    }
                }
                items.push({ key: key, value: normalized });
            }
        });
        return items;
    },

    saveFeatured: function(selector) {
        const items = this.collectItems(`${selector} .tuneable-input`, false);
        this.saveItems(items);
    },

    applyFeatured: function(selector) {
        const items = this.collectItems(`${selector} .tuneable-input`, false);
        this.applyItems(items);
    },

    saveChanges: function() {
        const items = this.collectItems('#tuneables-table-body .tuneable-input', true);
        if (items.length === 0) {
            Monolith.UI.toast('No changes to save', 'info');
            return;
        }
        this.saveItems(items);
    },

    applyChanges: function() {
        const items = this.collectItems('#tuneables-table-body .tuneable-input', true);
        if (items.length === 0) {
            Monolith.UI.toast('No changes to apply', 'info');
            return;
        }
        this.applyItems(items);
    },

    applyAll: function() {
        const items = this.collectItems('#tuneables-table-body .tuneable-input', false);
        this.applyItems(items);
    },

    saveSingle: function(key) {
        const input = $(`#tuneables-table-body .tuneable-input[data-key="${key}"]`);
        if (!input.length) {
            return;
        }
        const items = this.collectItems(input, false);
        this.saveItems(items);
    },

    applySingle: function(key) {
        const input = $(`#tuneables-table-body .tuneable-input[data-key="${key}"]`);
        if (!input.length) {
            return;
        }
        const items = this.collectItems(input, false);
        this.applyItems(items);
    },

    saveItems: async function(items) {
        if (!items || items.length === 0) {
            return;
        }

        try {
            const response = await Monolith.API.post('/system/tuneables/save', { items: items });
            if (!(response.Success || response.success)) {
                throw new Error(response.Error || response.error || 'Save failed');
            }

            const data = response.Data || response.data || {};
            this.handleSaveResults(data);
            await this.loadTuneables();
        } catch (error) {
            console.error('Save tuneables failed:', error);
            Monolith.UI.toast('Failed to save tuneables', 'error');
        }
    },

    applyItems: async function(items) {
        if (!items || items.length === 0) {
            return;
        }

        try {
            const response = await Monolith.API.post('/system/tuneables/apply', { items: items });
            if (!(response.Success || response.success)) {
                throw new Error(response.Error || response.error || 'Apply failed');
            }

            const data = response.Data || response.data || {};
            this.handleApplyResults(data);
            await this.loadTuneables();
        } catch (error) {
            console.error('Apply tuneables failed:', error);
            Monolith.UI.toast('Failed to apply tuneables', 'error');
        }
    },

    handleSaveResults: function(data) {
        const results = data.Results || data.results || [];
        if (!Array.isArray(results) || results.length === 0) {
            Monolith.UI.toast('Tuneables saved (will apply at next boot)', 'success');
            return;
        }

        const failures = results.filter(r => !(r.Success || r.success));
        if (failures.length === 0) {
            Monolith.UI.toast('Tuneables saved (will apply at next boot)', 'success');
            return;
        }

        const listItems = failures.map(item => `
            <li>
                <strong>${item.Key || item.key}</strong>
                <div class="text-muted small">${item.Error || item.error || 'Failed to save'}</div>
            </li>
        `).join('');

        const body = `
            <div class="alert alert-warning">
                ${failures.length} tuneable(s) failed to save.
            </div>
            <ul class="ps-3 mb-0">${listItems}</ul>
        `;
        Monolith.UI.showModal('Tuneable Save Results', body, { size: 'lg' });
    },

    handleApplyResults: function(data) {
        const results = data.Results || data.results || [];
        if (!Array.isArray(results) || results.length === 0) {
            Monolith.UI.toast('Tuneables saved and applied', 'success');
            return;
        }

        const failures = results.filter(r => !(r.Success || r.success));
        if (failures.length === 0) {
            Monolith.UI.toast('Tuneables saved and applied', 'success');
            return;
        }

        const listItems = failures.map(item => `
            <li>
                <strong>${item.Key || item.key}</strong>
                <div class="text-muted small">${item.Error || item.error || 'Failed to apply'}</div>
            </li>
        `).join('');

        const body = `
            <div class="alert alert-warning">
                ${failures.length} tuneable(s) failed to apply.
            </div>
            <ul class="ps-3 mb-0">${listItems}</ul>
        `;
        Monolith.UI.showModal('Tuneable Apply Results', body, { size: 'lg' });
    }
};

if (typeof Monolith !== 'undefined') {
    Monolith.Pages = Monolith.Pages || {};
    Monolith.Pages.AdvancedSettings = AdvancedSettings;
}
