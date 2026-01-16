// Firewall Traffic Shaper Module
var TrafficShaper = {
    rules: [],

    init: function() {
        console.log('Initializing Traffic Shaper module...');
    },

    renderPage: function() {
        console.log('Rendering Traffic Shaper page...');
        this.loadRules();
        this.attachEventHandlers();
    },

    loadRules: async function() {
        try {
            const response = await Monolith.API.get('/firewall/traffic-shaper');
            if (response.success || response.Success) {
                const data = response.data || response.Data || {};
                const items = data.items || data || [];
                const rulesArray = Array.isArray(items) ? items : (Array.isArray(data) ? data : []);
                this.rules = rulesArray.map(r => this.normalizeRule(r));
            } else {
                this.rules = [];
            }
            this.renderRules();
        } catch (error) {
            console.error('Error loading traffic shaper rules:', error);
            this.showMessage('Failed to load traffic shaper rules', 'error');
            this.rules = [];
            this.renderRules();
        }
    },

    normalizeRule: function(rule) {
        return {
            id: rule.Id || rule.id,
            name: rule.Name || rule.name,
            interface: rule.Interface || rule.interface,
            bandwidthUp: rule.BandwidthUp || rule.bandwidthUp,
            bandwidthDown: rule.BandwidthDown || rule.bandwidthDown,
            scheduler: rule.Scheduler || rule.scheduler,
            description: rule.Description || rule.description,
            enabled: rule.Enabled !== undefined ? rule.Enabled : (rule.enabled !== undefined ? rule.enabled : true)
        };
    },

    renderRules: function() {
        const tbody = $('#trafficShaperRulesTable tbody');
        if (!tbody.length) return;
        
        if (this.rules.length === 0) {
            tbody.html('<tr><td colspan="8" class="text-center text-muted">No traffic shaper rules configured</td></tr>');
            return;
        }

        let html = '';
        this.rules.forEach(rule => {
            const statusBadge = rule.enabled 
                ? '<span class="badge bg-success">Enabled</span>'
                : '<span class="badge bg-secondary">Disabled</span>';
            
            html += `
                <tr>
                    <td><code>${rule.name}</code></td>
                    <td>${rule.interface}</td>
                    <td>${this.formatBandwidth(rule.bandwidthUp)}</td>
                    <td>${this.formatBandwidth(rule.bandwidthDown)}</td>
                    <td><span class="badge bg-info">${rule.scheduler}</span></td>
                    <td>${rule.description || '-'}</td>
                    <td>${statusBadge}</td>
                    <td>
                        <button class="btn btn-sm btn-outline-primary me-1" data-action="edit-shaper" data-id="${rule.id}">Edit</button>
                        <button class="btn btn-sm btn-outline-danger" data-action="delete-shaper" data-id="${rule.id}">Delete</button>
                    </td>
                </tr>
            `;
        });
        tbody.html(html);
    },

    formatBandwidth: function(kbps) {
        if (kbps >= 1000) {
            return `${(kbps / 1000).toFixed(1)} Mbps`;
        }
        return `${kbps} Kbps`;
    },

    attachEventHandlers: function() {
        $(document).off('click', '#btn-add-shaper-rule');
        $(document).on('click', '#btn-add-shaper-rule', () => {
            this.showAddRuleModal();
        });

        $(document).off('click', '[data-action="edit-shaper"]');
        $(document).on('click', '[data-action="edit-shaper"]', (e) => {
            const id = $(e.currentTarget).data('id');
            this.editRule(id);
        });

        $(document).off('click', '[data-action="delete-shaper"]');
        $(document).on('click', '[data-action="delete-shaper"]', (e) => {
            const id = $(e.currentTarget).data('id');
            this.deleteRule(id);
        });
    },

    showAddRuleModal: function() {
        this.showRuleModal(null);
    },

    showRuleModal: function(rule) {
        const isEdit = rule !== null;
        const modalHtml = `
            <div class="modal fade" id="trafficShaperRuleModal" tabindex="-1" aria-labelledby="trafficShaperRuleModalLabel" aria-hidden="true">
                <div class="modal-dialog modal-lg">
                    <div class="modal-content">
                        <div class="modal-header">
                            <h5 class="modal-title" id="trafficShaperRuleModalLabel">${isEdit ? 'Edit' : 'Add'} Traffic Shaper Rule</h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                        </div>
                        <div class="modal-body">
                            <form id="trafficShaperRuleForm">
                                <div class="mb-3">
                                    <label for="shaperName" class="form-label">Name <span class="text-danger">*</span></label>
                                    <input type="text" class="form-control" id="shaperName" required 
                                           value="${rule ? rule.name : ''}" 
                                           placeholder="e.g., WAN_LIMIT">
                                </div>
                                <div class="mb-3">
                                    <label for="shaperInterface" class="form-label">Interface <span class="text-danger">*</span></label>
                                    <input type="text" class="form-control" id="shaperInterface" required 
                                           value="${rule ? rule.interface : ''}" 
                                           placeholder="e.g., eth0">
                                </div>
                                <div class="row">
                                    <div class="col-md-6 mb-3">
                                        <label for="shaperBandwidthUp" class="form-label">Bandwidth Up (Kbps) <span class="text-danger">*</span></label>
                                        <input type="number" class="form-control" id="shaperBandwidthUp" required min="1" 
                                               value="${rule ? rule.bandwidthUp : '1000'}">
                                        <small class="form-text text-muted">Upload bandwidth limit in Kbps</small>
                                    </div>
                                    <div class="col-md-6 mb-3">
                                        <label for="shaperBandwidthDown" class="form-label">Bandwidth Down (Kbps) <span class="text-danger">*</span></label>
                                        <input type="number" class="form-control" id="shaperBandwidthDown" required min="1" 
                                               value="${rule ? rule.bandwidthDown : '1000'}">
                                        <small class="form-text text-muted">Download bandwidth limit in Kbps</small>
                                    </div>
                                </div>
                                <div class="mb-3">
                                    <label for="shaperScheduler" class="form-label">Scheduler <span class="text-danger">*</span></label>
                                    <select class="form-select" id="shaperScheduler" required>
                                        <option value="fq_codel" ${rule && rule.scheduler === 'fq_codel' ? 'selected' : ''}>FQ-CoDel</option>
                                        <option value="hfsc" ${rule && rule.scheduler === 'hfsc' ? 'selected' : ''}>HFSC</option>
                                        <option value="cbq" ${rule && rule.scheduler === 'cbq' ? 'selected' : ''}>CBQ</option>
                                        <option value="priq" ${rule && rule.scheduler === 'priq' ? 'selected' : ''}>PRIQ</option>
                                    </select>
                                </div>
                                <div class="mb-3">
                                    <label for="shaperDescription" class="form-label">Description</label>
                                    <input type="text" class="form-control" id="shaperDescription" 
                                           value="${rule ? rule.description : ''}" 
                                           placeholder="Optional description">
                                </div>
                                <div class="mb-3 form-check">
                                    <input type="checkbox" class="form-check-input" id="shaperEnabled" 
                                           ${rule && rule.enabled !== false ? 'checked' : ''}>
                                    <label class="form-check-label" for="shaperEnabled">
                                        Enabled
                                    </label>
                                </div>
                            </form>
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
                            <button type="button" class="btn btn-primary" data-action="save-shaper-submit">${isEdit ? 'Update' : 'Create'}</button>
                        </div>
                    </div>
                </div>
            </div>
        `;
        
        $('#trafficShaperRuleModal').remove();
        $('body').append(modalHtml);
        const modal = new bootstrap.Modal(document.getElementById('trafficShaperRuleModal'));
        modal.show();

        $(document).off('click', '[data-action="save-shaper-submit"]');
        $(document).on('click', '[data-action="save-shaper-submit"]', () => {
            this.saveRule(rule ? rule.id : null);
        });
        
        $('#trafficShaperRuleModal').on('hidden.bs.modal', function() {
            $(this).remove();
        });
    },

    editRule: async function(id) {
        try {
            const response = await Monolith.API.get(`/firewall/traffic-shaper/${id}`);
            if (response.success || response.Success) {
                const rule = this.normalizeRule(response.data || response.Data);
                this.showRuleModal(rule);
            } else {
                this.showMessage('Failed to load traffic shaper rule', 'error');
            }
        } catch (error) {
            console.error('Error loading traffic shaper rule:', error);
            this.showMessage('Failed to load traffic shaper rule', 'error');
        }
    },

    saveRule: async function(id) {
        const form = document.getElementById('trafficShaperRuleForm');
        if (!form || !form.checkValidity()) {
            if (form) form.reportValidity();
            return;
        }

        const rule = {
            name: $('#shaperName').val().trim(),
            interface: $('#shaperInterface').val().trim(),
            bandwidthUp: parseInt($('#shaperBandwidthUp').val()) || 1000,
            bandwidthDown: parseInt($('#shaperBandwidthDown').val()) || 1000,
            scheduler: $('#shaperScheduler').val(),
            description: $('#shaperDescription').val().trim(),
            enabled: $('#shaperEnabled').is(':checked')
        };

        try {
            let response;
            if (id) {
                response = await Monolith.API.put(`/firewall/traffic-shaper/${id}`, rule);
            } else {
                response = await Monolith.API.post('/firewall/traffic-shaper', rule);
            }

            if (response.success || response.Success) {
                const modalEl = document.getElementById('trafficShaperRuleModal');
                if (modalEl) {
                    const modal = bootstrap.Modal.getInstance(modalEl);
                    if (modal) modal.hide();
                }
                this.showMessage(id ? 'Traffic shaper rule updated successfully' : 'Traffic shaper rule created successfully', 'success');
                this.loadRules();
                this.markPendingChanges();
            } else {
                this.showMessage(response.error || response.Error || 'Failed to save traffic shaper rule', 'error');
            }
        } catch (error) {
            console.error('Error saving traffic shaper rule:', error);
            this.showMessage('Failed to save traffic shaper rule', 'error');
        }
    },

    deleteRule: async function(id) {
        if (!confirm('Are you sure you want to delete this traffic shaper rule? This action cannot be undone.')) {
            return;
        }

        try {
            const response = await Monolith.API.delete(`/firewall/traffic-shaper/${id}`);
            if (response.success || response.Success) {
                this.showMessage('Traffic shaper rule deleted successfully', 'success');
                this.loadRules();
                this.markPendingChanges();
            } else {
                this.showMessage(response.error || response.Error || 'Failed to delete traffic shaper rule', 'error');
            }
        } catch (error) {
            console.error('Error deleting traffic shaper rule:', error);
            this.showMessage('Failed to delete traffic shaper rule', 'error');
        }
    },

    markPendingChanges: function() {
        $('#applyChangesBanner').removeClass('d-none');
    },

    showMessage: function(message, type) {
        const alert = $('#trafficShaperStatusMessage');
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
    Monolith.Pages.Firewall.TrafficShaper = TrafficShaper;
}
