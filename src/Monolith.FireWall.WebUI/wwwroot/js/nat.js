// Firewall NAT Module
var Nat = {
    rules: [],
    aliases: [],

    init: function() {
        console.log('Initializing NAT module...');
        this.loadAliases();
        this.loadRules();
        this.attachEventHandlers();
    },

    loadAliases: async function() {
        try {
            const response = await Monolith.API.get('/firewall/aliases');
            if (response.Success || response.success) {
                const data = response.Data || response.data || {};
                const items = data.items || data || [];
                this.aliases = items.map(a => ({
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
                this.rules = items.map(r => this.normalizeRule(r));
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
        const tbody = $('#natTableBody');
        if (this.rules.length === 0) {
            tbody.html('<tr><td colspan="9" class="text-center text-muted">No NAT rules configured</td></tr>');
            return;
        }

        let html = '';
        this.rules.forEach(rule => {
            const statusBadge = rule.enabled
                ? '<span class="badge bg-success">Enabled</span>'
                : '<span class="badge bg-secondary">Disabled</span>';

            html += `
                <tr data-rule-id="${rule.id}">
                    <td><strong>${rule.ruleNumber}</strong></td>
                    <td><code>${rule.interface}</code></td>
                    <td><span class="badge bg-info">${this.formatFamily(rule.addressFamily)}</span></td>
                    <td><span class="badge bg-secondary">${rule.protocol}</span></td>
                    <td>${this.formatAddress(rule.sourceType, rule.sourceValue, rule.sourcePort)}</td>
                    <td>${this.formatAddress(rule.destinationType, rule.destinationValue, rule.destinationPort)}</td>
                    <td>${this.formatTarget(rule.redirectTargetIp, rule.redirectTargetPort)}</td>
                    <td>${rule.description || '-'}</td>
                    <td>${statusBadge}</td>
                    <td>
                        <button class="btn btn-sm btn-outline-primary me-1" onclick="Nat.editRule(${rule.id})">Edit</button>
                        <button class="btn btn-sm btn-outline-danger" onclick="Nat.deleteRule(${rule.id})">Delete</button>
                    </td>
                </tr>
            `;
        });
        tbody.html(html);
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
            this.showRuleModal(null);
        });

        $(document).off('click', '#btnApplyChanges');
        $(document).on('click', '#btnApplyChanges', () => {
            this.applyChanges();
        });

        $(document).off('click', '#btnDiscardChanges');
        $(document).on('click', '#btnDiscardChanges', () => {
            this.discardChanges();
        });
    },

    showRuleModal: function(rule) {
        const isEdit = rule !== null;
        const aliasOptions = this.aliases.map(a => `<option value="${a.name}">${a.name}</option>`).join('');
        const modalHtml = `
            <div class="modal fade" id="natRuleModal" tabindex="-1" aria-labelledby="natRuleModalLabel" aria-hidden="true">
                <div class="modal-dialog modal-xl">
                    <div class="modal-content">
                        <div class="modal-header">
                            <h5 class="modal-title" id="natRuleModalLabel">${isEdit ? 'Edit' : 'Add'} NAT Rule</h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                        </div>
                        <div class="modal-body">
                            <form id="natRuleForm">
                                <div class="row">
                                    <div class="col-md-4 mb-3">
                                        <label for="natType" class="form-label">Type <span class="text-danger">*</span></label>
                                        <select class="form-select" id="natType" required>
                                            <option value="port_forward" ${rule && rule.type === 'port_forward' ? 'selected' : ''}>Port Forward</option>
                                            <option value="one_to_one" ${rule && rule.type === 'one_to_one' ? 'selected' : ''}>1:1 NAT</option>
                                            <option value="outbound" ${rule && rule.type === 'outbound' ? 'selected' : ''}>Outbound</option>
                                        </select>
                                    </div>
                                    <div class="col-md-4 mb-3">
                                        <label for="natInterface" class="form-label">Interface <span class="text-danger">*</span></label>
                                        <input type="text" class="form-control" id="natInterface" required
                                               value="${rule ? rule.interface : ''}"
                                               placeholder="e.g., wan">
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
                                    <div class="col-md-4 mb-3">
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
                                    <div class="col-md-4 mb-3">
                                        <label for="natDestinationPort" class="form-label">Destination Port</label>
                                        <input type="text" class="form-control" id="natDestinationPort"
                                               value="${rule ? (rule.destinationPort || '') : ''}"
                                               placeholder="e.g., 443">
                                    </div>
                                </div>
                                <div class="row">
                                    <div class="col-md-6 mb-3">
                                        <label for="natRedirectTargetIp" class="form-label">Redirect Target IP <span class="text-danger">*</span></label>
                                        <input type="text" class="form-control" id="natRedirectTargetIp" required
                                               value="${rule ? (rule.redirectTargetIp || '') : ''}"
                                               placeholder="Target IP">
                                    </div>
                                    <div class="col-md-6 mb-3">
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
                            <button type="button" class="btn btn-primary" onclick="Nat.saveRule(${rule ? rule.id : 'null'})">${isEdit ? 'Update' : 'Create'}</button>
                        </div>
                    </div>
                </div>
            </div>
        `;

        $('#natRuleModal').remove();
        $('body').append(modalHtml);
        const modal = new bootstrap.Modal(document.getElementById('natRuleModal'));
        modal.show();

        $('#natRuleModal').on('hidden.bs.modal', function() {
            $(this).remove();
        });
    },

    editRule: async function(id) {
        try {
            const response = await Monolith.API.get(`/firewall/nat/${id}`);
            if (response.Success || response.success) {
                const rule = this.normalizeRule(response.Data || response.data);
                this.showRuleModal(rule);
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
        if (!form.checkValidity()) {
            form.reportValidity();
            return;
        }

        const rule = {
            type: $('#natType').val(),
            interface: $('#natInterface').val().trim(),
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
            enabled: $('#natEnabled').is(':checked')
        };

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
