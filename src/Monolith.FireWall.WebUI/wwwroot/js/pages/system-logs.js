// System Logs Page - Tabbed Interface
var SystemLogs = {
    currentTab: 'monolith',
    currentCategory: 'all',
    currentSecurityCategory: 'firewall',
    isInitialized: false,
    logs: {
        monolith: [],
        system: [],
        security: []
    },
    pagination: {
        monolith: { limit: 100, offset: 0, total: 0 },
        system: { limit: 100, offset: 0, total: 0 },
        security: { limit: 100, offset: 0, total: 0 }
    },
    filters: {
        monolith: { category: '', level: '', source: '', startDate: '', endDate: '' },
        system: { category: '', level: '', source: '', startDate: '', endDate: '' },
        security: { category: '', level: '', source: '', startDate: '', endDate: '' }
    },

    init: function() {
        console.log('Initializing System Logs page...');
    },

    renderPage: function() {
        console.log('Rendering System Logs page...');
        this.renderStructure();
        this.loadMonolithLogs();
        this.attachEventHandlers();
    },

    /**
     * Render the main page structure with tabs
     */
    renderStructure: function() {
        const container = $('#page-content');
        if (!container.length) return;

        // Render page header
        if (Monolith.PageHeader && typeof Monolith.PageHeader.render === 'function') {
            Monolith.PageHeader.render({
                title: "System Logs",
                icon: "fa-clipboard-list",
                description: "View and filter system logs, Monolith events, and security logs",
                container: container,
                prepend: true
            });
        }

        container.append(`
            <div class="container-fluid p-4">

                <!-- Status Messages -->
                <div id="logsStatusMessage" class="alert d-none"></div>

                <!-- Main Tabs -->
                <ul class="nav nav-tabs mb-4" id="logsMainTabs" role="tablist">
                    <li class="nav-item" role="presentation">
                        <button class="nav-link active" id="monolith-tab" data-bs-toggle="tab" data-bs-target="#monolith-logs" 
                                type="button" role="tab" aria-controls="monolith-logs" aria-selected="true">
                            Monolith Logs
                        </button>
                    </li>
                    <li class="nav-item" role="presentation">
                        <button class="nav-link" id="system-tab" data-bs-toggle="tab" data-bs-target="#system-logs" 
                                type="button" role="tab" aria-controls="system-logs" aria-selected="false">
                            System Logs
                        </button>
                    </li>
                    <li class="nav-item" role="presentation">
                        <button class="nav-link" id="security-tab" data-bs-toggle="tab" data-bs-target="#security-logs" 
                                type="button" role="tab" aria-controls="security-logs" aria-selected="false">
                            Security Logs
                        </button>
                    </li>
                </ul>

                <!-- Tab Content -->
                <div class="tab-content" id="logsTabContent">
                    ${this.renderMonolithTab()}
                    ${this.renderSystemTab()}
                    ${this.renderSecurityTab()}
                </div>
            </div>
        `);
    },

    /**
     * Render Monolith Logs tab with category filter menu
     */
    renderMonolithTab: function() {
        return `
            <div class="tab-pane fade show active" id="monolith-logs" role="tabpanel" aria-labelledby="monolith-tab">
                <!-- Category Filter Menu -->
                <div class="mb-3">
                    <ul class="nav nav-pills" id="monolithCategoryMenu">
                        <li class="nav-item">
                            <a class="nav-link active" href="#" data-category="all">All</a>
                        </li>
                        <li class="nav-item">
                            <a class="nav-link" href="#" data-category="Auth">Auth</a>
                        </li>
                        <li class="nav-item">
                            <a class="nav-link" href="#" data-category="Changes">Changes</a>
                        </li>
                        <li class="nav-item">
                            <a class="nav-link" href="#" data-category="Package">Package</a>
                        </li>
                        <li class="nav-item">
                            <a class="nav-link" href="#" data-category="Module">Module</a>
                        </li>
                        <li class="nav-item">
                            <a class="nav-link" href="#" data-category="User">User</a>
                        </li>
                        <li class="nav-item">
                            <a class="nav-link" href="#" data-category="Permission">Permission</a>
                        </li>
                    </ul>
                </div>

                <!-- Filters and Controls -->
                <div class="card mb-3">
                    <div class="card-body">
                        <div class="row g-3">
                            <div class="col-md-3">
                                <label class="form-label">Level</label>
                                <select class="form-select" id="monolithLevelFilter">
                                    <option value="">All Levels</option>
                                    <option value="Info">Info</option>
                                    <option value="Warning">Warning</option>
                                    <option value="Error">Error</option>
                                    <option value="Critical">Critical</option>
                                </select>
                            </div>
                            <div class="col-md-3">
                                <label class="form-label">Source</label>
                                <input type="text" class="form-control" id="monolithSourceFilter" placeholder="Filter by source">
                            </div>
                            <div class="col-md-2">
                                <label class="form-label">Start Date</label>
                                <input type="date" class="form-control" id="monolithStartDate">
                            </div>
                            <div class="col-md-2">
                                <label class="form-label">End Date</label>
                                <input type="date" class="form-control" id="monolithEndDate">
                            </div>
                            <div class="col-md-2 d-flex align-items-end">
                                <button type="button" class="btn btn-primary w-100" id="btnApplyMonolithFilters">Apply Filters</button>
                            </div>
                        </div>
                    </div>
                </div>

                <!-- Logs Table -->
                <div class="card">
                    <div class="card-body">
                        <div class="table-responsive">
                            <table class="table table-hover" id="monolithLogsTable">
                                <thead>
                                    <tr>
                                        <th>Timestamp</th>
                                        <th>Category</th>
                                        <th>Level</th>
                                        <th>Source</th>
                                        <th>Message</th>
                                        <th>User</th>
                                        <th>IP</th>
                                    </tr>
                                </thead>
                                <tbody id="monolithLogsTableBody">
                                    <tr><td colspan="7" class="text-center text-muted">Loading logs...</td></tr>
                                </tbody>
                            </table>
                        </div>
                        <!-- Pagination -->
                        <div class="d-flex justify-content-between align-items-center mt-3">
                            <div>
                                <span class="text-muted">Showing <span id="monolithLogsCount">0</span> of <span id="monolithLogsTotal">0</span> logs</span>
                            </div>
                            <div>
                                <button class="btn btn-sm btn-outline-primary" id="btnMonolithPrev" disabled>Previous</button>
                                <button class="btn btn-sm btn-outline-primary ms-2" id="btnMonolithNext" disabled>Next</button>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `;
    },

    /**
     * Render System Logs tab
     */
    renderSystemTab: function() {
        return `
            <div class="tab-pane fade" id="system-logs" role="tabpanel" aria-labelledby="system-tab">
                <!-- Filters and Controls -->
                <div class="card mb-3">
                    <div class="card-body">
                        <div class="row g-3">
                            <div class="col-md-3">
                                <label class="form-label">Category</label>
                                <select class="form-select" id="systemCategoryFilter">
                                    <option value="">All Categories</option>
                                    <option value="Service">Service</option>
                                    <option value="Configuration">Configuration</option>
                                    <option value="Network">Network</option>
                                    <option value="Storage">Storage</option>
                                    <option value="Update">Update</option>
                                </select>
                            </div>
                            <div class="col-md-3">
                                <label class="form-label">Level</label>
                                <select class="form-select" id="systemLevelFilter">
                                    <option value="">All Levels</option>
                                    <option value="Info">Info</option>
                                    <option value="Warning">Warning</option>
                                    <option value="Error">Error</option>
                                    <option value="Critical">Critical</option>
                                </select>
                            </div>
                            <div class="col-md-2">
                                <label class="form-label">Start Date</label>
                                <input type="date" class="form-control" id="systemStartDate">
                            </div>
                            <div class="col-md-2">
                                <label class="form-label">End Date</label>
                                <input type="date" class="form-control" id="systemEndDate">
                            </div>
                            <div class="col-md-2 d-flex align-items-end">
                                <button type="button" class="btn btn-primary w-100" id="btnApplySystemFilters">Apply Filters</button>
                            </div>
                        </div>
                    </div>
                </div>

                <!-- Logs Table -->
                <div class="card">
                    <div class="card-body">
                        <div class="table-responsive">
                            <table class="table table-hover" id="systemLogsTable">
                                <thead>
                                    <tr>
                                        <th>Timestamp</th>
                                        <th>Category</th>
                                        <th>Level</th>
                                        <th>Source</th>
                                        <th>Message</th>
                                        <th>Details</th>
                                    </tr>
                                </thead>
                                <tbody id="systemLogsTableBody">
                                    <tr><td colspan="6" class="text-center text-muted">Loading logs...</td></tr>
                                </tbody>
                            </table>
                        </div>
                        <!-- Pagination -->
                        <div class="d-flex justify-content-between align-items-center mt-3">
                            <div>
                                <span class="text-muted">Showing <span id="systemLogsCount">0</span> of <span id="systemLogsTotal">0</span> logs</span>
                            </div>
                            <div>
                                <button class="btn btn-sm btn-outline-primary" id="btnSystemPrev" disabled>Previous</button>
                                <button class="btn btn-sm btn-outline-primary ms-2" id="btnSystemNext" disabled>Next</button>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `;
    },

    /**
     * Render Security Logs tab with horizontal sub-tabs
     */
    renderSecurityTab: function() {
        return `
            <div class="tab-pane fade" id="security-logs" role="tabpanel" aria-labelledby="security-tab">
                <!-- Horizontal Sub-Tabs -->
                <ul class="nav nav-tabs mb-3" id="securitySubTabs" role="tablist">
                    <li class="nav-item" role="presentation">
                        <button class="nav-link active" data-category="Firewall">Firewall Events</button>
                    </li>
                    <li class="nav-item" role="presentation">
                        <button class="nav-link" data-category="Intrusion">Intrusion</button>
                    </li>
                    <li class="nav-item" role="presentation">
                        <button class="nav-link" data-category="Access">Access</button>
                    </li>
                    <li class="nav-item" role="presentation">
                        <button class="nav-link" data-category="Threat">Threat</button>
                    </li>
                    <li class="nav-item" role="presentation">
                        <button class="nav-link" data-category="Audit">Audit</button>
                    </li>
                </ul>

                <!-- Filters and Controls -->
                <div class="card mb-3">
                    <div class="card-body">
                        <div class="row g-3">
                            <div class="col-md-3">
                                <label class="form-label">Level</label>
                                <select class="form-select" id="securityLevelFilter">
                                    <option value="">All Levels</option>
                                    <option value="Info">Info</option>
                                    <option value="Warning">Warning</option>
                                    <option value="Error">Error</option>
                                    <option value="Critical">Critical</option>
                                </select>
                            </div>
                            <div class="col-md-3">
                                <label class="form-label">Source</label>
                                <input type="text" class="form-control" id="securitySourceFilter" placeholder="Filter by source">
                            </div>
                            <div class="col-md-2">
                                <label class="form-label">Start Date</label>
                                <input type="date" class="form-control" id="securityStartDate">
                            </div>
                            <div class="col-md-2">
                                <label class="form-label">End Date</label>
                                <input type="date" class="form-control" id="securityEndDate">
                            </div>
                            <div class="col-md-2 d-flex align-items-end">
                                <button type="button" class="btn btn-primary w-100" id="btnApplySecurityFilters">Apply Filters</button>
                            </div>
                        </div>
                    </div>
                </div>

                <!-- Logs Table -->
                <div class="card">
                    <div class="card-body">
                        <div class="table-responsive">
                            <table class="table table-hover" id="securityLogsTable">
                                <thead>
                                    <tr>
                                        <th>Timestamp</th>
                                        <th>Category</th>
                                        <th>Level</th>
                                        <th>Source</th>
                                        <th>Message</th>
                                        <th>User</th>
                                        <th>IP</th>
                                        <th>Details</th>
                                    </tr>
                                </thead>
                                <tbody id="securityLogsTableBody">
                                    <tr><td colspan="8" class="text-center text-muted">Loading logs...</td></tr>
                                </tbody>
                            </table>
                        </div>
                        <!-- Pagination -->
                        <div class="d-flex justify-content-between align-items-center mt-3">
                            <div>
                                <span class="text-muted">Showing <span id="securityLogsCount">0</span> of <span id="securityLogsTotal">0</span> logs</span>
                            </div>
                            <div>
                                <button class="btn btn-sm btn-outline-primary" id="btnSecurityPrev" disabled>Previous</button>
                                <button class="btn btn-sm btn-outline-primary ms-2" id="btnSecurityNext" disabled>Next</button>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `;
    },

    /**
     * Load Monolith logs
     */
    loadMonolithLogs: async function() {
        try {
            const params = new URLSearchParams({
                category: this.currentCategory === 'all' ? '' : this.currentCategory,
                level: this.filters.monolith.level || '',
                source: this.filters.monolith.source || '',
                limit: this.pagination.monolith.limit,
                offset: this.pagination.monolith.offset
            });

            if (this.filters.monolith.startDate) params.append('startDate', this.filters.monolith.startDate);
            if (this.filters.monolith.endDate) params.append('endDate', this.filters.monolith.endDate);

            const response = await Monolith.API.get(`/logs/monolith?${params}`);
            if ((response.Success || response.success) && (response.Data || response.data)) {
                const data = response.Data || response.data;
                this.logs.monolith = data.Logs || data.logs || [];
                this.pagination.monolith.total = data.TotalCount || data.totalCount || 0;
                this.renderMonolithLogs();
            }
        } catch (error) {
            console.error('Error loading Monolith logs:', error);
            this.showMessage('Error loading Monolith logs: ' + error.message, 'danger');
        }
    },

    /**
     * Load System logs
     */
    loadSystemLogs: async function() {
        try {
            const params = new URLSearchParams({
                category: this.filters.system.category || '',
                level: this.filters.system.level || '',
                source: this.filters.system.source || '',
                limit: this.pagination.system.limit,
                offset: this.pagination.system.offset
            });

            if (this.filters.system.startDate) params.append('startDate', this.filters.system.startDate);
            if (this.filters.system.endDate) params.append('endDate', this.filters.system.endDate);

            const response = await Monolith.API.get(`/logs/system?${params}`);
            if ((response.Success || response.success) && (response.Data || response.data)) {
                const data = response.Data || response.data;
                this.logs.system = data.Logs || data.logs || [];
                this.pagination.system.total = data.TotalCount || data.totalCount || 0;
                this.renderSystemLogs();
            }
        } catch (error) {
            console.error('Error loading System logs:', error);
            this.showMessage('Error loading System logs: ' + error.message, 'danger');
        }
    },

    /**
     * Load Security logs
     */
    loadSecurityLogs: async function() {
        try {
            const params = new URLSearchParams({
                category: this.currentSecurityCategory,
                level: this.filters.security.level || '',
                source: this.filters.security.source || '',
                limit: this.pagination.security.limit,
                offset: this.pagination.security.offset
            });

            if (this.filters.security.startDate) params.append('startDate', this.filters.security.startDate);
            if (this.filters.security.endDate) params.append('endDate', this.filters.security.endDate);

            const response = await Monolith.API.get(`/logs/security?${params}`);
            if ((response.Success || response.success) && (response.Data || response.data)) {
                const data = response.Data || response.data;
                this.logs.security = data.Logs || data.logs || [];
                this.pagination.security.total = data.TotalCount || data.totalCount || 0;
                this.renderSecurityLogs();
            }
        } catch (error) {
            console.error('Error loading Security logs:', error);
            this.showMessage('Error loading Security logs: ' + error.message, 'danger');
        }
    },

    /**
     * Render Monolith logs table
     */
    renderMonolithLogs: function() {
        const tbody = $('#monolithLogsTableBody');
        if (!tbody.length) return;

        if (this.logs.monolith.length === 0) {
            tbody.html('<tr><td colspan="7" class="text-center text-muted">No logs found</td></tr>');
            return;
        }

        const rows = this.logs.monolith.map(log => {
            const timestamp = new Date(log.Timestamp || log.timestamp).toLocaleString();
            const levelBadge = this.getLevelBadge(log.Level || log.level);
            return `
                <tr>
                    <td>${timestamp}</td>
                    <td><span class="badge bg-secondary">${log.Category || log.category}</span></td>
                    <td>${levelBadge}</td>
                    <td>${log.Source || log.source}</td>
                    <td>${this.escapeHtml(log.Message || log.message)}</td>
                    <td>${log.UserId || log.userId || '-'}</td>
                    <td>${log.IpAddress || log.ipAddress || '-'}</td>
                </tr>
            `;
        }).join('');

        tbody.html(rows);
        $('#monolithLogsCount').text(this.logs.monolith.length);
        $('#monolithLogsTotal').text(this.pagination.monolith.total);
        $('#btnMonolithPrev').prop('disabled', this.pagination.monolith.offset === 0);
        $('#btnMonolithNext').prop('disabled', this.pagination.monolith.offset + this.logs.monolith.length >= this.pagination.monolith.total);
    },

    /**
     * Render System logs table
     */
    renderSystemLogs: function() {
        const tbody = $('#systemLogsTableBody');
        if (!tbody.length) return;

        if (this.logs.system.length === 0) {
            tbody.html('<tr><td colspan="6" class="text-center text-muted">No logs found</td></tr>');
            return;
        }

        const rows = this.logs.system.map(log => {
            const timestamp = new Date(log.Timestamp || log.timestamp).toLocaleString();
            const levelBadge = this.getLevelBadge(log.Level || log.level);
            const details = log.Details || log.details ? JSON.stringify(log.Details || log.details) : '-';
            return `
                <tr>
                    <td>${timestamp}</td>
                    <td><span class="badge bg-secondary">${log.Category || log.category}</span></td>
                    <td>${levelBadge}</td>
                    <td>${log.Source || log.source}</td>
                    <td>${this.escapeHtml(log.Message || log.message)}</td>
                    <td><small class="text-muted">${this.escapeHtml(details)}</small></td>
                </tr>
            `;
        }).join('');

        tbody.html(rows);
        $('#systemLogsCount').text(this.logs.system.length);
        $('#systemLogsTotal').text(this.pagination.system.total);
        $('#btnSystemPrev').prop('disabled', this.pagination.system.offset === 0);
        $('#btnSystemNext').prop('disabled', this.pagination.system.offset + this.logs.system.length >= this.pagination.system.total);
    },

    /**
     * Render Security logs table
     */
    renderSecurityLogs: function() {
        const tbody = $('#securityLogsTableBody');
        if (!tbody.length) return;

        if (this.logs.security.length === 0) {
            tbody.html('<tr><td colspan="8" class="text-center text-muted">No logs found</td></tr>');
            return;
        }

        const rows = this.logs.security.map(log => {
            const timestamp = new Date(log.Timestamp || log.timestamp).toLocaleString();
            const levelBadge = this.getLevelBadge(log.Level || log.level);
            const details = log.Details || log.details ? JSON.stringify(log.Details || log.details) : '-';
            return `
                <tr>
                    <td>${timestamp}</td>
                    <td><span class="badge bg-secondary">${log.Category || log.category}</span></td>
                    <td>${levelBadge}</td>
                    <td>${log.Source || log.source}</td>
                    <td>${this.escapeHtml(log.Message || log.message)}</td>
                    <td>${log.UserId || log.userId || '-'}</td>
                    <td>${log.IpAddress || log.ipAddress || '-'}</td>
                    <td><small class="text-muted">${this.escapeHtml(details)}</small></td>
                </tr>
            `;
        }).join('');

        tbody.html(rows);
        $('#securityLogsCount').text(this.logs.security.length);
        $('#securityLogsTotal').text(this.pagination.security.total);
        $('#btnSecurityPrev').prop('disabled', this.pagination.security.offset === 0);
        $('#btnSecurityNext').prop('disabled', this.pagination.security.offset + this.logs.security.length >= this.pagination.security.total);
    },

    /**
     * Get level badge HTML
     */
    getLevelBadge: function(level) {
        const levelLower = (level || '').toLowerCase();
        const badgeClass = {
            'info': 'bg-info',
            'warning': 'bg-warning',
            'error': 'bg-danger',
            'critical': 'bg-danger'
        }[levelLower] || 'bg-secondary';
        return `<span class="badge ${badgeClass}">${level || 'Unknown'}</span>`;
    },

    /**
     * Escape HTML
     */
    escapeHtml: function(text) {
        if (!text) return '';
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    },

    /**
     * Show status message
     */
    showMessage: function(message, type) {
        const alert = $('#logsStatusMessage');
        if (!alert.length) return;
        alert.removeClass('d-none alert-success alert-danger alert-warning alert-info')
             .addClass(`alert-${type}`)
             .text(message);
        setTimeout(() => alert.addClass('d-none'), 5000);
    },

    /**
     * Attach event handlers
     */
    attachEventHandlers: function() {
        // Main tab switching - use .off() first to prevent duplicate handlers
        $('#logsMainTabs button[data-bs-toggle="tab"]').off('shown.bs.tab').on('shown.bs.tab', (e) => {
            const target = $(e.target).data('bs-target');
            if (target === '#monolith-logs') {
                this.currentTab = 'monolith';
                this.loadMonolithLogs();
            } else if (target === '#system-logs') {
                this.currentTab = 'system';
                this.loadSystemLogs();
            } else if (target === '#security-logs') {
                this.currentTab = 'security';
                this.loadSecurityLogs();
            }
        });

        // Monolith category filter - use .off() first to prevent duplicate handlers
        $('#monolithCategoryMenu a').off('click').on('click', (e) => {
            e.preventDefault();
            $('#monolithCategoryMenu a').removeClass('active');
            $(e.currentTarget).addClass('active');
            this.currentCategory = $(e.currentTarget).data('category');
            this.pagination.monolith.offset = 0;
            this.loadMonolithLogs();
        });

        // Security sub-tabs - use .off() first to prevent duplicate handlers
        $('#securitySubTabs button').off('click').on('click', (e) => {
            e.preventDefault();
            $('#securitySubTabs button').removeClass('active');
            $(e.currentTarget).addClass('active');
            this.currentSecurityCategory = $(e.currentTarget).data('category');
            this.pagination.security.offset = 0;
            this.loadSecurityLogs();
        });

        // Filter buttons - use .off() first to prevent duplicate handlers
        $('#btnApplyMonolithFilters').off('click').on('click', () => {
            this.filters.monolith.level = $('#monolithLevelFilter').val();
            this.filters.monolith.source = $('#monolithSourceFilter').val();
            this.filters.monolith.startDate = $('#monolithStartDate').val();
            this.filters.monolith.endDate = $('#monolithEndDate').val();
            this.pagination.monolith.offset = 0;
            this.loadMonolithLogs();
        });

        $('#btnApplySystemFilters').off('click').on('click', () => {
            this.filters.system.category = $('#systemCategoryFilter').val();
            this.filters.system.level = $('#systemLevelFilter').val();
            this.filters.system.startDate = $('#systemStartDate').val();
            this.filters.system.endDate = $('#systemEndDate').val();
            this.pagination.system.offset = 0;
            this.loadSystemLogs();
        });

        $('#btnApplySecurityFilters').off('click').on('click', () => {
            this.filters.security.level = $('#securityLevelFilter').val();
            this.filters.security.source = $('#securitySourceFilter').val();
            this.filters.security.startDate = $('#securityStartDate').val();
            this.filters.security.endDate = $('#securityEndDate').val();
            this.pagination.security.offset = 0;
            this.loadSecurityLogs();
        });

        // Pagination - use .off() first to prevent duplicate handlers
        $('#btnMonolithPrev').off('click').on('click', () => {
            this.pagination.monolith.offset = Math.max(0, this.pagination.monolith.offset - this.pagination.monolith.limit);
            this.loadMonolithLogs();
        });

        $('#btnMonolithNext').off('click').on('click', () => {
            this.pagination.monolith.offset += this.pagination.monolith.limit;
            this.loadMonolithLogs();
        });

        $('#btnSystemPrev').off('click').on('click', () => {
            this.pagination.system.offset = Math.max(0, this.pagination.system.offset - this.pagination.system.limit);
            this.loadSystemLogs();
        });

        $('#btnSystemNext').off('click').on('click', () => {
            this.pagination.system.offset += this.pagination.system.limit;
            this.loadSystemLogs();
        });

        $('#btnSecurityPrev').off('click').on('click', () => {
            this.pagination.security.offset = Math.max(0, this.pagination.security.offset - this.pagination.security.limit);
            this.loadSecurityLogs();
        });

        $('#btnSecurityNext').off('click').on('click', () => {
            this.pagination.security.offset += this.pagination.security.limit;
            this.loadSecurityLogs();
        });
    }
};

// Register with Monolith.Pages
if (typeof Monolith !== 'undefined') {
    Monolith.Pages = Monolith.Pages || {};
    Monolith.Pages.SystemLogs = SystemLogs;
}
