/**
 * User Profile Page with Tabs
 */
var Monolith = window.Monolith || {};
window.Monolith = Monolith;

Monolith.Pages = Monolith.Pages || {};

Monolith.Pages.Profile = {
    currentUser: null,

    /**
     * Initialize profile page
     */
    init: async function() {
        console.log('Initializing Profile page...');
        this.render();
        await this.loadProfile();
        this.attachEventHandlers();
    },

    /**
     * Render the main page structure with tabs
     */
    render: function() {
        const container = $('#page-content');
        container.html(`
            <div class="container-fluid p-4">
                <!-- Page Header -->
                <div class="row mb-4">
                    <div class="col-12">
                        <h2 class="page-title">
                            <svg width="24" height="24" fill="currentColor" viewBox="0 0 16 16" class="me-2">
                                <path d="M11 6a3 3 0 1 1-6 0 3 3 0 0 1 6 0z"/>
                                <path fill-rule="evenodd" d="M0 8a8 8 0 1 1 16 0A8 8 0 0 1 0 8zm8-7a7 7 0 0 0-5.468 11.37C3.242 11.226 4.805 10 8 10s4.757 1.225 5.468 2.37A7 7 0 0 0 8 1z"/>
                            </svg>
                            My Profile
                        </h2>
                        <p class="text-muted">Manage your account information, security settings, and view permissions</p>
                    </div>
                </div>

                <!-- Status Messages -->
                <div id="profileStatusMessage" class="alert d-none"></div>

                <!-- Main Tabs -->
                <ul class="nav nav-tabs mb-4" id="profileTabs" role="tablist">
                    <li class="nav-item" role="presentation">
                        <button class="nav-link active" id="basic-info-tab" data-bs-toggle="tab" data-bs-target="#basic-info" 
                                type="button" role="tab" aria-controls="basic-info" aria-selected="true">
                            <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-1">
                                <path d="M8 8a3 3 0 1 0 0-6 3 3 0 0 0 0 6zm2-3a2 2 0 1 1-4 0 2 2 0 0 1 4 0zm4 8c0 1-1 1-1 1H3s-1 0-1-1 1-4 6-4 6 3 6 4zm-1-.004c-.001-.246-.154-.986-.832-1.664C11.516 10.68 10.289 10 8 10c-2.29 0-3.516.68-4.168 1.332-.678.678-.83 1.418-.832 1.664h10z"/>
                            </svg>
                            Basic Information
                        </button>
                    </li>
                    <li class="nav-item" role="presentation">
                        <button class="nav-link" id="security-tab" data-bs-toggle="tab" data-bs-target="#security" 
                                type="button" role="tab" aria-controls="security" aria-selected="false">
                            <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-1">
                                <path d="M8 1a2 2 0 0 1 2 2v4H6V3a2 2 0 0 1 2-2zm3 6V3a3 3 0 0 0-6 0v4a2 2 0 0 0-2 2v5a2 2 0 0 0 2 2h6a2 2 0 0 0 2-2V9a2 2 0 0 0-2-2z"/>
                            </svg>
                            Security
                        </button>
                    </li>
                    <li class="nav-item" role="presentation">
                        <button class="nav-link" id="permissions-tab" data-bs-toggle="tab" data-bs-target="#permissions" 
                                type="button" role="tab" aria-controls="permissions" aria-selected="false">
                            <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-1">
                                <path d="M9.405 1.05c-.413-1.4-2.397-1.4-2.81 0l-.1.34a1.464 1.464 0 0 1-2.105.872l-.31-.17c-1.283-.698-2.686.705-1.987 1.987l.169.311c.446.82.023 1.841-.872 2.105l-.34.1c-1.4.413-1.4 2.397 0 2.81l.34.1a1.464 1.464 0 0 1 .872 2.105l-.17.31c-.698 1.283.705 2.686 1.987 1.987l.311-.169a1.464 1.464 0 0 1 2.105.872l.1.34c.413 1.4 2.397 1.4 2.81 0l.1-.34a1.464 1.464 0 0 1 2.105-.872l.31.17c1.283.698 2.686-.705 1.987-1.987l-.169-.311a1.464 1.464 0 0 1 .872-2.105l.34-.1c1.4-.413 1.4-2.397 0-2.81l-.34-.1a1.464 1.464 0 0 1-.872-2.105l.17-.31c.698-1.283-.705-2.686-1.987-1.987l-.311.169a1.464 1.464 0 0 1-2.105-.872l-.1-.34zM8 10.93a2.929 2.929 0 1 1 0-5.86 2.929 2.929 0 0 1 0 5.858z"/>
                            </svg>
                            Permissions
                        </button>
                    </li>
                </ul>

                <!-- Tab Content -->
                <div class="tab-content" id="profileTabContent">
                    <!-- Basic Information Tab -->
                    <div class="tab-pane fade show active" id="basic-info" role="tabpanel" aria-labelledby="basic-info-tab">
                        <div class="card">
                            <div class="card-header d-flex justify-content-between align-items-center">
                                <h5 class="mb-0">Basic Information</h5>
                                <button type="button" class="btn btn-sm btn-primary" id="btn-edit-profile">
                                    <svg width="14" height="14" fill="currentColor" viewBox="0 0 16 16" class="me-1">
                                        <path d="M12.146.146a.5.5 0 0 1 .708 0l3 3a.5.5 0 0 1 0 .708l-10 10a.5.5 0 0 1-.168.11l-5 2a.5.5 0 0 1-.65-.65l2-5a.5.5 0 0 1 .11-.168l10-10zM11.207 2.5 13.5 4.793 14.793 3.5 12.5 1.207 11.207 2.5zm1.586 3L10.5 3.207 4 9.707V10h.5a.5.5 0 0 1 .5.5v.5h.5a.5.5 0 0 1 .5.5v.5h.293l6.5-6.5zm-9.761 5.175-.106.106-1.528 3.821 3.821-1.528.106-.106A.5.5 0 0 1 5 12.5V12h-.5a.5.5 0 0 1-.5-.5V11h-.5a.5.5 0 0 1-.468-.325z"/>
                                    </svg>
                                    Edit
                                </button>
                            </div>
                            <div class="card-body">
                                <div class="row mb-4">
                                    <div class="col-md-3 text-center">
                                        <div class="profile-avatar mb-3">
                                            <svg width="100" height="100" fill="currentColor" viewBox="0 0 16 16" class="text-primary">
                                                <path d="M11 6a3 3 0 1 1-6 0 3 3 0 0 1 6 0z"/>
                                                <path fill-rule="evenodd" d="M0 8a8 8 0 1 1 16 0A8 8 0 0 1 0 8zm8-7a7 7 0 0 0-5.468 11.37C3.242 11.226 4.805 10 8 10s4.757 1.225 5.468 2.37A7 7 0 0 0 8 1z"/>
                                            </svg>
                                        </div>
                                        <div id="profile-username-display" class="fw-bold fs-5">Loading...</div>
                                        <div id="profile-userid-display" class="text-muted small">User ID: -</div>
                                    </div>
                                    <div class="col-md-9">
                                        <form id="profileForm">
                                            <div class="row mb-3">
                                                <label class="col-sm-3 col-form-label fw-bold">Username</label>
                                                <div class="col-sm-9">
                                                    <input type="text" class="form-control" id="profile-username" disabled>
                                                    <div class="form-text">Username cannot be changed</div>
                                                </div>
                                            </div>
                                            <div class="row mb-3">
                                                <label class="col-sm-3 col-form-label">Email</label>
                                                <div class="col-sm-9">
                                                    <input type="email" class="form-control" id="profile-email" placeholder="Enter email address">
                                                </div>
                                            </div>
                                            <div class="row mb-3">
                                                <label class="col-sm-3 col-form-label">Full Name</label>
                                                <div class="col-sm-9">
                                                    <input type="text" class="form-control" id="profile-fullname" placeholder="Enter full name">
                                                </div>
                                            </div>
                                            <div class="row mb-3">
                                                <label class="col-sm-3 col-form-label">Roles</label>
                                                <div class="col-sm-9">
                                                    <div id="profile-roles-display" class="mt-2">
                                                        <span class="text-muted">Loading...</span>
                                                    </div>
                                                    <div class="form-text">Roles are managed by administrators</div>
                                                </div>
                                            </div>
                                            <div class="row mb-3">
                                                <label class="col-sm-3 col-form-label">User ID</label>
                                                <div class="col-sm-9">
                                                    <input type="text" class="form-control" id="profile-userid" disabled>
                                                </div>
                                            </div>
                                            <div class="row mb-3">
                                                <label class="col-sm-3 col-form-label">Account Created</label>
                                                <div class="col-sm-9">
                                                    <input type="text" class="form-control" id="profile-created" disabled>
                                                </div>
                                            </div>
                                            <div class="row">
                                                <div class="col-sm-9 offset-sm-3">
                                                    <button type="submit" class="btn btn-primary" id="btn-save-profile" style="display: none;">
                                                        <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-1">
                                                            <path d="M15.854.146a.5.5 0 0 1 .11.54l-5.819 14.547a.75.75 0 0 1-1.329.124l-3.178-4.995L.643 7.184a.75.75 0 0 1 .124-1.33L15.314.037a.5.5 0 0 1 .54.11ZM6.636 10.07l2.761 4.338L14.13 2.576 6.636 10.07Zm6.787-8.201L1.591 6.602l4.339 2.76 7.494-7.493Z"/>
                                                        </svg>
                                                        Save Changes
                                                    </button>
                                                    <button type="button" class="btn btn-secondary" id="btn-cancel-edit" style="display: none;">Cancel</button>
                                                </div>
                                            </div>
                                        </form>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>

                    <!-- Security Tab -->
                    <div class="tab-pane fade" id="security" role="tabpanel" aria-labelledby="security-tab">
                        <div class="card">
                            <div class="card-header">
                                <h5 class="mb-0">Change Password</h5>
                            </div>
                            <div class="card-body">
                                <form id="changePasswordForm">
                                    <div class="row mb-3">
                                        <label class="col-sm-3 col-form-label">Current Password</label>
                                        <div class="col-sm-9">
                                            <input type="password" class="form-control" id="current-password" required>
                                        </div>
                                    </div>
                                    <div class="row mb-3">
                                        <label class="col-sm-3 col-form-label">New Password</label>
                                        <div class="col-sm-9">
                                            <input type="password" class="form-control" id="new-password" required>
                                            <div class="form-text">Password must be at least 8 characters long</div>
                                        </div>
                                    </div>
                                    <div class="row mb-3">
                                        <label class="col-sm-3 col-form-label">Confirm New Password</label>
                                        <div class="col-sm-9">
                                            <input type="password" class="form-control" id="confirm-password" required>
                                        </div>
                                    </div>
                                    <div class="row">
                                        <div class="col-sm-9 offset-sm-3">
                                            <button type="submit" class="btn btn-primary">
                                                <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-1">
                                                    <path d="M8 1a2 2 0 0 1 2 2v4H6V3a2 2 0 0 1 2-2zm3 6V3a3 3 0 0 0-6 0v4a2 2 0 0 0-2 2v5a2 2 0 0 0 2 2h6a2 2 0 0 0 2-2V9a2 2 0 0 0-2-2z"/>
                                                </svg>
                                                Change Password
                                            </button>
                                        </div>
                                    </div>
                                </form>
                            </div>
                        </div>

                        <div class="card mt-4">
                            <div class="card-header d-flex justify-content-between align-items-center">
                                <h5 class="mb-0">Two-Factor Authentication (2FA)</h5>
                            </div>
                            <div class="card-body">
                                <div class="row mb-3">
                                    <label class="col-sm-3 col-form-label fw-bold">2FA Status</label>
                                    <div class="col-sm-9">
                                        <div class="form-check form-switch">
                                            <input class="form-check-input" type="checkbox" id="twoFactorEnabled" disabled>
                                            <label class="form-check-label" for="twoFactorEnabled">
                                                Enable Two-Factor Authentication
                                            </label>
                                        </div>
                                        <div class="form-text">
                                            Two-factor authentication adds an extra layer of security to your account.
                                        </div>
                                    </div>
                                </div>
                                <div id="twoFactorSetup" style="display: none;">
                                    <hr class="my-4">
                                    <div class="row mb-3">
                                        <label class="col-sm-3 col-form-label">QR Code</label>
                                        <div class="col-sm-9">
                                            <div id="twoFactorQRCode" class="mb-3">
                                                <!-- QR code will be displayed here -->
                                            </div>
                                            <div class="form-text">Scan this QR code with your authenticator app</div>
                                        </div>
                                    </div>
                                    <div class="row mb-3">
                                        <label class="col-sm-3 col-form-label">Verification Code</label>
                                        <div class="col-sm-9">
                                            <input type="text" class="form-control" id="twoFactorCode" placeholder="Enter 6-digit code" maxlength="6">
                                            <div class="form-text">Enter the code from your authenticator app to verify</div>
                                        </div>
                                    </div>
                                    <div class="row">
                                        <div class="col-sm-9 offset-sm-3">
                                            <button type="button" class="btn btn-primary" id="btn-verify-2fa">Verify & Enable</button>
                                            <button type="button" class="btn btn-secondary" id="btn-cancel-2fa">Cancel</button>
                                        </div>
                                    </div>
                                </div>
                                <div id="twoFactorEnabledInfo" style="display: none;">
                                    <hr class="my-4">
                                    <div class="alert alert-info">
                                        <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-2">
                                            <path d="M8 16A8 8 0 1 0 8 0a8 8 0 0 0 0 16zm.93-9.412-1 4.705c-.07.34.029.533.304.533.194 0 .487-.07.686-.246l-.088.416c-.287.346-.92.598-1.465.598-.703 0-1.026-.599-1.076-1.314l-1.007-4.71c-.11-.44.114-.898.397-.998l.893-.242c.152-.041.294-.118.326-.255l.05-.145c.05-.45.238-.843.525-1.07l.11-.072c.4-.281.96-.405 1.465-.276l.039.01c.27.075.418.297.44.556l-.01.05c-.037.22-.061.444-.061.666 0 .213.018.42.05.62l-.01.05c-.09.27-.27.498-.51.657l-.003.002-.003.003c-.09.06-.18.15-.27.24l-.003.003c-.09.09-.15.18-.24.27l-.003.003-.003.002c-.16.24-.39.42-.66.51l-.05.01a3.5 3.5 0 0 1-.62.05H8.93z"/>
                                        </svg>
                                        Two-factor authentication is enabled for your account.
                                    </div>
                                    <div class="row">
                                        <div class="col-sm-9 offset-sm-3">
                                            <button type="button" class="btn btn-danger" id="btn-disable-2fa">Disable 2FA</button>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>

                    <!-- Permissions Tab -->
                    <div class="tab-pane fade" id="permissions" role="tabpanel" aria-labelledby="permissions-tab">
                        <div class="card">
                            <div class="card-header d-flex justify-content-between align-items-center">
                                <h5 class="mb-0">User Permissions</h5>
                                <span class="badge bg-primary" id="permissions-count">0 permissions</span>
                            </div>
                            <div class="card-body">
                                <div class="mb-3">
                                    <input type="text" class="form-control" id="permissions-filter" placeholder="Filter permissions...">
                                </div>
                                <div id="permissions-list" class="permission-grid">
                                    <div class="text-center text-muted">
                                        <div class="spinner-border spinner-border-sm me-2" role="status"></div>
                                        Loading permissions...
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `);
    },

    /**
     * Load user profile
     */
    loadProfile: async function() {
        try {
            const response = await Monolith.API.get('/user/current');
            if (response.success && response.data) {
                this.currentUser = response.data;
                this.renderBasicInfo();
                this.renderSecurity();
                this.renderPermissions();
            } else {
                this.showMessage('Failed to load profile', 'error');
            }
        } catch (error) {
            console.error('Error loading profile:', error);
            this.showMessage('Error loading profile', 'error');
        }
    },

    /**
     * Render basic information tab
     */
    renderBasicInfo: function() {
        if (!this.currentUser) return;

        const user = this.currentUser;
        const roles = user.roles || user.Roles || [];
        const email = user.email || user.Email || '';
        const fullName = user.fullName || user.FullName || '';
        const userId = user.id || user.Id || user.userId || user.UserId || 'N/A';
        const created = user.createdAt || user.CreatedAt || user.created || user.Created || 'N/A';

        $('#profile-username').val(user.username || user.Username || '');
        $('#profile-username-display').text(user.username || user.Username || '');
        $('#profile-email').val(email);
        $('#profile-fullname').val(fullName);
        $('#profile-userid').val(userId);
        $('#profile-userid-display').text(`User ID: ${userId}`);
        $('#profile-created').val(created);

        // Render roles
        const rolesHtml = roles.length > 0
            ? roles.map(role => `<span class="badge bg-primary me-1">${role}</span>`).join('')
            : '<span class="text-muted">No roles assigned</span>';
        $('#profile-roles-display').html(rolesHtml);
    },

    /**
     * Render security tab
     */
    renderSecurity: function() {
        if (!this.currentUser) return;

        // TODO: Load 2FA status from backend
        const twoFactorEnabled = false; // Placeholder
        $('#twoFactorEnabled').prop('checked', twoFactorEnabled);
        
        if (twoFactorEnabled) {
            $('#twoFactorSetup').hide();
            $('#twoFactorEnabledInfo').show();
        } else {
            $('#twoFactorSetup').hide();
            $('#twoFactorEnabledInfo').hide();
        }
    },

    /**
     * Render permissions tab
     */
    renderPermissions: function() {
        if (!this.currentUser) return;

        const permissions = this.currentUser.permissions || this.currentUser.Permissions || [];
        const count = permissions.length;
        
        $('#permissions-count').text(`${count} permission${count !== 1 ? 's' : ''}`);

        if (permissions.length === 0) {
            $('#permissions-list').html('<p class="text-muted text-center">No permissions assigned</p>');
            return;
        }

        // Group permissions by category (e.g., "system.", "network.", etc.)
        const grouped = {};
        permissions.forEach(perm => {
            const parts = perm.split('.');
            const category = parts.length > 1 ? parts[0] : 'other';
            if (!grouped[category]) {
                grouped[category] = [];
            }
            grouped[category].push(perm);
        });

        let html = '';
        Object.keys(grouped).sort().forEach(category => {
            html += `
                <div class="mb-4">
                    <h6 class="text-muted mb-2 text-uppercase">${category}</h6>
                    <div class="permission-badges">
                        ${grouped[category].map(perm => `
                            <span class="badge bg-secondary me-2 mb-2 permission-badge" data-permission="${perm}">${perm}</span>
                        `).join('')}
                    </div>
                </div>
            `;
        });

        $('#permissions-list').html(html);
        this.attachPermissionsFilter();
    },

    /**
     * Attach permissions filter
     */
    attachPermissionsFilter: function() {
        $(document).off('input', '#permissions-filter');
        $(document).on('input', '#permissions-filter', function() {
            const filter = $(this).val().toLowerCase();
            $('.permission-badge').each(function() {
                const perm = $(this).data('permission').toLowerCase();
                if (perm.includes(filter)) {
                    $(this).parent().show();
                    $(this).show();
                } else {
                    $(this).hide();
                }
            });
        });
    },

    /**
     * Attach event handlers
     */
    attachEventHandlers: function() {
        // Edit profile button
        $(document).off('click', '#btn-edit-profile');
        $(document).on('click', '#btn-edit-profile', () => {
            $('#profile-email, #profile-fullname').prop('disabled', false);
            $('#btn-save-profile, #btn-cancel-edit').show();
            $('#btn-edit-profile').hide();
        });

        // Cancel edit
        $(document).off('click', '#btn-cancel-edit');
        $(document).on('click', '#btn-cancel-edit', () => {
            this.renderBasicInfo(); // Reload from currentUser
            $('#profile-email, #profile-fullname').prop('disabled', true);
            $('#btn-save-profile, #btn-cancel-edit').hide();
            $('#btn-edit-profile').show();
        });

        // Save profile
        $(document).off('submit', '#profileForm');
        $(document).on('submit', '#profileForm', (e) => {
            e.preventDefault();
            this.saveProfile();
        });

        // Change password
        $(document).off('submit', '#changePasswordForm');
        $(document).on('submit', '#changePasswordForm', (e) => {
            e.preventDefault();
            this.changePassword();
        });

        // 2FA toggle
        $(document).off('change', '#twoFactorEnabled');
        $(document).on('change', '#twoFactorEnabled', () => {
            // TODO: Implement 2FA setup
            Monolith.UI.toast('2FA setup coming soon', 'info');
        });
    },

    /**
     * Save profile
     */
    saveProfile: async function() {
        const email = $('#profile-email').val();
        const fullName = $('#profile-fullname').val();

        try {
            // TODO: Replace with actual API call
            // const response = await Monolith.API.post('/profile/update', { email, fullName });
            
            this.showMessage('Profile updated successfully', 'success');
            $('#profile-email, #profile-fullname').prop('disabled', true);
            $('#btn-save-profile, #btn-cancel-edit').hide();
            $('#btn-edit-profile').show();
            
            // Update currentUser
            if (this.currentUser) {
                this.currentUser.email = email;
                this.currentUser.fullName = fullName;
            }
        } catch (error) {
            console.error('Error saving profile:', error);
            this.showMessage('Error saving profile', 'error');
        }
    },

    /**
     * Change password
     */
    changePassword: async function() {
        const currentPassword = $('#current-password').val();
        const newPassword = $('#new-password').val();
        const confirmPassword = $('#confirm-password').val();

        if (!currentPassword || !newPassword || !confirmPassword) {
            this.showMessage('Please fill in all fields', 'warning');
            return;
        }

        if (newPassword !== confirmPassword) {
            this.showMessage('Passwords do not match', 'warning');
            return;
        }

        if (newPassword.length < 8) {
            this.showMessage('Password must be at least 8 characters', 'warning');
            return;
        }

        try {
            // TODO: Replace with actual API call
            // const response = await Monolith.API.post('/profile/change-password', { currentPassword, newPassword });
            
            this.showMessage('Password changed successfully', 'success');
            $('#changePasswordForm')[0].reset();
        } catch (error) {
            console.error('Error changing password:', error);
            this.showMessage('Error changing password', 'error');
        }
    },

    /**
     * Show status message
     */
    showMessage: function(message, type) {
        const alert = $('#profileStatusMessage');
        alert.removeClass('d-none alert-success alert-danger alert-warning alert-info')
             .addClass(`alert-${type}`)
             .text(message);
        setTimeout(() => alert.addClass('d-none'), 5000);
    }
};
