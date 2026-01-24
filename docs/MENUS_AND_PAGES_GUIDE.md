# Menus and Pages Guide

This guide explains how to add menus and pages to Monolith Firewall, both in the core WebUI and in package modules.

## Table of Contents

1. [Adding Core Pages](#adding-core-pages)
2. [Adding Core Menu Items](#adding-core-menu-items)
3. [Adding Package Pages](#adding-package-pages)
4. [Adding Package Menu Items](#adding-package-menu-items)
5. [Menu Structure](#menu-structure)
6. [Page Structure](#page-structure)
7. [JavaScript Modules](#javascript-modules)
8. [CSS Styling](#css-styling)

---

## Adding Core Pages

Core pages are pages that are part of the main WebUI application, not from packages.

### Step 1: Create the Razor Page

Create a Razor Page in `src/Monolith.FireWall.WebUI/Pages/`:

**Example: `src/Monolith.FireWall.WebUI/Pages/Status/States.cshtml`**

```cshtml
@page "/status/states"
@{
    Layout = null;
    ViewData["Title"] = "Firewall States";
}

<div id="status-container"></div>

@section Scripts {
    <script src="/js/pages/status.js" data-module-js="status"></script>
}
```

**Key Points:**
- Use `@page "/your/route/path"` to define the route
- Set `Layout = null;` for SPA partial pages
- Add a container div with an ID for JavaScript to target
- Include JavaScript files in `@section Scripts`

### Step 2: Add Route to routes.json

Add the route definition to `src/Monolith.FireWall.WebUI/wwwroot/page/routes.json`:

```json
{
  "id": "status.states",
  "path": "/status/states",
  "title": "Firewall States",
  "kind": "internal",
  "requiresAuth": true,
  "shell": "<div id=\"status-container\"></div>",
  "assets": {
    "js": ["status"],
    "css": ["status"]
  }
}
```

**Fields:**
- `id`: Unique route identifier (use dot notation: `group.page`)
- `path`: URL path (must match `@page` directive)
- `title`: Page title
- `kind`: `"internal"` for app pages, `"login"` for login page
- `requiresAuth`: `true` if authentication required
- `shell`: HTML container (usually matches the div in the Razor page)
- `assets.js`: Array of JavaScript module names (without `.js` extension)
- `assets.css`: Array of CSS file names (without `.css` extension)

### Step 3: Create JavaScript Module

Create a JavaScript module in `src/Monolith.FireWall.WebUI/wwwroot/js/pages/`:

**Example: `src/Monolith.FireWall.WebUI/wwwroot/js/pages/status.js`**

```javascript
var Status = {
    init: function() {
        console.log('Initializing Status module...');
        if (window.location.pathname.startsWith('/status/')) {
            this.renderPage();
        }
    },

    renderPage: function() {
        const path = window.location.pathname || '';
        if (path.startsWith('/status/states')) {
            this.renderStates();
        } else if (path.startsWith('/status/routing-status')) {
            this.renderRoutingStatus();
        }
    },

    renderStates: function() {
        const container = $('#status-container, #page-content').first();
        if (!container.length) return;

        container.html('<h2>Firewall States</h2>');
        // Your rendering logic here
    }
};

// Register module
(function() {
    if (typeof Monolith === 'undefined') {
        window.Monolith = {};
    }
    if (typeof Monolith.Pages === 'undefined') {
        Monolith.Pages = {};
    }
    Monolith.Pages.Status = Status;
    Monolith.Pages.status = Status; // Also register lowercase
})();
```

**Key Points:**
- Module name should match the route ID (e.g., `Status` for `status.states`)
- Register with both PascalCase and lowercase for router compatibility
- Use `renderPage()` to determine which sub-page to render
- Target containers using jQuery selectors

### Step 4: Update Module Name Mapping (if needed)

If your route doesn't follow the standard pattern, update `UiManifestBuilder.GetInternalModuleName()`:

**File: `src/Monolith.FireWall.WebUI/Services/UiManifestBuilder.cs`**

```csharp
private static string GetInternalModuleName(string route)
{
    if (route.StartsWith("/status/routing-status", StringComparison.OrdinalIgnoreCase))
    {
        return "routing-status";
    }
    
    if (route.StartsWith("/status/", StringComparison.OrdinalIgnoreCase))
    {
        return "status";
    }
    
    // ... other routes
}
```

---

## Adding Core Menu Items

Menu items are defined in `routes.json` and automatically rendered by the menu system.

### Step 1: Add Menu Structure to routes.json

Add menu items to the `menu` array in `src/Monolith.FireWall.WebUI/wwwroot/page/routes.json`:

```json
{
  "menu": [
    {
      "label": "Status",
      "children": [
        {
          "label": "States",
          "routeId": "status.states",
          "icon": "fa-solid fa-network-wired"
        },
        {
          "label": "Routing Status",
          "routeId": "status.routing-status",
          "icon": "fa-solid fa-route"
        }
      ]
    }
  ]
}
```

**Fields:**
- `label`: Menu item text
- `routeId`: Must match the `id` in the routes array
- `icon`: FontAwesome icon class (e.g., `"fa-solid fa-shield-halved"`)
- `children`: Array of sub-menu items (for nested menus)

### Step 2: Ensure Menu Container Exists

The menu system looks for containers with specific IDs in `App.cshtml`:

```html
<ul class="dropdown-menu" id="menu-status">
    <li><span class="dropdown-item-text text-muted small">Loading...</span></li>
</ul>
```

**Container IDs:**
- `#menu-system` - System menu
- `#interfaces-menu` - Interfaces menu
- `#menu-firewall` - Firewall menu
- `#menu-status` - Status menu
- `#packages-menu` - Packages menu (dynamic)

### Step 3: Menu Rendering

The menu system (`menu.js`) automatically:
1. Loads menu data from `/api/cms/menu.json`
2. Resolves paths from `routeId` to actual routes
3. Renders menu items into the appropriate containers
4. Handles click events and navigation

**No additional code needed!** The menu system handles everything automatically.

---

## Adding Package Pages

Package pages are Razor Pages in Razor Class Libraries (RCLs) that are dynamically loaded.

### Step 1: Create Razor Page in Package

Create a Razor Page in your package's RCL:

**Example: `monolithfirewall-packages/monolith-network/Pages/DHCP/Config.cshtml`**

```cshtml
@page "/p/monolith-network/dhcp/config"
@{
    Layout = "~/Pages/Shared/_Layout.cshtml";
    ViewData["Title"] = "DHCP Configuration";
}

<div class="container-fluid p-4">
    <h2>DHCP Configuration</h2>
    <!-- Your page content here -->
</div>

@section Scripts {
    <script src="/js/pages/dhcp.js" data-module-js="dhcp"></script>
}

@section Styles {
    <link rel="stylesheet" href="/css/dhcp.css" data-module-css="dhcp" />
}
```

**Key Points:**
- Use `@page "/p/{package-id}/{module-id}/{page-name}"` pattern
- Set `Layout = "~/Pages/Shared/_Layout.cshtml"` to use shared layout
- Use `@section Scripts` and `@section Styles` for assets

### Step 2: Implement Module Interface

In your package's module class, implement `IMonolithModule` and provide page definitions:

**Example: `monolithfirewall-packages/monolith-network/Modules/DHCPModule.cs`**

```csharp
using Monolith.FireWall.Common.Interfaces;
using Monolith.FireWall.Common.Models;

public class DHCPModule : IMonolithModule
{
    public string Id => "dhcp";
    public string Name => "DHCP Server";
    public string PackageId => "monolith-network";

    public IEnumerable<PageDefinition> GetPages()
    {
        yield return new PageDefinition
        {
            Id = "dhcp.config",
            Path = "/p/monolith-network/dhcp/config",
            Title = "DHCP Configuration",
            ModuleId = this.Id,
            PackageId = this.PackageId
        };
    }

    public IEnumerable<MenuDefinition> GetMenuItems()
    {
        yield return new MenuDefinition
        {
            Label = "DHCP Server",
            RouteId = "p.monolith-network.dhcp.config",
            Icon = "network",
            ParentMenu = "Packages"
        };
    }

    // ... other interface methods
}
```

**Key Points:**
- `GetPages()` returns page definitions that are automatically discovered
- `GetMenuItems()` returns menu items that appear in the Packages menu
- Route IDs should use `p.{package-id}.{module-id}.{page-id}` format

### Step 3: Package Menu Integration

Package menu items are automatically:
1. Discovered via `GetMenuItems()` from all loaded modules
2. Grouped by package in the Packages menu
3. Rendered with proper routing and icons

**No additional configuration needed!**

---

## Adding Package Menu Items

Package menu items are added via the `GetMenuItems()` method in your module.

### Menu Definition Structure

```csharp
yield return new MenuDefinition
{
    Label = "DHCP Server",                    // Display text
    RouteId = "p.monolith-network.dhcp.config", // Must match page route ID
    Icon = "network",                          // Icon name (see icon mapping below)
    ParentMenu = "Packages"                    // Always "Packages" for package menus
};
```

### Icon Mapping

Icons are automatically mapped to FontAwesome classes. Common mappings:

- `"network"` → `fa-solid fa-network-wired`
- `"shield"` → `fa-solid fa-shield-halved`
- `"router"` → `fa-solid fa-route`
- `"package"` → `fa-solid fa-box-open`
- `"settings"` → `fa-solid fa-gear`
- `"activity"` → `fa-solid fa-chart-line`

You can also use full FontAwesome classes:
- `"fa-solid fa-shield-halved"` (full class)

### Nested Package Menus

Package menus automatically support nested submenus:

```csharp
yield return new MenuDefinition
{
    Label = "Monolith Network",
    Icon = "network",
    ParentMenu = "Packages",
    Children = new[]
    {
        new MenuDefinition
        {
            Label = "DHCP Server",
            RouteId = "p.monolith-network.dhcp.config",
            Icon = "network"
        },
        new MenuDefinition
        {
            Label = "DNS Server",
            RouteId = "p.monolith-network.dns.config",
            Icon = "network"
        }
    }
};
```

---

## Menu Structure

### Core Menu Groups

Core menus are defined in `routes.json`:

```json
{
  "menu": [
    {
      "label": "Status",
      "children": [
        {
          "label": "States",
          "routeId": "status.states",
          "icon": "fa-solid fa-network-wired"
        }
      ]
    },
    {
      "label": "Firewall",
      "children": [
        {
          "label": "Rules",
          "routeId": "firewall.rules",
          "icon": "fa-solid fa-shield-halved"
        }
      ]
    }
  ]
}
```

### Package Menu Groups

Package menus are automatically built from module `GetMenuItems()` calls and appear under the "Packages" menu.

### Menu Rendering Flow

1. **Backend (`UiManifestBuilder.cs`)**:
   - Loads base menu from `routes.json`
   - Merges package menus from loaded modules
   - Resolves all menu item paths from route IDs
   - Returns complete menu via `/api/cms/menu.json`

2. **Frontend (`menu.js`)**:
   - Fetches menu data from `/api/cms/menu.json`
   - Renders menu items into appropriate containers
   - Handles click events and navigation
   - Updates active states

---

## Page Structure

### Core Page Template

```cshtml
@page "/your/route/path"
@{
    Layout = null;
    ViewData["Title"] = "Page Title";
}

<div id="page-container"></div>

@section Scripts {
    <script src="/js/pages/your-module.js" data-module-js="your-module"></script>
}
```

### Package Page Template

```cshtml
@page "/p/{package-id}/{module-id}/{page-name}"
@{
    Layout = "~/Pages/Shared/_Layout.cshtml";
    ViewData["Title"] = "Page Title";
}

<div class="container-fluid p-4">
    <h2>Page Title</h2>
    <!-- Your content here -->
</div>

@section Scripts {
    <script src="/js/pages/your-module.js" data-module-js="your-module"></script>
}

@section Styles {
    <link rel="stylesheet" href="/css/your-styles.css" data-module-css="your-module" />
}
```

---

## JavaScript Modules

### Module Structure

```javascript
var YourModule = {
    init: function() {
        console.log('Initializing YourModule...');
        if (window.location.pathname.startsWith('/your/route')) {
            this.renderPage();
        }
    },

    renderPage: function() {
        const container = $('#page-container, #page-content').first();
        if (!container.length) return;

        // Your rendering logic
        container.html('<h2>Your Page</h2>');
    }
};

// Register module
(function() {
    if (typeof Monolith === 'undefined') {
        window.Monolith = {};
    }
    if (typeof Monolith.Pages === 'undefined') {
        Monolith.Pages = {};
    }
    Monolith.Pages.YourModule = YourModule;
    Monolith.Pages['your-module'] = YourModule; // Lowercase for router
})();
```

### Module Registration

Modules must be registered in the global `Monolith.Pages` object:
- **PascalCase**: `Monolith.Pages.YourModule` (for direct access)
- **lowercase/kebab-case**: `Monolith.Pages['your-module']` (for router lookup)

### Router Integration

The router (`cms-router.js`) automatically:
1. Loads JavaScript modules based on route `assets.js`
2. Looks up modules by name (tries multiple variations)
3. Calls `init()` or `renderPage()` on the module
4. Handles cleanup when navigating away

---

## CSS Styling

### Core Page Styles

Create CSS files in `src/Monolith.FireWall.WebUI/wwwroot/css/`:

**Example: `src/Monolith.FireWall.WebUI/wwwroot/css/status.css`**

```css
/* Status Pages Styles */

#status-container {
    position: relative;
    z-index: 1;
}

/* Your styles here */
```

### Package Page Styles

Package styles can be:
1. **Included in the package RCL** (`wwwroot/css/`)
2. **Referenced in the Razor page** via `@section Styles`
3. **Loaded dynamically** via route `assets.css`

### Theme Support

Use CSS variables for theme support:

```css
.my-element {
    background-color: var(--bs-light, #f8f9fa);
    color: var(--text, #1e293b);
}

[data-bs-theme="dark"] .my-element {
    background-color: #1e293b;
    color: #f1f5f9;
}
```

---

## Best Practices

### 1. Route Naming

- Use dot notation for route IDs: `group.page` or `p.package.module.page`
- Keep paths consistent with route IDs
- Use kebab-case for paths: `/status/routing-status`

### 2. Module Naming

- Match module name to route ID (e.g., `status.states` → `Status` module)
- Register with both PascalCase and lowercase
- Use descriptive, consistent names

### 3. Menu Organization

- Group related pages under the same menu
- Use consistent icons across related pages
- Keep menu hierarchies shallow (max 2-3 levels)

### 4. JavaScript Modules

- Always check for container existence before rendering
- Use jQuery for DOM manipulation (project standard)
- Register modules immediately (not on DOM ready)
- Handle errors gracefully

### 5. Package Integration

- Always implement `GetPages()` and `GetMenuItems()` in modules
- Use consistent route ID patterns: `p.{package}.{module}.{page}`
- Test package pages in isolation before integration

---

## Troubleshooting

### Menu Items Not Appearing

1. Check that `routeId` in menu matches route `id` in `routes.json`
2. Verify menu container exists in `App.cshtml`
3. Check browser console for menu loading errors
4. Verify `/api/cms/menu.json` returns your menu items

### Pages Not Loading

1. Verify `@page` directive matches route path
2. Check JavaScript module is registered correctly
3. Verify route exists in `routes.json`
4. Check browser console for module loading errors
5. Verify module name mapping in `UiManifestBuilder.GetInternalModuleName()`

### Package Pages Not Found

1. Verify Razor Page has correct `@page` directive
2. Check module implements `GetPages()` correctly
3. Verify package is loaded (check Core logs)
4. Check route ID format: `p.{package}.{module}.{page}`

### JavaScript Module Not Found

1. Verify module is registered in `Monolith.Pages`
2. Check module name matches route `assets.js` entry
3. Verify file is loaded (check Network tab)
4. Check for JavaScript syntax errors

---

## Examples

### Complete Core Page Example

**1. Razor Page** (`Pages/Example/Index.cshtml`):
```cshtml
@page "/example"
@{
    Layout = null;
    ViewData["Title"] = "Example Page";
}

<div id="example-container"></div>

@section Scripts {
    <script src="/js/pages/example.js" data-module-js="example"></script>
}
```

**2. Route** (`routes.json`):
```json
{
  "id": "example",
  "path": "/example",
  "title": "Example Page",
  "kind": "internal",
  "requiresAuth": true,
  "shell": "<div id=\"example-container\"></div>",
  "assets": {
    "js": ["example"]
  }
}
```

**3. JavaScript** (`wwwroot/js/pages/example.js`):
```javascript
var Example = {
    init: function() {
        this.renderPage();
    },
    renderPage: function() {
        $('#example-container').html('<h2>Example Page</h2>');
    }
};

Monolith.Pages = Monolith.Pages || {};
Monolith.Pages.Example = Example;
Monolith.Pages.example = Example;
```

**4. Menu Item** (`routes.json` menu array):
```json
{
  "label": "Example",
  "routeId": "example",
  "icon": "fa-solid fa-circle-info"
}
```

### Complete Package Page Example

**1. Razor Page** (`monolithfirewall-packages/my-package/Pages/MyPage.cshtml`):
```cshtml
@page "/p/my-package/mymodule/page"
@{
    Layout = "~/Pages/Shared/_Layout.cshtml";
    ViewData["Title"] = "My Page";
}

<div class="container-fluid p-4">
    <h2>My Page</h2>
</div>
```

**2. Module** (`my-package/Modules/MyModule.cs`):
```csharp
public IEnumerable<PageDefinition> GetPages()
{
    yield return new PageDefinition
    {
        Id = "mymodule.page",
        Path = "/p/my-package/mymodule/page",
        Title = "My Page",
        ModuleId = "mymodule",
        PackageId = "my-package"
    };
}

public IEnumerable<MenuDefinition> GetMenuItems()
{
    yield return new MenuDefinition
    {
        Label = "My Page",
        RouteId = "p.my-package.mymodule.page",
        Icon = "fa-solid fa-circle-info",
        ParentMenu = "Packages"
    };
}
```

---

## Additional Resources

- **Razor Pages Guide**: `docs/RAZOR_PAGES_USAGE_GUIDE.md`
- **Package Pages Guide**: `docs/PACKAGE_PAGES_GUIDE.md`
- **Module Interface**: `src/Monolith.FireWall.Common/Interfaces/IMonolithModule.cs`
- **Menu System**: `src/Monolith.FireWall.WebUI/wwwroot/js/core/menu.js`
- **Route Builder**: `src/Monolith.FireWall.WebUI/Services/UiManifestBuilder.cs`
