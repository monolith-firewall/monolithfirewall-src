// Firewall NAT Module
var Nat = {
    rules: [],
    aliases: [],
    interfaces: [],
    schedules: [],
    activeTab: 'port_forward', // Track active tab: port_forward, one_to_one, outbound

    init: function() {
        console.log('Initializing NAT module...');
    },

    renderPage: function() {
        console.log('NAT: renderPage() called - checking if Razor page exists...');
        const content = $('#page-content');
        
        // Check if the Razor page with tabs already exists
        const existingPage = content.find('[data-init-nat="true"]');
        if (existingPage.length > 0) {
            console.log('NAT: Razor page with tabs found, initializing...');
            // Razor page already loaded, just initialize it
            this.initializePage();
            return;
        }

        // Fallback: If Razor page doesn't exist, create tabbed structure dynamically
        console.log('NAT: Razor page not found, creating tabbed structure dynamically...');
        content.empty();

        content.append(`
            <div class="package-page nat-page" data-module="nat" data-package="firewall" data-init-nat="true">
                <div class="container-fluid p-4">
                    <div class="row mb-4">
                        <div class="col-12">
                            <h2 class="page-title">
                                <svg width="24" height="24" fill="currentColor" viewBox="0 0 16 16" class="me-2">
                                    <path d="M8 0a8 8 0 1 0 0 16A8 8 0 0 0 8 0zM4.5 7.5a.5.5 0 0 0 0 1h7a.5.5 0 0 0 0-1h-7z"/>
                                </svg>
                                NAT Rules
                            </h2>
                            <p class="text-muted">Configure Network Address Translation (NAT) rules</p>
                        </div>
                    </div>

                    <div id="natStatusMessage" class="alert d-none"></div>

                    <div id="pendingChangesBanner" class="alert alert-warning d-none mb-3">
                        <div class="d-flex justify-content-between align-items-center">
                            <div>
                                <strong>⚠ Pending Changes</strong>
                                <span class="ms-2">You have unsaved changes. Apply or discard them before making additional changes.</span>
                            </div>
                            <div>
                                <button type="button" class="btn btn-sm btn-success me-2" id="btnApplyChanges">Apply Changes</button>
                                <button type="button" class="btn btn-sm btn-secondary" id="btnDiscardChanges">Discard</button>
                            </div>
                        </div>
                    </div>

                    <div class="card">
                        <div class="card-header d-flex justify-content-between align-items-center">
                            <h5 class="mb-0">NAT Rules</h5>
                            <button type="button" class="btn btn-primary" id="btnAddRule">
                                <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-1">
                                    <path d="M8 4a.5.5 0 0 1 .5.5v3h3a.5.5 0 0 1 0 1h-3v3a.5.5 0 0 1-1 0v-3h-3a.5.5 0 0 1 0-1h3v-3A.5.5 0 0 1 8 4z"/>
                                </svg>
                                Add Rule
                            </button>
                        </div>
                        <div class="card-body">
                            <!-- Tabs Navigation -->
                            <ul class="nav nav-tabs mb-3" id="natTabs" role="tablist">
                                <li class="nav-item" role="presentation">
                                    <button class="nav-link active" id="portForwardTabBtn" data-bs-toggle="tab" data-bs-target="#portForwardTab" type="button" role="tab" aria-controls="portForwardTab" aria-selected="true" data-nat-type="port_forward">
                                        <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-1">
                                            <path d="M8 0a8 8 0 1 0 0 16A8 8 0 0 0 8 0zM4.5 7.5a.5.5 0 0 0 0 1h7a.5.5 0 0 0 0-1h-7z"/>
                                        </svg>
                                        Port Forward
                                    </button>
                                </li>
                                <li class="nav-item" role="presentation">
                                    <button class="nav-link" id="oneToOneTabBtn" data-bs-toggle="tab" data-bs-target="#oneToOneTab" type="button" role="tab" aria-controls="oneToOneTab" aria-selected="false" data-nat-type="one_to_one">
                                        <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-1">
                                            <path d="M8 0a8 8 0 1 0 0 16A8 8 0 0 0 8 0zM4.5 7.5a.5.5 0 0 0 0 1h7a.5.5 0 0 0 0-1h-7z"/>
                                        </svg>
                                        1:1
                                    </button>
                                </li>
                                <li class="nav-item" role="presentation">
                                    <button class="nav-link" id="outboundTabBtn" data-bs-toggle="tab" data-bs-target="#outboundTab" type="button" role="tab" aria-controls="outboundTab" aria-selected="false" data-nat-type="outbound">
                                        <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-1">
                                            <path d="M8 0a8 8 0 1 0 0 16A8 8 0 0 0 8 0zM4.5 7.5a.5.5 0 0 0 0 1h7a.5.5 0 0 0 0-1h-7z"/>
                                        </svg>
                                        Outbound
                                    </button>
                                </li>
                            </ul>

                            <!-- Tab Content -->
                            <div class="tab-content" id="natTabContent">
                                <!-- Port Forward Tab -->
                                <div class="tab-pane fade show active" id="portForwardTab" role="tabpanel" aria-labelledby="portForwardTabBtn">
                                    <div class="table-responsive">
                                        <table class="table table-hover" id="natTablePortForward">
                                            <thead>
                                                <tr>
                                                    <th style="width: 60px;">#</th>
                                                    <th>Interface</th>
                                                    <th>Family</th>
                                                    <th>Protocol</th>
                                                    <th>Source</th>
                                                    <th>Destination</th>
                                                    <th>Redirect Target</th>
                                                    <th>Description</th>
                                                    <th>Status</th>
                                                    <th>Actions</th>
                                                </tr>
                                            </thead>
                                            <tbody id="natTableBodyPortForward">
                                                <tr><td colspan="10" class="text-center text-muted">Loading NAT rules...</td></tr>
                                            </tbody>
                                        </table>
                                    </div>
                                </div>

                                <!-- 1:1 Tab -->
                                <div class="tab-pane fade" id="oneToOneTab" role="tabpanel" aria-labelledby="oneToOneTabBtn">
                                    <div class="table-responsive">
                                        <table class="table table-hover" id="natTableOneToOne">
                                            <thead>
                                                <tr>
                                                    <th style="width: 60px;">#</th>
                                                    <th>Interface</th>
                                                    <th>Family</th>
                                                    <th>Source IP</th>
                                                    <th>Destination IP</th>
                                                    <th>Redirect Target IP</th>
                                                    <th>Description</th>
                                                    <th>Status</th>
                                                    <th>Actions</th>
                                                </tr>
                                            </thead>
                                            <tbody id="natTableBodyOneToOne">
                                                <tr><td colspan="9" class="text-center text-muted">Loading NAT rules...</td></tr>
                                            </tbody>
                                        </table>
                                    </div>
                                </div>

                                <!-- Outbound Tab -->
                                <div class="tab-pane fade" id="outboundTab" role="tabpanel" aria-labelledby="outboundTabBtn">
                                    <div class="table-responsive">
                                        <table class="table table-hover" id="natTableOutbound">
                                            <thead>
                                                <tr>
                                                    <th style="width: 60px;">#</th>
                                                    <th>Interface</th>
                                                    <th>Family</th>
                                                    <th>Protocol</th>
                                                    <th>Source</th>
                                                    <th>Destination</th>
                                                    <th>NAT Target</th>
                                                    <th>Description</th>
                                                    <th>Status</th>
                                                    <th>Actions</th>
                                                </tr>
                                            </thead>
                                            <tbody id="natTableBodyOutbound">
                                                <tr><td colspan="10" class="text-center text-muted">Loading NAT rules...</td></tr>
                                            </tbody>
                                        </table>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `);

        // Initialize the page
        this.initializePage();
    },

    loadInterfaces: async function() {
        try {
            const response = await Monolith.API.get('/interfaces/assignments');
            if (response.Success || response.success) {
                const data = response.Data || response.data || {};
                const assigned = data.Assigned || data.assigned || data || [];
                const items = Array.isArray(assigned) ? assigned : [];
                this.interfaces = items.map(i => ({
                    interface: i.Interface || i.interface,
                    name: i.Name || i.name
                }));
            }
        } catch (error) {
            console.error('Failed to load interfaces:', error);
        }
    },

    loadSchedules: async function() {
        try {
            const response = await Monolith.API.get('/firewall/schedules');
            if (response.success || response.Success) {
                const data = response.data || response.Data || {};
                const items = data.items || data || [];
                this.schedules = Array.isArray(items) ? items : [];
            }
        } catch (error) {
            console.warn('Failed to load schedules:', error);
        }
    },

    loadAliases: async function() {
        try {
            const response = await Monolith.API.get('/firewall/aliases');
            if (response.Success || response.success) {
                const data = response.Data || response.data || {};
                const items = data.items || data || [];
                const aliasArray = Array.isArray(items) ? items : [];
                this.aliases = aliasArray.map(a => ({
                    id: a.Id || a.id,
                    name: a.Name || a.name,
                    type: a.Type || a.type
                }));
            }
        } catch (error) {
            console.warn('Failed to load aliases for NAT helper list:', error);
            this.aliases = [];
        }
    },

    loadRules: async function() {
        try {
            const response = await Monolith.API.get('/firewall/nat');
            if (response.Success || response.success) {
                const data = response.Data || response.data || {};
                const items = data.items || data || [];
                const rulesArray = Array.isArray(items) ? items : (Array.isArray(data) ? data : []);
                this.rules = rulesArray.map(r => this.normalizeRule(r));
            } else {
                this.rules = [];
            }
            this.renderRules();
        } catch (error) {
            console.error('Error loading NAT rules:', error);
            this.showMessage('Failed to load NAT rules', 'danger');
            this.rules = [];
            this.renderRules();
        }
    },

    // Initialize when page is loaded (for Razor page direct access)
    initializePage: function() {
        console.log('NAT: Initializing page...');
        this.init();
        this.activeTab = 'port_forward';
        this.loadInterfaces();
        this.loadSchedules();
        this.loadAliases();
        this.loadRules();
        this.attachEventHandlers();
    },

    normalizeRule: function(rule) {
        return {
            id: rule.Id || rule.id,
            ruleNumber: rule.RuleNumber || rule.ruleNumber,
            type: rule.Type || rule.type || 'port_forward',
            interface: rule.Interface || rule.interface,
            addressFamily: rule.AddressFamily || rule.addressFamily || 'ipv4',
            protocol: rule.Protocol || rule.protocol,
            sourceType: rule.SourceType || rule.sourceType || 'any',
            sourceValue: rule.SourceValue || rule.sourceValue,
            sourcePort: rule.SourcePort || rule.sourcePort,
            destinationType: rule.DestinationType || rule.destinationType || 'any',
            destinationValue: rule.DestinationValue || rule.destinationValue,
            destinationPort: rule.DestinationPort || rule.destinationPort,
            redirectTargetIp: rule.RedirectTargetIp || rule.redirectTargetIp,
            redirectTargetPort: rule.RedirectTargetPort || rule.redirectTargetPort,
            reflectionMode: rule.ReflectionMode || rule.reflectionMode || 'default',
            description: rule.Description || rule.description,
            enabled: rule.Enabled !== undefined ? rule.Enabled : (rule.enabled !== undefined ? rule.enabled : true)
        };
    },

    renderRules: function() {
        // Render rules for all tabs
        this.renderRulesByType('port_forward');
        this.renderRulesByType('one_to_one');
        this.renderRulesByType('outbound');
    },

    renderRulesByType: function(type) {
        // Filter rules by type
        const filteredRules = this.rules.filter(rule => rule.type === type);
        
        // Determine table body ID based on type
        let tbodyId, colspan;
        if (type === 'port_forward') {
            tbodyId = '#natTableBodyPortForward';
            colspan = 10;
        } else if (type === 'one_to_one') {
            tbodyId = '#natTableBodyOneToOne';
            colspan = 9;
        } else { // outbound
            tbodyId = '#natTableBodyOutbound';
            colspan = 10;
        }

        const tbody = $(tbodyId);
        if (!tbody.length) return;

        if (filteredRules.length === 0) {
            tbody.html(`<tr><td colspan="${colspan}" class="text-center text-muted">No ${this.getTypeLabel(type)} rules configured</td></tr>`);
            return;
        }

        let html = '';
        filteredRules.forEach(rule => {
            const statusBadge = rule.enabled
                ? '<span class="badge bg-success">Enabled</span>'
                : '<span class="badge bg-secondary">Disabled</span>';

            if (type === 'port_forward') {
                // Port Forward table columns
                html += `
                    <tr data-rule-id="${rule.id}">
                        <td><strong>${rule.ruleNumber || ''}</strong></td>
                        <td><code>${rule.interface || '-'}</code></td>
                        <td><span class="badge bg-info">${this.formatFamily(rule.addressFamily)}</span></td>
                        <td><span class="badge bg-secondary">${rule.protocol || 'any'}</span></td>
                        <td>${this.formatAddress(rule.sourceType, rule.sourceValue, rule.sourcePort)}</td>
                        <td>${this.formatAddress(rule.destinationType, rule.destinationValue, rule.destinationPort)}</td>
                        <td>${this.formatTarget(rule.redirectTargetIp, rule.redirectTargetPort)}</td>
                        <td>${rule.description || '-'}</td>
                        <td>${statusBadge}</td>
                        <td>
                            <button class="btn btn-sm btn-outline-primary me-1" data-action="edit-nat" data-id="${rule.id}">Edit</button>
                            <button class="btn btn-sm btn-outline-danger" data-action="delete-nat" data-id="${rule.id}">Delete</button>
                        </td>
                    </tr>
                `;
            } else if (type === 'one_to_one') {
                // 1:1 table columns (no ports)
                html += `
                    <tr data-rule-id="${rule.id}">
                        <td><strong>${rule.ruleNumber || ''}</strong></td>
                        <td><code>${rule.interface || '-'}</code></td>
                        <td><span class="badge bg-info">${this.formatFamily(rule.addressFamily)}</span></td>
                        <td>${this.formatAddress(rule.sourceType, rule.sourceValue, null)}</td>
                        <td>${this.formatAddress(rule.destinationType, rule.destinationValue, null)}</td>
                        <td>${rule.redirectTargetIp || '-'}</td>
                        <td>${rule.description || '-'}</td>
                        <td>${statusBadge}</td>
                        <td>
                            <button class="btn btn-sm btn-outline-primary me-1" data-action="edit-nat" data-id="${rule.id}">Edit</button>
                            <button class="btn btn-sm btn-outline-danger" data-action="delete-nat" data-id="${rule.id}">Delete</button>
                        </td>
                    </tr>
                `;
            } else { // outbound
                // Outbound table columns
                html += `
                    <tr data-rule-id="${rule.id}">
                        <td><strong>${rule.ruleNumber || ''}</strong></td>
                        <td><code>${rule.interface || '-'}</code></td>
                        <td><span class="badge bg-info">${this.formatFamily(rule.addressFamily)}</span></td>
                        <td><span class="badge bg-secondary">${rule.protocol || 'any'}</span></td>
                        <td>${this.formatAddress(rule.sourceType, rule.sourceValue, rule.sourcePort)}</td>
                        <td>${this.formatAddress(rule.destinationType, rule.destinationValue, rule.destinationPort)}</td>
                        <td>${this.formatTarget(rule.redirectTargetIp, rule.redirectTargetPort)}</td>
                        <td>${rule.description || '-'}</td>
                        <td>${statusBadge}</td>
                        <td>
                            <button class="btn btn-sm btn-outline-primary me-1" data-action="edit-nat" data-id="${rule.id}">Edit</button>
                            <button class="btn btn-sm btn-outline-danger" data-action="delete-nat" data-id="${rule.id}">Delete</button>
                        </td>
                    </tr>
                `;
            }
        });
        tbody.html(html);
    },

    getTypeLabel: function(type) {
        switch(type) {
            case 'port_forward': return 'Port Forward';
            case 'one_to_one': return '1:1';
            case 'outbound': return 'Outbound';
            default: return 'NAT';
        }
    },

    formatFamily: function(family) {
        if (family === 'ipv6') return 'IPv6';
        if (family === 'dual') return 'IPv4/IPv6';
        return 'IPv4';
    },

    formatAddress: function(type, value, port) {
        if (type === 'any' || !type) return '<span class="text-muted">Any</span>';
        const valueDisplay = value ? (type === 'alias' ? `<code>${value}</code>` : value) : '-';
        if (!port) return valueDisplay;
        return `${valueDisplay}:${port}`;
    },

    formatTarget: function(ip, port) {
        if (!ip) return '-';
        return port ? `${ip}:${port}` : ip;
    },

    attachEventHandlers: function() {
        $(document).off('click', '#btnAddRule');
        $(document).on('click', '#btnAddRule', () => {
            // Pre-select type based on active tab
            this.showRuleModal(null, this.activeTab);
        });

        $(document).off('click', '[data-action="edit-nat"]');
        $(document).on('click', '[data-action="edit-nat"]', (e) => {
            const id = $(e.currentTarget).data('id');
            this.editRule(id);
        });

        $(document).off('click', '[data-action="delete-nat"]');
        $(document).on('click', '[data-action="delete-nat"]', (e) => {
            const id = $(e.currentTarget).data('id');
            this.deleteRule(id);
        });

        $(document).off('click', '#btnApplyChanges');
        $(document).on('click', '#btnApplyChanges', () => {
            this.applyChanges();
        });

        $(document).off('click', '#btnDiscardChanges');
        $(document).on('click', '#btnDiscardChanges', () => {
            this.discardChanges();
        });

        // Tab change handler
        $(document).off('shown.bs.tab', '#natTabs button[data-bs-toggle="tab"]');
        $(document).on('shown.bs.tab', '#natTabs button[data-bs-toggle="tab"]', (e) => {
            const tabButton = $(e.target);
            const natType = tabButton.data('nat-type');
            if (natType) {
                this.activeTab = natType;
                console.log('Active tab changed to:', natType);
            }
        });
    },

    showRuleModal: function(rule, defaultType) {
        const isEdit = rule !== null;
        const ruleType = rule ? rule.type : (defaultType || 'port_forward');
        const aliasOptions = this.aliases.map(a => `<option value="${a.name}">${a.name}</option>`).join('');
        
        const interfaceOptions = this.interfaces.map(i => 
            `<option value="${i.interface}" ${rule && rule.interface === i.interface ? 'selected' : ''}>${i.name} (${i.interface})</option>`
        ).join('');

        const scheduleOptions = [
            '<option value="">None (Always Active)</option>',
            ...this.schedules.map(s => `<option value="${s.id}" ${rule && rule.scheduleId === s.id ? 'selected' : ''}>${s.name}</option>`)
        ].join('');

        // Determine which fields to show based on type
        const showPorts = ruleType === 'port_forward' || ruleType === 'outbound';
        const showRedirectPort = ruleType === 'port_forward';
        const isOneToOne = ruleType === 'one_to_one';
        const isOutbound = ruleType === 'outbound';

        const modalHtml = `
            <div class="modal fade" id="natRuleModal" tabindex="-1" aria-labelledby="natRuleModalLabel" aria-hidden="true">
                <div class="modal-dialog modal-xl">
                    <div class="modal-content">
                        <div class="modal-header">
                            <h5 class="modal-title" id="natRuleModalLabel">${isEdit ? 'Edit' : 'Add'} ${this.getTypeLabel(ruleType)} NAT Rule</h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                        </div>
                        <div class="modal-body">
                            <form id="natRuleForm">
                                <div class="row">
                                    <div class="col-md-4 mb-3">
                                        <label for="natType" class="form-label">Type <span class="text-danger">*</span></label>
                                        <select class="form-select" id="natType" required>
                                            <option value="port_forward" ${ruleType === 'port_forward' ? 'selected' : ''}>Port Forward</option>
                                            <option value="one_to_one" ${ruleType === 'one_to_one' ? 'selected' : ''}>1:1 NAT</option>
                                            <option value="outbound" ${ruleType === 'outbound' ? 'selected' : ''}>Outbound</option>
                                        </select>
                                    </div>
                                    <div class="col-md-4 mb-3">
                                        <label for="natInterface" class="form-label">Interface <span class="text-danger">*</span></label>
                                        <select class="form-select" id="natInterface" required>
                                            <option value="" disabled ${!rule ? 'selected' : ''}>Select Interface...</option>
                                            ${interfaceOptions}
                                        </select>
                                    </div>
                                    <div class="col-md-4 mb-3">
                                        <label for="natAddressFamily" class="form-label">Address Family</label>
                                        <select class="form-select" id="natAddressFamily">
                                            <option value="ipv4" ${rule && rule.addressFamily === 'ipv4' ? 'selected' : ''}>IPv4</option>
                                            <option value="ipv6" ${rule && rule.addressFamily === 'ipv6' ? 'selected' : ''}>IPv6</option>
                                            <option value="dual" ${rule && rule.addressFamily === 'dual' ? 'selected' : ''}>IPv4 + IPv6</option>
                                        </select>
                                    </div>
                                </div>
                                <div class="row">
                                    <div class="col-md-4 mb-3">
                                        <label for="natProtocol" class="form-label">Protocol</label>
                                        <select class="form-select" id="natProtocol">
                                            <option value="tcp" ${rule && rule.protocol === 'tcp' ? 'selected' : ''}>TCP</option>
                                            <option value="udp" ${rule && rule.protocol === 'udp' ? 'selected' : ''}>UDP</option>
                                            <option value="tcp/udp" ${rule && rule.protocol === 'tcp/udp' ? 'selected' : ''}>TCP/UDP</option>
                                            <option value="icmp" ${rule && rule.protocol === 'icmp' ? 'selected' : ''}>ICMP</option>
                                            <option value="any" ${rule && rule.protocol === 'any' ? 'selected' : ''}>Any</option>
                                        </select>
                                    </div>
                                    <div class="col-md-4 mb-3">
                                        <label for="natReflectionMode" class="form-label">Reflection</label>
                                        <select class="form-select" id="natReflectionMode">
                                            <option value="default" ${rule && rule.reflectionMode === 'default' ? 'selected' : ''}>Default</option>
                                            <option value="proxy" ${rule && rule.reflectionMode === 'proxy' ? 'selected' : ''}>Proxy</option>
                                            <option value="nat" ${rule && rule.reflectionMode === 'nat' ? 'selected' : ''}>NAT</option>
                                            <option value="disabled" ${rule && rule.reflectionMode === 'disabled' ? 'selected' : ''}>Disabled</option>
                                        </select>
                                    </div>
                                    <div class="col-md-4 mb-3">
                                        <label for="natSchedule" class="form-label">Schedule</label>
                                        <select class="form-select" id="natSchedule">
                                            ${scheduleOptions}
                                        </select>
                                    </div>
                                </div>
                                <div class="alert alert-info d-none" id="natIpv6Note">
                                    NAT is not applicable for pure IPv6. Only Outbound rules with IPv4 or dual-stack are supported.
                                </div>
                                <div class="row">
                                    <div class="col-md-4 mb-3">
                                        <label for="natSourceType" class="form-label">Source Type</label>
                                        <select class="form-select" id="natSourceType">
                                            <option value="any" ${rule && rule.sourceType === 'any' ? 'selected' : ''}>Any</option>
                                            <option value="single" ${rule && rule.sourceType === 'single' ? 'selected' : ''}>Single</option>
                                            <option value="network" ${rule && rule.sourceType === 'network' ? 'selected' : ''}>Network</option>
                                            <option value="alias" ${rule && rule.sourceType === 'alias' ? 'selected' : ''}>Alias</option>
                                        </select>
                                    </div>
                                    <div class="col-md-4 mb-3">
                                        <label for="natSourceValue" class="form-label">Source Value</label>
                                        <input type="text" class="form-control" id="natSourceValue"
                                               value="${rule ? (rule.sourceValue || '') : ''}"
                                               list="natAliasList"
                                               placeholder="IP, network, or alias">
                                    </div>
                                    <div class="col-md-4 mb-3 nat-field-port" style="${showPorts ? '' : 'display: none;'}">
                                        <label for="natSourcePort" class="form-label">Source Port</label>
                                        <input type="text" class="form-control" id="natSourcePort"
                                               value="${rule ? (rule.sourcePort || '') : ''}"
                                               placeholder="e.g., 80">
                                    </div>
                                </div>
                                <div class="row">
                                    <div class="col-md-4 mb-3">
                                        <label for="natDestinationType" class="form-label">Destination Type</label>
                                        <select class="form-select" id="natDestinationType">
                                            <option value="any" ${rule && rule.destinationType === 'any' ? 'selected' : ''}>Any</option>
                                            <option value="single" ${rule && rule.destinationType === 'single' ? 'selected' : ''}>Single</option>
                                            <option value="network" ${rule && rule.destinationType === 'network' ? 'selected' : ''}>Network</option>
                                            <option value="alias" ${rule && rule.destinationType === 'alias' ? 'selected' : ''}>Alias</option>
                                        </select>
                                    </div>
                                    <div class="col-md-4 mb-3">
                                        <label for="natDestinationValue" class="form-label">Destination Value</label>
                                        <input type="text" class="form-control" id="natDestinationValue"
                                               value="${rule ? (rule.destinationValue || '') : ''}"
                                               list="natAliasList"
                                               placeholder="IP, network, or alias">
                                    </div>
                                    <div class="col-md-4 mb-3 nat-field-port" style="${showPorts ? '' : 'display: none;'}">
                                        <label for="natDestinationPort" class="form-label">Destination Port</label>
                                        <input type="text" class="form-control" id="natDestinationPort"
                                               value="${rule ? (rule.destinationPort || '') : ''}"
                                               placeholder="e.g., 443">
                                    </div>
                                </div>
                                <div class="row">
                                    <div class="col-md-6 mb-3">
                                        <label for="natRedirectTargetIp" class="form-label">${isOutbound ? 'NAT Target IP (SNAT)' : 'Redirect Target IP (DNAT)'} <span class="text-danger">*</span></label>
                                        <input type="text" class="form-control" id="natRedirectTargetIp" required
                                               value="${rule ? (rule.redirectTargetIp || '') : ''}"
                                               placeholder="${isOutbound ? 'SNAT Target IP' : 'Target IP'}">
                                    </div>
                                    <div class="col-md-6 mb-3 nat-field-redirect-port" style="${showRedirectPort ? '' : 'display: none;'}">
                                        <label for="natRedirectTargetPort" class="form-label">Redirect Target Port</label>
                                        <input type="text" class="form-control" id="natRedirectTargetPort"
                                               value="${rule ? (rule.redirectTargetPort || '') : ''}"
                                               placeholder="Target port">
                                    </div>
                                </div>
                                <div class="mb-3">
                                    <label for="natDescription" class="form-label">Description</label>
                                    <input type="text" class="form-control" id="natDescription"
                                           value="${rule ? (rule.description || '') : ''}"
                                           placeholder="Optional description">
                                </div>
                                <div class="mb-3 form-check">
                                    <input type="checkbox" class="form-check-input" id="natEnabled"
                                           ${rule && rule.enabled !== false ? 'checked' : ''}>
                                    <label class="form-check-label" for="natEnabled">
                                        Enabled
                                    </label>
                                </div>
                                <datalist id="natAliasList">
                                    ${aliasOptions}
                                </datalist>
                            </form>
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
                            <button type="button" class="btn btn-primary" data-action="save-nat-submit">${isEdit ? 'Update' : 'Create'}</button>
                        </div>
                    </div>
                </div>
            </div>
        `;

        $('#natRuleModal').remove();
        $('body').append(modalHtml);
        const modal = new bootstrap.Modal(document.getElementById('natRuleModal'));
        modal.show();

        // Initialize field visibility based on type
        this.toggleNatFieldsByType(ruleType);
        
        // Show/hide IPv6 note based on address family
        const updateIpv6Note = () => {
            const addressFamily = $('#natAddressFamily').val();
            const type = $('#natType').val();
            const ipv6Note = $('#natIpv6Note');
            if (addressFamily === 'ipv6' && (type === 'port_forward' || type === 'one_to_one')) {
                ipv6Note.removeClass('d-none');
            } else {
                ipv6Note.addClass('d-none');
            }
        };
        
        updateIpv6Note();
        
        // Update fields when type changes
        $('#natType').on('change', () => {
            const newType = $('#natType').val();
            this.toggleNatFieldsByType(newType);
            updateIpv6Note();
        });
        
        $('#natAddressFamily').on('change', updateIpv6Note);

        $(document).off('click', '[data-action="save-nat-submit"]');
        $(document).on('click', '[data-action="save-nat-submit"]', () => {
            this.saveRule(rule ? rule.id : null);
        });

        $('#natRuleModal').on('hidden.bs.modal', function() {
            $(this).remove();
        });
    },

    editRule: async function(id) {
        try {
            const response = await Monolith.API.get(`/firewall/nat/${id}`);
            if (response.Success || response.success) {
                const rule = this.normalizeRule(response.Data || response.data);
                this.showRuleModal(rule, rule.type);
            } else {
                this.showMessage('Failed to load NAT rule', 'danger');
            }
        } catch (error) {
            console.error('Error loading NAT rule:', error);
            this.showMessage('Failed to load NAT rule', 'danger');
        }
    },

    saveRule: async function(id) {
        const form = document.getElementById('natRuleForm');
        if (!form || !form.checkValidity()) {
            if (form) form.reportValidity();
            return;
        }

        const scheduleId = $('#natSchedule').val();
        const rule = {
            type: $('#natType').val(),
            interface: $('#natInterface').val(),
            addressFamily: $('#natAddressFamily').val(),
            protocol: $('#natProtocol').val(),
            sourceType: $('#natSourceType').val(),
            sourceValue: $('#natSourceValue').val().trim() || null,
            sourcePort: $('#natSourcePort').val().trim() || null,
            destinationType: $('#natDestinationType').val(),
            destinationValue: $('#natDestinationValue').val().trim() || null,
            destinationPort: $('#natDestinationPort').val().trim() || null,
            redirectTargetIp: $('#natRedirectTargetIp').val().trim(),
            redirectTargetPort: $('#natRedirectTargetPort').val().trim() || null,
            reflectionMode: $('#natReflectionMode').val(),
            description: $('#natDescription').val().trim(),
            enabled: $('#natEnabled').is(':checked'),
            scheduleId: scheduleId ? parseInt(scheduleId) : null
        };

        // Guard IPv6 NAT cases
        if (rule.addressFamily === 'ipv6' && (rule.type === 'port_forward' || rule.type === 'one_to_one')) {
            Monolith.UI.toast('IPv6 does not support port forward / 1:1 NAT. Use firewall rules instead.', 'warning');
            return;
        }

        try {
            let response;
            if (id) {
                response = await Monolith.API.put(`/firewall/nat/${id}`, rule);
            } else {
                response = await Monolith.API.post('/firewall/nat', rule);
            }

            if (response.Success || response.success) {
                bootstrap.Modal.getInstance(document.getElementById('natRuleModal')).hide();
                this.showMessage(id ? 'NAT rule updated successfully' : 'NAT rule created successfully', 'success');
                this.loadRules();
                this.markPendingChanges();
            } else {
                this.showMessage(response.error || response.Error || 'Failed to save NAT rule', 'danger');
            }
        } catch (error) {
            console.error('Error saving NAT rule:', error);
            this.showMessage('Failed to save NAT rule', 'danger');
        }
    },

    deleteRule: async function(id) {
        if (!confirm('Are you sure you want to delete this NAT rule? This action cannot be undone.')) {
            return;
        }

        try {
            const response = await Monolith.API.delete(`/firewall/nat/${id}`);
            if (response.Success || response.success) {
                this.showMessage('NAT rule deleted successfully', 'success');
                this.loadRules();
                this.markPendingChanges();
            } else {
                this.showMessage(response.error || response.Error || 'Failed to delete NAT rule', 'danger');
            }
        } catch (error) {
            console.error('Error deleting NAT rule:', error);
            this.showMessage('Failed to delete NAT rule', 'danger');
        }
    },

    markPendingChanges: function() {
        $('#pendingChangesBanner').removeClass('d-none');
    },

    showMessage: function(message, type) {
        const alert = $('#natStatusMessage');
        if (!alert.length) return;
        alert.removeClass('d-none alert-success alert-danger alert-warning alert-info')
             .addClass(`alert-${type}`)
             .text(message);
        setTimeout(() => alert.addClass('d-none'), 5000);
    },

    applyChanges: async function() {
        if (!confirm('Apply all pending firewall changes? This will update the system configuration.')) {
            return;
        }

        try {
            const response = await Monolith.API.post('/firewall/apply', {});
            if (response.Success || response.success) {
                this.showMessage('Changes applied successfully', 'success');
                $('#pendingChangesBanner').addClass('d-none');
            } else {
                this.showMessage(response.error || response.Error || 'Failed to apply changes', 'danger');
            }
        } catch (error) {
            console.error('Error applying changes:', error);
            this.showMessage('Failed to apply changes', 'danger');
        }
    },

    discardChanges: async function() {
        if (!confirm('Discard all pending changes? This will revert all unsaved modifications.')) {
            return;
        }

        try {
            const response = await Monolith.API.post('/firewall/discard', {});
            if (response.Success || response.success) {
                this.showMessage('Changes discarded', 'info');
                $('#pendingChangesBanner').addClass('d-none');
                this.loadRules();
            } else {
                this.showMessage(response.error || response.Error || 'Failed to discard changes', 'danger');
            }
        } catch (error) {
            console.error('Error discarding changes:', error);
            this.showMessage('Failed to discard changes', 'danger');
        }
    },

    toggleNatFieldsByType: function(type) {
        // Show/hide fields based on NAT type
        const showPorts = type === 'port_forward' || type === 'outbound';
        const showRedirectPort = type === 'port_forward';
        const isOneToOne = type === 'one_to_one';
        const isOutbound = type === 'outbound';

        // Toggle port fields
        $('.nat-field-port').toggle(showPorts);
        
        // Toggle redirect target port (only for port forward)
        $('.nat-field-redirect-port').toggle(showRedirectPort);

        // Update redirect target label
        const redirectLabel = $('#natRedirectTargetIp').closest('.mb-3').find('label');
        if (redirectLabel.length) {
            if (isOutbound) {
                redirectLabel.html('NAT Target IP (SNAT) <span class="text-danger">*</span>');
                $('#natRedirectTargetIp').attr('placeholder', 'SNAT Target IP');
            } else {
                redirectLabel.html('Redirect Target IP (DNAT) <span class="text-danger">*</span>');
                $('#natRedirectTargetIp').attr('placeholder', 'Target IP');
            }
        }

        // Update modal title
        const modalTitle = $('#natRuleModalLabel');
        if (modalTitle.length) {
            const isEdit = modalTitle.text().includes('Edit');
            modalTitle.text(`${isEdit ? 'Edit' : 'Add'} ${this.getTypeLabel(type)} NAT Rule`);
        }
    }
};

// Register with Monolith.Pages.Firewall
if (typeof Monolith !== 'undefined') {
    Monolith.Pages = Monolith.Pages || {};
    Monolith.Pages.Firewall = Monolith.Pages.Firewall || {};
    Monolith.Pages.Firewall.Nat = Nat;
    // Also register at root level for backward compatibility
    Monolith.Pages.Nat = Nat;
}
