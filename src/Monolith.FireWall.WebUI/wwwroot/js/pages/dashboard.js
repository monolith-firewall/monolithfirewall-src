// Dashboard Widget System
var Monolith = window.Monolith || {};
Monolith.Pages = Monolith.Pages || {};

Monolith.Pages.Dashboard = {
    widgets: [],
    layout: null,
    refreshTimers: {},
    editMode: false,
    gridColumns: 12, // Default: 12 columns (3-column layout)
    systemSeries: {
        cpu: [],
        memory: [],
        disk: [],
        maxPoints: 30
    },
    trafficSeries: {
        rx: [],
        tx: [],
        rxLoss: [],
        txLoss: [],
        timestamps: [],
        interfaces: {}, // Store per-interface series: { "eth0": { rx: [], tx: [], rxLoss: [], txLoss: [], timestamps: [] } }
        maxPoints: 30
    },
    trafficLast: null,

    init: function() {
        console.log('Initializing Dashboard...');
        
        // Ensure container exists
        const container = $('#dashboard-container');
        if (container.length === 0 && window.location.pathname === '/dashboard') {
            console.error('Dashboard container not found! Waiting...');
            setTimeout(() => this.init(), 100);
            return;
        }
        
        // Clear any existing state
        this.widgets = [];
        this.layout = null;
    },

    renderPage: function() {
        console.log('Rendering Dashboard page...');
        this.loadWidgets();
    },

    loadWidgets: async function() {
        try {
            console.log('Loading widgets...');
            // Load available widgets
            const widgetsResponse = await Monolith.API.get('/dashboard/widgets');
            if (widgetsResponse.success && widgetsResponse.data) {
                const raw = widgetsResponse.data;
                const list = Array.isArray(raw)
                    ? raw
                    : (raw.widgets || raw.Widgets || []);

                this.widgets = (list || [])
                    .map(w => this.normalizeWidget(w))
                    .filter(w => !!w.id);

                console.log(`Loaded ${this.widgets.length} widgets:`, this.widgets.map(w => w.id));
            } else {
                console.error('Failed to load widgets:', widgetsResponse);
                Monolith.UI.toast('Failed to load widgets', 'error');
                return;
            }

            // Load user's layout (may fail if not authenticated, use default)
            try {
                const layoutResponse = await Monolith.API.get('/dashboard/layout');
                if (layoutResponse.success && layoutResponse.data && layoutResponse.data.widgets && layoutResponse.data.widgets.length > 0) {
                    this.layout = layoutResponse.data;
                    // Load grid columns setting if available
                    if (this.layout.gridColumns) {
                        this.gridColumns = this.layout.gridColumns;
                    }
                    console.log('Loaded user layout with', this.layout.widgets.length, 'widgets, grid columns:', this.gridColumns);
                } else {
                    // Use default layout if none saved
                    console.log('No saved layout, using default');
                    this.layout = this.getDefaultLayout();
                }
            } catch (layoutError) {
                console.warn('Could not load user layout, using default:', layoutError);
                // Use default layout if auth fails or other error
                this.layout = this.getDefaultLayout();
            }
            
            // Apply grid columns setting
            this.applyGridColumns();

            console.log('Layout to render:', this.layout);
            this.render();
        } catch (error) {
            console.error('Error loading dashboard:', error);
            Monolith.UI.toast('Failed to load dashboard', 'error');
        }
    },

    getDefaultLayout: function() {
        // Return default layout matching the server-side default
        return {
            widgets: [
                { id: "system.info", order: 1, width: 4, height: 2, visible: true },
                { id: "system.details", order: 2, width: 4, height: 3, visible: true },
                { id: "system.network", order: 3, width: 4, height: 2, visible: true },
                { id: "system.traffic", order: 4, width: 4, height: 3, visible: true },
                { id: "system.activity", order: 5, width: 4, height: 3, visible: true }
            ]
        };
    },

    // DOM helpers (widget ids can include dots like "system.info")
    toDomId: function(widgetId) {
        return String(widgetId).replace(/[^a-zA-Z0-9_-]/g, '-');
    },

    widgetBodyDomId: function(widgetId) {
        return `widget-body-${this.toDomId(widgetId)}`;
    },

    widgetBodySelector: function(widgetId) {
        return `#${this.widgetBodyDomId(widgetId)}`;
    },

    normalizeWidget: function(w) {
        // Support both camelCase (WebUI) and PascalCase (Core WidgetDefinition)
        return {
            id: w.id ?? w.Id,
            title: w.title ?? w.Title,
            package: w.package ?? w.Package,
            module: w.module ?? w.Module,
            description: w.description ?? w.Description ?? '',
            icon: w.icon ?? w.Icon ?? '',
            defaultWidth: w.defaultWidth ?? w.DefaultWidth ?? 4,
            defaultHeight: w.defaultHeight ?? w.DefaultHeight ?? 2,
            refreshInterval: w.refreshInterval ?? w.RefreshInterval ?? 0,
            requiredPermissions: w.requiredPermissions ?? w.RequiredPermissions ?? []
        };
    },

    render: function() {
        // Ensure we have widgets before rendering
        if (!this.widgets || this.widgets.length === 0) {
            console.error('Cannot render: widgets not loaded');
            $('#dashboard-container').html('<div class="alert alert-danger">Failed to load widgets. Please refresh the page.</div>');
            return;
        }

        const container = $('#dashboard-container');
        container.html(`
            <div class="container-fluid dashboard-page">
                <div class="d-flex justify-content-end align-items-center mb-4">
                    <div class="btn-group" role="group">
                        <button class="btn btn-sm btn-outline-secondary" id="add-widget-btn" 
                                data-bs-toggle="tooltip" data-bs-placement="bottom" 
                                title="Add Widget - Add new widgets to your dashboard">
                            <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16">
                                <path d="M8 4a.5.5 0 0 1 .5.5v3h3a.5.5 0 0 1 0 1h-3v3a.5.5 0 0 1-1 0v-3h-3a.5.5 0 0 1 0-1h3v-3A.5.5 0 0 1 8 4z"/>
                            </svg>
                        </button>
                        <button class="btn btn-sm btn-outline-secondary" id="edit-layout-btn" 
                                data-bs-toggle="tooltip" data-bs-placement="bottom" 
                                title="Edit Layout - Drag widgets to reorder">
                            <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16">
                                <path d="M12.854.146a.5.5 0 0 0-.707 0L10.5 1.793 14.207 5.5l1.647-1.646a.5.5 0 0 0 0-.708l-3-3zm.646 6.061L9.793 2.5 3.293 9H3.5a.5.5 0 0 1 .5.5v.5h.5a.5.5 0 0 1 .5.5v.5h.5a.5.5 0 0 1 .5.5v.5h.5a.5.5 0 0 1 .5.5v.207l6.5-6.5zm-7.468 7.468A.5.5 0 0 1 6 13.5V13h-.5a.5.5 0 0 1-.5-.5V12h-.5a.5.5 0 0 1-.5-.5V11h-.5a.5.5 0 0 1-.5-.5V10.293l6.5-6.5 4.707 4.707-6.5 6.5z"/>
                            </svg>
                        </button>
                        <button class="btn btn-sm btn-outline-secondary" id="grid-settings-btn" 
                                data-bs-toggle="tooltip" data-bs-placement="bottom" 
                                title="Grid Settings - Configure grid columns (1, 2, or 3)">
                            <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16">
                                <path d="M1 2.5A1.5 1.5 0 0 1 2.5 1h3A1.5 1.5 0 0 1 7 2.5v3A1.5 1.5 0 0 1 5.5 7h-3A1.5 1.5 0 0 1 1 5.5v-3zM2.5 2a.5.5 0 0 0-.5.5v3a.5.5 0 0 0 .5.5h3a.5.5 0 0 0 .5-.5v-3a.5.5 0 0 0-.5-.5h-3zm6.5.5A1.5 1.5 0 0 1 10.5 1h3A1.5 1.5 0 0 1 15 2.5v3A1.5 1.5 0 0 1 13.5 7h-3A1.5 1.5 0 0 1 9 5.5v-3zm1.5-.5a.5.5 0 0 0-.5.5v3a.5.5 0 0 0 .5.5h3a.5.5 0 0 0 .5-.5v-3a.5.5 0 0 0-.5-.5h-3zM1 10.5A1.5 1.5 0 0 1 2.5 9h3A1.5 1.5 0 0 1 7 10.5v3A1.5 1.5 0 0 1 5.5 15h-3A1.5 1.5 0 0 1 1 13.5v-3zm1.5-.5a.5.5 0 0 0-.5.5v3a.5.5 0 0 0 .5.5h3a.5.5 0 0 0 .5-.5v-3a.5.5 0 0 0-.5-.5h-3zm6.5.5A1.5 1.5 0 0 1 10.5 9h3a1.5 1.5 0 0 1 1.5 1.5v3a1.5 1.5 0 0 1-1.5 1.5h-3A1.5 1.5 0 0 1 9 13.5v-3zm1.5-.5a.5.5 0 0 0-.5.5v3a.5.5 0 0 0 .5.5h3a.5.5 0 0 0 .5-.5v-3a.5.5 0 0 0-.5-.5h-3z"/>
                            </svg>
                        </button>
                        <button class="btn btn-sm btn-outline-primary" id="reset-layout-btn" 
                                data-bs-toggle="tooltip" data-bs-placement="bottom" 
                                title="Reset Layout - Restore default widget layout">
                            <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16">
                                <path d="M8 3a5 5 0 1 0 4.546 2.914.5.5 0 0 1 .908-.417A6 6 0 1 1 8 2v1z"/>
                                <path d="M8 4.466V.534a.25.25 0 0 1 .41-.192l2.36 1.966c.12.1.12.284 0 .384L8.41 4.658A.25.25 0 0 1 8 4.466z"/>
                            </svg>
                        </button>
                    </div>
                </div>
                <!-- Grid Settings Modal -->
                <div class="modal fade" id="grid-settings-modal" tabindex="-1">
                    <div class="modal-dialog">
                        <div class="modal-content">
                            <div class="modal-header">
                                <h5 class="modal-title">Grid Settings</h5>
                                <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                            </div>
                            <div class="modal-body">
                                <p class="text-muted mb-3">Choose the number of columns for your dashboard grid:</p>
                                <div class="row g-3">
                                    <div class="col-4">
                                        <div class="grid-option-card text-center p-3 border rounded cursor-pointer" 
                                             data-columns="12" style="cursor: pointer; border: 2px solid #dee2e6 !important;">
                                            <div class="mb-2">
                                                <div class="d-flex gap-1 justify-content-center">
                                                    <div class="bg-primary" style="width: 20px; height: 30px; border-radius: 4px;"></div>
                                                    <div class="bg-primary" style="width: 20px; height: 30px; border-radius: 4px;"></div>
                                                    <div class="bg-primary" style="width: 20px; height: 30px; border-radius: 4px;"></div>
                                                </div>
                                            </div>
                                            <strong>3 Columns</strong>
                                            <div class="small text-muted">12 columns</div>
                                        </div>
                                    </div>
                                    <div class="col-4">
                                        <div class="grid-option-card text-center p-3 border rounded cursor-pointer" 
                                             data-columns="6" style="cursor: pointer; border: 2px solid #dee2e6 !important;">
                                            <div class="mb-2">
                                                <div class="d-flex gap-1 justify-content-center">
                                                    <div class="bg-primary" style="width: 30px; height: 30px; border-radius: 4px;"></div>
                                                    <div class="bg-primary" style="width: 30px; height: 30px; border-radius: 4px;"></div>
                                                </div>
                                            </div>
                                            <strong>2 Columns</strong>
                                            <div class="small text-muted">6 columns</div>
                                        </div>
                                    </div>
                                    <div class="col-4">
                                        <div class="grid-option-card text-center p-3 border rounded cursor-pointer" 
                                             data-columns="4" style="cursor: pointer; border: 2px solid #dee2e6 !important;">
                                            <div class="mb-2">
                                                <div class="d-flex gap-1 justify-content-center">
                                                    <div class="bg-primary" style="width: 60px; height: 30px; border-radius: 4px;"></div>
                                                </div>
                                            </div>
                                            <strong>1 Column</strong>
                                            <div class="small text-muted">4 columns</div>
                                        </div>
                                    </div>
                                </div>
                            </div>
                            <div class="modal-footer">
                                <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
                            </div>
                        </div>
                    </div>
                </div>
                <!-- Add Widget Modal -->
                <div class="modal fade" id="add-widget-modal" tabindex="-1">
                    <div class="modal-dialog">
                        <div class="modal-content">
                            <div class="modal-header">
                                <h5 class="modal-title">Add Widget</h5>
                                <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                            </div>
                            <div class="modal-body">
                                <div class="mb-3">
                                    <input type="text" class="form-control" id="widget-search" 
                                           placeholder="Search widgets...">
                                </div>
                                <div id="available-widgets-list" style="max-height: 400px; overflow-y: auto;">
                                    <!-- Widgets will be listed here -->
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
                <div class="dashboard-grid" id="widget-grid">
                    <!-- Widgets will be rendered here -->
                </div>
            </div>
        `);

        this.renderWidgets();
        this.initDragDrop();
        this.startAutoRefresh();
        this.applyGridColumns(); // Apply grid columns after rendering

        // Initialize tooltips
        const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
        tooltipTriggerList.map(function (tooltipTriggerEl) {
            return new bootstrap.Tooltip(tooltipTriggerEl);
        });

        $('#reset-layout-btn').on('click', () => this.resetLayout());
        $('#edit-layout-btn').on('click', () => this.toggleEditMode());
        $('#add-widget-btn').on('click', () => this.showAddWidgetModal());
        $('#grid-settings-btn').on('click', () => this.showGridSettingsModal());
        $('#widget-search').on('input', () => this.filterAvailableWidgets());
        
        // Grid settings modal - column selection (use event delegation since modal is created dynamically)
        $(document).on('click', '.grid-option-card', function() {
            const columns = parseInt($(this).data('columns'));
            Dashboard.setGridColumns(columns);
            const modal = bootstrap.Modal.getInstance(document.getElementById('grid-settings-modal'));
            if (modal) modal.hide();
        });
        
        // Initialize edit mode state
        this.editMode = false;
    },

    renderWidgets: function() {
        const grid = $('#widget-grid');
        grid.empty();

        // If no layout, use default
        if (!this.layout || !this.layout.widgets || this.layout.widgets.length === 0) {
            console.log('No layout found, using default');
            this.layout = this.getDefaultLayout();
        }

        // Check if we have widgets loaded
        if (!this.widgets || this.widgets.length === 0) {
            console.error('No widgets loaded! Cannot render dashboard.');
            grid.html('<div class="alert alert-warning">No widgets available. Please refresh the page.</div>');
            return;
        }

        console.log('Rendering widgets:', {
            layoutWidgets: this.layout.widgets.length,
            availableWidgets: this.widgets.length,
            widgetIds: this.widgets.map(w => w.id)
        });

        // Sort by order
        const sortedWidgets = this.layout.widgets.sort((a, b) => a.order - b.order);

        let renderedCount = 0;
        sortedWidgets.forEach(layoutWidget => {
            if (!layoutWidget.visible) {
                console.log(`Skipping hidden widget: ${layoutWidget.id}`);
                return;
            }

            const widgetDef = this.widgets.find(w => w.id === layoutWidget.id);
            if (!widgetDef) {
                console.warn(`Widget definition not found for: ${layoutWidget.id}`);
                return;
            }

            renderedCount++;

            const bodyDomId = this.widgetBodyDomId(layoutWidget.id);
            const widgetHtml = `
                <div class="widget-card" data-widget-id="${layoutWidget.id}" 
                     style="grid-column: span ${layoutWidget.width}; grid-row: span ${layoutWidget.height};">
                    <div class="widget-header">
                        <div class="widget-drag-handle">
                            <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16">
                                <path d="M7 2a1 1 0 1 1-2 0 1 1 0 0 1 2 0zm3 0a1 1 0 1 1-2 0 1 1 0 0 1 2 0zM7 5a1 1 0 1 1-2 0 1 1 0 0 1 2 0zm3 0a1 1 0 1 1-2 0 1 1 0 0 1 2 0zM7 8a1 1 0 1 1-2 0 1 1 0 0 1 2 0zm3 0a1 1 0 1 1-2 0 1 1 0 0 1 2 0zm-3 3a1 1 0 1 1-2 0 1 1 0 0 1 2 0zm3 0a1 1 0 1 1-2 0 1 1 0 0 1 2 0zm-3 3a1 1 0 1 1-2 0 1 1 0 0 1 2 0zm3 0a1 1 0 1 1-2 0 1 1 0 0 1 2 0z"/>
                            </svg>
                        </div>
                        <h5 class="widget-title">${widgetDef.title}</h5>
                        <div class="widget-actions">
                            <button class="btn-widget-refresh" data-widget-id="${layoutWidget.id}" 
                                    data-bs-toggle="tooltip" title="Refresh widget data">
                                <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16">
                                    <path fill-rule="evenodd" d="M8 3a5 5 0 1 0 4.546 2.914.5.5 0 0 1 .908-.417A6 6 0 1 1 8 2v1z"/>
                                    <path d="M8 4.466V.534a.25.25 0 0 1 .41-.192l2.36 1.966c.12.1.12.284 0 .384L8.41 4.658A.25.25 0 0 1 8 4.466z"/>
                                </svg>
                            </button>
                            <button class="btn-widget-remove" data-widget-id="${layoutWidget.id}" 
                                    data-bs-toggle="tooltip" title="Remove widget"
                                    style="display: none;">
                                <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16">
                                    <path d="M2.146 2.854a.5.5 0 1 1 .708-.708L8 7.293l5.146-5.147a.5.5 0 0 1 .708.708L8.707 8l5.147 5.146a.5.5 0 0 1-.708.708L8 8.707l-5.146 5.147a.5.5 0 0 1-.708-.708L7.293 8 2.146 2.854Z"/>
                                </svg>
                            </button>
                        </div>
                    </div>
                    <div class="widget-body" id="${bodyDomId}">
                        <div class="widget-loading">
                            <div class="spinner-border spinner-border-sm text-primary" role="status">
                                <span class="visually-hidden">Loading...</span>
                            </div>
                        </div>
                    </div>
                </div>
            `;

            grid.append(widgetHtml);
            this.loadWidgetData(layoutWidget.id);
        });

        console.log(`Rendered ${renderedCount} widgets`);

        if (renderedCount === 0) {
            grid.html('<div class="alert alert-warning">No widgets could be rendered. Check console for details.</div>');
        }

        // Attach refresh button handlers
        $('.btn-widget-refresh').on('click', function() {
            const widgetId = $(this).data('widget-id');
            Dashboard.loadWidgetData(widgetId);
        });

        // Attach remove button handlers
        $('.btn-widget-remove').on('click', function() {
            const widgetId = $(this).data('widget-id');
            Dashboard.removeWidget(widgetId);
        });

        // Initialize tooltips for widget buttons
        $('[data-bs-toggle="tooltip"]').each(function() {
            new bootstrap.Tooltip(this);
        });
    },

    removeWidget: function(widgetId) {
        if (!confirm(`Remove "${this.widgets.find(w => w.id === widgetId)?.title || widgetId}" widget?`)) {
            return;
        }

        // Remove from layout
        this.layout.widgets = this.layout.widgets.filter(w => w.id !== widgetId);
        
        // Stop refresh timer
        if (this.refreshTimers[widgetId]) {
            clearInterval(this.refreshTimers[widgetId]);
            delete this.refreshTimers[widgetId];
        }

        // Save and re-render
        this.saveLayout().then(() => {
            this.renderWidgets();
            this.initDragDrop();
            this.startAutoRefresh();
            Monolith.UI.toast('Widget removed', 'success');
        });
    },

    loadWidgetData: async function(widgetId) {
        // Only load data for widgets that are in the layout and visible
        if (!this.layout || !this.layout.widgets) {
            return;
        }
        
        const layoutWidget = this.layout.widgets.find(w => w.id === widgetId);
        if (!layoutWidget || !layoutWidget.visible) {
            // Widget is not in layout or not visible, don't load data
            return;
        }
        
        try {
            const response = await Monolith.API.get(`/dashboard/widget/${widgetId}/data`);
            if (response.success) {
                this.renderWidgetData(widgetId, response.data);
            }
        } catch (error) {
            console.error(`Error loading widget ${widgetId}:`, error);
            // Only show error if widget is actually rendered
            const widgetElement = $(this.widgetBodySelector(widgetId));
            if (widgetElement.length > 0) {
                widgetElement.html('<div class="alert alert-danger">Failed to load data</div>');
            }
        }
    },

    renderWidgetData: function(widgetId, data) {
        const container = $(this.widgetBodySelector(widgetId));
        
        try {
            switch(widgetId) {
                case 'system.info':
                    container.html(this.renderSystemInfo(data));
                    break;
                case 'system.details':
                    container.html(this.renderSystemDetails(data));
                    break;
                case 'system.network':
                    container.html(this.renderNetworkInfo(data));
                    break;
                case 'system.traffic':
                    container.html(this.renderSystemTraffic(data));
                    break;
                case 'system.activity':
                    container.html(this.renderActivity(data));
                    break;
                case 'network.dhcp.status':
                    container.html(this.renderDhcpStatus(data));
                    break;
                default:
                    console.warn(`Widget renderer not implemented for: ${widgetId}`);
                    container.html(`<div class="alert alert-info">
                        <i class="fa-solid fa-info-circle me-2"></i>
                        Widget data renderer not implemented for: <code>${widgetId}</code>
                    </div>`);
            }
        } catch (error) {
            console.error(`Error rendering widget ${widgetId}:`, error);
            container.html(`<div class="alert alert-danger m-3">
                <i class="fa-solid fa-exclamation-triangle me-2"></i>
                <strong>Widget Error</strong><br>
                <small>This widget failed to load due to a script error.</small>
                ${error.message ? `<br><small class="text-muted">${error.message}</small>` : ''}
            </div>`);
        }
    },


    renderSystemInfo: function(data) {
        const cpuUsage = data.cpu ? data.cpu.usage : 0;
        const memPercent = data.memory ? data.memory.percent : 0;
        const diskPercent = data.disk ? data.disk.percent : 0;

        this.pushSeries(this.systemSeries.cpu, cpuUsage, this.systemSeries.maxPoints);
        this.pushSeries(this.systemSeries.memory, memPercent, this.systemSeries.maxPoints);
        this.pushSeries(this.systemSeries.disk, diskPercent, this.systemSeries.maxPoints);

        return `
            <div class="system-info-grid">
                <div class="metric-card">
                    <div class="metric-header">
                        <span>CPU</span>
                        <span class="metric-value">${cpuUsage}%</span>
                    </div>
                    <div class="metric-sparkline">
                        ${this.buildSparkline(this.systemSeries.cpu, 100, 'sparkline-cpu')}
                    </div>
                    <div class="metric-sub">${data.cpu ? data.cpu.cores : 0} cores</div>
                </div>
                <div class="metric-card">
                    <div class="metric-header">
                        <span>Memory</span>
                        <span class="metric-value">${memPercent}%</span>
                    </div>
                    <div class="metric-sparkline">
                        ${this.buildSparkline(this.systemSeries.memory, 100, 'sparkline-memory')}
                    </div>
                    <div class="metric-sub">${data.memory ? data.memory.used : 0} MB / ${data.memory ? data.memory.total : 0} MB</div>
                </div>
                <div class="metric-card">
                    <div class="metric-header">
                        <span>Disk</span>
                        <span class="metric-value">${diskPercent}%</span>
                    </div>
                    <div class="metric-sparkline">
                        ${this.buildSparkline(this.systemSeries.disk, 100, 'sparkline-disk')}
                    </div>
                    <div class="metric-sub">${data.disk ? data.disk.used : 0} MB / ${data.disk ? data.disk.total : 0} MB</div>
                </div>
            </div>
            <div class="system-uptime">Uptime: ${data.uptime || '-'}</div>
        `;
    },

    renderSystemDetails: function(data) {
        const system = data.system || {};
        const hardware = data.hardware || {};
        const bios = data.bios || {};
        const dnsServers = (data.dnsServers && data.dnsServers.length > 0)
            ? data.dnsServers.join(', ')
            : 'Not set';
        const conntrack = data.conntrack || {};
        const time = data.time || {};
        const user = data.user || {};

        return `
            <div class="system-details-grid">
                <div class="details-card">
                    <div class="details-title">System</div>
                    <div class="details-row"><span>Hostname</span><span>${system.hostname || '-'}</span></div>
                    <div class="details-row"><span>Domain</span><span>${system.domain || '-'}</span></div>
                    <div class="details-row"><span>OS</span><span>${system.os || '-'}</span></div>
                    <div class="details-row"><span>Kernel</span><span>${system.kernel || '-'}</span></div>
                    <div class="details-row"><span>Timezone</span><span>${system.timezone || '-'}</span></div>
                    <div class="details-row"><span>Uptime</span><span>${system.uptime || '-'}</span></div>
                </div>
                <div class="details-card">
                    <div class="details-title">Hardware</div>
                    <div class="details-row"><span>Vendor</span><span>${hardware.vendor || '-'}</span></div>
                    <div class="details-row"><span>Model</span><span>${hardware.model || '-'}</span></div>
                    <div class="details-row"><span>Version</span><span>${hardware.version || '-'}</span></div>
                    <div class="details-row"><span>CPU</span><span>${hardware.cpu || '-'} (${hardware.cores || 0} cores)</span></div>
                    <div class="details-row"><span>BIOS</span><span>${bios.vendor || '-'} ${bios.version || ''}</span></div>
                    <div class="details-row"><span>BIOS Date</span><span>${bios.date || '-'}</span></div>
                </div>
                <div class="details-card">
                    <div class="details-title">Network + State</div>
                    <div class="details-row"><span>DNS</span><span>${dnsServers}</span></div>
                    <div class="details-row"><span>Conntrack</span><span>${conntrack.count || 0} / ${conntrack.max || 0}</span></div>
                    <div class="details-row"><span>User</span><span>${user.name || '-'}</span></div>
                    <div class="details-row"><span>Local Time</span><span>${time.local || '-'}</span></div>
                    <div class="details-row"><span>UTC</span><span>${time.utc || '-'}</span></div>
                    <div class="details-row"><span>Last Update</span><span>${data.lastUpdate || '-'}</span></div>
                </div>
            </div>
        `;
    },

    renderNetworkInfo: function(data) {
        if (!data || !data.interfaces || data.interfaces.length === 0) {
            return '<div class="text-muted">No managed interfaces found.</div>';
        }

        let html = '<div class="network-list">';
        data.interfaces.forEach(iface => {
            const statusClass = iface.status === 'up' ? 'text-success' : 'text-danger';
            const rx = this.formatBytes(iface.rxBytes || 0);
            const tx = this.formatBytes(iface.txBytes || 0);
            html += `
                <div class="network-item">
                    <div class="d-flex justify-content-between align-items-center mb-1">
                        <strong>${iface.name}</strong>
                        <span class="badge bg-${iface.status === 'up' ? 'success' : 'secondary'}">${iface.status}</span>
                    </div>
                    <div class="text-muted small">
                        <div>IP: ${iface.ip}</div>
                        <div>RX: ${rx} | TX: ${tx}</div>
                        ${iface.rxLossPercent !== undefined || iface.txLossPercent !== undefined ? 
                            `<div>Loss: RX ${(iface.rxLossPercent || 0).toFixed(2)}% | TX ${(iface.txLossPercent || 0).toFixed(2)}%</div>` : 
                            ''}
                    </div>
                </div>
            `;
        });
        html += '</div>';
        return html;
    },

    renderSystemTraffic: function(data) {
        if (!data || !data.interfaces) {
            return '<div class="text-muted">No traffic data available.</div>';
        }

        const rates = this.calculateTrafficRates(data);
        const totalRxLoss = data.totalRxLossPercent || 0;
        const totalTxLoss = data.totalTxLossPercent || 0;
        this.pushTrafficSeries(rates.totalRx, rates.totalTx, this.trafficSeries.maxPoints, totalRxLoss, totalTxLoss);

        const rxRate = this.formatRate(rates.totalRx);
        const txRate = this.formatRate(rates.totalTx);

        // Build individual charts with timestamps for total
        const rxChart = this.buildTrafficSparkline(this.trafficSeries.rx, this.trafficSeries.timestamps, 'rx');
        const txChart = this.buildTrafficSparkline(this.trafficSeries.tx, this.trafficSeries.timestamps, 'tx');

        // Build per-interface graphs
        const interfaceGraphs = data.interfaces.map(iface => {
            const rate = rates.interfaces[iface.name] || { rx: 0, tx: 0 };
            const statusBadge = iface.status === 'up' ? 'success' : 'secondary';
            
            // Push interface traffic data
            const rxLoss = iface.rxLossPercent || 0;
            const txLoss = iface.txLossPercent || 0;
            this.pushInterfaceTraffic(iface.name, rate.rx, rate.tx, this.trafficSeries.maxPoints, rxLoss, txLoss);
            
            // Get interface series
            const ifaceSeries = this.trafficSeries.interfaces[iface.name] || { rx: [], tx: [], rxLoss: [], txLoss: [], timestamps: [] };
            
            // Build combined graph for this interface (RX and TX together)
            const ifaceChart = this.buildInterfaceTrafficSparkline(
                ifaceSeries.rx, 
                ifaceSeries.tx, 
                ifaceSeries.timestamps, 
                iface.name,
                ifaceSeries.rxLoss,
                ifaceSeries.txLoss
            );
            
            const ifaceRxLossClass = rxLoss > 1 ? 'text-danger' : rxLoss > 0.1 ? 'text-warning' : 'text-muted';
            const ifaceTxLossClass = txLoss > 1 ? 'text-danger' : txLoss > 0.1 ? 'text-warning' : 'text-muted';
            
            return `
                <div class="interface-traffic-card mb-3 p-3 border rounded">
                    <div class="d-flex justify-content-between align-items-center mb-2">
                        <div class="d-flex align-items-center gap-2">
                            <span class="badge bg-${statusBadge}">${iface.status}</span>
                            <strong>${iface.name}</strong>
                        </div>
                        <div class="d-flex gap-3">
                            <div class="text-primary">
                                <small class="text-muted d-block">RX</small>
                                <strong>${this.formatRate(rate.rx)}</strong>
                                <small class="d-block ${ifaceRxLossClass}">Loss: ${rxLoss.toFixed(2)}%</small>
                            </div>
                            <div class="text-success">
                                <small class="text-muted d-block">TX</small>
                                <strong>${this.formatRate(rate.tx)}</strong>
                                <small class="d-block ${ifaceTxLossClass}">Loss: ${txLoss.toFixed(2)}%</small>
                            </div>
                        </div>
                    </div>
                    ${ifaceChart}
                </div>
            `;
        }).join('');

        const rxLossClass = totalRxLoss > 1 ? 'text-danger' : totalRxLoss > 0.1 ? 'text-warning' : 'text-muted';
        const txLossClass = totalTxLoss > 1 ? 'text-danger' : totalTxLoss > 0.1 ? 'text-warning' : 'text-muted';
        
        return `
            <div class="traffic-summary mb-3 p-3 bg-light rounded">
                <div class="d-flex justify-content-between gap-3">
                    <div class="traffic-metric text-center flex-fill">
                        <div class="traffic-label text-muted small mb-1">Total RX</div>
                        <div class="traffic-value text-primary fw-bold fs-5">${rxRate}</div>
                        <div class="traffic-loss ${rxLossClass} small mt-1">Loss: ${totalRxLoss.toFixed(2)}%</div>
                    </div>
                    <div class="traffic-metric text-center flex-fill">
                        <div class="traffic-label text-muted small mb-1">Total TX</div>
                        <div class="traffic-value text-success fw-bold fs-5">${txRate}</div>
                        <div class="traffic-loss ${txLossClass} small mt-1">Loss: ${totalTxLoss.toFixed(2)}%</div>
                    </div>
                </div>
            </div>
            <div class="interface-traffic-section">
                <h6 class="mb-3 text-muted">Per-Interface Traffic</h6>
                ${interfaceGraphs || '<div class="text-muted">No managed interfaces.</div>'}
            </div>
        `;
    },

    renderActivity: function(data) {
        let html = '<div class="activity-list">';
        data.logs.forEach(log => {
            const typeClass = log.type === 'warning' ? 'warning' : log.type === 'error' ? 'danger' : 'info';
            html += `
                <div class="activity-item">
                    <span class="badge bg-${typeClass}">${log.type}</span>
                    <span class="activity-time">${log.time}</span>
                    <div class="activity-message">${log.message}</div>
                </div>
            `;
        });
        html += '</div>';
        return html;
    },

    renderDhcpStatus: function(data) {
        const statusClass = data.enabled && data.status === 'Running' ? 'success' : 'danger';
        let html = `
            <div class="dhcp-status">
                <div class="d-flex justify-content-between align-items-center mb-3">
                    <div>
                        <strong>Status:</strong>
                        <span class="badge bg-${statusClass} ms-2">${data.status}</span>
                    </div>
                    <div>
                        <strong>Active Leases:</strong>
                        <span class="ms-2">${data.activeLeases} / ${data.poolSize}</span>
                    </div>
                </div>
                <div class="dhcp-leases">
                    <h6 class="mb-2">Recent Leases:</h6>
        `;
        
        if (data.leases && data.leases.length > 0) {
            data.leases.forEach(lease => {
                html += `
                    <div class="lease-item mb-2 p-2 border rounded">
                        <div class="d-flex justify-content-between">
                            <div>
                                <strong>${lease.ip}</strong>
                                <small class="text-muted d-block">${lease.mac}</small>
                            </div>
                            <div class="text-end">
                                <div class="small">${lease.hostname || 'Unknown'}</div>
                                <div class="text-muted small">${lease.expires}</div>
                            </div>
                        </div>
                    </div>
                `;
            });
        } else {
            html += '<div class="text-muted small">No active leases</div>';
        }
        
        html += '</div></div>';
        return html;
    },

    pushSeries: function(series, value, maxPoints) {
        if (!Array.isArray(series)) {
            return;
        }

        const numeric = Number(value);
        series.push(Number.isFinite(numeric) ? numeric : 0);
        if (series.length > maxPoints) {
            series.shift();
        }
    },

    pushTrafficSeries: function(rxValue, txValue, maxPoints, rxLossValue, txLossValue) {
        const rxNumeric = Number(rxValue);
        const txNumeric = Number(txValue);
        const rxLossNumeric = Number(rxLossValue) || 0;
        const txLossNumeric = Number(txLossValue) || 0;
        
        this.trafficSeries.rx.push(Number.isFinite(rxNumeric) ? rxNumeric : 0);
        this.trafficSeries.tx.push(Number.isFinite(txNumeric) ? txNumeric : 0);
        this.trafficSeries.rxLoss.push(Number.isFinite(rxLossNumeric) ? rxLossNumeric : 0);
        this.trafficSeries.txLoss.push(Number.isFinite(txLossNumeric) ? txLossNumeric : 0);
        this.trafficSeries.timestamps.push(new Date());
        
        if (this.trafficSeries.rx.length > maxPoints) {
            this.trafficSeries.rx.shift();
            this.trafficSeries.tx.shift();
            this.trafficSeries.rxLoss.shift();
            this.trafficSeries.txLoss.shift();
            this.trafficSeries.timestamps.shift();
        }
    },

    pushInterfaceTraffic: function(interfaceName, rxValue, txValue, maxPoints, rxLossValue, txLossValue) {
        if (!this.trafficSeries.interfaces[interfaceName]) {
            this.trafficSeries.interfaces[interfaceName] = {
                rx: [],
                tx: [],
                rxLoss: [],
                txLoss: [],
                timestamps: []
            };
        }
        
        const iface = this.trafficSeries.interfaces[interfaceName];
        const rxNumeric = Number(rxValue);
        const txNumeric = Number(txValue);
        const rxLossNumeric = Number(rxLossValue) || 0;
        const txLossNumeric = Number(txLossValue) || 0;
        
        iface.rx.push(Number.isFinite(rxNumeric) ? rxNumeric : 0);
        iface.tx.push(Number.isFinite(txNumeric) ? txNumeric : 0);
        iface.rxLoss.push(Number.isFinite(rxLossNumeric) ? rxLossNumeric : 0);
        iface.txLoss.push(Number.isFinite(txLossNumeric) ? txLossNumeric : 0);
        iface.timestamps.push(new Date());
        
        if (iface.rx.length > maxPoints) {
            iface.rx.shift();
            iface.tx.shift();
            iface.rxLoss.shift();
            iface.txLoss.shift();
            iface.timestamps.shift();
        }
    },

    buildSparklinePath: function(series, maxValue) {
        const points = Array.isArray(series) ? series : [];
        if (points.length === 0) {
            return 'M0,30 L100,30';
        }

        const max = maxValue || Math.max(...points, 1);
        const height = 30;
        const width = 100;
        const step = points.length > 1 ? width / (points.length - 1) : width;

        return points.map((value, index) => {
            const x = index * step;
            const y = height - (Math.min(value, max) / max) * height;
            return `${index === 0 ? 'M' : 'L'}${x.toFixed(2)},${y.toFixed(2)}`;
        }).join(' ');
    },

    buildSparkline: function(series, maxValue, className) {
        const path = this.buildSparklinePath(series, maxValue);
        return `
            <svg viewBox="0 0 100 30" class="sparkline">
                <path class="${className}" d="${path}" />
            </svg>
        `;
    },

    buildTrafficSparkline: function(series, timestamps, type) {
        const points = Array.isArray(series) ? series : [];
        const times = Array.isArray(timestamps) ? timestamps : [];
        
        if (points.length === 0) {
            return '<div class="text-muted small">No data yet</div>';
        }

        const max = Math.max(...points, 1);
        const height = 60;
        const width = 100;
        const step = points.length > 1 ? width / (points.length - 1) : width;
        const color = type === 'rx' ? '#0d6efd' : '#198754';
        
        // Build Y-axis scale (4 tick marks: 0, 25%, 50%, 75%, 100%)
        const scaleSteps = 4;
        const scaleValues = [];
        for (let i = 0; i <= scaleSteps; i++) {
            const value = (max * (scaleSteps - i) / scaleSteps);
            scaleValues.push({
                value: value,
                y: (i * height / scaleSteps),
                label: this.formatRate ? this.formatRate(value) : this.formatBytes(value)
            });
        }

        const pathData = points.map((value, index) => {
            const x = index * step;
            const y = height - (value / max) * height;
            return `${index === 0 ? 'M' : 'L'} ${x},${y}`;
        }).join(' ');
        
        // Build timestamp labels (show first, middle, last)
        let timeLabels = '';
        if (times.length > 0) {
            const firstTime = times[0];
            const lastTime = times[times.length - 1];
            const midIndex = Math.floor(times.length / 2);
            const midTime = times[midIndex];
            
            const formatTime = (date) => {
                if (!date) return '';
                const d = new Date(date);
                return d.toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
            };
            
            timeLabels = `
                <div class="traffic-timestamps d-flex justify-content-between mt-1">
                    <small class="text-muted">${formatTime(firstTime)}</small>
                    ${times.length > 2 ? `<small class="text-muted">${formatTime(midTime)}</small>` : '<small></small>'}
                    <small class="text-muted">${formatTime(lastTime)}</small>
                </div>
            `;
        }

        return `
            <div class="traffic-sparkline-wrapper d-flex align-items-start gap-2">
                <div class="traffic-y-axis text-muted small" style="min-width: 70px; text-align: right; padding-top: 2px;">
                    ${scaleValues.map(s => `<div style="height: ${height / scaleSteps}px; line-height: ${height / scaleSteps}px;">${s.label}</div>`).join('')}
                </div>
                <div class="flex-fill">
                    <svg class="traffic-sparkline" viewBox="0 0 ${width} ${height}" preserveAspectRatio="none" style="width: 100%; height: ${height}px;">
                        <path d="${pathData}" stroke="${color}" stroke-width="1" fill="none" />
                        <path d="M0,${height} ${pathData} L${width},${height}" fill="${color}" opacity="0.1" />
                    </svg>
                    ${timeLabels}
                </div>
            </div>
        `;
    },

    buildInterfaceTrafficSparkline: function(rxSeries, txSeries, timestamps, interfaceName, rxLossSeries, txLossSeries) {
        const rxPoints = Array.isArray(rxSeries) ? rxSeries : [];
        const txPoints = Array.isArray(txSeries) ? txSeries : [];
        const rxLossPoints = Array.isArray(rxLossSeries) ? rxLossSeries : [];
        const txLossPoints = Array.isArray(txLossSeries) ? txLossSeries : [];
        const times = Array.isArray(timestamps) ? timestamps : [];
        
        if (rxPoints.length === 0 && txPoints.length === 0) {
            return '<div class="text-muted small">No data yet</div>';
        }

        // Calculate max for traffic (bytes/sec) - use separate scale for loss (%)
        const maxTraffic = Math.max(
            Math.max(...rxPoints, 0),
            Math.max(...txPoints, 0),
            1
        );
        const maxLoss = Math.max(
            Math.max(...rxLossPoints, 0),
            Math.max(...txLossPoints, 0),
            1
        );
        const height = 50;
        const width = 100;
        const step = Math.max(rxPoints.length, txPoints.length) > 1 
            ? width / (Math.max(rxPoints.length, txPoints.length) - 1) 
            : width;

        const currentRx = rxPoints.length > 0 ? rxPoints[rxPoints.length - 1] : 0;
        const currentTx = txPoints.length > 0 ? txPoints[txPoints.length - 1] : 0;

        const rxPathData = rxPoints.map((value, index) => {
            const x = index * step;
            const y = height - (value / maxTraffic) * height;
            return `${index === 0 ? 'M' : 'L'} ${x},${y}`;
        }).join(' ');

        const txPathData = txPoints.map((value, index) => {
            const x = index * step;
            const y = height - (value / maxTraffic) * height;
            return `${index === 0 ? 'M' : 'L'} ${x},${y}`;
        }).join(' ');

        // Build Y-axis scale (4 tick marks: 0, 25%, 50%, 75%, 100%)
        const scaleSteps = 4;
        const scaleValues = [];
        for (let i = 0; i <= scaleSteps; i++) {
            const value = (maxTraffic * (scaleSteps - i) / scaleSteps);
            scaleValues.push({
                value: value,
                y: (i * height / scaleSteps),
                label: this.formatRate ? this.formatRate(value) : this.formatBytes(value)
            });
        }

        // Packet loss paths (scaled to fit in the same graph, shown as percentage of height)
        const rxLossPathData = rxLossPoints.length > 0 ? rxLossPoints.map((value, index) => {
            const x = index * step;
            // Scale loss % to graph height (e.g., 10% loss = 10% of graph height from bottom)
            const lossScale = Math.min(maxLoss, 10); // Cap at 10% for visibility
            const y = height - (value / lossScale) * height;
            return `${index === 0 ? 'M' : 'L'} ${x},${y}`;
        }).join(' ') : '';

        const txLossPathData = txLossPoints.length > 0 ? txLossPoints.map((value, index) => {
            const x = index * step;
            const lossScale = Math.min(maxLoss, 10);
            const y = height - (value / lossScale) * height;
            return `${index === 0 ? 'M' : 'L'} ${x},${y}`;
        }).join(' ') : '';

        // Build timestamp labels
        let timeLabels = '';
        if (times.length > 0) {
            const firstTime = times[0];
            const lastTime = times[times.length - 1];
            const midIndex = Math.floor(times.length / 2);
            const midTime = times[midIndex];
            
            const formatTime = (date) => {
                if (!date) return '';
                const d = new Date(date);
                return d.toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
            };
            
            timeLabels = `
                <div class="traffic-timestamps d-flex justify-content-between mt-1 pt-1 border-top">
                    <small class="text-muted">${formatTime(firstTime)}</small>
                    ${times.length > 2 ? `<small class="text-muted">${formatTime(midTime)}</small>` : '<small></small>'}
                    <small class="text-muted">${formatTime(lastTime)}</small>
                </div>
            `;
        }

        return `
            <div class="interface-sparkline-wrapper d-flex align-items-start gap-2">
                <div class="interface-y-axis text-muted small" style="min-width: 70px; text-align: right; padding-top: 2px;">
                    ${scaleValues.map(s => `<div style="height: ${height / scaleSteps}px; line-height: ${height / scaleSteps}px;">${s.label}</div>`).join('')}
                </div>
                <div class="flex-fill">
                    <svg class="interface-sparkline" viewBox="0 0 ${width} ${height}" preserveAspectRatio="none" style="width: 100%; height: ${height}px;">
                        ${rxPathData ? `
                            <path d="M0,${height} ${rxPathData} L${width},${height}" fill="#0d6efd" opacity="0.15" />
                            <path d="${rxPathData}" stroke="#0d6efd" stroke-width="1" fill="none" />
                        ` : ''}
                        ${txPathData ? `
                            <path d="M0,${height} ${txPathData} L${width},${height}" fill="#198754" opacity="0.15" />
                            <path d="${txPathData}" stroke="#198754" stroke-width="1" fill="none" />
                        ` : ''}
                        ${rxLossPathData ? `
                            <path d="${rxLossPathData}" stroke="#dc3545" stroke-width="0.8" fill="none" stroke-dasharray="3,2" opacity="0.8" />
                        ` : ''}
                        ${txLossPathData ? `
                            <path d="${txLossPathData}" stroke="#fd7e14" stroke-width="0.8" fill="none" stroke-dasharray="3,2" opacity="0.8" />
                        ` : ''}
                    </svg>
                    <div class="d-flex justify-content-center gap-3 mt-1">
                        <small class="text-primary"><i class="fa-solid fa-circle" style="font-size: 0.5rem;"></i> RX</small>
                        <small class="text-success"><i class="fa-solid fa-circle" style="font-size: 0.5rem;"></i> TX</small>
                        ${rxLossPoints.length > 0 || txLossPoints.length > 0 ? `
                            <small class="text-danger"><i class="fa-solid fa-circle" style="font-size: 0.5rem;"></i> RX Loss</small>
                            <small class="text-warning"><i class="fa-solid fa-circle" style="font-size: 0.5rem;"></i> TX Loss</small>
                        ` : ''}
                    </div>
                    ${timeLabels}
                </div>
            </div>
        `;
    },

    buildDualSparkline: function(rxSeries, txSeries) {
        const max = Math.max(
            ...(rxSeries || []),
            ...(txSeries || []),
            1
        );
        const rxPath = this.buildSparklinePath(rxSeries, max);
        const txPath = this.buildSparklinePath(txSeries, max);

        return `
            <svg viewBox="0 0 100 30" class="sparkline">
                <path class="sparkline-rx" d="${rxPath}" />
                <path class="sparkline-tx" d="${txPath}" />
            </svg>
        `;
    },

    formatBytes: function(bytes) {
        const value = Number(bytes) || 0;
        if (value <= 0) {
            return '0 B';
        }

        const units = ['B', 'KB', 'MB', 'GB', 'TB'];
        let size = value;
        let unitIndex = 0;
        while (size >= 1024 && unitIndex < units.length - 1) {
            size /= 1024;
            unitIndex += 1;
        }

        const digits = size >= 10 || unitIndex === 0 ? 0 : 1;
        return `${size.toFixed(digits)} ${units[unitIndex]}`;
    },

    formatRate: function(bytesPerSecond) {
        return `${this.formatBytes(bytesPerSecond)}/s`;
    },

    calculateTrafficRates: function(data) {
        const now = Date.now();
        const last = this.trafficLast;
        const totalRxBytes = Number(data.totalRxBytes) || 0;
        const totalTxBytes = Number(data.totalTxBytes) || 0;
        const rates = {
            totalRx: 0,
            totalTx: 0,
            interfaces: {}
        };

        const interfaceMap = {};
        if (Array.isArray(data.interfaces)) {
            data.interfaces.forEach(iface => {
                interfaceMap[iface.name] = {
                    rxBytes: Number(iface.rxBytes) || 0,
                    txBytes: Number(iface.txBytes) || 0,
                    rxLossPercent: Number(iface.rxLossPercent) || 0,
                    txLossPercent: Number(iface.txLossPercent) || 0
                };
            });
        }

        if (last && last.timestamp && now > last.timestamp) {
            const deltaSec = (now - last.timestamp) / 1000;
            if (deltaSec > 0) {
                rates.totalRx = Math.max(0, (totalRxBytes - last.totalRxBytes) / deltaSec);
                rates.totalTx = Math.max(0, (totalTxBytes - last.totalTxBytes) / deltaSec);
            }

            Object.keys(interfaceMap).forEach(name => {
                const current = interfaceMap[name];
                const prev = last.interfaces && last.interfaces[name];
                let rxRate = 0;
                let txRate = 0;

                if (prev && deltaSec > 0) {
                    rxRate = Math.max(0, (current.rxBytes - prev.rxBytes) / deltaSec);
                    txRate = Math.max(0, (current.txBytes - prev.txBytes) / deltaSec);
                }

                rates.interfaces[name] = { rx: rxRate, tx: txRate };
            });
        } else {
            Object.keys(interfaceMap).forEach(name => {
                rates.interfaces[name] = { rx: 0, tx: 0 };
            });
        }

        this.trafficLast = {
            timestamp: now,
            totalRxBytes: totalRxBytes,
            totalTxBytes: totalTxBytes,
            totalRxLossPercent: Number(data.totalRxLossPercent) || 0,
            totalTxLossPercent: Number(data.totalTxLossPercent) || 0,
            interfaces: interfaceMap
        };

        return rates;
    },

    initDragDrop: function() {
        if (typeof $.fn.sortable === 'undefined') {
            console.warn('jQuery UI Sortable not available');
            return;
        }

        // Destroy existing sortable if it exists
        if ($('#widget-grid').hasClass('ui-sortable')) {
            $('#widget-grid').sortable('destroy');
        }

        $('#widget-grid').sortable({
            handle: '.widget-drag-handle',
            placeholder: 'widget-placeholder',
            tolerance: 'pointer',
            cursor: 'move',
            opacity: 0.8,
            disabled: true, // Always disabled by default, will be enabled in edit mode
            start: function(e, ui) {
                ui.placeholder.height(ui.item.height());
            },
            stop: () => {
                // Only save if in edit mode
                if (this.editMode) {
                    this.saveLayout();
                }
            }
        });
        
        // Update drag handles and sortable state based on edit mode
        if (this.editMode) {
            $('#widget-grid').sortable('enable');
            $('.widget-drag-handle').css('cursor', 'move').css('opacity', '1');
        } else {
            $('#widget-grid').sortable('disable');
            $('.widget-drag-handle').css('cursor', 'default').css('opacity', '0.5');
        }
    },

    startAutoRefresh: function() {
        // Clear existing timers
        Object.values(this.refreshTimers).forEach(timer => clearInterval(timer));
        this.refreshTimers = {};

        // Only refresh widgets that are in the layout and visible
        if (!this.layout || !this.layout.widgets) {
            return;
        }

        this.layout.widgets.forEach(layoutWidget => {
            // Only refresh visible widgets
            if (!layoutWidget.visible) {
                return;
            }

            // Find widget definition to get refresh interval
            const widgetDef = this.widgets.find(w => w.id === layoutWidget.id);
            if (widgetDef && widgetDef.refreshInterval > 0) {
                this.refreshTimers[layoutWidget.id] = setInterval(() => {
                    this.loadWidgetData(layoutWidget.id);
                }, widgetDef.refreshInterval * 1000);
            }
        });
    },

    saveLayout: async function() {
        // If edit mode, get order from DOM
        if (this.editMode) {
            const widgets = [];
            let order = 1;

            $('#widget-grid .widget-card').each(function() {
                const widgetId = $(this).data('widget-id');
                const layoutWidget = Dashboard.layout.widgets.find(w => w.id === widgetId);
                
                widgets.push({
                    id: widgetId,
                    order: order++,
                    width: layoutWidget?.width || 4,
                    height: layoutWidget?.height || 2,
                    visible: true
                });
            });

            this.layout = { 
                widgets,
                gridColumns: this.gridColumns
            };
        } else if (this.layout) {
            // Ensure gridColumns is saved
            this.layout.gridColumns = this.gridColumns;
        }

        const layout = { 
            widgets: this.layout.widgets,
            gridColumns: this.gridColumns
        };

        try {
            const response = await Monolith.API.post('/dashboard/layout', layout);
            if (response.success) {
                this.layout = layout;
                console.log('Layout saved with grid columns:', this.gridColumns);
                return Promise.resolve();
            } else {
                throw new Error(response.error || 'Failed to save');
            }
        } catch (error) {
            console.error('Error saving layout:', error);
            Monolith.UI.toast('Failed to save layout', 'error');
            return Promise.reject(error);
        }
    },

    toggleEditMode: function() {
        this.editMode = !this.editMode;
        const btn = $('#edit-layout-btn');
        
        if (this.editMode) {
            btn.removeClass('btn-outline-secondary').addClass('btn-primary');
            btn.text('Save Layout');
            // Enable drag-drop
            if ($('#widget-grid').hasClass('ui-sortable')) {
                $('#widget-grid').sortable('enable');
            }
            $('.widget-drag-handle').css('cursor', 'move').css('opacity', '1');
            // Show remove buttons
            $('.btn-widget-remove').css('display', 'inline-block');
            Monolith.UI.toast('Edit mode enabled - drag widgets to reorder', 'info');
        } else {
            btn.removeClass('btn-primary').addClass('btn-outline-secondary');
            btn.text('Edit Layout');
            // Disable drag-drop and save layout
            if ($('#widget-grid').hasClass('ui-sortable')) {
                $('#widget-grid').sortable('disable');
            }
            $('.widget-drag-handle').css('cursor', 'default').css('opacity', '0.5');
            // Hide remove buttons
            $('.btn-widget-remove').css('display', 'none');
            this.saveLayout();
        }
    },

    showGridSettingsModal: function() {
        const modal = new bootstrap.Modal(document.getElementById('grid-settings-modal'));
        
        // Highlight current selection
        $('.grid-option-card').each(function() {
            const columns = parseInt($(this).data('columns'));
            if (columns === Dashboard.gridColumns) {
                $(this).css('border-color', '#0d6efd').css('background-color', 'rgba(13, 110, 253, 0.1)');
            } else {
                $(this).css('border-color', '#dee2e6').css('background-color', 'transparent');
            }
        });
        
        modal.show();
    },

    setGridColumns: function(columns) {
        this.gridColumns = columns;
        this.applyGridColumns();
        
        // Save to layout
        if (!this.layout) {
            this.layout = this.getDefaultLayout();
        }
        this.layout.gridColumns = columns;
        this.saveLayout();
        
        Monolith.UI.toast(`Grid set to ${this.getGridColumnLabel(columns)}`, 'success');
    },

    applyGridColumns: function() {
        const grid = $('#widget-grid');
        if (grid.length) {
            grid.css('grid-template-columns', `repeat(${this.gridColumns}, 1fr)`);
        }
    },

    getGridColumnLabel: function(columns) {
        if (columns === 12) return '3 columns';
        if (columns === 6) return '2 columns';
        if (columns === 4) return '1 column';
        return `${columns} columns`;
    },

    showAddWidgetModal: function() {
        const modal = new bootstrap.Modal(document.getElementById('add-widget-modal'));
        this.renderAvailableWidgets();
        modal.show();
    },

    renderAvailableWidgets: function(filter = '') {
        const container = $('#available-widgets-list');
        container.empty();

        if (!this.widgets || this.widgets.length === 0) {
            container.html('<div class="text-muted">No widgets available</div>');
            return;
        }

        // Get currently added widget IDs
        const currentWidgetIds = (this.layout?.widgets || []).map(w => w.id);

        // Filter widgets
        const filtered = this.widgets.filter(w => {
            const matchesFilter = !filter || 
                w.title.toLowerCase().includes(filter.toLowerCase()) ||
                w.description.toLowerCase().includes(filter.toLowerCase()) ||
                w.id.toLowerCase().includes(filter.toLowerCase());
            return matchesFilter;
        });

        if (filtered.length === 0) {
            container.html('<div class="text-muted">No widgets match your search</div>');
            return;
        }

        filtered.forEach(widget => {
            const isAdded = currentWidgetIds.includes(widget.id);
            const addBtn = isAdded 
                ? '<span class="badge bg-success">Added</span>'
                : `<button class="btn btn-sm btn-primary add-widget-item" data-widget-id="${widget.id}">Add</button>`;

            container.append(`
                <div class="widget-item-card p-3 mb-2 border rounded">
                    <div class="d-flex justify-content-between align-items-start">
                        <div class="flex-grow-1">
                            <h6 class="mb-1">${widget.title}</h6>
                            <p class="text-muted small mb-1">${widget.description}</p>
                            <div class="small text-muted">
                                <span>Package: ${widget.package}</span>
                                <span class="ms-2">Refresh: ${widget.refreshInterval}s</span>
                            </div>
                        </div>
                        <div>
                            ${addBtn}
                        </div>
                    </div>
                </div>
            `);
        });

        // Attach click handlers
        $('.add-widget-item').on('click', (e) => {
            const widgetId = $(e.currentTarget).data('widget-id');
            this.addWidget(widgetId);
        });
    },

    filterAvailableWidgets: function() {
        const filter = $('#widget-search').val();
        this.renderAvailableWidgets(filter);
    },

    addWidget: function(widgetId) {
        const widget = this.widgets.find(w => w.id === widgetId);
        if (!widget) {
            Monolith.UI.toast('Widget not found', 'error');
            return;
        }

        // Check if already added
        if (this.layout.widgets.some(w => w.id === widgetId)) {
            Monolith.UI.toast('Widget already added', 'info');
            return;
        }

        // Add widget to layout
        const maxOrder = this.layout.widgets.length > 0 
            ? Math.max(...this.layout.widgets.map(w => w.order))
            : 0;

        this.layout.widgets.push({
            id: widgetId,
            order: maxOrder + 1,
            width: widget.defaultWidth || 4,
            height: widget.defaultHeight || 2,
            visible: true
        });

        // Save layout
        this.saveLayout().then(() => {
            // Re-render dashboard
            this.renderWidgets();
            this.initDragDrop();
            this.startAutoRefresh();
            
            // Close modal
            const modal = bootstrap.Modal.getInstance(document.getElementById('add-widget-modal'));
            if (modal) modal.hide();
            
            Monolith.UI.toast(`Added ${widget.title} widget`, 'success');
        });
    },

    resetLayout: async function() {
        if (!confirm('Reset dashboard to default layout?')) return;

        try {
            // Send empty widgets array to clear the layout
            const response = await Monolith.API.post('/dashboard/layout', { widgets: [] });
            if (response.success) {
                // Clear current layout
                this.layout = null;
                // Reload widgets to get the default layout
                await this.loadWidgets();
                Monolith.UI.toast('Layout reset successfully', 'success');
            } else {
                throw new Error(response.error || 'Failed to reset layout');
            }
        } catch (error) {
            console.error('Error resetting layout:', error);
            Monolith.UI.toast('Failed to reset layout: ' + error.message, 'error');
        }
    },

    destroy: function() {
        // Clean up timers
        Object.values(this.refreshTimers).forEach(timer => clearInterval(timer));
        this.refreshTimers = {};
    }
};

// Backward compatibility: expose as global Dashboard
window.Dashboard = Monolith.Pages.Dashboard;
