/**
 * Permissions Page
 */
var Monolith = window.Monolith || {};
window.Monolith = Monolith;

Monolith.Pages = Monolith.Pages || {};

Monolith.Pages.Permissions = {
    allPermissions: [],
    
    /**
     * Initialize permissions page
     */
    init: async function() {
        await this.loadPermissions();
    },

    /**
     * Load all permissions dynamically from installed packages
     */
    loadPermissions: async function() {
        try {
            const container = $('#permissions-container, #page-content').first();
            if (container.length) {
                Monolith.UI.showLoading(container);
            }
            
            // Load permissions from API
            const response = await Monolith.API.get('/api/permissions');
            if (response.success && response.data && Array.isArray(response.data)) {
                // Normalize permission objects to ensure consistent property names (handle both camelCase and PascalCase)
                this.allPermissions = response.data.map(perm => ({
                    id: perm.id || perm.Id || '',
                    name: perm.name || perm.Name || perm.id || perm.Id || '',
                    category: perm.category || perm.Category || 'Other',
                    subcategory: perm.subcategory || perm.Subcategory || '',
                    packageId: perm.packageId || perm.PackageId || 'core',
                    moduleId: perm.moduleId || perm.ModuleId || '',
                    description: perm.description || perm.Description || ''
                }));
            } else {
                console.warn('No permissions data received from API, using fallback');
                this.allPermissions = this.getCorePermissions();
            }
            
            this.renderPermissions();
        } catch (error) {
            console.error('Error loading permissions:', error);
            // Use fallback on error
            this.allPermissions = this.getCorePermissions();
            this.renderPermissions();
            if (Monolith.UI && Monolith.UI.toast) {
                Monolith.UI.toast('Error loading permissions, showing core permissions only', 'warning');
            }
        }
    },

    /**
     * Get core system permissions (always available)
     */
    getCorePermissions: function() {
        return [
            // Core system permissions
            { id: 'system.users.read', name: 'View Users', category: 'System', subcategory: 'Users' },
            { id: 'system.users.write', name: 'Manage Users', category: 'System', subcategory: 'Users' },
            { id: 'system.users.delete', name: 'Delete Users', category: 'System', subcategory: 'Users' },
            { id: 'system.groups.read', name: 'View Groups', category: 'System', subcategory: 'Groups' },
            { id: 'system.groups.write', name: 'Manage Groups', category: 'System', subcategory: 'Groups' },
            { id: 'system.groups.delete', name: 'Delete Groups', category: 'System', subcategory: 'Groups' },
            { id: 'system.permissions.read', name: 'View Permissions', category: 'System', subcategory: 'Permissions' },
            { id: 'system.settings.read', name: 'View Settings', category: 'System', subcategory: 'Settings' },
            { id: 'system.settings.write', name: 'Manage Settings', category: 'System', subcategory: 'Settings' }
        ];
    },

    /**
     * Render permissions page
     */
    renderPermissions: function() {
        const container = $('#permissions-container, #page-content').first();
        if (!container.length) return;

        // Render page header
        if (Monolith.PageHeader && typeof Monolith.PageHeader.render === 'function') {
            Monolith.PageHeader.render({
                title: "Permissions",
                icon: "fa-key",
                description: "View all available system and module permissions",
                container: container,
                prepend: true
            });
        }

        // Group by category
        const grouped = {};
        this.allPermissions.forEach(perm => {
            if (!grouped[perm.category]) {
                grouped[perm.category] = {};
            }
            if (!grouped[perm.category][perm.subcategory]) {
                grouped[perm.category][perm.subcategory] = [];
            }
            grouped[perm.category][perm.subcategory].push(perm);
        });

        let html = `
            <div class="container-fluid p-4">
                <div class="row">
                    <div class="col-12">
                            <p class="text-muted">View all available permissions in the system</p>
                        </div>
                    </div>
                </div>

                <div class="row">
                    <div class="col-12">
                        <div class="alert alert-info">
                            <svg width="16" height="16" fill="currentColor" class="me-2" viewBox="0 0 16 16">
                                <path d="M8 15A7 7 0 1 1 8 1a7 7 0 0 1 0 14zm0 1A8 8 0 1 0 8 0a8 8 0 0 0 0 16z"/>
                                <path d="m8.93 6.588-2.29.287-.082.38.45.083c.294.07.352.176.288.469l-.738 3.468c-.194.897.105 1.319.808 1.319.545 0 1.178-.252 1.465-.598l.088-.416c-.2.176-.492.246-.686.246-.275 0-.375-.193-.304-.533L8.93 6.588zM9 4.5a1 1 0 1 1-2 0 1 1 0 0 1 2 0z"/>
                            </svg>
                            <strong>Note:</strong> Permissions are assigned to user groups. To grant permissions, edit a user group and select the desired permissions.
                        </div>
                    </div>
                </div>
        `;

        // Render each category
        Object.keys(grouped).sort().forEach(category => {
            html += `
                <div class="row">
                    <div class="col-12">
                        <div class="card">
                            <div class="card-header">
                                <h5 class="mb-0">${category}</h5>
                            </div>
                            <div class="card-body">
            `;

            // Render subcategories
            Object.keys(grouped[category]).sort().forEach(subcategory => {
                const perms = grouped[category][subcategory];
                html += `
                    <div class="mb-4">
                        <h6 class="text-primary mb-3">${subcategory}</h6>
                        <div class="table-responsive">
                            <table class="table table-sm">
                                <thead>
                                    <tr>
                                        <th>Permission ID</th>
                                        <th>Name</th>
                                        <th>Description</th>
                                    </tr>
                                </thead>
                                <tbody>
                `;

                perms.forEach(perm => {
                    const description = perm.description || `${perm.packageId !== 'core' ? `From ${perm.packageId}` : 'Core system permission'}`;
                    html += `
                        <tr>
                            <td><code>${perm.id}</code></td>
                            <td>${perm.name}</td>
                            <td class="text-muted">${description}</td>
                        </tr>
                    `;
                });

                html += `
                                </tbody>
                            </table>
                        </div>
                    </div>
                `;
            });

            html += `
                            </div>
                        </div>
                    </div>
                </div>
            `;
        });

        html += '</div>';

        $('#page-content').html(html);
    }
};
