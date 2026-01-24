// Routing Status Page - Self-contained (no dependency on Status module)
var RoutingStatus = {
    init: function() {
        console.log('Initializing Routing Status module...');
        // Auto-render if we're on the routing-status page
        if (window.location.pathname.startsWith('/status/routing-status')) {
            this.renderPage();
        }
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
                        <h4 class="mb-0">Routing Status</h4>
                        <p class="text-muted mb-0 small">View current routing configuration and status</p>
                    </div>
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
                            <small class="text-muted">For more advanced routing settings, visit <a href="/system/advanced" data-route="/system/advanced">Advanced Settings</a></small>
                        </div>
                    </div>
                </div>
            </div>
        `);

        // Attach handlers
        $('#routing-enable-masquerade').off('click').on('click', async () => {
            await this.enableMasquerade();
        });
        
        $('#routing-apply-ip-forwarding').off('click').on('click', async () => {
            await this.applyIpForwarding();
        });
    },

    attachRoutingStatusHandlers: function() {
        $('#routing-status-refresh').off('click').on('click', () => this.loadRoutingStatus());
    },


    applyIpForwarding: async function() {
        const enabled = $('#routing-toggle-ip-forwarding').is(':checked');
        const value = enabled ? '1' : '0';

        try {
            const response = await Monolith.API.post('/api/system/tuneables/apply', {
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

    enableMasquerade: async function() {
        if (!confirm('Enable NAT Masquerade? This will allow LAN traffic to access the internet through WAN.')) {
            return;
        }

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
