// Package Manager Page
var Packages = {
    installed: [],
    available: [],
    activity: [],
    fetchedAt: null,
    activeTab: 'installed',

    init: function() {
        console.log('Initializing Package Manager...');
        this.render();
        this.loadInstalled();
        this.loadAvailable();
        this.loadActivity();
    },

    normalizeId: function(value) {
        return (value || '').toString().trim().toLowerCase();
    },

    getCategory: function(pkg) {
        const c = (pkg && (pkg.category || pkg.Category)) ? (pkg.category || pkg.Category) : '';
        const category = (c || '').toString().trim();
        return category ? category : 'Other';
    },

    compareVersions: function(a, b) {
        // Simple numeric dotted version compare (e.g. 1.2.10 > 1.2.3).
        // Falls back to string compare if parsing fails.
        const as = (a || '').toString().trim();
        const bs = (b || '').toString().trim();
        if (!as && !bs) return 0;
        if (!as) return -1;
        if (!bs) return 1;

        const ap = as.split('.').map(x => parseInt(x, 10));
        const bp = bs.split('.').map(x => parseInt(x, 10));
        if (ap.some(Number.isNaN) || bp.some(Number.isNaN)) {
            return as.localeCompare(bs);
        }

        const len = Math.max(ap.length, bp.length);
        for (let i = 0; i < len; i++) {
            const av = ap[i] || 0;
            const bv = bp[i] || 0;
            if (av > bv) return 1;
            if (av < bv) return -1;
        }
        return 0;
    },

    render: function() {
        const container = $('#packages-container');
        
        // Render page header
        if (Monolith.PageHeader && typeof Monolith.PageHeader.render === 'function') {
            Monolith.PageHeader.render({
                title: "Package Manager",
                icon: "fa-box-open",
                description: "Install, update, and manage Monolith packages",
                container: container,
                prepend: true
            });
        }

        // Add toolbar with search and refresh under header
        container.append(`
            <div class="container-fluid p-4 packages-shell">
                <div class="packages-toolbar d-flex justify-content-between align-items-center mb-3">
                    <div class="packages-actions">
                        <button class="btn btn-outline-primary" id="packages-refresh">
                            <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-1">
                                <path fill-rule="evenodd" d="M8 3a5 5 0 1 0 4.546 2.914.5.5 0 0 1 .908-.417A6 6 0 1 1 8 2v1z"/>
                                <path d="M8 4.466V.534a.25.25 0 0 1 .41-.192l2.36 1.966c.12.1.12.284 0 .384L8.41 4.658A.25.25 0 0 1 8 4.466z"/>
                            </svg>
                            Refresh
                        </button>
                    </div>
                </div>

                <div class="packages-toolbar">
                    <div class="input-group">
                        <span class="input-group-text">
                            <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16">
                                <path d="M11.742 10.344a6.5 6.5 0 1 0-1.397 1.398h-.001c.03.04.062.078.098.115l3.85 3.85a1 1 0 0 0 1.415-1.414l-3.85-3.85a1.007 1.007 0 0 0-.115-.1zM12 6.5a5.5 5.5 0 1 1-11 0 5.5 5.5 0 0 1 11 0z"/>
                            </svg>
                        </span>
                        <input type="text" class="form-control" id="packages-search" placeholder="Search packages...">
                    </div>
                    <div class="packages-meta text-muted small" id="packages-meta">Syncing packages...</div>
                </div>

                <ul class="nav nav-tabs packages-tabs" role="tablist">
                    <li class="nav-item" role="presentation">
                        <button class="nav-link active" id="packages-installed-tab" data-bs-toggle="tab" data-bs-target="#packages-installed" type="button" role="tab">
                            Installed
                        </button>
                    </li>
                    <li class="nav-item" role="presentation">
                        <button class="nav-link" id="packages-available-tab" data-bs-toggle="tab" data-bs-target="#packages-available" type="button" role="tab">
                            Available
                        </button>
                    </li>
                    <li class="nav-item" role="presentation">
                        <button class="nav-link" id="packages-activity-tab" data-bs-toggle="tab" data-bs-target="#packages-activity" type="button" role="tab">
                            Activity
                        </button>
                    </li>
                </ul>

                <div class="tab-content packages-content">
                    <div class="tab-pane fade show active" id="packages-installed" role="tabpanel">
                        <div id="packages-installed-list" class="packages-grid"></div>
                    </div>
                    <div class="tab-pane fade" id="packages-available" role="tabpanel">
                        <div id="packages-available-list" class="packages-grid"></div>
                    </div>
                    <div class="tab-pane fade" id="packages-activity" role="tabpanel">
                        <div class="card">
                            <div class="card-header d-flex justify-content-between align-items-center">
                                <span>Package Activity</span>
                                <span class="text-muted small" id="packages-activity-count">0 events</span>
                            </div>
                            <div class="table-responsive">
                                <table class="table table-hover align-middle mb-0">
                                    <thead>
                                        <tr>
                                            <th>Timestamp</th>
                                            <th>Action</th>
                                            <th>Package</th>
                                            <th>Status</th>
                                            <th>Details</th>
                                        </tr>
                                    </thead>
                                    <tbody id="packages-activity-body">
                                        <tr>
                                            <td colspan="5" class="text-center text-muted py-4">Loading activity...</td>
                                        </tr>
                                    </tbody>
                                </table>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `);

        // Use .off() first to prevent duplicate handlers on re-init
        $('#packages-refresh').off('click').on('click', () => {
            this.loadInstalled();
            this.loadAvailable();
            this.loadActivity();
        });
        $('#packages-search').off('input').on('input', () => this.renderInstalled());
    },

    loadInstalled: async function() {
        try {
            const response = await Monolith.API.get('/core?action=get-packages');
            if (response.Success || response.success) {
                this.installed = response.Data || response.data || [];
                this.renderInstalled();
            } else {
                this.renderInstalledError('Failed to load installed packages');
            }
        } catch (error) {
            console.error('Error loading packages:', error);
            this.renderInstalledError('Failed to load installed packages');
        }
    },

    loadAvailable: async function() {
        try {
            const response = await Monolith.API.get('/packages/available?version=1.0.0');
            if (response.Success || response.success) {
                const data = response.Data || response.data || {};
                this.available = data.packages || [];
                this.fetchedAt = data.fetchedAtUtc || null;
                this.renderAvailable();
                this.updateMeta();
                return;
            }

            this.renderAvailableError('Failed to load updates feed');
        } catch (error) {
            console.error('Error loading available packages:', error);
            this.renderAvailableError('Failed to load updates feed');
        }
    },

    loadActivity: async function() {
        try {
            const response = await Monolith.API.get('/logs/monolith?category=Package&limit=50');
            if (response.Success || response.success) {
                const data = response.Data || response.data || {};
                this.activity = data.Logs || data.logs || [];
                this.renderActivity();
                return;
            }

            this.renderActivityError('Failed to load activity');
        } catch (error) {
            console.error('Error loading activity:', error);
            this.renderActivityError('Failed to load activity');
        }
    },

    renderInstalled: function() {
        const query = ($('#packages-search').val() || '').toString().toLowerCase();
        const list = $('#packages-installed-list');
        const filtered = (this.installed || []).filter(pkg => {
            const name = (pkg.name || pkg.Name || '').toLowerCase();
            return !query || name.includes(query);
        });

        if (filtered.length === 0) {
            list.html('<div class="alert alert-info">No installed packages found</div>');
            return;
        }

        const availableById = {};
        (this.available || []).forEach(p => {
            availableById[this.normalizeId(p.id || p.Id)] = p;
        });

        const updates = [];
        const installed = [];
        filtered.forEach(pkg => {
            const pkgId = this.normalizeId(pkg.id || pkg.Id);
            const avail = availableById[pkgId];
            const installedVersion = pkg.version || pkg.Version || '';
            const availableVersion = avail ? (avail.version || avail.Version || '') : '';
            const hasUpdate = avail && availableVersion && this.compareVersions(availableVersion, installedVersion) > 0;
            if (hasUpdate) updates.push({ pkg, avail });
            else installed.push({ pkg, avail });
        });

        let html = '';
        const renderCard = (pkg, available, badgeHtml) => {
            const moduleCount = (pkg.modules || []).length;
            const installMeta = pkg.installedAt
                ? `Installed ${Monolith.UI.formatDate(pkg.installedAt)}`
                : 'Installed';
            const pkgIdRaw = pkg.id || pkg.Id;
            const pkgId = this.normalizeId(pkgIdRaw);
            const pkgName = (pkg.name || pkg.Name || '').replace(/'/g, "\\'");
            const currentVersion = (pkg.version || pkg.Version || 'n/a').replace(/'/g, "\\'");
            const newVersion = available ? ((available.version || available.Version || 'n/a').replace(/'/g, "\\'")) : 'n/a';
            const hasUpdate = available && available.downloadUrl && this.compareVersions(newVersion, currentVersion) > 0;
            return `
                <div class="card package-card">
                    <div class="card-body">
                        <div class="d-flex justify-content-between align-items-start mb-2">
                            <div>
                                <h5 class="card-title mb-1">${pkg.name || pkg.Name}</h5>
                                <div class="text-muted small">${pkg.description || pkg.Description || 'No description'}</div>
                            </div>
                            ${badgeHtml}
                        </div>
                        <div class="package-meta">
                            <div><strong>Version:</strong> ${pkg.version || pkg.Version || 'n/a'}</div>
                            <div><strong>Modules:</strong> ${moduleCount}</div>
                            <div><strong>Status:</strong> ${installMeta}</div>
                        </div>
                        ${this.renderPermissionSummary(pkg)}
                        <div class="package-actions mt-3">
                            ${hasUpdate
                                ? `<button class="btn btn-sm btn-primary" onclick="Packages.update('${available.id || available.Id}', '${available.downloadUrl}', '${pkgName}', '${currentVersion}', '${newVersion}')">Update</button>`
                                : ''}
                            <button class="btn btn-sm btn-outline-primary" onclick="Packages.showDetails('${pkgIdRaw}')">
                                Details
                            </button>
                            <button class="btn btn-sm btn-outline-warning" ${available ? '' : 'disabled'} onclick="Packages.forceReinstall('${pkgId}')">
                                Force reinstall
                            </button>
                            <button class="btn btn-sm btn-outline-danger" onclick="Packages.uninstall('${pkgIdRaw}')">
                                Uninstall
                            </button>
                        </div>
                    </div>
                </div>
            `;
        };

        html += `
            <div class="w-100 mb-2 d-flex justify-content-between align-items-center">
                <div class="fw-semibold">Updates <span class="badge bg-warning text-dark ms-1">${updates.length}</span></div>
                <div class="text-muted small">Packages with a newer version available</div>
            </div>
        `;
        if (updates.length === 0) {
            html += `<div class="alert alert-light border w-100">No package updates available.</div>`;
        } else {
            updates.forEach(entry => {
                const installedVersion = entry.pkg.version || entry.pkg.Version || 'n/a';
                const availableVersion = entry.avail.version || entry.avail.Version || 'n/a';
                const badge = `<span class="badge bg-warning text-dark">Update: ${installedVersion} → ${availableVersion}</span>`;
                html += renderCard(entry.pkg, entry.avail, badge);
            });
        }

        html += `
            <div class="w-100 mt-4 mb-2 d-flex justify-content-between align-items-center">
                <div class="fw-semibold">Installed</div>
                <div class="text-muted small">${installed.length} package(s)</div>
            </div>
        `;
        installed.forEach(entry => {
            const badge = `<span class="badge bg-success">Installed</span>`;
            html += renderCard(entry.pkg, entry.avail, badge);
        });

        list.html(html);
    },

    renderInstalledError: function(message) {
        $('#packages-installed-list').html(`<div class="alert alert-danger">${message}</div>`);
    },

    renderAvailable: function() {
        const list = $('#packages-available-list');
        if (!this.available || this.available.length === 0) {
            list.html('<div class="alert alert-info">No packages available</div>');
            return;
        }

        const installedIds = {};
        (this.installed || []).forEach(pkg => {
            installedIds[this.normalizeId(pkg.id || pkg.Id)] = true;
        });

        const groups = {};
        (this.available || []).forEach(pkg => {
            // Hide already-installed packages from the Available tab.
            if (installedIds[this.normalizeId(pkg.id || pkg.Id)]) {
                return;
            }

            const category = this.getCategory(pkg);
            if (!groups[category]) groups[category] = [];
            groups[category].push(pkg);
        });

        const totalAvailable = Object.values(groups).reduce((sum, items) => sum + items.length, 0);
        if (totalAvailable === 0) {
            list.html('<div class="alert alert-info">No packages available</div>');
            return;
        }

        const preferredOrder = ['Network', 'VPN', 'Diagnostics', 'System', 'Other'];
        const categories = Object.keys(groups).sort((a, b) => {
            const ai = preferredOrder.indexOf(a);
            const bi = preferredOrder.indexOf(b);
            if (ai !== -1 || bi !== -1) {
                return (ai === -1 ? 999 : ai) - (bi === -1 ? 999 : bi);
            }
            return a.localeCompare(b);
        });

        let html = '';
        categories.forEach(category => {
            html += `
                <div class="w-100 mt-1 mb-2 d-flex justify-content-between align-items-center">
                    <div class="fw-semibold">${category}</div>
                    <div class="text-muted small">${groups[category].length} package(s)</div>
                </div>
            `;

            groups[category].forEach(pkg => {
                const status = 'Available';
                const badge = '<span class="badge bg-primary">Available</span>';
                const action = `<button class="btn btn-sm btn-primary" onclick="Packages.install('${pkg.id}', '${pkg.downloadUrl}')">Install</button>`;

                html += `
                    <div class="card package-card">
                        <div class="card-body">
                            <div class="d-flex justify-content-between align-items-start mb-2">
                                <div>
                                    <h5 class="card-title mb-1">${pkg.name}</h5>
                                    <div class="text-muted small">${pkg.description || 'No description'}</div>
                                </div>
                                ${badge}
                            </div>
                            <div class="package-meta">
                                <div><strong>Version:</strong> ${pkg.version || 'n/a'}</div>
                                <div><strong>Status:</strong> ${status}</div>
                            </div>
                            <div class="package-actions mt-3">
                                ${action}
                                <button class="btn btn-sm btn-outline-primary" onclick="Packages.showAvailableDetails('${pkg.id}')">
                                    Details
                                </button>
                            </div>
                        </div>
                    </div>
                `;
            });
        });

        list.html(html);
    },

    update: function(packageId, downloadUrl, packageName, currentVersion, newVersion) {
        const pkg = this.available.find(p => (p.id || p.Id) === packageId);
        if (!pkg) {
            Monolith.UI.toast('Package not found in updates feed', 'error');
            return;
        }

        const modalBody = `
            <div class="install-summary">
                <h5 class="mb-2">${packageName}</h5>
                <div class="text-muted small mb-3">${pkg.description || 'No description'}</div>
                <div class="package-meta">
                    <div><strong>Current Version:</strong> ${currentVersion}</div>
                    <div><strong>New Version:</strong> ${newVersion}</div>
                    <div><strong>Source:</strong> updates.monolithfirewall.com</div>
                </div>
                <div class="install-status mt-3 text-muted">Ready to update.</div>
            </div>
        `;

        const footer = `
            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
            <button type="button" class="btn btn-primary" id="update-confirm-btn">Update</button>
        `;

        const modal = Monolith.UI.showModal('Update Package', modalBody, {
            size: 'lg',
            footerHtml: footer,
            staticBackdrop: true
        });

        modal.element.find('#update-confirm-btn').on('click', async () => {
            const statusEl = modal.element.find('.install-status');
            const btnEl = modal.element.find('#update-confirm-btn');
            statusEl.html('<div class="spinner-border spinner-border-sm me-2" role="status"></div>Downloading and updating...');
            btnEl.prop('disabled', true);

            try {
                const response = await Monolith.API.post('/packages/install', {
                    packageId: pkg.id,
                    downloadUrl: downloadUrl,
                    sha256: pkg.sha256 || pkg.Sha256 || null,
                    overwrite: true
                });

                if (!response) {
                    throw new Error('No response from server');
                }

                const success = response.Success || response.success;
                const error = response.Error || response.error;
                
                if (!success) {
                    const errorMsg = error || 'Update failed';
                    throw new Error(errorMsg);
                }

                const data = response.Data || response.data || {};
                const requiresRestart = data.requiresRestart || false;
                
                if (requiresRestart) {
                    statusEl.html('<div class="text-warning"><strong>✓ Updated successfully.</strong><br><small>Services will restart automatically in a few seconds.</small></div>');
                    Monolith.UI.toast('Package updated - services restarting', 'success');
                } else {
                    statusEl.html('<div class="text-success"><strong>✓ Updated successfully.</strong></div>');
                    Monolith.UI.toast('Package updated successfully', 'success');
                }
                
                this.loadInstalled();
                this.loadAvailable();
                this.loadActivity();
                setTimeout(() => modal.instance.hide(), 2000);
            } catch (error) {
                console.error('Update failed:', error);
                const errorMsg = error.message || error.toString() || 'Update failed';
                statusEl.html(`<div class="text-danger"><strong>✗ Update failed</strong><br><small>${errorMsg}</small></div>`);
                Monolith.UI.toast(`Update failed: ${errorMsg}`, 'error');
                btnEl.prop('disabled', false);
            }
        });
    },

    forceReinstall: function(packageId) {
        const pkg = (this.available || []).find(p => (p.id || p.Id) === packageId);
        if (!pkg || !pkg.downloadUrl) {
            Monolith.UI.toast('No download URL available for this package (refresh updates feed)', 'error');
            return;
        }

        this.install(pkg.id || pkg.Id, pkg.downloadUrl);
    },

    renderAvailableError: function(message) {
        $('#packages-available-list').html(`<div class="alert alert-danger">${message}</div>`);
    },

    renderActivity: function() {
        const tbody = $('#packages-activity-body');
        const count = $('#packages-activity-count');
        count.text(`${this.activity.length} events`);

        if (!this.activity || this.activity.length === 0) {
            tbody.html('<tr><td colspan="5" class="text-center text-muted py-4">No activity recorded</td></tr>');
            return;
        }

        let html = '';
        this.activity.forEach(entry => {
            const level = (entry.Level || entry.level || '').toLowerCase();
            const statusBadge = level === 'error'
                ? '<span class="badge bg-danger">Error</span>'
                : level === 'warning'
                    ? '<span class="badge bg-warning text-dark">Warning</span>'
                    : '<span class="badge bg-success">Info</span>';

            html += `
                <tr>
                    <td>${Monolith.UI.formatDate(entry.Timestamp || entry.timestamp)}</td>
                    <td>${entry.Message || entry.message}</td>
                    <td>${this.extractPackageName(entry)}</td>
                    <td>${statusBadge}</td>
                    <td class="text-muted small">${this.formatDetails(entry.Details || entry.details)}</td>
                </tr>
            `;
        });

        tbody.html(html);
    },

    renderActivityError: function(message) {
        $('#packages-activity-body').html(`<tr><td colspan="5" class="text-center text-danger py-4">${message}</td></tr>`);
    },

    showDetails: function(packageId) {
        const pkg = this.installed.find(p => (p.id || p.Id) === packageId);
        if (!pkg) {
            return;
        }

        const modulesHtml = (pkg.modules || []).map(module => `
            <div class="permission-row">
                <div class="fw-semibold">${module.name}</div>
                <div class="text-muted small">${module.id}</div>
                <div class="permission-meta">${this.formatPermissionList(module.systemPermissions || [])}</div>
            </div>
        `).join('');

        const body = `
            <div class="package-detail-grid">
                <div>
                    <div class="text-muted small">Package ID</div>
                    <div class="fw-semibold">${pkg.id}</div>
                </div>
                <div>
                    <div class="text-muted small">Version</div>
                    <div class="fw-semibold">${pkg.version}</div>
                </div>
                <div>
                    <div class="text-muted small">Author</div>
                    <div class="fw-semibold">${pkg.author || 'Unknown'}</div>
                </div>
                <div>
                    <div class="text-muted small">Installed</div>
                    <div class="fw-semibold">${pkg.installedAt ? Monolith.UI.formatDate(pkg.installedAt) : 'Unknown'}</div>
                </div>
            </div>
            <div class="mt-3">
                <div class="text-muted small mb-2">Module Permissions</div>
                <div class="permission-list">${modulesHtml || '<span class="text-muted">No modules</span>'}</div>
            </div>
        `;

        Monolith.UI.showModal(`Package Details`, body, { size: 'lg' });
    },

    showAvailableDetails: function(packageId) {
        const pkg = this.available.find(p => p.id === packageId);
        if (!pkg) {
            return;
        }

        const body = `
            <div class="package-detail-grid">
                <div>
                    <div class="text-muted small">Package ID</div>
                    <div class="fw-semibold">${pkg.id}</div>
                </div>
                <div>
                    <div class="text-muted small">Version</div>
                    <div class="fw-semibold">${pkg.version || 'n/a'}</div>
                </div>
                <div>
                    <div class="text-muted small">Author</div>
                    <div class="fw-semibold">${pkg.author || 'Unknown'}</div>
                </div>
                <div>
                    <div class="text-muted small">Homepage</div>
                    <div class="fw-semibold">${pkg.homepage || 'n/a'}</div>
                </div>
            </div>
            <div class="mt-3">
                <div class="text-muted small mb-2">Release Notes</div>
                <div class="text-muted">${pkg.releaseNotes || 'No release notes provided.'}</div>
            </div>
        `;

        Monolith.UI.showModal(`Package Details`, body, { size: 'lg' });
    },

    install: function(packageId, downloadUrl) {
        const pkg = this.available.find(p => p.id === packageId);
        if (!pkg) {
            Monolith.UI.toast('Package not found', 'error');
            return;
        }

        const modalBody = `
            <div class="install-summary">
                <h5 class="mb-2">${pkg.name}</h5>
                <div class="text-muted small mb-3">${pkg.description || 'No description'}</div>
                <div class="package-meta">
                    <div><strong>Version:</strong> ${pkg.version}</div>
                    <div><strong>Source:</strong> updates.monolithfirewall.com</div>
                </div>
                <div class="install-status mt-3 text-muted">Ready to install.</div>
            </div>
        `;

        const footer = `
            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
            <button type="button" class="btn btn-primary" id="install-confirm-btn">Install</button>
        `;

        const modal = Monolith.UI.showModal('Install Package', modalBody, {
            size: 'lg',
            footerHtml: footer,
            staticBackdrop: true
        });

        modal.element.find('#install-confirm-btn').on('click', async () => {
            const statusEl = modal.element.find('.install-status');
            const btnEl = modal.element.find('#install-confirm-btn');
            statusEl.html('<div class="spinner-border spinner-border-sm me-2" role="status"></div>Downloading and installing...');
            btnEl.prop('disabled', true);

            try {
                const response = await Monolith.API.post('/packages/install', {
                    packageId: pkg.id,
                    downloadUrl: downloadUrl,
                    sha256: pkg.sha256 || pkg.Sha256 || null,
                    overwrite: true
                });

                if (!response) {
                    throw new Error('No response from server');
                }

                const success = response.Success || response.success;
                const error = response.Error || response.error;
                
                if (!success) {
                    const errorMsg = error || 'Install failed';
                    throw new Error(errorMsg);
                }

                const data = response.Data || response.data || {};
                const requiresRestart = data.requiresRestart || false;
                const isUpdate = data.isUpdate || false;
                
                if (requiresRestart) {
                    statusEl.html('<div class="text-warning"><strong>✓ Installed successfully.</strong><br><small>Services will restart automatically in a few seconds.</small></div>');
                    Monolith.UI.toast(isUpdate ? 'Package updated - services restarting' : 'Package installed - services restarting', 'success');
                } else {
                    statusEl.html('<div class="text-success"><strong>✓ Installed successfully.</strong></div>');
                    Monolith.UI.toast(isUpdate ? 'Package updated successfully' : 'Package installed successfully', 'success');
                }
                
                this.loadInstalled();
                this.loadAvailable();
                this.loadActivity();
                setTimeout(() => modal.instance.hide(), 2000);
            } catch (error) {
                console.error('Install failed:', error);
                const errorMsg = error.message || error.toString() || 'Installation failed';
                statusEl.html(`<div class="text-danger"><strong>✗ Installation failed</strong><br><small>${errorMsg}</small></div>`);
                Monolith.UI.toast(`Install failed: ${errorMsg}`, 'error');
                btnEl.prop('disabled', false);
            }
        });
    },

    uninstall: function(packageId) {
        const pkg = this.installed.find(p => (p.id || p.Id) === packageId);
        if (!pkg) {
            return;
        }

        const body = `
            <div class="install-summary">
                <h5 class="mb-2">${pkg.name}</h5>
                <div class="text-muted small mb-3">Removing this package will disable all modules immediately.</div>
                <div class="package-meta">
                    <div><strong>Version:</strong> ${pkg.version}</div>
                    <div><strong>Modules:</strong> ${(pkg.modules || []).length}</div>
                </div>
                <div class="install-status mt-3 text-muted">Ready to uninstall.</div>
            </div>
        `;

        const footer = `
            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
            <button type="button" class="btn btn-danger" id="uninstall-confirm-btn">Uninstall</button>
        `;

        const modal = Monolith.UI.showModal('Uninstall Package', body, {
            size: 'lg',
            footerHtml: footer,
            staticBackdrop: true
        });

        modal.element.find('#uninstall-confirm-btn').on('click', async () => {
            const statusEl = modal.element.find('.install-status');
            statusEl.text('Removing package...');
            modal.element.find('#uninstall-confirm-btn').prop('disabled', true);

            try {
                const response = await Monolith.API.post('/packages/uninstall', { packageId: pkg.id });
                if (!(response.Success || response.success)) {
                    throw new Error(response.Error || response.error || 'Uninstall failed');
                }

                statusEl.text('Package removed.');
                Monolith.UI.toast('Package removed', 'success');
                this.loadInstalled();
                this.loadAvailable();
                this.loadActivity();
                setTimeout(() => modal.instance.hide(), 1500);
            } catch (error) {
                console.error('Uninstall failed:', error);
                statusEl.text('Uninstall failed. Check logs.');
                Monolith.UI.toast('Uninstall failed', 'error');
                modal.element.find('#uninstall-confirm-btn').prop('disabled', false);
            }
        });
    },

    renderPermissionSummary: function(pkg) {
        const permissions = [];
        (pkg.modules || []).forEach(module => {
            (module.systemPermissions || []).forEach(permission => {
                const label = `${permission.type}:${permission.resource}`;
                if (!permissions.includes(label)) {
                    permissions.push(label);
                }
            });
        });

        if (permissions.length === 0) {
            return '<div class="package-permissions text-muted">System access: none</div>';
        }

        const tags = permissions.slice(0, 3).map(item => `<span class="badge bg-light text-dark border">${item}</span>`);
        const extra = permissions.length > 3 ? `<span class="text-muted small">+${permissions.length - 3} more</span>` : '';
        return `<div class="package-permissions">System access: ${tags.join(' ')} ${extra}</div>`;
    },

    formatPermissionList: function(systemPermissions) {
        if (!systemPermissions || systemPermissions.length === 0) {
            return '<span class="text-muted">No system access</span>';
        }

        return systemPermissions.map(p => `${p.type} ${p.resource}`).join(', ');
    },

    formatDetails: function(details) {
        if (!details) {
            return '-';
        }

        if (typeof details === 'string') {
            return details;
        }

        if (details.packageId) {
            return details.packageId;
        }

        return Object.values(details).slice(0, 2).join(' ');
    },

    extractPackageName: function(entry) {
        const details = entry.Details || entry.details;
        if (details && details.packageId) {
            return details.packageId;
        }
        return '-';
    },

    updateMeta: function() {
        if (!this.fetchedAt) {
            $('#packages-meta').text('Updates feed unavailable');
            return;
        }

        $('#packages-meta').text(`Updates refreshed ${Monolith.UI.formatDate(this.fetchedAt)}`);
    }
};

if (typeof Monolith !== 'undefined') {
    Monolith.Pages = Monolith.Pages || {};
    Monolith.Pages.Packages = Packages;
}
