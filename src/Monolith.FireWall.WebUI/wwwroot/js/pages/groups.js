/**
 * User Groups Page with RBAC Editor
 */
Monolith.Pages = Monolith.Pages || {};
Monolith.Pages.Groups = {
    groups: [],
    allPermissions: [],
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
            const response = await Monolith.API.get('/usergroups');
            if (response.success && response.data) {
                this.groups = response.data;
                this.renderTable();
            }
        } catch (error) {
            console.error('Error loading groups:', error);
            Monolith.UI.toast('Error loading groups', 'error');
        }
    },

    loadAllPermissions: async function() {
        // Load permissions from Core
        try {
            const response = await Monolith.API.get('/core?action=get-packages');
            if (response.success && response.data) {
                // Extract permissions from packages/modules
                this.allPermissions = [];
                // TODO: Parse permissions from packages
            }
        } catch (error) {
            console.error('Error loading permissions:', error);
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
            this.groups.forEach(group => {
                const perms = group.permissions || [];
                const statusBadge = group.enabled 
                    ? '<span class="badge badge-success">Enabled</span>'
                    : '<span class="badge badge-danger">Disabled</span>';
                
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
                        <td>-</td>
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

    showAddModal: function() {
        this.currentGroup = null;
        this.showGroupModal();
    },

    showEditModal: async function(id) {
        try {
            const response = await Monolith.API.get(`/usergroups/${id}`);
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
        const categories = this.permissionCategories || {
            'System': ['system.*', 'system.users.*', 'system.settings.*'],
            'Packages': ['packages.*', 'packages.install', 'packages.uninstall']
        };

        let html = '';
        for (const [category, perms] of Object.entries(categories)) {
            html += `
                <div class="mb-3">
                    <div class="d-flex align-items-center mb-2">
                        <button type="button" class="btn btn-sm btn-link p-0 permission-category-toggle" 
                                data-category="${category.toLowerCase()}">
                            <i class="bi bi-chevron-down"></i>
                        </button>
                        <strong class="ms-2">${category}</strong>
                    </div>
                    <div class="permission-items ms-4" data-category="${category.toLowerCase()}">
            `;
            
            perms.forEach(perm => {
                const checked = selectedPerms.includes(perm) || selectedPerms.includes('*') ? 'checked' : '';
                html += `
                    <div class="form-check">
                        <input class="form-check-input permission-checkbox" type="checkbox" 
                               id="perm-${perm.replace(/[.*]/g, '-')}" value="${perm}" ${checked}>
                        <label class="form-check-label" for="perm-${perm.replace(/[.*]/g, '-')}">
                            ${perm}
                        </label>
                    </div>
                `;
            });
            
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
                const response = await Monolith.API.post('/usergroups', {
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
                const response = await Monolith.API.put(`/usergroups/${id}`, {
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
            const response = await Monolith.API.delete(`/usergroups/${id}`);
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
