/**
 * Monolith.PageBase - Base factory for creating page modules with SignalR integration
 *
 * Provides consistent patterns for:
 * - SignalR channel subscription/unsubscription
 * - Event routing to page handlers
 * - Polling fallback when SignalR disconnects
 * - Lifecycle management (init/destroy)
 *
 * Usage:
 *   var MyPage = Monolith.PageBase.create({
 *       channels: ['interfaces', 'gateways'],  // SignalR channels to subscribe
 *       pollInterval: 30000,                    // Fallback polling interval (ms)
 *
 *       // Event handlers - called when SignalR events arrive
 *       events: {
 *           'InterfaceStatusChanged': function(data) {
 *               this.updateInterfaceRow(data.interfaceName, data);
 *           },
 *           'GatewayStatusChanged': function(data) {
 *               this.updateGatewayRow(data.gatewayId, data);
 *           }
 *       },
 *
 *       // Lifecycle hooks
 *       init: function() {
 *           this.data = [];
 *           this.renderPage();
 *       },
 *
 *       loadData: async function() {
 *           const response = await Monolith.API.get('/api/mydata');
 *           this.data = response.data || [];
 *           this.renderTable();
 *       },
 *
 *       renderPage: function() {
 *           // Render page structure
 *       },
 *
 *       // Custom methods
 *       updateInterfaceRow: function(name, data) { ... },
 *       updateGatewayRow: function(id, data) { ... }
 *   });
 *
 *   // Initialize page
 *   MyPage.init();
 *
 *   // Cleanup when leaving (optional - for SPA navigation)
 *   MyPage.destroy();
 */
(function() {
    'use strict';

    const PageBase = {
        /**
         * Create a new page module with SignalR integration.
         * @param {Object} config - Page configuration
         * @param {string[]} config.channels - SignalR channels to subscribe to
         * @param {number} config.pollInterval - Fallback polling interval in ms
         * @param {Object} config.events - Event name -> handler map
         * @param {Function} config.init - Initialization hook
         * @param {Function} config.destroy - Cleanup hook
         * @param {Function} config.loadData - Data loading function
         * @param {Function} config.renderPage - Page rendering function
         * @returns {Object} Page module instance
         */
        create: function(config) {
            config = config || {};

            const page = {
                // Configuration
                _channels: config.channels || [],
                _events: config.events || {},
                _pollInterval: config.pollInterval || 30000,
                _pollTimer: null,
                _isActive: false,
                _signalRHandler: null,

                /**
                 * Initialize the page.
                 * - Sets up SignalR subscriptions
                 * - Calls custom init hook
                 * - Loads initial data
                 */
                init: function() {
                    this._isActive = true;

                    // Subscribe to SignalR channels
                    this._subscribeToChannels();

                    // Call custom init
                    if (config.init) {
                        config.init.call(this);
                    }

                    // Load initial data
                    if (config.loadData) {
                        this.loadData();
                    }
                },

                /**
                 * Destroy/cleanup the page.
                 * - Unsubscribes from SignalR channels
                 * - Stops polling
                 * - Calls custom destroy hook
                 */
                destroy: function() {
                    this._isActive = false;

                    // Unsubscribe from channels
                    this._unsubscribeFromChannels();

                    // Stop polling
                    this._stopPolling();

                    // Call custom destroy
                    if (config.destroy) {
                        config.destroy.call(this);
                    }
                },

                /**
                 * Render the page structure.
                 */
                renderPage: function() {
                    if (config.renderPage) {
                        config.renderPage.call(this);
                    }
                },

                /**
                 * Load page data.
                 * @returns {Promise}
                 */
                loadData: async function() {
                    if (config.loadData) {
                        return config.loadData.call(this);
                    }
                },

                /**
                 * Refresh data (alias for loadData).
                 * @returns {Promise}
                 */
                refresh: async function() {
                    return this.loadData();
                },

                /**
                 * Subscribe to all configured SignalR channels.
                 * @private
                 */
                _subscribeToChannels: function() {
                    if (!Monolith.SignalR || this._channels.length === 0) {
                        // No SignalR or no channels - use polling
                        this._startPolling();
                        return;
                    }

                    // Create a single handler for all channels
                    this._signalRHandler = this._handleEvent.bind(this);

                    this._channels.forEach(channel => {
                        Monolith.SignalR.subscribe(channel, this._signalRHandler);
                    });
                },

                /**
                 * Unsubscribe from all SignalR channels.
                 * @private
                 */
                _unsubscribeFromChannels: function() {
                    if (!Monolith.SignalR || !this._signalRHandler) {
                        return;
                    }

                    this._channels.forEach(channel => {
                        Monolith.SignalR.unsubscribe(channel, this._signalRHandler);
                    });

                    this._signalRHandler = null;
                },

                /**
                 * Handle SignalR event and route to appropriate handler.
                 * @private
                 */
                _handleEvent: function(eventName, data) {
                    if (!this._isActive) {
                        return;
                    }

                    const handler = this._events[eventName];
                    if (handler) {
                        try {
                            handler.call(this, data);
                        } catch (error) {
                            console.error('[PageBase] Event handler error:', eventName, error);
                        }
                    }
                },

                /**
                 * Start polling fallback for data refresh.
                 * @private
                 */
                _startPolling: function() {
                    if (this._pollTimer || !config.loadData || !this._pollInterval) {
                        return;
                    }

                    this._pollTimer = setInterval(() => {
                        if (this._isActive) {
                            this.loadData();
                        }
                    }, this._pollInterval);
                },

                /**
                 * Stop polling fallback.
                 * @private
                 */
                _stopPolling: function() {
                    if (this._pollTimer) {
                        clearInterval(this._pollTimer);
                        this._pollTimer = null;
                    }
                },

                /**
                 * Check if page is currently active.
                 * @returns {boolean}
                 */
                isActive: function() {
                    return this._isActive;
                }
            };

            // Copy all custom methods from config to page
            Object.keys(config).forEach(key => {
                if (!['channels', 'events', 'pollInterval', 'init', 'destroy', 'loadData', 'renderPage'].includes(key)) {
                    if (typeof config[key] === 'function') {
                        page[key] = config[key].bind(page);
                    } else {
                        page[key] = config[key];
                    }
                }
            });

            return page;
        },

        /**
         * Create a page module and register it in Monolith.Pages namespace.
         * @param {string} name - Page name (e.g., 'Interfaces', 'Routing')
         * @param {Object} config - Page configuration (same as create())
         * @returns {Object} Page module instance
         */
        register: function(name, config) {
            const page = this.create(config);

            window.Monolith = window.Monolith || {};
            window.Monolith.Pages = window.Monolith.Pages || {};
            window.Monolith.Pages[name] = page;

            return page;
        }
    };

    // Export to global namespace
    window.Monolith = window.Monolith || {};
    window.Monolith.PageBase = PageBase;
})();
