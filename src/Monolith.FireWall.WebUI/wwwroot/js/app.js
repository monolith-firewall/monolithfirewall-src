/**
 * Monolith Firewall Main Application
 */
$(document).ready(function() {
    // Initialize router first (needed for login page)
    Monolith.Router.init();
    
    // Initialize authentication
    Monolith.Auth.init().then(async isAuthenticated => {
        if (!isAuthenticated) {
            window.location.hash = '#/login';
        } else {
            // Check if setup is needed (skip if already on setup page)
            if (!window.location.pathname.startsWith('/setup')) {
                try {
                    const setupStatus = await Monolith.API.get('/setup/status');
                    if (setupStatus.needsSetup) {
                        window.location.href = '/setup';
                        return;
                    }
                } catch (err) {
                    console.error('Failed to check setup status:', err);
                    // Continue normally if check fails
                }
            }

            // Update user info in navbar
            updateUserInfo();
            // Navigate to dashboard if no hash is set
            if (!window.location.hash || window.location.hash === '#') {
                window.location.hash = '#/dashboard';
            }
        }
    });

    // Handle login form submission
    $(document).on('submit', '#login-form', async function(e) {
        e.preventDefault();
        
        const username = $('#username').val();
        const password = $('#password').val();
        
        const success = await Monolith.Auth.login(username, password);
        
        if (success) {
            updateUserInfo();
            window.location.hash = '#/dashboard';
        } else {
            $('#login-error').text('Invalid credentials').show();
        }
    });

    // Handle logout
    $(document).on('click', '#btn-logout', function() {
        stopMonitoringUi();
        Monolith.Auth.logout();
    });

    // Load users page
    $(document).on('click', '#btn-add-user', function() {
        showAddUserModal();
    });

    // Load groups page
    $(document).on('click', '#btn-add-group', function() {
        showAddGroupModal();
    });

});

function updateUserInfo() {
    if (Monolith.Auth.currentUser) {
        $('#user-info').text(Monolith.Auth.currentUser.username);
        // Load package menus after authentication
        loadPackageMenus();
        startMonitoringUi();
    }
}

async function loadPackageMenus() {
    try {
        // Load interfaces
        await loadInterfaces();
        
        // Load packages/modules
        await loadPackagesMenu();
    } catch (error) {
        console.error('Error loading menus:', error);
    }
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

        renderInterfacesMenu(interfaces);
    } catch (error) {
        console.error('Error loading interfaces:', error);
        $('#interfaces-list-placeholder').html('<span class="dropdown-item-text text-muted small">Failed to load interfaces</span>');
    }
}

function renderInterfacesMenu(interfaces) {
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
            <li><a class="dropdown-item" href="#/interfaces/${iface.interface}" data-route="/interfaces/${iface.interface}">
                ${statusDot}
                <strong>${iface.name}</strong>
                <small class="text-muted d-block ms-3">${iface.ip || 'No IP assigned'}</small>
            </a></li>
        `;
    });
    
    if (html) {
        container.replaceWith(html);
    }
}

async function loadPackagesMenu() {
    // Use new Menu manager
    if (Monolith.Menu) {
        await Monolith.Menu.init();
    } else {
        console.error('Menu manager not available');
        $('#packages-menu').html('<li><span class="dropdown-item-text text-muted small">Menu system not initialized</span></li>');
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
        renderMonitoringStatusMenu(monitors);
    } catch (error) {
        console.error('Error loading monitoring status:', error);
        $('#monitoring-status-menu').html('<li><span class="dropdown-item-text text-muted small">Failed to load status</span></li>');
    }
}

async function loadNotifications() {
    try {
        const response = await Monolith.API.get('/monitoring/notifications?limit=20&unreadOnly=false');
        const data = response.Data || response.data || {};
        renderNotificationsMenu(data.Notifications || data.notifications || [], data.UnreadCount || data.unreadCount || 0);
    } catch (error) {
        console.error('Error loading notifications:', error);
        $('#notifications-menu').html('<li><span class="dropdown-item-text text-muted small">Failed to load notifications</span></li>');
    }
}

function renderMonitoringStatusMenu(monitors) {
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
}

function renderNotificationsMenu(notifications, unreadCount) {
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
        container.html('<li><span class="dropdown-item-text text-muted small">No notifications</span></li>');
        return;
    }

    const items = notifications.map(n => {
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
                    <div class="d-flex align-items-center justify-content-between">
                        <span>${title}</span>
                        <span class="badge ${badgeClass} text-uppercase">${severity}</span>
                    </div>
                    <div class="small text-muted">${message}</div>
                    <div class="small text-muted">${createdText}</div>
                </a>
            </li>
        `;
    }).join('');

    const footer = `
        <li><hr class="dropdown-divider"></li>
        <li><a href="javascript:void(0)" class="dropdown-item text-center" id="notifications-mark-read">Mark all read</a></li>
    `;

    container.html(items + footer);
}

