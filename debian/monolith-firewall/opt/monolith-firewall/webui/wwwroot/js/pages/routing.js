// Routing Page - Gateways + Static Routes
var Routing = {
    gateways: [],
    routes: [],
    interfaces: [],

    init: function() {
        console.log('Initializing Routing page...');
        this.render();
        this.loadData();
        this.attachHandlers();
    },

    render: function() {
        const container = $('#page-content');
        container.html(`
            <div class="container-fluid p-4 routing-shell">
                <div class="routing-hero">
                    <div>
                        <h2 class="page-title mb-1">Routing</h2>
                        <p class="text-muted mb-0">Gateways and static route management</p>
                    </div>
                    <div class="routing-actions">
                        <button class="btn btn-outline-primary" id="routing-refresh">Refresh</button>
                    </div>
                </div>

                <ul class="nav nav-tabs routing-tabs" role="tablist">
                    <li class="nav-item" role="presentation">
                        <button class="nav-link active" id="routing-gateways-tab" data-bs-toggle="tab" data-bs-target="#routing-gateways" type="button" role="tab">
                            Gateways
                        </button>
                    </li>
                    <li class="nav-item" role="presentation">
                        <button class="nav-link" id="routing-routes-tab" data-bs-toggle="tab" data-bs-target="#routing-routes" type="button" role="tab">
                            Static Routes
                        </button>
                    </li>
                </ul>

                <div class="tab-content routing-content">
                    <div class="tab-pane fade show active" id="routing-gateways" role="tabpanel">
                        <div class="card">
                            <div class="card-header d-flex justify-content-between align-items-center">
                                <span>Gateways</span>
                                <span class="text-muted small" id="gateway-count">0 gateways</span>
                            </div>
                            <div class="table-responsive">
                                <table class="table table-hover align-middle mb-0">
                                    <thead>
                                        <tr>
                                            <th>Name</th>
                                            <th>Gateway</th>
                                            <th>Interface</th>
                                            <th>Source</th>
                                            <th>Metric</th>
                                        </tr>
                                    </thead>
                                    <tbody id="gateways-body">
                                        <tr><td colspan="5" class="text-center text-muted py-4">Loading gateways...</td></tr>
                                    </tbody>
                                </table>
                            </div>
                        </div>
                    </div>
                    <div class="tab-pane fade" id="routing-routes" role="tabpanel">
                        <div class="card">
                            <div class="card-header d-flex justify-content-between align-items-center">
                                <span>Static Routes</span>
                                <div>
                                    <button class="btn btn-sm btn-primary" id="routing-add-route">Add Route</button>
                                </div>
                            </div>
                            <div class="table-responsive">
                                <table class="table table-hover align-middle mb-0">
                                    <thead>
                                        <tr>
                                            <th>Destination</th>
                                            <th>Gateway</th>
                                            <th>Interface</th>
                                            <th>Metric</th>
                                            <th>Status</th>
                                            <th>Description</th>
                                            <th class="text-end">Actions</th>
                                        </tr>
                                    </thead>
                                    <tbody id="routes-body">
                                        <tr><td colspan="7" class="text-center text-muted py-4">Loading routes...</td></tr>
                                    </tbody>
                                </table>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `);
    },

    attachHandlers: function() {
        $('#routing-refresh').on('click', () => this.loadData());
        $('#routing-add-route').on('click', () => this.showRouteModal());
    },

    loadData: async function() {
        await Promise.all([
            this.loadGateways(),
            this.loadRoutes(),
            this.loadInterfaces()
        ]);
    },

    loadGateways: async function() {
        try {
            const response = await Monolith.API.get('/routing/gateways');
            if (response.Success || response.success) {
                this.gateways = response.Data || response.data || [];
            } else {
                this.gateways = [];
            }
            this.renderGateways();
        } catch (error) {
            console.error('Failed to load gateways:', error);
            this.gateways = [];
            this.renderGateways(true);
        }
    },

    loadRoutes: async function() {
        try {
            const response = await Monolith.API.get('/routing/routes');
            if (response.Success || response.success) {
                this.routes = response.Data || response.data || [];
            } else {
                this.routes = [];
            }
            this.renderRoutes();
        } catch (error) {
            console.error('Failed to load routes:', error);
            this.routes = [];
            this.renderRoutes(true);
        }
    },

    loadInterfaces: async function() {
        try {
            const response = await Monolith.API.get('/interfaces/assignments');
            if (response.Success || response.success) {
                const data = response.Data || response.data || {};
                const assigned = data.Assigned || data.assigned || [];
                const unassigned = data.Unassigned || data.unassigned || [];
                const interfaces = [];
                assigned.forEach(a => {
                    if (a.Interface || a.interface) {
                        interfaces.push(a.Interface || a.interface);
                    }
                });
                unassigned.forEach(u => {
                    if (u.Interface || u.interface) {
                        interfaces.push(u.Interface || u.interface);
                    }
                });
                this.interfaces = [...new Set(interfaces)];
                return;
            }
        } catch (error) {
            console.error('Failed to load interfaces for routing:', error);
        }
        this.interfaces = [];
    },

    renderGateways: function(isError) {
        const tbody = $('#gateways-body');
        const count = $('#gateway-count');
        count.text(`${this.gateways.length} gateways`);

        if (isError) {
            tbody.html('<tr><td colspan="5" class="text-center text-danger py-4">Failed to load gateways</td></tr>');
            return;
        }

        if (!this.gateways.length) {
            tbody.html('<tr><td colspan="5" class="text-center text-muted py-4">No gateways detected</td></tr>');
            return;
        }

        let html = '';
        this.gateways.forEach(gw => {
            const source = (gw.Source || gw.source || '').toLowerCase();
            const sourceBadge = source === 'dhcp'
                ? '<span class="badge bg-info-subtle text-info border">DHCP</span>'
                : '<span class="badge bg-light text-muted border">Static</span>';
            const metric = gw.Metric !== undefined ? gw.Metric : gw.metric;

            html += `
                <tr>
                    <td>
                        <div class="fw-semibold">${gw.Name || gw.name}</div>
                        ${gw.IsDefault || gw.isDefault ? '<div class="text-muted small">Default</div>' : ''}
                    </td>
                    <td><code>${gw.Address || gw.address}</code></td>
                    <td>${gw.Interface || gw.interface || '-'}</td>
                    <td>${sourceBadge}</td>
                    <td>${metric !== undefined && metric !== null ? metric : '-'}</td>
                </tr>
            `;
        });

        tbody.html(html);
    },

    renderRoutes: function(isError) {
        const tbody = $('#routes-body');
        if (isError) {
            tbody.html('<tr><td colspan="7" class="text-center text-danger py-4">Failed to load routes</td></tr>');
            return;
        }

        if (!this.routes.length) {
            tbody.html('<tr><td colspan="7" class="text-center text-muted py-4">No static routes configured</td></tr>');
            return;
        }

        let html = '';
        this.routes.forEach(route => {
            const active = route.Active || route.active;
            const statusBadge = active
                ? '<span class="badge bg-success">Active</span>'
                : '<span class="badge bg-secondary">Inactive</span>';
            const metric = route.Metric !== undefined ? route.Metric : route.metric;

            html += `
                <tr>
                    <td><code>${route.Destination || route.destination}</code></td>
                    <td>${route.Gateway || route.gateway || '-'}</td>
                    <td>${route.Interface || route.interface || '-'}</td>
                    <td>${metric !== undefined && metric !== null ? metric : '-'}</td>
                    <td>${statusBadge}</td>
                    <td>${route.Description || route.description || '-'}</td>
                    <td class="text-end">
                        <button class="btn btn-sm btn-outline-danger" onclick="Routing.deleteRoute(${route.Id || route.id})">Delete</button>
                    </td>
                </tr>
            `;
        });

        tbody.html(html);
    },

    showRouteModal: function() {
        const interfaceOptions = this.buildOptions(this.interfaces, '', 'Optional interface');

        const body = `
            <form id="route-form">
                <div class="mb-3">
                    <label class="form-label">Destination (CIDR)</label>
                    <input type="text" class="form-control" id="route-destination" placeholder="192.168.50.0/24 or default">
                </div>
                <div class="mb-3">
                    <label class="form-label">Gateway</label>
                    <input type="text" class="form-control" id="route-gateway" placeholder="192.168.1.1">
                </div>
                <div class="mb-3">
                    <label class="form-label">Interface</label>
                    <select class="form-select" id="route-interface">
                        ${interfaceOptions}
                    </select>
                    <div class="form-text">Gateway or interface is required.</div>
                </div>
                <div class="mb-3">
                    <label class="form-label">Metric</label>
                    <input type="number" class="form-control" id="route-metric" min="0">
                </div>
                <div class="mb-3">
                    <label class="form-label">Description</label>
                    <input type="text" class="form-control" id="route-description">
                </div>
            </form>
        `;

        const footer = `
            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
            <button type="button" class="btn btn-primary" id="route-save-btn">Add Route</button>
        `;

        const modal = Monolith.UI.showModal('Add Static Route', body, { size: 'lg', footerHtml: footer, staticBackdrop: true });
        modal.element.find('#route-save-btn').on('click', async () => {
            const destination = modal.element.find('#route-destination').val();
            const gateway = modal.element.find('#route-gateway').val();
            const iface = modal.element.find('#route-interface').val();
            const metric = modal.element.find('#route-metric').val();
            const description = modal.element.find('#route-description').val();

            if (!destination) {
                Monolith.UI.toast('Destination is required', 'warning');
                return;
            }

            if (!gateway && !iface) {
                Monolith.UI.toast('Gateway or interface is required', 'warning');
                return;
            }

            const payload = {
                destination: destination,
                gateway: gateway || null,
                interface: iface || null,
                metric: metric ? parseInt(metric, 10) : null,
                description: description || null
            };

            try {
                const response = await Monolith.API.post('/routing/routes', payload);
                if (!(response.Success || response.success)) {
                    throw new Error(response.Error || response.error || 'Failed to add route');
                }

                Monolith.UI.toast('Route added', 'success');
                modal.instance.hide();
                this.loadRoutes();
            } catch (error) {
                console.error('Add route failed:', error);
                Monolith.UI.toast('Failed to add route', 'error');
            }
        });
    },

    deleteRoute: function(id) {
        if (!confirm('Delete this static route?')) {
            return;
        }

        Monolith.API.delete(`/routing/routes/${id}`)
            .then(response => {
                if (!(response.Success || response.success)) {
                    throw new Error(response.Error || response.error || 'Delete failed');
                }
                Monolith.UI.toast('Route deleted', 'success');
                this.loadRoutes();
            })
            .catch(error => {
                console.error('Delete route failed:', error);
                Monolith.UI.toast('Failed to delete route', 'error');
            });
    },

    buildOptions: function(items, selected, placeholder) {
        let html = '';
        if (placeholder) {
            html += `<option value="">${placeholder}</option>`;
        }

        items.forEach(item => {
            const isSelected = item === selected ? 'selected' : '';
            html += `<option value="${item}" ${isSelected}>${item}</option>`;
        });
        return html;
    }
};

if (typeof Monolith !== 'undefined') {
    Monolith.Pages = Monolith.Pages || {};
    Monolith.Pages.Routing = Routing;
}
