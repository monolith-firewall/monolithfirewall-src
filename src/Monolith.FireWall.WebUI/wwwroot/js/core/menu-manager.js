/**
 * MonolithFireWall Menu Manager
 * Manages persistent menu system that survives page navigation
 */
var Monolith = window.Monolith || {};
window.Monolith = Monolith;

Monolith.Menu = {
    menuHtml: null,
    initialized: false,
    dropdowns: [],

    /**
     * Initialize menu system
     */
    init: async function() {
        if (this.initialized) {
            return;
        }

        console.log('Initializing menu system...');
        await this.loadMenu();
        this.renderMenu();
        this.attachEventHandlers();
        this.initialized = true;
        console.log('Menu system initialized');
    },

    /**
     * Load menu data from Core
     */
    loadMenu: async function() {
        try {
            // Load packages
            const packagesResponse = await Monolith.API.get('/core?action=get-packages');
            if (!(packagesResponse.Success || packagesResponse.success)) {
                console.warn('Failed to load packages for menu');
                return;
            }

            const packages = packagesResponse.Data || packagesResponse.data || [];
            if (!Array.isArray(packages) || packages.length === 0) {
                this.menuHtml = '<li><span class="dropdown-item-text text-muted small">No packages installed</span></li>';
                return;
            }

            // Load menus
            const menusResponse = await Monolith.API.get('/core?action=get-menus');
            const menus = (menusResponse.Success || menusResponse.success)
                ? (menusResponse.Data || menusResponse.data || [])
                : [];

            // Build menu HTML
            this.menuHtml = this.buildMenuHtml(packages, menus);
        } catch (error) {
            console.error('Error loading menu:', error);
            this.menuHtml = '<li><span class="dropdown-item-text text-muted small">Failed to load menu</span></li>';
        }
    },

    /**
     * Build menu HTML from packages and menus
     */
    buildMenuHtml: function(packages, menus) {
        if (!menus || menus.length === 0) {
            return '<li><span class="dropdown-item-text text-muted small">No menu items available</span></li>';
        }

        let html = '';
        
        // Group menus by package
        const menusByPackage = {};
        menus.forEach(menu => {
            const packageId = menu.PackageId || menu.packageId || 'unknown';
            if (!menusByPackage[packageId]) {
                menusByPackage[packageId] = [];
            }
            menusByPackage[packageId].push(menu);
        });

        // Build menu items
        Object.keys(menusByPackage).forEach(packageId => {
            const packageMenus = menusByPackage[packageId];
            const package = packages.find(p => (p.Id || p.id) === packageId);
            const packageName = package ? (package.Name || package.name || packageId) : packageId;

            // Helper function to extract module name from menu ID (e.g., "network-dhcp" -> "dhcp")
            const getModuleFromMenuId = (menuId) => {
                if (!menuId) return '';
                const parts = menuId.split('-');
                return parts.length > 1 ? parts.slice(1).join('-') : menuId;
            };

            if (packageMenus.length === 1) {
                // Single menu item - no submenu
                const menu = packageMenus[0];
                const menuId = menu.Id || menu.id || '';
                const moduleName = getModuleFromMenuId(menuId);
                const route = menu.Route || menu.route || `/p/${packageId}/${moduleName}`;
                const label = menu.Label || menu.label || menuId;
                const icon = menu.Icon || menu.icon || 'shield';

                html += `
                    <li>
                        <a class="dropdown-item" href="#${route}" data-route="${route}">
                            <svg class="dropdown-icon" width="14" height="14" fill="currentColor" viewBox="0 0 16 16">
                                <path d="M8 0a8 8 0 1 0 0 16A8 8 0 0 0 8 0zM4.5 7.5a.5.5 0 0 0 0 1h7a.5.5 0 0 0 0-1h-7z"/>
                            </svg>
                            ${label}
                        </a>
                    </li>
                `;
            } else {
                // Multiple menu items - create submenu
                html += `
                    <li class="dropend">
                        <a class="dropdown-item dropdown-toggle" href="#" role="button" data-bs-toggle="dropdown" aria-expanded="false" data-no-route="true">
                            <svg class="dropdown-icon" width="14" height="14" fill="currentColor" viewBox="0 0 16 16">
                                <path d="M8 0a8 8 0 1 0 0 16A8 8 0 0 0 8 0zM4.5 7.5a.5.5 0 0 0 0 1h7a.5.5 0 0 0 0-1h-7z"/>
                            </svg>
                            ${packageName}
                            <svg class="ms-auto" width="12" height="12" fill="currentColor" viewBox="0 0 16 16" style="transform: rotate(-90deg);">
                                <path fill-rule="evenodd" d="M1.646 4.646a.5.5 0 0 1 .708 0L8 10.293l5.646-5.647a.5.5 0 0 1 .708.708l-6 6a.5.5 0 0 1-.708 0l-6-6a.5.5 0 0 1 0-.708z"/>
                            </svg>
                        </a>
                        <ul class="dropdown-menu">
                `;

                packageMenus.forEach(menu => {
                    const menuId = menu.Id || menu.id || '';
                    const moduleName = getModuleFromMenuId(menuId);
                    const route = menu.Route || menu.route || `/p/${packageId}/${moduleName}`;
                    const label = menu.Label || menu.label || menuId;
                    const icon = menu.Icon || menu.icon || 'shield';

                    html += `
                        <li>
                            <a class="dropdown-item" href="#${route}" data-route="${route}">
                                <svg class="dropdown-icon" width="14" height="14" fill="currentColor" viewBox="0 0 16 16">
                                    <path d="M8 0a8 8 0 1 0 0 16A8 8 0 0 0 8 0zM4.5 7.5a.5.5 0 0 0 0 1h7a.5.5 0 0 0 0-1h-7z"/>
                                </svg>
                                ${label}
                            </a>
                        </li>
                    `;
                });

                html += `
                        </ul>
                    </li>
                `;
            }
        });

        return html || '<li><span class="dropdown-item-text text-muted small">No menu items</span></li>';
    },

    /**
     * Render menu HTML
     */
    renderMenu: function() {
        if (!this.menuHtml) {
            return;
        }

        const container = $('#packages-menu');
        if (container.length === 0) {
            console.warn('Packages menu container not found');
            return;
        }

        container.html(this.menuHtml);
        this.initDropdowns();
    },

    /**
     * Initialize Bootstrap dropdowns (for click support, but we use hover)
     */
    initDropdowns: function() {
        // Don't initialize Bootstrap dropdowns - we'll use manual hover
        // This prevents conflicts with our hover implementation
        this.dropdowns = [];
    },

    /**
     * Attach event handlers (use delegation for persistence)
     */
    attachEventHandlers: function() {
        // Prevent navigation on dropdown toggles (they don't have data-route)
        $(document).off('click', '#packages-menu a[data-no-route]');
        $(document).on('click', '#packages-menu a[data-no-route]', function(e) {
            e.preventDefault();
            // Don't navigate - just toggle submenu
        });

        // Hover support for submenus (pfSense style) - manual positioning
        $(document).off('mouseenter', '#packages-menu .dropend');
        $(document).on('mouseenter', '#packages-menu .dropend', function(e) {
            const $dropend = $(this);
            const $link = $dropend.find('.dropdown-toggle').first();
            const $submenu = $dropend.find('.dropdown-menu').first();
            
            if ($submenu.length && $link.length) {
                // Get position of the parent link
                const offset = $link.offset();
                const height = $link.outerHeight();
                const width = $link.outerWidth();
                
                // Position submenu to the right of the parent item
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

        // Hide submenu when leaving the dropend
        $(document).off('mouseleave', '#packages-menu .dropend');
        $(document).on('mouseleave', '#packages-menu .dropend', function() {
            const $dropend = $(this);
            const $submenu = $dropend.find('.dropdown-menu').first();
            
            // Small delay to allow moving to submenu
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

        // Keep submenu open when hovering over it
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

        // Use event delegation - handlers persist across content changes
        // Only handle links with data-route (not dropdown toggles)
        $(document).off('click', '#packages-menu a[data-route]');
        $(document).on('click', '#packages-menu a[data-route]', function(e) {
            e.preventDefault();
            e.stopPropagation();
            const route = $(this).data('route');
            if (route) {
                // Close any open dropdowns
                $('#packages-menu .dropdown-menu').css('display', 'none').removeClass('show');
                window.location.hash = '#' + route;
            }
        });
    },

    /**
     * Refresh menu (when packages change)
     */
    refresh: async function() {
        console.log('Refreshing menu...');
        await this.loadMenu();
        this.renderMenu();
        this.attachEventHandlers();
    }
};
