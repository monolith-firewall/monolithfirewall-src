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
                        <button class="nav-link active" id="routing-status-tab" data-bs-toggle="tab" data-bs-target="#routing-status" type="button" role="tab">
                            <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-1">
                                <path d="M8 0a8 8 0 1 0 0 16A8 8 0 0 0 8 0zM1.612 10.867L.5 9.5l1.112-1.367a1 1 0 0 1 1.547-.217L4 9.5l.341-.85a1 1 0 0 1 1.547-.217L7 9.5l.793-1.367a1 1 0 0 1 1.547.217L10.5 9.5l.341-.85a1 1 0 0 1 1.547-.217L13 9.5l.793-1.367a1 1 0 0 1 1.547.217L15.5 9.5l-1.112 1.367a1 1 0 0 1-1.547.217L12 9.5l-.341.85a1 1 0 0 1-1.547.217L9 9.5l-.793 1.367a1 1 0 0 1-1.547-.217L5.5 9.5l-.341.85a1 1 0 0 1-1.547.217L3 9.5l-.793 1.367a1 1 0 0 1-1.547-.217L.5 9.5l1.112 1.367z"/>
                            </svg>
                            Routing Status
                        </button>
                    </li>
                    <li class="nav-item" role="presentation">
                        <button class="nav-link" id="routing-gateways-tab" data-bs-toggle="tab" data-bs-target="#routing-gateways" type="button" role="tab">
                            Gateways
                        </button>
                    </li>
                    <li class="nav-item" role="presentation">
                        <button class="nav-link" id="routing-routes-tab" data-bs-toggle="tab" data-bs-target="#routing-routes" type="button" role="tab">
                            Static Routes
                        </button>
                    </li>
                    <li class="nav-item" role="presentation">
                        <button class="nav-link" id="routing-tests-tab" data-bs-toggle="tab" data-bs-target="#routing-tests" type="button" role="tab">
                            <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-1">
                                <path d="M8 15A7 7 0 1 1 8 1a7 7 0 0 1 0 14zm0 1A8 8 0 1 0 8 0a8 8 0 0 0 0 16z"/>
                                <path d="M5.255 5.786a.237.237 0 0 0 .241.247h.825c.138 0 .248-.113.266-.25.09-.656.54-1.134 1.342-1.134.686 0 1.314.343 1.314 1.168 0 .635-.374.927-.965 1.371-.673.489-1.206 1.06-1.168 1.987l.003.217a.25.25 0 0 0 .25.246h.811a.25.25 0 0 0 .25-.25v-.105c0-.718.273-.927 1.01-1.486.609-.463 1.244-.977 1.244-2.056 0-1.511-1.276-2.241-2.673-2.241-1.326 0-2.786.647-2.754 2.533zm1.533 2.767h.855c.132 0 .248.112.266.25l.001.003c0 .138-.112.25-.25.25h-.855a.25.25 0 0 1-.25-.25v-.003a.25.25 0 0 1 .25-.25z"/>
                            </svg>
                            Routing Tests
                        </button>
                    </li>
                </ul>

                <div class="tab-content routing-content">
                    <!-- Routing Status Tab -->
                    <div class="tab-pane fade show active" id="routing-status" role="tabpanel">
                        <div class="card mb-4">
                            <div class="card-header d-flex justify-content-between align-items-center">
                                <h5 class="mb-0">Routing Status</h5>
                                <button type="button" class="btn btn-sm btn-outline-secondary" id="routing-status-refresh">
                                    <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-1">
                                        <path d="M8 3a5 5 0 1 0 4.546 2.914.5.5 0 0 1 .908-.417A6 6 0 1 1 8 2v1z"/>
                                        <path d="M8 4.466V.534a.25.25 0 0 1 .41-.192l2.36 1.966c.12.1.12.284 0 .384L8.41 4.658A.25.25 0 0 1 8 4.466z"/>
                                    </svg>
                                    Refresh
                                </button>
                            </div>
                            <div class="card-body" id="routing-status-content">
                                <div class="text-center text-muted py-4">
                                    <div class="spinner-border spinner-border-sm me-2" role="status"></div>
                                    Loading routing status...
                                </div>
                            </div>
                        </div>
                    </div>

                    <div class="tab-pane fade" id="routing-gateways" role="tabpanel">
                        <div class="card mb-4">
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
                        
                        <!-- Gateway Configuration Card -->
                        <div class="card">
                            <div class="card-header">
                                <h5 class="mb-0">Gateway Configuration</h5>
                            </div>
                            <div class="card-body">
                                <div class="mb-3">
                                    <label class="form-label">Current Default Gateway</label>
                                    <div class="input-group">
                                        <input type="text" class="form-control" id="routing-gateway-address" 
                                               placeholder="e.g., 10.100.0.1">
                                        <span class="input-group-text">via</span>
                                        <input type="text" class="form-control" id="routing-gateway-interface" 
                                               placeholder="e.g., ens18">
                                    </div>
                                    <small class="text-muted">Gateway IP address and interface (leave empty to use DHCP)</small>
                                </div>
                                <div class="btn-group">
                                    <button type="button" class="btn btn-sm btn-primary" id="routing-set-gateway">
                                        Set Default Gateway
                                    </button>
                                    <button type="button" class="btn btn-sm btn-outline-secondary" id="routing-test-gateway">
                                        Test Gateway Connectivity
                                    </button>
                                </div>
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

                    <!-- Routing Tests Tab -->
                    <div class="tab-pane fade" id="routing-tests" role="tabpanel">
                        <div class="card">
                            <div class="card-header">
                                <h5 class="mb-0">Routing Diagnostic Tools</h5>
                            </div>
                            <div class="card-body">
                                <div class="mb-4">
                                    <label class="form-label">Test Target</label>
                                    <div class="input-group">
                                        <input type="text" class="form-control" id="routing-test-target" 
                                               placeholder="e.g., 8.8.8.8 or google.com">
                                        <button type="button" class="btn btn-outline-primary" id="routing-ping-test">
                                            Ping Test
                                        </button>
                                        <button type="button" class="btn btn-outline-primary" id="routing-trace-test">
                                            Trace Route
                                        </button>
                                    </div>
                                    <small class="text-muted">Enter an IP address or hostname to test connectivity</small>
                                </div>
                                <div id="routing-test-results" class="mt-3" style="display: none;">
                                    <label class="form-label">Test Results</label>
                                    <pre class="bg-dark text-light p-3 rounded" id="routing-test-output" style="max-height: 400px; overflow-y: auto; font-size: 0.875rem;"></pre>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `);
    },

    attachHandlers: function() {
        $('#routing-refresh').on('click', () => this.loadData());
        $('#routing-status-refresh').on('click', () => this.loadRoutingStatus());
        $('#routing-add-route').on('click', () => this.showRouteModal());
        
        // IP forwarding toggle
        $(document).off('click', '#routing-apply-ip-forwarding');
        $(document).on('click', '#routing-apply-ip-forwarding', () => {
            this.applyIpForwarding();
        });

        // NAT Masquerade enable button
        $(document).off('click', '#routing-enable-masquerade');
        $(document).on('click', '#routing-enable-masquerade', () => {
            this.enableNatMasquerade();
        });

        // Gateway configuration
        $(document).off('click', '#routing-set-gateway');
        $(document).on('click', '#routing-set-gateway', () => {
            this.setDefaultGateway();
        });

        $(document).off('click', '#routing-test-gateway');
        $(document).on('click', '#routing-test-gateway', () => {
            this.testGateway();
        });

        // Routing tests
        $(document).off('click', '#routing-ping-test');
        $(document).on('click', '#routing-ping-test', () => {
            this.runPingTest();
        });

        $(document).off('click', '#routing-trace-test');
        $(document).on('click', '#routing-trace-test', () => {
            this.runTraceTest();
        });
    },

    loadData: async function() {
        await Promise.all([
            this.loadRoutingStatus(),
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

        // Find default gateway and populate configuration fields
        const defaultGateway = this.gateways.find(gw => gw.IsDefault || gw.isDefault);
        if (defaultGateway) {
            $('#routing-gateway-address').val(defaultGateway.Address || defaultGateway.address || '');
            $('#routing-gateway-interface').val(defaultGateway.Interface || defaultGateway.interface || '');
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

    loadRoutingStatus: async function() {
        try {
            const response = await Monolith.API.get('/routing/status');
            if (response.Success || response.success) {
                const data = response.Data || response.data || {};
                this.renderRoutingStatus(data);
            } else {
                $('#routing-status-content').html(`
                    <div class="alert alert-danger">
                        Failed to load routing status: ${response.Error || response.error || 'Unknown error'}
                    </div>
                `);
            }
        } catch (error) {
            console.error('Failed to load routing status:', error);
            $('#routing-status-content').html(`
                <div class="alert alert-danger">
                    Failed to load routing status: ${error.message || 'Unknown error'}
                </div>
            `);
        }
    },

    renderRoutingStatus: function(data) {
        const ipForwarding = data.IpForwardingEnabled || data.ipForwardingEnabled || false;
        const defaultGateway = data.DefaultGateway || data.defaultGateway || null;
        const routes = data.Routes || data.routes || [];
        const natMasquerade = data.NatMasqueradeEnabled || data.natMasqueradeEnabled || false;

        const gatewayInfo = defaultGateway 
            ? `<strong>${defaultGateway.Address || defaultGateway.address || 'N/A'}</strong> via <code>${defaultGateway.Interface || defaultGateway.interface || 'N/A'}</code>`
            : '<span class="text-muted">No default gateway configured</span>';

        const routesHtml = routes.length > 0
            ? routes.map(route => {
                const dest = route.Destination || route.destination || 'default';
                const gw = route.Gateway || route.gateway || '-';
                const iface = route.Interface || route.interface || '-';
                const proto = route.Protocol || route.protocol || '-';
                const isDefault = route.IsDefault || route.isDefault || false;
                return `
                    <tr>
                        <td>${isDefault ? '<strong>default</strong>' : dest}</td>
                        <td>${gw}</td>
                        <td>${iface}</td>
                        <td><span class="badge bg-secondary">${proto}</span></td>
                    </tr>
                `;
            }).join('')
            : '<tr><td colspan="4" class="text-center text-muted">No routes found</td></tr>';

        $('#routing-status-content').html(`
            <div class="row mb-4">
                <div class="col-md-6">
                    <div class="card border-0 bg-light">
                        <div class="card-body">
                            <h6 class="card-title">IP Forwarding</h6>
                            <div class="d-flex align-items-center">
                                <span class="badge ${ipForwarding ? 'bg-success' : 'bg-danger'} me-2">
                                    ${ipForwarding ? 'Enabled' : 'Disabled'}
                                </span>
                                ${!ipForwarding ? '<small class="text-muted">Required for routing between interfaces</small>' : ''}
                            </div>
                        </div>
                    </div>
                </div>
                <div class="col-md-6">
                    <div class="card border-0 bg-light">
                        <div class="card-body">
                            <h6 class="card-title">NAT Masquerade</h6>
                            <div class="d-flex align-items-center">
                                <span class="badge ${natMasquerade ? 'bg-success' : 'bg-warning'} me-2">
                                    ${natMasquerade ? 'Enabled' : 'Not Configured'}
                                </span>
                                ${!natMasquerade ? '<small class="text-muted">Required for LAN→WAN outbound traffic</small>' : ''}
                            </div>
                            ${!natMasquerade ? '<div class="mt-2"><button type="button" class="btn btn-sm btn-primary" id="routing-enable-masquerade">Enable NAT Masquerade</button></div>' : ''}
                        </div>
                    </div>
                </div>
            </div>
            <div class="mb-4">
                <h6>Default Gateway</h6>
                <p class="mb-0">${gatewayInfo}</p>
            </div>
            <div class="mb-4">
                <h6>Routing Table</h6>
                <div class="table-responsive">
                    <table class="table table-sm table-hover">
                        <thead>
                            <tr>
                                <th>Destination</th>
                                <th>Gateway</th>
                                <th>Interface</th>
                                <th>Protocol</th>
                            </tr>
                        </thead>
                        <tbody>
                            ${routesHtml}
                        </tbody>
                    </table>
                </div>
            </div>
            <div class="mt-4">
                <h6>Routing Configuration</h6>
                <div class="card border-0 bg-light">
                    <div class="card-body">
                        <div class="form-check form-switch mb-3">
                            <input class="form-check-input" type="checkbox" id="routing-toggle-ip-forwarding" ${ipForwarding ? 'checked' : ''}>
                            <label class="form-check-label" for="routing-toggle-ip-forwarding">
                                <strong>Enable IP Forwarding</strong>
                                <div class="text-muted small">Allow routing between network interfaces</div>
                            </label>
                        </div>
                        <button type="button" class="btn btn-sm btn-primary" id="routing-apply-ip-forwarding">
                            Apply IP Forwarding Setting
                        </button>
                        <div class="mt-2">
                            <small class="text-muted">For more advanced routing settings, visit <a href="#/system/advanced">Advanced Settings</a></small>
                        </div>
                    </div>
                </div>
            </div>
        `);
    },

    applyIpForwarding: async function() {
        const enabled = $('#routing-toggle-ip-forwarding').is(':checked');
        const value = enabled ? '1' : '0';

        try {
            const response = await Monolith.API.post('/system/tuneables/apply', {
                items: [
                    { key: 'net.ipv4.ip_forward', value: value }
                ]
            });

            if (response.Success || response.success) {
                Monolith.UI.toast(`IP forwarding ${enabled ? 'enabled' : 'disabled'}`, 'success');
                await this.loadRoutingStatus();
            } else {
                Monolith.UI.toast(`Failed to ${enabled ? 'enable' : 'disable'} IP forwarding: ${response.Error || response.error || 'Unknown error'}`, 'error');
            }
        } catch (error) {
            console.error('Failed to apply IP forwarding:', error);
            Monolith.UI.toast(`Failed to apply IP forwarding: ${error.message || 'Unknown error'}`, 'error');
        }
    },

    enableNatMasquerade: async function() {
        const button = $('#routing-enable-masquerade');
        const originalText = button.text();
        
        try {
            button.prop('disabled', true).text('Applying...');
            
            // Apply firewall rules, which will automatically configure masquerade for WAN interfaces
            const response = await Monolith.API.post('/api/firewall/apply', {});

            if (response.Success || response.success) {
                Monolith.UI.toast('NAT Masquerade enabled. Firewall rules have been applied.', 'success');
                // Reload routing status to show updated masquerade status
                await this.loadRoutingStatus();
            } else {
                const errorMsg = response.Error || response.error || 'Unknown error';
                Monolith.UI.toast(`Failed to enable NAT Masquerade: ${errorMsg}`, 'error');
                
                // Check if the error is about missing WAN interfaces
                if (errorMsg.toLowerCase().includes('wan') || errorMsg.toLowerCase().includes('interface')) {
                    Monolith.UI.toast('Note: NAT Masquerade requires at least one WAN interface to be configured. Please configure a WAN interface first.', 'warning');
                }
            }
        } catch (error) {
            console.error('Failed to enable NAT Masquerade:', error);
            Monolith.UI.toast(`Failed to enable NAT Masquerade: ${error.message || 'Unknown error'}`, 'error');
        } finally {
            button.prop('disabled', false).text(originalText);
        }
    },

    setDefaultGateway: async function() {
        const gateway = $('#routing-gateway-address').val()?.trim() || '';
        const iface = $('#routing-gateway-interface').val()?.trim() || '';

        if (!gateway && !iface) {
            Monolith.UI.toast('Please specify either gateway address or interface', 'warning');
            return;
        }

        if (gateway && !this.isValidIp(gateway)) {
            Monolith.UI.toast('Invalid gateway IP address', 'error');
            return;
        }

        try {
            // Gateway configuration should be set through interface configuration
            Monolith.UI.toast('Gateway configuration should be set through interface configuration. Please use the Interfaces page to configure gateway for WAN interface.', 'info');
        } catch (error) {
            console.error('Failed to set gateway:', error);
            Monolith.UI.toast(`Failed to set gateway: ${error.message || 'Unknown error'}`, 'error');
        }
    },

    testGateway: async function() {
        const gateway = $('#routing-gateway-address').val()?.trim() || '';
        
        if (!gateway) {
            Monolith.UI.toast('Please enter a gateway address to test', 'warning');
            return;
        }

        if (!this.isValidIp(gateway)) {
            Monolith.UI.toast('Invalid gateway IP address', 'error');
            return;
        }

        $('#routing-test-results').show();
        $('#routing-test-output').text(`Testing gateway ${gateway}...\n`);

        try {
            const response = await Monolith.API.post('/api/system/command', {
                command: 'ping',
                args: ['-c', '3', '-W', '2', gateway]
            });

            if (response.Success || response.success) {
                const output = response.Data?.StdOut || response.Data?.stdOut || response.Data || '';
                $('#routing-test-output').text(`Gateway Test Results for ${gateway}:\n\n${output}`);
            } else {
                $('#routing-test-output').text(`Gateway test failed: ${response.Error || response.error || 'Unknown error'}`);
            }
        } catch (error) {
            console.error('Failed to test gateway:', error);
            $('#routing-test-output').text(`Gateway test error: ${error.message || 'Unknown error'}`);
        }
    },

    runPingTest: async function() {
        const target = $('#routing-test-target').val()?.trim() || '';
        
        if (!target) {
            Monolith.UI.toast('Please enter a target to ping', 'warning');
            return;
        }

        $('#routing-test-results').show();
        $('#routing-test-output').text(`Pinging ${target}...\n`);

        try {
            const response = await Monolith.API.post('/api/system/command', {
                command: 'ping',
                args: ['-c', '4', '-W', '2', target]
            });

            if (response.Success || response.success) {
                const output = response.Data?.StdOut || response.Data?.stdOut || response.Data || '';
                $('#routing-test-output').text(`Ping Test Results for ${target}:\n\n${output}`);
            } else {
                const error = response.Data?.StdErr || response.Data?.stdErr || response.Error || response.error || 'Unknown error';
                $('#routing-test-output').text(`Ping test failed:\n${error}`);
            }
        } catch (error) {
            console.error('Failed to run ping test:', error);
            $('#routing-test-output').text(`Ping test error: ${error.message || 'Unknown error'}`);
        }
    },

    runTraceTest: async function() {
        const target = $('#routing-test-target').val()?.trim() || '';
        
        if (!target) {
            Monolith.UI.toast('Please enter a target to trace', 'warning');
            return;
        }

        $('#routing-test-results').show();
        $('#routing-test-output').text(`Tracing route to ${target}...\n`);

        try {
            // Try traceroute first, fallback to tracepath
            let command = 'traceroute';
            let args = ['-n', '-m', '15', target];

            const response = await Monolith.API.post('/api/system/command', {
                command: command,
                args: args
            });

            if (response.Success || response.success) {
                const output = response.Data?.StdOut || response.Data?.stdOut || response.Data || '';
                $('#routing-test-output').text(`Trace Route Results for ${target}:\n\n${output}`);
            } else {
                // Try tracepath as fallback
                const tracepathResponse = await Monolith.API.post('/api/system/command', {
                    command: 'tracepath',
                    args: ['-n', target]
                });

                if (tracepathResponse.Success || tracepathResponse.success) {
                    const output = tracepathResponse.Data?.StdOut || tracepathResponse.Data?.stdOut || tracepathResponse.Data || '';
                    $('#routing-test-output').text(`Trace Route Results for ${target}:\n\n${output}`);
                } else {
                    const error = tracepathResponse.Data?.StdErr || tracepathResponse.Data?.stdErr || tracepathResponse.Error || tracepathResponse.error || 'Unknown error';
                    $('#routing-test-output').text(`Trace route failed:\n${error}\n\nNote: Both traceroute and tracepath commands failed.`);
                }
            }
        } catch (error) {
            console.error('Failed to run trace test:', error);
            $('#routing-test-output').text(`Trace test error: ${error.message || 'Unknown error'}`);
        }
    },

    isValidIp: function(ip) {
        const ipv4Regex = /^(\d{1,3}\.){3}\d{1,3}$/;
        const ipv6Regex = /^([0-9a-fA-F]{1,4}:){7}[0-9a-fA-F]{1,4}$/;
        return ipv4Regex.test(ip) || ipv6Regex.test(ip);
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
