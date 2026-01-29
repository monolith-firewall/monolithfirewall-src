// Routing Page - Gateways + Static Routes with real-time SignalR updates
var Routing = {
    gateways: [],
    routes: [],
    interfaces: [],
    _signalRHandler: null,

    init: function() {
        console.log('Initializing Routing page...');
        this.render();
        this.loadData();
        this.attachHandlers();
        this._subscribeToSignalR();
    },

    destroy: function() {
        this._unsubscribeFromSignalR();
    },

    _subscribeToSignalR: function() {
        if (!Monolith.SignalR) {
            console.log('[Routing] SignalR not available');
            return;
        }

        this._signalRHandler = (eventName, data) => {
            switch (eventName) {
                case 'GatewayStatusChanged':
                    this.updateGatewayRow(data.gatewayId, data);
                    break;
                case 'GatewayStatusBatch':
                    if (Array.isArray(data)) {
                        data.forEach(item => this.updateGatewayRow(item.gatewayId, item));
                    }
                    break;
                case 'RoutingTableChanged':
                    // Reload routes when routing table changes
                    this.loadRoutes();
                    break;
            }
        };

        Monolith.SignalR.subscribe('gateways', this._signalRHandler);
        Monolith.SignalR.subscribe('routing', this._signalRHandler);
        console.log('[Routing] Subscribed to SignalR gateways/routing channels');
    },

    _unsubscribeFromSignalR: function() {
        if (Monolith.SignalR && this._signalRHandler) {
            Monolith.SignalR.unsubscribe('gateways', this._signalRHandler);
            Monolith.SignalR.unsubscribe('routing', this._signalRHandler);
            this._signalRHandler = null;
            console.log('[Routing] Unsubscribed from SignalR');
        }
    },

    /**
     * Update a single gateway row in real-time
     */
    updateGatewayRow: function(gatewayId, status) {
        const row = $(`#gateways-body tr[data-gateway-id="${gatewayId}"]`);
        if (!row.length) return;

        // Update status indicator if there is one
        const statusCell = row.find('.gateway-status');
        if (statusCell.length && status.status) {
            let statusClass = 'bg-secondary';
            let statusText = status.status;
            if (status.status === 'online') {
                statusClass = 'bg-success';
            } else if (status.status === 'offline') {
                statusClass = 'bg-danger';
            } else if (status.status === 'degraded') {
                statusClass = 'bg-warning';
            }
            statusCell.removeClass('bg-success bg-danger bg-warning bg-secondary')
                      .addClass(statusClass)
                      .text(statusText.toUpperCase());
        }

        // Update latency if shown
        const latencyCell = row.find('.gateway-latency');
        if (latencyCell.length && status.latencyMs !== undefined) {
            latencyCell.text(status.latencyMs !== null ? `${status.latencyMs}ms` : '-');
        }

        // Flash highlight
        row.addClass('table-info');
        setTimeout(() => row.removeClass('table-info'), 1500);

        // Update local state
        const gateway = this.gateways.find(g => g.id === gatewayId);
        if (gateway) {
            gateway.status = status.status;
            gateway.latencyMs = status.latencyMs;
        }

        console.log(`[Routing] Real-time update: Gateway ${gatewayId} = ${status.status}`);
    },

    render: function() {
        const container = $('#page-content');
        
        // Render page header
        if (Monolith.PageHeader && typeof Monolith.PageHeader.render === 'function') {
            Monolith.PageHeader.render({
                title: "Routing",
                icon: "fa-route",
                description: "Gateways and static route management",
                container: container,
                prepend: true
            });
        }

        // Add toolbar with refresh button under header
        container.append(`
            <div class="container-fluid p-4 routing-shell">
                <div class="d-flex justify-content-end mb-3">
                    <button class="btn btn-outline-primary" id="routing-refresh">Refresh</button>
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
                    <div class="tab-pane fade show active" id="routing-gateways" role="tabpanel">
                        <div class="card mb-4">
                            <div class="card-header d-flex justify-content-between align-items-center">
                                <span class="fw-semibold">Gateways</span>
                                <div class="d-flex align-items-center gap-2">
                                    <span class="text-muted small" id="gateway-count">0 gateways</span>
                                    <button class="btn btn-sm btn-primary" id="routing-add-gateway">Add Gateway</button>
                                </div>
                            </div>
                            <div class="table-responsive">
                                <table class="table table-hover align-middle mb-0">
                                    <thead>
                                        <tr>
                                            <th>Name</th>
                                            <th>Gateway</th>
                                            <th>Interface</th>
                                            <th>Family</th>
                                            <th>Source</th>
                                            <th>Metric</th>
                                            <th class="text-end">Actions</th>
                                        </tr>
                                    </thead>
                                    <tbody id="gateways-body">
                                        <tr><td colspan="7" class="text-center text-muted py-4">Loading gateways...</td></tr>
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
                                            <th>Family</th>
                                            <th>Interface</th>
                                            <th>Metric</th>
                                            <th>Status</th>
                                            <th>Description</th>
                                            <th class="text-end">Actions</th>
                                        </tr>
                                    </thead>
                                    <tbody id="routes-body">
                                        <tr><td colspan="8" class="text-center text-muted py-4">Loading routes...</td></tr>
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
        // Use .off() first to prevent duplicate handlers on re-init
        $('#routing-refresh').off('click').on('click', () => this.loadData());
        $('#routing-add-route').off('click').on('click', () => this.showRouteModal());
        $('#routing-add-gateway').off('click').on('click', () => this.showGatewayModal());

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
            tbody.html('<tr><td colspan="7" class="text-center text-danger py-4">Failed to load gateways</td></tr>');
            return;
        }

        if (!this.gateways.length) {
            tbody.html('<tr><td colspan="7" class="text-center text-muted py-4">No gateways detected</td></tr>');
            return;
        }

        let html = '';
        this.gateways.forEach(gw => {
            // Determine source: dynamic if IsDynamic is true, otherwise static
            const isDynamic = gw.IsDynamic || gw.isDynamic || false;
            const source = isDynamic ? 'dynamic' : 'static';
            const sourceBadge = source === 'dynamic'
                ? '<span class="badge bg-info-subtle text-info border">Dynamic</span>'
                : '<span class="badge bg-light text-muted border">Static</span>';
            const metric = gw.Metric !== undefined ? gw.Metric : gw.metric;
            const isDefault = gw.IsDefault || gw.isDefault;
            const family = (gw.AddressFamily || gw.addressFamily || '').toLowerCase();
            const familyBadge = family === 'ipv6'
                ? '<span class="badge bg-primary-subtle text-primary border">IPv6</span>'
                : '<span class="badge bg-secondary-subtle text-secondary border">IPv4</span>';
            const defaultBadge = isDefault ? '<span class="badge bg-success-subtle text-success border ms-1">Default</span>' : '';
            const canDelete = !isDynamic;
            
            // Get interface name - show it properly, not "-"
            const interfaceName = gw.Interface || gw.interface;
            const interfaceDisplay = interfaceName && interfaceName.trim() ? interfaceName : '<span class="text-muted">-</span>';

            html += `
                <tr>
                    <td>
                        <div class="fw-semibold">${gw.Name || gw.name || 'Unnamed'}</div>
                        ${defaultBadge}
                    </td>
                    <td><code>${gw.Address || gw.address || 'N/A'}</code></td>
                    <td>${interfaceDisplay}</td>
                    <td>${familyBadge}</td>
                    <td>${sourceBadge}</td>
                    <td>${metric !== undefined && metric !== null ? metric : '-'}</td>
                    <td class="text-end">
                        <button class="btn btn-sm btn-outline-danger" ${canDelete ? '' : 'disabled'} onclick="Routing.deleteGateway(${gw.Id || gw.id})">
                            Delete
                        </button>
                    </td>
                </tr>
            `;
        });

        tbody.html(html);
    },

    renderRoutes: function(isError) {
        const tbody = $('#routes-body');
        if (isError) {
            tbody.html('<tr><td colspan="8" class="text-center text-danger py-4">Failed to load routes</td></tr>');
            return;
        }

        if (!this.routes.length) {
            tbody.html('<tr><td colspan="8" class="text-center text-muted py-4">No static routes configured</td></tr>');
            return;
        }

        let html = '';
        this.routes.forEach(route => {
            const active = route.Active || route.active;
            const statusBadge = active
                ? '<span class="badge bg-success">Active</span>'
                : '<span class="badge bg-secondary">Inactive</span>';
            const metric = route.Metric !== undefined ? route.Metric : route.metric;
            const family = (route.AddressFamily || route.addressFamily || 'ipv4').toLowerCase();
            const familyBadge = family === 'ipv6'
                ? '<span class="badge bg-primary-subtle text-primary border">IPv6</span>'
                : '<span class="badge bg-secondary-subtle text-secondary border">IPv4</span>';

            html += `
                <tr>
                    <td><code>${route.Destination || route.destination}</code></td>
                    <td>${route.Gateway || route.gateway || '-'}</td>
                    <td>${familyBadge}</td>
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

    showGatewayModal: function() {
        const interfaceOptions = this.buildOptions(this.interfaces, '', 'Select interface (optional)');

        const body = `
            <form id="gateway-form">
                <div class="mb-3">
                    <label class="form-label">Name</label>
                    <input type="text" class="form-control" id="gateway-name" placeholder="ISP Gateway">
                </div>
                <div class="mb-3">
                    <label class="form-label">Gateway Address</label>
                    <input type="text" class="form-control" id="gateway-address" placeholder="192.0.2.1 or 2001:db8::1">
                    <div class="form-text">Supports IPv4 or IPv6.</div>
                </div>
                <div class="mb-3">
                    <label class="form-label">Interface</label>
                    <select class="form-select" id="gateway-interface">
                        ${interfaceOptions}
                    </select>
                </div>
                <div class="row g-2 mb-3">
                    <div class="col-md-6">
                        <label class="form-label">Metric</label>
                        <input type="number" class="form-control" id="gateway-metric" min="0" placeholder="Optional">
                    </div>
                    <div class="col-md-6 d-flex align-items-end">
                        <div class="form-check">
                            <input class="form-check-input" type="checkbox" id="gateway-default">
                            <label class="form-check-label" for="gateway-default">Set as default</label>
                        </div>
                    </div>
                </div>
                <div class="mb-3">
                    <label class="form-label">Description</label>
                    <input type="text" class="form-control" id="gateway-description" placeholder="Optional">
                </div>
            </form>
        `;

        const footer = `
            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
            <button type="button" class="btn btn-primary" id="gateway-save-btn">Add Gateway</button>
        `;

        const modal = Monolith.UI.showModal('Add Gateway', body, { size: 'lg', footerHtml: footer, staticBackdrop: true });
        modal.element.find('#gateway-save-btn').on('click', async () => {
            const name = modal.element.find('#gateway-name').val()?.trim();
            const address = modal.element.find('#gateway-address').val()?.trim();
            const iface = modal.element.find('#gateway-interface').val();
            const metricVal = modal.element.find('#gateway-metric').val();
            const isDefault = modal.element.find('#gateway-default').is(':checked');
            const description = modal.element.find('#gateway-description').val()?.trim();

            if (!name) {
                Monolith.UI.toast('Name is required', 'warning');
                return;
            }

            if (!address) {
                Monolith.UI.toast('Gateway address is required', 'warning');
                return;
            }

            const payload = {
                name,
                address,
                interface: iface || null,
                metric: metricVal ? parseInt(metricVal, 10) : null,
                isDefault: isDefault,
                description: description || null
            };

            try {
                const response = await Monolith.API.post('/routing/gateways', payload);
                if (!(response.Success || response.success)) {
                    throw new Error(response.Error || response.error || 'Failed to add gateway');
                }
                Monolith.UI.toast('Gateway added', 'success');
                modal.instance.hide();
                this.loadGateways();
            } catch (error) {
                console.error('Add gateway failed:', error);
                Monolith.UI.toast(error.message || 'Failed to add gateway', 'error');
            }
        });
    },

    deleteGateway: function(id) {
        if (!confirm('Delete this gateway?')) {
            return;
        }

        Monolith.API.delete(`/routing/gateways/${id}`)
            .then(response => {
                if (!(response.Success || response.success)) {
                    throw new Error(response.Error || response.error || 'Failed to delete gateway');
                }
                Monolith.UI.toast('Gateway deleted', 'success');
                this.loadGateways();
            })
            .catch(error => {
                console.error('Failed to delete gateway:', error);
                Monolith.UI.toast(error.message || 'Failed to delete gateway', 'error');
            });
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
                    <label class="form-label">Address Family</label>
                    <select class="form-select" id="route-family">
                        <option value="auto" selected>Auto (from CIDR)</option>
                        <option value="ipv4">IPv4</option>
                        <option value="ipv6">IPv6</option>
                    </select>
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
            const family = modal.element.find('#route-family').val();

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
                description: description || null,
                addressFamily: family && family !== 'auto' ? family : null
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

    renderFamilyBadge: function(family) {
        const fam = (family || 'ipv4').toLowerCase();
        return fam === 'ipv6'
            ? '<span class="badge bg-primary-subtle text-primary border">IPv6</span>'
            : '<span class="badge bg-secondary-subtle text-secondary border">IPv4</span>';
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
