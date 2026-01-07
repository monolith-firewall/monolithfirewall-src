/**
 * Backup & Restore Page
 */
var Monolith = window.Monolith || {};
window.Monolith = Monolith;

Monolith.Pages = Monolith.Pages || {};
Monolith.Pages.Backup = {
    init: function() {
        this.render();
        this.attachEventHandlers();
        this.loadBackups();
        this.loadSettings();
    },

    render: function() {
        const html = `
            <div class="container-fluid content-container p-4">
                <div class="row">
                    <div class="col-12">
                        <div class="card shadow-sm">
                            <div class="card-header bg-primary text-white">
                                <h3 class="mb-0">
                                    <svg width="24" height="24" fill="currentColor" viewBox="0 0 16 16" class="me-2">
                                        <path d="M8 4a.5.5 0 0 1 .5.5v3h3a.5.5 0 0 1 0 1h-3v3a.5.5 0 0 1-1 0v-3h-3a.5.5 0 0 1 0-1h3v-3A.5.5 0 0 1 8 4z"/>
                                        <path d="M0 2a2 2 0 0 1 2-2h12a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H2a2 2 0 0 1-2-2V2zm15 0a1 1 0 0 0-1-1H2a1 1 0 0 0-1 1v12a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1V2z"/>
                                    </svg>
                                    Backup & Restore
                                </h3>
                            </div>
                            <div class="card-body">
                                <!-- Status Messages -->
                                <div id="backup-status-message" class="alert d-none"></div>

                                <!-- Tabs -->
                                <ul class="nav nav-tabs mb-4" id="backupTabs" role="tablist">
                                    <li class="nav-item" role="presentation">
                                        <button class="nav-link active" id="local-tab" data-bs-toggle="tab" 
                                                data-bs-target="#local" type="button" role="tab" 
                                                aria-controls="local" aria-selected="true">
                                            <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-1">
                                                <path d="M8 4a.5.5 0 0 1 .5.5v3h3a.5.5 0 0 1 0 1h-3v3a.5.5 0 0 1-1 0v-3h-3a.5.5 0 0 1 0-1h3v-3A.5.5 0 0 1 8 4z"/>
                                                <path d="M0 2a2 2 0 0 1 2-2h12a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H2a2 2 0 0 1-2-2V2zm15 0a1 1 0 0 0-1-1H2a1 1 0 0 0-1 1v12a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1V2z"/>
                                            </svg>
                                            Local Backups
                                        </button>
                                    </li>
                                    <li class="nav-item" role="presentation">
                                        <button class="nav-link" id="cloud-tab" data-bs-toggle="tab" 
                                                data-bs-target="#cloud" type="button" role="tab" 
                                                aria-controls="cloud" aria-selected="false">
                                            <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-1">
                                                <path d="M3.5 0a.5.5 0 0 1 .5.5V1h8V.5a.5.5 0 0 1 1 0V1h1a2 2 0 0 1 2 2v11a2 2 0 0 1-2 2H2a2 2 0 0 1-2-2V3a2 2 0 0 1 2-2h1V.5a.5.5 0 0 1 .5-.5zM1 4v10a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1V4H1z"/>
                                            </svg>
                                            Cloud Backups
                                        </button>
                                    </li>
                                    <li class="nav-item" role="presentation">
                                        <button class="nav-link" id="settings-tab" data-bs-toggle="tab" 
                                                data-bs-target="#settings" type="button" role="tab" 
                                                aria-controls="settings" aria-selected="false">
                                            <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-1">
                                                <path d="M9.405 1.05c-.413-1.4-2.397-1.4-2.81 0l-.1.34a1.464 1.464 0 0 1-2.105.872l-.31-.17c-1.283-.698-2.686.705-1.987 1.987l.169.311c.446.82.023 1.841-.872 2.105l-.34.1c-1.4.413-1.4 2.397 0 2.81l.34.1a1.464 1.464 0 0 1 .872 2.105l-.17.31c-.698 1.283.705 2.686 1.987 1.987l.311-.169a1.464 1.464 0 0 1 2.105.872l.1.34c.413 1.4 2.397 1.4 2.81 0l.1-.34a1.464 1.464 0 0 1 2.105-.872l.31.17c1.283.698 2.686-.705 1.987-1.987l-.169-.311a1.464 1.464 0 0 1 .872-2.105l.34-.1c1.4-.413 1.4-2.397 0-2.81l-.34-.1a1.464 1.464 0 0 1-.872-2.105l.17-.31c.698-1.283-.705-2.686-1.987-1.987l-.311.169a1.464 1.464 0 0 1-2.105-.872l-.1-.34zM8 10.93a2.929 2.929 0 1 1 0-5.86 2.929 2.929 0 0 1 0 5.858z"/>
                                            </svg>
                                            Settings
                                        </button>
                                    </li>
                                </ul>

                                <!-- Tab Content -->
                                <div class="tab-content" id="backupTabContent">
                                    <!-- Local Backups Tab -->
                                    <div class="tab-pane fade show active" id="local" role="tabpanel" aria-labelledby="local-tab">
                                        <div class="d-flex justify-content-between align-items-center mb-3">
                                            <h5 class="mb-0">Local Backups</h5>
                                            <div class="btn-group">
                                                <button type="button" class="btn btn-success" id="btn-upload-backup">
                                                    <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-1">
                                                        <path d="M.5 9.9a.5.5 0 0 1 .5.5v2.5a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1v-2.5a.5.5 0 0 1 1 0v2.5a2 2 0 0 1-2 2H2a2 2 0 0 1-2-2v-2.5a.5.5 0 0 1 .5-.5z"/>
                                                        <path d="M7.646 1.146a.5.5 0 0 1 .708 0l3 3a.5.5 0 0 1-.708.708L8.5 2.707V11.5a.5.5 0 0 1-1 0V2.707L5.354 4.854a.5.5 0 1 1-.708-.708l3-3z"/>
                                                    </svg>
                                                    Upload Backup
                                                </button>
                                                <button type="button" class="btn btn-primary" id="btn-create-backup">
                                                    <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-1">
                                                        <path d="M8 4a.5.5 0 0 1 .5.5v3h3a.5.5 0 0 1 0 1h-3v3a.5.5 0 0 1-1 0v-3h-3a.5.5 0 0 1 0-1h3v-3A.5.5 0 0 1 8 4z"/>
                                                    </svg>
                                                    Create Backup
                                                </button>
                                            </div>
                                        </div>

                                        <div class="table-responsive">
                                            <table class="table table-hover" id="backupsTable">
                                                <thead>
                                                    <tr>
                                                        <th>Date</th>
                                                        <th>Description</th>
                                                        <th>Size</th>
                                                        <th>Actions</th>
                                                    </tr>
                                                </thead>
                                                <tbody>
                                                    <tr>
                                                        <td colspan="4" class="text-center text-muted">
                                                            <div class="spinner-border spinner-border-sm me-2" role="status">
                                                                <span class="visually-hidden">Loading...</span>
                                                            </div>
                                                            Loading backups...
                                                        </td>
                                                    </tr>
                                                </tbody>
                                            </table>
                                        </div>
                                    </div>

                                    <!-- Cloud Backups Tab -->
                                    <div class="tab-pane fade" id="cloud" role="tabpanel" aria-labelledby="cloud-tab">
                                        <div class="alert alert-info">
                                            <h5 class="alert-heading">
                                                <svg width="20" height="20" fill="currentColor" viewBox="0 0 16 16" class="me-2">
                                                    <path d="M8 15A7 7 0 1 1 8 1a7 7 0 0 1 0 14zm0 1A8 8 0 1 0 8 0a8 8 0 0 0 0 16z"/>
                                                    <path d="m8.93 6.588-2.29.287-.082.38.45.083c.294.07.352.176.288.469l-.738 3.468c-.194.897.105 1.319.808 1.319.545 0 1.178-.252 1.465-.598l.088-.416c-.2.176-.492.246-.686.246-.275 0-.375-.193-.304-.533L8.93 6.588zM9 4.5a1 1 0 1 1-2 0 1 1 0 0 1 2 0z"/>
                                                </svg>
                                                Cloud Backup Coming Soon
                                            </h5>
                                            <p class="mb-0">
                                                Cloud backup functionality will be available in a future update. 
                                                This will support S3, Azure Blob Storage, and Google Cloud Storage.
                                            </p>
                                        </div>
                                    </div>

                                    <!-- Settings Tab -->
                                    <div class="tab-pane fade" id="settings" role="tabpanel" aria-labelledby="settings-tab">
                                        <div class="row">
                                            <div class="col-12">
                                                <h5 class="mb-4">Backup Settings</h5>
                                                
                                                <!-- Naming Pattern -->
                                                <div class="card mb-3">
                                                    <div class="card-header">
                                                        <h6 class="mb-0">Backup Naming</h6>
                                                    </div>
                                                    <div class="card-body">
                                                        <div class="mb-3">
                                                            <label for="naming-pattern" class="form-label">Naming Pattern</label>
                                                            <input type="text" class="form-control" id="naming-pattern" 
                                                                   placeholder="monolith-backup-{timestamp}">
                                                            <div class="form-text">
                                                                Available placeholders: <code>{timestamp}</code>, <code>{date}</code>, 
                                                                <code>{time}</code>, <code>{datetime}</code>, <code>{description}</code>
                                                            </div>
                                                        </div>
                                                    </div>
                                                </div>

                                                <!-- What to Include -->
                                                <div class="card mb-3">
                                                    <div class="card-header">
                                                        <h6 class="mb-0">What to Include in Backups</h6>
                                                    </div>
                                                    <div class="card-body">
                                                        <div class="form-check mb-2">
                                                            <input class="form-check-input" type="checkbox" id="include-database" checked>
                                                            <label class="form-check-label" for="include-database">
                                                                Database (always included)
                                                            </label>
                                                        </div>
                                                        <div class="form-check mb-2">
                                                            <input class="form-check-input" type="checkbox" id="include-config">
                                                            <label class="form-check-label" for="include-config">
                                                                Configuration Files
                                                            </label>
                                                        </div>
                                                        <div class="form-check mb-2">
                                                            <input class="form-check-input" type="checkbox" id="include-logs">
                                                            <label class="form-check-label" for="include-logs">
                                                                Log Files
                                                            </label>
                                                        </div>
                                                    </div>
                                                </div>

                                                <!-- Advanced Settings -->
                                                <div class="card mb-3">
                                                    <div class="card-header">
                                                        <h6 class="mb-0">Advanced Settings</h6>
                                                    </div>
                                                    <div class="card-body">
                                                        <div class="mb-3">
                                                            <label for="max-backups" class="form-label">Maximum Backups to Keep</label>
                                                            <input type="number" class="form-control" id="max-backups" min="1" max="100" value="10">
                                                            <div class="form-text">Automatically delete oldest backups when this limit is reached</div>
                                                        </div>
                                                        <div class="form-check mb-3">
                                                            <input class="form-check-input" type="checkbox" id="auto-backup">
                                                            <label class="form-check-label" for="auto-backup">
                                                                Enable Automatic Backups
                                                            </label>
                                                        </div>
                                                        <div class="mb-3" id="auto-backup-interval-group" style="display: none;">
                                                            <label for="auto-backup-interval" class="form-label">Backup Interval (hours)</label>
                                                            <input type="number" class="form-control" id="auto-backup-interval" min="1" max="168" value="24">
                                                        </div>
                                                    </div>
                                                </div>

                                                <!-- Additional Locations -->
                                                <div class="card mb-3">
                                                    <div class="card-header d-flex justify-content-between align-items-center">
                                                        <h6 class="mb-0">Additional Backup Locations</h6>
                                                        <button type="button" class="btn btn-sm btn-primary" id="btn-add-location">
                                                            <svg width="14" height="14" fill="currentColor" viewBox="0 0 16 16" class="me-1">
                                                                <path d="M8 4a.5.5 0 0 1 .5.5v3h3a.5.5 0 0 1 0 1h-3v3a.5.5 0 0 1-1 0v-3h-3a.5.5 0 0 1 0-1h3v-3A.5.5 0 0 1 8 4z"/>
                                                            </svg>
                                                            Add Location
                                                        </button>
                                                    </div>
                                                    <div class="card-body">
                                                        <div id="additional-locations-list">
                                                            <p class="text-muted mb-0">No additional locations configured</p>
                                                        </div>
                                                    </div>
                                                </div>

                                                <!-- Save Button -->
                                                <div class="d-flex justify-content-end">
                                                    <button type="button" class="btn btn-primary" id="btn-save-settings">
                                                        <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-1">
                                                            <path d="M15.854.146a.5.5 0 0 1 .11.54l-5.819 14.547a.75.75 0 0 1-1.329.124l-3.178-4.995L.643 7.184a.75.75 0 0 1 .124-1.33L15.314.037a.5.5 0 0 1 .54.11ZM6.636 10.07l2.761 4.338L14.13 2.576 6.636 10.07Zm6.787-8.201L1.591 6.602l4.339 2.76 7.494-7.493Z"/>
                                                        </svg>
                                                        Save Settings
                                                    </button>
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>

            <!-- Create Backup Modal -->
            <div class="modal fade" id="createBackupModal" tabindex="-1">
                <div class="modal-dialog">
                    <div class="modal-content">
                        <div class="modal-header">
                            <h5 class="modal-title">Create Backup</h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                        </div>
                        <div class="modal-body">
                            <div class="mb-3">
                                <label for="backup-description" class="form-label">Description (optional)</label>
                                <input type="text" class="form-control" id="backup-description" 
                                       placeholder="e.g., Before system update">
                                <div class="form-text">Add a description to help identify this backup later.</div>
                            </div>
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
                            <button type="button" class="btn btn-primary" id="btn-confirm-create-backup">
                                <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-1">
                                    <path d="M15.854.146a.5.5 0 0 1 .11.54l-5.819 14.547a.75.75 0 0 1-1.329.124l-3.178-4.995L.643 7.184a.75.75 0 0 1 .124-1.33L15.314.037a.5.5 0 0 1 .54.11ZM6.636 10.07l2.761 4.338L14.13 2.576 6.636 10.07Zm6.787-8.201L1.591 6.602l4.339 2.76 7.494-7.493Z"/>
                                </svg>
                                Create Backup
                            </button>
                        </div>
                    </div>
                </div>
            </div>

            <!-- Upload Backup Modal -->
            <div class="modal fade" id="uploadBackupModal" tabindex="-1">
                <div class="modal-dialog">
                    <div class="modal-content">
                        <div class="modal-header">
                            <h5 class="modal-title">Upload Backup</h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                        </div>
                        <div class="modal-body">
                            <div class="mb-3">
                                <label for="backup-file" class="form-label">Backup File (.db.gz)</label>
                                <input type="file" class="form-control" id="backup-file" accept=".db.gz,application/gzip">
                                <div class="form-text">Select a backup file to upload. Maximum size: 100 MB</div>
                            </div>
                            <div class="mb-3">
                                <label for="upload-description" class="form-label">Description (optional)</label>
                                <input type="text" class="form-control" id="upload-description" 
                                       placeholder="e.g., Restored from previous system">
                                <div class="form-text">Add a description to help identify this backup.</div>
                            </div>
                            <div id="upload-progress" class="d-none">
                                <div class="progress">
                                    <div class="progress-bar progress-bar-striped progress-bar-animated" 
                                         role="progressbar" style="width: 100%">Uploading...</div>
                                </div>
                            </div>
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
                            <button type="button" class="btn btn-success" id="btn-confirm-upload-backup">
                                <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-1">
                                    <path d="M.5 9.9a.5.5 0 0 1 .5.5v2.5a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1v-2.5a.5.5 0 0 1 1 0v2.5a2 2 0 0 1-2 2H2a2 2 0 0 1-2-2v-2.5a.5.5 0 0 1 .5-.5z"/>
                                    <path d="M7.646 1.146a.5.5 0 0 1 .708 0l3 3a.5.5 0 0 1-.708.708L8.5 2.707V11.5a.5.5 0 0 1-1 0V2.707L5.354 4.854a.5.5 0 1 1-.708-.708l3-3z"/>
                                </svg>
                                Upload Backup
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        `;

        $('#page-content').html(html);
    },

    attachEventHandlers: function() {
        const self = this;

        // Create backup button
        $(document).on('click', '#btn-create-backup', function() {
            $('#backup-description').val('');
            new bootstrap.Modal(document.getElementById('createBackupModal')).show();
        });

        // Upload backup button
        $(document).on('click', '#btn-upload-backup', function() {
            $('#backup-file').val('');
            $('#upload-description').val('');
            $('#upload-progress').addClass('d-none');
            new bootstrap.Modal(document.getElementById('uploadBackupModal')).show();
        });

        // Confirm create backup
        $(document).on('click', '#btn-confirm-create-backup', function() {
            self.createBackup();
        });

        // Confirm upload backup
        $(document).on('click', '#btn-confirm-upload-backup', function() {
            self.uploadBackup();
        });

        // Download backup
        $(document).on('click', '.btn-download-backup', function() {
            const fileName = $(this).data('filename');
            self.downloadBackup(fileName);
        });

        // Restore backup
        $(document).on('click', '.btn-restore-backup', function() {
            const fileName = $(this).data('filename');
            self.restoreBackup(fileName);
        });

        // Delete backup
        $(document).on('click', '.btn-delete-backup', function() {
            const fileName = $(this).data('filename');
            self.deleteBackup(fileName);
        });

        // Auto backup toggle
        $(document).on('change', '#auto-backup', function() {
            if ($(this).is(':checked')) {
                $('#auto-backup-interval-group').show();
            } else {
                $('#auto-backup-interval-group').hide();
            }
        });

        // Add location
        $(document).on('click', '#btn-add-location', function() {
            self.addLocation();
        });

        // Remove location
        $(document).on('click', '.btn-remove-location', function() {
            const id = $(this).data('id');
            self.removeLocation(id);
        });

        // Save settings
        $(document).on('click', '#btn-save-settings', function() {
            self.saveSettings();
        });
    },

    loadBackups: function() {
        $.ajax({
            url: '/api/core',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({
                action: 'backup.list'
            }),
            headers: {
                'X-Requested-With': 'XMLHttpRequest'
            },
            success: (response) => {
                if (response.success || response.Success) {
                    const backups = response.data || response.Data || [];
                    this.renderBackupsTable(backups);
                } else {
                    this.showMessage('Failed to load backups: ' + (response.error || response.Error || 'Unknown error'), 'danger');
                    this.renderBackupsTable([]);
                }
            },
            error: (xhr, status, error) => {
                this.showMessage('Failed to load backups: ' + error, 'danger');
                this.renderBackupsTable([]);
            }
        });
    },

    renderBackupsTable: function(backups) {
        const tbody = $('#backupsTable tbody');
        tbody.empty();

        if (backups.length === 0) {
            tbody.html('<tr><td colspan="4" class="text-center text-muted">No backups found</td></tr>');
            return;
        }

        backups.forEach(backup => {
            const fileName = backup.fileName || backup.FileName || '';
            const createdAt = backup.createdAt || backup.CreatedAt || '';
            const description = backup.description || backup.Description || '<em class="text-muted">No description</em>';
            const size = backup.size || backup.Size || 0;
            const sizeFormatted = this.formatFileSize(size);
            const dateFormatted = createdAt ? new Date(createdAt).toLocaleString() : 'Unknown';

            tbody.append(`
                <tr>
                    <td>${dateFormatted}</td>
                    <td>${description}</td>
                    <td>${sizeFormatted}</td>
                    <td>
                        <div class="btn-group btn-group-sm">
                            <button type="button" class="btn btn-outline-success btn-download-backup" 
                                    data-filename="${fileName}" title="Download">
                                <svg width="14" height="14" fill="currentColor" viewBox="0 0 16 16">
                                    <path d="M.5 9.9a.5.5 0 0 1 .5.5v2.5a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1v-2.5a.5.5 0 0 1 1 0v2.5a2 2 0 0 1-2 2H2a2 2 0 0 1-2-2v-2.5a.5.5 0 0 1 .5-.5z"/>
                                    <path d="M7.646 11.854a.5.5 0 0 0 .708 0l3-3a.5.5 0 0 0-.708-.708L8.5 10.293V1.5a.5.5 0 0 0-1 0v8.793L5.354 8.146a.5.5 0 1 0-.708.708l3 3z"/>
                                </svg>
                            </button>
                            <button type="button" class="btn btn-outline-primary btn-restore-backup" 
                                    data-filename="${fileName}" title="Restore">
                                <svg width="14" height="14" fill="currentColor" viewBox="0 0 16 16">
                                    <path d="M8 3a5 5 0 1 0 4.546 2.914.5.5 0 0 1 .908-.417A6 6 0 1 1 8 2v1z"/>
                                    <path d="M8 4.466V.534a.25.25 0 0 1 .41-.192l2.36 1.966c.12.1.12.284 0 .384L8.41 4.658A.25.25 0 0 1 8 4.466z"/>
                                </svg>
                            </button>
                            <button type="button" class="btn btn-outline-danger btn-delete-backup" 
                                    data-filename="${fileName}" title="Delete">
                                <svg width="14" height="14" fill="currentColor" viewBox="0 0 16 16">
                                    <path d="M5.5 5.5A.5.5 0 0 1 6 6v6a.5.5 0 0 1-1 0V6a.5.5 0 0 1 .5-.5zm2.5 0a.5.5 0 0 1 .5.5v6a.5.5 0 0 1-1 0V6a.5.5 0 0 1 .5-.5zm3 .5a.5.5 0 0 0-1 0v6a.5.5 0 0 0 1 0V6z"/>
                                    <path fill-rule="evenodd" d="M14.5 3a1 1 0 0 1-1 1H13v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V4h-.5a1 1 0 0 1-1-1V2a1 1 0 0 1 1-1H6a1 1 0 0 1 1-1h2a1 1 0 0 1 1 1h3.5a1 1 0 0 1 1 1v1zM4.118 4 4 4.059V13a1 1 0 0 0 1 1h6a1 1 0 0 0 1-1V4.059L11.882 4H4.118zM2.5 3V2h11v1h-11z"/>
                                </svg>
                            </button>
                        </div>
                    </td>
                </tr>
            `);
        });
    },

    createBackup: function() {
        const description = $('#backup-description').val() || null;
        const modal = bootstrap.Modal.getInstance(document.getElementById('createBackupModal'));

        this.showMessage('Creating backup...', 'info');

        $.ajax({
            url: '/api/core',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({
                action: 'backup.create',
                payload: {
                    description: description
                }
            }),
            headers: {
                'X-Requested-With': 'XMLHttpRequest'
            },
            success: (response) => {
                if (response.success || response.Success) {
                    this.showMessage('Backup created successfully!', 'success');
                    modal.hide();
                    this.loadBackups();
                } else {
                    this.showMessage('Failed to create backup: ' + (response.error || response.Error || 'Unknown error'), 'danger');
                }
            },
            error: (xhr, status, error) => {
                this.showMessage('Failed to create backup: ' + error, 'danger');
            }
        });
    },

    restoreBackup: function(fileName) {
        if (!confirm(`Are you sure you want to restore from backup "${fileName}"?\n\nThis will:\n- Stop the Core service\n- Replace the current database\n- Restart all services\n\nA safety backup will be created before restore.`)) {
            return;
        }

        this.showMessage('Restoring backup... This may take a moment.', 'warning');

        $.ajax({
            url: '/api/core',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({
                action: 'backup.restore',
                payload: {
                    fileName: fileName
                }
            }),
            headers: {
                'X-Requested-With': 'XMLHttpRequest'
            },
            success: (response) => {
                if (response.success || response.Success) {
                    this.showMessage('Backup restored successfully! Services have been restarted. The page will reload in 5 seconds...', 'success');
                    setTimeout(() => {
                        window.location.reload();
                    }, 5000);
                } else {
                    this.showMessage('Failed to restore backup: ' + (response.error || response.Error || 'Unknown error'), 'danger');
                }
            },
            error: (xhr, status, error) => {
                this.showMessage('Failed to restore backup: ' + error, 'danger');
            }
        });
    },

    downloadBackup: function(fileName) {
        // Create a download link and trigger it
        const url = `/api/backup/download/${encodeURIComponent(fileName)}`;
        const link = document.createElement('a');
        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        this.showMessage('Download started...', 'info');
    },

    uploadBackup: function() {
        const fileInput = document.getElementById('backup-file');
        const file = fileInput.files[0];
        const description = $('#upload-description').val() || null;
        const modal = bootstrap.Modal.getInstance(document.getElementById('uploadBackupModal'));

        if (!file) {
            this.showMessage('Please select a backup file to upload', 'danger');
            return;
        }

        // Validate file extension
        if (!file.name.endsWith('.db.gz')) {
            this.showMessage('Invalid file type. Only .db.gz backup files are allowed.', 'danger');
            return;
        }

        // Validate file size (100 MB max)
        const maxSize = 100 * 1024 * 1024; // 100 MB
        if (file.size > maxSize) {
            this.showMessage('File size exceeds maximum allowed size of 100 MB', 'danger');
            return;
        }

        // Show progress
        $('#upload-progress').removeClass('d-none');
        $('#btn-confirm-upload-backup').prop('disabled', true);

        // Create form data
        const formData = new FormData();
        formData.append('file', file);
        if (description) {
            formData.append('description', description);
        }

        $.ajax({
            url: '/api/backup/upload',
            method: 'POST',
            data: formData,
            processData: false,
            contentType: false,
            headers: {
                'X-Requested-With': 'XMLHttpRequest'
            },
            success: (response) => {
                if (response.success) {
                    this.showMessage('Backup uploaded successfully!', 'success');
                    modal.hide();
                    this.loadBackups();
                } else {
                    this.showMessage('Failed to upload backup: ' + (response.error || 'Unknown error'), 'danger');
                }
            },
            error: (xhr, status, error) => {
                let errorMsg = error;
                if (xhr.responseJSON && xhr.responseJSON.error) {
                    errorMsg = xhr.responseJSON.error;
                }
                this.showMessage('Failed to upload backup: ' + errorMsg, 'danger');
            },
            complete: () => {
                $('#upload-progress').addClass('d-none');
                $('#btn-confirm-upload-backup').prop('disabled', false);
            }
        });
    },

    deleteBackup: function(fileName) {
        if (!confirm(`Are you sure you want to delete backup "${fileName}"?\n\nThis action cannot be undone.`)) {
            return;
        }

        $.ajax({
            url: '/api/core',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({
                action: 'backup.delete',
                payload: {
                    fileName: fileName
                }
            }),
            headers: {
                'X-Requested-With': 'XMLHttpRequest'
            },
            success: (response) => {
                if (response.success || response.Success) {
                    this.showMessage('Backup deleted successfully!', 'success');
                    this.loadBackups();
                } else {
                    this.showMessage('Failed to delete backup: ' + (response.error || response.Error || 'Unknown error'), 'danger');
                }
            },
            error: (xhr, status, error) => {
                this.showMessage('Failed to delete backup: ' + error, 'danger');
            }
        });
    },

    formatFileSize: function(bytes) {
        if (bytes === 0) return '0 B';
        const k = 1024;
        const sizes = ['B', 'KB', 'MB', 'GB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return Math.round(bytes / Math.pow(k, i) * 100) / 100 + ' ' + sizes[i];
    },

    showMessage: function(message, type) {
        const alertClass = 'alert-' + type;
        const $message = $('#backup-status-message');
        $message.removeClass('d-none alert-success alert-danger alert-warning alert-info')
                .addClass(alertClass)
                .html(`
                    <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-2">
                        <path d="M8 15A7 7 0 1 1 8 1a7 7 0 0 1 0 14zm0 1A8 8 0 1 0 8 0a8 8 0 0 0 0 16z"/>
                        <path d="m8.93 6.588-2.29.287-.082.38.45.083c.294.07.352.176.288.469l-.738 3.468c-.194.897.105 1.319.808 1.319.545 0 1.178-.252 1.465-.598l.088-.416c-.2.176-.492.246-.686.246-.275 0-.375-.193-.304-.533L8.93 6.588zM9 4.5a1 1 0 1 1-2 0 1 1 0 0 1 2 0z"/>
                    </svg>
                    ${message}
                `);
        
        setTimeout(() => {
            $message.addClass('d-none');
        }, 5000);
    },

    loadSettings: function() {
        $.ajax({
            url: '/api/core',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({
                action: 'backup.settings.get'
            }),
            headers: {
                'X-Requested-With': 'XMLHttpRequest'
            },
            success: (response) => {
                if (response.success || response.Success) {
                    const settings = response.data || response.Data || {};
                    this.renderSettings(settings);
                }
            },
            error: (xhr, status, error) => {
                console.error('Failed to load settings:', error);
            }
        });
    },

    renderSettings: function(settings) {
        $('#naming-pattern').val(settings.namingPattern || 'monolith-backup-{timestamp}');
        $('#include-database').prop('checked', settings.includeDatabase !== false);
        $('#include-config').prop('checked', settings.includeConfigFiles === true);
        $('#include-logs').prop('checked', settings.includeLogs === true);
        $('#max-backups').val(settings.maxBackups || 10);
        $('#auto-backup').prop('checked', settings.autoBackupEnabled === true);
        $('#auto-backup-interval').val(settings.autoBackupInterval || 24);
        
        if (settings.autoBackupEnabled) {
            $('#auto-backup-interval-group').show();
        }

        this.renderLocations(settings.additionalLocations || []);
    },

    renderLocations: function(locations) {
        const $list = $('#additional-locations-list');
        $list.empty();

        if (locations.length === 0) {
            $list.html('<p class="text-muted mb-0">No additional locations configured</p>');
            return;
        }

        locations.forEach(loc => {
            const $item = $(`
                <div class="card mb-2 location-item" data-id="${loc.id}">
                    <div class="card-body">
                        <div class="row align-items-center">
                            <div class="col-md-1">
                                <div class="form-check">
                                    <input class="form-check-input location-enabled" type="checkbox" 
                                           ${loc.enabled ? 'checked' : ''} data-id="${loc.id}">
                                </div>
                            </div>
                            <div class="col-md-4">
                                <input type="text" class="form-control form-control-sm location-path" 
                                       value="${loc.path || ''}" placeholder="/path/to/backup" data-id="${loc.id}">
                            </div>
                            <div class="col-md-5">
                                <input type="text" class="form-control form-control-sm location-desc" 
                                       value="${loc.description || ''}" placeholder="Description (optional)" data-id="${loc.id}">
                            </div>
                            <div class="col-md-2 text-end">
                                <button type="button" class="btn btn-sm btn-outline-danger btn-remove-location" 
                                        data-id="${loc.id}">
                                    <svg width="14" height="14" fill="currentColor" viewBox="0 0 16 16">
                                        <path d="M5.5 5.5A.5.5 0 0 1 6 6v6a.5.5 0 0 1-1 0V6a.5.5 0 0 1 .5-.5zm2.5 0a.5.5 0 0 1 .5.5v6a.5.5 0 0 1-1 0V6a.5.5 0 0 1 .5-.5zm3 .5a.5.5 0 0 0-1 0v6a.5.5 0 0 0 1 0V6z"/>
                                        <path fill-rule="evenodd" d="M14.5 3a1 1 0 0 1-1 1H13v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V4h-.5a1 1 0 0 1-1-1V2a1 1 0 0 1 1-1H6a1 1 0 0 1 1-1h2a1 1 0 0 1 1 1h3.5a1 1 0 0 1 1 1v1zM4.118 4 4 4.059V13a1 1 0 0 0 1 1h6a1 1 0 0 0 1-1V4.059L11.882 4H4.118zM2.5 3V2h11v1h-11z"/>
                                    </svg>
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            `);
            $list.append($item);
        });
    },

    addLocation: function() {
        const id = 'loc-' + Date.now();
        const locations = this.getLocationsFromUI();
        locations.push({
            id: id,
            path: '',
            enabled: true,
            description: ''
        });
        this.renderLocations(locations);
    },

    removeLocation: function(id) {
        const locations = this.getLocationsFromUI();
        const filtered = locations.filter(loc => loc.id !== id);
        this.renderLocations(filtered);
    },

    getLocationsFromUI: function() {
        const locations = [];
        $('.location-item').each(function() {
            const id = $(this).data('id');
            const path = $(this).find('.location-path').val();
            const enabled = $(this).find('.location-enabled').is(':checked');
            const description = $(this).find('.location-desc').val();
            if (path) {
                locations.push({ id, path, enabled, description });
            }
        });
        return locations;
    },

    saveSettings: function() {
        const settings = {
            namingPattern: $('#naming-pattern').val() || 'monolith-backup-{timestamp}',
            includeDatabase: $('#include-database').is(':checked'),
            includeConfigFiles: $('#include-config').is(':checked'),
            includeLogs: $('#include-logs').is(':checked'),
            maxBackups: parseInt($('#max-backups').val()) || 10,
            autoBackupEnabled: $('#auto-backup').is(':checked'),
            autoBackupInterval: parseInt($('#auto-backup-interval').val()) || 24,
            additionalLocations: this.getLocationsFromUI()
        };

        $.ajax({
            url: '/api/core',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({
                action: 'backup.settings.update',
                payload: {
                    settings: settings
                }
            }),
            headers: {
                'X-Requested-With': 'XMLHttpRequest'
            },
            success: (response) => {
                if (response.success || response.Success) {
                    this.showMessage('Settings saved successfully!', 'success');
                } else {
                    this.showMessage('Failed to save settings: ' + (response.error || response.Error || 'Unknown error'), 'danger');
                }
            },
            error: (xhr, status, error) => {
                this.showMessage('Failed to save settings: ' + error, 'danger');
            }
        });
    }
};
