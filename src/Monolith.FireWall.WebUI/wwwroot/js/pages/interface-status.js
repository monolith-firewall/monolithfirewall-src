// Interface Status Page - Real-time interface operational state monitoring

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

var InterfaceStatus = {
    interfaces: [],
    operationalState: {},
    _signalRHandler: null,
    _refreshInterval: null,

    init: function() {
        console.log('Initializing Interface Status page...');
        this.render();
        this.loadData();
        this._subscribeToSignalR();
        this._startAutoRefresh();
    },

    destroy: function() {
        this._unsubscribeFromSignalR();
        this._stopAutoRefresh();
    },

    _subscribeToSignalR: function() {
        if (!Monolith.SignalR) return;

        this._signalRHandler = (eventName, data) => {
            switch (eventName) {
                case 'InterfaceLinkChanged':
                    this.handleLinkChange(data);
                    break;
                case 'InterfaceIpChanged':
                    this.handleIpChange(data);
                    break;
                case 'InterfaceStateChanged':
                    this.handleStateChange(data);
                    break;
            }
        };

        Monolith.SignalR.subscribe('interfaces', this._signalRHandler);
        console.log('[InterfaceStatus] Subscribed to SignalR');
    },

    _unsubscribeFromSignalR: function() {
        if (Monolith.SignalR && this._signalRHandler) {
            Monolith.SignalR.unsubscribe('interfaces', this._signalRHandler);
            this._signalRHandler = null;
        }
    },

    _startAutoRefresh: function() {
        // Refresh data every 30 seconds
        this._refreshInterval = setInterval(() => {
            this.loadOperationalState();
        }, 30000);
    },

    _stopAutoRefresh: function() {
        if (this._refreshInterval) {
            clearInterval(this._refreshInterval);
            this._refreshInterval = null;
        }
    },

    render: function() {
        // Render standardized page header
        if (Monolith.PageHeader && typeof Monolith.PageHeader.render === 'function') {
            Monolith.PageHeader.render({
                title: "Interface Status",
                icon: "fa-ethernet",
                description: "Real-time interface operational state and health",
                container: '#interface-status-container',
                prepend: false
            });
        }

        var html = `
            <div class="d-flex justify-content-end mb-3">
                <div class="btn-group">
                    <button class="btn btn-outline-primary" onclick="InterfaceStatus.loadData()" title="Refresh">
                        <i class="fa-solid fa-arrows-rotate"></i>
                    </button>
                    <button class="btn btn-primary" onclick="InterfaceStatus.refreshAllInterfaces()">
                        <i class="fa-solid fa-tower-broadcast me-1"></i>Refresh All
                    </button>
                </div>
            </div>

            <!-- Summary Cards -->
            <div class="row mb-4" id="interface-summary">
                <div class="col-md-3">
                    <div class="card bg-success bg-opacity-10 border-success">
                        <div class="card-body text-center">
                            <h3 class="mb-0" id="summary-up">-</h3>
                            <small class="text-success">Link Up</small>
                        </div>
                    </div>
                </div>
                <div class="col-md-3">
                    <div class="card bg-danger bg-opacity-10 border-danger">
                        <div class="card-body text-center">
                            <h3 class="mb-0" id="summary-down">-</h3>
                            <small class="text-danger">Link Down</small>
                        </div>
                    </div>
                </div>
                <div class="col-md-3">
                    <div class="card bg-info bg-opacity-10 border-info">
                        <div class="card-body text-center">
                            <h3 class="mb-0" id="summary-dhcp">-</h3>
                            <small class="text-info">DHCP Active</small>
                        </div>
                    </div>
                </div>
                <div class="col-md-3">
                    <div class="card bg-secondary bg-opacity-10 border-secondary">
                        <div class="card-body text-center">
                            <h3 class="mb-0" id="summary-total">-</h3>
                            <small class="text-secondary">Total</small>
                        </div>
                    </div>
                </div>
            </div>

            <!-- Interface Cards -->
            <div id="interfaces-grid" class="row g-4">
                <div class="col-12 text-center py-4">
                    <div class="spinner-border spinner-border-sm"></div> Loading interfaces...
                </div>
            </div>

            <!-- Interface Detail Modal -->
            <div class="modal fade" id="interface-detail-modal" tabindex="-1">
                <div class="modal-dialog modal-lg">
                    <div class="modal-content">
                        <div class="modal-header">
                            <h5 class="modal-title">Interface Details</h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                        </div>
                        <div class="modal-body" id="interface-detail-content">
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Close</button>
                        </div>
                    </div>
                </div>
            </div>
        `;
        $('#interface-status-container').append(html);
    },

    loadData: async function() {
        try {
            // Load interface assignments
            const ifResponse = await Monolith.Core.call('interfaces.list', {});
            this.interfaces = ifResponse.data || [];

            // Load operational state
            await this.loadOperationalState();
        } catch (error) {
            console.error('Failed to load interface data:', error);
            $('#interfaces-grid').html('<div class="col-12 text-center text-danger py-4">Failed to load data</div>');
        }
    },

    loadOperationalState: async function() {
        try {
            const stateResponse = await Monolith.Core.call('operational.interfaces.list', {});
            var stateList = stateResponse.data || [];

            // Index by interface name
            this.operationalState = {};
            stateList.forEach(s => {
                this.operationalState[s.interfaceName] = s;
            });

            this.renderInterfaces();
            this.updateSummary();
        } catch (error) {
            console.error('Failed to load operational state:', error);
        }
    },

    renderInterfaces: function() {
        if (this.interfaces.length === 0) {
            $('#interfaces-grid').html('<div class="col-12 text-center text-muted py-4">No interfaces configured</div>');
            return;
        }

        var html = this.interfaces.map(iface => {
            var state = this.operationalState[iface.interfaceName] || {};
            return this.renderInterfaceCard(iface, state);
        }).join('');

        $('#interfaces-grid').html(html);
    },

    renderInterfaceCard: function(iface, state) {
        var linkState = state.linkState || 'unknown';
        var healthStatus = state.healthStatus || 'unknown';

        var linkClass = linkState === 'up' ? 'success' : linkState === 'down' ? 'danger' : 'secondary';
        var linkIcon = linkState === 'up' ? 'fa-solid fa-circle-check' : linkState === 'down' ? 'fa-solid fa-circle-xmark' : 'fa-solid fa-circle-question';

        var healthClass = healthStatus === 'healthy' ? 'success' : healthStatus === 'degraded' ? 'warning' : healthStatus === 'down' ? 'danger' : 'secondary';

        var roleLabel = iface.role || 'Unassigned';
        var roleBadgeClass = iface.role === 'WAN' ? 'bg-primary' : iface.role === 'LAN' ? 'bg-success' : 'bg-secondary';

        var ipv4Display = state.currentIpv4Address ?
            `${state.currentIpv4Address}/${state.currentIpv4Prefix || 24}` :
            '<span class="text-muted">No IPv4</span>';

        var ipv6Display = state.currentIpv6Address ?
            `<small class="text-muted">${this.truncateIpv6(state.currentIpv6Address)}</small>` : '';

        var dhcpInfo = '';
        if (iface.addressMode === 'dhcp' && state.dhcpLeaseExpires) {
            var leaseExpires = new Date(state.dhcpLeaseExpires);
            var now = new Date();
            var remaining = Math.max(0, Math.floor((leaseExpires - now) / 1000 / 60));
            dhcpInfo = `<div class="mt-2"><small class="text-info"><i class="fa-solid fa-tower-broadcast me-1"></i>DHCP lease: ${remaining}m remaining</small></div>`;
        }

        var speedInfo = state.speedMbps ? `${state.speedMbps} Mbps ${state.duplex || ''}` : '';

        var trafficStats = '';
        if (state.trafficStats) {
            var rxMB = (state.trafficStats.rxBytes / 1024 / 1024).toFixed(1);
            var txMB = (state.trafficStats.txBytes / 1024 / 1024).toFixed(1);
            trafficStats = `
                <div class="d-flex justify-content-between mt-2 text-muted small">
                    <span><i class="fa-solid fa-arrow-down text-success"></i> ${rxMB} MB</span>
                    <span><i class="fa-solid fa-arrow-up text-primary"></i> ${txMB} MB</span>
                </div>
            `;
        }

        return `
            <div class="col-md-6 col-lg-4">
                <div class="card h-100 border-${linkClass}">
                    <div class="card-header d-flex justify-content-between align-items-center bg-${linkClass} bg-opacity-10">
                        <div>
                            <i class="${linkIcon} text-${linkClass} me-2"></i>
                            <strong>${this.escapeHtml(iface.friendlyName || iface.interfaceName)}</strong>
                        </div>
                        <span class="badge ${roleBadgeClass}">${roleLabel}</span>
                    </div>
                    <div class="card-body">
                        <div class="mb-2">
                            <small class="text-muted">Interface:</small>
                            <code class="ms-1">${this.escapeHtml(iface.interfaceName)}</code>
                        </div>
                        <div class="mb-2">
                            <small class="text-muted">IPv4:</small>
                            <span class="ms-1">${ipv4Display}</span>
                        </div>
                        ${ipv6Display ? `<div class="mb-2">${ipv6Display}</div>` : ''}
                        ${state.macAddress ? `
                            <div class="mb-2">
                                <small class="text-muted">MAC:</small>
                                <code class="ms-1 small">${state.macAddress}</code>
                            </div>
                        ` : ''}
                        ${speedInfo ? `
                            <div class="mb-2">
                                <small class="text-muted">Speed:</small>
                                <span class="ms-1">${speedInfo}</span>
                            </div>
                        ` : ''}
                        <div class="mb-2">
                            <small class="text-muted">Health:</small>
                            <span class="badge bg-${healthClass} ms-1">${healthStatus}</span>
                        </div>
                        ${dhcpInfo}
                        ${trafficStats}
                    </div>
                    <div class="card-footer bg-transparent">
                        <div class="btn-group btn-group-sm w-100">
                            <button class="btn btn-outline-primary" onclick="InterfaceStatus.refreshInterface('${iface.interfaceName}')" title="Refresh">
                                <i class="fa-solid fa-arrows-rotate"></i>
                            </button>
                            <button class="btn btn-outline-secondary" onclick="InterfaceStatus.showDetails('${iface.interfaceName}')" title="Details">
                                <i class="fa-solid fa-circle-info"></i>
                            </button>
                        </div>
                        <small class="text-muted d-block text-center mt-2">
                            Last seen: ${state.lastSeenAt ? this.formatRelativeTime(state.lastSeenAt) : 'Never'}
                        </small>
                    </div>
                </div>
            </div>
        `;
    },

    updateSummary: function() {
        var counts = { up: 0, down: 0, dhcp: 0, total: 0 };

        this.interfaces.forEach(iface => {
            counts.total++;
            var state = this.operationalState[iface.interfaceName] || {};
            if (state.linkState === 'up') counts.up++;
            else if (state.linkState === 'down') counts.down++;

            if (iface.addressMode === 'dhcp' && state.dhcpLeaseExpires) {
                counts.dhcp++;
            }
        });

        $('#summary-up').text(counts.up);
        $('#summary-down').text(counts.down);
        $('#summary-dhcp').text(counts.dhcp);
        $('#summary-total').text(counts.total);
    },

    handleLinkChange: function(data) {
        console.log('[InterfaceStatus] Link change:', data);
        var state = this.operationalState[data.interfaceName] || {};
        state.linkState = data.newState;
        state.lastLinkChangeAt = new Date().toISOString();
        this.operationalState[data.interfaceName] = state;
        this.renderInterfaces();
        this.updateSummary();

        var statusText = data.newState === 'up' ? 'is now up' : 'went down';
        Monolith.Toast.info(`Interface ${data.interfaceName} ${statusText}`);
    },

    handleIpChange: function(data) {
        console.log('[InterfaceStatus] IP change:', data);
        var state = this.operationalState[data.interfaceName] || {};
        if (data.family === 'ipv4') {
            state.currentIpv4Address = data.newAddress;
            state.currentIpv4Prefix = data.prefix;
        } else if (data.family === 'ipv6') {
            state.currentIpv6Address = data.newAddress;
            state.currentIpv6Prefix = data.prefix;
        }
        state.lastIpChangeAt = new Date().toISOString();
        this.operationalState[data.interfaceName] = state;
        this.renderInterfaces();

        Monolith.Toast.info(`Interface ${data.interfaceName} IP changed to ${data.newAddress}`);
    },

    handleStateChange: function(data) {
        console.log('[InterfaceStatus] State change:', data);
        this.operationalState[data.interfaceName] = data;
        this.renderInterfaces();
        this.updateSummary();
    },

    refreshInterface: async function(interfaceName) {
        var card = $(`[onclick*="refreshInterface('${interfaceName}')"]`).closest('.card');
        card.addClass('opacity-50');

        try {
            var response = await Monolith.Core.call('operational.interfaces.refresh', { interfaceName: interfaceName });
            if (response.success && response.data) {
                this.operationalState[interfaceName] = response.data;
                this.renderInterfaces();
                this.updateSummary();
                Monolith.Toast.success(`Refreshed ${interfaceName}`);
            } else {
                Monolith.Toast.error('Refresh failed: ' + (response.error || 'Unknown error'));
            }
        } catch (error) {
            console.error('Refresh failed:', error);
            Monolith.Toast.error('Failed to refresh interface');
        } finally {
            card.removeClass('opacity-50');
        }
    },

    refreshAllInterfaces: async function() {
        var btn = $('button:contains("Refresh All")');
        btn.prop('disabled', true).html('<span class="spinner-border spinner-border-sm me-1"></span>Refreshing...');

        try {
            for (var iface of this.interfaces) {
                await this.refreshInterface(iface.interfaceName);
            }
            Monolith.Toast.success('All interfaces refreshed');
        } catch (error) {
            console.error('Refresh all failed:', error);
        } finally {
            btn.prop('disabled', false).html('<i class="fa-solid fa-tower-broadcast me-1"></i>Refresh All');
        }
    },

    showDetails: function(interfaceName) {
        var iface = this.interfaces.find(i => i.interfaceName === interfaceName);
        var state = this.operationalState[interfaceName] || {};

        var html = `
            <div class="row">
                <div class="col-md-6">
                    <h6>Configuration</h6>
                    <table class="table table-sm">
                        <tr><td class="text-muted">Interface Name</td><td><code>${this.escapeHtml(interfaceName)}</code></td></tr>
                        <tr><td class="text-muted">Friendly Name</td><td>${this.escapeHtml(iface?.friendlyName || '-')}</td></tr>
                        <tr><td class="text-muted">Role</td><td>${this.escapeHtml(iface?.role || 'Unassigned')}</td></tr>
                        <tr><td class="text-muted">Address Mode</td><td>${this.escapeHtml(iface?.addressMode || '-')}</td></tr>
                        <tr><td class="text-muted">Configured IPv4</td><td>${this.escapeHtml(iface?.ipv4Address || '-')}</td></tr>
                        <tr><td class="text-muted">Configured Gateway</td><td>${this.escapeHtml(iface?.ipv4Gateway || '-')}</td></tr>
                    </table>
                </div>
                <div class="col-md-6">
                    <h6>Operational State</h6>
                    <table class="table table-sm">
                        <tr><td class="text-muted">Link State</td><td><span class="badge bg-${state.linkState === 'up' ? 'success' : 'danger'}">${state.linkState || 'unknown'}</span></td></tr>
                        <tr><td class="text-muted">Health Status</td><td><span class="badge bg-${state.healthStatus === 'healthy' ? 'success' : 'warning'}">${state.healthStatus || 'unknown'}</span></td></tr>
                        <tr><td class="text-muted">Current IPv4</td><td>${state.currentIpv4Address ? `${state.currentIpv4Address}/${state.currentIpv4Prefix}` : '-'}</td></tr>
                        <tr><td class="text-muted">Current IPv6</td><td>${state.currentIpv6Address || '-'}</td></tr>
                        <tr><td class="text-muted">MAC Address</td><td><code>${state.macAddress || '-'}</code></td></tr>
                        <tr><td class="text-muted">Speed</td><td>${state.speedMbps ? `${state.speedMbps} Mbps` : '-'}</td></tr>
                        <tr><td class="text-muted">Duplex</td><td>${state.duplex || '-'}</td></tr>
                        <tr><td class="text-muted">MTU</td><td>${state.mtu || '-'}</td></tr>
                    </table>
                </div>
            </div>
            ${state.dhcpLeaseObtained ? `
            <div class="row mt-3">
                <div class="col-12">
                    <h6>DHCP Lease</h6>
                    <table class="table table-sm">
                        <tr><td class="text-muted">Server</td><td>${state.dhcpServerAddress || '-'}</td></tr>
                        <tr><td class="text-muted">Gateway</td><td>${state.dhcpGateway || '-'}</td></tr>
                        <tr><td class="text-muted">Obtained</td><td>${state.dhcpLeaseObtained ? new Date(state.dhcpLeaseObtained).toLocaleString() : '-'}</td></tr>
                        <tr><td class="text-muted">Expires</td><td>${state.dhcpLeaseExpires ? new Date(state.dhcpLeaseExpires).toLocaleString() : '-'}</td></tr>
                    </table>
                </div>
            </div>
            ` : ''}
            ${state.trafficStats ? `
            <div class="row mt-3">
                <div class="col-12">
                    <h6>Traffic Statistics</h6>
                    <table class="table table-sm">
                        <tr>
                            <td class="text-muted">RX</td>
                            <td>${this.formatBytes(state.trafficStats.rxBytes)} (${state.trafficStats.rxPackets} packets, ${state.trafficStats.rxErrors} errors)</td>
                        </tr>
                        <tr>
                            <td class="text-muted">TX</td>
                            <td>${this.formatBytes(state.trafficStats.txBytes)} (${state.trafficStats.txPackets} packets, ${state.trafficStats.txErrors} errors)</td>
                        </tr>
                    </table>
                </div>
            </div>
            ` : ''}
            <div class="row mt-3">
                <div class="col-12">
                    <h6>Timestamps</h6>
                    <table class="table table-sm">
                        <tr><td class="text-muted">Last Seen</td><td>${state.lastSeenAt ? new Date(state.lastSeenAt).toLocaleString() : 'Never'}</td></tr>
                        <tr><td class="text-muted">Last Link Change</td><td>${state.lastLinkChangeAt ? new Date(state.lastLinkChangeAt).toLocaleString() : 'Never'}</td></tr>
                        <tr><td class="text-muted">Last IP Change</td><td>${state.lastIpChangeAt ? new Date(state.lastIpChangeAt).toLocaleString() : 'Never'}</td></tr>
                    </table>
                </div>
            </div>
        `;

        $('#interface-detail-modal .modal-title').text(`Interface Details - ${iface?.friendlyName || interfaceName}`);
        $('#interface-detail-content').html(html);
        new bootstrap.Modal('#interface-detail-modal').show();
    },

    truncateIpv6: function(ipv6) {
        if (!ipv6 || ipv6.length <= 20) return ipv6;
        return ipv6.substring(0, 17) + '...';
    },

    formatBytes: function(bytes) {
        if (!bytes) return '0 B';
        var sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
        var i = Math.floor(Math.log(bytes) / Math.log(1024));
        return (bytes / Math.pow(1024, i)).toFixed(2) + ' ' + sizes[i];
    },

    formatRelativeTime: function(dateStr) {
        if (!dateStr) return 'Never';
        var date = new Date(dateStr);
        var now = new Date();
        var diffMs = now - date;
        var diffSec = Math.floor(diffMs / 1000);

        if (diffSec < 60) return 'Just now';
        if (diffSec < 3600) return `${Math.floor(diffSec / 60)}m ago`;
        if (diffSec < 86400) return `${Math.floor(diffSec / 3600)}h ago`;
        return `${Math.floor(diffSec / 86400)}d ago`;
    },

    escapeHtml: function(text) {
        if (!text) return '';
        var div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
};

// Initialize when DOM is ready
$(document).ready(function() {
    InterfaceStatus.init();
});

// Cleanup on page unload
$(window).on('beforeunload', function() {
    InterfaceStatus.destroy();
});
