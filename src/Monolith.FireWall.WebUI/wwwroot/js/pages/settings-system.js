// System Settings Tab
var SettingsSystem = {
    settings: {},
    hasPendingChanges: false,

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
                <!-- Staged Changes Alert -->
                <div id="staged-changes-alert" class="alert alert-warning d-none mb-4">
                    <div class="d-flex align-items-center justify-content-between">
                        <div>
                            <i class="fa-solid fa-clock me-2"></i>
                            <strong>Changes Staged:</strong> Settings have been saved but not yet applied.
                        </div>
                        <div class="d-flex gap-2">
                            <a href="/system/pending-changes" class="btn btn-warning btn-sm">
                                <i class="fa-solid fa-list me-1"></i>View Changes
                            </a>
                            <button type="button" class="btn btn-success btn-sm" id="apply-staged-btn">
                                <i class="fa-solid fa-check me-1"></i>Apply Now
                            </button>
                        </div>
                    </div>
                </div>

                <div class="row">
                    <div class="col-md-8">
                        <div class="card mb-4">
                            <div class="card-header d-flex justify-content-between align-items-center">
                                <h5 class="mb-0">System Information</h5>
                                <span id="settings-status-badge" class="badge bg-success d-none">Applied</span>
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
                            <button type="submit" class="btn btn-primary">
                                <i class="fa-solid fa-save me-1"></i>Save Settings
                            </button>
                            <button type="button" class="btn btn-outline-secondary" id="reset-settings-btn">
                                <i class="fa-solid fa-undo me-1"></i>Reset
                            </button>
                        </div>
                    </div>

                    <div class="col-md-4">
                        <div class="card mb-3">
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

                        <!-- Quick Actions Card -->
                        <div class="card">
                            <div class="card-header">
                                <h5 class="mb-0">Quick Actions</h5>
                            </div>
                            <div class="card-body">
                                <a href="/system/pending-changes" class="btn btn-outline-primary btn-sm w-100 mb-2">
                                    <i class="fa-solid fa-clock-rotate-left me-1"></i>View Pending Changes
                                </a>
                                <a href="/system/advanced" class="btn btn-outline-secondary btn-sm w-100">
                                    <i class="fa-solid fa-sliders me-1"></i>Advanced Settings
                                </a>
                            </div>
                        </div>
                    </div>
                </div>
            </form>
        `);

        // Use .off() first to prevent duplicate handlers on re-init
        $('#system-settings-form').off('submit').on('submit', (e) => {
            e.preventDefault();
            this.saveSettings();
        });

        $('#reset-settings-btn').off('click').on('click', () => this.resetSettings());
        $('#apply-staged-btn').off('click').on('click', () => this.applyStagedChanges());

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

            const data = response.Data || response.data || {};

            // Check if changes were staged
            if (data.staged) {
                this.hasPendingChanges = true;
                this.showStagedAlert(true);
                Monolith.UI.toast('Settings saved and staged. Apply from Pending Changes to activate.', 'info');

                // Notify the pending changes indicator in navbar
                if (window.Monolith && Monolith.PendingChanges && Monolith.PendingChanges.notifyChange) {
                    Monolith.PendingChanges.notifyChange();
                }
            } else {
                this.hasPendingChanges = false;
                this.showStagedAlert(false);
                Monolith.UI.toast('System settings saved successfully', 'success');
            }

            await this.loadSettings();
        } catch (error) {
            console.error('Failed to save system settings:', error);
            Monolith.UI.toast('Failed to save system settings', 'error');
        }
    },

    applyStagedChanges: async function() {
        try {
            const response = await Monolith.API.post('/api/core', {
                action: 'config.apply-all',
                appliedBy: Monolith.Auth ? Monolith.Auth.getUsername() : 'admin'
            });

            if (response.Success && response.Data) {
                const data = response.Data;
                const appliedCount = data.AppliedCount || data.appliedCount || 0;
                const failedCount = data.FailedCount || data.failedCount || 0;

                if (failedCount > 0) {
                    Monolith.UI.toast(`Applied ${appliedCount} changes, ${failedCount} failed`, 'warning');
                } else {
                    Monolith.UI.toast(`Applied ${appliedCount} changes successfully`, 'success');
                }

                this.hasPendingChanges = false;
                this.showStagedAlert(false);

                // Notify the pending changes indicator
                if (window.Monolith && Monolith.PendingChanges && Monolith.PendingChanges.notifyChange) {
                    Monolith.PendingChanges.notifyChange();
                }

                // Refresh settings to show applied state
                await this.loadSettings();
            } else {
                Monolith.UI.toast(response.Error || 'Failed to apply changes', 'error');
            }
        } catch (error) {
            console.error('Failed to apply staged changes:', error);
            Monolith.UI.toast('Failed to apply staged changes', 'error');
        }
    },

    showStagedAlert: function(show) {
        const alert = $('#staged-changes-alert');
        const badge = $('#settings-status-badge');

        if (show) {
            alert.removeClass('d-none');
            badge.removeClass('bg-success').addClass('bg-warning').text('Staged').removeClass('d-none');
        } else {
            alert.addClass('d-none');
            badge.removeClass('bg-warning').addClass('bg-success').text('Applied').addClass('d-none');
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