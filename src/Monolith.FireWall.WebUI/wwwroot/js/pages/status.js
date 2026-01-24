// Status Pages
var Status = {
    init: function() {
        console.log('Initializing Status module...');
        // Auto-render if we're on a status page
        if (window.location.pathname.startsWith('/status/')) {
            this.renderPage();
        }
    },

    renderPage: function() {
        console.log('Rendering Status page...');
        const path = window.location.pathname || '';
        if (path.startsWith('/status/states')) {
            this.renderStates();
        } else {
            // Default to states page
            // Note: routing-status is handled by routing-status.js module
            this.renderStates();
        }
    },


    renderStates: function() {
        const container = $('#status-container, #page-content').first();
        if (!container.length) return;

        const self = this;
        self.statesData = {
            states: [],
            total: 0,
            page: 1,
            pageSize: 50,
            totalPages: 0,
            filters: {
                protocol: '',
                sourceIp: '',
                destIp: '',
                sourcePort: '',
                destPort: '',
                state: '',
                interface: '',
                direction: '',
                search: '',
                minAge: ''
            },
            autoRefresh: false,
            autoRefreshInterval: 10,
            autoRefreshTimer: null
        };

        // Render page header first (title and description can be auto-detected from route)
        if (Monolith.PageHeader && typeof Monolith.PageHeader.render === 'function') {
            Monolith.PageHeader.render({
                title: "Firewall States",
                icon: "fa-network-wired",
                description: "View and manage active firewall connections",
                container: container,
                prepend: true
            });
        }

        // Render page content
        container.append(`
            <div class="container-fluid p-4">
                <!-- Header -->
                <div class="card mb-3">
                    <div class="card-header d-flex justify-content-between align-items-center">
                        <h4 class="mb-0">
                            <i class="fa-solid fa-network-wired me-2"></i>
                            Firewall States
                            <span class="badge bg-primary ms-2" id="states-count">0</span>
                        </h4>
                        <div class="d-flex gap-2">
                            <div class="input-group" style="width: 200px;">
                                <span class="input-group-text"><i class="fa-solid fa-clock"></i></span>
                                <select class="form-select form-select-sm" id="auto-refresh-interval">
                                    <option value="0">Auto-refresh: Off</option>
                                    <option value="5">5 seconds</option>
                                    <option value="10" selected>10 seconds</option>
                                    <option value="30">30 seconds</option>
                                    <option value="60">60 seconds</option>
                                </select>
                            </div>
                            <button type="button" class="btn btn-sm btn-primary" id="btn-refresh">
                                <i class="fa-solid fa-rotate"></i> Refresh
                            </button>
                        </div>
                    </div>
                </div>

                <!-- Filter Panel -->
                <div class="card mb-3">
                    <div class="card-header">
                        <button class="btn btn-sm btn-link text-decoration-none p-0" type="button" data-bs-toggle="collapse" data-bs-target="#filterPanel">
                            <i class="fa-solid fa-filter me-2"></i>State Filters
                            <i class="fa-solid fa-chevron-down ms-2"></i>
                        </button>
                    </div>
                    <div class="collapse show" id="filterPanel">
                        <div class="card-body">
                            <div class="row g-3">
                                <div class="col-md-3">
                                    <label class="form-label small">Protocol</label>
                                    <select class="form-select form-select-sm" id="filter-protocol">
                                        <option value="">All</option>
                                        <option value="tcp">TCP</option>
                                        <option value="udp">UDP</option>
                                        <option value="icmp">ICMP</option>
                                        <option value="other">Other</option>
                                    </select>
                                </div>
                                <div class="col-md-3">
                                    <label class="form-label small">Source IP</label>
                                    <input type="text" class="form-control form-control-sm" id="filter-source-ip" placeholder="192.168.1.1">
                                </div>
                                <div class="col-md-3">
                                    <label class="form-label small">Destination IP</label>
                                    <input type="text" class="form-control form-control-sm" id="filter-dest-ip" placeholder="8.8.8.8">
                                </div>
                                <div class="col-md-3">
                                    <label class="form-label small">Source Port</label>
                                    <input type="text" class="form-control form-control-sm" id="filter-source-port" placeholder="54321">
                                </div>
                                <div class="col-md-3">
                                    <label class="form-label small">Destination Port</label>
                                    <input type="text" class="form-control form-control-sm" id="filter-dest-port" placeholder="80">
                                </div>
                                <div class="col-md-3">
                                    <label class="form-label small">State</label>
                                    <input type="text" class="form-control form-control-sm" id="filter-state" placeholder="ESTABLISHED">
                                </div>
                                <div class="col-md-3">
                                    <label class="form-label small">Interface</label>
                                    <input type="text" class="form-control form-control-sm" id="filter-interface" placeholder="eth0">
                                </div>
                                <div class="col-md-3">
                                    <label class="form-label small">Direction</label>
                                    <select class="form-select form-select-sm" id="filter-direction">
                                        <option value="">All</option>
                                        <option value="in">Inbound</option>
                                        <option value="out">Outbound</option>
                                    </select>
                                </div>
                                <div class="col-md-6">
                                    <label class="form-label small">Search</label>
                                    <input type="text" class="form-control form-control-sm" id="filter-search" placeholder="Search across all fields...">
                                </div>
                                <div class="col-md-3">
                                    <label class="form-label small">Min Age (seconds)</label>
                                    <input type="number" class="form-control form-control-sm" id="filter-min-age" placeholder="0" min="0">
                                </div>
                                <div class="col-md-3 d-flex align-items-end">
                                    <button type="button" class="btn btn-sm btn-secondary w-100" id="btn-clear-filters">
                                        <i class="fa-solid fa-times me-1"></i>Clear Filters
                                    </button>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>

                <!-- States Table -->
                <div class="card">
                    <div class="card-body p-0">
                        <div class="table-responsive" style="max-height: 600px; overflow-y: auto;">
                            <table class="table table-hover table-sm mb-0" id="states-table">
                                <thead class="table-light sticky-top">
                                    <tr>
                                        <th>Protocol</th>
                                        <th>Source</th>
                                        <th>Destination</th>
                                        <th>State</th>
                                        <th>Interface</th>
                                        <th>Age</th>
                                        <th>Packets</th>
                                        <th>Bytes</th>
                                        <th>Actions</th>
                                    </tr>
                                </thead>
                                <tbody id="states-tbody">
                                    <tr>
                                        <td colspan="9" class="text-center text-muted py-4">
                                            <div class="spinner-border spinner-border-sm me-2" role="status"></div>
                                            Loading states...
                                        </td>
                                    </tr>
                                </tbody>
                            </table>
                        </div>
                        <!-- Pagination -->
                        <div class="card-footer d-flex justify-content-between align-items-center">
                            <div>
                                <span class="text-muted small">
                                    Showing <span id="states-showing">0</span> of <span id="states-total">0</span> states
                                </span>
                            </div>
                            <div class="d-flex gap-2 align-items-center">
                                <label class="small text-muted me-2">Page size:</label>
                                <select class="form-select form-select-sm" id="page-size" style="width: 80px;">
                                    <option value="25">25</option>
                                    <option value="50" selected>50</option>
                                    <option value="100">100</option>
                                    <option value="200">200</option>
                                </select>
                                <div class="btn-group" role="group">
                                    <button type="button" class="btn btn-sm btn-outline-secondary" id="btn-prev" disabled>
                                        <i class="fa-solid fa-chevron-left"></i>
                                    </button>
                                    <span class="btn btn-sm btn-outline-secondary disabled">
                                        Page <span id="current-page">1</span> of <span id="total-pages">1</span>
                                    </span>
                                    <button type="button" class="btn btn-sm btn-outline-secondary" id="btn-next" disabled>
                                        <i class="fa-solid fa-chevron-right"></i>
                                    </button>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `);

        // Attach event handlers
        $('#btn-refresh').on('click', () => self.loadStates());
        $('#auto-refresh-interval').on('change', function() {
            const interval = parseInt($(this).val()) || 0;
            self.setAutoRefresh(interval);
        });
        $('#btn-clear-filters').on('click', () => self.clearFilters());
        $('#page-size').on('change', function() {
            self.statesData.pageSize = parseInt($(this).val()) || 50;
            self.statesData.page = 1;
            self.loadStates();
        });
        $('#btn-prev').on('click', () => {
            if (self.statesData.page > 1) {
                self.statesData.page--;
                self.loadStates();
            }
        });
        $('#btn-next').on('click', () => {
            if (self.statesData.page < self.statesData.totalPages) {
                self.statesData.page++;
                self.loadStates();
            }
        });

        // Filter inputs - debounced
        let filterTimeout;
        $('#filter-protocol, #filter-source-ip, #filter-dest-ip, #filter-source-port, #filter-dest-port, #filter-state, #filter-interface, #filter-direction, #filter-search, #filter-min-age').on('input', function() {
            clearTimeout(filterTimeout);
            filterTimeout = setTimeout(() => {
                self.updateFilters();
                self.statesData.page = 1;
                self.loadStates();
            }, 500);
        });

        // Initial load
        self.loadStates();
    },

    updateFilters: function() {
        this.statesData.filters = {
            protocol: $('#filter-protocol').val() || '',
            sourceIp: $('#filter-source-ip').val() || '',
            destIp: $('#filter-dest-ip').val() || '',
            sourcePort: $('#filter-source-port').val() || '',
            destPort: $('#filter-dest-port').val() || '',
            state: $('#filter-state').val() || '',
            interface: $('#filter-interface').val() || '',
            direction: $('#filter-direction').val() || '',
            search: $('#filter-search').val() || '',
            minAge: $('#filter-min-age').val() || ''
        };
    },

    clearFilters: function() {
        $('#filter-protocol').val('');
        $('#filter-source-ip').val('');
        $('#filter-dest-ip').val('');
        $('#filter-source-port').val('');
        $('#filter-dest-port').val('');
        $('#filter-state').val('');
        $('#filter-interface').val('');
        $('#filter-direction').val('');
        $('#filter-search').val('');
        $('#filter-min-age').val('');
        this.updateFilters();
        this.statesData.page = 1;
        this.loadStates();
    },

    loadStates: function() {
        const self = this;
        const tbody = $('#states-tbody');
        
        tbody.html(`
            <tr>
                <td colspan="9" class="text-center text-muted py-4">
                    <div class="spinner-border spinner-border-sm me-2" role="status"></div>
                    Loading states...
                </td>
            </tr>
        `);

        // Build query string
        const params = new URLSearchParams();
        if (self.statesData.filters.protocol) params.append('protocol', self.statesData.filters.protocol);
        if (self.statesData.filters.sourceIp) params.append('sourceIp', self.statesData.filters.sourceIp);
        if (self.statesData.filters.destIp) params.append('destIp', self.statesData.filters.destIp);
        if (self.statesData.filters.sourcePort) params.append('sourcePort', self.statesData.filters.sourcePort);
        if (self.statesData.filters.destPort) params.append('destPort', self.statesData.filters.destPort);
        if (self.statesData.filters.state) params.append('state', self.statesData.filters.state);
        if (self.statesData.filters.interface) params.append('interface', self.statesData.filters.interface);
        if (self.statesData.filters.direction) params.append('direction', self.statesData.filters.direction);
        if (self.statesData.filters.search) params.append('search', self.statesData.filters.search);
        if (self.statesData.filters.minAge) params.append('minAge', self.statesData.filters.minAge);
        params.append('page', self.statesData.page);
        params.append('pageSize', self.statesData.pageSize);

        Monolith.API.get(`/api/firewall/states?${params.toString()}`)
            .then(function(response) {
                console.log('States API response:', response);
                
                // Handle both Success (PascalCase) and success (camelCase) responses
                const success = response.Success !== undefined ? response.Success : response.success;
                const data = response.Data || response.data;
                
                if (success && data) {
                    // The data object contains States, Total, Page, PageSize, TotalPages
                    self.statesData.states = data.States || data.states || [];
                    self.statesData.total = data.Total !== undefined ? data.Total : (data.total !== undefined ? data.total : 0);
                    self.statesData.page = data.Page !== undefined ? data.Page : (data.page !== undefined ? data.page : 1);
                    self.statesData.pageSize = data.PageSize !== undefined ? data.PageSize : (data.pageSize !== undefined ? data.pageSize : 50);
                    self.statesData.totalPages = data.TotalPages !== undefined ? data.TotalPages : (data.totalPages !== undefined ? data.totalPages : 1);
                    
                    console.log(`Loaded ${self.statesData.states.length} states (total: ${self.statesData.total})`);
                    self.renderStatesTable();
                    self.updatePagination();
                } else {
                    const errorMsg = response.Error || response.error || 'Failed to load states';
                    console.error('Failed to load states:', errorMsg);
                    tbody.html(`
                        <tr>
                            <td colspan="9" class="text-center text-danger py-4">
                                <i class="fa-solid fa-exclamation-triangle me-2"></i>
                                ${errorMsg}
                            </td>
                        </tr>
                    `);
                }
            })
            .catch(function(error) {
                console.error('Error loading states:', error);
                tbody.html(`
                    <tr>
                        <td colspan="9" class="text-center text-danger py-4">
                            <i class="fa-solid fa-exclamation-triangle me-2"></i>
                            Error loading states: ${error.message || error}
                        </td>
                    </tr>
                `);
            });
    },

    renderStatesTable: function() {
        const self = this;
        const tbody = $('#states-tbody');
        const states = self.statesData.states;

        if (states.length === 0) {
            tbody.html(`
                <tr>
                    <td colspan="9" class="text-center text-muted py-4">
                        No states found matching the current filters.
                    </td>
                </tr>
            `);
            return;
        }

        let html = '';
        states.forEach(state => {
            // Handle both camelCase and PascalCase property names
            const protocol = state.Protocol || state.protocol || 'unknown';
            const sourceIp = state.SourceIp || state.sourceIp || '';
            const sourcePort = state.SourcePort || state.sourcePort;
            const destIp = state.DestIp || state.destIp || '';
            const destPort = state.DestPort || state.destPort;
            const stateValue = state.State || state.state || 'unknown';
            const iface = state.Interface || state.interface || 'unknown';
            const direction = state.Direction || state.direction || 'unknown';
            const age = state.Age || state.age || 0;
            const packetsIn = state.PacketsIn || state.packetsIn || 0;
            const packetsOut = state.PacketsOut || state.packetsOut || 0;
            const bytesIn = state.BytesIn || state.bytesIn || 0;
            const bytesOut = state.BytesOut || state.bytesOut || 0;
            const id = state.Id || state.id || '';
            
            const stateClass = self.getStateClass(stateValue);
            const sourcePortStr = sourcePort ? `:${sourcePort}` : '';
            const destPortStr = destPort ? `:${destPort}` : '';
            
            html += `
                <tr>
                    <td><span class="badge bg-secondary">${protocol.toUpperCase()}</span></td>
                    <td><code>${sourceIp}${sourcePortStr}</code></td>
                    <td><code>${destIp}${destPortStr}</code></td>
                    <td><span class="badge ${stateClass}">${stateValue}</span></td>
                    <td>${iface}</td>
                    <td>${self.formatAge(age)}</td>
                    <td>${packetsIn}/${packetsOut}</td>
                    <td>${self.formatBytes(bytesIn)}/${self.formatBytes(bytesOut)}</td>
                    <td>
                        <button class="btn btn-sm btn-danger" onclick="Status.killState('${id}')" title="Kill connection">
                            <i class="fa-solid fa-ban"></i>
                        </button>
                    </td>
                </tr>
            `;
        });

        tbody.html(html);
    },

    getStateClass: function(state) {
        const s = state.toLowerCase();
        if (s.includes('established')) return 'bg-success';
        if (s.includes('time_wait') || s.includes('time-wait')) return 'bg-warning';
        if (s.includes('syn_sent') || s.includes('syn-sent')) return 'bg-info';
        if (s.includes('closed') || s.includes('fin_wait') || s.includes('fin-wait')) return 'bg-secondary';
        return 'bg-primary';
    },

    formatAge: function(seconds) {
        if (seconds < 60) return `${seconds}s`;
        if (seconds < 3600) return `${Math.floor(seconds / 60)}m`;
        return `${Math.floor(seconds / 3600)}h`;
    },

    formatBytes: function(bytes) {
        if (bytes < 1024) return `${bytes}B`;
        if (bytes < 1048576) return `${(bytes / 1024).toFixed(1)}KB`;
        return `${(bytes / 1048576).toFixed(1)}MB`;
    },

    updatePagination: function() {
        const self = this;
        $('#states-count').text(self.statesData.total);
        $('#states-showing').text(self.statesData.states.length);
        $('#states-total').text(self.statesData.total);
        $('#current-page').text(self.statesData.page);
        $('#total-pages').text(self.statesData.totalPages);
        $('#btn-prev').prop('disabled', self.statesData.page <= 1);
        $('#btn-next').prop('disabled', self.statesData.page >= self.statesData.totalPages);
    },

    setAutoRefresh: function(intervalSeconds) {
        const self = this;
        
        if (self.statesData.autoRefreshTimer) {
            clearInterval(self.statesData.autoRefreshTimer);
            self.statesData.autoRefreshTimer = null;
        }

        if (intervalSeconds > 0) {
            self.statesData.autoRefresh = true;
            self.statesData.autoRefreshInterval = intervalSeconds;
            self.statesData.autoRefreshTimer = setInterval(() => {
                self.loadStates();
            }, intervalSeconds * 1000);
        } else {
            self.statesData.autoRefresh = false;
        }
    },

    killState: function(stateId) {
        const self = this;
        
        if (!confirm('Are you sure you want to kill this connection?')) {
            return;
        }

        Monolith.API.post('/api/firewall/states/kill', { id: stateId })
            .then(function(response) {
                const success = response.Success !== undefined ? response.Success : response.success;
                if (success) {
                    if (typeof Monolith !== 'undefined' && Monolith.UI) {
                        Monolith.UI.toast('Connection killed successfully', 'success');
                    }
                    self.loadStates();
                } else {
                    if (typeof Monolith !== 'undefined' && Monolith.UI) {
                        Monolith.UI.toast(response.Error || response.error || 'Failed to kill connection', 'error');
                    }
                }
            })
            .catch(function(error) {
                console.error('Error killing state:', error);
                if (typeof Monolith !== 'undefined' && Monolith.UI) {
                    Monolith.UI.toast(`Error: ${error.message || error}`, 'error');
                }
            });
    },

    // Note: renderRoutingStatus is now handled by routing-status.js module
    // This function is kept for backwards compatibility but routing-status.js is self-contained
    renderRoutingStatus: function() {
        // This should not be called anymore - routing-status.js handles it
        console.warn('Status.renderRoutingStatus() called - routing-status.js should handle this');
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

// Register module immediately (before router tries to find it)
(function() {
    if (typeof Monolith === 'undefined') {
        window.Monolith = {};
    }
    if (typeof Monolith.Pages === 'undefined') {
        Monolith.Pages = {};
    }
    // Register with both Status (PascalCase) and status (lowercase) for compatibility
    Monolith.Pages.Status = Status;
    Monolith.Pages.status = Status; // Also register lowercase for router compatibility
    console.log('Status module registered:', typeof Monolith.Pages.Status, typeof Monolith.Pages.status);
})();