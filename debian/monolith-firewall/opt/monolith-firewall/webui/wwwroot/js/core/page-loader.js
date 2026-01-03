/**
 * MonolithFireWall Page Loader
 * Loads page assets on demand based on the active route.
 */
var Monolith = window.Monolith || {};
window.Monolith = Monolith;

Monolith.PageLoader = {
    internalRoutes: {
        '/dashboard': { module: 'dashboard', asset: 'dashboard', css: ['dashboard'] },
        '/users': { module: 'users', asset: 'users' },
        '/groups': { module: 'groups', asset: 'groups' },
        '/permissions': { module: 'permissions', asset: 'permissions' },
        '/profile': { module: 'profile', asset: 'profile' },
        '/system/packages': { module: 'packages', asset: 'packages', css: ['packages'] },
        '/system/modules': { module: 'modules', asset: 'modules', css: ['modules'] },
        '/system/updates': { module: 'updates', asset: 'updates' },
        '/system/settings': { module: 'settings', asset: 'settings' },
        '/system/advanced': { module: 'advanced-settings', asset: 'advanced-settings', css: ['advanced-settings'] },
        '/system/routing': { module: 'routing', asset: 'routing', css: ['routing'] },
        '/system/logs': { module: 'system-logs', asset: 'system-logs', css: ['system-logs'] }
    },

    load: async function(pageDef) {
        if (!pageDef || !pageDef.route || pageDef.route === '/login') {
            return;
        }

        if (!Monolith.ModuleLoader) {
            console.warn('ModuleLoader not available for PageLoader');
            return;
        }

        if (pageDef.isPackagePage || pageDef.route.startsWith('/p/')) {
            await this.loadPackagePage(pageDef);
            return;
        }

        if (pageDef.route.startsWith('/firewall/')) {
            await this.loadFirewallPage(pageDef);
            return;
        }

        await this.loadInternalPage(pageDef);
    },

    loadInternalPage: async function(pageDef) {
        const info = this.resolveInternalRoute(pageDef.route);
        if (!info) {
            return;
        }

        await this.loadScript('pages', info.module, info.asset, `page-${info.module}-${info.asset}`);
        this.loadStyles('pages', info.module, info.css || [], `page-${info.module}`);
        this.initModuleByName(info.asset);
    },

    loadFirewallPage: async function(pageDef) {
        const moduleName = pageDef.route.split('/').pop();
        if (!moduleName) {
            return;
        }

        await this.loadScript('pages', 'firewall', moduleName, `page-firewall-${moduleName}`);
        this.loadStyles('pages', 'firewall', ['firewall'], 'page-firewall');
        this.initModuleByName(moduleName);
    },

    loadPackagePage: async function(pageDef) {
        const info = this.parsePackageRoute(pageDef.route);
        if (!info) {
            return;
        }

        const primaryAsset = info.moduleId;
        const moduleId = `package-${info.packageId}-${info.moduleId}-${primaryAsset}`;

        await this.tryLoadScript('package', info.packageId, primaryAsset, moduleId, info.moduleId);
        this.loadStyles('package', info.packageId, [primaryAsset], `package-${info.packageId}-${info.moduleId}`, info.moduleId);

        if (info.pageId && info.pageId !== info.moduleId) {
            const pageModuleId = `package-${info.packageId}-${info.moduleId}-${info.pageId}`;
            await this.tryLoadScript('package', info.packageId, info.pageId, pageModuleId, info.moduleId);
            this.loadStyles('package', info.packageId, [info.pageId], `package-${info.packageId}-${info.moduleId}-page`, info.moduleId);
        }

        this.initModuleByName(info.moduleId);
        if (info.pageId && info.pageId !== info.moduleId) {
            this.initModuleByName(info.pageId);
        }
    },

    resolveInternalRoute: function(route) {
        if (this.internalRoutes[route]) {
            return this.internalRoutes[route];
        }

        if (route.startsWith('/interfaces')) {
            return { module: 'interfaces', asset: 'interfaces' };
        }

        if (route.startsWith('/status/')) {
            return { module: 'status', asset: 'status' };
        }

        return null;
    },

    parsePackageRoute: function(route) {
        const parts = route.split('/').filter(Boolean);
        if (parts.length < 3 || parts[0] !== 'p') {
            return null;
        }

        return {
            packageId: parts[1],
            moduleId: parts[2],
            pageId: parts.length > 3 ? parts[3] : parts[2]
        };
    },

    loadScript: async function(kind, module, assetName, moduleId, packageModule) {
        const fileName = `${assetName}.js`;
        const url = this.buildAssetUrl(kind, module, fileName, packageModule);
        await Monolith.ModuleLoader.loadScript(url, moduleId);
    },

    tryLoadScript: async function(kind, module, assetName, moduleId, packageModule) {
        try {
            await this.loadScript(kind, module, assetName, moduleId, packageModule);
        } catch (error) {
            console.warn(`Optional script not loaded: ${assetName}`, error);
        }
    },

    loadStyles: function(kind, module, assets, idPrefix, packageModule) {
        if (!assets || assets.length === 0) {
            return;
        }

        assets.forEach(assetName => {
            const fileName = `${assetName}.css`;
            const url = this.buildAssetUrl(kind, module, fileName, packageModule);
            Monolith.ModuleLoader.loadStyle(url, `${idPrefix}-${assetName}`);
        });
    },

    buildAssetUrl: function(kind, module, fileName, packageModule) {
        if (kind === 'package') {
            const packageId = encodeURIComponent(module);
            const moduleId = encodeURIComponent(packageModule || '');
            return `/assets/package/${packageId}/${moduleId}/${fileName}`;
        }

        return `/assets/pages/${encodeURIComponent(module)}/${fileName}`;
    },

    initModuleByName: function(moduleName) {
        const pascal = this.toPascalCase(moduleName);
        const targets = [
            `Monolith.Pages.${pascal}`,
            `Monolith.Pages.${moduleName}`,
            pascal,
            moduleName
        ];

        for (const target of targets) {
            if (this.tryInit(target)) {
                return true;
            }
        }

        return false;
    },

    tryInit: function(path) {
        const obj = this.getObjectByPath(path);
        if (obj && typeof obj.init === 'function') {
            obj.init();
            return true;
        }

        return false;
    },

    getObjectByPath: function(path) {
        if (!path) {
            return null;
        }

        return path.split('.').reduce((obj, key) => {
            if (!obj || obj[key] === undefined) {
                return null;
            }
            return obj[key];
        }, window);
    },

    toPascalCase: function(value) {
        return (value || '')
            .split('-')
            .map(part => part ? part.charAt(0).toUpperCase() + part.slice(1) : '')
            .join('');
    }
};
