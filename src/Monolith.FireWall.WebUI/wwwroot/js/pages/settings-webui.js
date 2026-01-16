// Web UI Settings Tab
var SettingsWebUI = {
    settings: {},
    availableInterfaces: [],

    init: function() {
        console.log('Initializing Web UI Settings tab...');
    },

    renderPage: function() {
        console.log('Rendering Web UI Settings tab...');
        this.renderStructure();
        this.loadInterfaces().then(() => {
            this.loadSettings();
        });
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

    renderStructure: function() {
        const container = $('#webui-tab-content');
        if (!container.length) return;

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
            $('#port-change-warning').show();
        });
        
        $('#add-binding-address').on('click', () => {
            this.addBindingAddress('');
        });
    },

    loadSettings: async function() {
        try {
            const response = await Monolith.API.get('/system/settings/webui');
            const data = response.Data || response.data || {};
            this.settings = {
                httpPort: data.HttpPort || data.httpPort || 80,
                httpsPort: data.HttpsPort || data.httpsPort || 443,
                bindAll: data.BindAll !== false,
                bindingAddresses: data.BindingAddresses || []
            };
        } catch (error) {
            console.error('Failed to load WebUI settings:', error);
            this.settings = {
                httpPort: 80,
                httpsPort: 443,
                bindAll: true,
                bindingAddresses: []
            };
        }

        $('#http-port').val(this.settings.httpPort);
        $('#https-port').val(this.settings.httpsPort);
        $('#bind-all-interfaces').prop('checked', this.settings.bindAll).trigger('change');
        
        this.renderBindingAddresses(this.settings.bindingAddresses);
        this.updateStatusDisplay();
    },

    updateStatusDisplay: function() {
        $('#current-http-port').text(this.settings.httpPort);
        $('#current-https-port').text(this.settings.httpsPort);
        $('#current-binding').text(this.settings.bindAll ? 'All Interfaces' : `${this.settings.bindingAddresses.length} addresses`);
    },

    renderBindingAddresses: function(addresses) {
        const container = $('#binding-addresses-list');
        if (!container.length) return;
        container.empty();
        
        if (addresses && addresses.length > 0) {
            addresses.forEach(addr => this.addBindingAddress(addr));
        } else if (!this.settings.bindAll) {
            this.addBindingAddress('');
        }
    },

    addBindingAddress: function(value) {
        const container = $('#binding-addresses-list');
        if (!container.length) return;

        const row = $(`
            <div class="input-group mb-2">
                <select class="form-select binding-address-input">
                    <option value="">Select Address...</option>
                </select>
                <button class="btn btn-outline-danger remove-binding-btn" type="button">Remove</button>
            </div>
        `);

        const select = row.find('select');
        this.availableInterfaces.forEach(iface => {
            select.append(`<option value="${iface.ip}" ${iface.ip === value ? 'selected' : ''}>${iface.label}</option>`);
        });

        row.find('.remove-binding-btn').on('click', function() {
            $(this).closest('.input-group').remove();
        });

        container.append(row);
    },

    getBindingAddresses: function() {
        const addresses = [];
        $('.binding-address-input').each(function() {
            const val = $(this).val();
            if (val) addresses.push(val);
        });
        return addresses;
    },

    saveSettings: async function() {
        const payload = {
            httpPort: parseInt($('#http-port').val()),
            httpsPort: parseInt($('#https-port').val()),
            bindAll: $('#bind-all-interfaces').is(':checked'),
            bindingAddresses: this.getBindingAddresses()
        };

        try {
            const response = await Monolith.API.post('/system/settings/webui', payload);
            if (!(response.Success || response.success)) {
                throw new Error(response.Error || response.error || 'Failed to save settings');
            }

            Monolith.UI.toast('WebUI settings saved. Re-applying configuration...', 'success');
            $('#port-change-warning').hide();
            await this.loadSettings();
        } catch (error) {
            console.error('Failed to save WebUI settings:', error);
            Monolith.UI.toast('Failed to save WebUI settings', 'error');
        }
    },

    resetSettings: function() {
        if (confirm('Reset WebUI settings to defaults?')) {
            this.loadSettings();
        }
    }
};

if (typeof Monolith !== 'undefined') {
    Monolith.Pages = Monolith.Pages || {};
    Monolith.Pages.SettingsWebUI = SettingsWebUI;
}