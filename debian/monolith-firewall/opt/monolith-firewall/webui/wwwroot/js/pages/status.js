// Status Pages
var Status = {
    init: function() {
        console.log('Initializing Status module...');
    },

    renderPage: function() {
        console.log('Rendering Status page...');
        const path = window.location.pathname || '';
        if (path.startsWith('/status/system')) {
            this.renderSystem();
        } else if (path.startsWith('/status/interfaces')) {
            this.renderInterfaces();
        } else if (path.startsWith('/status/services')) {
            this.renderServices();
        } else if (path.startsWith('/status/logs')) {
            this.renderLogs();
        } else if (path.startsWith('/status/states')) {
            this.renderStates();
        } else {
            this.renderSystem();
        }
    },

    renderSystem: function() {
        const container = $('#status-system-container, #page-content').first();
        if (!container.length) return;
        
        container.html(`
            <div class="container-fluid">
                <div class="row">
                    <div class="col-12">
                        <div class="card">
                            <div class="card-header">
                                <h4 class="mb-0">System Status</h4>
                            </div>
                            <div class="card-body">
                                <p class="text-muted">System status information - Coming soon</p>
                                <p>This page will display system uptime, CPU usage, memory usage, and disk usage.</p>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `);
    },

    renderInterfaces: function() {
        const container = $('#status-interfaces-container, #page-content').first();
        if (!container.length) return;

        container.html(`
            <div class="container-fluid">
                <div class="row">
                    <div class="col-12">
                        <div class="card">
                            <div class="card-header">
                                <h4 class="mb-0">Interface Status</h4>
                            </div>
                            <div class="card-body">
                                <p class="text-muted">Interface status information - Coming soon</p>
                                <p>This page will display detailed interface statistics and status.</p>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `);
    },

    renderServices: function() {
        const container = $('#status-services-container, #page-content').first();
        if (!container.length) return;

        container.html(`
            <div class="container-fluid">
                <div class="row">
                    <div class="col-12">
                        <div class="card">
                            <div class="card-header">
                                <h4 class="mb-0">Services Status</h4>
                            </div>
                            <div class="card-body">
                                <p class="text-muted">Services status information - Coming soon</p>
                                <p>This page will display the status of all system services.</p>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `);
    },

    renderLogs: function() {
        const container = $('#status-logs-container, #page-content').first();
        if (!container.length) return;

        container.html(`
            <div class="container-fluid">
                <div class="row">
                    <div class="col-12">
                        <div class="card">
                            <div class="card-header">
                                <h4 class="mb-0">System Logs</h4>
                            </div>
                            <div class="card-body">
                                <p class="text-muted">System logs viewer - Coming soon</p>
                                <p>This page will display system logs with filtering and search capabilities.</p>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `);
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

        container.html(`
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

        $.ajax({
            url: `/api/firewall/states?${params.toString()}`,
            method: 'GET',
            success: function(response) {
                // Handle both Success (PascalCase) and success (camelCase) responses
                const success = response.Success !== undefined ? response.Success : response.success;
                const data = response.Data || response.data;
                
                if (success && data) {
                    self.statesData.states = data.States || data.states || [];
                    self.statesData.total = data.Total || data.total || 0;
                    self.statesData.page = data.Page || data.page || 1;
                    self.statesData.pageSize = data.PageSize || data.pageSize || 50;
                    self.statesData.totalPages = data.TotalPages || data.totalPages || 1;
                    self.renderStatesTable();
                    self.updatePagination();
                } else {
                    tbody.html(`
                        <tr>
                            <td colspan="9" class="text-center text-danger py-4">
                                <i class="fa-solid fa-exclamation-triangle me-2"></i>
                                ${response.Error || response.error || 'Failed to load states'}
                            </td>
                        </tr>
                    `);
                }
            },
            error: function(xhr, status, error) {
                tbody.html(`
                    <tr>
                        <td colspan="9" class="text-center text-danger py-4">
                            <i class="fa-solid fa-exclamation-triangle me-2"></i>
                            Error loading states: ${error}
                        </td>
                    </tr>
                `);
            }
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

        $.ajax({
            url: '/api/firewall/states/kill',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({ id: stateId }),
            success: function(response) {
                if (response.Success) {
                    if (typeof Monolith !== 'undefined' && Monolith.UI) {
                        Monolith.UI.toast('Connection killed successfully', 'success');
                    }
                    self.loadStates();
                } else {
                    if (typeof Monolith !== 'undefined' && Monolith.UI) {
                        Monolith.UI.toast(response.Error || 'Failed to kill connection', 'error');
                    }
                }
            },
            error: function(xhr, status, error) {
                if (typeof Monolith !== 'undefined' && Monolith.UI) {
                    Monolith.UI.toast(`Error: ${error}`, 'error');
                }
            }
        });
    }
};

if (typeof Monolith !== 'undefined') {
    Monolith.Pages = Monolith.Pages || {};
    Monolith.Pages.Status = Status;
}