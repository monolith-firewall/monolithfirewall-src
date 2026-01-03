/**
 * MonolithFireWall Module Loader
 * Manages loading and caching of module scripts and stylesheets
 */
var Monolith = window.Monolith || {};
window.Monolith = Monolith;

Monolith.ModuleLoader = {
    loadedScripts: new Set(),
    loadedStyles: new Set(),
    scriptPromises: new Map(), // Track loading promises to avoid duplicates

    /**
     * Load a module script (loads once, caches)
     */
    loadScript: function(src, moduleId) {
        // Return existing promise if already loading
        if (this.scriptPromises.has(moduleId)) {
            return this.scriptPromises.get(moduleId);
        }

        // Return resolved promise if already loaded
        if (this.loadedScripts.has(moduleId)) {
            return Promise.resolve();
        }

        // Create loading promise
        const promise = new Promise((resolve, reject) => {
            // Check if script already exists in DOM
            const existingScript = document.querySelector(`script[data-module-js="${moduleId}"]`);
            if (existingScript) {
                this.loadedScripts.add(moduleId);
                resolve();
                return;
            }

            // Create and load script
            const script = document.createElement('script');
            script.src = src;
            script.setAttribute('data-module-js', moduleId);
            script.async = true;

            script.onload = () => {
                this.loadedScripts.add(moduleId);
                this.scriptPromises.delete(moduleId);
                console.log(`Module script loaded: ${moduleId}`);
                resolve();
            };

            script.onerror = () => {
                this.scriptPromises.delete(moduleId);
                console.error(`Failed to load module script: ${moduleId}`);
                reject(new Error(`Failed to load script: ${src}`));
            };

            document.head.appendChild(script);
        });

        this.scriptPromises.set(moduleId, promise);
        return promise;
    },

    /**
     * Load a module stylesheet (loads once, caches)
     */
    loadStyle: function(href, moduleId) {
        // Skip if already loaded
        if (this.loadedStyles.has(moduleId)) {
            return;
        }

        // Check if link already exists in DOM
        const existingLink = document.querySelector(`link[data-module-css="${moduleId}"]`);
        if (existingLink) {
            this.loadedStyles.add(moduleId);
            return;
        }

        // Create and load stylesheet
        const link = document.createElement('link');
        link.rel = 'stylesheet';
        link.href = href;
        link.setAttribute('data-module-css', moduleId);

        link.onload = () => {
            this.loadedStyles.add(moduleId);
            console.log(`Module stylesheet loaded: ${moduleId}`);
        };

        link.onerror = () => {
            console.error(`Failed to load module stylesheet: ${moduleId}`);
        };

        document.head.appendChild(link);
        this.loadedStyles.add(moduleId);
    },

    /**
     * Check if a module script is loaded
     */
    isScriptLoaded: function(moduleId) {
        return this.loadedScripts.has(moduleId);
    },

    /**
     * Check if a module stylesheet is loaded
     */
    isStyleLoaded: function(moduleId) {
        return this.loadedStyles.has(moduleId);
    },

    /**
     * Clear all loaded modules (for testing/debugging)
     */
    clear: function() {
        this.loadedScripts.clear();
        this.loadedStyles.clear();
        this.scriptPromises.clear();
    }
};
