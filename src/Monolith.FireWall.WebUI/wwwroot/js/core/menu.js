/**
 * Monolith Firewall Menu Manager
 * Handles all menu rendering and management
 */
var Monolith = window.Monolith || {};
Monolith.Menu = {
    menuData: null,
    routesById: {},
    routesByPath: {},

    init: async function() {
        console.log('Initializing Menu system...');
        try {
            await this.load();
            this.render();
            this.attachEventHandlers();
            console.log('Menu system initialized');
        } catch (error) {
            console.error('Failed to initialize menu system:', error);
        }
    },

    load: async function() {
        try {
            const response = await Monolith.API.get('/api/cms/menu.json');
            if (!response || !(response.success || response.Success)) {
                throw new Error('Failed to load menu: invalid response');
            }
            this.menuData = response.menu || response.Menu || [];
            
            // Resolve missing paths from routeId
            this.resolveMenuPaths(this.menuData);
            
            // Build route lookup maps from manifest if available
            // Wait a bit for router to initialize
            setTimeout(() => {
                if (Monolith.CmsRouter && Monolith.CmsRouter.routesById) {
                    this.routesById = Monolith.CmsRouter.routesById;
                    this.routesByPath = Monolith.CmsRouter.routesByPath;
                    // Re-resolve paths now that router is ready
                    this.resolveMenuPaths(this.menuData);
                }
            }, 100);
            
            return this.menuData;
        } catch (error) {
            console.error('Error loading menu:', error);
            this.menuData = [];
            return [];
        }
    },

    resolveMenuPaths: function(menuItems) {
        if (!Array.isArray(menuItems)) return;
        
        menuItems.forEach(item => {
            if (!item) return;
            
            // If path is missing but routeId exists, try to resolve it
            if ((!item.path || item.path === null) && (item.routeId || item.RouteId)) {
                const routeId = item.routeId || item.RouteId;
                
                // Try router first (most reliable)
                const router = Monolith.CmsRouter;
                if (router && router.routesById && router.routesById[routeId]) {
                    const route = router.routesById[routeId];
                    if (route && route.path) {
                        item.path = route.path;
                        item.Path = route.path;
                    }
                }
                
                // Try our cache
                if ((!item.path || item.path === null) && this.routesById && this.routesById[routeId]) {
                    const route = this.routesById[routeId];
                    if (route && route.path) {
                        item.path = route.path;
                        item.Path = route.path;
                    }
                }
            }
            
            // Recursively resolve children
            if (item.children || item.Children) {
                this.resolveMenuPaths(item.children || item.Children);
            }
        });
    },

    render: function() {
        if (!this.menuData || !Array.isArray(this.menuData)) {
            console.warn('Menu data not loaded or invalid');
            return;
        }

        // Ensure paths are resolved before rendering
        this.resolveMenuPaths(this.menuData);

        const containerMap = {
            'system': '#menu-system',
            'interfaces': '#interfaces-menu',
            'firewall': '#menu-firewall',
            'status': '#menu-status',
            'packages': '#packages-menu'
        };

        this.menuData.forEach(group => {
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

        // Attach package menu hover handlers
        this.attachPackageMenuHandlers();
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
        const label = item.label || item.Label || 'Unnamed';
        const icon = item.icon || item.Icon || null;
        const iconClass = this.resolveIconClass(icon, groupKey);
        const path = this.getMenuPath(item);
        
        // Use consistent styling for all menus
        const displayLabel = `<span class="d-inline-flex align-items-center gap-2"><i class="dropdown-icon ${iconClass}"></i><span>${label}</span></span>`;

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

        return `<li><a class="dropdown-item" href="${path}" data-route="${path}">${displayLabel}</a></li>`;
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
            // Try to get route from router if available
            const router = Monolith.CmsRouter;
            if (router && router.routesById) {
                const route = router.routesById[routeId];
                if (route && route.path) {
                    return this.normalizePath(route.path);
                }
            }
            // Fallback: try our own cache
            const route = this.routesById[routeId];
            if (route && route.path) {
                return this.normalizePath(route.path);
            }
        }

        return null;
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

    resolveIconClass: function(icon, groupKey) {
        if (!icon || icon === null) {
            const defaults = {
                system: 'fa-solid fa-gear',
                status: 'fa-solid fa-chart-line',
                interfaces: 'fa-solid fa-network-wired',
                firewall: 'fa-solid fa-shield-halved',
                packages: 'fa-solid fa-box-open'
            };
            return defaults[groupKey] || 'fa-solid fa-circle-dot';
        }

        const value = String(icon).toLowerCase().trim();
        
        // If it's already a FontAwesome class, return as-is
        if (value.includes('fa-')) {
            return value.includes('fa-solid') || value.includes('fa-regular') || value.includes('fa-light') || value.includes('fa-thin') || value.includes('fa-duotone') || value.includes('fa-brands')
                ? value
                : `fa-solid ${value}`;
        }

        // Map common icon names to FontAwesome classes
        const map = {
            shield: 'fa-solid fa-shield-halved',
            firewall: 'fa-solid fa-fire-flame-curved',
            network: 'fa-solid fa-network-wired',
            router: 'fa-solid fa-route',
            package: 'fa-solid fa-box-open',
            module: 'fa-solid fa-puzzle-piece',
            settings: 'fa-solid fa-gear',
            gear: 'fa-solid fa-gear',
            chart: 'fa-solid fa-chart-line',
            status: 'fa-solid fa-chart-line',
            activity: 'fa-solid fa-chart-line',
            server: 'fa-solid fa-server',
            clipboard: 'fa-solid fa-clipboard-list',
            list: 'fa-solid fa-list-check',
            rightleft: 'fa-solid fa-right-left',
            nodes: 'fa-solid fa-circle-nodes',
            wave: 'fa-solid fa-wave-square',
            calendar: 'fa-solid fa-calendar-days',
            gauge: 'fa-solid fa-gauge-high',
            microchip: 'fa-solid fa-microchip',
            users: 'fa-solid fa-users',
            usergroup: 'fa-solid fa-user-group',
            key: 'fa-solid fa-key',
            arrows: 'fa-solid fa-arrows-rotate',
            cloud: 'fa-solid fa-cloud-arrow-up'
        };

        return map[value] || `fa-solid fa-${value}`;
    },

    attachEventHandlers: function() {
        // Package menu hover handlers are in attachPackageMenuHandlers
    },

    attachPackageMenuHandlers: function() {
        // Remove existing handlers
        $(document).off('mouseenter', '#packages-menu .dropend');
        $(document).off('mouseleave', '#packages-menu .dropend');
        $(document).off('mouseenter', '#packages-menu .dropend .dropdown-menu');
        $(document).off('mouseleave', '#packages-menu .dropend .dropdown-menu');

        // Hover support for package submenus (dropend)
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
                });
            }
        });

        $(document).on('mouseleave', '#packages-menu .dropend', function() {
            const $dropend = $(this);
            const $submenu = $dropend.find('.dropdown-menu').first();
            if ($submenu.length) {
                if (!$dropend.is(':hover') && !$submenu.is(':hover')) {
                    $submenu.css('display', 'none');
                }
            }
        });

        $(document).on('mouseenter', '#packages-menu .dropend .dropdown-menu', function() {
            const $submenu = $(this);
            $submenu.css('display', 'block');
        });

        $(document).on('mouseleave', '#packages-menu .dropend .dropdown-menu', function() {
            const $submenu = $(this);
            const $dropend = $submenu.closest('.dropend');
            if ($dropend.length) {
                if (!$dropend.is(':hover') && !$submenu.is(':hover')) {
                    $submenu.css('display', 'none');
                }
            }
        });
    },

    renderInterfacesMenu: function(interfaces) {
        const container = $('#interfaces-list-placeholder');
        if (!container.length) return;
        
        if (!interfaces || interfaces.length === 0) {
            container.html('<span class="dropdown-item-text text-muted small">No managed interfaces</span>');
            return;
        }

        let html = '';
        interfaces.forEach(iface => {
            const isUp = iface.status === 'up';
            const statusDot = isUp
                ? '<span class="badge bg-success me-2" style="width: 8px; height: 8px; padding: 0; border-radius: 50%;"></span>'
                : '<span class="badge bg-secondary me-2" style="width: 8px; height: 8px; padding: 0; border-radius: 50%;"></span>';
            
            html += `
                <li><a class="dropdown-item" href="/interfaces/${iface.interface}" data-route="/interfaces/${iface.interface}">
                    ${statusDot}
                    <strong>${iface.name}</strong>
                    <small class="text-muted d-block ms-3">${iface.ip || 'No IP assigned'}</small>
                </a></li>
            `;
        });
        
        if (html) {
            container.replaceWith(html);
        }
    },

    renderMonitoringStatusMenu: function(monitors) {
        const container = $('#monitoring-status-menu');
        const indicator = $('#monitoring-status-indicator');
        if (!container.length) return;

        if (!Array.isArray(monitors) || monitors.length === 0) {
            container.html('<li><span class="dropdown-item-text text-muted small">No monitors configured</span></li>');
            indicator.removeClass('status-ok status-warn status-error status-unknown').addClass('status-unknown');
            return;
        }

        let overall = 'ok';
        monitors.forEach(m => {
            const status = (m.Status || m.status || 'unknown').toLowerCase();
            if (status === 'error') {
                overall = 'error';
            } else if (status === 'warning' && overall !== 'error') {
                overall = 'warning';
            } else if (status === 'unknown' && overall === 'ok') {
                overall = 'unknown';
            }
        });

        indicator.removeClass('status-ok status-warn status-error status-unknown');
        indicator.addClass(`status-${overall === 'warning' ? 'warn' : overall}`);

        const items = monitors.map(m => {
            const status = (m.Status || m.status || 'unknown').toLowerCase();
            const name = m.Name || m.name || m.Key || m.key || 'Monitor';
            const message = m.Message || m.message || '';
            const lastCheck = m.LastCheckAt || m.lastCheckAt;
            const lastText = lastCheck ? new Date(lastCheck).toLocaleString() : 'Not checked yet';
            const badgeClass = status === 'ok' ? 'bg-success' : status === 'warning' ? 'bg-warning' : status === 'error' ? 'bg-danger' : 'bg-secondary';

            return `
                <li>
                    <div class="dropdown-item-text monitoring-item">
                        <div class="d-flex align-items-center justify-content-between">
                            <span>${name}</span>
                            <span class="badge ${badgeClass} text-uppercase">${status}</span>
                        </div>
                        <div class="small text-muted">${message || 'No details'}</div>
                        <div class="small text-muted">Last check: ${lastText}</div>
                    </div>
                </li>
            `;
        }).join('');

        container.html(items);
    },

    renderNotificationsMenu: function(notifications, unreadCount) {
        const container = $('#notifications-menu');
        const badge = $('#notifications-badge');
        if (!container.length) return;

        if (badge.length) {
            if (unreadCount > 0) {
                badge.text(unreadCount > 99 ? '99+' : unreadCount);
                badge.removeClass('d-none');
            } else {
                badge.addClass('d-none');
            }
        }

        if (!Array.isArray(notifications) || notifications.length === 0) {
            const noNotifContent = `
                <li><div class="dropdown-item-text text-center text-muted py-3"><i class="fa-solid fa-bell-slash fa-2x mb-2 d-block"></i>No notifications</div></li>
                <li><hr class="dropdown-divider"></li>
                <li><a href="/notifications" data-route="/notifications" class="dropdown-item text-center" style="font-weight: 500;"><i class="fa-solid fa-list me-2"></i>View All Notifications</a></li>
            `;
            container.html(noNotifContent);
            return;
        }

        // Show up to 5 most recent notifications (prioritize unread, but show read ones too)
        const unreadNotifications = notifications.filter(n => !(n.ReadAt || n.readAt));
        const readNotifications = notifications.filter(n => !!(n.ReadAt || n.readAt));
        const itemsToShow = [...unreadNotifications.slice(0, 5), ...readNotifications.slice(0, 5 - unreadNotifications.length)].slice(0, 5);

        const items = itemsToShow.map(n => {
            const severity = (n.Severity || n.severity || 'info').toLowerCase();
            const title = n.Title || n.title || 'Notification';
            const message = n.Message || n.message || '';
            const created = n.CreatedAt || n.createdAt;
            const createdText = created ? new Date(created).toLocaleString() : '';
            const readAt = n.ReadAt || n.readAt;
            const unreadClass = readAt ? '' : 'unread';
            const badgeClass = severity === 'error' ? 'bg-danger' : severity === 'warning' ? 'bg-warning' : 'bg-info';

            return `
                <li>
                    <a href="javascript:void(0)" class="dropdown-item notification-item ${unreadClass}" data-id="${n.Id || n.id}">
                        <div class="notification-title-row">
                            <span class="notification-title">${title}</span>
                            <span class="badge ${badgeClass} text-uppercase ms-auto flex-shrink-0">${severity}</span>
                        </div>
                        <div class="notification-message">${message || 'No message'}</div>
                        <div class="notification-date">${createdText}</div>
                    </a>
                </li>
            `;
        }).join('');

        const markReadBtn = unreadCount > 0
            ? `<li><a href="javascript:void(0)" class="dropdown-item text-center text-primary" id="notifications-mark-read" style="font-weight: 500;"><i class="fa-solid fa-check-double me-2"></i>Mark all read</a></li>`
            : '';

        const footer = `
            <li><hr class="dropdown-divider"></li>
            ${markReadBtn}
            <li>
                <a href="/notifications" data-route="/notifications" class="dropdown-item text-center" style="font-weight: 500;">
                    <i class="fa-solid fa-list me-2"></i>View All Notifications
                </a>
            </li>
        `;

        container.html(items + footer);
    }
};

