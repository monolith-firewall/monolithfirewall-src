/**
 * User Manager Page
 */
Monolith.Pages = Monolith.Pages || {};
Monolith.Pages.Users = {
    users: [],
    currentUser: null,

    init: function() {
        this.loadUsers();
        this.setupEventHandlers();
    },

    setupEventHandlers: function() {
        $(document).on('click', '#btn-add-user', () => this.showAddModal());
        $(document).on('click', '.btn-edit-user', (e) => {
            const id = $(e.currentTarget).data('id');
            this.showEditModal(id);
        });
        $(document).on('click', '.btn-delete-user', (e) => {
            const id = $(e.currentTarget).data('id');
            this.deleteUser(id);
        });
        $(document).on('submit', '#user-form', (e) => {
            e.preventDefault();
            this.saveUser();
        });
    },

    loadUsers: async function() {
        try {
            Monolith.UI.showLoading('#users-table-container');
            const response = await Monolith.API.get('/api/users');
            if (response.success && response.data) {
                this.users = response.data;
                this.renderTable();
            }
        } catch (error) {
            console.error('Error loading users:', error);
            Monolith.UI.toast('Error loading users', 'error');
        }
    },

    renderTable: function() {
        let html = `
            <div class="table-responsive">
                <table class="table table-hover">
                    <thead>
                        <tr>
                            <th>ID</th>
                            <th>Username</th>
                            <th>Email</th>
                            <th>Roles</th>
                            <th>Groups</th>
                            <th>Status</th>
                            <th>Actions</th>
                        </tr>
                    </thead>
                    <tbody>
        `;

        if (this.users.length === 0) {
            html += `
                <tr>
                    <td colspan="7" class="text-center text-muted py-4">No users found</td>
                </tr>
            `;
        } else {
            this.users.forEach(user => {
                const roles = user.roles || [];
                const groups = user.groups || [];
                const statusBadge = user.enabled 
                    ? '<span class="badge badge-success">Enabled</span>'
                    : '<span class="badge badge-danger">Disabled</span>';
                
                html += `
                    <tr>
                        <td>${user.id}</td>
                        <td><strong>${user.username}</strong></td>
                        <td>${user.email}</td>
                        <td>${roles.length > 0 ? roles.map(r => `<span class="badge badge-primary">${r}</span>`).join(' ') : '-'}</td>
                        <td>${groups.length > 0 ? groups.map(g => `<span class="badge badge-info">${g.name}</span>`).join(' ') : '-'}</td>
                        <td>${statusBadge}</td>
                        <td>
                            <button class="btn btn-sm btn-secondary btn-edit-user" data-id="${user.id}">Edit</button>
                            <button class="btn btn-sm btn-danger btn-delete-user" data-id="${user.id}">Delete</button>
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

        $('#users-table-container').html(html);
    },

    showAddModal: function() {
        this.currentUser = null;
        this.showUserModal();
    },

    showEditModal: async function(id) {
        try {
            const response = await Monolith.API.get(`/api/users/${id}`);
            if (response.success && response.data) {
                this.currentUser = response.data;
                this.showUserModal();
            }
        } catch (error) {
            Monolith.UI.toast('Error loading user', 'error');
        }
    },

    showUserModal: function() {
        const isEdit = this.currentUser !== null;
        const user = this.currentUser || { username: '', email: '', enabled: true, roles: [], groups: [] };
        
        // Load groups for selection
        this.loadGroupsForModal().then(groups => {
            const modal = `
                <div class="modal fade" id="userModal" tabindex="-1">
                    <div class="modal-dialog modal-lg">
                        <div class="modal-content">
                            <div class="modal-header">
                                <h5 class="modal-title">${isEdit ? 'Edit User' : 'Add User'}</h5>
                                <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                            </div>
                            <div class="modal-body">
                                <form id="user-form">
                                    <input type="hidden" id="user-id" value="${user.id || ''}">
                                    <div class="mb-3">
                                        <label for="user-username" class="form-label">Username</label>
                                        <input type="text" class="form-control" id="user-username" 
                                               value="${user.username}" ${isEdit ? 'readonly' : 'required'}>
                                    </div>
                                    <div class="mb-3">
                                        <label for="user-email" class="form-label">Email</label>
                                        <input type="email" class="form-control" id="user-email" 
                                               value="${user.email}" required>
                                    </div>
                                    <div class="mb-3">
                                        <label for="user-password" class="form-label">${isEdit ? 'New Password (leave blank to keep current)' : 'Password'}</label>
                                        <input type="password" class="form-control" id="user-password" 
                                               ${isEdit ? '' : 'required'}>
                                    </div>
                                    <div class="mb-3">
                                        <label class="form-label">Groups</label>
                                        <div id="user-groups-checkboxes">
                                            ${groups.map(g => `
                                                <div class="form-check">
                                                    <input class="form-check-input" type="checkbox" 
                                                           id="group-${g.id}" value="${g.id}"
                                                           ${user.groups && user.groups.some(ug => ug.id === g.id) ? 'checked' : ''}>
                                                    <label class="form-check-label" for="group-${g.id}">
                                                        ${g.name}
                                                    </label>
                                                </div>
                                            `).join('')}
                                        </div>
                                    </div>
                                    <div class="mb-3">
                                        <div class="form-check form-switch">
                                            <input class="form-check-input" type="checkbox" id="user-enabled" 
                                                   ${user.enabled ? 'checked' : ''}>
                                            <label class="form-check-label" for="user-enabled">Enabled</label>
                                        </div>
                                    </div>
                                </form>
                            </div>
                            <div class="modal-footer">
                                <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
                                <button type="submit" form="user-form" class="btn btn-primary">Save</button>
                            </div>
                        </div>
                    </div>
                </div>
            `;

            $('body').append(modal);
            const modalEl = new bootstrap.Modal(document.getElementById('userModal'));
            modalEl.show();
            
            $('#userModal').on('hidden.bs.modal', function() {
                $(this).remove();
            });
        });
    },

    loadGroupsForModal: async function() {
        try {
            const response = await Monolith.API.get('/api/usergroups');
            if (response.success && response.data) {
                return response.data;
            }
        } catch (error) {
            console.error('Error loading groups:', error);
        }
        return [];
    },

    saveUser: async function() {
        try {
            const id = $('#user-id').val();
            const username = $('#user-username').val();
            const email = $('#user-email').val();
            const password = $('#user-password').val();
            const enabled = $('#user-enabled').is(':checked');
            
            // Get selected groups
            const selectedGroups = [];
            $('#user-groups-checkboxes input:checked').each(function() {
                selectedGroups.push(parseInt($(this).val()));
            });

            if (!id) {
                // Create new user
                const response = await Monolith.API.post('/api/users', {
                    username: username,
                    email: email,
                    password: password,
                    roles: []
                });

                if (response.success) {
                    // Add to groups
                    for (const groupId of selectedGroups) {
                        await Monolith.API.post(`/api/usergroups/${groupId}/users/${response.data.id}`, {});
                    }
                    
                    bootstrap.Modal.getInstance(document.getElementById('userModal')).hide();
                    Monolith.UI.toast('User created successfully', 'success');
                    this.loadUsers();
                } else {
                    Monolith.UI.toast(response.error || 'Error creating user', 'error');
                }
            } else {
                // Update existing user
                const updateData = {
                    email: email,
                    enabled: enabled
                };

                if (password) {
                    updateData.password = password;
                }

                const response = await Monolith.API.put(`/api/users/${id}`, updateData);

                if (response.success) {
                    // Update group memberships
                    const currentGroups = this.currentUser.groups || [];
                    const currentGroupIds = currentGroups.map(g => g.id);
                    
                    // Remove from groups
                    for (const groupId of currentGroupIds) {
                        if (!selectedGroups.includes(groupId)) {
                            await Monolith.API.delete(`/api/usergroups/${groupId}/users/${id}`);
                        }
                    }
                    
                    // Add to groups
                    for (const groupId of selectedGroups) {
                        if (!currentGroupIds.includes(groupId)) {
                            await Monolith.API.post(`/api/usergroups/${groupId}/users/${id}`, {});
                        }
                    }
                    
                    bootstrap.Modal.getInstance(document.getElementById('userModal')).hide();
                    Monolith.UI.toast('User updated successfully', 'success');
                    this.loadUsers();
                } else {
                    Monolith.UI.toast(response.error || 'Error updating user', 'error');
                }
            }
        } catch (error) {
            console.error('Error saving user:', error);
            Monolith.UI.toast('Error saving user', 'error');
        }
    },

    deleteUser: async function(id) {
        if (!confirm('Are you sure you want to delete this user? This action cannot be undone.')) {
            return;
        }

        try {
            const response = await Monolith.API.delete(`/api/users/${id}`);
            if (response.success) {
                Monolith.UI.toast('User deleted successfully', 'success');
                this.loadUsers();
            } else {
                Monolith.UI.toast(response.error || 'Error deleting user', 'error');
            }
        } catch (error) {
            console.error('Error deleting user:', error);
            Monolith.UI.toast('Error deleting user', 'error');
        }
    }
};
