/**
 * Example: Complex Tabbed Page with Dynamic JS Loading
 *
 * This pattern splits a complex page into:
 * - Main page JS (this file) - handles tabs, common UI, loads sub-modules
 * - Tab-specific JS files - loaded on demand when tab is first clicked
 */

// Ensure Monolith.Core exists
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
                return { success: false, data: null, error: error.message };
            }
        }
    };
}

var MyComplexPage = {
    // Track which tab modules have been loaded
    _loadedTabs: {},
    _activeTab: null,

    init: function() {
        console.log('Initializing complex page...');
        this.render();
        this.attachTabHandlers();

        // Load default tab
        this.switchTab('overview');
    },

    destroy: function() {
        // Cleanup all loaded tab modules
        Object.keys(this._loadedTabs).forEach(tabId => {
            var module = this._loadedTabs[tabId];
            if (module && typeof module.destroy === 'function') {
                module.destroy();
            }
        });
        this._loadedTabs = {};
    },

    render: function() {
        var html = `
            <div class="page-header mb-4">
                <h1>Complex Page Example</h1>
            </div>

            <!-- Tab Navigation -->
            <ul class="nav nav-tabs mb-4" id="page-tabs">
                <li class="nav-item">
                    <a class="nav-link" href="#" data-tab="overview">
                        <i class="bi bi-house me-1"></i>Overview
                    </a>
                </li>
                <li class="nav-item">
                    <a class="nav-link" href="#" data-tab="configuration">
                        <i class="bi bi-gear me-1"></i>Configuration
                    </a>
                </li>
                <li class="nav-item">
                    <a class="nav-link" href="#" data-tab="advanced">
                        <i class="bi bi-sliders me-1"></i>Advanced
                    </a>
                </li>
                <li class="nav-item">
                    <a class="nav-link" href="#" data-tab="logs">
                        <i class="bi bi-journal-text me-1"></i>Logs
                    </a>
                </li>
            </ul>

            <!-- Tab Content Container -->
            <div id="tab-content">
                <div class="text-center py-4">
                    <div class="spinner-border"></div>
                    <p class="mt-2">Loading...</p>
                </div>
            </div>
        `;
        $('#my-complex-page-container').html(html);
    },

    attachTabHandlers: function() {
        var self = this;
        $('#page-tabs .nav-link').on('click', function(e) {
            e.preventDefault();
            var tabId = $(this).data('tab');
            self.switchTab(tabId);
        });
    },

    switchTab: async function(tabId) {
        var self = this;

        // Update active tab styling
        $('#page-tabs .nav-link').removeClass('active');
        $(`#page-tabs .nav-link[data-tab="${tabId}"]`).addClass('active');

        // Destroy previous tab module if it has cleanup
        if (this._activeTab && this._loadedTabs[this._activeTab]) {
            var prevModule = this._loadedTabs[this._activeTab];
            if (typeof prevModule.onHide === 'function') {
                prevModule.onHide();
            }
        }

        this._activeTab = tabId;

        // Show loading
        $('#tab-content').html(`
            <div class="text-center py-4">
                <div class="spinner-border spinner-border-sm"></div>
                <span class="ms-2">Loading ${tabId}...</span>
            </div>
        `);

        // Load tab module if not already loaded
        if (!this._loadedTabs[tabId]) {
            try {
                await this._loadTabModule(tabId);
            } catch (error) {
                console.error(`Failed to load tab ${tabId}:`, error);
                $('#tab-content').html(`
                    <div class="alert alert-danger">
                        Failed to load tab: ${error.message}
                    </div>
                `);
                return;
            }
        }

        // Initialize/show the tab
        var module = this._loadedTabs[tabId];
        if (module) {
            // Render the tab's container
            $('#tab-content').html(`<div id="tab-${tabId}-container"></div>`);

            // Call the module's render/show method
            if (typeof module.render === 'function') {
                module.render(`#tab-${tabId}-container`);
            }
            if (typeof module.onShow === 'function') {
                module.onShow();
            }
        }
    },

    /**
     * Dynamically load a tab's JavaScript module
     */
    _loadTabModule: function(tabId) {
        var self = this;

        return new Promise((resolve, reject) => {
            // Map tab IDs to their JS files
            var tabScripts = {
                'overview': '/js/pages/my-complex-page/tab-overview.js',
                'configuration': '/js/pages/my-complex-page/tab-configuration.js',
                'advanced': '/js/pages/my-complex-page/tab-advanced.js',
                'logs': '/js/pages/my-complex-page/tab-logs.js'
            };

            var scriptUrl = tabScripts[tabId];
            if (!scriptUrl) {
                reject(new Error(`Unknown tab: ${tabId}`));
                return;
            }

            // Check if already loading/loaded
            if (self._loadedTabs[tabId]) {
                resolve(self._loadedTabs[tabId]);
                return;
            }

            // Create script element
            var script = document.createElement('script');
            script.src = scriptUrl + '?v=' + Date.now(); // Cache bust
            script.async = true;

            script.onload = function() {
                // The loaded script should register itself
                // Convention: window.MyComplexPage_TabOverview, etc.
                var moduleNames = {
                    'overview': 'MyComplexPage_TabOverview',
                    'configuration': 'MyComplexPage_TabConfiguration',
                    'advanced': 'MyComplexPage_TabAdvanced',
                    'logs': 'MyComplexPage_TabLogs'
                };

                var moduleName = moduleNames[tabId];
                var module = window[moduleName];

                if (module) {
                    self._loadedTabs[tabId] = module;

                    // Initialize the module if it has an init method
                    if (typeof module.init === 'function') {
                        module.init(self); // Pass parent reference
                    }

                    resolve(module);
                } else {
                    reject(new Error(`Module ${moduleName} not found after loading script`));
                }
            };

            script.onerror = function() {
                reject(new Error(`Failed to load script: ${scriptUrl}`));
            };

            document.head.appendChild(script);
        });
    },

    /**
     * Shared utilities that tab modules can use
     */
    utils: {
        showLoading: function(container) {
            $(container).html(`
                <div class="text-center py-4">
                    <div class="spinner-border spinner-border-sm"></div>
                </div>
            `);
        },

        showError: function(container, message) {
            $(container).html(`
                <div class="alert alert-danger">${message}</div>
            `);
        },

        escapeHtml: function(text) {
            if (!text) return '';
            var div = document.createElement('div');
            div.textContent = text;
            return div.innerHTML;
        }
    }
};

// Initialize on DOM ready
$(document).ready(function() {
    MyComplexPage.init();
});

// Cleanup on page unload
$(window).on('beforeunload', function() {
    MyComplexPage.destroy();
});
