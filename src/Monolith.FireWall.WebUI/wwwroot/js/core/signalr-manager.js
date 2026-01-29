/**
 * Monolith.SignalR - Centralized SignalR connection manager
 *
 * Provides unified real-time communication for all WebUI pages.
 * Features:
 * - Single WebSocket connection to /hubs/system-events
 * - Channel-based subscriptions (interfaces, gateways, services, system, routing, pending)
 * - Automatic reconnection with exponential backoff
 * - Graceful fallback to polling when disconnected
 * - Event routing to page handlers
 *
 * Usage:
 *   // Subscribe to interface status updates
 *   Monolith.SignalR.subscribe('interfaces', function(eventName, data) {
 *       if (eventName === 'InterfaceStatusChanged') {
 *           console.log('Interface changed:', data.interfaceName, data.status);
 *       }
 *   });
 *
 *   // Unsubscribe when leaving page
 *   Monolith.SignalR.unsubscribe('interfaces', myHandler);
 *
 *   // Check connection status
 *   if (Monolith.SignalR.isConnected()) { ... }
 */
(function() {
    'use strict';

    const HUB_URL = '/hubs/system-events';
    const RECONNECT_DELAYS = [0, 2000, 5000, 10000, 30000];

    let connection = null;
    let subscriptions = new Map(); // channel -> Set<handler>
    let connectionState = 'disconnected'; // disconnected, connecting, connected, reconnecting
    let pollingTimers = new Map(); // channel -> timer id
    let initPromise = null;

    // Event names for each channel
    const CHANNEL_EVENTS = {
        interfaces: ['InterfaceStatusChanged', 'InterfaceStatusBatch'],
        gateways: ['GatewayStatusChanged', 'GatewayStatusBatch'],
        services: ['ServiceStatusChanged', 'ServiceStatusBatch'],
        system: ['SystemMetricsUpdated'],
        routing: ['RoutingTableChanged'],
        pending: ['PendingCountChanged', 'ChangeAdded', 'ChangesApplied', 'ChangesDiscarded']
    };

    // Polling fallback endpoints for each channel
    const POLLING_ENDPOINTS = {
        interfaces: '/api/interfaces/status',
        gateways: '/api/routing/gateways/status',
        services: '/api/services/status',
        system: '/api/monitoring/metrics',
        pending: '/api/pending/count'
    };

    // Polling intervals (ms)
    const POLLING_INTERVALS = {
        interfaces: 5000,
        gateways: 15000,
        services: 30000,
        system: 10000,
        pending: 10000
    };

    const SignalRManager = {
        /**
         * Initialize the SignalR connection.
         * Safe to call multiple times - will reuse existing connection.
         * @returns {Promise} Resolves when connected (or immediately if SignalR unavailable)
         */
        init: function() {
            if (initPromise) {
                return initPromise;
            }

            if (typeof signalR === 'undefined') {
                console.warn('[SignalR] Client library not loaded, using polling fallback');
                connectionState = 'disconnected';
                return Promise.resolve();
            }

            initPromise = this._connect();
            return initPromise;
        },

        /**
         * Establish SignalR connection.
         * @private
         */
        _connect: async function() {
            if (connection) {
                return;
            }

            connectionState = 'connecting';
            console.log('[SignalR] Connecting to', HUB_URL);

            try {
                connection = new signalR.HubConnectionBuilder()
                    .withUrl(HUB_URL)
                    .withAutomaticReconnect(RECONNECT_DELAYS)
                    .configureLogging(signalR.LogLevel.Warning)
                    .build();

                // Register all event handlers
                this._registerEventHandlers();

                // Connection state handlers
                connection.onreconnecting((error) => {
                    connectionState = 'reconnecting';
                    console.log('[SignalR] Reconnecting...', error?.message || '');
                    this._startPollingFallback();
                });

                connection.onreconnected((connectionId) => {
                    connectionState = 'connected';
                    console.log('[SignalR] Reconnected:', connectionId);
                    this._stopPollingFallback();
                    this._resubscribeAll();
                });

                connection.onclose((error) => {
                    connectionState = 'disconnected';
                    console.log('[SignalR] Connection closed', error?.message || '');
                    this._startPollingFallback();
                });

                await connection.start();
                connectionState = 'connected';
                console.log('[SignalR] Connected');

                // Subscribe to any channels that were registered before connection
                await this._resubscribeAll();

            } catch (error) {
                console.error('[SignalR] Connection failed:', error);
                connectionState = 'disconnected';
                connection = null;
                initPromise = null;
                this._startPollingFallback();
            }
        },

        /**
         * Register handlers for all known events.
         * @private
         */
        _registerEventHandlers: function() {
            if (!connection) return;

            // Register handlers for each channel's events
            Object.entries(CHANNEL_EVENTS).forEach(([channel, events]) => {
                events.forEach(eventName => {
                    connection.on(eventName, (data) => {
                        this._dispatchEvent(channel, eventName, data);
                    });
                });
            });
        },

        /**
         * Dispatch an event to all handlers subscribed to a channel.
         * @private
         */
        _dispatchEvent: function(channel, eventName, data) {
            const handlers = subscriptions.get(channel);
            if (!handlers || handlers.size === 0) {
                return;
            }

            handlers.forEach(handler => {
                try {
                    handler(eventName, data);
                } catch (error) {
                    console.error(`[SignalR] Handler error for ${channel}:${eventName}:`, error);
                }
            });
        },

        /**
         * Subscribe to a channel to receive events.
         * @param {string} channel - Channel name (interfaces, gateways, services, system, routing, pending)
         * @param {function} handler - Callback function(eventName, data)
         */
        subscribe: function(channel, handler) {
            if (!channel || typeof handler !== 'function') {
                console.warn('[SignalR] Invalid subscribe call:', channel);
                return;
            }

            // Add to local subscriptions
            if (!subscriptions.has(channel)) {
                subscriptions.set(channel, new Set());
            }
            subscriptions.get(channel).add(handler);

            // Subscribe on server if connected
            if (connectionState === 'connected' && connection) {
                connection.invoke('Subscribe', channel).catch(err => {
                    console.error('[SignalR] Failed to subscribe to', channel, err);
                });
            }

            // Start polling fallback if not connected
            if (connectionState !== 'connected') {
                this._startChannelPolling(channel);
            }

            console.log('[SignalR] Subscribed to:', channel);
        },

        /**
         * Unsubscribe a handler from a channel.
         * @param {string} channel - Channel name
         * @param {function} handler - The handler to remove
         */
        unsubscribe: function(channel, handler) {
            if (!subscriptions.has(channel)) {
                return;
            }

            subscriptions.get(channel).delete(handler);

            // If no more handlers, unsubscribe from server
            if (subscriptions.get(channel).size === 0) {
                subscriptions.delete(channel);

                if (connectionState === 'connected' && connection) {
                    connection.invoke('Unsubscribe', channel).catch(err => {
                        console.error('[SignalR] Failed to unsubscribe from', channel, err);
                    });
                }

                // Stop polling for this channel
                this._stopChannelPolling(channel);
            }

            console.log('[SignalR] Unsubscribed from:', channel);
        },

        /**
         * Unsubscribe all handlers from a channel.
         * @param {string} channel - Channel name
         */
        unsubscribeAll: function(channel) {
            if (!subscriptions.has(channel)) {
                return;
            }

            subscriptions.delete(channel);

            if (connectionState === 'connected' && connection) {
                connection.invoke('Unsubscribe', channel).catch(err => {
                    console.error('[SignalR] Failed to unsubscribe from', channel, err);
                });
            }

            this._stopChannelPolling(channel);
            console.log('[SignalR] Unsubscribed all from:', channel);
        },

        /**
         * Check if connected to SignalR hub.
         * @returns {boolean}
         */
        isConnected: function() {
            return connectionState === 'connected';
        },

        /**
         * Get current connection state.
         * @returns {string} 'disconnected', 'connecting', 'connected', or 'reconnecting'
         */
        getConnectionState: function() {
            return connectionState;
        },

        /**
         * Resubscribe to all channels after reconnection.
         * @private
         */
        _resubscribeAll: async function() {
            if (!connection || connectionState !== 'connected') {
                return;
            }

            const channels = Array.from(subscriptions.keys());
            if (channels.length === 0) {
                return;
            }

            try {
                await connection.invoke('SubscribeMany', channels);
                console.log('[SignalR] Resubscribed to:', channels.join(', '));
            } catch (error) {
                console.error('[SignalR] Failed to resubscribe:', error);
            }
        },

        /**
         * Start polling fallback for all subscribed channels.
         * @private
         */
        _startPollingFallback: function() {
            subscriptions.forEach((_, channel) => {
                this._startChannelPolling(channel);
            });
        },

        /**
         * Stop all polling fallbacks.
         * @private
         */
        _stopPollingFallback: function() {
            pollingTimers.forEach((timerId, channel) => {
                clearInterval(timerId);
            });
            pollingTimers.clear();
        },

        /**
         * Start polling for a specific channel.
         * @private
         */
        _startChannelPolling: function(channel) {
            // Don't start if already polling or connected
            if (pollingTimers.has(channel) || connectionState === 'connected') {
                return;
            }

            const endpoint = POLLING_ENDPOINTS[channel];
            const interval = POLLING_INTERVALS[channel];

            if (!endpoint || !interval) {
                return;
            }

            console.log('[SignalR] Starting polling fallback for:', channel);

            // Poll immediately once
            this._pollChannel(channel, endpoint);

            // Then set up interval
            const timerId = setInterval(() => {
                this._pollChannel(channel, endpoint);
            }, interval);

            pollingTimers.set(channel, timerId);
        },

        /**
         * Stop polling for a specific channel.
         * @private
         */
        _stopChannelPolling: function(channel) {
            const timerId = pollingTimers.get(channel);
            if (timerId) {
                clearInterval(timerId);
                pollingTimers.delete(channel);
            }
        },

        /**
         * Poll a channel's endpoint and dispatch results.
         * @private
         */
        _pollChannel: async function(channel, endpoint) {
            try {
                const response = await fetch(endpoint);
                if (!response.ok) {
                    return;
                }

                const data = await response.json();

                // Map polling response to appropriate event
                switch (channel) {
                    case 'interfaces':
                        this._dispatchEvent(channel, 'InterfaceStatusBatch', data.data || data);
                        break;
                    case 'gateways':
                        this._dispatchEvent(channel, 'GatewayStatusBatch', data.data || data);
                        break;
                    case 'services':
                        this._dispatchEvent(channel, 'ServiceStatusBatch', data.data || data);
                        break;
                    case 'system':
                        this._dispatchEvent(channel, 'SystemMetricsUpdated', data.data || data);
                        break;
                    case 'pending':
                        this._dispatchEvent(channel, 'PendingCountChanged', data.data?.count ?? data.count ?? 0);
                        break;
                }
            } catch (error) {
                // Silent fail for polling - don't spam console
            }
        },

        /**
         * Manually trigger a reconnection attempt.
         */
        reconnect: async function() {
            if (connection) {
                try {
                    await connection.stop();
                } catch (e) {
                    // Ignore
                }
                connection = null;
            }
            initPromise = null;
            await this.init();
        }
    };

    // Export to global namespace
    window.Monolith = window.Monolith || {};
    window.Monolith.SignalR = SignalRManager;

    // Auto-initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => {
            SignalRManager.init();
        });
    } else {
        SignalRManager.init();
    }
})();
