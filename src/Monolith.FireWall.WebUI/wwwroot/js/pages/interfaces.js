// Interfaces Page
var Interfaces = {
    assignments: [],
    vlans: [],
    bridges: [],
    unassigned: [],
    availableInterfaces: [],

    init: function() {
        console.log('Initializing Interfaces page...');
        this.render();
        this.loadData();
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
                                <path d="M0 8a8 8 0 1 1 16 0A8 8 0 0 1 0 8zm7.5-6.923c-.67.204-1.335.82-1.887 1.855A7.97 7.97 0 0 0 5.145 4H7.5V1.077zM4.09 4a9.267 9.267 0 0 1 .64-1.539 6.7 6.7 0 0 1 .597-.933A7.025 7.025 0 0 0 2.255 4H4.09zm-.582 3.5c.03-.877.138-1.718.312-2.5H1.674a6.958 6.958 0 0 0-.656 2.5h2.49zM4.847 5a12.5 12.5 0 0 0-.338 2.5H7.5V5H4.847zM8.5 5v2.5h2.99a12.495 12.495 0 0 0-.337-2.5H8.5zM4.51 8.5a12.5 12.5 0 0 0 .337 2.5H7.5V8.5H4.51zm3.99 0V11h2.653c.187-.765.306-1.608.338-2.5H8.5zM5.145 12c.138.386.295.744.468 1.068.552 1.035 1.218 1.65 1.887 1.855V12H5.145zm.182 2.472a6.696 6.696 0 0 1-.597-.933A9.268 9.268 0 0 1 4.09 12H2.255a7.024 7.024 0 0 0 3.072 2.472zM3.82 11a13.652 13.652 0 0 1-.312-2.5h-2.49c.062.89.291 1.733.656 2.5H3.82zm6.853 3.472A7.024 7.024 0 0 0 13.745 12H11.91a9.27 9.27 0 0 1-.64 1.539 6.688 6.688 0 0 1-.597.933zM8.5 12v2.923c.67-.204 1.335-.82 1.887-1.855.173-.324.33-.682.468-1.068H8.5zm3.68-1h2.146c.365-.767.594-1.61.656-2.5h-2.49a13.65 13.65 0 0 1-.312 2.5zm2.802-3.5a6.959 6.959 0 0 0-.656-2.5H12.18c.174.782.282 1.623.312 2.5h2.49zM11.27 2.461c.247.464.462.98.64 1.539h1.835a7.024 7.024 0 0 0-3.072-2.472c.218.284.418.598.597.933zM10.855 4a7.966 7.966 0 0 0-.468-1.068C9.835 1.897 9.17 1.282 8.5 1.077V4h2.355z"/>
                            </svg>
                            Interfaces
                        </h2>
                        <p class="text-muted">Network interface assignments, VLANs, and bridge configuration</p>
                    </div>
                </div>

                <!-- Status Messages -->
                <div id="interfacesStatusMessage" class="alert d-none"></div>

                <!-- Main Tabs -->
                <ul class="nav nav-tabs mb-4" id="interfacesTabs" role="tablist">
                    <li class="nav-item" role="presentation">
                        <button class="nav-link active" id="assignments-tab" data-bs-toggle="tab" data-bs-target="#assignments" 
                                type="button" role="tab" aria-controls="assignments" aria-selected="true">
                            <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-1">
                                <path d="M12 0H4a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h8a2 2 0 0 0 2-2V2a2 2 0 0 0-2-2zM5 4h6a.5.5 0 0 1 0 1H5a.5.5 0 0 1 0-1zm-.5 2.5A.5.5 0 0 1 5 6h6a.5.5 0 0 1 0 1H5a.5.5 0 0 1-.5-.5zM5 8h6a.5.5 0 0 1 0 1H5a.5.5 0 0 1 0-1zm0 2h3a.5.5 0 0 1 0 1H5a.5.5 0 0 1 0-1z"/>
                            </svg>
                            Interface Assignments
                        </button>
                    </li>
                    <li class="nav-item" role="presentation">
                        <button class="nav-link" id="vlans-tab" data-bs-toggle="tab" data-bs-target="#vlans" 
                                type="button" role="tab" aria-controls="vlans" aria-selected="false">
                            <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-1">
                                <path d="M14 1a1 1 0 0 1 1 1v12a1 1 0 0 1-1 1H2a1 1 0 0 1-1-1V2a1 1 0 0 1 1-1h12zM2 0a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V2a2 2 0 0 0-2-2H2z"/>
                                <path d="M5 8a.5.5 0 0 1 .5-.5h7a.5.5 0 0 1 0 1h-7A.5.5 0 0 1 5 8zm0-2.5a.5.5 0 0 1 .5-.5h7a.5.5 0 0 1 0 1h-7a.5.5 0 0 1-.5-.5zm0 5a.5.5 0 0 1 .5-.5h7a.5.5 0 0 1 0 1h-7a.5.5 0 0 1-.5-.5zm-1-5a.5.5 0 1 1-1 0 .5.5 0 0 1 1 0zM4 8a.5.5 0 1 1-1 0 .5.5 0 0 1 1 0zm0 2.5a.5.5 0 1 1-1 0 .5.5 0 0 1 1 0z"/>
                            </svg>
                            VLANs
                        </button>
                    </li>
                    <li class="nav-item" role="presentation">
                        <button class="nav-link" id="bridges-tab" data-bs-toggle="tab" data-bs-target="#bridges" 
                                type="button" role="tab" aria-controls="bridges" aria-selected="false">
                            <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-1">
                                <path d="M8 15A7 7 0 1 1 8 1a7 7 0 0 1 0 14zm0 1A8 8 0 1 0 8 0a8 8 0 0 0 0 16z"/>
                                <path d="M5.146 8.146a.5.5 0 0 1 .708 0L8 10.793l2.146-2.647a.5.5 0 0 1 .708.708l-2.5 3a.5.5 0 0 1-.708 0l-2.5-3a.5.5 0 0 1 0-.708z"/>
                            </svg>
                            Bridges
                        </button>
                    </li>
                </ul>

                <!-- Tab Content -->
                <div class="tab-content" id="interfacesTabContent">
                    <!-- Interface Assignments Tab -->
                    <div class="tab-pane fade show active" id="assignments" role="tabpanel" aria-labelledby="assignments-tab">
                        <div class="card mb-4">
                            <div class="card-header d-flex justify-content-between align-items-center">
                                <h5 class="mb-0">Unassigned Interfaces</h5>
                                <span class="text-muted small">Not managed by Monolith</span>
                            </div>
                            <div class="card-body">
                                <div class="table-responsive">
                                    <table class="table table-hover" id="unassignedTable">
                                        <thead>
                                            <tr>
                                                <th>Interface</th>
                                                <th>MAC</th>
                                                <th>Status</th>
                                                <th>IP Address</th>
                                                <th>Actions</th>
                                            </tr>
                                        </thead>
                                        <tbody>
                                            <tr>
                                                <td colspan="5" class="text-center text-muted">
                                                    <div class="spinner-border spinner-border-sm me-2" role="status"></div>
                                                    Loading interfaces...
                                                </td>
                                            </tr>
                                        </tbody>
                                    </table>
                                </div>
                            </div>
                        </div>

                        <div class="card">
                            <div class="card-header d-flex justify-content-between align-items-center">
                                <h5 class="mb-0">Managed Assignments</h5>
                                <div class="d-flex flex-wrap gap-2">
                                    <button type="button" class="btn btn-sm btn-outline-secondary" id="btn-check-config">Check Config</button>
                                    <button type="button" class="btn btn-sm btn-outline-warning" id="btn-fix-config">Fix Config</button>
                                    <button type="button" class="btn btn-sm btn-success" id="btn-save-config">Save</button>
                                    <button type="button" class="btn btn-sm btn-primary" id="btn-apply-now">Apply Now</button>
                                    <button type="button" class="btn btn-sm btn-primary" id="btn-add-assignment">
                                        <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-1">
                                            <path d="M8 4a.5.5 0 0 1 .5.5v3h3a.5.5 0 0 1 0 1h-3v3a.5.5 0 0 1-1 0v-3h-3a.5.5 0 0 1 0-1h3v-3A.5.5 0 0 1 8 4z"/>
                                        </svg>
                                        Add Assignment
                                    </button>
                                </div>
                            </div>
                            <div class="card-body">
                                <div class="table-responsive">
                                    <table class="table table-hover" id="assignmentsTable">
                                        <thead>
                                            <tr>
                                                <th>Interface</th>
                                                <th>Name</th>
                                                <th>Type</th>
                                                <th>Status</th>
                                                <th>IP Address</th>
                                                <th>Description</th>
                                                <th>Actions</th>
                                            </tr>
                                        </thead>
                                        <tbody>
                                            <tr>
                                                <td colspan="7" class="text-center text-muted">
                                                    <div class="spinner-border spinner-border-sm me-2" role="status"></div>
                                                    Loading assignments...
                                                </td>
                                            </tr>
                                        </tbody>
                                    </table>
                                </div>
                            </div>
                        </div>
                    </div>

                    <!-- VLANs Tab -->
                    <div class="tab-pane fade" id="vlans" role="tabpanel" aria-labelledby="vlans-tab">
                        <div class="card">
                            <div class="card-header d-flex justify-content-between align-items-center">
                                <h5 class="mb-0">VLAN Configuration</h5>
                                <button type="button" class="btn btn-sm btn-primary" id="btn-add-vlan">
                                    <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-1">
                                        <path d="M8 4a.5.5 0 0 1 .5.5v3h3a.5.5 0 0 1 0 1h-3v3a.5.5 0 0 1-1 0v-3h-3a.5.5 0 0 1 0-1h3v-3A.5.5 0 0 1 8 4z"/>
                                    </svg>
                                    Add VLAN
                                </button>
                            </div>
                            <div class="card-body">
                                <div class="table-responsive">
                                    <table class="table table-hover" id="vlansTable">
                                        <thead>
                                            <tr>
                                                <th>VLAN ID</th>
                                                <th>Parent Interface</th>
                                                <th>Tag</th>
                                                <th>Priority</th>
                                                <th>Description</th>
                                                <th>Status</th>
                                                <th>Actions</th>
                                            </tr>
                                        </thead>
                                        <tbody>
                                            <tr>
                                                <td colspan="7" class="text-center text-muted">
                                                    <div class="spinner-border spinner-border-sm me-2" role="status"></div>
                                                    Loading VLANs...
                                                </td>
                                            </tr>
                                        </tbody>
                                    </table>
                                </div>
                            </div>
                        </div>
                    </div>

                    <!-- Bridges Tab -->
                    <div class="tab-pane fade" id="bridges" role="tabpanel" aria-labelledby="bridges-tab">
                        <div class="card">
                            <div class="card-header d-flex justify-content-between align-items-center">
                                <h5 class="mb-0">Bridge Configuration</h5>
                                <button type="button" class="btn btn-sm btn-primary" id="btn-add-bridge">
                                    <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16" class="me-1">
                                        <path d="M8 4a.5.5 0 0 1 .5.5v3h3a.5.5 0 0 1 0 1h-3v3a.5.5 0 0 1-1 0v-3h-3a.5.5 0 0 1 0-1h3v-3A.5.5 0 0 1 8 4z"/>
                                    </svg>
                                    Add Bridge
                                </button>
                            </div>
                            <div class="card-body">
                                <div class="table-responsive">
                                    <table class="table table-hover" id="bridgesTable">
                                        <thead>
                                            <tr>
                                                <th>Bridge Name</th>
                                                <th>Member Interfaces</th>
                                                <th>IP Address</th>
                                                <th>Description</th>
                                                <th>Status</th>
                                                <th>Actions</th>
                                            </tr>
                                        </thead>
                                        <tbody>
                                            <tr>
                                                <td colspan="6" class="text-center text-muted">
                                                    <div class="spinner-border spinner-border-sm me-2" role="status"></div>
                                                    Loading bridges...
                                                </td>
                                            </tr>
                                        </tbody>
                                    </table>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `);
    },

    /**
     * Load all data from backend
     */
    loadData: async function() {
        try {
            await Promise.all([
                this.loadAssignments(),
                this.loadVlans(),
                this.loadBridges(),
                this.loadAvailableInterfaces()
            ]);
        } catch (error) {
            console.error('Error loading interfaces data:', error);
            this.showMessage('Failed to load interfaces data', 'error');
        }
    },

    /**
     * Load interface assignments
     */
    loadAssignments: async function() {
        try {
            const response = await Monolith.API.get('/interfaces/assignments');
            if (response.Success || response.success) {
                const data = response.Data || response.data || {};
                const assigned = data.Assigned || data.assigned || [];
                const unassigned = data.Unassigned || data.unassigned || [];
                const vlans = data.Vlans || data.vlans || [];
                const bridges = data.Bridges || data.bridges || [];

                this.assignments = assigned.map(a => this.normalizeAssignment(a));
                this.unassigned = unassigned.map(u => ({
                    interface: u.Interface || u.interface,
                    mac: u.MacAddress || u.macAddress || '-',
                    status: u.Status || u.status || 'down',
                    ip: u.IpAddress || u.ipAddress || null
                }));
                if (vlans.length > 0) {
                    this.vlans = vlans.map(v => this.normalizeAssignment(v));
                }
                if (bridges.length > 0) {
                    this.bridges = bridges.map(b => this.normalizeAssignment(b));
                }
            } else {
                this.assignments = [];
                this.unassigned = [];
            }
            this.renderAssignments();
            this.renderUnassigned();
        } catch (error) {
            console.error('Error loading assignments:', error);
            this.showMessage('Failed to load interface assignments', 'error');
            this.assignments = [];
            this.unassigned = [];
            this.renderAssignments();
            this.renderUnassigned();
        }
    },

    /**
     * Load VLANs
     */
    loadVlans: async function() {
        try {
            const response = await Monolith.API.get('/interfaces/vlans');
            if (response.Success || response.success) {
                const data = response.Data || response.data || [];
                this.vlans = data.map(v => this.normalizeAssignment(v));
            } else {
                this.vlans = [];
            }
            this.renderVlans();
        } catch (error) {
            console.error('Error loading VLANs:', error);
            this.showMessage('Failed to load VLANs', 'error');
            this.vlans = [];
            this.renderVlans();
        }
    },

    /**
     * Load bridges
     */
    loadBridges: async function() {
        try {
            const response = await Monolith.API.get('/interfaces/bridges');
            if (response.Success || response.success) {
                const data = response.Data || response.data || [];
                this.bridges = data.map(b => this.normalizeAssignment(b));
            } else {
                this.bridges = [];
            }
            this.renderBridges();
        } catch (error) {
            console.error('Error loading bridges:', error);
            this.showMessage('Failed to load bridges', 'error');
            this.bridges = [];
            this.renderBridges();
        }
    },

    /**
     * Load available physical interfaces
     */
    loadAvailableInterfaces: async function() {
        try {
            const response = await Monolith.API.get('/interfaces/available');
            if (response.Success || response.success) {
                const data = response.Data || response.data || [];
                this.availableInterfaces = data.map(i => i.Interface || i.interface || i);
            } else {
                this.availableInterfaces = [];
            }
        } catch (error) {
            console.error('Error loading available interfaces:', error);
            this.availableInterfaces = [];
        }
    },

    /**
     * Render interface assignments table
     */
    renderAssignments: function() {
        const tbody = $('#assignmentsTable tbody');
        if (this.assignments.length === 0) {
            tbody.html('<tr><td colspan="7" class="text-center text-muted">No interface assignments configured</td></tr>');
            return;
        }

        let html = '';
        this.assignments.forEach(assignment => {
            const statusBadge = assignment.status === 'up' 
                ? '<span class="badge bg-success">UP</span>'
                : '<span class="badge bg-secondary">DOWN</span>';
            const typeLabel = assignment.type ? assignment.type.toUpperCase() : 'N/A';
            const managedBadge = assignment.managed
                ? '<span class="badge bg-primary-subtle text-primary border">Managed</span>'
                : '<span class="badge bg-light text-muted border">External</span>';
            
            html += `
                <tr>
                    <td>
                        <code>${assignment.interface}</code>
                        <div class="mt-1">${managedBadge}</div>
                    </td>
                    <td><strong>${assignment.name}</strong></td>
                    <td>${typeLabel}</td>
                    <td>${statusBadge}</td>
                    <td>${assignment.ip || '-'}</td>
                    <td>${assignment.description || '-'}</td>
                    <td>
                        <button class="btn btn-sm btn-outline-primary me-1" onclick="Interfaces.editAssignment('${assignment.interface}')">
                            <svg width="14" height="14" fill="currentColor" viewBox="0 0 16 16">
                                <path d="M12.854.146a.5.5 0 0 0-.707 0L7.5 4.793 2.854.146a.5.5 0 0 0-.707.707L6.793 5.5.146 12.146a.5.5 0 0 0 .708.708L7.5 6.207l6.146 6.147a.5.5 0 0 0 .708-.708L8.207 5.5l4.647-4.646a.5.5 0 0 0 0-.707z"/>
                            </svg>
                            Edit
                        </button>
                        <button class="btn btn-sm btn-outline-danger" onclick="Interfaces.deleteAssignment('${assignment.interface}')">
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

    /**
     * Render unassigned interfaces table
     */
    renderUnassigned: function() {
        const tbody = $('#unassignedTable tbody');
        if (this.unassigned.length === 0) {
            tbody.html('<tr><td colspan="5" class="text-center text-muted">No unassigned interfaces detected</td></tr>');
            return;
        }

        let html = '';
        this.unassigned.forEach(entry => {
            const statusBadge = entry.status === 'up'
                ? '<span class="badge bg-success">UP</span>'
                : '<span class="badge bg-secondary">DOWN</span>';
            html += `
                <tr>
                    <td><code>${entry.interface}</code></td>
                    <td>${entry.mac || '-'}</td>
                    <td>${statusBadge}</td>
                    <td>${entry.ip || '-'}</td>
                    <td>
                        <button class="btn btn-sm btn-outline-primary" onclick="Interfaces.showAddAssignmentModal('${entry.interface}')">Assign</button>
                    </td>
                </tr>
            `;
        });
        tbody.html(html);
    },

    /**
     * Render VLANs table
     */
    renderVlans: function() {
        const tbody = $('#vlansTable tbody');
        if (this.vlans.length === 0) {
            tbody.html('<tr><td colspan="7" class="text-center text-muted">No VLANs configured</td></tr>');
            return;
        }

        let html = '';
        this.vlans.forEach(vlan => {
            const statusBadge = vlan.status === 'up' 
                ? '<span class="badge bg-success">UP</span>'
                : '<span class="badge bg-secondary">DOWN</span>';
            
            html += `
                <tr>
                    <td><code>${vlan.interface}</code></td>
                    <td>${vlan.parentInterface || '-'}</td>
                    <td>${vlan.vlanId || '-'}</td>
                    <td>-</td>
                    <td>${vlan.description || '-'}</td>
                    <td>${statusBadge}</td>
                    <td>
                        <button class="btn btn-sm btn-outline-primary me-1" onclick="Interfaces.editVlan('${vlan.interface}')">Edit</button>
                        <button class="btn btn-sm btn-outline-danger" onclick="Interfaces.deleteVlan('${vlan.interface}')">Delete</button>
                    </td>
                </tr>
            `;
        });
        tbody.html(html);
    },

    /**
     * Render bridges table
     */
    renderBridges: function() {
        const tbody = $('#bridgesTable tbody');
        if (this.bridges.length === 0) {
            tbody.html('<tr><td colspan="6" class="text-center text-muted">No bridges configured</td></tr>');
            return;
        }

        let html = '';
        this.bridges.forEach(bridge => {
            const statusBadge = bridge.status === 'up' 
                ? '<span class="badge bg-success">UP</span>'
                : '<span class="badge bg-secondary">DOWN</span>';
            
            html += `
                <tr>
                    <td><code>${bridge.interface}</code></td>
                    <td>${(bridge.bridgePorts || []).join(', ')}</td>
                    <td>${bridge.ip || '-'}</td>
                    <td>${bridge.description || '-'}</td>
                    <td>${statusBadge}</td>
                    <td>
                        <button class="btn btn-sm btn-outline-primary me-1" onclick="Interfaces.editBridge('${bridge.interface}')">Edit</button>
                        <button class="btn btn-sm btn-outline-danger" onclick="Interfaces.deleteBridge('${bridge.interface}')">Delete</button>
                    </td>
                </tr>
            `;
        });
        tbody.html(html);
    },

    normalizeAssignment: function(raw) {
        const ipModeRaw = raw.IpMode || raw.ipMode;
        let ipMode = ipModeRaw;
        if (typeof ipModeRaw === 'number') {
            ipMode = ipModeRaw === 1 ? 'dhcp' : ipModeRaw === 2 ? 'static' : 'none';
        }
        const roleRaw = raw.Role !== undefined ? raw.Role : raw.role;
        const role = this.roleToString(roleRaw);
        return {
            interface: raw.Interface || raw.interface,
            name: raw.Name || raw.name || raw.Interface || raw.interface,
            type: raw.Type || raw.type,
            status: raw.Status || raw.status || 'down',
            ip: raw.IpAddress || raw.ipAddress || raw.ip || null,
            description: raw.Description || raw.description || '',
            managed: raw.Managed !== undefined ? raw.Managed : (raw.managed !== undefined ? raw.managed : true),
            sourceFile: raw.SourceFile || raw.sourceFile,
            ipMode: ipMode || 'none',
            role: role,
            isManagement: raw.IsManagement !== undefined ? raw.IsManagement : (raw.isManagement !== undefined ? raw.isManagement : false),
            configAddress: raw.ConfigAddress || raw.configAddress,
            configPrefixLength: raw.ConfigPrefixLength || raw.configPrefixLength,
            gateway: raw.Gateway || raw.gateway,
            parentInterface: raw.ParentInterface || raw.parentInterface,
            vlanId: raw.VlanId || raw.vlanId,
            bridgePorts: raw.BridgePorts || raw.bridgePorts || [],
            bridgeStp: raw.BridgeStp !== undefined ? raw.BridgeStp : raw.bridgeStp,
            bridgeForwardDelay: raw.BridgeForwardDelay || raw.bridgeForwardDelay
        };
    },

    roleToString: function(value) {
        if (!value) {
            return 'opt';
        }
        if (typeof value === 'string') {
            return value.toLowerCase();
        }
        if (value === 1) return 'lan';
        if (value === 2) return 'wan';
        if (value === 3) return 'opt';
        return 'opt';
    },

    buildOptions: function(items, selected, placeholder) {
        let html = '';
        if (placeholder) {
            html += `<option value="">${placeholder}</option>`;
        }

        items.forEach(item => {
            const isSelected = item === selected ? 'selected' : '';
            html += `<option value="${item}" ${isSelected}>${item}</option>`;
        });
        return html;
    },

    getPhysicalInterfaces: function() {
        const set = new Set();
        this.unassigned.forEach(u => {
            if (u.interface) {
                set.add(u.interface);
            }
        });
        this.assignments.forEach(a => {
            if (a.type === 'physical' && a.interface) {
                set.add(a.interface);
            }
        });
        return Array.from(set);
    },

    getBridgePortOptions: function() {
        const set = new Set();
        this.unassigned.forEach(u => {
            if (u.interface) {
                set.add(u.interface);
            }
        });
        this.assignments.forEach(a => {
            if ((a.type === 'physical' || a.type === 'vlan') && a.interface) {
                set.add(a.interface);
            }
        });
        this.vlans.forEach(v => {
            if (v.interface) {
                set.add(v.interface);
            }
        });
        return Array.from(set);
    },

    /**
     * Attach event handlers
     */
    attachEventHandlers: function() {
        // Add assignment button
        $(document).off('click', '#btn-add-assignment');
        $(document).on('click', '#btn-add-assignment', () => {
            this.showAddAssignmentModal();
        });

        $(document).off('click', '#btn-check-config');
        $(document).on('click', '#btn-check-config', () => {
            this.checkConfig();
        });

        $(document).off('click', '#btn-fix-config');
        $(document).on('click', '#btn-fix-config', () => {
            this.fixConfig();
        });

        $(document).off('click', '#btn-save-config');
        $(document).on('click', '#btn-save-config', () => {
            this.saveConfig();
        });

        $(document).off('click', '#btn-apply-now');
        $(document).on('click', '#btn-apply-now', () => {
            this.applyNow();
        });

        // Add VLAN button
        $(document).off('click', '#btn-add-vlan');
        $(document).on('click', '#btn-add-vlan', () => {
            this.showAddVlanModal();
        });

        // Add bridge button
        $(document).off('click', '#btn-add-bridge');
        $(document).on('click', '#btn-add-bridge', () => {
            this.showAddBridgeModal();
        });
    },

    /**
     * Show add assignment modal
     */
    showAddAssignmentModal: function(prefillInterface) {
        this.showAssignmentModal({
            interface: prefillInterface || '',
            type: 'physical'
        });
    },

    /**
     * Show add VLAN modal
     */
    showAddVlanModal: function() {
        this.showVlanModal();
    },

    /**
     * Show add bridge modal
     */
    showAddBridgeModal: function() {
        this.showBridgeModal();
    },

    showAssignmentModal: function(assignment) {
        assignment = assignment || {};
        const isEdit = !!assignment.interface;
        const interfaces = this.getPhysicalInterfaces();
        if (assignment && assignment.interface && !interfaces.includes(assignment.interface)) {
            interfaces.unshift(assignment.interface);
        }

        const interfaceOptions = this.buildOptions(interfaces, assignment.interface, 'Select interface');
        const ipMode = assignment.ipMode || 'dhcp';
        const roleValue = assignment.role || 'opt';
        const title = isEdit ? `Edit ${assignment.interface}` : 'Add Assignment';

        const body = `
            <form id="assignment-form">
                <div class="mb-3">
                    <label class="form-label">Interface</label>
                    <select class="form-select" id="assignment-interface" ${isEdit ? 'disabled' : ''}>
                        ${interfaceOptions}
                    </select>
                </div>
                <div class="mb-3">
                    <label class="form-label">Name</label>
                    <input type="text" class="form-control" id="assignment-name" value="${assignment.name || ''}">
                </div>
                <div class="mb-3">
                    <label class="form-label">Description</label>
                    <input type="text" class="form-control" id="assignment-description" value="${assignment.description || ''}">
                </div>
                <div class="row g-2 mb-3">
                    <div class="col-md-6">
                        <label class="form-label">Interface Role</label>
                        <select class="form-select" id="assignment-role">
                            <option value="lan" ${roleValue === 'lan' ? 'selected' : ''}>LAN</option>
                            <option value="wan" ${roleValue === 'wan' ? 'selected' : ''}>WAN</option>
                            <option value="opt" ${roleValue === 'opt' ? 'selected' : ''}>OPT</option>
                        </select>
                    </div>
                    <div class="col-md-6 d-flex align-items-end">
                        <div class="form-check">
                            <input class="form-check-input" type="checkbox" id="assignment-management" ${assignment.isManagement ? 'checked' : ''}>
                            <label class="form-check-label" for="assignment-management">Management interface (allow WebUI)</label>
                        </div>
                    </div>
                </div>
                <div class="mb-3">
                    <label class="form-label">IP Mode</label>
                    <select class="form-select" id="assignment-ipmode">
                        <option value="dhcp" ${ipMode === 'dhcp' ? 'selected' : ''}>DHCP</option>
                        <option value="static" ${ipMode === 'static' ? 'selected' : ''}>Static</option>
                        <option value="none" ${ipMode === 'none' ? 'selected' : ''}>None</option>
                    </select>
                </div>
                <div id="assignment-static-fields">
                    <div class="row g-2 mb-3">
                        <div class="col-md-8">
                            <label class="form-label">Address</label>
                            <input type="text" class="form-control" id="assignment-address" value="${assignment.configAddress || ''}">
                        </div>
                        <div class="col-md-4">
                            <label class="form-label">Prefix</label>
                            <input type="number" class="form-control" id="assignment-prefix" value="${assignment.configPrefixLength || ''}" min="0" max="32">
                        </div>
                    </div>
                    <div class="mb-3">
                        <label class="form-label">Gateway</label>
                        <input type="text" class="form-control" id="assignment-gateway" value="${assignment.gateway || ''}">
                    </div>
                </div>
            </form>
        `;

        const footer = `
            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
            <button type="button" class="btn btn-primary" id="assignment-save-btn">${isEdit ? 'Save' : 'Add'}</button>
        `;

        const modal = Monolith.UI.showModal(title, body, { size: 'lg', footerHtml: footer, staticBackdrop: true });
        const staticFields = modal.element.find('#assignment-static-fields');
        const toggleStaticFields = () => {
            const mode = modal.element.find('#assignment-ipmode').val();
            staticFields.toggle(mode === 'static');
        };
        modal.element.find('#assignment-ipmode').on('change', toggleStaticFields);
        toggleStaticFields();

        modal.element.find('#assignment-save-btn').on('click', async () => {
            const iface = modal.element.find('#assignment-interface').val();
            const name = modal.element.find('#assignment-name').val();
            const description = modal.element.find('#assignment-description').val();
            const role = modal.element.find('#assignment-role').val();
            const isManagement = modal.element.find('#assignment-management').is(':checked');
            const ipModeValue = modal.element.find('#assignment-ipmode').val();
            const address = modal.element.find('#assignment-address').val();
            const prefix = modal.element.find('#assignment-prefix').val();
            const gateway = modal.element.find('#assignment-gateway').val();

            if (!iface) {
                Monolith.UI.toast('Interface is required', 'warning');
                return;
            }

            const payload = {
                interface: iface,
                name: name,
                description: description,
                type: 'physical',
                ipMode: ipModeValue,
                role: role,
                isManagement: isManagement,
                address: address || null,
                prefixLength: prefix ? parseInt(prefix, 10) : null,
                gateway: gateway || null
            };

            try {
                const response = await Monolith.API.post('/interfaces/assignments', payload);
                if (!(response.Success || response.success)) {
                    throw new Error(response.Error || response.error || 'Failed to save assignment');
                }

                Monolith.UI.toast('Assignment saved', 'success');
                modal.instance.hide();
                this.loadData();
            } catch (error) {
                console.error('Save assignment failed:', error);
                Monolith.UI.toast('Failed to save assignment', 'error');
            }
        });
    },

    showVlanModal: function(existing) {
        existing = existing || {};
        const isEdit = !!existing.interface;
        const parents = this.getPhysicalInterfaces();
        const parentOptions = this.buildOptions(parents, existing.parentInterface, 'Select parent');
        const vlanId = existing.vlanId || '';
        const ipMode = existing.ipMode || 'none';
        const roleValue = existing.role || 'opt';
        const title = isEdit ? `Edit ${existing.interface}` : 'Add VLAN';

        const body = `
            <form id="vlan-form">
                <div class="mb-3">
                    <label class="form-label">Parent Interface</label>
                    <select class="form-select" id="vlan-parent">
                        ${parentOptions}
                    </select>
                </div>
                <div class="mb-3">
                    <label class="form-label">VLAN ID</label>
                    <input type="number" class="form-control" id="vlan-id" value="${vlanId}" min="1" max="4094">
                </div>
                <div class="mb-3">
                    <label class="form-label">Name</label>
                    <input type="text" class="form-control" id="vlan-name" value="${existing.name || ''}">
                </div>
                <div class="mb-3">
                    <label class="form-label">Description</label>
                    <input type="text" class="form-control" id="vlan-description" value="${existing.description || ''}">
                </div>
                <div class="row g-2 mb-3">
                    <div class="col-md-6">
                        <label class="form-label">Interface Role</label>
                        <select class="form-select" id="vlan-role">
                            <option value="lan" ${roleValue === 'lan' ? 'selected' : ''}>LAN</option>
                            <option value="wan" ${roleValue === 'wan' ? 'selected' : ''}>WAN</option>
                            <option value="opt" ${roleValue === 'opt' ? 'selected' : ''}>OPT</option>
                        </select>
                    </div>
                    <div class="col-md-6 d-flex align-items-end">
                        <div class="form-check">
                            <input class="form-check-input" type="checkbox" id="vlan-management" ${existing.isManagement ? 'checked' : ''}>
                            <label class="form-check-label" for="vlan-management">Management interface (allow WebUI)</label>
                        </div>
                    </div>
                </div>
                <div class="mb-3">
                    <label class="form-label">IP Mode</label>
                    <select class="form-select" id="vlan-ipmode">
                        <option value="dhcp" ${ipMode === 'dhcp' ? 'selected' : ''}>DHCP</option>
                        <option value="static" ${ipMode === 'static' ? 'selected' : ''}>Static</option>
                        <option value="none" ${ipMode === 'none' ? 'selected' : ''}>None</option>
                    </select>
                </div>
                <div id="vlan-static-fields">
                    <div class="row g-2 mb-3">
                        <div class="col-md-8">
                            <label class="form-label">Address</label>
                            <input type="text" class="form-control" id="vlan-address" value="${existing.configAddress || ''}">
                        </div>
                        <div class="col-md-4">
                            <label class="form-label">Prefix</label>
                            <input type="number" class="form-control" id="vlan-prefix" value="${existing.configPrefixLength || ''}" min="0" max="32">
                        </div>
                    </div>
                    <div class="mb-3">
                        <label class="form-label">Gateway</label>
                        <input type="text" class="form-control" id="vlan-gateway" value="${existing.gateway || ''}">
                    </div>
                </div>
            </form>
        `;

        const footer = `
            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
            <button type="button" class="btn btn-primary" id="vlan-save-btn">${isEdit ? 'Save' : 'Add'}</button>
        `;

        const modal = Monolith.UI.showModal(title, body, { size: 'lg', footerHtml: footer, staticBackdrop: true });
        const staticFields = modal.element.find('#vlan-static-fields');
        const toggleStatic = () => {
            const mode = modal.element.find('#vlan-ipmode').val();
            staticFields.toggle(mode === 'static');
        };
        modal.element.find('#vlan-ipmode').on('change', toggleStatic);
        toggleStatic();

        modal.element.find('#vlan-save-btn').on('click', async () => {
            const parentInterface = modal.element.find('#vlan-parent').val();
            const vlan = modal.element.find('#vlan-id').val();
            const name = modal.element.find('#vlan-name').val();
            const description = modal.element.find('#vlan-description').val();
            const ipModeValue = modal.element.find('#vlan-ipmode').val();
            const role = modal.element.find('#vlan-role').val();
            const isManagement = modal.element.find('#vlan-management').is(':checked');
            const address = modal.element.find('#vlan-address').val();
            const prefix = modal.element.find('#vlan-prefix').val();
            const gateway = modal.element.find('#vlan-gateway').val();

            if (!parentInterface || !vlan) {
                Monolith.UI.toast('Parent and VLAN ID are required', 'warning');
                return;
            }

            const payload = {
                type: 'vlan',
                parentInterface: parentInterface,
                vlanId: parseInt(vlan, 10),
                name: name,
                description: description,
                ipMode: ipModeValue,
                role: role,
                isManagement: isManagement,
                address: address || null,
                prefixLength: prefix ? parseInt(prefix, 10) : null,
                gateway: gateway || null
            };

            try {
                const response = await Monolith.API.post('/interfaces/assignments', payload);
                if (!(response.Success || response.success)) {
                    throw new Error(response.Error || response.error || 'Failed to save VLAN');
                }

                Monolith.UI.toast('VLAN saved', 'success');
                modal.instance.hide();
                this.loadData();
            } catch (error) {
                console.error('Save VLAN failed:', error);
                Monolith.UI.toast('Failed to save VLAN', 'error');
            }
        });
    },

    showBridgeModal: function(existing) {
        existing = existing || {};
        const isEdit = !!existing.interface;
        const portOptions = this.getBridgePortOptions();
        const title = isEdit ? `Edit ${existing.interface}` : 'Add Bridge';
        const ipMode = existing.ipMode || 'none';
        const roleValue = existing.role || 'opt';

        const optionsHtml = portOptions.map(port => {
            const selected = existing.bridgePorts && existing.bridgePorts.includes(port) ? 'selected' : '';
            return `<option value="${port}" ${selected}>${port}</option>`;
        }).join('');

        const body = `
            <form id="bridge-form">
                <div class="mb-3">
                    <label class="form-label">Bridge Name</label>
                    <input type="text" class="form-control" id="bridge-name" value="${existing.interface || ''}" ${isEdit ? 'disabled' : ''}>
                </div>
                <div class="mb-3">
                    <label class="form-label">Member Interfaces</label>
                    <select multiple class="form-select" id="bridge-ports">
                        ${optionsHtml}
                    </select>
                    <div class="form-text">Hold Ctrl/Cmd to select multiple ports.</div>
                </div>
                <div class="form-check form-switch mb-3">
                    <input class="form-check-input" type="checkbox" id="bridge-stp" ${existing.bridgeStp ? 'checked' : ''}>
                    <label class="form-check-label" for="bridge-stp">Enable STP</label>
                </div>
                <div class="mb-3">
                    <label class="form-label">Forward Delay (seconds)</label>
                    <input type="number" class="form-control" id="bridge-fd" value="${existing.bridgeForwardDelay || 0}" min="0" max="60">
                </div>
                <div class="mb-3">
                    <label class="form-label">IP Mode</label>
                    <select class="form-select" id="bridge-ipmode">
                        <option value="dhcp" ${ipMode === 'dhcp' ? 'selected' : ''}>DHCP</option>
                        <option value="static" ${ipMode === 'static' ? 'selected' : ''}>Static</option>
                        <option value="none" ${ipMode === 'none' ? 'selected' : ''}>None</option>
                    </select>
                </div>
                <div class="row g-2 mb-3">
                    <div class="col-md-6">
                        <label class="form-label">Interface Role</label>
                        <select class="form-select" id="bridge-role">
                            <option value="lan" ${roleValue === 'lan' ? 'selected' : ''}>LAN</option>
                            <option value="wan" ${roleValue === 'wan' ? 'selected' : ''}>WAN</option>
                            <option value="opt" ${roleValue === 'opt' ? 'selected' : ''}>OPT</option>
                        </select>
                    </div>
                    <div class="col-md-6 d-flex align-items-end">
                        <div class="form-check">
                            <input class="form-check-input" type="checkbox" id="bridge-management" ${existing.isManagement ? 'checked' : ''}>
                            <label class="form-check-label" for="bridge-management">Management interface (allow WebUI)</label>
                        </div>
                    </div>
                </div>
                <div id="bridge-static-fields">
                    <div class="row g-2 mb-3">
                        <div class="col-md-8">
                            <label class="form-label">Address</label>
                            <input type="text" class="form-control" id="bridge-address" value="${existing.configAddress || ''}">
                        </div>
                        <div class="col-md-4">
                            <label class="form-label">Prefix</label>
                            <input type="number" class="form-control" id="bridge-prefix" value="${existing.configPrefixLength || ''}" min="0" max="32">
                        </div>
                    </div>
                    <div class="mb-3">
                        <label class="form-label">Gateway</label>
                        <input type="text" class="form-control" id="bridge-gateway" value="${existing.gateway || ''}">
                    </div>
                </div>
            </form>
        `;

        const footer = `
            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
            <button type="button" class="btn btn-primary" id="bridge-save-btn">${isEdit ? 'Save' : 'Add'}</button>
        `;

        const modal = Monolith.UI.showModal(title, body, { size: 'lg', footerHtml: footer, staticBackdrop: true });
        const staticFields = modal.element.find('#bridge-static-fields');
        const toggleStatic = () => {
            const mode = modal.element.find('#bridge-ipmode').val();
            staticFields.toggle(mode === 'static');
        };
        modal.element.find('#bridge-ipmode').on('change', toggleStatic);
        toggleStatic();

        modal.element.find('#bridge-save-btn').on('click', async () => {
            const name = modal.element.find('#bridge-name').val();
            const ports = modal.element.find('#bridge-ports').val() || [];
            const stp = modal.element.find('#bridge-stp').is(':checked');
            const fd = modal.element.find('#bridge-fd').val();
            const ipModeValue = modal.element.find('#bridge-ipmode').val();
            const role = modal.element.find('#bridge-role').val();
            const isManagement = modal.element.find('#bridge-management').is(':checked');
            const address = modal.element.find('#bridge-address').val();
            const prefix = modal.element.find('#bridge-prefix').val();
            const gateway = modal.element.find('#bridge-gateway').val();

            if (!name || ports.length === 0) {
                Monolith.UI.toast('Bridge name and ports are required', 'warning');
                return;
            }

            const payload = {
                type: 'bridge',
                interface: name,
                bridgePorts: ports,
                bridgeStp: stp,
                bridgeForwardDelay: fd ? parseInt(fd, 10) : null,
                name: name,
                ipMode: ipModeValue,
                role: role,
                isManagement: isManagement,
                address: address || null,
                prefixLength: prefix ? parseInt(prefix, 10) : null,
                gateway: gateway || null
            };

            try {
                const response = await Monolith.API.post('/interfaces/assignments', payload);
                if (!(response.Success || response.success)) {
                    throw new Error(response.Error || response.error || 'Failed to save bridge');
                }

                Monolith.UI.toast('Bridge saved', 'success');
                modal.instance.hide();
                this.loadData();
            } catch (error) {
                console.error('Save bridge failed:', error);
                Monolith.UI.toast('Failed to save bridge', 'error');
            }
        });
    },

    checkConfig: async function() {
        try {
            const response = await Monolith.API.post('/interfaces/config/check', {});
            if (!(response.Success || response.success)) {
                throw new Error(response.Error || response.error || 'Check failed');
            }

            const data = response.Data || response.data || {};
            const issues = data.Issues || data.issues || [];
            if (!issues.length) {
                Monolith.UI.toast('No configuration issues detected', 'success');
                return;
            }

            const listItems = issues.map(issue => `
                <li>
                    <strong>${issue.Type || issue.type}</strong> - ${issue.Message || issue.message}
                    ${issue.File || issue.file ? `<div class="text-muted small">${issue.File || issue.file}</div>` : ''}
                    ${issue.Detail || issue.detail ? `<div class="text-muted small">${issue.Detail || issue.detail}</div>` : ''}
                </li>
            `).join('');

            const body = `
                <div class="alert alert-warning">
                    <strong>${issues.length}</strong> issue(s) detected in interface configuration.
                </div>
                <ul class="ps-3 mb-0">${listItems}</ul>
            `;
            Monolith.UI.showModal('Interface Config Check', body, { size: 'lg' });
        } catch (error) {
            console.error('Config check failed:', error);
            Monolith.UI.toast('Config check failed', 'error');
        }
    },

    saveConfig: async function() {
        try {
            const response = await Monolith.API.post('/interfaces/config/apply', {});
            if (!(response.Success || response.success)) {
                throw new Error(response.Error || response.error || 'Save failed');
            }

            const data = response.Data || response.data || {};
            Monolith.UI.toast(data.Message || 'Configuration saved', 'success');
        } catch (error) {
            console.error('Save failed:', error);
            Monolith.UI.toast('Failed to save configuration', 'error');
        }
    },

    applyNow: async function() {
        try {
            const response = await Monolith.API.post('/interfaces/config/apply-now', {});
            if (!(response.Success || response.success)) {
                throw new Error(response.Error || response.error || 'Apply failed');
            }

            const data = response.Data || response.data || {};
            Monolith.UI.toast(data.Message || 'Interfaces applied', 'success');
        } catch (error) {
            console.error('Apply now failed:', error);
            Monolith.UI.toast('Failed to apply interfaces', 'error');
        }
    },

    fixConfig: async function() {
        try {
            const response = await Monolith.API.post('/interfaces/config/fix', {});
            if (!(response.Success || response.success)) {
                throw new Error(response.Error || response.error || 'Fix failed');
            }

            const data = response.Data || response.data || {};
            Monolith.UI.toast(data.Message || 'Configuration fixed', 'success');
            this.loadData();
        } catch (error) {
            console.error('Fix failed:', error);
            Monolith.UI.toast('Failed to fix configuration', 'error');
        }
    },

    /**
     * Edit assignment
     */
    editAssignment: function(interfaceName) {
        const assignment = this.assignments.find(a => a.interface === interfaceName);
        if (!assignment) {
            Monolith.UI.toast('Assignment not found', 'warning');
            return;
        }
        this.showAssignmentModal(assignment);
    },

    /**
     * Delete assignment
     */
    deleteAssignment: function(interfaceName) {
        const warning = `Delete assignment for ${interfaceName}? This will move its current configuration to /etc/network/interfaces.d/monolith-unmanaged and Monolith will no longer manage it.`;
        if (confirm(warning)) {
            Monolith.API.delete(`/interfaces/assignments/${encodeURIComponent(interfaceName)}`)
                .then(response => {
                    if (!(response.Success || response.success)) {
                        throw new Error(response.Error || response.error || 'Delete failed');
                    }
                    Monolith.UI.toast('Assignment removed', 'success');
                    this.loadData();
                })
                .catch(error => {
                    console.error('Delete assignment failed:', error);
                    Monolith.UI.toast('Failed to delete assignment', 'error');
                });
        }
    },

    /**
     * Edit VLAN
     */
    editVlan: function(vlanInterface) {
        const vlan = this.vlans.find(v => v.interface === vlanInterface);
        if (!vlan) {
            Monolith.UI.toast('VLAN not found', 'warning');
            return;
        }
        this.showVlanModal(vlan);
    },

    /**
     * Delete VLAN
     */
    deleteVlan: function(vlanInterface) {
        const warning = `Delete VLAN ${vlanInterface}? This will move its current configuration to /etc/network/interfaces.d/monolith-unmanaged and Monolith will no longer manage it.`;
        if (confirm(warning)) {
            Monolith.API.delete(`/interfaces/assignments/${encodeURIComponent(vlanInterface)}`)
                .then(response => {
                    if (!(response.Success || response.success)) {
                        throw new Error(response.Error || response.error || 'Delete failed');
                    }
                    Monolith.UI.toast('VLAN removed', 'success');
                    this.loadData();
                })
                .catch(error => {
                    console.error('Delete VLAN failed:', error);
                    Monolith.UI.toast('Failed to delete VLAN', 'error');
                });
        }
    },

    /**
     * Edit bridge
     */
    editBridge: function(bridgeName) {
        const bridge = this.bridges.find(b => b.interface === bridgeName);
        if (!bridge) {
            Monolith.UI.toast('Bridge not found', 'warning');
            return;
        }
        this.showBridgeModal(bridge);
    },

    /**
     * Delete bridge
     */
    deleteBridge: function(bridgeName) {
        const warning = `Delete bridge ${bridgeName}? This will move its current configuration to /etc/network/interfaces.d/monolith-unmanaged and Monolith will no longer manage it.`;
        if (confirm(warning)) {
            Monolith.API.delete(`/interfaces/assignments/${encodeURIComponent(bridgeName)}`)
                .then(response => {
                    if (!(response.Success || response.success)) {
                        throw new Error(response.Error || response.error || 'Delete failed');
                    }
                    Monolith.UI.toast('Bridge removed', 'success');
                    this.loadData();
                })
                .catch(error => {
                    console.error('Delete bridge failed:', error);
                    Monolith.UI.toast('Failed to delete bridge', 'error');
                });
        }
    },

    /**
     * Show status message
     */
    showMessage: function(message, type) {
        const alert = $('#interfacesStatusMessage');
        alert.removeClass('d-none alert-success alert-danger alert-warning alert-info')
             .addClass(`alert-${type}`)
             .text(message);
        setTimeout(() => alert.addClass('d-none'), 5000);
    }
};

// Register with Monolith.Pages
if (typeof Monolith !== 'undefined') {
    Monolith.Pages = Monolith.Pages || {};
    Monolith.Pages.Interfaces = Interfaces;
}