$(document).on('click', '.notification-item', async function() {
    const id = $(this).data('id');
    if (!id) return;
    try {
        await Monolith.API.post('/monitoring/notifications/read', { ids: [id] });
        loadNotifications();
    } catch (error) {
        console.error('Error marking notification read:', error);
    }
});

$(document).on('click', '#notifications-mark-read', async function() {
    try {
        await Monolith.API.post('/monitoring/notifications/read', { all: true });
        loadNotifications();
    } catch (error) {
        console.error('Error marking notifications read:', error);
    }
});

function renderPackagesMenu(packages, menus) {
    const container = $('#packages-menu');
    if (!container.length) return;
    
    let html = '';
    
    // Group menus by package
    const packageMenus = {};
    menus.forEach(menu => {
        const pkgId = menu.PackageId || menu.packageId;
        const pkgName = menu.PackageName || menu.packageName;
        const moduleId = menu.ModuleId || menu.moduleId;
        const moduleName = menu.ModuleName || menu.moduleName || moduleId;
        
        if (pkgId) {
            if (!packageMenus[pkgId]) {
                packageMenus[pkgId] = {
                    name: pkgName || pkgId,
                    menus: []
                };
            }
            packageMenus[pkgId].menus.push({
                ...menu,
                moduleId: moduleId,
                moduleDisplayName: moduleName.toUpperCase()
            });
        }
    });
    
    // Render each package as a nested dropdown
    Object.keys(packageMenus).sort().forEach(pkgId => {
        const pkgData = packageMenus[pkgId];
        const pkgName = pkgData.name;
        const pkgMenus = pkgData.menus;
        
        if (pkgMenus.length > 0) {
            const packageMenuId = `package-${pkgId.replace(/[^a-z0-9]/gi, '-')}`;
            html += `
                <li class="dropend">
                    <a class="dropdown-item" href="javascript:void(0);" id="${packageMenuId}-toggle">
                        <svg class="dropdown-icon" width="14" height="14" fill="currentColor" viewBox="0 0 16 16">
                            <path d="M2.5 3.5a.5.5 0 0 1 0-1h11a.5.5 0 0 1 0 1h-11zm2-2a.5.5 0 0 1 0-1h7a.5.5 0 0 1 0 1h-7zM0 13a1.5 1.5 0 0 0 1.5 1.5h13A1.5 1.5 0 0 0 16 13V6a1.5 1.5 0 0 0-1.5-1.5h-13A1.5 1.5 0 0 0 0 6v7zm1.5.5A.5.5 0 0 1 1 13V6a.5.5 0 0 1 .5-.5h13a.5.5 0 0 1 .5.5v7a.5.5 0 0 1-.5.5h-13z"/>
                        </svg>
                        ${pkgName}
                        <svg class="ms-auto" width="12" height="12" fill="currentColor" viewBox="0 0 16 16" style="transform: rotate(-90deg);">
                            <path fill-rule="evenodd" d="M1.646 4.646a.5.5 0 0 1 .708 0L8 10.293l5.646-5.647a.5.5 0 0 1 .708.708l-6 6a.5.5 0 0 1-.708 0l-6-6a.5.5 0 0 1 0-.708z"/>
                        </svg>
                    </a>
                    <ul class="dropdown-menu" id="${packageMenuId}">
            `;
            
            pkgMenus.forEach(menu => {
                const menuLabel = menu.Label || menu.label;
                const menuId = menu.Id || menu.id;
                const moduleDisplayName = menu.moduleDisplayName || (menu.moduleId || '').toUpperCase();
                const children = menu.Children || menu.children || [];
                
                if (children && children.length > 0) {
                    // Module with submenu - render children with module badge
                    children.forEach(child => {
                        const childLabel = child.Label || child.label;
                        const childId = child.Id || child.id;
                        
                        // Dynamically construct route from package/module/page structure
                        const pageName = childId.split('-').pop();
                        const route = `/p/${pkgId}/${menu.moduleId || menuId.split('-').pop()}/${pageName}`;
                        
                        // Create descriptive label with module badge
                        const displayLabel = `<span class="badge bg-primary me-2" style="font-size: 0.7rem; font-weight: 600;">${moduleDisplayName}</span>${childLabel}`;
                        
                        html += `
                            <li><a class="dropdown-item" href="#${route}" data-route="${route}">
                                ${displayLabel}
                            </a></li>
                        `;
                    });
                } else {
                    // Direct module link (no submenu) - still show badge
                    const moduleIdForRoute = menu.moduleId || menuId.split('-').pop();
                    const route = `/p/${pkgId}/${moduleIdForRoute}`;
                    const displayLabel = `<span class="badge bg-primary me-2" style="font-size: 0.7rem; font-weight: 600;">${moduleDisplayName}</span>${menuLabel}`;
                    
                    html += `
                        <li><a class="dropdown-item" href="#${route}" data-route="${route}">
                            <svg class="dropdown-icon" width="14" height="14" fill="currentColor" viewBox="0 0 16 16">
                                <path d="M8 4.754a3.246 3.246 0 1 0 0 6.492 3.246 3.246 0 0 0 0-6.492zM5.754 8a2.246 2.246 0 1 1 4.492 0 2.246 2.246 0 0 1-4.492 0z"/>
                            </svg>
                            ${displayLabel}
                        </a></li>
                    `;
                }
            });
            
            html += `
                    </ul>
                </li>
            `;
        }
    });
    
    if (html) {
        container.html(html);
        
        // Initialize jQuery-based nested dropdowns
        setTimeout(() => {
            $('#packages-menu .dropend > a').on('mouseenter', function() {
                const $submenu = $(this).next('.dropdown-menu');
                const $link = $(this);
                
                // Position submenu to the right of the parent item
                const offset = $link.offset();
                const height = $link.outerHeight();
                const width = $link.outerWidth();
                
                $submenu.css({
                    'display': 'block',
                    'position': 'fixed',
                    'top': offset.top + 'px',
                    'left': (offset.left + width) + 'px'
                });
            });
            
            $('#packages-menu .dropend').on('mouseleave', function() {
                $(this).find('.dropdown-menu').css('display', 'none');
            });
            
            // Keep submenu open when hovering over it
            $('#packages-menu .dropend .dropdown-menu').on('mouseenter', function() {
                $(this).css('display', 'block');
            });
        }, 100);
    } else {
        container.html('<li><span class="dropdown-item-text text-muted small">No package modules available</span></li>');
    }
}

