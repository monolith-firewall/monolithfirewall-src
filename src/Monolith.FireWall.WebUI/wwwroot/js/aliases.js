// Firewall Aliases Module
var Aliases = {
    aliases: [],

    init: function() {
        console.log('Initializing Aliases module...');
        this.loadAliases();
        this.attachEventHandlers();
    },

    loadAliases: async function() {
        try {
            const response = await Monolith.API.get('/firewall/aliases');
            if (response.Success || response.success) {
                const data = response.Data || response.data || {};
                const items = data.items || data || [];
                this.aliases = items.map(a => this.normalizeAlias(a));
            } else {
                this.aliases = [];
            }
            this.renderAliases();
        } catch (error) {
            console.error('Error loading aliases:', error);
            this.showMessage('Failed to load aliases', 'error');
            this.aliases = [];
            this.renderAliases();
        }
    },

    renderAliases: function() {
        const tbody = $('#aliasesTable tbody');
        if (this.aliases.length === 0) {
            tbody.html('<tr><td colspan="6" class="text-center text-muted">No aliases configured</td></tr>');
            return;
        }

        let html = '';
        this.aliases.forEach(alias => {
            const statusBadge = alias.enabled 
                ? '<span class="badge bg-success">Enabled</span>'
                : '<span class="badge bg-secondary">Disabled</span>';
            
            const contentDisplay = Array.isArray(alias.content) 
                ? alias.content.slice(0, 3).join(', ') + (alias.content.length > 3 ? '...' : '')
                : alias.content || '-';
            
            html += `
                <tr>
                    <td><code>${alias.name}</code></td>
                    <td><span class="badge bg-info">${alias.type}</span></td>
                    <td>${contentDisplay}</td>
                    <td>${alias.description || '-'}</td>
                    <td>${statusBadge}</td>
                    <td>
                        <button class="btn btn-sm btn-outline-primary me-1" onclick="Aliases.editAlias(${alias.id})" title="Edit alias">
                            <svg width="14" height="14" fill="currentColor" viewBox="0 0 16 16">
                                <path d="M12.854.146a.5.5 0 0 0-.707 0L11.5 1.793 14.207 4.5l1.647-1.646a.5.5 0 0 0 0-.708l-3-3zm.646 6.061L9.793 2.5 3.293 9H3.5a.5.5 0 0 1 .5.5v.5h.5a.5.5 0 0 1 .5.5h.5v.5a.5.5 0 0 1 .5.5h.5v.5a.5.5 0 0 1 .5.5h.207l6.5-6.5zm-7.468 7.468A.5.5 0 0 1 6 13.5V13h-.5a.5.5 0 0 1-.5-.5V12h-.5a.5.5 0 0 1-.5-.5V11h-.5a.5.5 0 0 1-.5-.5V10h-.5a.499.499 0 0 1-.175-.032l-.179.178a.5.5 0 0 0-.11.168l-2 5a.5.5 0 0 0 .65.65l5-2a.5.5 0 0 0 .168-.11l.178-.179a.499.499 0 0 1-.032-.175z"/>
                            </svg>
                        </button>
                        <button class="btn btn-sm btn-outline-danger" onclick="Aliases.deleteAlias(${alias.id})" title="Delete alias">
                            <svg width="14" height="14" fill="currentColor" viewBox="0 0 16 16">
                                <path d="M5.5 5.5A.5.5 0 0 1 6 6v6a.5.5 0 0 1-1 0V6a.5.5 0 0 1 .5-.5zm2.5 0a.5.5 0 0 1 .5.5v6a.5.5 0 0 1-1 0V6a.5.5 0 0 1 .5-.5zm3 .5a.5.5 0 0 0-1 0v6a.5.5 0 0 0 1 0V6z"/>
                                <path fill-rule="evenodd" d="M14.5 3a1 1 0 0 1-1 1H13v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V4h-.5a1 1 0 0 1-1-1V2a1 1 0 0 1 1-1H6a1 1 0 0 1 1-1h2a1 1 0 0 1 1 1h3.5a1 1 0 0 1 1 1v1zM4.118 4 4 4.059V13a1 1 0 0 0 1 1h6a1 1 0 0 0 1-1V4.059L11.882 4H4.118zM2.5 3V2h11v1h-11z"/>
                            </svg>
                        </button>
                    </td>
                </tr>
            `;
        });
        tbody.html(html);
    },

    attachEventHandlers: function() {
        $(document).off('click', '#btnAddAlias');
        $(document).on('click', '#btnAddAlias', () => {
            this.showAddAliasModal();
        });

        $(document).off('click', '#btnApplyChanges');
        $(document).on('click', '#btnApplyChanges', () => {
            this.applyChanges();
        });

        $(document).off('click', '#btnDiscardChanges');
        $(document).on('click', '#btnDiscardChanges', () => {
            this.discardChanges();
        });
    },

    showAddAliasModal: function() {
        this.showAliasModal(null);
    },

    showAliasModal: function(alias) {
        const isEdit = alias !== null;
        const modalHtml = `
            <div class="modal fade" id="aliasModal" tabindex="-1" aria-labelledby="aliasModalLabel" aria-hidden="true">
                <div class="modal-dialog modal-lg">
                    <div class="modal-content">
                        <div class="modal-header">
                            <h5 class="modal-title" id="aliasModalLabel">${isEdit ? 'Edit' : 'Add'} Firewall Alias</h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                        </div>
                        <div class="modal-body">
                            <form id="aliasForm">
                                <div class="mb-3">
                                    <label for="aliasName" class="form-label">Name <span class="text-danger">*</span></label>
                                    <input type="text" class="form-control" id="aliasName" required 
                                           value="${alias ? alias.name : ''}" 
                                           placeholder="e.g., LAN_NETWORK">
                                    <small class="form-text text-muted">Unique identifier for this alias</small>
                                </div>
                                <div class="mb-3">
                                    <label for="aliasType" class="form-label">Type <span class="text-danger">*</span></label>
                                    <select class="form-select" id="aliasType" required>
                                        <option value="host" ${alias && alias.type === 'host' ? 'selected' : ''}>Host</option>
                                        <option value="network" ${alias && alias.type === 'network' ? 'selected' : ''}>Network</option>
                                        <option value="port" ${alias && alias.type === 'port' ? 'selected' : ''}>Port</option>
                                        <option value="url" ${alias && alias.type === 'url' ? 'selected' : ''}>URL</option>
                                    </select>
                                </div>
                                <div class="mb-3">
                                    <label for="aliasDescription" class="form-label">Description</label>
                                    <input type="text" class="form-control" id="aliasDescription" 
                                           value="${alias ? alias.description : ''}" 
                                           placeholder="Optional description">
                                </div>
                                <div class="mb-3">
                                    <label for="aliasContent" class="form-label">Content <span class="text-danger">*</span></label>
                                    <textarea class="form-control" id="aliasContent" rows="4" required 
                                              placeholder="Enter one item per line (e.g., IP addresses, networks, ports, or URLs)">${alias && alias.content ? alias.content.join('\n') : ''}</textarea>
                                    <small class="form-text text-muted">One item per line</small>
                                </div>
                                <div class="mb-3 form-check">
                                    <input type="checkbox" class="form-check-input" id="aliasEnabled" 
                                           ${alias && alias.enabled !== false ? 'checked' : ''}>
                                    <label class="form-check-label" for="aliasEnabled">
                                        Enabled
                                    </label>
                                </div>
                            </form>
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
                            <button type="button" class="btn btn-primary" onclick="Aliases.saveAlias(${alias ? alias.id : 'null'})">${isEdit ? 'Update' : 'Create'}</button>
                        </div>
                    </div>
                </div>
            </div>
        `;
        
        // Remove existing modal if any
        $('#aliasModal').remove();
        $('body').append(modalHtml);
        const modal = new bootstrap.Modal(document.getElementById('aliasModal'));
        modal.show();
        
        // Clean up on hide
        $('#aliasModal').on('hidden.bs.modal', function() {
            $(this).remove();
        });
    },

    editAlias: async function(id) {
        try {
            const response = await Monolith.API.get(`/firewall/aliases/${id}`);
            if (response.Success || response.success) {
                const alias = this.normalizeAlias(response.Data || response.data);
                this.showAliasModal(alias);
            } else {
                this.showMessage('Failed to load alias', 'error');
            }
        } catch (error) {
            console.error('Error loading alias:', error);
            this.showMessage('Failed to load alias', 'error');
        }
    },

    normalizeAlias: function(alias) {
        return {
            id: alias.Id || alias.id,
            name: alias.Name || alias.name,
            type: alias.Type || alias.type,
            description: alias.Description || alias.description,
            content: alias.Content || alias.content || [],
            enabled: alias.Enabled !== undefined ? alias.Enabled : (alias.enabled !== undefined ? alias.enabled : true)
        };
    },

    saveAlias: async function(id) {
        const form = document.getElementById('aliasForm');
        if (!form.checkValidity()) {
            form.reportValidity();
            return;
        }

        const alias = {
            name: $('#aliasName').val().trim(),
            type: $('#aliasType').val(),
            description: $('#aliasDescription').val().trim(),
            content: $('#aliasContent').val().split('\n').map(line => line.trim()).filter(line => line.length > 0),
            enabled: $('#aliasEnabled').is(':checked')
        };

        if (alias.content.length === 0) {
            this.showMessage('Content cannot be empty', 'error');
            return;
        }

        try {
            let response;
            if (id) {
                response = await Monolith.API.put(`/firewall/aliases/${id}`, alias);
            } else {
                response = await Monolith.API.post('/firewall/aliases', alias);
            }

            if (response.Success || response.success) {
                bootstrap.Modal.getInstance(document.getElementById('aliasModal')).hide();
                this.showMessage(id ? 'Alias updated successfully' : 'Alias created successfully', 'success');
                this.loadAliases();
                this.markPendingChanges();
            } else {
                this.showMessage(response.Error || response.error || 'Failed to save alias', 'error');
            }
        } catch (error) {
            console.error('Error saving alias:', error);
            this.showMessage('Failed to save alias', 'error');
        }
    },

    deleteAlias: async function(id) {
        if (!confirm('Are you sure you want to delete this alias? This action cannot be undone.')) {
            return;
        }

        try {
            const response = await Monolith.API.delete(`/firewall/aliases/${id}`);
            if (response.Success || response.success) {
                this.showMessage('Alias deleted successfully', 'success');
                this.loadAliases();
                this.markPendingChanges();
            } else {
                this.showMessage(response.Error || response.error || 'Failed to delete alias', 'error');
            }
        } catch (error) {
            console.error('Error deleting alias:', error);
            this.showMessage('Failed to delete alias', 'error');
        }
    },

    markPendingChanges: function() {
        // Show apply changes banner
        $('#pendingChangesBanner').removeClass('d-none');
    },

    applyChanges: async function() {
        if (!confirm('Apply all pending firewall changes? This will update the system configuration.')) {
            return;
        }

        try {
            const response = await Monolith.API.post('/firewall/apply', {});
            if (response.Success || response.success) {
                this.showMessage('Changes applied successfully', 'success');
                $('#pendingChangesBanner').addClass('d-none');
            } else {
                this.showMessage(response.Error || response.error || 'Failed to apply changes', 'error');
            }
        } catch (error) {
            console.error('Error applying changes:', error);
            this.showMessage('Failed to apply changes', 'error');
        }
    },

    discardChanges: async function() {
        if (!confirm('Discard all pending changes? This will revert all unsaved modifications.')) {
            return;
        }

        try {
            const response = await Monolith.API.post('/firewall/discard', {});
            if (response.Success || response.success) {
                this.showMessage('Changes discarded', 'info');
                $('#pendingChangesBanner').addClass('d-none');
                this.loadAliases(); // Reload to show current state
            } else {
                this.showMessage(response.Error || response.error || 'Failed to discard changes', 'error');
            }
        } catch (error) {
            console.error('Error discarding changes:', error);
            this.showMessage('Failed to discard changes', 'error');
        }
    },

    showMessage: function(message, type) {
        const alert = $('#aliasesStatusMessage');
        alert.removeClass('d-none alert-success alert-danger alert-warning alert-info')
             .addClass(`alert-${type}`)
             .text(message);
        setTimeout(() => alert.addClass('d-none'), 5000);
    }
};

// Register with Monolith.Pages.Firewall
if (typeof Monolith !== 'undefined') {
    Monolith.Pages = Monolith.Pages || {};
    Monolith.Pages.Firewall = Monolith.Pages.Firewall || {};
    Monolith.Pages.Firewall.Aliases = Aliases;
    // Also register at root level for backward compatibility
    Monolith.Pages.Aliases = Aliases;
}
