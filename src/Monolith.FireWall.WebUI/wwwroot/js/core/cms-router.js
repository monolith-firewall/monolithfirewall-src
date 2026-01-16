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
            this.renderMenus();
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

        // Hover support for package submenus (dropend)
        $(document).off('mouseenter', '#packages-menu .dropend');
        $(document).on('mouseenter', '#packages-menu .dropend', function() {
            const $dropend = $(this);
            const $link = $dropend.find('a').first();
            const $submenu = $dropend.find('.dropdown-menu').first();

            if ($submenu.length && $link.length) {
                const offset = $link.offset();
                const width = $link.outerWidth();

                $submenu.css({
                    'display': 'block',
                    'position': 'fixed',
                    'top': offset.top + 'px',
                    'left': (offset.left + width) + 'px',
                    'z-index': '1050',
                    'opacity': '1',
                    'visibility': 'visible'
                }).addClass('show');
            }
        });

        $(document).off('mouseleave', '#packages-menu .dropend');
        $(document).on('mouseleave', '#packages-menu .dropend', function() {
            const $dropend = $(this);
            const $submenu = $dropend.find('.dropdown-menu').first();
            setTimeout(() => {
                if (!$dropend.is(':hover') && !$submenu.is(':hover')) {
                    $submenu.css({
                        'display': 'none',
                        'opacity': '0',
                        'visibility': 'hidden'
                    }).removeClass('show');
                }
            }, 200);
        });

        $(document).off('mouseenter', '#packages-menu .dropend .dropdown-menu');
        $(document).on('mouseenter', '#packages-menu .dropend .dropdown-menu', function() {
            $(this).css({
                'display': 'block',
                'opacity': '1',
                'visibility': 'visible'
            }).addClass('show');
        });

        $(document).off('mouseleave', '#packages-menu .dropend .dropdown-menu');
        $(document).on('mouseleave', '#packages-menu .dropend .dropdown-menu', function() {
            const $submenu = $(this);
            const $dropend = $submenu.closest('.dropend');
            setTimeout(() => {
                if (!$dropend.is(':hover') && !$submenu.is(':hover')) {
                    $submenu.css({
                        'display': 'none',
                        'opacity': '0',
                        'visibility': 'hidden'
                    }).removeClass('show');
                }
            }, 200);
        });
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

        moduleName = (route.meta && route.meta.module) ? route.meta.module : this.getModuleFromPath(route.path);
        if (moduleName) {
            this.initModuleByName(moduleName);
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

    renderMenus: function() {
        if (!this.manifest || !Array.isArray(this.manifest.menu)) {
            return;
        }

        const containerMap = {
            'system': '#menu-system',
            'interfaces': '#interfaces-menu',
            'firewall': '#menu-firewall',
            'status': '#menu-status',
            'packages': '#packages-menu'
        };

        this.manifest.menu.forEach(group => {
            if (!group || !group.label) {
                return;
            }
            const key = String(group.label).toLowerCase();
            const selector = containerMap[key];
            if (!selector) {
                return;
            }
            const container = $(selector);
            if (!container.length) {
                return;
            }

            const html = this.buildMenuHtml(group.children || [], key);
            let content = html || '<li><span class="dropdown-item-text text-muted small">No items</span></li>';
            if (!html && key === 'packages') {
                content = '<li><span class="dropdown-item-text text-muted small">No packages installed</span></li>';
            }
            container.html(content);

            if (key === 'interfaces') {
                container.append('<li><hr class="dropdown-divider"></li><li id="interfaces-list-placeholder"><span class="dropdown-item-text text-muted small">Loading interfaces...</span></li>');
            }
        });
    },

    buildMenuHtml: function(items, groupKey) {
        if (!Array.isArray(items) || items.length === 0) {
            return '';
        }

        return items.map(item => this.buildMenuItem(item, groupKey)).join('');
    },

    buildMenuItem: function(item, groupKey) {
        if (!item) {
            return '';
        }

        const hasChildren = Array.isArray(item.children) && item.children.length > 0;
        const label = item.label || item.Label || item.Label || 'Unnamed';
        const icon = item.icon || item.Icon || null;
        const iconClass = this.resolveIconClass(icon, groupKey);
        const path = this.getMenuPath(item);
        
        // Special styling for Status menu items
        const isStatusMenu = groupKey === 'status';
        const displayLabel = isStatusMenu 
            ? `<span class="d-inline-flex align-items-center gap-2 w-100"><i class="dropdown-icon ${iconClass}"></i><span class="flex-grow-1">${label}</span></span>`
            : `<span class="d-inline-flex align-items-center gap-2"><i class="dropdown-icon ${iconClass}"></i><span>${label}</span></span>`;

        // Handle dividers
        if (label.toLowerCase() === 'divider') {
            return `<li><hr class="dropdown-divider"></li>`;
        }

        if (hasChildren) {
            return `
                <li class="dropend">
                    <a class="dropdown-item d-flex align-items-center justify-content-between" href="javascript:void(0);" data-no-route="true">
                        ${displayLabel}
                        <i class="fa-solid fa-chevron-right"></i>
                    </a>
                    <ul class="dropdown-menu">
                        ${this.buildMenuHtml(item.children, groupKey)}
                    </ul>
                </li>
            `;
        }

        if (!path) {
            return `<li><span class="dropdown-item-text text-muted small">${label}</span></li>`;
        }

        const itemClass = isStatusMenu ? 'dropdown-item status-menu-item' : 'dropdown-item';
        return `<li><a class="${itemClass}" href="${path}" data-route="${path}">${displayLabel}</a></li>`;
    },

    getMenuPath: function(item) {
        if (!item) {
            return null;
        }

        const rawPath = item.Path || item.path;
        if (rawPath) {
            return this.normalizePath(rawPath);
        }

        const routeId = item.RouteId || item.routeId;
        if (routeId) {
            const route = this.routesById[routeId];
            if (route && route.path) {
                return this.normalizePath(route.path);
            }
        }

        return null;
    },

    resolveIconClass: function(icon, groupKey) {
        if (!icon) {
            const defaults = {
                system: 'fa-solid fa-gear',
                status: 'fa-solid fa-chart-line',
                interfaces: 'fa-solid fa-network-wired',
                firewall: 'fa-solid fa-shield-halved',
                status: 'fa-solid fa-circle-check',
                packages: 'fa-solid fa-box-open'
            };
            return defaults[groupKey] || 'fa-solid fa-circle-dot';
        }

        const value = String(icon).toLowerCase().trim();
        if (value.includes('fa-')) {
            return value.includes('fa-solid') || value.includes('fa-regular') || value.includes('fa-light') || value.includes('fa-thin') || value.includes('fa-duotone')
                ? value
                : `fa-solid ${value}`;
        }

        const map = {
            shield: 'fa-solid fa-shield-halved',
            firewall: 'fa-solid fa-fire-flame-curved',
            network: 'fa-solid fa-network-wired',
            router: 'fa-solid fa-route',
            package: 'fa-solid fa-box-open',
            module: 'fa-solid fa-puzzle-piece',
            settings: 'fa-solid fa-gear',
            system: 'fa-solid fa-gear',
            status: 'fa-solid fa-circle-check',
            logs: 'fa-solid fa-clipboard-list',
            user: 'fa-solid fa-user',
            users: 'fa-solid fa-users',
            bell: 'fa-solid fa-bell',
            home: 'fa-solid fa-house',
            info: 'fa-solid fa-circle-info'
        };

        return map[value] || 'fa-solid fa-circle-dot';
    },

    setActiveRoute: function(path) {
        $('.nav-link, .dropdown-item').removeClass('active');
        
        // Match exact or parent path for sub-pages
        $('.dropdown-item[data-route]').each(function() {
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
            if (!$active.length) {
                $active = $nav.find('[data-bs-toggle="tab"]').first();
            }
            if ($active.length) {
                const tab = bootstrap.Tab.getOrCreateInstance($active[0]);
                tab.show();
                // Manually trigger shown event if it was already active (bootstrap won't trigger it)
                $active.trigger('shown.bs.tab', {
                    target: $active[0],
                    relatedTarget: null
                });
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
