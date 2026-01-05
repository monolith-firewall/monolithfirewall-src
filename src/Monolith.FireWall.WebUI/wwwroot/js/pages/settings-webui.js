// Web UI Settings Tab
var SettingsWebUI = {
    settings: {},
    availableInterfaces: [],

    init: function() {
        console.log('Initializing Web UI Settings tab...');
        this.loadInterfaces();
        this.render();
        this.loadSettings();
    },

    loadInterfaces: async function() {
        try {
            const response = await Monolith.API.get('/interfaces/assignments');
            const data = response.Data || response.data || {};
            const assignments = Array.isArray(data) ? data : (data.assignments || []);
            
            // Extract IP addresses from interface assignments
            this.availableInterfaces = [];
            assignments.forEach(assignment => {
                if (assignment.IpAddress || assignment.ipAddress) {
                    const ip = assignment.IpAddress || assignment.ipAddress;
                    const iface = assignment.InterfaceName || assignment.interfaceName || '';
                    this.availableInterfaces.push({
                        ip: ip,
                        interface: iface,
                        label: `${iface} (${ip})`
                    });
                }
            });
            
            // Also add common localhost addresses
            this.availableInterfaces.push(
                { ip: '127.0.0.1', interface: 'localhost', label: 'localhost (127.0.0.1)' },
                { ip: '::1', interface: 'localhost', label: 'localhost IPv6 (::1)' }
            );
        } catch (error) {
            console.error('Failed to load interfaces:', error);
            this.availableInterfaces = [
                { ip: '127.0.0.1', interface: 'localhost', label: 'localhost (127.0.0.1)' }
            ];
        }
    },

    render: function() {
        const container = $('#webui-tab-content');
        container.html(`
            <form id="webui-settings-form">
                <div class="row">
                    <div class="col-md-8">
                        <div class="card mb-4">
                            <div class="card-header">
                                <h5 class="mb-0">Web UI Configuration</h5>
                            </div>
                            <div class="card-body">
                                <div class="mb-3">
                                    <label for="http-port" class="form-label">HTTP Port</label>
                                    <input type="number" class="form-control" id="http-port" min="1" max="65535" placeholder="80">
                                    <div class="form-text">Port for HTTP access (default: 80)</div>
                                </div>

                                <div class="mb-3">
                                    <label for="https-port" class="form-label">HTTPS Port</label>
                                    <input type="number" class="form-control" id="https-port" min="1" max="65535" placeholder="443">
                                    <div class="form-text">Port for HTTPS access (default: 443)</div>
                                </div>

                                <div class="mb-3">
                                    <div class="form-check mb-3">
                                        <input class="form-check-input" type="checkbox" id="bind-all-interfaces">
                                        <label class="form-check-label" for="bind-all-interfaces">
                                            Bind to all interfaces
                                        </label>
                                        <div class="form-text">If checked, WebUI will be accessible on all network interfaces</div>
                                    </div>
                                </div>

                                <div class="mb-3" id="binding-addresses-section" style="display: none;">
                                    <label class="form-label">Binding Addresses</label>
                                    <div id="binding-addresses-list" class="d-grid gap-2"></div>
                                    <button type="button" class="btn btn-outline-secondary btn-sm mt-2" id="add-binding-address">
                                        Add IP Address
                                    </button>
                                    <div class="form-text">Select specific IP addresses to bind WebUI to</div>
                                </div>

                                <div class="alert alert-warning" id="port-change-warning" style="display: none;">
                                    <strong>Warning:</strong> Changing the port may cause you to lose connection. 
                                    Make sure you can access the new port before applying.
                                </div>
                            </div>
                        </div>

                        <div class="d-flex gap-2">
                            <button type="submit" class="btn btn-primary">Save & Apply</button>
                            <button type="button" class="btn btn-outline-secondary" id="reset-webui-btn">Reset to Defaults</button>
                        </div>
                    </div>

                    <div class="col-md-4">
                        <div class="card">
                            <div class="card-header">
                                <h5 class="mb-0">Current Status</h5>
                            </div>
                            <div class="card-body">
                                <dl class="row mb-0">
                                    <dt class="col-sm-6">HTTP Port:</dt>
                                    <dd class="col-sm-6" id="current-http-port">-</dd>
                                    
                                    <dt class="col-sm-6">HTTPS Port:</dt>
                                    <dd class="col-sm-6" id="current-https-port">-</dd>
                                    
                                    <dt class="col-sm-6">Binding:</dt>
                                    <dd class="col-sm-6" id="current-binding">-</dd>
                                </dl>
                            </div>
                        </div>
                    </div>
                </div>
            </form>
        `);

        $('#webui-settings-form').on('submit', (e) => {
            e.preventDefault();
            this.saveSettings();
        });

        $('#reset-webui-btn').on('click', () => this.resetSettings());

        $('#bind-all-interfaces').on('change', () => {
            const bindAll = $('#bind-all-interfaces').is(':checked');
            $('#binding-addresses-section').toggle(!bindAll);
        });

        $('#http-port, #https-port').on('change', () => {
            this.checkPortChange();
        });

        $(document).off('click', '#add-binding-address');
        $(document).on('click', '#add-binding-address', () => this.addBindingAddress());

        $(document).off('click', '.binding-remove-btn');
        $(document).on('click', '.binding-remove-btn', (e) => {
            $(e.currentTarget).closest('.binding-address-row').remove();
        });
    },

    loadSettings: async function() {
        try {
            const response = await Monolith.API.get('/webui/settings');
            const data = response.Data || response.data || {};
            this.settings = {
                httpPort: data.httpPort || data.HttpPort || 80,
                httpsPort: data.httpsPort || data.HttpsPort || 443,
                bindToAllInterfaces: data.bindToAllInterfaces !== false,
                bindingAddresses: data.bindingAddresses || data.BindingAddresses || []
            };
        } catch (error) {
            console.error('Failed to load WebUI settings:', error);
            this.settings = {
                httpPort: 80,
                httpsPort: 443,
                bindToAllInterfaces: true,
                bindingAddresses: []
            };
            Monolith.UI.toast('Failed to load WebUI settings', 'error');
        }

        $('#http-port').val(this.settings.httpPort);
        $('#https-port').val(this.settings.httpsPort);
        $('#bind-all-interfaces').prop('checked', this.settings.bindToAllInterfaces);
        $('#binding-addresses-section').toggle(!this.settings.bindToAllInterfaces);
        this.renderBindingAddresses(this.settings.bindingAddresses);
        this.updateCurrentStatus();
    },

    saveSettings: async function() {
        const httpPort = parseInt($('#http-port').val()) || 80;
        const httpsPort = parseInt($('#https-port').val()) || 443;
        const bindAll = $('#bind-all-interfaces').is(':checked');
        const bindingAddresses = bindAll ? [] : this.getBindingAddresses();

        // Validate ports
        if (httpPort < 1 || httpPort > 65535) {
            Monolith.UI.toast('HTTP port must be between 1 and 65535', 'error');
            return;
        }

        if (httpsPort < 1 || httpsPort > 65535) {
            Monolith.UI.toast('HTTPS port must be between 1 and 65535', 'error');
            return;
        }

        const payload = {
            httpPort: httpPort,
            httpsPort: httpsPort,
            bindToAllInterfaces: bindAll,
            bindingAddresses: bindingAddresses
        };

        try {
            const response = await Monolith.API.post('/webui/settings', payload);
            if (!(response.Success || response.success)) {
                throw new Error(response.Error || response.error || 'Failed to save settings');
            }

            const updateResult = response.Data || response.data || {};
            if (updateResult.requiresRestart || updateResult.RequiresRestart) {
                // Show confirmation for restart
                if (confirm('WebUI settings have been saved. The WebUI service needs to be restarted for changes to take effect. Restart now?')) {
                    await this.restartService();
                } else {
                    Monolith.UI.toast('Settings saved. Restart WebUI service to apply changes.', 'info');
                }
            } else {
                Monolith.UI.toast('WebUI settings saved successfully', 'success');
            }

            await this.loadSettings();
        } catch (error) {
            console.error('Failed to save WebUI settings:', error);
            Monolith.UI.toast('Failed to save WebUI settings', 'error');
        }
    },

    restartService: async function() {
        try {
            Monolith.UI.toast('Restarting WebUI service...', 'info');
            const response = await Monolith.API.post('/webui/service/restart');
            if (response.Success || response.success) {
                Monolith.UI.toast('WebUI service restarted successfully', 'success');
                // Give it a moment, then reload page
                setTimeout(() => {
                    window.location.reload();
                }, 2000);
            } else {
                throw new Error(response.Error || response.error || 'Failed to restart service');
            }
        } catch (error) {
            console.error('Failed to restart WebUI service:', error);
            Monolith.UI.toast('Failed to restart WebUI service', 'error');
        }
    },

    resetSettings: function() {
        if (confirm('Reset WebUI settings to defaults (HTTP=80, HTTPS=443, all interfaces)?')) {
            this.settings = {
                httpPort: 80,
                httpsPort: 443,
                bindToAllInterfaces: true,
                bindingAddresses: []
            };
            $('#http-port').val(80);
            $('#https-port').val(443);
            $('#bind-all-interfaces').prop('checked', true);
            $('#binding-addresses-section').hide();
            this.renderBindingAddresses([]);
            Monolith.UI.toast('WebUI settings reset to defaults', 'info');
        }
    },

    checkPortChange: function() {
        const httpPort = parseInt($('#http-port').val()) || 80;
        const httpsPort = parseInt($('#https-port').val()) || 443;
        const currentHttp = this.settings.httpPort || 80;
        const currentHttps = this.settings.httpsPort || 443;

        if (httpPort !== currentHttp || httpsPort !== currentHttps) {
            $('#port-change-warning').show();
        } else {
            $('#port-change-warning').hide();
        }
    },

    renderBindingAddresses: function(addresses) {
        const container = $('#binding-addresses-list');
        const entries = Array.isArray(addresses) ? addresses.filter(a => a) : [];
        const initial = entries.length > 0 ? entries : [''];
        const rows = initial.map(value => this.buildBindingRow(value)).join('');
        container.html(rows);
        
        // Bind select change handlers
        container.find('.binding-address-select').on('change', function() {
            const val = $(this).val();
            const input = $(this).siblings('.binding-address-input');
            if (val === 'custom') {
                input.show().focus();
            } else if (val) {
                input.hide();
                input.val(val);
            } else {
                input.hide();
                input.val('');
            }
        });
    },

    buildBindingRow: function(value) {
        const safeValue = value ? value.toString() : '';
        const isCustom = safeValue && !this.availableInterfaces.find(i => i.ip === safeValue);
        const options = this.availableInterfaces.map(iface => 
            `<option value="${iface.ip}" ${iface.ip === safeValue ? 'selected' : ''}>${iface.label}</option>`
        ).join('');
        
        return `
            <div class="input-group binding-address-row">
                <select class="form-select binding-address-select">
                    <option value="">Select IP address...</option>
                    ${options}
                    <option value="custom" ${isCustom ? 'selected' : ''}>Custom IP...</option>
                </select>
                <input type="text" class="form-control binding-address-input" value="${isCustom ? safeValue : ''}" 
                       placeholder="e.g., 192.168.1.1" 
                       style="${isCustom ? '' : 'display: none;'}">
                <button class="btn btn-outline-danger binding-remove-btn" type="button">Remove</button>
            </div>
        `;
    },

    addBindingAddress: function() {
        const container = $('#binding-addresses-list');
        container.append(this.buildBindingRow(''));
        
        // Handle select change for all selects
        container.find('.binding-address-select').off('change').on('change', function() {
            const val = $(this).val();
            const input = $(this).siblings('.binding-address-input');
            if (val === 'custom') {
                input.show().focus();
            } else if (val) {
                input.hide();
                input.val(val);
            } else {
                input.hide();
                input.val('');
            }
        });
    },

    getBindingAddresses: function() {
        const values = [];
        $('#binding-addresses-list .binding-address-row').each((_, row) => {
            const select = $(row).find('.binding-address-select');
            const input = $(row).find('.binding-address-input');
            let value = select.val();
            if (value === 'custom') {
                value = input.val();
            }
            if (value && value.trim()) {
                values.push(value.trim());
            }
        });
        return values;
    },

    updateCurrentStatus: function() {
        $('#current-http-port').text(this.settings.httpPort || 80);
        $('#current-https-port').text(this.settings.httpsPort || 443);
        if (this.settings.bindToAllInterfaces) {
            $('#current-binding').text('All interfaces');
        } else {
            const count = this.settings.bindingAddresses?.length || 0;
            $('#current-binding').text(count > 0 ? `${count} address(es)` : 'Not configured');
        }
    }
};

if (typeof Monolith !== 'undefined') {
    Monolith.Pages = Monolith.Pages || {};
    Monolith.Pages.SettingsWebUI = SettingsWebUI;
}
