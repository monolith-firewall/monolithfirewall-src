/**
 * MonolithFireWall API Client
 */
var Monolith = window.Monolith || {};
window.Monolith = Monolith;

Monolith.API = {
    baseUrl: '/api',
    buildUrl: function(endpoint) {
        if (!endpoint) {
            return this.baseUrl;
        }

        if (endpoint.startsWith('http://') || endpoint.startsWith('https://')) {
            return endpoint;
        }

        let normalized = endpoint.startsWith('/') ? endpoint : `/${endpoint}`;

        if (normalized === this.baseUrl || normalized.startsWith(`${this.baseUrl}/`)) {
            return normalized;
        }

        if (normalized.startsWith('/api/')) {
            return `${this.baseUrl}${normalized.slice('/api'.length)}`;
        }

        return `${this.baseUrl}${normalized}`;
    },

    /**
     * GET request
     */
    get: async function(endpoint) {
        try {
            const response = await fetch(this.buildUrl(endpoint), {
                headers: {
                    'Content-Type': 'application/json',
                    'Cache-Control': 'no-cache',
                    'Pragma': 'no-cache',
                    'X-Requested-With': 'XMLHttpRequest'
                },
                cache: 'no-cache',
                credentials: 'same-origin'
            });
            
            if (!response.ok) {
                const errorData = await response.json().catch(() => ({}));
                throw new Error(errorData.error || `HTTP ${response.status}: ${response.statusText}`);
            }
            
            return await response.json();
        } catch (error) {
            console.error('API GET error:', error);
            throw error;
        }
    },

    /**
     * POST request
     */
    post: async function(endpoint, data) {
        try {
            const response = await fetch(this.buildUrl(endpoint), {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Cache-Control': 'no-cache',
                    'Pragma': 'no-cache',
                    'X-Requested-With': 'XMLHttpRequest'
                },

                body: JSON.stringify(data),
                cache: 'no-cache',
                credentials: 'same-origin'
            });
            
            if (!response.ok) {
                const errorData = await response.json().catch(() => ({}));
                throw new Error(errorData.error || `HTTP ${response.status}: ${response.statusText}`);
            }
            
            return await response.json();
        } catch (error) {
            console.error('API POST error:', error);
            throw error;
        }
    },

    /**
     * PUT request
     */
    put: async function(endpoint, data) {
        try {
            const response = await fetch(this.buildUrl(endpoint), {
                method: 'PUT',
                headers: {
                    'Content-Type': 'application/json',
                    'Cache-Control': 'no-cache',
                    'Pragma': 'no-cache',
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: JSON.stringify(data),
                cache: 'no-cache',
                credentials: 'same-origin'
            });
            
            if (!response.ok) {
                const errorData = await response.json().catch(() => ({}));
                throw new Error(errorData.error || `HTTP ${response.status}: ${response.statusText}`);
            }
            
            return await response.json();
        } catch (error) {
            console.error('API PUT error:', error);
            throw error;
        }
    },

    /**
     * DELETE request
     */
    delete: async function(endpoint) {
        try {
            const response = await fetch(this.buildUrl(endpoint), {
                method: 'DELETE',
                headers: {
                    'Content-Type': 'application/json',
                    'Cache-Control': 'no-cache',
                    'Pragma': 'no-cache',
                    'X-Requested-With': 'XMLHttpRequest'
                },
                cache: 'no-cache',
                credentials: 'same-origin'
            });
            
            if (!response.ok) {
                const errorData = await response.json().catch(() => ({}));
                throw new Error(errorData.error || `HTTP ${response.status}: ${response.statusText}`);
            }
            
            return await response.json();
        } catch (error) {
            console.error('API DELETE error:', error);
            throw error;
        }
    }
};
