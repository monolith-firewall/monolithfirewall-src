/**
 * Monolith Firewall Page Header Manager
 * Standardized page header with breadcrumb navigation
 */
var Monolith = window.Monolith || {};
Monolith.PageHeader = {
    /**
     * Render a page header
     * @param {Object} options - Configuration options
     * @param {string} [options.title] - Page title (if not provided, tries to get from route)
     * @param {string} [options.icon] - FontAwesome icon class (e.g., "fa-network-wired" or "fa-solid fa-network-wired")
     * @param {string} [options.description] - Optional description/subtitle below title (if not provided, tries to get from route)
     * @param {Array} [options.breadcrumbs] - Custom breadcrumbs array. If not provided, auto-generates from route
     * @param {string} [options.container] - Container selector (default: "#page-content")
     * @param {boolean} [options.prepend] - Prepend to container instead of replacing (default: false)
     */
    render: function(options) {
        options = options || {};
        
        const container = $(options.container || '#page-content');
        if (!container.length) {
            console.warn('PageHeader.render: container not found:', options.container || '#page-content');
            return;
        }

        // Get current route info
        const currentPath = window.location.pathname || '/';
        const routeInfo = this.getRouteInfo(currentPath);

        // Use provided title or get from route
        const title = options.title || routeInfo?.title || 'Page';
        
        // Generate breadcrumbs if not provided
        const breadcrumbs = options.breadcrumbs || this.generateBreadcrumbs();

        // Resolve icon (use provided, or route icon, or auto-detect)
        const icon = this.resolveIcon(options.icon || routeInfo?.icon, title);

        // Use provided description or get from route
        const description = options.description || routeInfo?.description || null;

        // Build HTML
        const html = this.buildHeaderHtml(title, icon, description, breadcrumbs);

        // Check if header already exists and remove it first
        container.find('.page-header').remove();
        
        // Insert into container
        if (options.prepend) {
            container.prepend(html);
        } else {
            // If not prepending, replace container content (for backwards compatibility)
            container.html(html);
        }

        // Attach navigation handlers
        this.attachNavigationHandlers();
    },

    /**
     * Auto-generate breadcrumbs from current route
     * @param {string} [path] - Route path (default: current pathname)
     * @returns {Array} Breadcrumb items
     */
    generateBreadcrumbs: function(path) {
        const currentPath = path || window.location.pathname || '/';
        const breadcrumbs = [];

        // Always start with Dashboard
        breadcrumbs.push({
            label: 'Dashboard',
            path: '/dashboard',
            icon: 'fa-home'
        });

        // If we're already on dashboard, return early
        if (currentPath === '/dashboard' || currentPath === '/') {
            return breadcrumbs;
        }

        // Handle package pages specially
        if (currentPath.startsWith('/p/')) {
            return this.generatePackageBreadcrumbs(currentPath, breadcrumbs);
        }

        // Split path into segments
        const segments = currentPath.split('/').filter(Boolean);
        
        // Build breadcrumb chain
        let accumulatedPath = '';
        const router = Monolith.CmsRouter;

        segments.forEach((segment, index) => {
            accumulatedPath += '/' + segment;
            const isLast = index === segments.length - 1;

            // Try to get route info
            const routeInfo = this.getRouteInfo(accumulatedPath);
            
            if (routeInfo) {
                breadcrumbs.push({
                    label: routeInfo.title || this.capitalize(segment),
                    path: isLast ? null : routeInfo.path, // Current page has no link
                    icon: routeInfo.icon || this.getIconForSegment(segment)
                });
            } else {
                // For parent segments without routes, try to find a sibling route
                // Example: /status/routing-status -> /status doesn't exist, but /status/states might
                if (!isLast && router && router.routesByPath) {
                    // Try to find a route that starts with this path
                    const parentPath = accumulatedPath;
                    let foundSibling = null;
                    for (const routePath in router.routesByPath) {
                        if (routePath.startsWith(parentPath + '/') && routePath !== currentPath) {
                            foundSibling = router.routesByPath[routePath];
                            break;
                        }
                    }
                    
                    if (foundSibling) {
                        // Use the parent segment name as label, link to sibling
                        breadcrumbs.push({
                            label: this.capitalize(segment.replace(/-/g, ' ')),
                            path: foundSibling.path || foundSibling.Path,
                            icon: this.getIconForSegment(segment)
                        });
                    } else {
                        // No sibling found, just use segment name without link
                        breadcrumbs.push({
                            label: this.capitalize(segment.replace(/-/g, ' ')),
                            path: null,
                            icon: this.getIconForSegment(segment)
                        });
                    }
                } else {
                    // Fallback: use segment name
                    breadcrumbs.push({
                        label: this.capitalize(segment.replace(/-/g, ' ')),
                        path: isLast ? null : accumulatedPath,
                        icon: this.getIconForSegment(segment)
                    });
                }
            }
        });

        return breadcrumbs;
    },

    /**
     * Generate breadcrumbs for package pages
     * @param {string} path - Package page path (e.g., /p/monolith-network/dhcp/config)
     * @param {Array} [existingBreadcrumbs] - Already created breadcrumbs (e.g., Dashboard)
     * @returns {Array} Breadcrumb items
     */
    generatePackageBreadcrumbs: function(path, existingBreadcrumbs) {
        const breadcrumbs = existingBreadcrumbs || [
            { label: 'Dashboard', path: '/dashboard', icon: 'fa-home' }
        ];
        
        // Add Packages breadcrumb if not already there
        const hasPackages = breadcrumbs.some(b => b.path === '/system/packages');
        if (!hasPackages) {
            breadcrumbs.push({ label: 'Packages', path: '/system/packages', icon: 'fa-box-open' });
        }

        // Parse package path: /p/{package}/{module}/{page}
        const segments = path.split('/').filter(Boolean);
        if (segments.length < 3 || segments[0] !== 'p') {
            return breadcrumbs;
        }

        const packageId = segments[1];
        const moduleId = segments[2];
        const pageId = segments[3] || null;

        // Get package page info
        const pageInfo = this.getPackagePageInfo(path);
        
        // Add package name
        breadcrumbs.push({
            label: this.capitalize(packageId.replace(/-/g, ' ')),
            path: null, // No direct link to package
            icon: 'fa-box-open'
        });

        // Add module name if different from package
        if (moduleId && moduleId !== packageId) {
            breadcrumbs.push({
                label: this.capitalize(moduleId.replace(/-/g, ' ')),
                path: null,
                icon: pageInfo?.icon ? this.resolveIconClass(pageInfo.icon) : 'fa-puzzle-piece'
            });
        }

        // Add page name (current)
        if (pageInfo) {
            breadcrumbs.push({
                label: pageInfo.title || this.capitalize((pageId || moduleId).replace(/-/g, ' ')),
                path: null, // Current page
                icon: pageInfo.icon ? this.resolveIconClass(pageInfo.icon) : null
            });
        }

        return breadcrumbs;
    },

    /**
     * Get icon for a path segment
     * @param {string} segment - Path segment
     * @returns {string|null} Icon class
     */
    getIconForSegment: function(segment) {
        const iconMap = {
            'status': 'fa-chart-line',
            'firewall': 'fa-shield-halved',
            'system': 'fa-gear',
            'interfaces': 'fa-network-wired',
            'users': 'fa-users',
            'groups': 'fa-user-group',
            'permissions': 'fa-key',
            'packages': 'fa-box-open',
            'modules': 'fa-puzzle-piece',
            'routing': 'fa-route',
            'settings': 'fa-gear',
            'advanced': 'fa-sliders',
            'logs': 'fa-clipboard-list',
            'backup': 'fa-cloud-arrow-up'
        };

        const normalized = segment.toLowerCase();
        return iconMap[normalized] || null;
    },

    /**
     * Resolve icon class (ensure it's a full FontAwesome class)
     * @param {string} icon - Icon identifier
     * @returns {string|null} Full icon class or null
     */
    resolveIconClass: function(icon) {
        if (!icon) return null;
        if (icon.includes('fa-')) {
            return icon.includes('fa-solid') || icon.includes('fa-regular') || icon.includes('fa-light') || icon.includes('fa-thin') || icon.includes('fa-duotone') || icon.includes('fa-brands')
                ? icon
                : `fa-solid ${icon}`;
        }
        // If it's just an icon name without fa- prefix, add it
        if (icon.startsWith('fa-')) {
            return `fa-solid ${icon}`;
        }
        return `fa-solid fa-${icon}`;
    },

    /**
     * Get route information for breadcrumb generation
     * @param {string} path - Route path
     * @returns {Object|null} Route info with title, path, icon, etc.
     */
    getRouteInfo: function(path) {
        const router = Monolith.CmsRouter;
        if (!router || !router.routesByPath) {
            return null;
        }

        const normalizedPath = this.normalizePath(path);
        const route = router.routesByPath[normalizedPath];

        if (!route) {
            // Check if it's a package page
            if (normalizedPath.startsWith('/p/')) {
                return this.getPackagePageInfo(normalizedPath);
            }
            return null;
        }

        const meta = route.meta || route.Meta || {};
        return {
            title: route.title || route.Title || null,
            path: route.path || route.Path || normalizedPath,
            icon: route.icon || route.Icon || meta.icon || meta.Icon || null,
            description: route.description || route.Description || meta.description || meta.Description || null
        };
    },

    /**
     * Get package page information from manifest
     * @param {string} path - Package page path (e.g., /p/monolith-network/dhcp/config)
     * @returns {Object|null} Page info with title, icon, description
     */
    getPackagePageInfo: function(path) {
        const router = Monolith.CmsRouter;
        if (!router) {
            return null;
        }

        // First try routesByPath (most reliable)
        if (router.routesByPath) {
            const normalizedPath = this.normalizePath(path);
            const route = router.routesByPath[normalizedPath];
            if (route) {
                const meta = route.meta || route.Meta || {};
                return {
                    title: route.title || route.Title || null,
                    path: route.path || route.Path || path,
                    icon: route.icon || route.Icon || meta.icon || meta.Icon || null,
                    description: route.description || route.Description || meta.description || meta.Description || null
                };
            }
        }

        // Fallback: try manifest routes
        if (router.manifest && router.manifest.routes) {
            const route = router.manifest.routes.find(r => {
                const routePath = r.path || r.Path || '';
                return this.normalizePath(routePath) === this.normalizePath(path);
            });

            if (route) {
                const meta = route.meta || route.Meta || {};
                return {
                    title: route.title || route.Title || null,
                    path: route.path || route.Path || path,
                    icon: route.icon || route.Icon || meta.icon || meta.Icon || null,
                    description: route.description || route.Description || meta.description || meta.Description || null
                };
            }
        }

        return null;
    },

    /**
     * Resolve icon class
     * @param {string} [icon] - Explicit icon
     * @param {string} [title] - Page title for fallback
     * @returns {string} FontAwesome icon class
     */
    resolveIcon: function(icon, title) {
        if (icon) {
            // If already a full class, return as-is
            if (icon.includes('fa-')) {
                return icon.includes('fa-solid') || icon.includes('fa-regular') || icon.includes('fa-light') || icon.includes('fa-thin') || icon.includes('fa-duotone') || icon.includes('fa-brands')
                    ? icon
                    : `fa-solid ${icon}`;
            }
            // Otherwise prepend fa-solid
            return `fa-solid ${icon}`;
        }

        // Auto-detect from current path
        const path = window.location.pathname || '';
        
        if (path.startsWith('/status/')) {
            return 'fa-solid fa-chart-line';
        } else if (path.startsWith('/firewall/')) {
            return 'fa-solid fa-shield-halved';
        } else if (path.startsWith('/system/')) {
            return 'fa-solid fa-gear';
        } else if (path.startsWith('/interfaces/')) {
            return 'fa-solid fa-network-wired';
        } else if (path.startsWith('/users') || path.startsWith('/groups') || path.startsWith('/permissions')) {
            return 'fa-solid fa-users';
        } else if (path.startsWith('/dashboard')) {
            return 'fa-solid fa-gauge-high';
        }

        // Default icon
        return 'fa-solid fa-circle-dot';
    },

    /**
     * Build header HTML
     * @param {string} title - Page title
     * @param {string} icon - Icon class
     * @param {string} [description] - Optional description
     * @param {Array} breadcrumbs - Breadcrumb items
     * @returns {string} HTML string
     */
    buildHeaderHtml: function(title, icon, description, breadcrumbs) {
        const descriptionHtml = description 
            ? `<span class="page-subtitle">${description}</span>`
            : '';

        const breadcrumbHtml = this.buildBreadcrumbHtml(breadcrumbs);

        return `
            <nav class="page-header navbar navbar-expand-lg">
                <div class="container-fluid">
                    <div class="page-header-title">
                        <h1 class="page-title">
                            <span class="page-icon">
                                <i class="${icon}"></i>
                            </span>
                            <span class="title-text">
                                <span class="module-title">${title}</span>
                                ${descriptionHtml}
                            </span>
                        </h1>
                    </div>
                    <div class="page-header-breadcrumb">
                        ${breadcrumbHtml}
                    </div>
                </div>
            </nav>
        `;
    },

    /**
     * Build breadcrumb HTML
     * @param {Array} breadcrumbs - Breadcrumb items
     * @returns {string} HTML string
     */
    buildBreadcrumbHtml: function(breadcrumbs) {
        if (!Array.isArray(breadcrumbs) || breadcrumbs.length === 0) {
            return '';
        }

        const items = breadcrumbs.map((crumb, index) => {
            const isLast = index === breadcrumbs.length - 1;
            
            // Resolve icon class
            const iconClass = crumb.icon ? this.resolveIconClass(crumb.icon) : null;
            const iconHtml = iconClass 
                ? `<i class="${iconClass}"></i>`
                : '';

            if (isLast || !crumb.path) {
                // Current page (no link)
                return `
                    <span class="breadcrumb-current">
                        ${iconHtml ? `<i class="${iconClass}"></i>` : ''}
                        <span class="module-name">${crumb.label}</span>
                    </span>
                `;
            } else {
                // Link
                return `
                    <a href="#" class="breadcrumb-link" data-route="${crumb.path}">
                        ${iconHtml}<span>${crumb.label}</span>
                    </a>
                    <span class="breadcrumb-separator">/</span>
                `;
            }
        }).join('');

        return items;
    },

    /**
     * Attach navigation handlers for breadcrumb links
     */
    attachNavigationHandlers: function() {
        // Remove existing handlers to avoid duplicates
        $(document).off('click', '.page-header .breadcrumb-link');
        
        // Attach new handlers
        $(document).on('click', '.page-header .breadcrumb-link', function(e) {
            e.preventDefault();
            const route = $(this).data('route');
            if (route && Monolith.CmsRouter) {
                Monolith.CmsRouter.navigate(route);
            }
        });
    },

    /**
     * Normalize path (ensure leading slash, remove trailing slash)
     * @param {string} path - Path to normalize
     * @returns {string} Normalized path
     */
    normalizePath: function(path) {
        if (!path) return '/';
        if (!path.startsWith('/')) {
            path = '/' + path;
        }
        if (path.length > 1 && path.endsWith('/')) {
            return path.slice(0, -1);
        }
        return path;
    },

    /**
     * Capitalize first letter of each word
     * @param {string} str - String to capitalize
     * @returns {string} Capitalized string
     */
    capitalize: function(str) {
        if (!str) return '';
        return str.split(' ')
            .map(word => word.charAt(0).toUpperCase() + word.slice(1).toLowerCase())
            .join(' ');
    }
};