// Old renderNetworkMenu removed - packages now go in Packages menu

async function loadUsersTable() {
    try {
        const response = await Monolith.API.get('/users');
        if (response.success && response.data) {
            renderUsersTable(response.data);
        }
    } catch (error) {
        console.error('Error loading users:', error);
        Monolith.UI.toast('Error loading users', 'error');
    }
}

function renderUsersTable(users) {
    let html = `
        <table class="table table-hover">
            <thead>
                <tr>
                    <th>ID</th>
                    <th>Username</th>
                    <th>Email</th>
                    <th>Roles</th>
                    <th>Status</th>
                    <th>Actions</th>
                </tr>
            </thead>
            <tbody>
    `;

    users.forEach(user => {
        const roles = user.roles || [];
        const statusBadge = user.enabled 
            ? '<span class="badge badge-success">Enabled</span>'
            : '<span class="badge badge-danger">Disabled</span>';
        
        html += `
            <tr>
                <td>${user.id}</td>
                <td>${user.username}</td>
                <td>${user.email}</td>
                <td>${roles.map(r => `<span class="badge badge-primary">${r}</span>`).join(' ')}</td>
                <td>${statusBadge}</td>
                <td>
                    <button class="btn btn-sm btn-secondary" onclick="editUser(${user.id})">Edit</button>
                    <button class="btn btn-sm btn-danger" onclick="deleteUser(${user.id})">Delete</button>
                </td>
            </tr>
        `;
    });

    html += `
            </tbody>
        </table>
    `;

    $('#users-table-container').html(html);
}

