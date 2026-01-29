// Gateway Health Page - Real-time gateway monitoring and health status

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

var GatewayHealth = {
    gateways: [],
    healthData: {},
    _signalRHandler: null,
    _refreshInterval: null,

    init: function() {
        console.log('Initializing Gateway Health page...');
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
                case 'GatewayStatusChanged':
                    this.updateGatewayHealth(data.gatewayId, data);
                    break;
                case 'GatewayHealthCheck':
                    this.handleHealthCheckResult(data);
                    break;
            }
        };

        Monolith.SignalR.subscribe('gateways', this._signalRHandler);
        console.log('[GatewayHealth] Subscribed to SignalR');
    },

    _unsubscribeFromSignalR: function() {
        if (Monolith.SignalR && this._signalRHandler) {
            Monolith.SignalR.unsubscribe('gateways', this._signalRHandler);
            this._signalRHandler = null;
        }
    },

    _startAutoRefresh: function() {
        // Refresh health data every 30 seconds
        this._refreshInterval = setInterval(() => {
            this.loadHealthData();
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
                title: "Gateway Health",
                icon: "fa-heart-pulse",
                description: "Monitor gateway status, latency, and packet loss",
                container: '#gateway-health-container',
                prepend: false
            });
        }

        var html = `
            <div class="d-flex justify-content-end mb-3">
                <div class="btn-group">
                    <button class="btn btn-outline-primary" onclick="GatewayHealth.loadData()" title="Refresh">
                        <i class="fa-solid fa-arrows-rotate"></i>
                    </button>
                    <button class="btn btn-primary" onclick="GatewayHealth.checkAllGateways()">
                        <i class="fa-solid fa-tower-broadcast me-1"></i>Check All
                    </button>
                </div>
            </div>

            <!-- Summary Cards -->
            <div class="row mb-4" id="health-summary">
                <div class="col-md-3">
                    <div class="card bg-success bg-opacity-10 border-success">
                        <div class="card-body text-center">
                            <h3 class="mb-0" id="summary-online">-</h3>
                            <small class="text-success">Online</small>
                        </div>
                    </div>
                </div>
                <div class="col-md-3">
                    <div class="card bg-warning bg-opacity-10 border-warning">
                        <div class="card-body text-center">
                            <h3 class="mb-0" id="summary-degraded">-</h3>
                            <small class="text-warning">Degraded</small>
                        </div>
                    </div>
                </div>
                <div class="col-md-3">
                    <div class="card bg-danger bg-opacity-10 border-danger">
                        <div class="card-body text-center">
                            <h3 class="mb-0" id="summary-offline">-</h3>
                            <small class="text-danger">Offline</small>
                        </div>
                    </div>
                </div>
                <div class="col-md-3">
                    <div class="card bg-secondary bg-opacity-10 border-secondary">
                        <div class="card-body text-center">
                            <h3 class="mb-0" id="summary-unknown">-</h3>
                            <small class="text-secondary">Unknown</small>
                        </div>
                    </div>
                </div>
            </div>

            <!-- Gateway Health Table -->
            <div class="card">
                <div class="card-body p-0">
                    <div class="table-responsive">
                        <table class="table table-hover mb-0" id="health-table">
                            <thead class="table-light">
                                <tr>
                                    <th>Gateway</th>
                                    <th>Interface</th>
                                    <th>Monitor IP</th>
                                    <th>Status</th>
                                    <th>Latency</th>
                                    <th>Packet Loss</th>
                                    <th>Last Check</th>
                                    <th class="text-end">Actions</th>
                                </tr>
                            </thead>
                            <tbody id="health-body">
                                <tr><td colspan="8" class="text-center py-4"><div class="spinner-border spinner-border-sm"></div> Loading...</td></tr>
                            </tbody>
                        </table>
                    </div>
                </div>
            </div>

            <!-- Health History Modal -->
            <div class="modal fade" id="history-modal" tabindex="-1">
                <div class="modal-dialog modal-lg">
                    <div class="modal-content">
                        <div class="modal-header">
                            <h5 class="modal-title">Health History</h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                        </div>
                        <div class="modal-body">
                            <div id="history-content">
                                <div class="text-center py-4">
                                    <div class="spinner-border spinner-border-sm"></div> Loading history...
                                </div>
                            </div>
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Close</button>
                        </div>
                    </div>
                </div>
            </div>
        `;
        $('#gateway-health-container').append(html);
    },

    loadData: async function() {
        try {
            // Load gateways first
            const gwResponse = await Monolith.Core.call('routing.gateways.list', {});
            this.gateways = gwResponse.data || [];

            // Then load health data
            await this.loadHealthData();
        } catch (error) {
            console.error('Failed to load gateway data:', error);
            $('#health-body').html('<tr><td colspan="8" class="text-center text-danger">Failed to load data</td></tr>');
        }
    },

    loadHealthData: async function() {
        try {
            const healthResponse = await Monolith.Core.call('gateway.health.list', {});
            var healthList = healthResponse.data || [];

            // Index by gateway ID
            this.healthData = {};
            healthList.forEach(h => {
                this.healthData[h.gatewayId] = h;
            });

            this.renderHealth();
            this.updateSummary();
        } catch (error) {
            console.error('Failed to load health data:', error);
        }
    },

    renderHealth: function() {
        if (this.gateways.length === 0) {
            $('#health-body').html('<tr><td colspan="8" class="text-center text-muted py-4">No gateways configured</td></tr>');
            return;
        }

        var html = this.gateways.map(gw => {
            var health = this.healthData[gw.id] || {};
            var status = health.status || 'unknown';
            var latencyMs = health.latencyMs;
            var packetLoss = health.packetLossPercent;
            var lastCheck = health.lastCheckAt;
            var lastError = health.lastError;

            var statusClass = this.getStatusClass(status);
            var statusBadge = this.getStatusBadge(status);

            var latencyDisplay = latencyMs != null ? `${latencyMs} ms` : '-';
            var latencyClass = '';
            if (latencyMs != null) {
                if (latencyMs > 200) latencyClass = 'text-danger';
                else if (latencyMs > 100) latencyClass = 'text-warning';
            }

            var lossDisplay = packetLoss != null ? `${packetLoss.toFixed(1)}%` : '-';
            var lossClass = '';
            if (packetLoss != null) {
                if (packetLoss > 10) lossClass = 'text-danger';
                else if (packetLoss > 5) lossClass = 'text-warning';
            }

            var lastCheckDisplay = lastCheck ? this.formatRelativeTime(lastCheck) : 'Never';

            return `
                <tr data-gateway-id="${gw.id}" class="${statusClass}">
                    <td>
                        <strong>${this.escapeHtml(gw.name)}</strong>
                        ${gw.description ? `<br><small class="text-muted">${this.escapeHtml(gw.description)}</small>` : ''}
                    </td>
                    <td><code>${this.escapeHtml(gw.interface || '-')}</code></td>
                    <td><code>${this.escapeHtml(gw.monitorIp || gw.gateway)}</code></td>
                    <td>
                        ${statusBadge}
                        ${lastError ? `<i class="fa-solid fa-triangle-exclamation text-warning ms-1" title="${this.escapeHtml(lastError)}"></i>` : ''}
                    </td>
                    <td class="${latencyClass}">${latencyDisplay}</td>
                    <td class="${lossClass}">${lossDisplay}</td>
                    <td><small class="text-muted">${lastCheckDisplay}</small></td>
                    <td class="text-end">
                        <button class="btn btn-sm btn-outline-primary me-1" onclick="GatewayHealth.checkGateway(${gw.id})" title="Check Now">
                            <i class="fa-solid fa-tower-broadcast"></i>
                        </button>
                        <button class="btn btn-sm btn-outline-secondary" onclick="GatewayHealth.showHistory(${gw.id})" title="History">
                            <i class="fa-solid fa-clock-rotate-left"></i>
                        </button>
                    </td>
                </tr>
            `;
        }).join('');

        $('#health-body').html(html);
    },

    getStatusClass: function(status) {
        switch (status) {
            case 'online': return '';
            case 'degraded': return 'table-warning';
            case 'offline': return 'table-danger';
            default: return 'table-secondary';
        }
    },

    getStatusBadge: function(status) {
        switch (status) {
            case 'online': return '<span class="badge bg-success"><i class="fa-solid fa-circle-check me-1"></i>Online</span>';
            case 'degraded': return '<span class="badge bg-warning text-dark"><i class="fa-solid fa-triangle-exclamation me-1"></i>Degraded</span>';
            case 'offline': return '<span class="badge bg-danger"><i class="fa-solid fa-circle-xmark me-1"></i>Offline</span>';
            default: return '<span class="badge bg-secondary"><i class="fa-solid fa-circle-question me-1"></i>Unknown</span>';
        }
    },

    updateSummary: function() {
        var counts = { online: 0, degraded: 0, offline: 0, unknown: 0 };

        this.gateways.forEach(gw => {
            var health = this.healthData[gw.id] || {};
            var status = health.status || 'unknown';
            if (counts.hasOwnProperty(status)) {
                counts[status]++;
            } else {
                counts.unknown++;
            }
        });

        $('#summary-online').text(counts.online);
        $('#summary-degraded').text(counts.degraded);
        $('#summary-offline').text(counts.offline);
        $('#summary-unknown').text(counts.unknown);
    },

    updateGatewayHealth: function(gatewayId, data) {
        this.healthData[gatewayId] = data;
        this.renderHealth();
        this.updateSummary();
    },

    handleHealthCheckResult: function(data) {
        if (data.statusChanged) {
            var statusText = data.newStatus === 'online' ? 'is now online' :
                            data.newStatus === 'offline' ? 'went offline' :
                            'is degraded';
            Monolith.Toast.info(`Gateway "${data.gatewayName}" ${statusText}`);
        }
        this.updateGatewayHealth(data.gatewayId, {
            status: data.newStatus,
            latencyMs: data.latencyMs,
            packetLossPercent: data.packetLossPercent,
            lastCheckAt: new Date().toISOString(),
            lastError: data.error
        });
    },

    checkGateway: async function(gatewayId) {
        var gw = this.gateways.find(g => g.id === gatewayId);
        var btn = $(`tr[data-gateway-id="${gatewayId}"] .btn-outline-primary`);
        btn.prop('disabled', true).html('<span class="spinner-border spinner-border-sm"></span>');

        try {
            var response = await Monolith.Core.call('gateway.health.check', { gatewayId: gatewayId });
            if (response.success && response.data) {
                this.handleHealthCheckResult(response.data);
                Monolith.Toast.success(`Health check completed for ${gw ? gw.name : 'gateway'}`);
            } else {
                Monolith.Toast.error('Health check failed: ' + (response.error || 'Unknown error'));
            }
        } catch (error) {
            console.error('Health check failed:', error);
            Monolith.Toast.error('Failed to check gateway health');
        } finally {
            btn.prop('disabled', false).html('<i class="fa-solid fa-tower-broadcast"></i>');
        }
    },

    checkAllGateways: async function() {
        var btn = $('.btn-primary:contains("Check All")');
        btn.prop('disabled', true).html('<span class="spinner-border spinner-border-sm me-1"></span>Checking...');

        try {
            var response = await Monolith.Core.call('gateway.health.check_all', {});
            if (response.success && response.data) {
                response.data.forEach(result => {
                    this.handleHealthCheckResult(result);
                });
                Monolith.Toast.success(`Checked ${response.data.length} gateways`);
            } else {
                Monolith.Toast.error('Health check failed: ' + (response.error || 'Unknown error'));
            }
        } catch (error) {
            console.error('Health check failed:', error);
            Monolith.Toast.error('Failed to check gateways');
        } finally {
            btn.prop('disabled', false).html('<i class="fa-solid fa-tower-broadcast me-1"></i>Check All');
        }
    },

    showHistory: function(gatewayId) {
        var gw = this.gateways.find(g => g.id === gatewayId);
        $('#history-modal .modal-title').text(`Health History - ${gw ? gw.name : 'Gateway'}`);
        $('#history-content').html(`
            <div class="text-center text-muted py-4">
                <i class="fa-solid fa-circle-info fa-2x mb-2"></i>
                <p>Health history tracking is available via the system logs.<br>
                Filter by "gateway" to see health check events.</p>
                <a href="#/system/logs" class="btn btn-outline-primary btn-sm">View Logs</a>
            </div>
        `);
        new bootstrap.Modal('#history-modal').show();
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
    GatewayHealth.init();
});

// Cleanup on page unload
$(window).on('beforeunload', function() {
    GatewayHealth.destroy();
});
