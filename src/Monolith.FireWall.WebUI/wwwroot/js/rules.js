var Rules = {
    interfaces: [],
    rules: [],
    defaults: null,
    interfaceSettings: [],
    aliases: [],
    addressAliases: [],
    portAliases: [],
    schedules: [],
    systemSets: [
        { value: 'rfc1918', label: 'RFC1918 (IPv4)' },
        { value: 'iana_reserved', label: 'IANA Reserved (IPv4)' },
        { value: 'rfc4193', label: 'RFC4193 ULA (IPv6)' },
        { value: 'iana_reserved_v6', label: 'IANA Reserved (IPv6)' }
    ],

    init: function() {
        console.log('Initializing Firewall Rules page...');
    },

    renderPage: function() {
        console.log('Rendering Firewall Rules page...');
        this.attachEventHandlers();
        this.loadData();
    },

    loadData: async function() {
        await Promise.all([
            this.loadInterfaces(),
            this.loadDefaults(),
            this.loadInterfaceSettings(),
            this.loadSchedules(),
            this.loadRules(),
            this.loadAliases()
        ]);
        this.renderTabs();
    },

    loadInterfaces: async function() {
        try {
            const response = await Monolith.API.get('/interfaces/assignments');
            if (response.Success || response.success) {
                const data = response.Data || response.data || {};
                const assigned = data.Assigned || data.assigned || data || [];
                const items = Array.isArray(assigned) ? assigned : [];
                this.interfaces = items.map(i => this.normalizeInterface(i));
            } else {
                this.interfaces = [];
            }
        } catch (error) {
            console.error('Failed to load interfaces:', error);
            this.interfaces = [];
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

    loadDefaults: async function() {
        try {
            const response = await Monolith.API.get('/firewall/defaults');
            if (response.Success || response.success) {
                this.defaults = response.Data || response.data || null;
            }
        } catch (error) {
            console.warn('Failed to load firewall defaults:', error);
            this.defaults = null;
        }
    },

    loadInterfaceSettings: async function() {
        try {
            const response = await Monolith.API.get('/firewall/interface-settings');
            if (response.Success || response.success) {
                const data = response.Data || response.data || [];
                this.interfaceSettings = Array.isArray(data) ? data : [];
            } else {
                this.interfaceSettings = [];
            }
        } catch (error) {
            console.warn('Failed to load interface settings:', error);
            this.interfaceSettings = [];
        }
    },

    loadRules: async function() {
        try {
            const response = await Monolith.API.get('/firewall/rules');
            if (response.Success || response.success) {
                const data = response.Data || response.data || [];
                this.rules = Array.isArray(data) ? data.map(r => this.normalizeRule(r)) : [];
            } else {
                this.rules = [];
            }
        } catch (error) {
            console.error('Failed to load firewall rules:', error);
            this.rules = [];
        }
    },

    loadAliases: async function() {
        try {
            const response = await Monolith.API.get('/firewall/aliases');
            if (response.Success || response.success) {
                const data = response.Data || response.data || {};
                const items = data.items || data || [];
                const aliasArray = Array.isArray(items) ? items : [];
                this.aliases = aliasArray.map(a => this.normalizeAlias(a));
            } else {
                this.aliases = [];
            }
        } catch (error) {
            console.warn('Failed to load aliases:', error);
            this.aliases = [];
        }

        this.addressAliases = this.aliases
            .filter(a => a.type === 'host' || a.type === 'network')
            .map(a => a.name)
            .sort((a, b) => a.localeCompare(b));

        this.portAliases = this.aliases
            .filter(a => a.type === 'port')
            .map(a => a.name)
            .sort((a, b) => a.localeCompare(b));
    },

    normalizeInterface: function(item) {
        return {
            interface: item.Interface || item.interface,
            name: item.Name || item.name,
            role: item.Role !== undefined ? item.Role : (item.role !== undefined ? item.role : 0),
            isManagement: item.IsManagement !== undefined ? item.IsManagement : (item.isManagement !== undefined ? item.isManagement : false)
        };
    },

    normalizeRule: function(rule) {
        return {
            id: rule.Id || rule.id,
            ruleNumber: rule.RuleNumber || rule.ruleNumber,
            interface: rule.Interface || rule.interface,
            direction: rule.Direction || rule.direction,
            action: rule.Action || rule.action,
            addressFamily: rule.AddressFamily || rule.addressFamily || 'ipv4',
            protocol: rule.Protocol || rule.protocol,
            sourceType: rule.SourceType || rule.sourceType,
            sourceValue: rule.SourceValue || rule.sourceValue,
            sourcePort: rule.SourcePort || rule.sourcePort,
            destinationType: rule.DestinationType || rule.destinationType,
            destinationValue: rule.DestinationValue || rule.destinationValue,
            destinationPort: rule.DestinationPort || rule.destinationPort,
            gateway: rule.Gateway || rule.gateway,
            logEnabled: rule.LogEnabled !== undefined ? rule.LogEnabled : (rule.logEnabled !== undefined ? rule.logEnabled : false),
            enabled: rule.Enabled !== undefined ? rule.Enabled : (rule.enabled !== undefined ? rule.enabled : true),
            description: rule.Description || rule.description,
            isSystem: rule.IsSystem !== undefined ? rule.IsSystem : (rule.isSystem !== undefined ? rule.isSystem : false),
            systemTag: rule.SystemTag || rule.systemTag || null,
            isManaged: rule.IsManaged !== undefined ? rule.IsManaged : (rule.isManaged !== undefined ? rule.isManaged : false),
            managedBy: rule.ManagedBy || rule.managedBy || null
        };
    },

    normalizeAlias: function(alias) {
        return {
            id: alias.Id || alias.id,
            name: alias.Name || alias.name,
            type: (alias.Type || alias.type || '').toLowerCase()
        };
    },

    renderTabs: function() {
        const tabs = $('#rulesTabs');
        const content = $('#rulesTabContent');
        if (!tabs.length || !content.length) return;
        
        tabs.empty();
        content.empty();

        if (this.interfaces.length === 0) {
            tabs.append('<li class="nav-item"><span class="nav-link active">No Interfaces</span></li>');
            content.append('<div class="alert alert-info">Assign an interface to configure firewall rules.</div>');
            return;
        }

        this.interfaces.forEach((iface, index) => {
            const tabId = `rules-${iface.interface}`;
            const activeClass = index === 0 ? 'active' : '';
            const roleLabel = this.roleLabel(iface.role);
            
            // Get per-interface settings
            const settings = this.interfaceSettings.find(s => s.InterfaceName === iface.interface || s.interfaceName === iface.interface) || {};
            const defaultAction = settings.DefaultAction || settings.defaultAction || this.defaultActionLabel(iface.role);
            const blockReserved = settings.BlockReserved !== undefined ? settings.BlockReserved : (settings.blockReserved !== undefined ? settings.blockReserved : (iface.role === 2 && this.defaults?.BlockReservedOnWan));
            const blockBogon = settings.BlockBogon !== undefined ? settings.BlockBogon : (settings.blockBogon !== undefined ? settings.blockBogon : false);

            tabs.append(`
                <li class="nav-item" role="presentation">
                    <button class="nav-link ${activeClass}" id="${tabId}-tab" data-bs-toggle="tab" data-bs-target="#${tabId}" type="button" role="tab">
                        ${iface.name} <span class="badge bg-secondary ms-2">${roleLabel}</span>
                    </button>
                </li>
            `);

            content.append(`
                <div class="tab-pane fade ${activeClass ? 'show active' : ''}" id="${tabId}" role="tabpanel">
                    <div class="card mb-3">
                        <div class="card-body py-2">
                            <div class="row align-items-center">
                                <div class="col-auto">
                                    <label class="fw-bold me-2">Default Action:</label>
                                    <select class="form-select form-select-sm d-inline-block w-auto" 
                                            onchange="Rules.saveInterfaceSettings('${iface.interface}', this, 'action')">
                                        <option value="pass" ${defaultAction === 'pass' ? 'selected' : ''}>Pass</option>
                                        <option value="block" ${defaultAction === 'block' ? 'selected' : ''}>Block</option>
                                        <option value="reject" ${defaultAction === 'reject' ? 'selected' : ''}>Reject</option>
                                    </select>
                                </div>
                                <div class="col-auto">
                                    <div class="form-check form-switch d-inline-block align-middle mb-0">
                                        <input class="form-check-input" type="checkbox" id="blockReserved-${iface.interface}" 
                                               ${blockReserved ? 'checked' : ''}
                                               onchange="Rules.saveInterfaceSettings('${iface.interface}', this, 'reserved')">
                                        <label class="form-check-label" for="blockReserved-${iface.interface}">Block Reserved</label>
                                    </div>
                                </div>
                                <div class="col-auto">
                                    <div class="form-check form-switch d-inline-block align-middle mb-0">
                                        <input class="form-check-input" type="checkbox" id="blockBogon-${iface.interface}" 
                                               ${blockBogon ? 'checked' : ''}
                                               onchange="Rules.saveInterfaceSettings('${iface.interface}', this, 'bogon')">
                                        <label class="form-check-label" for="blockBogon-${iface.interface}">Block Bogon</label>
                                    </div>
                                </div>
                                <div class="col text-end">
                                    <button class="btn btn-primary btn-sm" data-interface="${iface.interface}" data-action="add-rule">Add Rule</button>
                                </div>
                            </div>
                        </div>
                    </div>
                    <div class="table-responsive">
                        <table class="table table-hover">
                            <thead>
                                <tr>
                                    <th style="width: 70px;">#</th>
                                    <th>Action</th>
                                    <th>Protocol</th>
                                    <th>Source</th>
                                    <th>Destination</th>
                                    <th>Description</th>
                                    <th>Status</th>
                                    <th style="width: 140px;">Order</th>
                                    <th style="width: 120px;">Actions</th>
                                </tr>
                            </thead>
                            <tbody id="${tabId}-body">
                                <tr><td colspan="9" class="text-center text-muted">Loading rules...</td></tr>
                            </tbody>
                        </table>
                    </div>
                </div>
            `);

            this.renderRulesForInterface(iface.interface, `${tabId}-body`);
        });
    },

    saveInterfaceSettings: async function(interfaceName, element, type) {
        const settings = this.interfaceSettings.find(s => s.InterfaceName === interfaceName || s.interfaceName === interfaceName) || {};
        
        let payload = {
            interfaceName: interfaceName,
            defaultAction: settings.DefaultAction || settings.defaultAction || 'block',
            blockReserved: settings.BlockReserved !== undefined ? settings.BlockReserved : (settings.blockReserved || false),
            blockBogon: settings.BlockBogon !== undefined ? settings.BlockBogon : (settings.blockBogon || false)
        };

        if (type === 'action') {
            payload.defaultAction = $(element).val();
        } else if (type === 'reserved') {
            payload.blockReserved = $(element).is(':checked');
        } else if (type === 'bogon') {
            payload.blockBogon = $(element).is(':checked');
        }

        try {
            const response = await Monolith.API.post('/firewall/interface-settings', payload);
            if (response.Success || response.success) {
                // Update local state
                const index = this.interfaceSettings.findIndex(s => s.InterfaceName === interfaceName || s.interfaceName === interfaceName);
                if (index !== -1) {
                    this.interfaceSettings[index] = payload;
                } else {
                    this.interfaceSettings.push(payload);
                }
                
                this.markPendingChanges();
                this.showMessage('Interface settings updated', 'success');
            } else {
                this.showMessage(response.Error || response.error || 'Failed to update settings', 'danger');
                // Revert UI if failed (simplified: just reload data)
                this.loadInterfaceSettings().then(() => this.renderTabs());
            }
        } catch (error) {
            console.error('Failed to update interface settings:', error);
            this.showMessage('Failed to update settings', 'danger');
        }
    },

    renderRulesForInterface: function(interfaceName, bodyId) {
        const tbody = $('#' + bodyId);
        if (!tbody.length) return;
        
        const rules = this.rules.filter(r => r.interface && r.interface.toLowerCase() === interfaceName.toLowerCase());

        if (rules.length === 0) {
            tbody.html('<tr><td colspan="9" class="text-center text-muted">No rules configured</td></tr>');
            return;
        }

        let html = '';
        rules.sort((a, b) => (a.isSystem === b.isSystem ? a.ruleNumber - b.ruleNumber : a.isSystem ? -1 : 1));
        rules.forEach((rule, idx) => {
            const actionBadge = this.actionBadge(rule.action);
            const statusBadge = rule.enabled
                ? '<span class="badge bg-success">Enabled</span>'
                : '<span class="badge bg-secondary">Disabled</span>';
            const systemBadge = rule.isSystem
                ? '<span class="badge bg-info ms-1">System</span>'
                : '';
            const managedBadge = rule.isManaged
                ? '<span class="badge bg-warning text-dark ms-1">Managed</span>'
                : '';
            const isLocked = rule.isSystem || rule.isManaged;
            const orderControls = isLocked
                ? '<span class="text-muted">-</span>'
                : `
                    <button class="btn btn-sm btn-outline-secondary me-1" data-action="move-up" data-id="${rule.id}" data-interface="${interfaceName}" ${idx === 0 ? 'disabled' : ''}>Up</button>
                    <button class="btn btn-sm btn-outline-secondary" data-action="move-down" data-id="${rule.id}" data-interface="${interfaceName}" ${idx === rules.length - 1 ? 'disabled' : ''}>Down</button>
                `;
            const actions = isLocked
                ? '<span class="text-muted">Locked</span>'
                : `
                    <button class="btn btn-sm btn-outline-primary me-1" data-action="edit-rule" data-id="${rule.id}">Edit</button>
                    <button class="btn btn-sm btn-outline-danger" data-action="delete-rule" data-id="${rule.id}">Delete</button>
                `;

            html += `
                <tr>
                    <td><strong>${rule.ruleNumber || ''}</strong>${systemBadge}${managedBadge}</td>
                    <td>${actionBadge}</td>
                    <td>${this.protocolLabel(rule.protocol)}</td>
                    <td>${this.formatEndpoint(rule.sourceType, rule.sourceValue, rule.sourcePort)}</td>
                    <td>${this.formatEndpoint(rule.destinationType, rule.destinationValue, rule.destinationPort)}</td>
                    <td>${rule.description || '-'}</td>
                    <td>${statusBadge}</td>
                    <td>${orderControls}</td>
                    <td>${actions}</td>
                </tr>
            `;
        });

        tbody.html(html);
    },

    attachEventHandlers: function() {
        $(document).off('click', '[data-action="add-rule"]');
        $(document).on('click', '[data-action="add-rule"]', (e) => {
            const iface = $(e.currentTarget).data('interface');
            this.showRuleModal(null, iface);
        });

        $(document).off('click', '[data-action="edit-rule"]');
        $(document).on('click', '[data-action="edit-rule"]', (e) => {
            const id = $(e.currentTarget).data('id');
            const rule = this.rules.find(r => r.id === id);
            if (rule) {
                this.showRuleModal(rule, rule.interface);
            }
        });

        $(document).off('click', '[data-action="delete-rule"]');
        $(document).on('click', '[data-action="delete-rule"]', (e) => {
            const id = $(e.currentTarget).data('id');
            this.deleteRule(id);
        });

        $(document).off('click', '[data-action="move-up"], [data-action="move-down"]');
        $(document).on('click', '[data-action="move-up"], [data-action="move-down"]', (e) => {
            const action = $(e.currentTarget).data('action');
            const id = $(e.currentTarget).data('id');
            const iface = $(e.currentTarget).data('interface');
            this.moveRule(iface, id, action === 'move-up' ? -1 : 1);
        });

        $(document).off('click', '#btnApplyChanges');
        $(document).on('click', '#btnApplyChanges', () => {
            this.applyChanges();
        });

        $(document).off('click', '#btnPreviewChanges');
        $(document).on('click', '#btnPreviewChanges', () => {
            this.previewChanges();
        });

        $(document).off('click', '#btnDiscardChanges');
        $(document).on('click', '#btnDiscardChanges', () => {
            this.discardChanges();
        });

        $(document).off('click', '#btnSaveDefaults');
        $(document).on('click', '#btnSaveDefaults', () => {
            this.saveDefaults();
        });
    },

    showRuleModal: function(rule, interfaceName) {
        const isEdit = rule !== null;
        const interfaceOptions = this.interfaces.map(i => {
            const selected = interfaceName && i.interface === interfaceName ? 'selected' : '';
            return `<option value="${i.interface}" ${selected}>${i.name}</option>`;
        }).join('');

        const scheduleOptions = [
            '<option value="">None (Always Active)</option>',
            ...this.schedules.map(s => `<option value="${s.id}" ${rule && rule.scheduleId === s.id ? 'selected' : ''}>${s.name}</option>`)
        ].join('');

        const modalHtml = `
            <div class="modal fade" id="ruleModal" tabindex="-1" aria-labelledby="ruleModalLabel" aria-hidden="true">
                <div class="modal-dialog modal-xl">
                    <div class="modal-content">
                        <div class="modal-header">
                            <h5 class="modal-title" id="ruleModalLabel">${isEdit ? 'Edit' : 'Add'} Firewall Rule</h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                        </div>
                        <div class="modal-body">
                            <form id="ruleForm">
                                <div class="row">
                                    <div class="col-md-3 mb-3">
                                        <label class="form-label">Interface</label>
                                        <select class="form-select" id="ruleInterface" required>
                                            ${interfaceOptions}
                                        </select>
                                    </div>
                                    <div class="col-md-3 mb-3">
                                        <label class="form-label">Direction</label>
                                        <select class="form-select" id="ruleDirection">
                                            <option value="in" ${rule && rule.direction === 'in' ? 'selected' : ''}>In</option>
                                            <option value="out" ${rule && rule.direction === 'out' ? 'selected' : ''}>Out</option>
                                            <option value="forward" ${rule && rule.direction === 'forward' ? 'selected' : ''}>Forward</option>
                                        </select>
                                    </div>
                                    <div class="col-md-3 mb-3">
                                        <label class="form-label">Action</label>
                                        <select class="form-select" id="ruleAction">
                                            <option value="pass" ${rule && rule.action === 'pass' ? 'selected' : ''}>Pass</option>
                                            <option value="block" ${rule && rule.action === 'block' ? 'selected' : ''}>Block</option>
                                            <option value="reject" ${rule && rule.action === 'reject' ? 'selected' : ''}>Reject</option>
                                        </select>
                                    </div>
                                    <div class="col-md-3 mb-3">
                                        <label class="form-label">Address Family</label>
                                        <select class="form-select" id="ruleFamily">
                                            <option value="ipv4" ${!rule || rule.addressFamily === 'ipv4' ? 'selected' : ''}>IPv4</option>
                                            <option value="ipv6" ${rule && rule.addressFamily === 'ipv6' ? 'selected' : ''}>IPv6</option>
                                            <option value="dual" ${rule && rule.addressFamily === 'dual' ? 'selected' : ''}>IPv4 + IPv6</option>
                                        </select>
                                    </div>
                                </div>
                                <div class="row">
                                    <div class="col-md-4 mb-3">
                                        <label class="form-label">Protocol</label>
                                        <select class="form-select" id="ruleProtocol">
                                            <option value="any" ${!rule || rule.protocol === 'any' ? 'selected' : ''}>Any</option>
                                            <option value="tcp" ${rule && rule.protocol === 'tcp' ? 'selected' : ''}>TCP</option>
                                            <option value="udp" ${rule && rule.protocol === 'udp' ? 'selected' : ''}>UDP</option>
                                            <option value="tcp/udp" ${rule && rule.protocol === 'tcp/udp' ? 'selected' : ''}>TCP/UDP</option>
                                            <option value="icmp" ${rule && rule.protocol === 'icmp' ? 'selected' : ''}>ICMP</option>
                                        </select>
                                    </div>
                                    <div class="col-md-4 mb-3">
                                        <label class="form-label">Log</label>
                                        <select class="form-select" id="ruleLog">
                                            <option value="false" ${!rule || !rule.logEnabled ? 'selected' : ''}>No</option>
                                            <option value="true" ${rule && rule.logEnabled ? 'selected' : ''}>Yes</option>
                                        </select>
                                    </div>
                                    <div class="col-md-4 mb-3">
                                        <label class="form-label">Schedule</label>
                                        <select class="form-select" id="ruleSchedule">
                                            ${scheduleOptions}
                                        </select>
                                    </div>
                                </div>

                                <div class="row">
                                    <div class="col-md-4 mb-3">
                                        <label class="form-label">Source Type</label>
                                        <select class="form-select" id="ruleSourceType">
                                            <option value="any" ${!rule || rule.sourceType === 'any' ? 'selected' : ''}>Any</option>
                                            <option value="single" ${rule && rule.sourceType === 'single' ? 'selected' : ''}>Single</option>
                                            <option value="network" ${rule && rule.sourceType === 'network' ? 'selected' : ''}>Network</option>
                                            <option value="alias" ${rule && rule.sourceType === 'alias' ? 'selected' : ''}>Alias</option>
                                            <option value="system" ${rule && rule.sourceType === 'system' ? 'selected' : ''}>System</option>
                                        </select>
                                    </div>
                                    <div class="col-md-4 mb-3">
                                        <label class="form-label">Source Value</label>
                                        <input type="text" class="form-control" id="ruleSourceValue" value="${rule ? (rule.sourceValue || '') : ''}" placeholder="IP, network, or alias">
                                        <div class="form-text" id="ruleSourceHint">Use addresses matching the selected family.</div>
                                    </div>
                                    <div class="col-md-4 mb-3">
                                        <label class="form-label">Source Port</label>
                                        <input type="text" class="form-control" id="ruleSourcePort" value="${rule ? (rule.sourcePort || '') : ''}" placeholder="Port or alias" list="rulesPortAliasList">
                                    </div>
                                </div>

                                <div class="row">
                                    <div class="col-md-4 mb-3">
                                        <label class="form-label">Destination Type</label>
                                        <select class="form-select" id="ruleDestinationType">
                                            <option value="any" ${!rule || rule.destinationType === 'any' ? 'selected' : ''}>Any</option>
                                            <option value="single" ${rule && rule.destinationType === 'single' ? 'selected' : ''}>Single</option>
                                            <option value="network" ${rule && rule.destinationType === 'network' ? 'selected' : ''}>Network</option>
                                            <option value="alias" ${rule && rule.destinationType === 'alias' ? 'selected' : ''}>Alias</option>
                                            <option value="system" ${rule && rule.destinationType === 'system' ? 'selected' : ''}>System</option>
                                        </select>
                                    </div>
                                    <div class="col-md-4 mb-3">
                                        <label class="form-label">Destination Value</label>
                                        <input type="text" class="form-control" id="ruleDestinationValue" value="${rule ? (rule.destinationValue || '') : ''}" placeholder="IP, network, or alias">
                                        <div class="form-text" id="ruleDestinationHint">Use addresses matching the selected family.</div>
                                    </div>
                                    <div class="col-md-4 mb-3">
                                        <label class="form-label">Destination Port</label>
                                        <input type="text" class="form-control" id="ruleDestinationPort" value="${rule ? (rule.destinationPort || '') : ''}" placeholder="Port or alias" list="rulesPortAliasList">
                                    </div>
                                </div>

                                <div class="mb-3">
                                    <label class="form-label">Description</label>
                                    <input type="text" class="form-control" id="ruleDescription" value="${rule ? (rule.description || '') : ''}" placeholder="Optional description">
                                </div>

                                <div class="form-check mb-3">
                                    <input class="form-check-input" type="checkbox" id="ruleEnabled" ${!rule || rule.enabled ? 'checked' : ''}>
                                    <label class="form-check-label" for="ruleEnabled">Enabled</label>
                                </div>
                            </form>
                            <datalist id="rulesAddressAliasList">
                                ${this.addressAliases.map(name => `<option value="${name}"></option>`).join('')}
                            </datalist>
                            <datalist id="rulesPortAliasList">
                                ${this.portAliases.map(name => `<option value="${name}"></option>`).join('')}
                            </datalist>
                            <datalist id="rulesSystemSetList">
                                ${this.systemSetOptions($('#ruleFamily').val() || (rule ? rule.addressFamily : 'ipv4')).map(option => `<option value="${option.value}">${option.label}</option>`).join('')}
                            </datalist>
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
                            <button type="button" class="btn btn-primary" data-action="save-rule" data-id="${rule ? rule.id : ''}">${isEdit ? 'Update' : 'Create'}</button>
                        </div>
                    </div>
                </div>
            </div>
        `;

        $('#ruleModal').remove();
        $('body').append(modalHtml);
        const modal = new bootstrap.Modal(document.getElementById('ruleModal'));
        modal.show();

        $(document).off('click', '[data-action="save-rule"]');
        $(document).on('click', '[data-action="save-rule"]', () => {
            const id = rule ? rule.id : null;
            this.saveRule(id);
        });

        this.bindRuleModalInputs();

        $('#ruleModal').on('hidden.bs.modal', function() {
            $(this).remove();
        });
    },

    saveRule: async function(id) {
        const form = document.getElementById('ruleForm');
        if (!form || !form.checkValidity()) {
            if (form) form.reportValidity();
            return;
        }

        const scheduleId = $('#ruleSchedule').val();
        const rule = {
            interface: $('#ruleInterface').val(),
            direction: $('#ruleDirection').val(),
            action: $('#ruleAction').val(),
            addressFamily: $('#ruleFamily').val(),
            protocol: $('#ruleProtocol').val(),
            sourceType: $('#ruleSourceType').val(),
            sourceValue: $('#ruleSourceValue').val().trim() || null,
            sourcePort: $('#ruleSourcePort').val().trim() || null,
            destinationType: $('#ruleDestinationType').val(),
            destinationValue: $('#ruleDestinationValue').val().trim() || null,
            destinationPort: $('#ruleDestinationPort').val().trim() || null,
            logEnabled: $('#ruleLog').val() === 'true',
            enabled: $('#ruleEnabled').is(':checked'),
            description: $('#ruleDescription').val().trim(),
            scheduleId: scheduleId ? parseInt(scheduleId) : null
        };

        // Simple family validation: if value looks IPv4/IPv6, ensure it matches family (unless dual)
        const family = rule.addressFamily || 'ipv4';
        if (family !== 'dual') {
            const srcFam = this.detectFamily(rule.sourceValue);
            const dstFam = this.detectFamily(rule.destinationValue);
            if (srcFam && srcFam !== family) {
                Monolith.UI.toast(`Source value appears ${srcFam} but rule family is ${family}`, 'warning');
                return;
            }
            if (dstFam && dstFam !== family) {
                Monolith.UI.toast(`Destination value appears ${dstFam} but rule family is ${family}`, 'warning');
                return;
            }
        }

        try {
            let response;
            if (id) {
                response = await Monolith.API.put(`/firewall/rules/${id}`, payload);
            } else {
                response = await Monolith.API.post('/firewall/rules', payload);
            }

            if (response.Success || response.success) {
                bootstrap.Modal.getInstance(document.getElementById('ruleModal')).hide();
                this.markPendingChanges();
                await this.loadRules();
                this.renderTabs();
                this.showMessage('Rule saved successfully', 'success');
            } else {
                this.showMessage(response.Error || response.error || 'Failed to save rule', 'danger');
            }
        } catch (error) {
            console.error('Failed to save rule:', error);
            this.showMessage('Failed to save rule', 'danger');
        }
    },

    deleteRule: async function(id) {
        if (!confirm('Delete this rule?')) {
            return;
        }

        try {
            const response = await Monolith.API.delete(`/firewall/rules/${id}`);
            if (response.Success || response.success) {
                this.markPendingChanges();
                await this.loadRules();
                this.renderTabs();
                this.showMessage('Rule deleted', 'success');
            } else {
                this.showMessage(response.Error || response.error || 'Failed to delete rule', 'danger');
            }
        } catch (error) {
            console.error('Failed to delete rule:', error);
            this.showMessage('Failed to delete rule', 'danger');
        }
    },

    moveRule: async function(interfaceName, id, direction) {
        const rules = this.rules.filter(r => r.interface && r.interface.toLowerCase() === interfaceName.toLowerCase() && !r.isSystem);
        const index = rules.findIndex(r => r.id === id);
        if (index === -1) {
            return;
        }

        const targetIndex = index + direction;
        if (targetIndex < 0 || targetIndex >= rules.length) {
            return;
        }

        const reordered = rules.map(r => r.id);
        const temp = reordered[index];
        reordered[index] = reordered[targetIndex];
        reordered[targetIndex] = temp;

        try {
            const response = await Monolith.API.post('/firewall/rules/reorder', {
                interface: interfaceName,
                ruleIds: reordered
            });
            if (response.Success || response.success) {
                this.markPendingChanges();
                await this.loadRules();
                this.renderTabs();
            } else {
                this.showMessage(response.Error || response.error || 'Failed to reorder rules', 'danger');
            }
        } catch (error) {
            console.error('Failed to reorder rules:', error);
            this.showMessage('Failed to reorder rules', 'danger');
        }
    },

    markPendingChanges: function() {
        $('#pendingChangesBanner').removeClass('d-none');
    },

    applyChanges: async function() {
        if (!confirm('Apply firewall changes now?')) {
            return;
        }

        try {
            const response = await Monolith.API.post('/firewall/apply', {});
            if (response.Success || response.success) {
                $('#pendingChangesBanner').addClass('d-none');
                this.showMessage('Firewall rules applied', 'success');
            } else {
                this.showMessage(response.Error || response.error || 'Failed to apply rules', 'danger');
            }
        } catch (error) {
            console.error('Failed to apply rules:', error);
            this.showMessage('Failed to apply rules', 'danger');
        }
    },

    previewChanges: async function() {
        try {
            const response = await Monolith.API.get('/firewall/preview');
            if (response.Success || response.success) {
                const data = response.Data || response.data || {};
                this.showPreviewModal(data);
            } else {
                this.showMessage(response.Error || response.error || 'Failed to load preview', 'danger');
            }
        } catch (error) {
            console.error('Failed to load preview:', error);
            this.showMessage('Failed to load preview', 'danger');
        }
    },

    showPreviewModal: function(data) {
        const warnings = data.warnings || data.Warnings || [];
        const warningHtml = warnings.length > 0
            ? `<div class="alert alert-warning small mb-3">${warnings.map(w => `<div>${w}</div>`).join('')}</div>`
            : '';
        const configText = data.config || data.Config || '';
        const modalHtml = `
            <div class="modal fade" id="firewallPreviewModal" tabindex="-1" aria-labelledby="firewallPreviewLabel" aria-hidden="true">
                <div class="modal-dialog modal-xl">
                    <div class="modal-content">
                        <div class="modal-header">
                            <h5 class="modal-title" id="firewallPreviewLabel">Firewall Config Preview</h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                        </div>
                        <div class="modal-body">
                            ${warningHtml}
                            <pre class="bg-light border rounded p-3 small" style="max-height: 60vh; overflow: auto;">${this.escapeHtml(configText)}</pre>
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Close</button>
                        </div>
                    </div>
                </div>
            </div>
        `;

        $('#firewallPreviewModal').remove();
        $('body').append(modalHtml);
        const modal = new bootstrap.Modal(document.getElementById('firewallPreviewModal'));
        modal.show();
        $('#firewallPreviewModal').on('hidden.bs.modal', function() {
            $(this).remove();
        });
    },

    discardChanges: async function() {
        if (!confirm('Discard pending firewall changes?')) {
            return;
        }

        try {
            const response = await Monolith.API.post('/firewall/discard', {});
            if (response.Success || response.success) {
                $('#pendingChangesBanner').addClass('d-none');
                await this.loadRules();
                this.renderTabs();
                this.showMessage('Pending changes discarded', 'info');
            } else {
                this.showMessage(response.Error || response.error || 'Failed to discard changes', 'danger');
            }
        } catch (error) {
            console.error('Failed to discard changes:', error);
            this.showMessage('Failed to discard changes', 'danger');
        }
    },

    renderDefaults: function() {
        if (!this.defaults) {
            return;
        }

        const lan = this.defaults.LanDefaultAction || this.defaults.lanDefaultAction || 'pass';
        const wan = this.defaults.WanDefaultAction || this.defaults.wanDefaultAction || 'block';
        const opt = this.defaults.OptDefaultAction || this.defaults.optDefaultAction || 'block';
        const blockReserved = this.defaults.BlockReservedOnWan !== undefined
            ? this.defaults.BlockReservedOnWan
            : (this.defaults.blockReservedOnWan !== undefined ? this.defaults.blockReservedOnWan : true);
        const allowWebUi = this.defaults.AllowManagementWebUi !== undefined
            ? this.defaults.AllowManagementWebUi
            : (this.defaults.allowManagementWebUi !== undefined ? this.defaults.allowManagementWebUi : true);
        const allowDevAccess = this.defaults.AllowDeveloperSystemAccess !== undefined
            ? this.defaults.AllowDeveloperSystemAccess
            : (this.defaults.allowDeveloperSystemAccess !== undefined ? this.defaults.allowDeveloperSystemAccess : true);

        $('#defaultLanAction').val(lan);
        $('#defaultWanAction').val(wan);
        $('#defaultOptAction').val(opt);
        $('#defaultBlockReserved').prop('checked', !!blockReserved);
        $('#defaultAllowWebUi').prop('checked', !!allowWebUi);
        $('#defaultAllowDevAccess').prop('checked', !!allowDevAccess);
    },

    saveDefaults: async function() {
        const payload = {
            lanDefaultAction: $('#defaultLanAction').val(),
            wanDefaultAction: $('#defaultWanAction').val(),
            optDefaultAction: $('#defaultOptAction').val(),
            blockReservedOnWan: $('#defaultBlockReserved').is(':checked'),
            allowManagementWebUi: $('#defaultAllowWebUi').is(':checked'),
            allowDeveloperSystemAccess: $('#defaultAllowDevAccess').is(':checked')
        };

        try {
            const response = await Monolith.API.post('/firewall/defaults', payload);
            if (response.Success || response.success) {
                this.defaults = response.Data || response.data || this.defaults;
                this.renderDefaults();
                this.showMessage('Defaults saved', 'success');
                this.markPendingChanges();
            } else {
                this.showMessage(response.Error || response.error || 'Failed to save defaults', 'danger');
            }
        } catch (error) {
            console.error('Failed to save defaults:', error);
            this.showMessage('Failed to save defaults', 'danger');
        }
    },

    actionBadge: function(action) {
        switch (action) {
            case 'pass':
                return '<span class="badge bg-success">Pass</span>';
            case 'reject':
                return '<span class="badge bg-warning">Reject</span>';
            default:
                return '<span class="badge bg-danger">Block</span>';
        }
    },

    protocolLabel: function(protocol) {
        return protocol ? protocol.toUpperCase() : 'ANY';
    },

    formatEndpoint: function(type, value, port) {
        if (!type || type === 'any') {
            return '<span class="text-muted">Any</span>';
        }
        let result = value || '-';
        if (type === 'alias') {
            result = `<code>${result}</code>`;
        }
        if (type === 'system') {
            result = `<span class="text-muted">${this.systemSetLabel(value)}</span>`;
        }
        if (port) {
            result += ':' + port;
        }
        return result;
    },

    bindRuleModalInputs: function() {
        const updateSource = () => {
            this.updateAddressInput('#ruleSourceType', '#ruleSourceValue');
        };
        const updateDestination = () => {
            this.updateAddressInput('#ruleDestinationType', '#ruleDestinationValue');
        };
        const updateSystemList = () => {
            const family = $('#ruleFamily').val() || 'ipv4';
            const options = this.systemSetOptions(family)
                .map(option => `<option value="${option.value}">${option.label}</option>`)
                .join('');
            $('#rulesSystemSetList').html(options);
            updateSource();
            updateDestination();
        };

        $(document).off('change', '#ruleSourceType');
        $(document).on('change', '#ruleSourceType', updateSource);
        $(document).off('change', '#ruleDestinationType');
        $(document).on('change', '#ruleDestinationType', updateDestination);
        $(document).off('change', '#ruleFamily');
        $(document).on('change', '#ruleFamily', () => {
            const family = $('#ruleFamily').val() || 'ipv4';
            $('#ruleSourceHint, #ruleDestinationHint').text(family === 'dual'
                ? 'IPv4 or IPv6 allowed.'
                : `Use ${family.toUpperCase()} addresses.`);
            updateSystemList();
        });

        updateSystemList();
    },

    updateAddressInput: function(typeSelector, valueSelector) {
        const type = $(typeSelector).val();
        const input = $(valueSelector);
        if (type === 'alias') {
            input.attr('list', 'rulesAddressAliasList');
            input.attr('placeholder', 'Alias name');
        } else if (type === 'system') {
            input.attr('list', 'rulesSystemSetList');
            input.attr('placeholder', 'System set');
        } else {
            input.removeAttr('list');
            input.attr('placeholder', 'IP or network');
        }
    },

    detectFamily: function(value) {
        if (!value) return null;
        if (value.includes(':')) return 'ipv6';
        if (value.match(/^(\d{1,3}\.){3}\d{1,3}(\/\d+)?$/)) return 'ipv4';
        return null;
    },

    systemSetOptions: function(family) {
        if (family === 'ipv4') {
            return this.systemSets.filter(s => s.value === 'rfc1918' || s.value === 'iana_reserved');
        }
        if (family === 'ipv6') {
            return this.systemSets.filter(s => s.value === 'rfc4193' || s.value === 'iana_reserved_v6');
        }
        return this.systemSets.slice();
    },

    systemSetLabel: function(value) {
        const match = this.systemSets.find(s => s.value === value);
        return match ? match.label : (value || '-');
    },

    roleLabel: function(role) {
        if (typeof role === 'string') {
            return role.toUpperCase();
        }
        switch (role) {
            case 1:
                return 'LAN';
            case 2:
                return 'WAN';
            case 3:
                return 'OPT';
            default:
                return 'UNKNOWN';
        }
    },

    defaultActionLabel: function(role) {
        if (!this.defaults) {
            return 'block';
        }

        const action = role === 1
            ? this.defaults.LanDefaultAction || this.defaults.lanDefaultAction
            : role === 2
                ? this.defaults.WanDefaultAction || this.defaults.wanDefaultAction
                : this.defaults.OptDefaultAction || this.defaults.optDefaultAction;
        return action || 'block';
    },

    showMessage: function(message, type) {
        const alert = $('#rulesStatusMessage');
        if (!alert.length) return;
        alert.removeClass('d-none alert-success alert-danger alert-warning alert-info')
             .addClass(`alert-${type}`)
             .text(message);
        setTimeout(() => alert.addClass('d-none'), 5000);
    },

    escapeHtml: function(value) {
        return (value || '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#039;');
    }
};

if (typeof Monolith !== 'undefined') {
    Monolith.Pages = Monolith.Pages || {};
    Monolith.Pages.Firewall = Monolith.Pages.Firewall || {};
    Monolith.Pages.Firewall.Rules = Rules;
    Monolith.Pages.Rules = Rules;
}
