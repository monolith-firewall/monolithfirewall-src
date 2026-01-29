/**
 * MonolithFireWall CMS Router
 * History API routing + CMS content loader.
 */
var Monolith = window.Monolith || {};
window.Monolith = Monolith;

Monolith.CmsRouter = {
    manifest: null,
    routesByPath: {},
    routesById: {},
    initialized: false,
    activePackageAssets: null,
    activeModulePath: null,
    loaderSelector: '#app-loader',
    loaderTextSelector: '.loader-text',

    init: async function() {
        console.log('CmsRouter.init() called');
        if (this.initialized) {
            return;
        }

        this.toggleLoader(true, 'Loading application...');
        try {
            this.manifest = await Monolith.Cms.loadManifest();
            this.indexRoutes();
            // Menu rendering moved to menu.js
            this.attachEvents();
            this.initialized = true;

            const initialPath = window.location.pathname || '/';
            await this.navigate(initialPath, { replace: true });
        } catch (error) {
            console.error('Failed to initialize CMS router:', error);
            this.renderError('/', error.message || 'Failed to initialize UI');
        } finally {
            this.toggleLoader(false);
        }
    },

    indexRoutes: function() {
        this.routesByPath = {};
        this.routesById = {};
        const routes = (this.manifest && this.manifest.routes) ? this.manifest.routes : [];
        routes.forEach(route => {
            if (route && route.path) {
                const key = this.normalizePath(route.path);
                this.routesByPath[key] = route;
            }
            if (route && route.id) {
                this.routesById[route.id] = route;
            }
        });
    },

    normalizePath: function(path) {
        if (!path) {
            return '/';
        }
        if (!path.startsWith('/')) {
            path = '/' + path;
        }
        if (path.length > 1 && path.endsWith('/')) {
            return path.slice(0, -1);
        }
        return path;
    },

    getDefaultPath: function() {
        if (!this.manifest || !this.manifest.defaultRouteId) {
            return '/dashboard';
        }
        const route = this.routesById[this.manifest.defaultRouteId];
        return route && route.path ? route.path : '/dashboard';
    },

    attachEvents: function() {
        window.addEventListener('popstate', (event) => {
            const path = window.location.pathname || '/';
            // Don't modify history during popstate - browser already changed it
            this.navigate(path, { skipHistory: true });
        });

        $(document).on('click', 'a[data-route]', (e) => {
            const route = $(e.currentTarget).data('route');
            if (!route) {
                return;
            }
            e.preventDefault();
            this.push(route);
        });

        $(document).on('click', 'a[href^="/"]', (e) => {
            const link = e.currentTarget;
            const href = link.getAttribute('href');
            if (!href || link.hasAttribute('data-no-route') || link.hasAttribute('data-route')) {
                return;
            }
            if (href.startsWith('/api') || href.startsWith('/assets') || href.startsWith('/_content') || href.startsWith('/css') || href.startsWith('/js')) {
                return;
            }
            if (link.getAttribute('target')) {
                return;
            }
            if (href === '/login' || href.startsWith('/setup')) {
                return;
            }
            e.preventDefault();
            this.push(href);
        });

        // Package menu hover handlers moved to menu.js
    },

    push: async function(path) {
        const normalized = this.normalizePath(path);
        window.history.pushState({ path: normalized }, '', normalized);
        // Skip history modification since we just did pushState
        await this.navigate(normalized, { skipHistory: true });
    },

    navigate: async function(path, options) {
        const normalized = this.normalizePath(path || '/');
        const targetPath = normalized === '/' ? this.getDefaultPath() : normalized;
        const route = this.resolveRoute(targetPath);

        if (!route) {
            this.renderNotFound(targetPath);
            return;
        }

        if (route.kind === 'login') {
            window.location.href = '/login';
            return;
        }

        if (route.requiresAuth && !Monolith.Auth.currentUser) {
            window.location.href = '/login';
            return;
        }

        const $page = $('#page-content');
        
        // Show in-content loader instead of full-page overlay
        this.renderContentLoader($page);

        try {
            const response = await Monolith.Cms.getPage(route.path);
            const html = response.html || response.Html || '';
            const assets = response.assets || response.Assets || {};
            const css = assets.css || assets.Css || [];
            const js = assets.js || assets.Js || [];
            const isPackage = route.kind && String(route.kind).toLowerCase() === 'package';

            // Cleanup the current module before replacing content
            this.cleanupActiveModule();

            $page.empty().html(html);
            if (this.activePackageAssets) {
                this.clearPackageAssets();
            }

            const loadedAssets = await this.loadAssets(css, js, { forceScripts: isPackage });
            if (isPackage) {
                this.activePackageAssets = loadedAssets;
            }
            this.initModules(route);
            
            // Small delay to ensure DOM is ready for tab activation
            setTimeout(() => {
                this.activateTabs($page);
                this.setActiveRoute(route.path);
            }, 50);

            // Update browser URL if needed (unless skipHistory is set)
            if (!options || !options.skipHistory) {
                if (window.location.pathname !== route.path) {
                    window.history.replaceState({ path: route.path }, '', route.path);
                }
            }
        } catch (error) {
            console.error('Failed to load route:', error);
            this.renderError(route.path, error.message || 'Failed to load page');
        }
    },

    renderContentLoader: function($container) {
        $container.html(`
            <div class="d-flex flex-column align-items-center justify-content-center p-5 mt-5">
                <div class="spinner-border text-primary" style="width: 3rem; height: 3rem;" role="status">
                    <span class="visually-hidden">Loading...</span>
                </div>
                <div class="mt-3 text-muted fw-medium">Loading page content...</div>
            </div>
        `);
    },

    resolveRoute: function(path) {
        const normalized = this.normalizePath(path);
        if (this.routesByPath[normalized]) {
            return this.routesByPath[normalized];
        }

        const routes = this.manifest && Array.isArray(this.manifest.routes) ? this.manifest.routes : [];
        const pathParts = normalized.split('/').filter(Boolean);

        for (const candidate of routes) {
            if (!candidate || !candidate.path || !candidate.path.includes('{')) {
                continue;
            }

            const candidateParts = candidate.path.split('/').filter(Boolean);
            if (candidateParts.length !== pathParts.length) {
                continue;
            }

            let matched = true;
            for (let i = 0; i < candidateParts.length; i++) {
                const part = candidateParts[i];
                if (part.startsWith('{') && part.endsWith('}')) {
                    continue;
                }
                if (part.toLowerCase() !== pathParts[i].toLowerCase()) {
                    matched = false;
                    break;
                }
            }

            if (matched) {
                return candidate;
            }
        }

        return null;
    },

    loadAssets: async function(cssList, jsList, options) {
        const forceScripts = options && options.forceScripts;
        const loaded = { css: [], js: [] };

        if (Array.isArray(cssList)) {
            cssList.forEach(url => {
                if (!url) return;
                const id = this.assetId('css', url);
                loaded.css.push(id);
                Monolith.ModuleLoader.loadStyle(url, id);
            });
        }

        if (Array.isArray(jsList)) {
            for (const url of jsList) {
                if (!url) continue;
                const id = this.assetId('js', url);
                loaded.js.push(id);
                await Monolith.ModuleLoader.loadScript(url, id, { force: !!forceScripts });
            }
        }

        return loaded;
    },

    assetId: function(prefix, url) {
        return `${prefix}-${String(url).replace(/[^a-z0-9]/gi, '-')}`;
    },

    initModules: function(route) {
        if (!route) {
            return;
        }

        let moduleName = null;
        if (route.kind === 'package' && route.meta) {
            moduleName = route.meta.moduleId || route.meta.module || null;
            const pageId = route.meta.pageId || null;
            if (moduleName) {
                this.initModuleByName(moduleName);
            }
            if (pageId && pageId !== moduleName) {
                this.initModuleByName(pageId);
            }
            return;
        }

        // Try both camelCase and PascalCase for meta (JSON serialization might vary)
        const meta = route.meta || route.Meta || {};
        moduleName = meta.module || meta.Module || this.getModuleFromPath(route.path);
        if (moduleName) {
            console.log(`Initializing module for route ${route.path}: ${moduleName}`);
            this.initModuleByName(moduleName);
        } else {
            console.warn(`No module name found for route: ${route.path}`);
        }
    },

    initModuleByName: function(moduleName, retryCount = 0) {
        const pascal = this.toPascalCase(moduleName);
        const targets = [
            `Monolith.Pages.${pascal}`,
            `Monolith.Pages.${moduleName}`,
            pascal,
            moduleName
        ];

        console.log(`Searching for module: ${moduleName} (Targets: ${targets.join(', ')})`);

        for (const target of targets) {
            const obj = this.getObjectByPath(target);
            if (obj) {
                console.log(`Found module object at path: ${target}`, obj);
            }
            if (this.initOrRenderModule(target)) {
                console.log(`Successfully initialized module: ${target}`);
                return true;
            }
        }

        // If not found and we haven't retried too much, try again after a short delay
        if (retryCount < 5) {
            console.warn(`Module ${moduleName} not found yet, retrying (${retryCount + 1}/5)...`);
            setTimeout(() => {
                this.initModuleByName(moduleName, retryCount + 1);
            }, 100 * (retryCount + 1));
            return false;
        }

        console.error(`Module ${moduleName} could not be found after ${retryCount} retries.`);
        return false;
    },

    initOrRenderModule: function(path) {
        const obj = this.getObjectByPath(path);
        if (obj) {
            if (typeof obj.renderPage === 'function') {
                if (!obj.isInitialized && typeof obj.init === 'function') {
                    console.log(`Initializing module: ${path}`);
                    obj.init();
                    obj.isInitialized = true;
                }
                console.log(`Rendering module page: ${path}`);
                obj.renderPage();
                this.activeModulePath = path; // Track as active module
                return true;
            } else if (typeof obj.init === 'function') {
                // Fallback for modules that only have init()
                console.warn(`Module ${path} is missing renderPage(), falling back to init().`);
                obj.init();
                obj.isInitialized = true;
                this.activeModulePath = path;
                return true;
            }
        }
        return false;
    },

    cleanupActiveModule: function() {
        if (!this.activeModulePath) {
            return;
        }

        try {
            const obj = this.getObjectByPath(this.activeModulePath);
            if (obj && typeof obj.destroy === 'function') {
                console.log(`Cleaning up module: ${this.activeModulePath}`);
                obj.destroy();
            }
        } catch (error) {
            console.error(`Error during module cleanup (${this.activeModulePath}):`, error);
        } finally {
            this.activeModulePath = null;
        }
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
    },

    getModuleFromPath: function(path) {
        if (!path) {
            return null;
        }
        const parts = path.split('/').filter(Boolean);
        return parts.length ? parts[parts.length - 1] : null;
    },

    // Menu rendering functions moved to menu.js

    setActiveRoute: function(path) {
        // Only remove active from top navbar navigation, not from tabbed page navigation
        $('.top-navbar .nav-link, .top-navbar .dropdown-item').removeClass('active');
        
        // Match exact or parent path for sub-pages
        $('.top-navbar .dropdown-item[data-route]').each(function() {
            const route = $(this).data('route');
            if (path === route || (path.startsWith(route) && route !== '/')) {
                $(this).addClass('active');
                // Also activate parent dropdown if in a group
                $(this).closest('.dropdown').find('.nav-link').addClass('active');
            }
        });
    },

    activateTabs: function($container) {
        const $scope = $container && $container.length ? $container : $(document);
        $scope.find('.nav-tabs').each(function() {
            const $nav = $(this);
            let $active = $nav.find('[data-bs-toggle="tab"].active').first();
            const wasAlreadyActive = $active.length > 0;
            if (!$active.length) {
                $active = $nav.find('[data-bs-toggle="tab"]').first();
            }
            if ($active.length) {
                const tab = bootstrap.Tab.getOrCreateInstance($active[0]);
                tab.show();
                // Only manually trigger shown event if tab was already active
                // (bootstrap won't trigger shown.bs.tab if tab is already showing)
                if (wasAlreadyActive) {
                    $active.trigger('shown.bs.tab', {
                        target: $active[0],
                        relatedTarget: null
                    });
                }
            }
        });
    },

    renderNotFound: function(path) {
        $('#page-content').html(`
            <div class="container-fluid content-container p-4">
                <div class="alert alert-warning">
                    <h4>Page not found</h4>
                    <p>The route <code>${path}</code> is not defined.</p>
                </div>
            </div>
        `);
    },

    renderError: function(path, message) {
        $('#page-content').html(`
            <div class="container-fluid content-container p-4">
                <div class="alert alert-danger">
                    <h4>Page Load Error</h4>
                    <p>The page at <code>${path}</code> could not be loaded.</p>
                    <p class="mb-0"><strong>Error:</strong> ${message}</p>
                </div>
            </div>
        `);
    },

    toggleLoader: function(show, message) {
        const $loader = $(this.loaderSelector);
        if (!$loader.length) {
            return;
        }

        if (message) {
            $loader.find(this.loaderTextSelector).text(message);
        }

        if (show) {
            $loader.removeClass('d-none');
        } else {
            $loader.addClass('d-none');
        }
    },

    clearPackageAssets: function() {
        if (!this.activePackageAssets) {
            return;
        }

        if (Array.isArray(this.activePackageAssets.js)) {
            this.activePackageAssets.js.forEach(id => Monolith.ModuleLoader.unloadScript(id));
        }

        if (Array.isArray(this.activePackageAssets.css)) {
            this.activePackageAssets.css.forEach(id => Monolith.ModuleLoader.unloadStyle(id));
        }

        this.activePackageAssets = null;
    }
};
