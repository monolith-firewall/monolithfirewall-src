// Update Manager Page
var Updates = {
    init: function() {
        console.log('Initializing Update Manager...');
        this.render();
        this.checkUpdates();
    },

    getCategory: function(pkg) {
        const c = (pkg && (pkg.category || pkg.Category)) ? (pkg.category || pkg.Category) : '';
        const category = (c || '').toString().trim();
        return category ? category : 'Other';
    },

    compareVersions: function(a, b) {
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
        const container = $('#updates-container');
        
        // Render page header
        if (Monolith.PageHeader && typeof Monolith.PageHeader.render === 'function') {
            Monolith.PageHeader.render({
                title: "Update Manager",
                icon: "fa-arrows-rotate",
                description: "Check for system and package updates",
                container: container,
                prepend: true
            });
        }

        container.append(`
            <div class="container-fluid p-4">
                <div class="row mb-4">
                    <div class="col-md-12">
                        <button class="btn btn-primary" id="check-updates-btn">
                            <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-1">
                                <path fill-rule="evenodd" d="M8 3a5 5 0 1 0 4.546 2.914.5.5 0 0 1 .908-.417A6 6 0 1 1 8 2v1z"/>
                                <path d="M8 4.466V.534a.25.25 0 0 1 .41-.192l2.36 1.966c.12.1.12.284 0 .384L8.41 4.658A.25.25 0 0 1 8 4.466z"/>
                            </svg>
                            Check for Updates
                        </button>
                    </div>
                </div>

                <div class="row g-4">
                    <div class="col-md-6">
                        <div class="card">
                            <div class="card-header">
                                <h5 class="mb-0">System Updates</h5>
                            </div>
                            <div class="card-body" id="system-updates">
                                <div class="text-center py-3">
                                    <div class="spinner-border text-primary" role="status">
                                        <span class="visually-hidden">Loading...</span>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>

                    <div class="col-md-6">
                        <div class="card">
                            <div class="card-header">
                                <h5 class="mb-0">Package Updates</h5>
                            </div>
                            <div class="card-body" id="package-updates">
                                <div class="text-center py-3">
                                    <div class="spinner-border text-primary" role="status">
                                        <span class="visually-hidden">Loading...</span>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>

                <div class="row mt-4">
                    <div class="col-12">
                        <div class="card">
                            <div class="card-header">
                                <h5 class="mb-0">Update History</h5>
                            </div>
                            <div class="card-body" id="update-history">
                                <div class="text-center py-3">
                                    <p class="text-muted">No update history available</p>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `);

        // Use .off() first to prevent duplicate handlers on re-init
        $('#check-updates-btn').off('click').on('click', () => this.checkUpdates());
    },

    checkUpdates: async function() {
        // System updates
        $('#system-updates').html(`
            <div class="alert alert-success">
                <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-2">
                    <path d="M16 8A8 8 0 1 1 0 8a8 8 0 0 1 16 0zm-3.97-3.03a.75.75 0 0 0-1.08.022L7.477 9.417 5.384 7.323a.75.75 0 0 0-1.06 1.06L6.97 11.03a.75.75 0 0 0 1.079-.02l3.992-4.99a.75.75 0 0 0-.01-1.05z"/>
                </svg>
                <strong>Monolith FireWall Core</strong><br>
                <small>Version 1.0.0 - Up to date</small>
            </div>
            <p class="text-muted small mb-0">Last checked: ${new Date().toLocaleString()}</p>
        `);

        // Package updates (real)
        const packageContainer = $('#package-updates');
        packageContainer.html(`
            <div class="text-center py-3">
                <div class="spinner-border text-primary" role="status">
                    <span class="visually-hidden">Loading...</span>
                </div>
            </div>
        `);

        try {
            const [installedResp, availableResp] = await Promise.all([
                Monolith.API.get('/core?action=get-packages'),
                Monolith.API.get('/packages/available?version=1.0.0')
            ]);

            const installed = (installedResp.Success || installedResp.success) ? (installedResp.Data || installedResp.data || []) : [];
            const available = (availableResp.Success || availableResp.success) ? ((availableResp.Data || availableResp.data || {}).packages || []) : [];

            const availableById = {};
            available.forEach(p => {
                availableById[(p.id || p.Id || '').toString().trim().toLowerCase()] = p;
            });

            const updates = [];
            installed.forEach(pkg => {
                const id = (pkg.id || pkg.Id || '').toString().trim().toLowerCase();
                const avail = availableById[id];
                if (!avail) return;
                const installedVersion = pkg.version || pkg.Version || '';
                const availableVersion = avail.version || avail.Version || '';
                if (availableVersion && this.compareVersions(availableVersion, installedVersion) > 0) {
                    updates.push({ pkg, avail });
                }
            });

            if (updates.length === 0) {
                packageContainer.html(`
                    <div class="alert alert-success mb-2">
                        <strong>All packages are up to date</strong><br>
                        <small>${installed.length} package(s) checked</small>
                    </div>
                    <p class="text-muted small mb-0">Last checked: ${new Date().toLocaleString()}</p>
                `);
                return;
            }

            // Group updates by category
            const groups = {};
            updates.forEach(entry => {
                const category = this.getCategory(entry.avail);
                if (!groups[category]) groups[category] = [];
                groups[category].push(entry);
            });
            const preferredOrder = ['Network', 'VPN', 'Diagnostics', 'System', 'Other'];
            const categories = Object.keys(groups).sort((a, b) => {
                const ai = preferredOrder.indexOf(a);
                const bi = preferredOrder.indexOf(b);
                if (ai !== -1 || bi !== -1) {
                    return (ai === -1 ? 999 : ai) - (bi === -1 ? 999 : bi);
                }
                return a.localeCompare(b);
            });

            let html = `
                <div class="alert alert-warning text-dark mb-3">
                    <strong>${updates.length} package update(s) available</strong><br>
                    <small>${installed.length} installed package(s) checked</small>
                </div>
            `;

            categories.forEach(category => {
                html += `
                    <div class="mb-2 d-flex justify-content-between align-items-center">
                        <div class="fw-semibold">${category}</div>
                        <div class="text-muted small">${groups[category].length} update(s)</div>
                    </div>
                    <div class="list-group mb-3">
                `;

                groups[category].forEach(entry => {
                    const name = entry.pkg.name || entry.pkg.Name || entry.avail.name || entry.avail.Name || entry.pkg.id || entry.pkg.Id;
                    const fromV = entry.pkg.version || entry.pkg.Version || 'n/a';
                    const toV = entry.avail.version || entry.avail.Version || 'n/a';
                    html += `
                        <div class="list-group-item d-flex justify-content-between align-items-center">
                            <div>
                                <div class="fw-semibold">${name}</div>
                                <div class="text-muted small">${fromV} → ${toV}</div>
                            </div>
                            <a class="btn btn-sm btn-outline-primary" href="/system/packages" data-route="/system/packages">Update</a>
                        </div>
                    `;
                });

                html += `</div>`;
            });

            packageContainer.html(html);
        } catch (err) {
            console.error('Failed to check package updates:', err);
            packageContainer.html(`<div class="alert alert-danger">Failed to check package updates</div>`);
        }
    }
};

if (typeof Monolith !== 'undefined') {
    Monolith.Pages = Monolith.Pages || {};
    Monolith.Pages.Updates = Updates;
}
