// Firewall Virtual IPs Module
var VirtualIps = {
    virtualIps: [],

    init: function() {
        console.log('Initializing Virtual IPs module...');
    },

    renderPage: function() {
        console.log('Rendering Virtual IPs page...');
        this.loadVirtualIps();
        this.attachEventHandlers();
    },

    loadVirtualIps: async function() {
        try {
            const response = await Monolith.API.get('/firewall/virtual-ips');
            if (response.success || response.Success) {
                const data = response.data || response.Data || {};
                const items = data.items || data || [];
                const vipArray = Array.isArray(items) ? items : [];
                this.virtualIps = vipArray.map(v => this.normalizeVirtualIp(v));
            } else {
                this.virtualIps = [];
            }
            this.renderVirtualIps();
        } catch (error) {
            console.error('Error loading virtual IPs:', error);
            this.showMessage('Failed to load virtual IPs', 'error');
            this.virtualIps = [];
            this.renderVirtualIps();
        }
    },

    normalizeVirtualIp: function(vip) {
        return {
            id: vip.Id || vip.id,
            name: vip.Name || vip.name,
            type: vip.Type || vip.type,
            interface: vip.Interface || vip.interface,
            address: vip.Address || vip.address,
            subnetBits: vip.SubnetBits || vip.subnetBits,
            description: vip.Description || vip.description,
            enabled: vip.Enabled !== undefined ? vip.Enabled : (vip.enabled !== undefined ? vip.enabled : true),
            vhid: vip.Vhid || vip.vhid,
            advskew: vip.Advskew || vip.advskew,
            carpPassword: vip.CarpPassword || vip.carpPassword
        };
    },

    renderVirtualIps: function() {
        const tbody = $('#virtualIpsTable tbody');
        if (!tbody.length) return;

        if (this.virtualIps.length === 0) {
            tbody.html('<tr><td colspan="7" class="text-center text-muted">No virtual IPs configured</td></tr>');
            return;
        }

        let html = '';
        this.virtualIps.forEach(vip => {
            const statusBadge = vip.enabled 
                ? '<span class="badge bg-success">Enabled</span>'
                : '<span class="badge bg-secondary">Disabled</span>';
            
            html += `
                <tr>
                    <td><code>${vip.name}</code></td>
                    <td><span class="badge bg-info">${vip.type}</span></td>
                    <td>${vip.interface}</td>
                    <td>${vip.address}/${vip.subnetBits}</td>
                    <td>${vip.description || '-'}</td>
                    <td>${statusBadge}</td>
                    <td>
                        <button class="btn btn-sm btn-outline-primary me-1" data-action="edit-vip" data-id="${vip.id}">Edit</button>
                        <button class="btn btn-sm btn-outline-danger" data-action="delete-vip" data-id="${vip.id}">Delete</button>
                    </td>
                </tr>
            `;
        });
        tbody.html(html);
    },

    attachEventHandlers: function() {
        $(document).off('click', '#btn-add-virtual-ip');
        $(document).on('click', '#btn-add-virtual-ip', () => {
            this.showAddVirtualIpModal();
        });

        $(document).off('click', '[data-action="edit-vip"]');
        $(document).on('click', '[data-action="edit-vip"]', (e) => {
            const id = $(e.currentTarget).data('id');
            this.editVirtualIp(id);
        });

        $(document).off('click', '[data-action="delete-vip"]');
        $(document).on('click', '[data-action="delete-vip"]', (e) => {
            const id = $(e.currentTarget).data('id');
            this.deleteVirtualIp(id);
        });
    },

    showAddVirtualIpModal: function() {
        this.showVirtualIpModal(null);
    },

    showVirtualIpModal: function(vip) {
        const isEdit = vip !== null;
        const isCarp = vip && vip.type === 'carp';
        const modalHtml = `
            <div class="modal fade" id="virtualIpModal" tabindex="-1" aria-labelledby="virtualIpModalLabel" aria-hidden="true">
                <div class="modal-dialog modal-lg">
                    <div class="modal-content">
                        <div class="modal-header">
                            <h5 class="modal-title" id="virtualIpModalLabel">${isEdit ? 'Edit' : 'Add'} Virtual IP</h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                        </div>
                        <div class="modal-body">
                            <form id="virtualIpForm">
                                <div class="mb-3">
                                    <label for="vipName" class="form-label">Name <span class="text-danger">*</span></label>
                                    <input type="text" class="form-control" id="vipName" required 
                                           value="${vip ? vip.name : ''}" 
                                           placeholder="e.g., VIP_WAN">
                                </div>
                                <div class="mb-3">
                                    <label for="vipType" class="form-label">Type <span class="text-danger">*</span></label>
                                    <select class="form-select" id="vipType" required>
                                        <option value="ipalias" ${vip && vip.type === 'ipalias' ? 'selected' : ''}>IP Alias</option>
                                        <option value="carp" ${vip && vip.type === 'carp' ? 'selected' : ''}>CARP</option>
                                        <option value="proxyarp" ${vip && vip.type === 'proxyarp' ? 'selected' : ''}>Proxy ARP</option>
                                        <option value="other" ${vip && vip.type === 'other' ? 'selected' : ''}>Other</option>
                                    </select>
                                </div>
                                <div class="mb-3">
                                    <label for="vipInterface" class="form-label">Interface <span class="text-danger">*</span></label>
                                    <input type="text" class="form-control" id="vipInterface" required 
                                           value="${vip ? vip.interface : ''}" 
                                           placeholder="e.g., eth0">
                                </div>
                                <div class="row">
                                    <div class="col-md-8 mb-3">
                                        <label for="vipAddress" class="form-label">Address <span class="text-danger">*</span></label>
                                        <input type="text" class="form-control" id="vipAddress" required 
                                               value="${vip ? vip.address : ''}" 
                                               placeholder="e.g., 192.168.1.100">
                                    </div>
                                    <div class="col-md-4 mb-3">
                                        <label for="vipSubnetBits" class="form-label">Subnet Bits</label>
                                        <input type="number" class="form-control" id="vipSubnetBits" min="0" max="32" 
                                               value="${vip ? vip.subnetBits : '24'}">
                                    </div>
                                </div>
                                <div id="carpFields" style="display: ${isCarp ? 'block' : 'none'};">
                                    <div class="row">
                                        <div class="col-md-4 mb-3">
                                            <label for="vipVhid" class="form-label">VHID</label>
                                            <input type="number" class="form-control" id="vipVhid" min="1" max="255" 
                                                   value="${vip && vip.vhid ? vip.vhid : ''}">
                                        </div>
                                        <div class="col-md-4 mb-3">
                                            <label for="vipAdvskew" class="form-label">Advskew</label>
                                            <input type="text" class="form-control" id="vipAdvskew" 
                                                   value="${vip && vip.advskew ? vip.advskew : ''}">
                                        </div>
                                        <div class="col-md-4 mb-3">
                                            <label for="vipCarpPassword" class="form-label">CARP Password</label>
                                            <input type="password" class="form-control" id="vipCarpPassword" 
                                                   value="${vip && vip.carpPassword ? vip.carpPassword : ''}">
                                        </div>
                                    </div>
                                </div>
                                <div class="mb-3">
                                    <label for="vipDescription" class="form-label">Description</label>
                                    <input type="text" class="form-control" id="vipDescription" 
                                           value="${vip ? vip.description : ''}" 
                                           placeholder="Optional description">
                                </div>
                                <div class="mb-3 form-check">
                                    <input type="checkbox" class="form-check-input" id="vipEnabled" 
                                           ${vip && vip.enabled !== false ? 'checked' : ''}>
                                    <label class="form-check-label" for="vipEnabled">
                                        Enabled
                                    </label>
                                </div>
                            </form>
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
                            <button type="button" class="btn btn-primary" data-action="save-vip-submit">${isEdit ? 'Update' : 'Create'}</button>
                        </div>
                    </div>
                </div>
            </div>
        `;
        
        $('#virtualIpModal').remove();
        $('body').append(modalHtml);
        const modal = new bootstrap.Modal(document.getElementById('virtualIpModal'));
        modal.show();

        $('#vipType').on('change', () => this.onTypeChange());

        $(document).off('click', '[data-action="save-vip-submit"]');
        $(document).on('click', '[data-action="save-vip-submit"]', () => {
            this.saveVirtualIp(vip ? vip.id : null);
        });
        
        $('#virtualIpModal').on('hidden.bs.modal', function() {
            $(this).remove();
        });
    },

    onTypeChange: function() {
        const type = $('#vipType').val();
        $('#carpFields').toggle(type === 'carp');
    },

    editVirtualIp: async function(id) {
        try {
            const response = await Monolith.API.get(`/firewall/virtual-ips/${id}`);
            if (response.success || response.Success) {
                const vip = this.normalizeVirtualIp(response.data || response.Data);
                this.showVirtualIpModal(vip);
            } else {
                this.showMessage('Failed to load virtual IP', 'error');
            }
        } catch (error) {
            console.error('Error loading virtual IP:', error);
            this.showMessage('Failed to load virtual IP', 'error');
        }
    },

    saveVirtualIp: async function(id) {
        const form = document.getElementById('virtualIpForm');
        if (!form || !form.checkValidity()) {
            if (form) form.reportValidity();
            return;
        }

        const vip = {
            name: $('#vipName').val().trim(),
            type: $('#vipType').val(),
            interface: $('#vipInterface').val().trim(),
            address: $('#vipAddress').val().trim(),
            subnetBits: parseInt($('#vipSubnetBits').val()) || 24,
            description: $('#vipDescription').val().trim(),
            enabled: $('#vipEnabled').is(':checked')
        };

        // Add CARP-specific fields if type is CARP
        if (vip.type === 'carp') {
            const vhid = $('#vipVhid').val();
            if (vhid) vip.vhid = parseInt(vhid);
            vip.advskew = $('#vipAdvskew').val().trim() || null;
            vip.carpPassword = $('#vipCarpPassword').val() || null;
        }

        try {
            let response;
            if (id) {
                response = await Monolith.API.put(`/firewall/virtual-ips/${id}`, vip);
            } else {
                response = await Monolith.API.post('/firewall/virtual-ips', vip);
            }

            if (response.success || response.Success) {
                const modalEl = document.getElementById('virtualIpModal');
                if (modalEl) {
                    const modal = bootstrap.Modal.getInstance(modalEl);
                    if (modal) modal.hide();
                }
                this.showMessage(id ? 'Virtual IP updated successfully' : 'Virtual IP created successfully', 'success');
                this.loadVirtualIps();
                this.markPendingChanges();
            } else {
                this.showMessage(response.error || response.Error || 'Failed to save virtual IP', 'error');
            }
        } catch (error) {
            console.error('Error saving virtual IP:', error);
            this.showMessage('Failed to save virtual IP', 'error');
        }
    },

    deleteVirtualIp: async function(id) {
        if (!confirm('Are you sure you want to delete this virtual IP? This action cannot be undone.')) {
            return;
        }

        try {
            const response = await Monolith.API.delete(`/firewall/virtual-ips/${id}`);
            if (response.success || response.Success) {
                this.showMessage('Virtual IP deleted successfully', 'success');
                this.loadVirtualIps();
                this.markPendingChanges();
            } else {
                this.showMessage(response.error || response.Error || 'Failed to delete virtual IP', 'error');
            }
        } catch (error) {
            console.error('Error deleting virtual IP:', error);
            this.showMessage('Failed to delete virtual IP', 'error');
        }
    },

    markPendingChanges: function() {
        $('#applyChangesBanner').removeClass('d-none');
    },

    applyChanges: async function() {
        if (!confirm('Apply all pending firewall changes? This will update the system configuration.')) {
            return;
        }

        try {
            const response = await Monolith.API.post('/firewall/apply', {});
            if (response.success || response.Success) {
                this.showMessage('Changes applied successfully', 'success');
                $('#applyChangesBanner').addClass('d-none');
            } else {
                this.showMessage(response.error || response.Error || 'Failed to apply changes', 'error');
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
            if (response.success || response.Success) {
                this.showMessage('Changes discarded', 'info');
                $('#applyChangesBanner').addClass('d-none');
                this.loadVirtualIps();
            } else {
                this.showMessage(response.error || response.Error || 'Failed to discard changes', 'error');
            }
        } catch (error) {
            console.error('Error discarding changes:', error);
            this.showMessage('Failed to discard changes', 'error');
        }
    },

    showMessage: function(message, type) {
        const alert = $('#virtualIpsStatusMessage');
        if (!alert.length) return;
        alert.removeClass('d-none alert-success alert-danger alert-warning alert-info')
             .addClass(`alert-${type}`)
             .text(message);
        setTimeout(() => alert.addClass('d-none'), 5000);
    }
};

// Register with Monolith.Pages
if (typeof Monolith !== 'undefined') {
    Monolith.Pages = Monolith.Pages || {};
    Monolith.Pages.Firewall = Monolith.Pages.Firewall || {};
    Monolith.Pages.Firewall.VirtualIps = VirtualIps;
}
