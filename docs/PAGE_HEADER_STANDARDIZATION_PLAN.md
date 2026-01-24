# Page Header Standardization Plan

## Overview
Create a standardized, reusable page header component that can be used across all pages with automatic breadcrumb generation and flexible configuration options.

## Goals
1. **Standardize Layout**: Consistent page headers across all pages
2. **Auto-Generate Breadcrumbs**: Automatically build breadcrumb navigation from route hierarchy
3. **Flexible Configuration**: Support title, icon, description, and custom breadcrumbs
4. **Easy Integration**: Simple API for pages to use
5. **Optional Description**: Support for subtitle/description under the title

## Architecture

### New Module: `page-header.js`
Location: `src/Monolith.FireWall.WebUI/wwwroot/js/core/page-header.js`

### API Design

```javascript
Monolith.PageHeader = {
    /**
     * Render a page header
     * @param {Object} options - Configuration options
     * @param {string} options.title - Page title (required)
     * @param {string} [options.icon] - FontAwesome icon class (e.g., "fa-network-wired")
     * @param {string} [options.description] - Optional description/subtitle below title
     * @param {Array} [options.breadcrumbs] - Custom breadcrumbs array. If not provided, auto-generates from route
     * @param {string} [options.container] - Container selector (default: "#page-content")
     * @param {boolean} [options.prepend] - Prepend to container instead of replacing (default: false)
     */
    render: function(options) { ... },
    
    /**
     * Auto-generate breadcrumbs from current route
     * @param {string} [path] - Route path (default: current pathname)
     * @returns {Array} Breadcrumb items
     */
    generateBreadcrumbs: function(path) { ... },
    
    /**
     * Get route information for breadcrumb generation
     * @param {string} path - Route path
     * @returns {Object} Route info with title, path, etc.
     */
    getRouteInfo: function(path) { ... }
};
```

### Usage Examples

#### Basic Usage (Auto-generated breadcrumbs)
```javascript
Monolith.PageHeader.render({
    title: "Firewall States",
    icon: "fa-network-wired",
    description: "View and manage active firewall connections"
});
```

#### With Custom Breadcrumbs
```javascript
Monolith.PageHeader.render({
    title: "Custom Page",
    icon: "fa-shield-halved",
    breadcrumbs: [
        { label: "Dashboard", path: "/dashboard", icon: "fa-home" },
        { label: "Security", path: "/security" },
        { label: "Custom Page" } // Current page (no link)
    ]
});
```

#### Without Description
```javascript
Monolith.PageHeader.render({
    title: "Settings",
    icon: "fa-gear"
});
```

## Implementation Details

### 1. Auto-Generate Breadcrumbs

**Logic:**
- Parse current route path (e.g., `/status/routing-status`)
- Split path into segments: `["", "status", "routing-status"]`
- For each segment:
  - Look up route in `Monolith.CmsRouter.routesByPath`
  - If route found, use its `title` and `path`
  - If not found, use segment name (capitalized)
- Build breadcrumb chain: Dashboard → Segment1 → Segment2 → Current

**Example:**
- Path: `/status/routing-status`
- Segments: `["", "status", "routing-status"]`
- Breadcrumbs:
  1. Dashboard (`/dashboard`)
  2. Status (`/status/states` or `/status/routing-status` - use first available)
  3. Routing Status (current, no link)

### 2. Route Hierarchy Detection

**Strategy:**
- Check if parent routes exist in router
- For `/status/routing-status`:
  - Check `/status` exists → add to breadcrumbs
  - Check `/status/states` exists → could be alternative
  - Use first available parent route

### 3. Icon Resolution

**Priority:**
1. Explicitly provided icon
2. Route metadata icon (if available)
3. Default icon based on route category:
   - `/status/*` → `fa-chart-line`
   - `/firewall/*` → `fa-shield-halved`
   - `/system/*` → `fa-gear`
   - `/interfaces/*` → `fa-network-wired`
   - Default → `fa-circle-dot`

### 4. HTML Structure

```html
<nav class="page-header navbar navbar-expand-lg">
    <div class="container-fluid">
        <div class="page-header-title">
            <h1 class="page-title">
                <span class="page-icon">
                    <i class="fas fa-{icon}"></i>
                </span>
                <span class="title-text">
                    <span class="module-title">{title}</span>
                    <!-- Optional description -->
                    <span class="page-subtitle">{description}</span>
                </span>
            </h1>
        </div>
        <div class="page-header-breadcrumb">
            {breadcrumb items}
        </div>
    </div>
</nav>
```

### 5. Breadcrumb Item Structure

```javascript
{
    label: "Dashboard",      // Display text
    path: "/dashboard",      // Route path (null for current page)
    icon: "fa-home"         // Optional icon
}
```

