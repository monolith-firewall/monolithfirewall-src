/**
 * MonolithFireWall SPA Router
 * Hash-based routing for single-page application
 */
var Monolith = window.Monolith || {};
window.Monolith = Monolith;

Monolith.Router = {
    routes: {},
    currentRoute: null,
    menuItems: [],

    /**
     * Initialize router
     */
    init: function() {
        // Add built-in routes immediately (before loading from Core)
        this.routes['/dashboard'] = { route: '/dashboard', title: 'Dashboard', package: 'system', module: 'dashboard' };
        this.routes['/users'] = { route: '/users', title: 'User Manager', package: 'system', module: 'users' };
        this.routes['/groups'] = { route: '/groups', title: 'User Groups', package: 'system', module: 'groups' };
        this.routes['/permissions'] = { route: '/permissions', title: 'Permissions', package: 'system', module: 'permissions' };
        this.routes['/login'] = { route: '/login', title: 'Login' };
        this.routes['/about'] = { route: '/about', title: 'About Monolith Firewall', package: 'system', module: 'about' };
        this.routes['/system/advanced'] = { route: '/system/advanced', title: 'Advanced Settings', package: 'system', module: 'advanced-settings' };
        
        // Load routes from Core (async, won't block)
        this.loadRoutes();
        
        // Listen for hash changes
        $(window).on('hashchange', () => this.navigate());
        
        // Listen for navigation clicks
        $(document).on('click', 'a[data-route]', (e) => {
            e.preventDefault();
            const route = $(e.currentTarget).data('route');
            window.location.hash = '#' + route;
        });
        
        // Initial navigation
        this.navigate();
    },

    /**
     * Load routes from Core
     */
    loadRoutes: async function() {
        try {
            // Load pages from Core packages
            try {
                const response = await Monolith.API.get('/core?action=get-pages');
                if ((response.Success || response.success) && (response.Data || response.data)) {
                    const pages = response.Data || response.data;
                    if (Array.isArray(pages)) {
                        pages.forEach(page => {
                            const route = (page.Route || page.route).toLowerCase();
                            this.routes[route] = {
                                route: route,
                                title: page.Title || page.title || route,
                                razorPath: page.RazorPath || page.razorPath,
                                requiredPermissions: page.RequiredPermissions || page.requiredPermissions || [],
                                isPackagePage: true
                            };
                        });
                        console.log(`Loaded ${pages.length} package routes from Core`);
                    }
                }
            } catch (error) {
                console.warn('Could not load package routes from Core:', error);
            }

            // Add built-in routes (always available) - only if not already added
            if (!this.routes['/dashboard']) this.routes['/dashboard'] = { route: '/dashboard', title: 'Dashboard', package: 'system', module: 'dashboard' };
            if (!this.routes['/users']) this.routes['/users'] = { route: '/users', title: 'User Manager', package: 'system', module: 'users' };
            if (!this.routes['/groups']) this.routes['/groups'] = { route: '/groups', title: 'User Groups', package: 'system', module: 'groups' };
            if (!this.routes['/permissions']) this.routes['/permissions'] = { route: '/permissions', title: 'Permissions', package: 'system', module: 'permissions' };
            if (!this.routes['/profile']) this.routes['/profile'] = { route: '/profile', title: 'My Profile', package: 'system', module: 'profile' };
            if (!this.routes['/system/packages']) this.routes['/system/packages'] = { route: '/system/packages', title: 'Package Manager', package: 'system', module: 'packages' };
            if (!this.routes['/system/modules']) this.routes['/system/modules'] = { route: '/system/modules', title: 'Module Manager', package: 'system', module: 'modules' };
            if (!this.routes['/system/updates']) this.routes['/system/updates'] = { route: '/system/updates', title: 'Update Manager', package: 'system', module: 'updates' };
            if (!this.routes['/system/settings']) this.routes['/system/settings'] = { route: '/system/settings', title: 'General Settings', package: 'system', module: 'settings' };
            if (!this.routes['/system/advanced']) this.routes['/system/advanced'] = { route: '/system/advanced', title: 'Advanced Settings', package: 'system', module: 'advanced-settings' };
            if (!this.routes['/system/routing']) this.routes['/system/routing'] = { route: '/system/routing', title: 'Routing', package: 'system', module: 'routing' };
            if (!this.routes['/interfaces']) this.routes['/interfaces'] = { route: '/interfaces', title: 'Interfaces', package: 'system', module: 'interfaces' };
            if (!this.routes['/system/logs']) this.routes['/system/logs'] = { route: '/system/logs', title: 'System Logs', package: 'system', module: 'system-logs' };
            if (!this.routes['/firewall/rules']) this.routes['/firewall/rules'] = { route: '/firewall/rules', title: 'Firewall Rules', package: 'system', module: 'firewall-rules' };
            if (!this.routes['/firewall/aliases']) this.routes['/firewall/aliases'] = { route: '/firewall/aliases', title: 'Firewall Aliases', package: 'system', module: 'firewall-aliases' };
            if (!this.routes['/firewall/nat']) this.routes['/firewall/nat'] = { route: '/firewall/nat', title: 'NAT Rules', package: 'system', module: 'firewall-nat' };
            if (!this.routes['/firewall/virtual-ips']) this.routes['/firewall/virtual-ips'] = { route: '/firewall/virtual-ips', title: 'Virtual IPs', package: 'system', module: 'firewall-virtual-ips' };
            if (!this.routes['/firewall/traffic-shaper']) this.routes['/firewall/traffic-shaper'] = { route: '/firewall/traffic-shaper', title: 'Traffic Shaper', package: 'system', module: 'firewall-traffic-shaper' };
            if (!this.routes['/firewall/schedules']) this.routes['/firewall/schedules'] = { route: '/firewall/schedules', title: 'Firewall Schedules', package: 'system', module: 'firewall-schedules' };
            if (!this.routes['/status/system']) this.routes['/status/system'] = { route: '/status/system', title: 'System Status', package: 'system', module: 'status' };
            if (!this.routes['/status/interfaces']) this.routes['/status/interfaces'] = { route: '/status/interfaces', title: 'Interface Status', package: 'system', module: 'status' };
            if (!this.routes['/status/services']) this.routes['/status/services'] = { route: '/status/services', title: 'Services Status', package: 'system', module: 'status' };
            if (!this.routes['/status/logs']) this.routes['/status/logs'] = { route: '/status/logs', title: 'System Logs', package: 'system', module: 'status' };
            if (!this.routes['/login']) this.routes['/login'] = { route: '/login', title: 'Login' };
            if (!this.routes['/about']) this.routes['/about'] = { route: '/about', title: 'About Monolith Firewall', package: 'system', module: 'about' };
        } catch (error) {
            console.error('Error loading routes:', error);
        }
    },

    /**
     * Navigate to route
     */
    navigate: async function() {
        let hash = window.location.hash.slice(1) || '/dashboard';
        
        // Remove leading slash if present
        if (hash.startsWith('/')) {
            hash = hash.substring(1);
        }
        
        const route = '/' + hash;
        const pageDef = this.routes[route.toLowerCase()];
        
        if (pageDef) {
            await this.loadPage(pageDef);
            return;
        }

        if (route.startsWith('/p/')) {
            await this.loadPage({ route: route, isPackagePage: true });
            return;
        }

        // Default to dashboard
        window.location.hash = '#/dashboard';
    },

    /**
     * Load page
     */
    loadPage: async function(pageDef) {
        try {
            // Update active nav link
            $('.nav-link').removeClass('active');
            $(`.nav-link[data-route="${pageDef.route}"]`).addClass('active');

            // Check if login page
            if (pageDef.route === '/login') {
                $('#top-navbar').hide();
                $('#page-content').html(this.getLoginPage());
                return;
            }

            // Show navbar for other pages
            $('#top-navbar').show();

            // Check permissions
            if (pageDef.requiredPermissions && pageDef.requiredPermissions.length > 0) {
                if (!Monolith.Auth.hasPermission(pageDef.requiredPermissions)) {
                    Monolith.UI.toast('Access denied', 'error');
                    window.location.hash = '#/dashboard';
                    return;
                }
            }

            // Load page content
            if (pageDef.isPackagePage || pageDef.route.startsWith('/p/')) {
                // Package page - use new SPA partial loading
                await this.loadPackagePage(pageDef);
            } else if (pageDef.route.startsWith('/firewall/')) {
                // Firewall pages - load as SPA partial
                await this.loadFirewallPage(pageDef);
            } else {
                // Built-in page
                $('#page-content').html(this.getBuiltInPage(pageDef));
                if (Monolith.PageLoader && typeof Monolith.PageLoader.load === 'function') {
                    await Monolith.PageLoader.load(pageDef);
                }
            }
            
            this.currentRoute = pageDef.route;
        } catch (error) {
            console.error('Error loading page:', error);
            Monolith.UI.toast('Error loading page', 'error');
        }
    },

    /**
     * Get built-in page HTML
     */
    getBuiltInPage: function(pageDef) {
        switch (pageDef.route) {
            case '/dashboard':
                return '<div id="dashboard-container"></div>';
            case '/users':
                return this.getUsersPage();
            case '/groups':
                return this.getGroupsPage();
            case '/permissions':
                return '<div id="permissions-container"></div>';
            case '/profile':
                return '<div id="profile-container"></div>';
            case '/system/packages':
                return '<div id="packages-container"></div>';
            case '/system/modules':
                return '<div id="modules-container"></div>';
            case '/system/updates':
                return '<div id="updates-container"></div>';
            case '/system/settings':
                return '<div id="settings-container"></div>';
            case '/system/advanced':
                return '<div id="advanced-settings-container"></div>';
            case '/system/routing':
                return '<div id="routing-container"></div>';
            case '/interfaces':
                return '<div id="page-content"></div>';
            case '/system/logs':
                return '<div id="page-content"></div>';
            case '/firewall/aliases':
            case '/firewall/nat':
            case '/firewall/virtual-ips':
            case '/firewall/traffic-shaper':
            case '/firewall/schedules':
                return '<div id="page-content"></div>';
            case '/status/system':
            case '/status/interfaces':
            case '/status/services':
            case '/status/logs':
                return '<div id="status-container"></div>';
            default:
                // Handle dynamic interface routes
                if (pageDef.route.startsWith('/interfaces/')) {
                    return '<div id="interfaces-container"></div>';
                }
                return `<div class="container mt-5"><h1>${pageDef.title || 'Page'}</h1></div>`;
        }
    },

    /**
     * Load package page (SPA partial)
     */
    loadPackagePage: async function(pageDef) {
        try {
            // Fetch page content
            const response = await fetch(pageDef.route);
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }

            const html = await response.text();
            
            // Insert content into page-content
            $('#page-content').html(html);
            // Ensure container-fluid has content-container class for centering
            $('#page-content').find('.container-fluid').addClass('content-container');
            $('#page-content').find('link[data-module-css], script[data-module-js]').remove();

            if (Monolith.PageLoader && typeof Monolith.PageLoader.load === 'function') {
                await Monolith.PageLoader.load(pageDef);
            }

        } catch (error) {
            console.error('Error loading package page:', error);
            $('#page-content').html(`
                <div class="container-fluid content-container p-4">
                    <div class="alert alert-danger">
                        <h4>Page Load Error</h4>
                        <p>The page at <code>${pageDef.route}</code> could not be loaded.</p>
                        <p class="mb-0"><strong>Error:</strong> ${error.message}</p>
                    </div>
                </div>
            `);
        }
    },

    /**
     * Load firewall page (SPA partial)
     */
    loadFirewallPage: async function(pageDef) {
        try {
            // Extract module name from route: /firewall/aliases -> aliases
            const module = pageDef.route.split('/').pop();

            // Fetch page content from /firewall/{module}
            const response = await fetch(`/firewall/${module}`);
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }

            const html = await response.text();

            // Insert content into page-content
            $('#page-content').html(html);
            // Ensure container-fluid has content-container class for centering
            $('#page-content').find('.container-fluid').addClass('content-container');
            $('#page-content').find('link[data-module-css], script[data-module-js]').remove();

            if (Monolith.PageLoader && typeof Monolith.PageLoader.load === 'function') {
                await Monolith.PageLoader.load(pageDef);
            }

        } catch (error) {
            console.error('Error loading firewall page:', error);
            $('#page-content').html(`
                <div class="container-fluid content-container p-4">
                    <div class="alert alert-danger">
                        <h4>Page Load Error</h4>
                        <p>The page at <code>${pageDef.route}</code> could not be loaded.</p>
                        <p class="mb-0"><strong>Error:</strong> ${error.message}</p>
                    </div>
                </div>
            `);
        }
    },

    /**
     * Get login page HTML
     */
    getLoginPage: function() {
        return `
            <div class="login-container">
                <div class="login-left">
                    <div class="login-brand">
                        <div class="login-brand-icon">🛡️</div>
                        <h1>MonolithFireWall</h1>
                        <p>Next-Generation Firewall Management</p>
                    </div>
                </div>
                <div class="login-right">
                    <div class="login-card">
                        <div class="login-card-header">
                            <h3>Welcome Back</h3>
                            <p>Sign in to your account</p>
                        </div>
                        <div class="login-card-body">
                            <form id="login-form" class="login-form">
                                <div class="form-floating mb-3">
                                    <input type="text" class="form-control" id="username" placeholder="Username" required autofocus>
                                    <label for="username">Username</label>
                                </div>
                                <div class="form-floating mb-3">
                                    <input type="password" class="form-control" id="password" placeholder="Password" required>
                                    <label for="password">Password</label>
                                </div>
                                <div class="d-flex justify-content-between align-items-center mb-3">
                                    <div class="form-check">
                                        <input type="checkbox" class="form-check-input" id="remember-me">
                                        <label class="form-check-label" for="remember-me">Remember me</label>
                                    </div>
                                </div>
                                <button type="submit" class="btn btn-primary btn-login">
                                    <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16">
                                        <path fill-rule="evenodd" d="M6 3.5a.5.5 0 0 1 .5-.5h8a.5.5 0 0 1 .5.5v9a.5.5 0 0 1-.5.5h-8a.5.5 0 0 1-.5-.5v-2a.5.5 0 0 0-1 0v2A1.5 1.5 0 0 0 6.5 14h8a1.5 1.5 0 0 0 1.5-1.5v-9A1.5 1.5 0 0 0 14.5 2h-8A1.5 1.5 0 0 0 5 3.5v2a.5.5 0 0 0 1 0v-2z"/>
                                        <path fill-rule="evenodd" d="M11.854 8.354a.5.5 0 0 0 0-.708l-3-3a.5.5 0 1 0-.708.708L10.293 7.5H1.5a.5.5 0 0 0 0 1h8.793l-2.147 2.146a.5.5 0 0 0 .708.708l3-3z"/>
                                    </svg>
                                    Sign In
                                </button>
                                <div id="login-error" class="alert alert-danger mt-3 fade-in" style="display:none;"></div>
                            </form>
                        </div>
                    </div>
                </div>
            </div>
        `;
    },

    /**
     * Get dashboard page HTML
     */
    getDashboardPage: function() {
        return `
            <div class="container-fluid">
                <h1 class="mb-4">Dashboard</h1>
                <div class="row">
                    <div class="col-md-12">
                        <div class="card">
                            <div class="card-header">
                                <h5 class="mb-0">System Status</h5>
                            </div>
                            <div class="card-body">
                                <p>Welcome to MonolithFireWall!</p>
                                <p>Core service and WebUI are running.</p>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `;
    },

    /**
     * Get users page HTML
     */
    getUsersPage: function() {
        return `
            <div class="container-fluid">
                <div class="d-flex justify-content-between align-items-center mb-4">
                    <h1>User Manager</h1>
                    <button class="btn btn-primary" id="btn-add-user">
                        <i class="bi bi-plus-circle"></i> Add User
                    </button>
                </div>
                <div class="card">
                    <div class="card-header">
                        <h5 class="mb-0">Users</h5>
                    </div>
                    <div class="card-body">
                        <div id="users-table-container">
                            <div class="text-center py-5">
                                <div class="spinner-border text-primary" role="status">
                                    <span class="visually-hidden">Loading...</span>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `;
    },

    /**
     * Get groups page HTML
     */
    getGroupsPage: function() {
        return `
            <div class="container-fluid">
                <div class="d-flex justify-content-between align-items-center mb-4">
                    <h1>User Groups</h1>
                    <button class="btn btn-primary" id="btn-add-group">
                        <i class="bi bi-plus-circle"></i> Add Group
                    </button>
                </div>
                <div class="card">
                    <div class="card-header">
                        <h5 class="mb-0">Groups</h5>
                    </div>
                    <div class="card-body">
                        <div id="groups-table-container">
                            <div class="text-center py-5">
                                <div class="spinner-border text-primary" role="status">
                                    <span class="visually-hidden">Loading...</span>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `;
    },

    /**
     * Get permissions page HTML
     */
    getPermissionsPage: function() {
        return `
            <div id="permissions-container"></div>
        `;
    },

    /**
     * Get profile page HTML
     */
    getProfilePage: function() {
        return `
            <div id="profile-container"></div>
        `;
    }
};
