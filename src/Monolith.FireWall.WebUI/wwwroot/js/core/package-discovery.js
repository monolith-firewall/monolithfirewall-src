/**
 * Package Discovery Utility
 * Provides client-side utilities for discovering packages, modules, and permissions dynamically.
 */
Monolith.Packages = (function() {
    'use strict';

    let _packagesCache = null;
    let _modulesCache = null;
    let _cacheTime = 0;
    const CACHE_TIMEOUT = 5 * 60 * 1000; // 5 minutes

    /**
     * Get all installed packages.
     * @returns {Promise<Array>} Array of package objects
     */
    async function getInstalledPackages() {
        try {
            const response = await Monolith.API.get('/api/setup/packages');
            const data = response.data || response.Data || response;
            return data.packages || data.Packages || [];
        } catch (err) {
            console.error('Failed to get installed packages:', err);
            return [];
        }
    }

    /**
     * Get all modules from all installed packages.
     * @returns {Promise<Array>} Array of module objects
     */
    async function getAllModules() {
        const now = Date.now();
        if (_modulesCache && (now - _cacheTime) < CACHE_TIMEOUT) {
            return _modulesCache;
        }

        try {
            const request = {
                action: 'get-modules'
            };
            const response = await Monolith.API.post('/api/core', request);
            const data = response.data || response.Data || response;
            const modules = data.data || data.Data || [];
            
            _modulesCache = modules;
            _cacheTime = now;
            return modules;
        } catch (err) {
            console.error('Failed to get modules:', err);
            return _modulesCache || [];
        }
    }

    /**
     * Find the package that provides a specific module.
     * @param {string} moduleId - The module ID to find (e.g., "dhcp", "dns", "interfaces")
     * @returns {Promise<string|null>} Package ID or null if not found
     */
    async function findPackageByModule(moduleId) {
        const modules = await getAllModules();
        const module = modules.find(m => 
            (m.id || m.Id || '').toLowerCase() === moduleId.toLowerCase() &&
            (m.enabled !== false && m.Enabled !== false)
        );
        return module ? (module.packageId || module.PackageId || null) : null;
    }

    /**
     * Check if a package is installed.
     * @param {string} packageId - The package ID to check
     * @returns {Promise<boolean>} True if installed
     */
    async function isPackageInstalled(packageId) {
        const packages = await getInstalledPackages();
        return packages.some(p => 
            (p.packageId || p.PackageId || p.id || p.Id || '').toLowerCase() === packageId.toLowerCase()
        );
    }

    /**
     * Get all permissions from all installed packages.
     * @returns {Promise<Array>} Array of permission objects
     */
    async function getAllPermissions() {
        try {
            const response = await Monolith.API.get('/api/permissions');
            return response.permissions || response.data || [];
        } catch (err) {
            console.error('Failed to get permissions:', err);
            return [];
        }
    }

    /**
     * Get permissions grouped by category.
     * @returns {Promise<Object>} Object with categories as keys and permission arrays as values
     */
    async function getPermissionsByCategory() {
        try {
            const response = await Monolith.API.get('/api/permissions/categories');
            return response.categories || response.data || {};
        } catch (err) {
            console.error('Failed to get permissions by category:', err);
            return {};
        }
    }

    /**
     * Get all widgets from all installed packages.
     * @returns {Promise<Array>} Array of widget objects
     */
    async function getAllWidgets() {
        try {
            const response = await Monolith.API.get('/api/widgets');
            return response.widgets || response.data || [];
        } catch (err) {
            console.error('Failed to get widgets:', err);
            return [];
        }
    }

    /**
     * Clear the module cache (useful after package installation/uninstallation).
     */
    function clearCache() {
        _modulesCache = null;
        _packagesCache = null;
        _cacheTime = 0;
    }

    return {
        getInstalledPackages,
        getAllModules,
        findPackageByModule,
        isPackageInstalled,
        getAllPermissions,
        getPermissionsByCategory,
        getAllWidgets,
        clearCache
    };
})();