## Integration Points

### 1. Router Integration
- Access `Monolith.CmsRouter.routesByPath` for route lookup
- Access `Monolith.CmsRouter.routesById` for route metadata

### 2. Menu Integration
- Optionally use menu structure to determine parent relationships
- Use menu labels for breadcrumb text

### 3. Page Module Integration
- Pages call `Monolith.PageHeader.render()` in their `renderPage()` method
- Can be called before or after page content

## Migration Path

### Phase 1: Create Module
1. Create `page-header.js` in `js/core/`
2. Implement basic rendering
3. Implement auto-breadcrumb generation
4. Add to `_Layout.cshtml` script includes

### Phase 2: Update Existing Pages
1. Update `status.js` to use `Monolith.PageHeader.render()`
2. Update `routing-status.js` to use `Monolith.PageHeader.render()`
3. Test both pages

### Phase 3: Rollout to Other Pages
1. Update dashboard (if needed)
2. Update users, groups, permissions pages
3. Update firewall pages
4. Update system pages

### Phase 4: Package Page Support
1. Add support for package pages (`/p/{package}/{module}/{page}`)
2. Auto-generate breadcrumbs: Dashboard → Packages → Package Name → Module → Page

## Configuration Options

### Option 1: Simple (Title Only)
```javascript
Monolith.PageHeader.render({
    title: "Page Title"
});
```

### Option 2: With Icon
```javascript
Monolith.PageHeader.render({
    title: "Page Title",
    icon: "fa-shield-halved"
});
```

### Option 3: With Description
```javascript
Monolith.PageHeader.render({
    title: "Page Title",
    icon: "fa-shield-halved",
    description: "Manage firewall rules and configurations"
});
```

### Option 4: Custom Breadcrumbs
```javascript
Monolith.PageHeader.render({
    title: "Page Title",
    icon: "fa-shield-halved",
    description: "Manage firewall rules",
    breadcrumbs: [
        { label: "Dashboard", path: "/dashboard", icon: "fa-home" },
        { label: "Security", path: "/security" },
        { label: "Page Title" }
    ]
});
```

## CSS Considerations

### Existing Styles
- `package-header.css` already has styles for `.page-header`
- May need minor adjustments for description/subtitle

### New Styles Needed
- `.page-subtitle` styling (if not already present)
- Responsive adjustments for description

## Benefits

1. **Consistency**: All pages use same header structure
2. **Maintainability**: Single source of truth for header rendering
3. **Flexibility**: Easy to customize per page
4. **Auto-Generation**: Less code duplication
5. **Future-Proof**: Easy to add new features (badges, actions, etc.)

## Implementation Checklist

- [ ] Create `js/core/page-header.js`
- [ ] Implement `render()` method
- [ ] Implement `generateBreadcrumbs()` method
- [ ] Implement `getRouteInfo()` method
- [ ] Add navigation click handlers
- [ ] Test with existing pages (status, routing-status)
- [ ] Update status.js to use new module
- [ ] Update routing-status.js to use new module
- [ ] Test auto-breadcrumb generation
- [ ] Test custom breadcrumbs
- [ ] Test with description option
- [ ] Update CSS if needed
- [ ] Document usage in MENUS_AND_PAGES_GUIDE.md
- [ ] Rollout to other pages (optional, can be done incrementally)

## Future Enhancements

1. **Action Buttons**: Add action buttons to header (e.g., "Add", "Refresh")
2. **Badges**: Support for badges/notifications in header
3. **Tabs**: Support for tabs in header (for sub-pages)
4. **Breadcrumb Dropdowns**: For long breadcrumb chains
5. **Context Menu**: Right-click menu on header

## Example: Before and After

### Before (Current)
```javascript
// In status.js
container.html(`
    <nav class="page-header navbar navbar-expand-lg">
        <div class="container-fluid">
            <div class="page-header-title">
                <h1 class="page-title">
                    <span class="page-icon">
                        <i class="fas fa-network-wired"></i>
                    </span>
                    <span class="title-text">
                        <span class="module-title">Firewall States</span>
                    </span>
                </h1>
            </div>
            <div class="page-header-breadcrumb">
                <a href="#" class="breadcrumb-link" data-route="/dashboard">...</a>
                ...
            </div>
        </div>
    </nav>
    ...
`);
```

### After (Standardized)
```javascript
// In status.js
Monolith.PageHeader.render({
    title: "Firewall States",
    icon: "fa-network-wired",
    description: "View and manage active firewall connections"
});

// Then render page content
container.html(`...page content...`);
```

## Notes

- Page header should be rendered before page content
- Container selector should be configurable (default: `#page-content`)
- Should support prepending (for pages that already have content)
- Navigation handlers should be attached automatically
- Should work with both SPA navigation and direct page loads
