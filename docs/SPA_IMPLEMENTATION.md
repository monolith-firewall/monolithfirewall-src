# Bootstrap 5.3.8 + jQuery SPA Implementation

**Complete implementation guide for Bootstrap 5.3.8, jQuery 3.7.1, and SPA setup with no-caching**

---

## Overview

The WebUI uses:
- **Bootstrap 5.3.8** - CSS framework
- **jQuery 3.7.1** - JavaScript library
- **SPA (Single Page Application)** - Hash-based routing
- **No-caching headers** - Prevent caching issues

---

## 1. Download Dependencies

### Bootstrap 5.3.8
```bash
cd /home/mlf/monolith-firewall/src/Monolith.FireWall.WebUI/wwwroot/css
wget https://cdn.jsdelivr.net/npm/bootstrap@5.3.8/dist/css/bootstrap.min.css -O bootstrap.min.css
wget https://cdn.jsdelivr.net/npm/bootstrap@5.3.8/dist/css/bootstrap.min.css.map -O bootstrap.min.css.map
```

### Bootstrap JS 5.3.8
```bash
cd /home/mlf/monolith-firewall/src/Monolith.FireWall.WebUI/wwwroot/js
wget https://cdn.jsdelivr.net/npm/bootstrap@5.3.8/dist/js/bootstrap.bundle.min.js -O bootstrap.bundle.min.js
wget https://cdn.jsdelivr.net/npm/bootstrap@5.3.8/dist/js/bootstrap.bundle.min.js.map -O bootstrap.bundle.min.js.map
```

### jQuery 3.7.1
```bash
cd /home/mlf/monolith-firewall/src/Monolith.FireWall.WebUI/wwwroot/js
wget https://code.jquery.com/jquery-3.7.1.min.js -O jquery.min.js
```

---

## 2. No-Caching Middleware

### File: `src/Monolith.FireWall.WebUI/Middleware/NoCacheMiddleware.cs`

```csharp
namespace Monolith.FireWall.WebUI.Middleware;

/// <summary>
/// Middleware to add no-cache headers to all responses
/// </summary>
public class NoCacheMiddleware
{
    private readonly RequestDelegate _next;

    public NoCacheMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Add no-cache headers
        context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
        context.Response.Headers.Append("Pragma", "no-cache");
        context.Response.Headers.Append("Expires", "0");

        await _next(context);
    }
}

public static class NoCacheMiddlewareExtensions
{
    public static IApplicationBuilder UseNoCache(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<NoCacheMiddleware>();
    }
}
```

### Usage in Program.cs

```csharp
var app = builder.Build();

// Add no-cache middleware FIRST (before static files)
app.UseNoCache();

// Then static files
app.UseStaticFiles();
```

---

## 3. SPA Layout

### File: `wwwroot/index.html`

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>MonolithFireWall</title>
    
    <!-- Bootstrap 5.3.8 CSS -->
    <link href="~/css/bootstrap.min.css" rel="stylesheet" />
    
    <!-- Custom CSS -->
    <link href="~/css/app.css" rel="stylesheet" />
</head>
<body>
    <!-- SPA Container -->
    <div id="app">
        <div id="page-content">
            <!-- Content loaded here -->
        </div>
    </div>

    <!-- jQuery 3.7.1 -->
    <script src="~/js/jquery.min.js"></script>
    
    <!-- Bootstrap 5.3.8 JS -->
    <script src="~/js/bootstrap.bundle.min.js"></script>
    
    <!-- SPA Router -->
    <script src="~/js/core/monolith.router.js"></script>
    <script src="~/js/core/monolith.api.js"></script>
    <script src="~/js/core/monolith.auth.js"></script>
    <script src="~/js/core/monolith.menu.js"></script>
    <script src="~/js/app.js"></script>
</body>
</html>
```

---

## 4. SPA Router

### File: `wwwroot/js/core/monolith.router.js`

```javascript
/**
 * MonolithFireWall SPA Router
 * Hash-based routing for single-page application
 */
const Monolith = Monolith || {};

