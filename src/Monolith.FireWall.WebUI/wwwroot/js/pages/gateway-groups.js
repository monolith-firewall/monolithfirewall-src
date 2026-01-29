// Gateway Groups Page - Multi-WAN failover and load balancing configuration

// Ensure Monolith.Core exists for API calls
if (!window.Monolith) window.Monolith = {};
if (!Monolith.Core) {
    Monolith.Core = {
        call: async function(action, payload) {
            try {
                var requestBody = { action: action };
                if (payload && Object.keys(payload).length > 0) {
                    requestBody.payload = payload;
                }
                var response = await Monolith.API.post('/api/core', requestBody);
                return {
                    success: response.success || response.Success || false,
                    data: response.data || response.Data || null,
                    error: response.error || response.Error || null
                };
            } catch (error) {
                console.error('Core API error:', error);
                return { success: false, data: null, error: error.message };
            }
        }
    };
}

var GatewayGroups = {
    groups: [],
    gateways: [],
    _signalRHandler: null,

    init: function() {
        console.log('Initializing Gateway Groups page...');
        this.render();
        this.loadData();
        this.attachHandlers();
        this._subscribeToSignalR();
    },

    destroy: function() {
        this._unsubscribeFromSignalR();
    },

    _subscribeToSignalR: function() {
        if (!Monolith.SignalR) return;

        this._signalRHandler = (eventName, data) => {
            switch (eventName) {
                case 'GatewayStatusChanged':
                    this.updateMemberHealth(data.gatewayId, data);
                    break;
                case 'GatewayGroupFailover':
                    this.handleFailoverEvent(data);
                    break;
            }
        };

        Monolith.SignalR.subscribe('gateways', this._signalRHandler);
        console.log('[GatewayGroups] Subscribed to SignalR');
    },

    _unsubscribeFromSignalR: function() {
        if (Monolith.SignalR && this._signalRHandler) {
            Monolith.SignalR.unsubscribe('gateways', this._signalRHandler);
            this._signalRHandler = null;
        }
    },

    render: function() {
        // Render standardized page header
        if (Monolith.PageHeader && typeof Monolith.PageHeader.render === 'function') {
            Monolith.PageHeader.render({
                title: "Gateway Groups",
                icon: "fa-diagram-3",
                description: "Configure multi-WAN failover and load balancing",
                container: '#gateway-groups-container',
                prepend: false
            });
        }

        var html = `
            <div class="d-flex justify-content-end mb-3">
                <button class="btn btn-primary" onclick="GatewayGroups.showCreateModal()">
                    <i class="fa-solid fa-plus me-1"></i>New Group
                </button>
            </div>

            <div class="card">
                <div class="card-body p-0">
                    <div class="table-responsive">
                        <table class="table table-hover mb-0" id="groups-table">
                            <thead class="table-light">
                                <tr>
                                    <th>Name</th>
                                    <th>Mode</th>
                                    <th>Members</th>
                                    <th>Status</th>
                                    <th>Active Gateway</th>
                                    <th class="text-end">Actions</th>
                                </tr>
                            </thead>
                            <tbody id="groups-body">
                                <tr><td colspan="6" class="text-center py-4"><div class="spinner-border spinner-border-sm"></div> Loading...</td></tr>
                            </tbody>
                        </table>
                    </div>
                </div>
            </div>

            <!-- Create/Edit Modal -->
            <div class="modal fade" id="group-modal" tabindex="-1">
                <div class="modal-dialog modal-lg">
                    <div class="modal-content">
                        <div class="modal-header">
                            <h5 class="modal-title" id="group-modal-title">New Gateway Group</h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                        </div>
                        <div class="modal-body">
                            <form id="group-form">
                                <input type="hidden" id="group-id" />

                                <div class="row mb-3">
                                    <div class="col-md-6">
                                        <label class="form-label">Name</label>
                                        <input type="text" class="form-control" id="group-name" required />
                                    </div>
                                    <div class="col-md-6">
                                        <label class="form-label">Mode</label>
                                        <select class="form-select" id="group-mode">
                                            <option value="failover">Failover</option>
                                            <option value="loadbalance">Load Balance</option>
                                            <option value="weighted">Weighted</option>
                                        </select>
                                    </div>
                                </div>

                                <div class="mb-3">
                                    <label class="form-label">Description</label>
                                    <input type="text" class="form-control" id="group-description" />
                                </div>

                                <div class="row mb-3">
                                    <div class="col-md-6">
                                        <label class="form-label">Trigger Level</label>
                                        <select class="form-select" id="group-trigger">
                                            <option value="member_down">Member Down</option>
                                            <option value="packet_loss">Packet Loss</option>
                                            <option value="latency_high">High Latency</option>
                                            <option value="any">Any Issue</option>
                                        </select>
                                    </div>
                                    <div class="col-md-3">
                                        <label class="form-label">Packet Loss %</label>
                                        <input type="number" class="form-control" id="group-packet-loss" value="20" min="1" max="100" />
                                    </div>
                                    <div class="col-md-3">
                                        <label class="form-label">Latency (ms)</label>
                                        <input type="number" class="form-control" id="group-latency" value="500" min="1" />
                                    </div>
                                </div>

                                <hr />
                                <h6>Members</h6>
                                <div id="members-container" class="mb-3"></div>
                                <button type="button" class="btn btn-outline-secondary btn-sm" onclick="GatewayGroups.addMemberRow()">
                                    <i class="fa-solid fa-plus me-1"></i>Add Member
                                </button>
                            </form>
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
                            <button type="button" class="btn btn-primary" onclick="GatewayGroups.saveGroup()">Save</button>
                        </div>
                    </div>
                </div>
            </div>
        `;
        $('#gateway-groups-container').append(html);
    },

    attachHandlers: function() {
        $('#group-mode').on('change', function() {
            GatewayGroups.updateMemberOptions();
        });
    },

    loadData: async function() {
        try {
            // Load gateways first for the dropdown
            const gwResponse = await Monolith.Core.call('routing.gateways.list', {});
            this.gateways = gwResponse.data || [];

            // Load groups
            const response = await Monolith.Core.call('gateway.groups.list', {});
            this.groups = response.data || [];
            this.renderGroups();
        } catch (error) {
            console.error('Failed to load gateway groups:', error);
            $('#groups-body').html('<tr><td colspan="6" class="text-center text-danger">Failed to load data</td></tr>');
        }
    },

    renderGroups: function() {
        if (this.groups.length === 0) {
            $('#groups-body').html('<tr><td colspan="6" class="text-center text-muted py-4">No gateway groups configured</td></tr>');
            return;
        }

        var html = this.groups.map(group => {
            var modeLabel = { 'failover': 'Failover', 'loadbalance': 'Load Balance', 'weighted': 'Weighted' }[group.mode] || group.mode;
            var memberCount = group.members ? group.members.length : 0;
            var healthyCount = group.currentStatus ? group.currentStatus.healthyMemberCount : 0;
            var activeTier = group.currentStatus ? group.currentStatus.activeTier : '-';
            var activeGateway = this.getActiveGatewayName(group);

            var statusClass = 'bg-secondary';
            var statusText = 'Unknown';
            if (memberCount > 0) {
                if (healthyCount === memberCount) {
                    statusClass = 'bg-success';
                    statusText = 'Healthy';
                } else if (healthyCount > 0) {
                    statusClass = 'bg-warning';
                    statusText = 'Degraded';
                } else {
                    statusClass = 'bg-danger';
                    statusText = 'Down';
                }
            }

            return `
                <tr data-group-id="${group.id}">
                    <td>
                        <strong>${this.escapeHtml(group.name)}</strong>
                        ${group.description ? `<br><small class="text-muted">${this.escapeHtml(group.description)}</small>` : ''}
                    </td>
                    <td><span class="badge bg-info">${modeLabel}</span></td>
                    <td>${healthyCount}/${memberCount} healthy</td>
                    <td><span class="badge ${statusClass}">${statusText}</span></td>
                    <td>${activeGateway}</td>
                    <td class="text-end">
                        <button class="btn btn-sm btn-outline-primary me-1" onclick="GatewayGroups.editGroup(${group.id})" title="Edit">
                            <i class="fa-solid fa-pencil"></i>
                        </button>
                        <button class="btn btn-sm btn-outline-danger" onclick="GatewayGroups.deleteGroup(${group.id})" title="Delete">
                            <i class="fa-solid fa-trash"></i>
                        </button>
                    </td>
                </tr>
            `;
        }).join('');

        $('#groups-body').html(html);
    },

    getActiveGatewayName: function(group) {
        if (!group.currentStatus || !group.currentStatus.activeGatewayIds || group.currentStatus.activeGatewayIds.length === 0) {
            return '-';
        }

        var names = group.currentStatus.activeGatewayIds.map(id => {
            var gw = this.gateways.find(g => g.id === id);
            return gw ? gw.name : `#${id}`;
        });

        return names.join(', ');
    },

    showCreateModal: function() {
        $('#group-modal-title').text('New Gateway Group');
        $('#group-id').val('');
        $('#group-name').val('');
        $('#group-description').val('');
        $('#group-mode').val('failover');
        $('#group-trigger').val('member_down');
        $('#group-packet-loss').val('20');
        $('#group-latency').val('500');
        $('#members-container').html('');
        this.addMemberRow();
        new bootstrap.Modal('#group-modal').show();
    },

    editGroup: function(id) {
        var group = this.groups.find(g => g.id === id);
        if (!group) return;

        $('#group-modal-title').text('Edit Gateway Group');
        $('#group-id').val(group.id);
        $('#group-name').val(group.name);
        $('#group-description').val(group.description || '');
        $('#group-mode').val(group.mode);
        $('#group-trigger').val(group.triggerLevel);
        $('#group-packet-loss').val(group.packetLossThreshold || 20);
        $('#group-latency').val(group.latencyThresholdMs || 500);

        $('#members-container').html('');
        if (group.members && group.members.length > 0) {
            group.members.forEach(m => this.addMemberRow(m));
        } else {
            this.addMemberRow();
        }

        new bootstrap.Modal('#group-modal').show();
    },

    addMemberRow: function(member) {
        var gatewayOptions = this.gateways.map(g =>
            `<option value="${g.id}" ${member && member.gatewayId === g.id ? 'selected' : ''}>${this.escapeHtml(g.name)}</option>`
        ).join('');

        var mode = $('#group-mode').val();
        var showWeight = mode === 'weighted' || mode === 'loadbalance';

        var html = `
            <div class="member-row d-flex gap-2 mb-2 align-items-center">
                <select class="form-select member-gateway" style="flex:2">
                    <option value="">-- Select Gateway --</option>
                    ${gatewayOptions}
                </select>
                <input type="number" class="form-control member-tier" placeholder="Tier" value="${member ? member.tier : 1}" min="1" max="10" style="width:80px" title="Tier (1=primary)" />
                <input type="number" class="form-control member-weight ${showWeight ? '' : 'd-none'}" placeholder="Weight" value="${member ? member.weight : 1}" min="1" max="100" style="width:90px" title="Weight" />
                <input type="number" class="form-control member-priority" placeholder="Priority" value="${member ? member.priority : 0}" min="0" style="width:90px" title="Priority within tier" />
                <button type="button" class="btn btn-outline-danger btn-sm" onclick="$(this).closest('.member-row').remove()">
                    <i class="fa-solid fa-xmark"></i>
                </button>
            </div>
        `;
        $('#members-container').append(html);
    },

    updateMemberOptions: function() {
        var mode = $('#group-mode').val();
        var showWeight = mode === 'weighted' || mode === 'loadbalance';
        $('.member-weight').toggleClass('d-none', !showWeight);
    },

    saveGroup: async function() {
        var id = $('#group-id').val();
        var name = $('#group-name').val().trim();
        if (!name) {
            alert('Name is required');
            return;
        }

        var members = [];
        $('.member-row').each(function() {
            var gatewayId = parseInt($(this).find('.member-gateway').val());
            if (gatewayId) {
                members.push({
                    gatewayId: gatewayId,
                    tier: parseInt($(this).find('.member-tier').val()) || 1,
                    weight: parseInt($(this).find('.member-weight').val()) || 1,
                    priority: parseInt($(this).find('.member-priority').val()) || 0
                });
            }
        });

        if (members.length === 0) {
            alert('At least one member is required');
            return;
        }

        var payload = {
            name: name,
            description: $('#group-description').val().trim() || null,
            mode: $('#group-mode').val(),
            triggerLevel: $('#group-trigger').val(),
            packetLossThreshold: parseInt($('#group-packet-loss').val()) || 20,
            latencyThresholdMs: parseInt($('#group-latency').val()) || 500,
            enabled: true,
            members: members
        };

        try {
            var action = id ? 'gateway.groups.update' : 'gateway.groups.create';
            if (id) payload.id = parseInt(id);

            var response = await Monolith.Core.call(action, payload);
            if (response.success) {
                bootstrap.Modal.getInstance('#group-modal').hide();
                this.loadData();
                Monolith.Toast.success(id ? 'Gateway group updated' : 'Gateway group created');
            } else {
                alert('Error: ' + (response.error || 'Unknown error'));
            }
        } catch (error) {
            console.error('Failed to save group:', error);
            alert('Failed to save gateway group');
        }
    },

    deleteGroup: async function(id) {
        var group = this.groups.find(g => g.id === id);
        if (!group) return;

        if (!confirm(`Delete gateway group "${group.name}"?`)) return;

        try {
            var response = await Monolith.Core.call('gateway.groups.delete', { id: id });
            if (response.success) {
                this.loadData();
                Monolith.Toast.success('Gateway group deleted');
            } else {
                alert('Error: ' + (response.error || 'Unknown error'));
            }
        } catch (error) {
            console.error('Failed to delete group:', error);
            alert('Failed to delete gateway group');
        }
    },

    updateMemberHealth: function(gatewayId, status) {
        // Update health indicators for this gateway in all groups
        this.groups.forEach(group => {
            if (group.members) {
                var member = group.members.find(m => m.gatewayId === gatewayId);
                if (member && member.health) {
                    member.health.status = status.status;
                    member.health.latencyMs = status.latencyMs;
                }
            }
        });
        this.renderGroups();
    },

    handleFailoverEvent: function(data) {
        console.log('[GatewayGroups] Failover event:', data);
        Monolith.Toast.warning(`Gateway group "${data.groupName}" failed over from tier ${data.previousTier} to tier ${data.newTier}`);
        this.loadData();
    },

    escapeHtml: function(text) {
        if (!text) return '';
        var div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
};

// Initialize when DOM is ready
$(document).ready(function() {
    GatewayGroups.init();
});

// Cleanup on page unload
$(window).on('beforeunload', function() {
    GatewayGroups.destroy();
});
