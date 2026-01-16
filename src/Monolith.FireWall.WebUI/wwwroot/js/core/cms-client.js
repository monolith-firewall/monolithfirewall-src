/**
 * MonolithFireWall CMS Client
 * Loads the UI manifest and page content dynamically.
 */
var Monolith = window.Monolith || {};
window.Monolith = Monolith;

Monolith.Cms = {
    manifest: null,
    loadingPromise: null,

    loadManifest: async function() {
        if (this.manifest) {
            return this.manifest;
        }

        if (this.loadingPromise) {
            return this.loadingPromise;
        }

        this.loadingPromise = (async () => {
            const response = await Monolith.API.get('/cms/manifest');
            if (!response || !(response.success || response.Success) || !response.data) {
                throw new Error('Failed to load CMS manifest');
            }
            this.manifest = response.data;
            return this.manifest;
        })();

        try {
            return await this.loadingPromise;
        } finally {
            this.loadingPromise = null;
        }
    },

    getPage: async function(route) {
        const normalized = (route || '/').trim();
        const cleaned = normalized.startsWith('/') ? normalized.slice(1) : normalized;
        const segments = cleaned
            .split('/')
            .filter(Boolean)
            .map(segment => encodeURIComponent(segment));
        const endpoint = segments.length > 0
            ? `/cms/page/${segments.join('/')}`
            : '/cms/page';

        try {
            const response = await Monolith.API.get(endpoint);
            if (!(response && (response.success || response.Success))) {
                throw new Error(response && (response.error || response.Error) ? (response.error || response.Error) : 'Failed to load page');
            }
            return response;
        } catch (error) {
            const queryRoute = normalized.startsWith('/') ? normalized : `/${normalized}`;
            const fallbackResponse = await Monolith.API.get(`/cms/page?route=${encodeURIComponent(queryRoute)}`);
            if (!(fallbackResponse && (fallbackResponse.success || fallbackResponse.Success))) {
                const message = fallbackResponse && (fallbackResponse.error || fallbackResponse.Error) ? (fallbackResponse.error || fallbackResponse.Error) : 'Failed to load page';
                throw new Error(message);
            }
            return fallbackResponse;
        }
    }
};