Monolith.Router = {
    routes: {},
    currentRoute: null,

    /**
     * Initialize router
     */
    init: function() {
        // Load routes from Core
        this.loadRoutes();
        
        // Listen for hash changes
        $(window).on('hashchange', () => this.navigate());
        
        // Initial navigation
        this.navigate();
    },

    /**
     * Load routes from Core
     */
    loadRoutes: async function() {
        try {
            const response = await Monolith.API.get('/core/pages');
            if (response.success && response.data) {
                response.data.forEach(page => {
                    this.routes[page.route.toLowerCase()] = page;
                });
            }
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
        } else {
            // Default to dashboard
            window.location.hash = '#/dashboard';
        }
    },

    /**
     * Load page
     */
    loadPage: async function(pageDef) {
        try {
            // Check permissions
            if (!Monolith.Auth.hasPermission(pageDef.requiredPermissions)) {
                Monolith.Toast.error('Access denied');
                window.location.hash = '#/dashboard';
                return;
            }

            // Load page content
            const response = await fetch(`/p/${pageDef.package}/${pageDef.module}/${pageDef.page}`);
            if (response.ok) {
                const html = await response.text();
                $('#page-content').html(html);
                
                // Load page-specific CSS
                await this.loadPageCSS(pageDef);
                
                // Load page-specific JS
                await this.loadPageJS(pageDef);
                
                this.currentRoute = pageDef.route;
            } else {
                Monolith.Toast.error('Page not found');
            }
        } catch (error) {
            console.error('Error loading page:', error);
            Monolith.Toast.error('Error loading page');
        }
    },

    /**
     * Load page CSS
     */
    loadPageCSS: async function(pageDef) {
        const cssUrl = `/p/${pageDef.package}/${pageDef.module}/${pageDef.page}?css`;
        try {
            const response = await fetch(cssUrl);
            if (response.ok) {
                const css = await response.text();
                const styleId = `page-css-${pageDef.package}-${pageDef.module}`;
                let styleEl = document.getElementById(styleId);
                if (!styleEl) {
                    styleEl = document.createElement('style');
                    styleEl.id = styleId;
                    document.head.appendChild(styleEl);
                }
                styleEl.textContent = css;
            }
        } catch (error) {
            // CSS is optional
        }
    },

    /**
     * Load page JS
     */
    loadPageJS: async function(pageDef) {
        const jsUrl = `/p/${pageDef.package}/${pageDef.module}/${pageDef.page}?js`;
        try {
            const response = await fetch(jsUrl);
            if (response.ok) {
                const js = await response.text();
                const scriptId = `page-js-${pageDef.package}-${pageDef.module}`;
                let scriptEl = document.getElementById(scriptId);
                if (scriptEl) {
                    scriptEl.remove();
                }
                scriptEl = document.createElement('script');
                scriptEl.id = scriptId;
                scriptEl.textContent = js;
                document.body.appendChild(scriptEl);
            }
        } catch (error) {
            // JS is optional
        }
    }
};
```

---

## 5. API Client

### File: `wwwroot/js/core/monolith.api.js`

```javascript
/**
 * MonolithFireWall API Client
 */
Monolith.API = {
    baseUrl: '/api',

    /**
     * GET request
     */
    get: async function(endpoint) {
        try {
            const response = await fetch(`${this.baseUrl}${endpoint}`, {
                method: 'GET',
                headers: {
                    'Content-Type': 'application/json'
                },
                cache: 'no-cache' // Prevent caching
            });
            
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }
            
            return await response.json();
        } catch (error) {
            console.error('API GET error:', error);
            throw error;
        }
    },

    /**
     * POST request
     */
    post: async function(endpoint, data) {
        try {
            const response = await fetch(`${this.baseUrl}${endpoint}`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(data),
                cache: 'no-cache'
            });
            
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }
            
            return await response.json();
        } catch (error) {
            console.error('API POST error:', error);
            throw error;
        }
    },

    /**
     * PUT request
     */
    put: async function(endpoint, data) {
        try {
            const response = await fetch(`${this.baseUrl}${endpoint}`, {
                method: 'PUT',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(data),
                cache: 'no-cache'
            });
            
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }
            
            return await response.json();
        } catch (error) {
            console.error('API PUT error:', error);
            throw error;
        }
    },

    /**
     * DELETE request
     */
    delete: async function(endpoint) {
        try {
            const response = await fetch(`${this.baseUrl}${endpoint}`, {
                method: 'DELETE',
                headers: {
                    'Content-Type': 'application/json'
                },
                cache: 'no-cache'
            });
            
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }
            
            return await response.json();
        } catch (error) {
            console.error('API DELETE error:', error);
            throw error;
        }
    }
};
```

---

## 6. Authentication

### File: `wwwroot/js/core/monolith.auth.js`

```javascript
/**
 * MonolithFireWall Authentication
 */
