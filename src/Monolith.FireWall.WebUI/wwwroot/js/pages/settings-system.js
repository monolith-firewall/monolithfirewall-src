// System Settings Tab
var SettingsSystem = {
    settings: {},

    init: function() {
        console.log('Initializing System Settings tab...');
    },

    renderPage: function() {
        console.log('Rendering System Settings tab...');
        this.renderStructure();
        this.loadTimezones().then(() => {
            this.loadSettings();
        });
    },

    renderStructure: function() {
        const container = $('#system-tab-content');
        if (!container.length) return;

        container.html(`
            <form id="system-settings-form">
                <div class="row">
                    <div class="col-md-8">
                        <div class="card mb-4">
                            <div class="card-header">
                                <h5 class="mb-0">System Information</h5>
                            </div>
                            <div class="card-body">
                                <div class="mb-3">
                                    <label for="hostname" class="form-label">Hostname</label>
                                    <input type="text" class="form-control" id="hostname" placeholder="monolith-fw">
                                    <div class="form-text">The hostname of this firewall</div>
                                </div>

                                <div class="mb-3">
                                    <label for="domain" class="form-label">Domain</label>
                                    <input type="text" class="form-control" id="domain" placeholder="local">
                                    <div class="form-text">The domain name for this system</div>
                                </div>

                                <div class="mb-3">
                                    <label for="timezone" class="form-label">Timezone</label>
                                    <select class="form-select" id="timezone">
                                        <option value="">Loading timezones...</option>
                                    </select>
                                    <div class="form-text">System timezone (loaded from OS)</div>
                                </div>

                                <div class="mb-3">
                                    <label class="form-label">DNS Servers</label>
                                    <div id="dns-servers-list" class="d-grid gap-2"></div>
                                    <button type="button" class="btn btn-outline-secondary btn-sm mt-2" id="add-dns-server">
                                        Add DNS Server
                                    </button>
                                    <div class="form-text">Default resolvers for managed interfaces.</div>
                                </div>
                            </div>
                        </div>

                        <div class="d-flex gap-2">
                            <button type="submit" class="btn btn-primary">Save Settings</button>
                            <button type="button" class="btn btn-outline-secondary" id="reset-settings-btn">Reset to Defaults</button>
                        </div>
                    </div>

                    <div class="col-md-4">
                        <div class="card">
                            <div class="card-header">
                                <h5 class="mb-0">System Info</h5>
                            </div>
                            <div class="card-body">
                                <dl class="row mb-0">
                                    <dt class="col-sm-6">Version:</dt>
                                    <dd class="col-sm-6">1.0.0</dd>
                                    
                                    <dt class="col-sm-6">Build:</dt>
                                    <dd class="col-sm-6">2026.01.01</dd>
                                    
                                    <dt class="col-sm-6">Platform:</dt>
                                    <dd class="col-sm-6">Linux x64</dd>
                                </dl>
                            </div>
                        </div>
                    </div>
                </div>
            </form>
        `);

        $('#system-settings-form').on('submit', (e) => {
            e.preventDefault();
            this.saveSettings();
        });

        $('#reset-settings-btn').on('click', () => this.resetSettings());

        $(document).off('click', '#add-dns-server');
        $(document).on('click', '#add-dns-server', () => this.addDnsServer());

        $(document).off('click', '.dns-remove-btn');
        $(document).on('click', '.dns-remove-btn', (e) => {
            $(e.currentTarget).closest('.dns-server-row').remove();
        });
    },

    loadTimezones: async function() {
        try {
            const response = await Monolith.API.get('/system/settings/timezones');
            const data = response.Data || response.data || {};
            const timezones = data.timezones || data.Timezones || [];
            
            const select = $('#timezone');
            if (!select.length) return;
            select.empty();
            
            if (timezones.length > 0) {
                timezones.forEach(tz => {
                    select.append(`<option value="${tz}">${tz}</option>`);
                });
            } else {
                // Fallback to common timezones if API fails
                const fallback = [
                    'UTC', 'America/New_York', 'America/Chicago', 'America/Denver', 
                    'America/Los_Angeles', 'Europe/London', 'Europe/Paris', 
                    'Europe/Berlin', 'Asia/Tokyo', 'Asia/Shanghai'
                ];
                fallback.forEach(tz => {
                    select.append(`<option value="${tz}">${tz}</option>`);
                });
                console.warn('Using fallback timezones - API returned no timezones');
            }
        } catch (error) {
            console.error('Failed to load timezones:', error);
            // Fallback to common timezones
            const select = $('#timezone');
            if (select.length) {
                select.empty();
                const fallback = [
                    'UTC', 'America/New_York', 'America/Chicago', 'America/Denver', 
                    'America/Los_Angeles', 'Europe/London', 'Europe/Paris', 
                    'Europe/Berlin', 'Asia/Tokyo', 'Asia/Shanghai'
                ];
                fallback.forEach(tz => {
                    select.append(`<option value="${tz}">${tz}</option>`);
                });
            }
            Monolith.UI.toast('Failed to load timezones from OS, using fallback list', 'warning');
        }
    },

    loadSettings: async function() {
        try {
            const response = await Monolith.API.get('/system/settings');
            const data = response.Data || response.data || {};
            this.settings = {
                hostname: data.Hostname || data.hostname || '',
                domain: data.Domain || data.domain || '',
                timezone: data.Timezone || data.timezone || 'UTC',
                dnsServers: data.DnsServers || data.dnsServers || []
            };
        } catch (error) {
            console.error('Failed to load system settings:', error);
            this.settings = {
                hostname: '',
                domain: '',
                timezone: 'UTC',
                dnsServers: []
            };
            Monolith.UI.toast('Failed to load system settings', 'error');
        }

        $('#hostname').val(this.settings.hostname);
        $('#domain').val(this.settings.domain);
        $('#timezone').val(this.settings.timezone);
        this.renderDnsServers(this.settings.dnsServers);
    },

    saveSettings: async function() {
        const payload = {
            hostname: $('#hostname').val(),
            domain: $('#domain').val(),
            timezone: $('#timezone').val(),
            dnsServers: this.getDnsServers()
        };

        try {
            const response = await Monolith.API.post('/system/settings', payload);
            if (!(response.Success || response.success)) {
                throw new Error(response.Error || response.error || 'Failed to save settings');
            }

            Monolith.UI.toast('System settings saved successfully', 'success');
            await this.loadSettings();
        } catch (error) {
            console.error('Failed to save system settings:', error);
            Monolith.UI.toast('Failed to save system settings', 'error');
        }
    },

    resetSettings: function() {
        if (confirm('Reset all system settings to defaults?')) {
            this.loadSettings();
            Monolith.UI.toast('System settings reset to defaults', 'info');
        }
    },

    renderDnsServers: function(servers) {
        const container = $('#dns-servers-list');
        if (!container.length) return;
        const entries = Array.isArray(servers) ? servers.filter(s => s) : [];
        const initial = entries.length > 0 ? entries : ['', ''];
        const rows = initial.map(value => this.buildDnsRow(value)).join('');
        container.html(rows);
    },

    buildDnsRow: function(value) {
        const safeValue = value ? value.toString() : '';
        return `
            <div class="input-group dns-server-row">
                <input type="text" class="form-control dns-server-input" value="${safeValue}" placeholder="1.1.1.1">
                <button class="btn btn-outline-danger dns-remove-btn" type="button">Remove</button>
            </div>
        `;
    },

    addDnsServer: function() {
        const container = $('#dns-servers-list');
        if (container.length) {
            container.append(this.buildDnsRow(''));
        }
    },

    getDnsServers: function() {
        const values = [];
        $('#dns-servers-list .dns-server-input').each((_, el) => {
            const value = $(el).val();
            if (value) {
                values.push(value.toString().trim());
            }
        });
        return values.filter(value => value.length > 0);
    }
};

if (typeof Monolith !== 'undefined') {
    Monolith.Pages = Monolith.Pages || {};
    Monolith.Pages.SettingsSystem = SettingsSystem;
}