// Notification event handlers
$(document).on('click', '.notification-item', async function() {
    const id = $(this).data('id');
    if (!id) return;
    try {
        await Monolith.API.post('/monitoring/notifications/read', { ids: [id] });
        // Reload notifications if loadNotifications function exists
        if (typeof loadNotifications === 'function') {
            loadNotifications();
        }
    } catch (error) {
        console.error('Error marking notification read:', error);
    }
});

$(document).on('click', '#notifications-mark-read', async function(e) {
    e.preventDefault();
    e.stopPropagation();

    const btn = $(this);
    const originalText = btn.html();
    btn.prop('disabled', true).html('<i class="fa-solid fa-spinner fa-spin me-2"></i>Marking read...');

    try {
        const response = await Monolith.API.post('/monitoring/notifications/read', { all: true });
        if (response.Success || response.success) {
            if (typeof Monolith !== 'undefined' && Monolith.UI) {
                Monolith.UI.toast('All notifications marked as read', 'success');
            }

            const dropdown = bootstrap.Dropdown.getInstance(btn.closest('.dropdown').find('[data-bs-toggle="dropdown"]')[0]);
            if (dropdown) {
                dropdown.hide();
            }

            if (typeof loadNotifications === 'function') {
                await loadNotifications();
            }

            if (window.location.pathname === '/notifications' && Monolith.Pages && Monolith.Pages.Notifications && typeof Monolith.Pages.Notifications.loadNotifications === 'function') {
                await Monolith.Pages.Notifications.loadNotifications();
            }
        } else {
            throw new Error(response.Error || response.error || 'Failed to mark notifications as read');
        }
    } catch (error) {
        console.error('Error marking notifications read:', error);
        if (typeof Monolith !== 'undefined' && Monolith.UI) {
            Monolith.UI.toast('Failed to mark notifications as read', 'error');
        }
        btn.html(originalText);
        btn.prop('disabled', false);
    }
});
