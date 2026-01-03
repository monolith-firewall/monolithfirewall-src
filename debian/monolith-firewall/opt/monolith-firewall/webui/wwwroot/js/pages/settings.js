// General Settings Page
var Settings = {
    settings: {},

    init: function() {
        console.log('Initializing General Settings...');
        this.render();
        this.loadSettings();
    },

    render: function() {
        const container = $('#settings-container');
        container.html(`
            <div class="container-fluid">
                <h1 class="mb-4">General Settings</h1>

                <form id="settings-form">
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
                                            <option value="UTC">UTC</option>
                                            <option value="America/New_York">America/New_York</option>
                                            <option value="America/Chicago">America/Chicago</option>
                                            <option value="America/Los_Angeles">America/Los_Angeles</option>
                                            <option value="Europe/London">Europe/London</option>
                                            <option value="Europe/Paris">Europe/Paris</option>
                                            <option value="Asia/Tokyo">Asia/Tokyo</option>
                                        </select>
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

                            <div class="card mb-4">
                                <div class="card-header">
                                    <h5 class="mb-0">Interface Preferences</h5>
                                </div>
                                <div class="card-body">
                                    <div class="mb-3">
                                        <label for="language" class="form-label">Language</label>
                                        <select class="form-select" id="language">
                                            <option value="en">English</option>
                                            <option value="es">Español</option>
                                            <option value="fr">Français</option>
                                            <option value="de">Deutsch</option>
                                        </select>
                                    </div>

                                    <div class="mb-3">
                                        <label for="theme" class="form-label">Theme</label>
                                        <select class="form-select" id="theme">
                                            <option value="light">Light</option>
                                            <option value="dark">Dark</option>
                                            <option value="auto">Auto (System)</option>
                                        </select>
                                    </div>

                                    <div class="form-check mb-3">
                                        <input class="form-check-input" type="checkbox" id="show-tooltips">
                                        <label class="form-check-label" for="show-tooltips">
                                            Show helpful tooltips
                                        </label>
                                    </div>

                                    <div class="form-check">
                                        <input class="form-check-input" type="checkbox" id="confirm-actions">
                                        <label class="form-check-label" for="confirm-actions">
                                            Confirm destructive actions
                                        </label>
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
                                    <h5 class="mb-0">Quick Actions</h5>
                                </div>
                                <div class="card-body">
                                    <div class="d-grid gap-2">
                                        <button type="button" class="btn btn-outline-primary">
                                            <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-1">
                                                <path fill-rule="evenodd" d="M8 3a5 5 0 1 0 4.546 2.914.5.5 0 0 1 .908-.417A6 6 0 1 1 8 2v1z"/>
                                                <path d="M8 4.466V.534a.25.25 0 0 1 .41-.192l2.36 1.966c.12.1.12.284 0 .384L8.41 4.658A.25.25 0 0 1 8 4.466z"/>
                                            </svg>
                                            Restart WebUI
                                        </button>
                                        <button type="button" class="btn btn-outline-warning">
                                            <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-1">
                                                <path fill-rule="evenodd" d="M8 3a5 5 0 1 0 4.546 2.914.5.5 0 0 1 .908-.417A6 6 0 1 1 8 2v1z"/>
                                                <path d="M8 4.466V.534a.25.25 0 0 1 .41-.192l2.36 1.966c.12.1.12.284 0 .384L8.41 4.658A.25.25 0 0 1 8 4.466z"/>
                                            </svg>
                                            Reboot System
                                        </button>
                                        <button type="button" class="btn btn-outline-danger">
                                            <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-1">
                                                <path d="M5.5 5.5A.5.5 0 0 1 6 6v6a.5.5 0 0 1-1 0V6a.5.5 0 0 1 .5-.5zm2.5 0a.5.5 0 0 1 .5.5v6a.5.5 0 0 1-1 0V6a.5.5 0 0 1 .5-.5zm3 .5a.5.5 0 0 0-1 0v6a.5.5 0 0 0 1 0V6z"/>
                                                <path fill-rule="evenodd" d="M14.5 3a1 1 0 0 1-1 1H13v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V4h-.5a1 1 0 0 1-1-1V2a1 1 0 0 1 1-1H6a1 1 0 0 1 1-1h2a1 1 0 0 1 1 1h3.5a1 1 0 0 1 1 1v1zM4.118 4 4 4.059V13a1 1 0 0 0 1 1h6a1 1 0 0 0 1-1V4.059L11.882 4H4.118zM2.5 3V2h11v1h-11z"/>
                                            </svg>
                                            Shutdown
                                        </button>
                                    </div>
                                </div>
                            </div>

                            <div class="card mt-3">
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
            </div>
        `);

        $('#settings-form').on('submit', (e) => {
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

    loadSettings: async function() {
        try {
            const response = await Monolith.API.get('/system/settings');
            const data = response.Data || response.data || {};
            this.settings = {
                hostname: data.Hostname || data.hostname || '',
                domain: data.Domain || data.domain || '',
                timezone: data.Timezone || data.timezone || 'UTC',
                dnsServers: data.DnsServers || data.dnsServers || [],
                language: 'en',
                theme: 'light',
                showTooltips: true,
                confirmActions: true
            };
        } catch (error) {
            console.error('Failed to load settings:', error);
            this.settings = {
                hostname: '',
                domain: '',
                timezone: 'UTC',
                dnsServers: [],
                language: 'en',
                theme: 'light',
                showTooltips: true,
                confirmActions: true
            };
            Monolith.UI.toast('Failed to load system settings', 'error');
        }

        $('#hostname').val(this.settings.hostname);
        $('#domain').val(this.settings.domain);
        $('#timezone').val(this.settings.timezone);
        $('#language').val(this.settings.language);
        $('#theme').val(this.settings.theme);
        $('#show-tooltips').prop('checked', this.settings.showTooltips);
        $('#confirm-actions').prop('checked', this.settings.confirmActions);
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

            Monolith.UI.toast('Settings saved successfully', 'success');
            await this.loadSettings();
        } catch (error) {
            console.error('Failed to save settings:', error);
            Monolith.UI.toast('Failed to save settings', 'error');
        }
    },

    resetSettings: function() {
        if (confirm('Reset all settings to defaults?')) {
            this.loadSettings();
            Monolith.UI.toast('Settings reset to defaults', 'info');
        }
    },

    renderDnsServers: function(servers) {
        const container = $('#dns-servers-list');
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
        container.append(this.buildDnsRow(''));
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
    Monolith.Pages.Settings = Settings;
}