async function loadGroupsTable() {
    try {
        const response = await Monolith.API.get('/usergroups');
        if (response.success && response.data) {
            renderGroupsTable(response.data);
        }
    } catch (error) {
        console.error('Error loading groups:', error);
        Monolith.UI.toast('Error loading groups', 'error');
    }
}

function renderGroupsTable(groups) {
    let html = `
        <table class="table table-hover">
            <thead>
                <tr>
                    <th>ID</th>
                    <th>Name</th>
                    <th>Description</th>
                    <th>Permissions</th>
                    <th>Status</th>
                    <th>Actions</th>
                </tr>
            </thead>
            <tbody>
    `;

    groups.forEach(group => {
        const perms = group.permissions || [];
        const statusBadge = group.enabled 
            ? '<span class="badge badge-success">Enabled</span>'
            : '<span class="badge badge-danger">Disabled</span>';
        
        html += `
            <tr>
                <td>${group.id}</td>
                <td><strong>${group.name}</strong></td>
                <td>${group.description || '-'}</td>
                <td>${perms.length > 0 ? perms.slice(0, 3).map(p => `<span class="badge badge-primary">${p}</span>`).join(' ') + (perms.length > 3 ? ' ...' : '') : '-'}</td>
                <td>${statusBadge}</td>
                <td>
                    <button class="btn btn-sm btn-secondary" onclick="editGroup(${group.id})">Edit</button>
                    <button class="btn btn-sm btn-danger" onclick="deleteGroup(${group.id})">Delete</button>
                </td>
            </tr>
        `;
    });

    html += `
            </tbody>
        </table>
    `;

    $('#groups-table-container').html(html);
}

function showAddUserModal() {
    // TODO: Implement add user modal
    Monolith.UI.toast('Add user feature coming soon', 'info');
}

function showAddGroupModal() {
    // TODO: Implement add group modal
    Monolith.UI.toast('Add group feature coming soon', 'info');
}

function editUser(id) {
    // TODO: Implement edit user
    Monolith.UI.toast('Edit user feature coming soon', 'info');
}

function deleteUser(id) {
    if (confirm('Are you sure you want to delete this user?')) {
        // TODO: Implement delete user
        Monolith.UI.toast('Delete user feature coming soon', 'info');
    }
}

function editGroup(id) {
    // TODO: Implement edit group
    Monolith.UI.toast('Edit group feature coming soon', 'info');
}

function deleteGroup(id) {
    if (confirm('Are you sure you want to delete this group?')) {
        // TODO: Implement delete group
        Monolith.UI.toast('Delete group feature coming soon', 'info');
    }
}
