/**
 * MonolithFireWall Core API Client
 * Wrapper for communicating with the Core service via /api/core
 */
var Monolith = window.Monolith || {};
window.Monolith = Monolith;

Monolith.Core = {
    /**
     * Call a Core API action
     * @param {string} action - The action name (e.g., 'gateway.groups.list')
     * @param {object} payload - Optional payload data
     * @returns {Promise<{success: boolean, data: any, error: string|null}>}
     */
    call: async function(action, payload) {
        try {
            var requestBody = { action: action };

            // Add payload fields to the request if provided
            if (payload && typeof payload === 'object') {
                // Check if payload should be nested or merged
                if (Object.keys(payload).length > 0) {
                    requestBody.payload = payload;
                }
            }

            var response = await fetch('/api/core', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: JSON.stringify(requestBody),
                credentials: 'same-origin'
            });

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }

            var result = await response.json();

            // Normalize response to lowercase keys for JS consistency
            // Core returns PascalCase (Success, Data, Error)
            return {
                success: result.success || result.Success || false,
                data: result.data || result.Data || null,
                error: result.error || result.Error || null
            };
        } catch (error) {
            console.error('Core API error:', error);
            return {
                success: false,
                data: null,
                error: error.message || 'Unknown error'
            };
        }
    },

    /**
     * Call a Core API action with GET method
     * @param {string} action - The action name
     * @returns {Promise<{success: boolean, data: any, error: string|null}>}
     */
    get: async function(action) {
        try {
            var response = await fetch(`/api/core?action=${encodeURIComponent(action)}`, {
                method: 'GET',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Requested-With': 'XMLHttpRequest'
                },
                credentials: 'same-origin'
            });

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }

            var result = await response.json();

            // Normalize response
            return {
                success: result.success || result.Success || false,
                data: result.data || result.Data || null,
                error: result.error || result.Error || null
            };
        } catch (error) {
            console.error('Core API error:', error);
            return {
                success: false,
                data: null,
                error: error.message || 'Unknown error'
            };
        }
    }
};
