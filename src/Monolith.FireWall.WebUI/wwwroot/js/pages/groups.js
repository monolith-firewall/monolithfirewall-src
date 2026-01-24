/**
 * User Groups Page with RBAC Editor
 */
Monolith.Pages = Monolith.Pages || {};
Monolith.Pages.Groups = {
    groups: [],
    allPermissions: [],
    permissionCategories: {},
    currentGroup: null,

    init: async function() {
        await this.loadPermissionCategories();
        this.loadGroups();
        this.loadAllPermissions();
        this.setupEventHandlers();
    },

    setupEventHandlers: function() {
        $(document).on('click', '#btn-add-group', () => this.showAddModal());
        $(document).on('click', '.btn-edit-group', (e) => {
            const id = $(e.currentTarget).data('id');
            this.showEditModal(id);
        });
        $(document).on('click', '.btn-delete-group', (e) => {
            const id = $(e.currentTarget).data('id');
            this.deleteGroup(id);
        });
        $(document).on('submit', '#group-form', (e) => {
            e.preventDefault();
            this.saveGroup();
        });
        $(document).on('click', '.permission-category-toggle', function() {
            const category = $(this).data('category');
            $(`.permission-item[data-category="${category}"]`).toggle();
        });
    },

    loadGroups: async function() {
        try {
            Monolith.UI.showLoading('#groups-table-container');
            const response = await Monolith.API.get('/api/usergroups');
            if (response.success && response.data) {
                this.groups = response.data;
                // Load user counts for each group
                await this.loadGroupUserCounts();
                this.renderTable();
            }
        } catch (error) {
            console.error('Error loading groups:', error);
            Monolith.UI.toast('Error loading groups', 'error');
        }
    },

    groupUserCounts: {},

    loadGroupUserCounts: async function() {
        this.groupUserCounts = {};
        for (const group of this.groups) {
            try {
                const response = await Monolith.API.get(`/api/usergroups/${group.id}/users`);
                if (response.success && response.data) {
                    const users = Array.isArray(response.data) ? response.data : [];
                    this.groupUserCounts[group.id] = users.length;
                }
            } catch (error) {
                console.warn(`Error loading users for group ${group.id}:`, error);
                this.groupUserCounts[group.id] = 0;
            }
        }
    },

    getGroupUserCount: function(groupId) {
        const count = this.groupUserCounts[groupId] ?? 0;
        return count > 0 ? `<span class="badge bg-info">${count}</span>` : '<span class="text-muted">0</span>';
    },

    loadAllPermissions: async function() {
        // Load permissions from API
        try {
            const response = await Monolith.API.get('/api/permissions');
            if (response.success && response.data) {
                this.allPermissions = response.data || [];
            }
        } catch (error) {
            console.error('Error loading permissions:', error);
            this.allPermissions = [];
        }
    },

    loadPermissionCategories: async function() {
        // Load permissions and group by category
        try {
            const response = await Monolith.API.get('/api/permissions');
            if (response.success && response.data) {
                const permissions = response.data || [];
                
                // Group by category -> subcategory -> permissions
                const categories = {};
                permissions.forEach(perm => {
                    const category = perm.category || perm.Category || 'Other';
                    const subcategory = perm.subcategory || perm.Subcategory || '';
                    const permId = perm.id || perm.Id || '';
                    
                    if (!categories[category]) {
                        categories[category] = {};
                    }
                    
                    if (!categories[category][subcategory]) {
                        categories[category][subcategory] = [];
                    }
                    
                    categories[category][subcategory].push(permId);
                });
                
                this.permissionCategories = categories;
            }
        } catch (error) {
            console.error('Error loading permission categories:', error);
            this.permissionCategories = {};
        }
    },

    renderTable: function() {
        let html = `
            <div class="table-responsive">
                <table class="table table-hover">
                    <thead>
                        <tr>
                            <th>ID</th>
                            <th>Name</th>
                            <th>Description</th>
                            <th>Permissions</th>
                            <th>Users</th>
                            <th>Status</th>
                            <th>Actions</th>
                        </tr>
                    </thead>
                    <tbody>
        `;

        if (this.groups.length === 0) {
            html += `
                <tr>
                    <td colspan="7" class="text-center text-muted py-4">No groups found</td>
                </tr>
            `;
        } else {
            const self = this;
            this.groups.forEach(function(group) {
                const perms = group.permissions || [];
                const statusBadge = group.enabled 
                    ? '<span class="badge badge-success">Enabled</span>'
                    : '<span class="badge badge-danger">Disabled</span>';
                const userCount = self.getGroupUserCount(group.id);
                
                html += `
                    <tr>
                        <td>${group.id}</td>
                        <td><strong>${group.name}</strong></td>
                        <td>${group.description || '-'}</td>
                        <td>
                            ${perms.length > 0 
                                ? perms.slice(0, 3).map(p => `<span class="badge badge-primary">${p}</span>`).join(' ') + 
                                  (perms.length > 3 ? ` <span class="text-muted">+${perms.length - 3} more</span>` : '')
                                : '-'}
                        </td>
                        <td>${userCount}</td>
                        <td>${statusBadge}</td>
                        <td>
                            <button class="btn btn-sm btn-secondary btn-edit-group" data-id="${group.id}">Edit</button>
                            <button class="btn btn-sm btn-danger btn-delete-group" data-id="${group.id}">Delete</button>
                        </td>
                    </tr>
                `;
            });
        }

        html += `
                    </tbody>
                </table>
            </div>
        `;

        $('#groups-table-container').html(html);
    },

    showAddModal: async function() {
        // Ensure permissions are loaded before showing modal
        if (Object.keys(this.permissionCategories).length === 0) {
            await this.loadPermissionCategories();
        }
        
        this.currentGroup = null;
        this.showGroupModal();
    },

    showEditModal: async function(id) {
        try {
            // Ensure permissions are loaded before showing modal
            if (Object.keys(this.permissionCategories).length === 0) {
                await this.loadPermissionCategories();
            }
            
            const response = await Monolith.API.get(`/api/usergroups/${id}`);
            if (response.success && response.data) {
                this.currentGroup = response.data;
                this.showGroupModal();
            }
        } catch (error) {
            Monolith.UI.toast('Error loading group', 'error');
        }
    },

    showGroupModal: function() {
        const isEdit = this.currentGroup !== null;
        const group = this.currentGroup || { name: '', description: '', enabled: true, permissions: [] };
        const selectedPerms = group.permissions || [];
        
        const modal = `
            <div class="modal fade" id="groupModal" tabindex="-1">
                <div class="modal-dialog modal-xl">
                    <div class="modal-content">
                        <div class="modal-header">
                            <h5 class="modal-title">${isEdit ? 'Edit Group' : 'Add Group'}</h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                        </div>
                        <div class="modal-body">
                            <form id="group-form">
                                <input type="hidden" id="group-id" value="${group.id || ''}">
                                <div class="row">
                                    <div class="col-md-6">
                                        <div class="mb-3">
                                            <label for="group-name" class="form-label">Group Name</label>
                                            <input type="text" class="form-control" id="group-name" 
                                                   value="${group.name}" required>
                                        </div>
                                        <div class="mb-3">
                                            <label for="group-description" class="form-label">Description</label>
                                            <textarea class="form-control" id="group-description" rows="3">${group.description || ''}</textarea>
                                        </div>
                                        <div class="mb-3">
                                            <div class="form-check form-switch">
                                                <input class="form-check-input" type="checkbox" id="group-enabled" 
                                                       ${group.enabled ? 'checked' : ''}>
                                                <label class="form-check-label" for="group-enabled">Enabled</label>
                                            </div>
                                        </div>
                                    </div>
                                    <div class="col-md-6">
                                        <label class="form-label">Permissions (RBAC)</label>
                                        <div class="border rounded p-3" style="max-height: 400px; overflow-y: auto;">
                                            <div class="mb-2">
                                                <input type="checkbox" id="perm-all" class="form-check-input">
                                                <label for="perm-all" class="form-check-label fw-bold">All Permissions (*)</label>
                                            </div>
                                            <hr>
                                            <div id="permissions-list">
                                                ${this.renderPermissionsList(selectedPerms)}
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            </form>
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
                            <button type="submit" form="group-form" class="btn btn-primary">Save</button>
                        </div>
                    </div>
                </div>
            </div>
        `;

        $('body').append(modal);
        const modalEl = new bootstrap.Modal(document.getElementById('groupModal'));
        modalEl.show();
        
        // Handle "All Permissions" checkbox
        $('#perm-all').on('change', function() {
            const checked = $(this).is(':checked');
            $('#permissions-list input[type="checkbox"]').prop('checked', checked);
        });
        
        $('#groupModal').on('hidden.bs.modal', function() {
            $(this).remove();
        });
    },

    renderPermissionsList: function(selectedPerms) {
        // Use cached categories (loaded in init)
        const categories = this.permissionCategories || {};
        
        if (Object.keys(categories).length === 0) {
            // If permissions aren't loaded, try loading them and update the UI
            const self = this;
            this.loadPermissionCategories().then(() => {
                // Re-render the permissions list after loading
                const html = self.renderPermissionsList(selectedPerms);
                $('#permissions-list').html(html);
            }).catch(err => {
                console.error('Failed to load permissions:', err);
                $('#permissions-list').html('<div class="text-danger">Error loading permissions. Please refresh the page.</div>');
            });
            return '<div class="text-muted"><i class="spinner-border spinner-border-sm me-2"></i>Loading permissions...</div>';
        }

        let html = '';
        for (const [category, subcategories] of Object.entries(categories)) {
            html += `
                <div class="mb-3">
                    <div class="d-flex align-items-center mb-2">
                        <button type="button" class="btn btn-sm btn-link p-0 permission-category-toggle" 
                                data-category="${category.toLowerCase().replace(/\s+/g, '-')}">
                            <i class="bi bi-chevron-down"></i>
                        </button>
                        <strong class="ms-2">${category}</strong>
                    </div>
                    <div class="permission-items ms-4" data-category="${category.toLowerCase().replace(/\s+/g, '-')}">
            `;
            
            // Render subcategories
            for (const [subcategory, perms] of Object.entries(subcategories)) {
                if (subcategory && subcategory !== '') {
                    html += `
                        <div class="mb-2">
                            <strong class="text-muted small">${subcategory}</strong>
                        </div>
                    `;
                }
                
                perms.forEach(permId => {
                    const checked = selectedPerms.includes(permId) || selectedPerms.includes('*') ? 'checked' : '';
                    const safeId = permId.replace(/[.*]/g, '-').replace(/\s+/g, '-');
                    html += `
                        <div class="form-check">
                            <input class="form-check-input permission-checkbox" type="checkbox" 
                                   id="perm-${safeId}" value="${permId}" ${checked}>
                            <label class="form-check-label" for="perm-${safeId}">
                                <code>${permId}</code>
                            </label>
                        </div>
                    `;
                });
            }
            
            html += `
                    </div>
                </div>
            `;
        }
        
        return html;
    },

    saveGroup: async function() {
        try {
            const id = $('#group-id').val();
            const name = $('#group-name').val();
            const description = $('#group-description').val();
            const enabled = $('#group-enabled').is(':checked');
            
            // Get selected permissions
            const selectedPerms = [];
            if ($('#perm-all').is(':checked')) {
                selectedPerms.push('*');
            } else {
                $('.permission-checkbox:checked').each(function() {
                    selectedPerms.push($(this).val());
                });
            }

            if (!id) {
                // Create new group
                const response = await Monolith.API.post('/api/usergroups', {
                    name: name,
                    description: description,
                    permissions: selectedPerms
                });

                if (response.success) {
                    bootstrap.Modal.getInstance(document.getElementById('groupModal')).hide();
                    Monolith.UI.toast('Group created successfully', 'success');
                    this.loadGroups();
                } else {
                    Monolith.UI.toast(response.error || 'Error creating group', 'error');
                }
            } else {
                // Update existing group
                const response = await Monolith.API.put(`/api/usergroups/${id}`, {
                    description: description,
                    permissions: selectedPerms,
                    enabled: enabled
                });

                if (response.success) {
                    bootstrap.Modal.getInstance(document.getElementById('groupModal')).hide();
                    Monolith.UI.toast('Group updated successfully', 'success');
                    this.loadGroups();
                } else {
                    Monolith.UI.toast(response.error || 'Error updating group', 'error');
                }
            }
        } catch (error) {
            console.error('Error saving group:', error);
            Monolith.UI.toast('Error saving group', 'error');
        }
    },

    deleteGroup: async function(id) {
        if (!confirm('Are you sure you want to delete this group? Users in this group will lose their permissions.')) {
            return;
        }

        try {
            const response = await Monolith.API.delete(`/api/usergroups/${id}`);
            if (response.success) {
                Monolith.UI.toast('Group deleted successfully', 'success');
                this.loadGroups();
            } else {
                Monolith.UI.toast(response.error || 'Error deleting group', 'error');
            }
        } catch (error) {
            console.error('Error deleting group:', error);
            Monolith.UI.toast('Error deleting group', 'error');
        }
    }
};
