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
                                        <i class="fas fa-arrow-right me-1"></i>
                                        Port Forward
                                    </button>
                                </li>
                                <li class="nav-item" role="presentation">
                                    <button class="nav-link" id="oneToOneTabBtn" data-bs-toggle="tab" data-bs-target="#oneToOneTab" type="button" role="tab" aria-controls="oneToOneTab" aria-selected="false" data-nat-type="one_to_one">
                                        <i class="fas fa-exchange-alt me-1"></i>
                                        1:1
                                    </button>
                                </li>
                                <li class="nav-item" role="presentation">
                                    <button class="nav-link" id="outboundTabBtn" data-bs-toggle="tab" data-bs-target="#outboundTab" type="button" role="tab" aria-controls="outboundTab" aria-selected="false" data-nat-type="outbound">
                                        <i class="fas fa-sign-out-alt me-1"></i>
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
            // Call type-specific modal based on active tab
            if (this.activeTab === 'port_forward') {
                this.showPortForwardModal(null);
            } else if (this.activeTab === 'one_to_one') {
                this.showOneToOneModal(null);
            } else if (this.activeTab === 'outbound') {
                this.showOutboundModal(null);
            }
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

        // Tab change handler - track active tab
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

    showPortForwardModal: function(rule) {
        const isEdit = rule !== null;
        const aliasOptions = this.aliases.map(a => `<option value="${a.name}">${a.name}</option>`).join('');
        
        const interfaceOptions = this.interfaces.map(i => 
            `<option value="${i.interface}" ${rule && rule.interface === i.interface ? 'selected' : ''}>${i.name} (${i.interface})</option>`
        ).join('');

        const scheduleOptions = [
            '<option value="">None (Always Active)</option>',
            ...this.schedules.map(s => `<option value="${s.id}" ${rule && rule.scheduleId === s.id ? 'selected' : ''}>${s.name}</option>`)
        ].join('');

        const modalHtml = `
            <div class="modal fade" id="portForwardModal" tabindex="-1" aria-labelledby="portForwardModalLabel" aria-hidden="true">
                <div class="modal-dialog modal-xl">
                    <div class="modal-content">
                        <div class="modal-header">
                            <h5 class="modal-title" id="portForwardModalLabel">${isEdit ? 'Edit' : 'Add'} Port Forwarding Rule</h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                        </div>
                        <div class="modal-body">
                            <form id="portForwardForm">
                                <div class="alert alert-warning" id="pfIpv6Warning" style="display: none;">
                                    <strong>Note:</strong> IPv6 does not support port forwarding. Use firewall rules instead.
                                </div>
                                <div class="row">
                                    <div class="col-md-6 mb-3">
                                        <label for="pfInterface" class="form-label">Interface <span class="text-danger">*</span></label>
                                        <select class="form-select" id="pfInterface" required>
                                            <option value="" disabled ${!rule ? 'selected' : ''}>Select Interface...</option>
                                            ${interfaceOptions}
                                        </select>
                                    </div>
                                    <div class="col-md-6 mb-3">
                                        <label for="pfAddressFamily" class="form-label">Address Family</label>
                                        <select class="form-select" id="pfAddressFamily">
                                            <option value="ipv4" ${rule && rule.addressFamily === 'ipv4' ? 'selected' : 'selected'}>IPv4</option>
                                            <option value="ipv6" ${rule && rule.addressFamily === 'ipv6' ? 'selected' : ''}>IPv6</option>
                                            <option value="dual" ${rule && rule.addressFamily === 'dual' ? 'selected' : ''}>IPv4 + IPv6</option>
                                        </select>
                                    </div>
                                </div>
                                <div class="row">
                                    <div class="col-md-4 mb-3">
                                        <label for="pfProtocol" class="form-label">Protocol <span class="text-danger">*</span></label>
                                        <select class="form-select" id="pfProtocol" required>
                                            <option value="tcp" ${rule && rule.protocol === 'tcp' ? 'selected' : 'selected'}>TCP</option>
                                            <option value="udp" ${rule && rule.protocol === 'udp' ? 'selected' : ''}>UDP</option>
                                            <option value="tcp/udp" ${rule && rule.protocol === 'tcp/udp' ? 'selected' : ''}>TCP/UDP</option>
                                            <option value="icmp" ${rule && rule.protocol === 'icmp' ? 'selected' : ''}>ICMP</option>
                                        </select>
                                    </div>
                                    <div class="col-md-4 mb-3">
                                        <label for="pfReflectionMode" class="form-label">Reflection Mode</label>
                                        <select class="form-select" id="pfReflectionMode">
                                            <option value="default" ${rule && rule.reflectionMode === 'default' ? 'selected' : 'selected'}>Default</option>
                                            <option value="proxy" ${rule && rule.reflectionMode === 'proxy' ? 'selected' : ''}>Proxy</option>
                                            <option value="nat" ${rule && rule.reflectionMode === 'nat' ? 'selected' : ''}>NAT</option>
                                            <option value="disabled" ${rule && rule.reflectionMode === 'disabled' ? 'selected' : ''}>Disabled</option>
                                        </select>
                                    </div>
                                    <div class="col-md-4 mb-3">
                                        <label for="pfSchedule" class="form-label">Schedule</label>
                                        <select class="form-select" id="pfSchedule">
                                            ${scheduleOptions}
                                        </select>
                                    </div>
                                </div>
                                <div class="row">
                                    <div class="col-md-4 mb-3">
                                        <label for="pfSourceType" class="form-label">Source Type</label>
                                        <select class="form-select" id="pfSourceType">
                                            <option value="any" ${rule && rule.sourceType === 'any' ? 'selected' : 'selected'}>Any</option>
                                            <option value="single" ${rule && rule.sourceType === 'single' ? 'selected' : ''}>Single IP</option>
                                            <option value="network" ${rule && rule.sourceType === 'network' ? 'selected' : ''}>Network</option>
                                            <option value="alias" ${rule && rule.sourceType === 'alias' ? 'selected' : ''}>Alias</option>
                                        </select>
                                    </div>
                                    <div class="col-md-4 mb-3">
                                        <label for="pfSourceValue" class="form-label">Source IP/Network</label>
                                        <input type="text" class="form-control" id="pfSourceValue"
                                               value="${rule ? (rule.sourceValue || '') : ''}"
                                               list="pfAliasList"
                                               placeholder="IP, network, or alias">
                                    </div>
                                    <div class="col-md-4 mb-3">
                                        <label for="pfSourcePort" class="form-label">Source Port</label>
                                        <input type="text" class="form-control" id="pfSourcePort"
                                               value="${rule ? (rule.sourcePort || '') : ''}"
                                               placeholder="Optional source port">
                                    </div>
                                </div>
                                <div class="row">
                                    <div class="col-md-4 mb-3">
                                        <label for="pfDestinationType" class="form-label">Destination Type</label>
                                        <select class="form-select" id="pfDestinationType">
                                            <option value="any" ${rule && rule.destinationType === 'any' ? 'selected' : ''}>Any</option>
                                            <option value="single" ${rule && rule.destinationType === 'single' ? 'selected' : 'selected'}>Single IP</option>
                                            <option value="network" ${rule && rule.destinationType === 'network' ? 'selected' : ''}>Network</option>
                                            <option value="alias" ${rule && rule.destinationType === 'alias' ? 'selected' : ''}>Alias</option>
                                        </select>
                                    </div>
                                    <div class="col-md-4 mb-3">
                                        <label for="pfDestinationValue" class="form-label">Destination IP <span class="text-danger">*</span></label>
                                        <input type="text" class="form-control" id="pfDestinationValue" required
                                               value="${rule ? (rule.destinationValue || '') : ''}"
                                               list="pfAliasList"
                                               placeholder="External IP address">
                                    </div>
                                    <div class="col-md-4 mb-3">
                                        <label for="pfDestinationPort" class="form-label">Destination Port <span class="text-danger">*</span></label>
                                        <input type="text" class="form-control" id="pfDestinationPort" required
                                               value="${rule ? (rule.destinationPort || '') : ''}"
                                               placeholder="e.g., 80, 443">
                                    </div>
                                </div>
                                <div class="row">
                                    <div class="col-md-6 mb-3">
                                        <label for="pfRedirectTargetIp" class="form-label">Redirect Target IP (Internal) <span class="text-danger">*</span></label>
                                        <input type="text" class="form-control" id="pfRedirectTargetIp" required
                                               value="${rule ? (rule.redirectTargetIp || '') : ''}"
                                               placeholder="Internal IP address">
                                    </div>
                                    <div class="col-md-6 mb-3">
                                        <label for="pfRedirectTargetPort" class="form-label">Redirect Target Port <span class="text-danger">*</span></label>
                                        <input type="text" class="form-control" id="pfRedirectTargetPort" required
                                               value="${rule ? (rule.redirectTargetPort || '') : ''}"
                                               placeholder="Internal port (or same as destination)">
                                    </div>
                                </div>
                                <div class="mb-3">
                                    <label for="pfDescription" class="form-label">Description</label>
                                    <input type="text" class="form-control" id="pfDescription"
                                           value="${rule ? (rule.description || '') : ''}"
                                           placeholder="Optional description">
                                </div>
                                <div class="mb-3 form-check">
                                    <input type="checkbox" class="form-check-input" id="pfEnabled"
                                           ${rule && rule.enabled !== false ? 'checked' : ''}>
                                    <label class="form-check-label" for="pfEnabled">
                                        Enabled
                                    </label>
                                </div>
                                <datalist id="pfAliasList">
                                    ${aliasOptions}
                                </datalist>
                            </form>
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
                            <button type="button" class="btn btn-primary" data-action="save-port-forward">${isEdit ? 'Update' : 'Create'}</button>
                        </div>
                    </div>
                </div>
            </div>
        `;

        $('#portForwardModal').remove();
        $('body').append(modalHtml);
        const modal = new bootstrap.Modal(document.getElementById('portForwardModal'));
        modal.show();

        // Show IPv6 warning if IPv6 selected
        const updateIpv6Warning = () => {
            const addressFamily = $('#pfAddressFamily').val();
            if (addressFamily === 'ipv6' || addressFamily === 'dual') {
                $('#pfIpv6Warning').show();
            } else {
                $('#pfIpv6Warning').hide();
            }
        };
        updateIpv6Warning();
        $('#pfAddressFamily').on('change', updateIpv6Warning);

        $(document).off('click', '[data-action="save-port-forward"]');
        $(document).on('click', '[data-action="save-port-forward"]', () => {
            this.savePortForwardRule(rule ? rule.id : null);
        });

        $('#portForwardModal').on('hidden.bs.modal', function() {
            $(this).remove();
        });
    },

    showOneToOneModal: function(rule) {
        const isEdit = rule !== null;
        const aliasOptions = this.aliases.map(a => `<option value="${a.name}">${a.name}</option>`).join('');
        
        const interfaceOptions = this.interfaces.map(i => 
            `<option value="${i.interface}" ${rule && rule.interface === i.interface ? 'selected' : ''}>${i.name} (${i.interface})</option>`
        ).join('');

        const modalHtml = `
            <div class="modal fade" id="oneToOneModal" tabindex="-1" aria-labelledby="oneToOneModalLabel" aria-hidden="true">
                <div class="modal-dialog modal-lg">
                    <div class="modal-content">
                        <div class="modal-header">
                            <h5 class="modal-title" id="oneToOneModalLabel">${isEdit ? 'Edit' : 'Add'} 1:1 NAT Rule</h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                        </div>
                        <div class="modal-body">
                            <form id="oneToOneForm">
                                <div class="alert alert-warning" id="otoIpv6Warning" style="display: none;">
                                    <strong>Note:</strong> IPv6 does not support 1:1 NAT. Use firewall rules instead.
                                </div>
                                <div class="row">
                                    <div class="col-md-6 mb-3">
                                        <label for="otoInterface" class="form-label">Interface <span class="text-danger">*</span></label>
                                        <select class="form-select" id="otoInterface" required>
                                            <option value="" disabled ${!rule ? 'selected' : ''}>Select Interface...</option>
                                            ${interfaceOptions}
                                        </select>
                                    </div>
                                    <div class="col-md-6 mb-3">
                                        <label for="otoAddressFamily" class="form-label">Address Family</label>
                                        <select class="form-select" id="otoAddressFamily">
                                            <option value="ipv4" ${rule && rule.addressFamily === 'ipv4' ? 'selected' : 'selected'}>IPv4</option>
                                            <option value="ipv6" ${rule && rule.addressFamily === 'ipv6' ? 'selected' : ''}>IPv6</option>
                                            <option value="dual" ${rule && rule.addressFamily === 'dual' ? 'selected' : ''}>IPv4 + IPv6</option>
                                        </select>
                                    </div>
                                </div>
                                <div class="row">
                                    <div class="col-md-6 mb-3">
                                        <label for="otoSourceValue" class="form-label">Source IP (External) <span class="text-danger">*</span></label>
                                        <input type="text" class="form-control" id="otoSourceValue" required
                                               value="${rule ? (rule.sourceValue || '') : ''}"
                                               placeholder="External IP address">
                                        <small class="form-text text-muted">The external IP address that will be mapped</small>
                                    </div>
                                    <div class="col-md-6 mb-3">
                                        <label for="otoDestinationValue" class="form-label">Destination IP (Internal) <span class="text-danger">*</span></label>
                                        <input type="text" class="form-control" id="otoDestinationValue" required
                                               value="${rule ? (rule.destinationValue || '') : ''}"
                                               placeholder="Internal IP address">
                                        <small class="form-text text-muted">The internal IP address to map to</small>
                                    </div>
                                </div>
                                <div class="row">
                                    <div class="col-md-6 mb-3">
                                        <label for="otoRedirectTargetIp" class="form-label">Redirect Target IP <span class="text-danger">*</span></label>
                                        <input type="text" class="form-control" id="otoRedirectTargetIp" required
                                               value="${rule ? (rule.redirectTargetIp || '') : ''}"
                                               placeholder="Target IP address">
                                        <small class="form-text text-muted">The IP address where traffic will be redirected</small>
                                    </div>
                                    <div class="col-md-6 mb-3">
                                        <label for="otoReflectionMode" class="form-label">Reflection Mode</label>
                                        <select class="form-select" id="otoReflectionMode">
                                            <option value="default" ${rule && rule.reflectionMode === 'default' ? 'selected' : 'selected'}>Default</option>
                                            <option value="proxy" ${rule && rule.reflectionMode === 'proxy' ? 'selected' : ''}>Proxy</option>
                                            <option value="nat" ${rule && rule.reflectionMode === 'nat' ? 'selected' : ''}>NAT</option>
                                            <option value="disabled" ${rule && rule.reflectionMode === 'disabled' ? 'selected' : ''}>Disabled</option>
                                        </select>
                                    </div>
                                </div>
                                <div class="mb-3">
                                    <label for="otoDescription" class="form-label">Description</label>
                                    <input type="text" class="form-control" id="otoDescription"
                                           value="${rule ? (rule.description || '') : ''}"
                                           placeholder="Optional description">
                                </div>
                                <div class="mb-3 form-check">
                                    <input type="checkbox" class="form-check-input" id="otoEnabled"
                                           ${rule && rule.enabled !== false ? 'checked' : ''}>
                                    <label class="form-check-label" for="otoEnabled">
                                        Enabled
                                    </label>
                                </div>
                            </form>
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
                            <button type="button" class="btn btn-primary" data-action="save-one-to-one">${isEdit ? 'Update' : 'Create'}</button>
                        </div>
                    </div>
                </div>
            </div>
        `;

        $('#oneToOneModal').remove();
        $('body').append(modalHtml);
        const modal = new bootstrap.Modal(document.getElementById('oneToOneModal'));
        modal.show();

        // Show IPv6 warning if IPv6 selected
        const updateIpv6Warning = () => {
            const addressFamily = $('#otoAddressFamily').val();
            if (addressFamily === 'ipv6' || addressFamily === 'dual') {
                $('#otoIpv6Warning').show();
            } else {
                $('#otoIpv6Warning').hide();
            }
        };
        updateIpv6Warning();
        $('#otoAddressFamily').on('change', updateIpv6Warning);

        $(document).off('click', '[data-action="save-one-to-one"]');
        $(document).on('click', '[data-action="save-one-to-one"]', () => {
            this.saveOneToOneRule(rule ? rule.id : null);
        });

        $('#oneToOneModal').on('hidden.bs.modal', function() {
            $(this).remove();
        });
    },

    showOutboundModal: function(rule) {
        const isEdit = rule !== null;
        const aliasOptions = this.aliases.map(a => `<option value="${a.name}">${a.name}</option>`).join('');
        
        const interfaceOptions = this.interfaces.map(i => 
            `<option value="${i.interface}" ${rule && rule.interface === i.interface ? 'selected' : ''}>${i.name} (${i.interface})</option>`
        ).join('');

        const scheduleOptions = [
            '<option value="">None (Always Active)</option>',
            ...this.schedules.map(s => `<option value="${s.id}" ${rule && rule.scheduleId === s.id ? 'selected' : ''}>${s.name}</option>`)
        ].join('');

        const modalHtml = `
            <div class="modal fade" id="outboundModal" tabindex="-1" aria-labelledby="outboundModalLabel" aria-hidden="true">
                <div class="modal-dialog modal-xl">
                    <div class="modal-content">
                        <div class="modal-header">
                            <h5 class="modal-title" id="outboundModalLabel">${isEdit ? 'Edit' : 'Add'} Outbound NAT Rule</h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                        </div>
                        <div class="modal-body">
                            <form id="outboundForm">
                                <div class="row">
                                    <div class="col-md-6 mb-3">
                                        <label for="obInterface" class="form-label">Interface <span class="text-danger">*</span></label>
                                        <select class="form-select" id="obInterface" required>
                                            <option value="" disabled ${!rule ? 'selected' : ''}>Select Interface...</option>
                                            ${interfaceOptions}
                                        </select>
                                        <small class="form-text text-muted">Source interface for outbound traffic</small>
                                    </div>
                                    <div class="col-md-6 mb-3">
                                        <label for="obAddressFamily" class="form-label">Address Family</label>
                                        <select class="form-select" id="obAddressFamily">
                                            <option value="ipv4" ${rule && rule.addressFamily === 'ipv4' ? 'selected' : 'selected'}>IPv4</option>
                                            <option value="ipv6" ${rule && rule.addressFamily === 'ipv6' ? 'selected' : ''}>IPv6</option>
                                            <option value="dual" ${rule && rule.addressFamily === 'dual' ? 'selected' : ''}>IPv4 + IPv6</option>
                                        </select>
                                    </div>
                                </div>
                                <div class="row">
                                    <div class="col-md-4 mb-3">
                                        <label for="obProtocol" class="form-label">Protocol</label>
                                        <select class="form-select" id="obProtocol">
                                            <option value="tcp" ${rule && rule.protocol === 'tcp' ? 'selected' : ''}>TCP</option>
                                            <option value="udp" ${rule && rule.protocol === 'udp' ? 'selected' : ''}>UDP</option>
                                            <option value="tcp/udp" ${rule && rule.protocol === 'tcp/udp' ? 'selected' : ''}>TCP/UDP</option>
                                            <option value="icmp" ${rule && rule.protocol === 'icmp' ? 'selected' : ''}>ICMP</option>
                                            <option value="any" ${rule && rule.protocol === 'any' ? 'selected' : 'selected'}>Any</option>
                                        </select>
                                    </div>
                                    <div class="col-md-4 mb-3">
                                        <label for="obReflectionMode" class="form-label">Reflection Mode</label>
                                        <select class="form-select" id="obReflectionMode">
                                            <option value="default" ${rule && rule.reflectionMode === 'default' ? 'selected' : 'selected'}>Default</option>
                                            <option value="proxy" ${rule && rule.reflectionMode === 'proxy' ? 'selected' : ''}>Proxy</option>
                                            <option value="nat" ${rule && rule.reflectionMode === 'nat' ? 'selected' : ''}>NAT</option>
                                            <option value="disabled" ${rule && rule.reflectionMode === 'disabled' ? 'selected' : ''}>Disabled</option>
                                        </select>
                                    </div>
                                    <div class="col-md-4 mb-3">
                                        <label for="obSchedule" class="form-label">Schedule</label>
                                        <select class="form-select" id="obSchedule">
                                            ${scheduleOptions}
                                        </select>
                                    </div>
                                </div>
                                <div class="row">
                                    <div class="col-md-4 mb-3">
                                        <label for="obSourceType" class="form-label">Source Type</label>
                                        <select class="form-select" id="obSourceType">
                                            <option value="any" ${rule && rule.sourceType === 'any' ? 'selected' : 'selected'}>Any</option>
                                            <option value="single" ${rule && rule.sourceType === 'single' ? 'selected' : ''}>Single IP</option>
                                            <option value="network" ${rule && rule.sourceType === 'network' ? 'selected' : ''}>Network</option>
                                            <option value="alias" ${rule && rule.sourceType === 'alias' ? 'selected' : ''}>Alias</option>
                                        </select>
                                    </div>
                                    <div class="col-md-4 mb-3">
                                        <label for="obSourceValue" class="form-label">Source IP/Network</label>
                                        <input type="text" class="form-control" id="obSourceValue"
                                               value="${rule ? (rule.sourceValue || '') : ''}"
                                               list="obAliasList"
                                               placeholder="IP, network, or alias">
                                    </div>
                                    <div class="col-md-4 mb-3">
                                        <label for="obSourcePort" class="form-label">Source Port</label>
                                        <input type="text" class="form-control" id="obSourcePort"
                                               value="${rule ? (rule.sourcePort || '') : ''}"
                                               placeholder="Optional source port">
                                    </div>
                                </div>
                                <div class="row">
                                    <div class="col-md-4 mb-3">
                                        <label for="obDestinationType" class="form-label">Destination Type</label>
                                        <select class="form-select" id="obDestinationType">
                                            <option value="any" ${rule && rule.destinationType === 'any' ? 'selected' : 'selected'}>Any</option>
                                            <option value="single" ${rule && rule.destinationType === 'single' ? 'selected' : ''}>Single IP</option>
                                            <option value="network" ${rule && rule.destinationType === 'network' ? 'selected' : ''}>Network</option>
                                            <option value="alias" ${rule && rule.destinationType === 'alias' ? 'selected' : ''}>Alias</option>
                                        </select>
                                    </div>
                                    <div class="col-md-4 mb-3">
                                        <label for="obDestinationValue" class="form-label">Destination IP/Network</label>
                                        <input type="text" class="form-control" id="obDestinationValue"
                                               value="${rule ? (rule.destinationValue || '') : ''}"
                                               list="obAliasList"
                                               placeholder="IP, network, or alias">
                                    </div>
                                    <div class="col-md-4 mb-3">
                                        <label for="obDestinationPort" class="form-label">Destination Port</label>
                                        <input type="text" class="form-control" id="obDestinationPort"
                                               value="${rule ? (rule.destinationPort || '') : ''}"
                                               placeholder="Optional destination port">
                                    </div>
                                </div>
                                <div class="row">
                                    <div class="col-md-6 mb-3">
                                        <label for="obRedirectTargetIp" class="form-label">NAT Target IP (SNAT) <span class="text-danger">*</span></label>
                                        <input type="text" class="form-control" id="obRedirectTargetIp" required
                                               value="${rule ? (rule.redirectTargetIp || '') : ''}"
                                               placeholder="Interface IP or specific IP address">
                                        <small class="form-text text-muted">The IP address to use as the source (SNAT target)</small>
                                    </div>
                                    <div class="col-md-6 mb-3">
                                        <label for="obRedirectTargetPort" class="form-label">NAT Target Port</label>
                                        <input type="text" class="form-control" id="obRedirectTargetPort"
                                               value="${rule ? (rule.redirectTargetPort || '') : ''}"
                                               placeholder="Optional port translation">
                                        <small class="form-text text-muted">Optional: Translate source port</small>
                                    </div>
                                </div>
                                <div class="mb-3">
                                    <label for="obDescription" class="form-label">Description</label>
                                    <input type="text" class="form-control" id="obDescription"
                                           value="${rule ? (rule.description || '') : ''}"
                                           placeholder="Optional description">
                                </div>
                                <div class="mb-3 form-check">
                                    <input type="checkbox" class="form-check-input" id="obEnabled"
                                           ${rule && rule.enabled !== false ? 'checked' : ''}>
                                    <label class="form-check-label" for="obEnabled">
                                        Enabled
                                    </label>
                                </div>
                                <datalist id="obAliasList">
                                    ${aliasOptions}
                                </datalist>
                            </form>
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
                            <button type="button" class="btn btn-primary" data-action="save-outbound">${isEdit ? 'Update' : 'Create'}</button>
                        </div>
                    </div>
                </div>
            </div>
        `;

        $('#outboundModal').remove();
        $('body').append(modalHtml);
        const modal = new bootstrap.Modal(document.getElementById('outboundModal'));
        modal.show();

        $(document).off('click', '[data-action="save-outbound"]');
        $(document).on('click', '[data-action="save-outbound"]', () => {
            this.saveOutboundRule(rule ? rule.id : null);
        });

        $('#outboundModal').on('hidden.bs.modal', function() {
            $(this).remove();
        });
    },

    editRule: async function(id) {
        try {
            const response = await Monolith.API.get(`/firewall/nat/${id}`);
            if (response.Success || response.success) {
                const rule = this.normalizeRule(response.Data || response.data);
                // Call type-specific modal based on rule type
                if (rule.type === 'port_forward') {
                    this.showPortForwardModal(rule);
                } else if (rule.type === 'one_to_one') {
                    this.showOneToOneModal(rule);
                } else if (rule.type === 'outbound') {
                    this.showOutboundModal(rule);
                } else {
                    this.showMessage('Unknown NAT rule type', 'danger');
                }
            } else {
                this.showMessage('Failed to load NAT rule', 'danger');
            }
        } catch (error) {
            console.error('Error loading NAT rule:', error);
            this.showMessage('Failed to load NAT rule', 'danger');
        }
    },

    savePortForwardRule: async function(id) {
        const form = document.getElementById('portForwardForm');
        if (!form || !form.checkValidity()) {
            if (form) form.reportValidity();
            return;
        }

        const addressFamily = $('#pfAddressFamily').val();
        if (addressFamily === 'ipv6' || addressFamily === 'dual') {
            Monolith.UI.toast('IPv6 does not support port forwarding. Use firewall rules instead.', 'warning');
            return;
        }

        const scheduleId = $('#pfSchedule').val();
        const rule = {
            type: 'port_forward', // Hard-coded
            interface: $('#pfInterface').val(),
            addressFamily: addressFamily,
            protocol: $('#pfProtocol').val(),
            sourceType: $('#pfSourceType').val(),
            sourceValue: $('#pfSourceValue').val().trim() || null,
            sourcePort: $('#pfSourcePort').val().trim() || null,
            destinationType: $('#pfDestinationType').val(),
            destinationValue: $('#pfDestinationValue').val().trim(),
            destinationPort: $('#pfDestinationPort').val().trim(),
            redirectTargetIp: $('#pfRedirectTargetIp').val().trim(),
            redirectTargetPort: $('#pfRedirectTargetPort').val().trim(),
            reflectionMode: $('#pfReflectionMode').val(),
            description: $('#pfDescription').val().trim(),
            enabled: $('#pfEnabled').is(':checked'),
            scheduleId: scheduleId ? parseInt(scheduleId) : null
        };

        try {
            let response;
            if (id) {
                response = await Monolith.API.put(`/firewall/nat/${id}`, rule);
            } else {
                response = await Monolith.API.post('/firewall/nat', rule);
            }

            if (response.Success || response.success) {
                bootstrap.Modal.getInstance(document.getElementById('portForwardModal')).hide();
                this.showMessage(id ? 'Port forwarding rule updated successfully' : 'Port forwarding rule created successfully', 'success');
                this.loadRules();
                this.markPendingChanges();
            } else {
                this.showMessage(response.error || response.Error || 'Failed to save port forwarding rule', 'danger');
            }
        } catch (error) {
            console.error('Error saving port forwarding rule:', error);
            this.showMessage('Failed to save port forwarding rule', 'danger');
        }
    },

    saveOneToOneRule: async function(id) {
        const form = document.getElementById('oneToOneForm');
        if (!form || !form.checkValidity()) {
            if (form) form.reportValidity();
            return;
        }

        const addressFamily = $('#otoAddressFamily').val();
        if (addressFamily === 'ipv6' || addressFamily === 'dual') {
            Monolith.UI.toast('IPv6 does not support 1:1 NAT. Use firewall rules instead.', 'warning');
            return;
        }

        const rule = {
            type: 'one_to_one', // Hard-coded
            interface: $('#otoInterface').val(),
            addressFamily: addressFamily,
            protocol: 'any', // Default for 1:1 NAT
            sourceType: 'single', // Always single IP for 1:1
            sourceValue: $('#otoSourceValue').val().trim(),
            sourcePort: null, // No ports for 1:1
            destinationType: 'single', // Always single IP for 1:1
            destinationValue: $('#otoDestinationValue').val().trim(),
            destinationPort: null, // No ports for 1:1
            redirectTargetIp: $('#otoRedirectTargetIp').val().trim(),
            redirectTargetPort: null, // No ports for 1:1
            reflectionMode: $('#otoReflectionMode').val(),
            description: $('#otoDescription').val().trim(),
            enabled: $('#otoEnabled').is(':checked'),
            scheduleId: null // Schedules not typically used for 1:1 NAT
        };

        try {
            let response;
            if (id) {
                response = await Monolith.API.put(`/firewall/nat/${id}`, rule);
            } else {
                response = await Monolith.API.post('/firewall/nat', rule);
            }

            if (response.Success || response.success) {
                bootstrap.Modal.getInstance(document.getElementById('oneToOneModal')).hide();
                this.showMessage(id ? '1:1 NAT rule updated successfully' : '1:1 NAT rule created successfully', 'success');
                this.loadRules();
                this.markPendingChanges();
            } else {
                this.showMessage(response.error || response.Error || 'Failed to save 1:1 NAT rule', 'danger');
            }
        } catch (error) {
            console.error('Error saving 1:1 NAT rule:', error);
            this.showMessage('Failed to save 1:1 NAT rule', 'danger');
        }
    },

    saveOutboundRule: async function(id) {
        const form = document.getElementById('outboundForm');
        if (!form || !form.checkValidity()) {
            if (form) form.reportValidity();
            return;
        }

        const scheduleId = $('#obSchedule').val();
        const rule = {
            type: 'outbound', // Hard-coded
            interface: $('#obInterface').val(),
            addressFamily: $('#obAddressFamily').val(),
            protocol: $('#obProtocol').val(),
            sourceType: $('#obSourceType').val(),
            sourceValue: $('#obSourceValue').val().trim() || null,
            sourcePort: $('#obSourcePort').val().trim() || null,
            destinationType: $('#obDestinationType').val(),
            destinationValue: $('#obDestinationValue').val().trim() || null,
            destinationPort: $('#obDestinationPort').val().trim() || null,
            redirectTargetIp: $('#obRedirectTargetIp').val().trim(),
            redirectTargetPort: $('#obRedirectTargetPort').val().trim() || null,
            reflectionMode: $('#obReflectionMode').val(),
            description: $('#obDescription').val().trim(),
            enabled: $('#obEnabled').is(':checked'),
            scheduleId: scheduleId ? parseInt(scheduleId) : null
        };

        try {
            let response;
            if (id) {
                response = await Monolith.API.put(`/firewall/nat/${id}`, rule);
            } else {
                response = await Monolith.API.post('/firewall/nat', rule);
            }

            if (response.Success || response.success) {
                bootstrap.Modal.getInstance(document.getElementById('outboundModal')).hide();
                this.showMessage(id ? 'Outbound NAT rule updated successfully' : 'Outbound NAT rule created successfully', 'success');
                this.loadRules();
                this.markPendingChanges();
            } else {
                this.showMessage(response.error || response.Error || 'Failed to save outbound NAT rule', 'danger');
            }
        } catch (error) {
            console.error('Error saving outbound NAT rule:', error);
            this.showMessage('Failed to save outbound NAT rule', 'danger');
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

};

// Register with Monolith.Pages.Firewall
if (typeof Monolith !== 'undefined') {
    Monolith.Pages = Monolith.Pages || {};
    Monolith.Pages.Firewall = Monolith.Pages.Firewall || {};
    Monolith.Pages.Firewall.Nat = Nat;
    // Also register at root level for backward compatibility
    Monolith.Pages.Nat = Nat;
}
