/**
 * Monolith Firewall Main Application
 */
$(document).ready(function() {
    const path = window.location.pathname || '/';
    if (path.startsWith('/setup') || path.startsWith('/login')) {
        return;
    }

    // Initialize authentication
    Monolith.Auth.init().then(async isAuthenticated => {
        if (!isAuthenticated) {
            window.location.href = '/login';
        } else {
            // Setup check is now handled by SetupRedirectMiddleware
            // No need to check here - middleware will redirect if needed

            // Update user info in navbar
            updateUserInfo();
            
            // Initialize theme toggle in navbar
            initNavbarThemeToggle();
            
            // Initialize menu system
            if (Monolith.Menu && typeof Monolith.Menu.init === 'function') {
                try {
                    await Monolith.Menu.init();
                } catch (error) {
                    console.error('Menu initialization failed:', error);
                }
            }
            
            if (Monolith.CmsRouter && !Monolith.CmsRouter.initialized) {
                console.log('CmsRouter found, initializing...');
                await Monolith.CmsRouter.init();
                console.log('CmsRouter initialized.');
                await loadInterfaces();
            } else {
                console.error('CmsRouter not found!');
            }
        }
    });

    // Handle logout
    $(document).on('click', '#btn-logout', function() {
        stopMonitoringUi();
        Monolith.Auth.logout();
    });

    // User/Group management is handled by their respective page modules (users.js, groups.js)

    // Guard tab behavior: ensure only the target pane is visible
    $(document).on('shown.bs.tab', '[data-bs-toggle="tab"]', function (e) {
        const targetSelector = $(e.target).attr('data-bs-target') || $(e.target).attr('href');
        if (!targetSelector) return;
        const $target = $(targetSelector);
        const $content = $target.closest('.tab-content');
        if ($content.length) {
            $content.find('.tab-pane').removeClass('show active');
            $target.addClass('show active');
        }
    });

    // Initialize NAT page when it's loaded
    function initNatPageIfNeeded() {
        const natPage = $('[data-init-nat="true"]');
        if (natPage.length && typeof Nat !== 'undefined' && typeof Nat.initializePage === 'function') {
            // Check if already initialized
            if (!natPage.data('nat-initialized')) {
                console.log('Initializing NAT page...');
                Nat.initializePage();
                natPage.data('nat-initialized', true);
            }
        }
    }

    // Check on page load
    initNatPageIfNeeded();

    // Also check when page content changes (for SPA navigation)
    const observer = new MutationObserver(function(mutations) {
        initNatPageIfNeeded();
    });

    // Observe the page content container
    const pageContent = document.getElementById('page-content');
    if (pageContent) {
        observer.observe(pageContent, {
            childList: true,
            subtree: true
        });
    }

    // Also listen for CMS router navigation events
    $(document).on('cms:page:loaded', function() {
        setTimeout(initNatPageIfNeeded, 100);
    });

});

function updateUserInfo() {
    if (Monolith.Auth.currentUser) {
        $('#user-info').text(Monolith.Auth.currentUser.username);
        startMonitoringUi();
    }
}

function initNavbarThemeToggle() {
    // Wait for theme manager to be ready
    if (!Monolith.Theme) {
        setTimeout(initNavbarThemeToggle, 100);
        return;
    }

    // Get current theme and set radio button
    const currentTheme = Monolith.Theme.getTheme();
    $(`#navbar-theme-${currentTheme}`).prop('checked', true);
    
    // Update button states
    $('#navbar-theme-toggle .btn').removeClass('active');
    $(`#navbar-theme-toggle label[for="navbar-theme-${currentTheme}"]`).addClass('active');

    // Handle theme change
    $(document).off('change', 'input[name="navbar-theme"]');
    $(document).on('change', 'input[name="navbar-theme"]', async function() {
        const theme = $(this).val();
        if (Monolith.Theme) {
            await Monolith.Theme.setTheme(theme);
            Monolith.UI.toast(`Theme changed to ${theme}`, 'success');
        }
    });

    // Listen for theme changes from other sources (e.g., profile page)
    document.addEventListener('themechange', function(e) {
        const theme = e.detail.theme;
        $(`#navbar-theme-${theme}`).prop('checked', true);
        $('#navbar-theme-toggle .btn').removeClass('active');
        $(`#navbar-theme-toggle label[for="navbar-theme-${theme}"]`).addClass('active');
    });
}

async function loadInterfaces() {
    try {
        const response = await Monolith.API.get('/interfaces/assignments');
        const data = response.Data || response.data || {};
        const assigned = data.Assigned || data.assigned || [];
        const vlans = data.Vlans || data.vlans || [];
        const bridges = data.Bridges || data.bridges || [];

        const interfaces = assigned
            .concat(vlans, bridges)
            .map(item => {
                const iface = item.Interface || item.interface || '';
                const name = item.Name || item.name || iface;
                const status = (item.Status || item.status || 'unknown').toLowerCase();
                const ip = item.IpAddress || item.ipAddress || item.ConfigAddress || item.configAddress || '';
                return {
                    name: name,
                    status: status,
                    ip: ip,
                    interface: iface
                };
            })
            .filter(item => item.interface);

        if (Monolith.Menu && typeof Monolith.Menu.renderInterfacesMenu === 'function') {
            Monolith.Menu.renderInterfacesMenu(interfaces);
        }
    } catch (error) {
        console.error('Error loading interfaces:', error);
        $('#interfaces-list-placeholder').html('<span class="dropdown-item-text text-muted small">Failed to load interfaces</span>');
    }
}

let monitoringStatusInterval = null;
let monitoringNotificationsInterval = null;

function startMonitoringUi() {
    if (monitoringStatusInterval) {
        clearInterval(monitoringStatusInterval);
    }
    if (monitoringNotificationsInterval) {
        clearInterval(monitoringNotificationsInterval);
    }

    loadMonitoringStatus();
    loadNotifications();

    monitoringStatusInterval = setInterval(loadMonitoringStatus, 30000);
    monitoringNotificationsInterval = setInterval(loadNotifications, 20000);
}

function stopMonitoringUi() {
    if (monitoringStatusInterval) {
        clearInterval(monitoringStatusInterval);
        monitoringStatusInterval = null;
    }
    if (monitoringNotificationsInterval) {
        clearInterval(monitoringNotificationsInterval);
        monitoringNotificationsInterval = null;
    }
}

async function loadMonitoringStatus() {
    try {
        const response = await Monolith.API.get('/monitoring/status');
        const monitors = response.Data || response.data || [];
        if (Monolith.Menu && typeof Monolith.Menu.renderMonitoringStatusMenu === 'function') {
            Monolith.Menu.renderMonitoringStatusMenu(monitors);
        }
    } catch (error) {
        console.error('Error loading monitoring status:', error);
        $('#monitoring-status-menu').html('<li><span class="dropdown-item-text text-muted small">Failed to load status</span></li>');
    }
}

async function loadNotifications() {
    try {
        const response = await Monolith.API.get('/monitoring/notifications?limit=20&unreadOnly=false');
        const data = response.Data || response.data || {};
        if (Monolith.Menu && typeof Monolith.Menu.renderNotificationsMenu === 'function') {
            Monolith.Menu.renderNotificationsMenu(data.Notifications || data.notifications || [], data.UnreadCount || data.unreadCount || 0);
        }
    } catch (error) {
        console.error('Error loading notifications:', error);
        $('#notifications-menu').html('<li><span class="dropdown-item-text text-muted small">Failed to load notifications</span></li>');
    }
}

// Old user/group management functions removed - now handled by users.js and groups.js page modules
