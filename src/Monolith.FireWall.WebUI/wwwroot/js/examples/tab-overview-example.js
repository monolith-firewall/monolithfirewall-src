/**
 * Example Tab Module: Overview Tab
 *
 * This is loaded dynamically when the Overview tab is clicked.
 * Naming convention: {ParentPage}_Tab{TabName}
 */

var MyComplexPage_TabOverview = {
    _parent: null,
    _container: null,
    _data: null,

    /**
     * Called once when module is first loaded
     * @param {object} parent - Reference to parent page module
     */
    init: function(parent) {
        this._parent = parent;
        console.log('[TabOverview] Initialized');
    },

    /**
     * Called each time the tab is shown
     * @param {string} containerSelector - Where to render content
     */
    render: function(containerSelector) {
        this._container = containerSelector;

        var html = `
            <div class="card">
                <div class="card-header">
                    <h5 class="mb-0">Overview</h5>
                </div>
                <div class="card-body">
                    <div id="overview-content">
                        <div class="text-center py-4">
                            <div class="spinner-border spinner-border-sm"></div>
                            <span class="ms-2">Loading overview data...</span>
                        </div>
                    </div>
                </div>
            </div>
        `;

        $(this._container).html(html);
        this.loadData();
    },

    /**
     * Called when tab becomes visible (after render or when switching back)
     */
    onShow: function() {
        console.log('[TabOverview] Tab shown');
        // Optionally refresh data when tab is shown again
    },

    /**
     * Called when switching away from this tab
     */
    onHide: function() {
        console.log('[TabOverview] Tab hidden');
        // Pause any intervals, cleanup temporary state
    },

    /**
     * Called when parent page is destroyed
     */
    destroy: function() {
        console.log('[TabOverview] Destroyed');
        // Full cleanup
    },

    loadData: async function() {
        try {
            // Use parent's Core.call or direct API
            var response = await Monolith.Core.call('some.overview.action', {});

            if (response.success) {
                this._data = response.data;
                this.renderContent();
            } else {
                this._parent.utils.showError('#overview-content',
                    'Failed to load: ' + (response.error || 'Unknown error'));
            }
        } catch (error) {
            this._parent.utils.showError('#overview-content', error.message);
        }
    },

    renderContent: function() {
        // Render actual content
        var html = `
            <div class="row">
                <div class="col-md-6">
                    <h6>Status</h6>
                    <p class="text-success">
                        <i class="bi bi-check-circle me-1"></i>All systems operational
                    </p>
                </div>
                <div class="col-md-6">
                    <h6>Last Updated</h6>
                    <p class="text-muted">${new Date().toLocaleString()}</p>
                </div>
            </div>
            <hr>
            <p>This content was loaded dynamically when you clicked the Overview tab.</p>
            <button class="btn btn-primary" onclick="MyComplexPage_TabOverview.refresh()">
                <i class="bi bi-arrow-clockwise me-1"></i>Refresh
            </button>
        `;

        $('#overview-content').html(html);
    },

    refresh: function() {
        this._parent.utils.showLoading('#overview-content');
        this.loadData();
    }
};