Monolith.Auth = {
    currentUser: null,

    /**
     * Initialize auth
     */
    init: async function() {
        await this.checkAuth();
    },

    /**
     * Check authentication status
     */
    checkAuth: async function() {
        try {
            const response = await Monolith.API.get('/user/current');
            if (response.success && response.data) {
                this.currentUser = response.data;
                return true;
            }
        } catch (error) {
            // Not authenticated
        }
        return false;
    },

    /**
     * Login
     */
    login: async function(username, password) {
        try {
            const response = await Monolith.API.post('/auth/login', {
                username: username,
                password: password
            });
            
            if (response.success) {
                this.currentUser = response.data.user;
                return true;
            }
            return false;
        } catch (error) {
            console.error('Login error:', error);
            return false;
        }
    },

    /**
     * Logout
     */
    logout: async function() {
        try {
            await Monolith.API.post('/auth/logout', {});
        } catch (error) {
            // Ignore errors
        }
        this.currentUser = null;
        window.location.hash = '#/login';
    },

    /**
     * Check permission
     */
    hasPermission: function(permissions) {
        if (!permissions || permissions.length === 0) {
            return true;
        }
        
        if (!this.currentUser) {
            return false;
        }
        
        const userPerms = this.currentUser.permissions || [];
        return permissions.some(perm => 
            userPerms.includes(perm) || 
            userPerms.includes('*')
        );
    }
};
```

---

## 7. Main App

### File: `wwwroot/js/app.js`

```javascript
/**
 * MonolithFireWall Main Application
 */
$(document).ready(function() {
    // Initialize authentication
    Monolith.Auth.init().then(isAuthenticated => {
        if (!isAuthenticated) {
            window.location.hash = '#/login';
        } else {
            // Initialize router
            Monolith.Router.init();
            
            // Build menu
            Monolith.Menu.build();
        }
    });
});
```

---

## 8. Bootstrap Styled Components

### Example: Login Page

```html
<div class="container mt-5">
    <div class="row justify-content-center">
        <div class="col-md-4">
            <div class="card">
                <div class="card-header">
                    <h4 class="mb-0">Login</h4>
                </div>
                <div class="card-body">
                    <form id="login-form">
                        <div class="mb-3">
                            <label for="username" class="form-label">Username</label>
                            <input type="text" class="form-control" id="username" required>
                        </div>
                        <div class="mb-3">
                            <label for="password" class="form-label">Password</label>
                            <input type="password" class="form-control" id="password" required>
                        </div>
                        <button type="submit" class="btn btn-primary w-100">Login</button>
                    </form>
                </div>
            </div>
        </div>
    </div>
</div>
```

### Example: Bootstrap Table

```html
<div class="container-fluid mt-3">
    <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
            <h5 class="mb-0">Users</h5>
            <button class="btn btn-sm btn-primary" onclick="addUser()">
                <i class="bi bi-plus"></i> Add User
            </button>
        </div>
        <div class="card-body">
            <table class="table table-striped">
                <thead>
                    <tr>
                        <th>ID</th>
                        <th>Username</th>
                        <th>Email</th>
                        <th>Roles</th>
                        <th>Actions</th>
                    </tr>
                </thead>
                <tbody id="users-table-body">
                    <!-- Loaded via JS -->
                </tbody>
            </table>
        </div>
    </div>
</div>
```

---

## 9. Program.cs Integration

```csharp
var app = builder.Build();

// No-cache middleware FIRST
app.UseNoCache();

// Static files
app.UseStaticFiles();

// Routing
app.UseRouting();

// Authentication
app.UseMiddleware<AuthenticationMiddleware>();

// Default route
app.MapGet("/", async context =>
{
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync("wwwroot/index.html");
});

app.Run("http://0.0.0.0:8080");
```

---

## 10. Package CSS/JS Loading

### In Package Razor View

```razor
@page "/p/network/dhcp/config"

<div class="container-fluid">
    <h1>DHCP Configuration</h1>
    <!-- Content -->
</div>

@section Scripts {
    <script>
        // jQuery code here
        $(document).ready(function() {
            // Load config
            Monolith.API.get('/packages/monolith-network/modules/network.dhcp/get-config')
                .then(response => {
                    if (response.success) {
                        // Populate form
                    }
                });
        });
    </script>
}
```

---

## Summary

✅ **Bootstrap 5.3.8** - CSS framework  
✅ **jQuery 3.7.1** - JavaScript library  
✅ **SPA Router** - Hash-based routing  
✅ **No-Caching** - Headers prevent caching  
✅ **API Client** - jQuery-based API calls  
✅ **Authentication** - Session management  

**Ready for Bootstrap/jQuery SPA!** 🚀
