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
     * Load all permissions
     */
    loadPermissions: async function() {
        try {
            // For now, we'll hardcode permissions until Core provides them
            this.allPermissions = this.getHardcodedPermissions();
            this.renderPermissions();
        } catch (error) {
            console.error('Error loading permissions:', error);
            Monolith.UI.toast('Error loading permissions', 'error');
        }
    },

    /**
     * Get hardcoded permissions (until Core provides them)
     */
    getHardcodedPermissions: function() {
        return [
            // System permissions
            { id: 'system.users.read', name: 'View Users', category: 'System', subcategory: 'Users' },
            { id: 'system.users.write', name: 'Manage Users', category: 'System', subcategory: 'Users' },
            { id: 'system.users.delete', name: 'Delete Users', category: 'System', subcategory: 'Users' },
            { id: 'system.groups.read', name: 'View Groups', category: 'System', subcategory: 'Groups' },
            { id: 'system.groups.write', name: 'Manage Groups', category: 'System', subcategory: 'Groups' },
            { id: 'system.groups.delete', name: 'Delete Groups', category: 'System', subcategory: 'Groups' },
            { id: 'system.permissions.read', name: 'View Permissions', category: 'System', subcategory: 'Permissions' },
            { id: 'system.settings.read', name: 'View Settings', category: 'System', subcategory: 'Settings' },
            { id: 'system.settings.write', name: 'Manage Settings', category: 'System', subcategory: 'Settings' },
            
            // Network permissions
            { id: 'network.interfaces.read', name: 'View Interfaces', category: 'Network', subcategory: 'Interfaces' },
            { id: 'network.interfaces.write', name: 'Manage Interfaces', category: 'Network', subcategory: 'Interfaces' },
            { id: 'network.firewall.read', name: 'View Firewall Rules', category: 'Network', subcategory: 'Firewall' },
            { id: 'network.firewall.write', name: 'Manage Firewall Rules', category: 'Network', subcategory: 'Firewall' },
            { id: 'network.dhcp.read', name: 'View DHCP Configuration', category: 'Network', subcategory: 'DHCP' },
            { id: 'network.dhcp.write', name: 'Manage DHCP Configuration', category: 'Network', subcategory: 'DHCP' },
            { id: 'network.dns.read', name: 'View DNS Configuration', category: 'Network', subcategory: 'DNS' },
            { id: 'network.dns.write', name: 'Manage DNS Configuration', category: 'Network', subcategory: 'DNS' },
            
            // Services permissions
            { id: 'services.status.read', name: 'View Service Status', category: 'Services', subcategory: 'Status' },
            { id: 'services.control.write', name: 'Control Services', category: 'Services', subcategory: 'Control' },
            
            // Diagnostics permissions
            { id: 'diagnostics.logs.read', name: 'View Logs', category: 'Diagnostics', subcategory: 'Logs' },
            { id: 'diagnostics.tools.use', name: 'Use Diagnostic Tools', category: 'Diagnostics', subcategory: 'Tools' }
        ];
    },

    /**
     * Render permissions page
     */
    renderPermissions: function() {
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
            <div class="container-fluid">
                <div class="row">
                    <div class="col-12">
                        <div class="page-header">
                            <h2 class="page-title">
                                <svg class="page-icon" width="24" height="24" fill="currentColor" viewBox="0 0 16 16">
                                    <path d="M8 1a2 2 0 0 1 2 2v4H6V3a2 2 0 0 1 2-2zm3 6V3a3 3 0 0 0-6 0v4a2 2 0 0 0-2 2v5a2 2 0 0 0 2 2h6a2 2 0 0 0 2-2V9a2 2 0 0 0-2-2z"/>
                                </svg>
                                System Permissions
                            </h2>
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
                    html += `
                        <tr>
                            <td><code>${perm.id}</code></td>
                            <td>${perm.name}</td>
                            <td class="text-muted">-</td>
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
