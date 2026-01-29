// Routing Status Page - Self-contained (no dependency on Status module)
var RoutingStatus = {
    _signalRSubscribed: false,
    _gatewayStatusCache: {},

    init: function() {
        console.log('Initializing Routing Status module...');
        // Auto-render if we're on the routing-status page
        if (window.location.pathname.startsWith('/status/routing-status')) {
            this.renderPage();
            this._subscribeToSignalR();
        }
    },

    destroy: function() {
        this._unsubscribeFromSignalR();
    },

    _subscribeToSignalR: function() {
        if (this._signalRSubscribed || !Monolith.SignalR) return;

        const self = this;

        // Subscribe to gateways and routing channels
        Monolith.SignalR.subscribe('gateways', function(event, data) {
            if (event === 'GatewayStatusChanged') {
                self._handleGatewayStatusChanged(data);
            }
        });

        Monolith.SignalR.subscribe('routing', function(event, data) {
            if (event === 'RoutingStatusChanged') {
                // Reload full routing status when routes change
                self.loadRoutingStatus();
            }
        });

        this._signalRSubscribed = true;
        console.log('Routing Status: SignalR subscribed');
    },

    _unsubscribeFromSignalR: function() {
        if (!this._signalRSubscribed || !Monolith.SignalR) return;

        Monolith.SignalR.unsubscribe('gateways');
        Monolith.SignalR.unsubscribe('routing');

        this._signalRSubscribed = false;
        console.log('Routing Status: SignalR unsubscribed');
    },

    _handleGatewayStatusChanged: function(data) {
        // Update gateway status cache
        this._gatewayStatusCache[data.gatewayId] = data;

        // Update any gateway status display in the UI
        this._updateGatewayStatusDisplay(data);
    },

    _updateGatewayStatusDisplay: function(data) {
        // Find gateway elements by ID or name and update their status
        const gatewayName = data.name || data.gatewayId;
        const statusBadge = data.status === 'online'
            ? '<span class="badge bg-success">Online</span>'
            : data.status === 'degraded'
            ? '<span class="badge bg-warning">Degraded</span>'
            : '<span class="badge bg-danger">Offline</span>';

        // Update latency display if present
        const latencyText = data.latencyMs !== null && data.latencyMs !== undefined
            ? `${data.latencyMs}ms`
            : '-';

        // Find and update gateway row if it exists in the table
        const gatewayRow = $(`tr[data-gateway-id="${data.gatewayId}"], tr[data-gateway="${gatewayName}"]`);
        if (gatewayRow.length) {
            gatewayRow.find('.gateway-status').html(statusBadge);
            gatewayRow.find('.gateway-latency').text(latencyText);

            // Visual feedback for update
            gatewayRow.addClass('table-active');
            setTimeout(() => gatewayRow.removeClass('table-active'), 1000);
        }

        console.log(`Gateway status updated: ${gatewayName} - ${data.status} (${latencyText})`);
    },

    renderPage: function() {
        console.log('Rendering Routing Status page...');
        this.renderRoutingStatus();
    },

    renderRoutingStatus: function() {
        const container = $('#status-container, #page-content').first();
        if (!container.length) return;

        // Render page header first
        if (Monolith.PageHeader && typeof Monolith.PageHeader.render === 'function') {
            Monolith.PageHeader.render({
                title: "Routing Status",
                icon: "fa-route",
                description: "View current routing configuration and status",
                container: container,
                prepend: true
            });
        }

        // Render page content
        container.append(`
            <div class="container-fluid p-4">
                <div class="d-flex justify-content-between align-items-center mb-4">
                    <div>
                        <button type="button" class="btn btn-outline-primary" id="routing-status-refresh">
                            <i class="bi bi-arrow-clockwise me-1"></i> Refresh
                        </button>
                    </div>
                </div>

                <div class="card mb-4">
                    <div class="card-body" id="routing-status-content">
                        <div class="text-center text-muted py-4">
                            <div class="spinner-border spinner-border-sm me-2" role="status"></div>
                            Loading routing status...
                        </div>
                    </div>
                </div>
            </div>
        `);

        this.loadRoutingStatus();
        this.attachRoutingStatusHandlers();
    },

    loadRoutingStatus: async function() {
        try {
            const response = await Monolith.API.get('/api/routing/status');
            if (response.Success || response.success) {
                const data = response.Data || response.data || {};
                this.renderRoutingStatusContent(data);
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

    renderRoutingStatusContent: function(data) {
        const self = this;
        const ipForwarding = data.IpForwardingEnabled || data.ipForwardingEnabled || false;
        const defaultGateway = data.DefaultGateway || data.defaultGateway || null;
        const routes = data.Routes || data.routes || [];
        const natMasquerade = data.NatMasqueradeEnabled || data.natMasqueradeEnabled || false;
        const gateways = data.Gateways || data.gateways || [];

        // Default gateway info with status
        let gatewayInfo;
        if (defaultGateway) {
            const gwAddress = defaultGateway.Address || defaultGateway.address || 'N/A';
            const gwInterface = defaultGateway.Interface || defaultGateway.interface || 'N/A';
            const gwStatus = defaultGateway.Status || defaultGateway.status || 'unknown';
            const gwLatency = defaultGateway.LatencyMs || defaultGateway.latencyMs;
            const statusBadge = gwStatus === 'online'
                ? '<span class="badge bg-success gateway-status">Online</span>'
                : gwStatus === 'degraded'
                ? '<span class="badge bg-warning gateway-status">Degraded</span>'
                : '<span class="badge bg-danger gateway-status">Offline</span>';
            const latencyText = gwLatency !== null && gwLatency !== undefined
                ? `<span class="gateway-latency">${gwLatency}ms</span>`
                : '';

            gatewayInfo = `
                <div class="d-flex align-items-center gap-2" data-gateway="${gwAddress}">
                    <strong>${gwAddress}</strong> via <code>${gwInterface}</code>
                    ${statusBadge}
                    ${latencyText}
                </div>
            `;
        } else {
            gatewayInfo = '<span class="text-muted">No default gateway configured</span>';
        }

        // Gateways table (if multiple gateways configured)
        let gatewaysHtml = '';
        if (gateways.length > 0) {
            const gwRows = gateways.map(gw => {
                const id = gw.Id || gw.id || '';
                const name = gw.Name || gw.name || id;
                const address = gw.Address || gw.address || '-';
                const iface = gw.Interface || gw.interface || '-';
                const status = gw.Status || gw.status || 'unknown';
                const latency = gw.LatencyMs || gw.latencyMs;
                const packetLoss = gw.PacketLossPercent || gw.packetLossPercent;

                const statusBadge = status === 'online'
                    ? '<span class="badge bg-success gateway-status">Online</span>'
                    : status === 'degraded'
                    ? '<span class="badge bg-warning gateway-status">Degraded</span>'
                    : '<span class="badge bg-danger gateway-status">Offline</span>';

                const latencyText = latency !== null && latency !== undefined ? `${latency}ms` : '-';
                const lossText = packetLoss !== null && packetLoss !== undefined ? `${packetLoss.toFixed(1)}%` : '-';

                return `
                    <tr data-gateway-id="${id}" data-gateway="${name}">
                        <td>${name}</td>
                        <td>${address}</td>
                        <td>${iface}</td>
                        <td class="gateway-status-cell">${statusBadge}</td>
                        <td class="gateway-latency">${latencyText}</td>
                        <td class="gateway-loss">${lossText}</td>
                    </tr>
                `;
            }).join('');

            gatewaysHtml = `
                <div class="mb-4">
                    <h6>Configured Gateways</h6>
                    <div class="table-responsive">
                        <table class="table table-sm table-hover">
                            <thead>
                                <tr>
                                    <th>Name</th>
                                    <th>Address</th>
                                    <th>Interface</th>
                                    <th>Status</th>
                                    <th>Latency</th>
                                    <th>Packet Loss</th>
                                </tr>
                            </thead>
                            <tbody>
                                ${gwRows}
                            </tbody>
                        </table>
                    </div>
                </div>
            `;
        }

        // Routes table
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

        // System status indicators
        const forwardingBadge = ipForwarding
            ? '<span class="badge bg-success">Enabled</span>'
            : '<span class="badge bg-secondary">Disabled</span>';
        const natBadge = natMasquerade
            ? '<span class="badge bg-success">Enabled</span>'
            : '<span class="badge bg-secondary">Disabled</span>';

        $('#routing-status-content').html(`
            <div class="row mb-4">
                <div class="col-md-6">
                    <div class="d-flex align-items-center gap-2 mb-2">
                        <span class="text-muted">IP Forwarding:</span>
                        ${forwardingBadge}
                    </div>
                    <div class="d-flex align-items-center gap-2">
                        <span class="text-muted">NAT Masquerade:</span>
                        ${natBadge}
                    </div>
                </div>
            </div>
            <div class="mb-4">
                <h6>Default Gateway</h6>
                <p class="mb-0">${gatewayInfo}</p>
            </div>
            ${gatewaysHtml}
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
        `);
    },

    attachRoutingStatusHandlers: function() {
        $('#routing-status-refresh').off('click').on('click', () => this.loadRoutingStatus());
    }
};

// Register module immediately
(function() {
    if (typeof Monolith === 'undefined') {
        window.Monolith = {};
    }
    if (typeof Monolith.Pages === 'undefined') {
        Monolith.Pages = {};
    }
    Monolith.Pages.RoutingStatus = RoutingStatus;
    Monolith.Pages['routing-status'] = RoutingStatus; // Also register lowercase for router compatibility
    console.log('Routing Status module registered:', typeof Monolith.Pages.RoutingStatus);
})();
