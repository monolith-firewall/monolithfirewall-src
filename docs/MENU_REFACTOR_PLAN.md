# Menu System Refactor Plan

## Overview
Consolidate all menu-related code into a single, clean architecture with a dedicated JSON endpoint and JavaScript module.

## Current Problems
1. **Menu code scattered across multiple files:**
   - `app.js`: `renderInterfacesMenu()`, `renderPackagesMenu()`, `renderMonitoringStatusMenu()`, `renderNotificationsMenu()`
   - `cms-router.js`: `renderMenus()`, `buildMenuHtml()`, `buildMenuItem()`, `getMenuPath()`, `resolveIconClass()`
   - Package menu hover handlers in `cms-router.js`

2. **Hardcoded menu structure in `App.cshtml`:**
   - System, Interfaces, Firewall, Status, Packages dropdowns are hardcoded
   - Only placeholders for dynamic content

3. **Menu data sources mixed:**
   - Static menu from `routes.json`
   - Dynamic menu from `UiManifestBuilder.BuildMenuFromRoutes()`
   - Package menus from `UiManifestBuilder.BuildPackagesMenu()`
   - Core API menus from `MergeCoreMenusAsync()`
   - All returned via `/api/cms/menu` but as part of full manifest

4. **No single source of truth:**
   - Menu structure built in multiple places
   - Icons handled inconsistently
   - No unified menu JSON format

## Solution

### Phase 1: Create Unified Menu JSON Endpoint

**New Endpoint:** `/api/cms/menu.json`

**Returns:** Complete menu structure with all items, including:
- System menu items (from routes)
- Interfaces menu items (from routes + dynamic interfaces)
- Firewall menu items (from routes)
- Status menu items (from routes)
- Packages menu items (dynamically from Core API)
- Icons at all levels (group, item, submenu, sub-submenu)

**Menu JSON Structure:**
```json
{
  "success": true,
  "menu": [
    {
      "label": "System",
      "icon": "fa-solid fa-gear",
      "children": [
        {
          "label": "Settings",
          "routeId": "system.settings",
          "path": "/system/settings",
          "icon": "fa-solid fa-sliders"
        },
        {
          "label": "Packages",
          "routeId": "system.packages",
          "path": "/system/packages",
          "icon": "fa-solid fa-box-open",
          "children": [] // Support nested menus
        }
      ]
    },
    {
      "label": "Packages",
      "icon": "fa-solid fa-box-open",
      "children": [
        {
          "label": "monolith-network",
          "icon": "fa-solid fa-network-wired",
          "children": [
            {
              "label": "DHCP",
              "routeId": "p.monolith-network.dhcp",
              "path": "/p/monolith-network/dhcp",
              "icon": "fa-solid fa-server"
            }
          ]
        }
      ]
    }
  ]
}
```

**Implementation:**
- Create new `GetMenuJson()` method in `CmsController`
- Extract menu building logic from `UiManifestBuilder` into dedicated method
- Ensure all menu sources are merged:
  - Base menu from `routes.json`
  - Routes-based menu from `BuildMenuFromRoutes()`
  - Package menu from `BuildPackagesMenu()`
  - Core API menus from `MergeCoreMenusAsync()`
- Support icons at all levels (group, item, children)

### Phase 2: Create `menu.js` Module

**New File:** `src/Monolith.FireWall.WebUI/wwwroot/js/core/menu.js`

**Responsibilities:**
- Load menu from `/api/cms/menu.json`
- Render complete menu structure
- Handle nested dropdowns (dropend)
- Support icons at all levels
- Handle dynamic interfaces list
- Handle monitoring status menu
- Handle notifications menu
- Package menu hover handlers

**API:**
```javascript
Monolith.Menu = {
    init: async function() { },
    load: async function() { },
    render: function(menuData) { },
    renderMenuItem: function(item, level) { },
    renderInterfacesMenu: function(interfaces) { },
    renderMonitoringStatusMenu: function(monitors) { },
    renderNotificationsMenu: function(notifications, unreadCount) { },
    attachEventHandlers: function() { }
}
```

### Phase 3: Clean Up Existing Files

**`app.js`:**
- Remove: `renderInterfacesMenu()`, `renderPackagesMenu()`, `renderMonitoringStatusMenu()`, `renderNotificationsMenu()`
- Keep: `loadInterfaces()` (but call `Monolith.Menu.renderInterfacesMenu()`)
- Keep: `loadMonitoringStatus()`, `loadNotifications()` (but call `Monolith.Menu.render*()`)
- Remove menu initialization code (moved to `menu.js`)

**`cms-router.js`:**
- Remove: `renderMenus()`, `buildMenuHtml()`, `buildMenuItem()`, `getMenuPath()`, `resolveIconClass()`
- Remove: Package menu hover handlers (lines 113-166)
- Keep: Route navigation logic only

**`App.cshtml`:**
- Keep: Basic navbar structure
- Keep: Placeholder containers (`#menu-system`, `#interfaces-menu`, etc.)
- Remove: Hardcoded menu items (lines 67-68, 79-84)
- Keep: Help menu, Monitoring, Notifications, User menu (these are special)

### Phase 4: Update Menu Building Logic

**`UiManifestBuilder.cs`:**
- Create `BuildCompleteMenu()` method that:
  - Merges base menu from `routes.json`
  - Adds routes-based menu items
  - Adds package menu items
  - Adds Core API menu items
  - Ensures icons are preserved at all levels
- Update `BuildPackagesMenu()` to include icons
- Ensure `MergeCoreMenusAsync()` preserves icons from Core API

### Phase 5: Icon Support

**Requirements:**
- Menu groups can have icons (e.g., "System" → `fa-solid fa-gear`)
- Menu items can have icons (e.g., "Settings" → `fa-solid fa-sliders`)
- Submenu items can have icons
- Sub-submenu items can have icons
- Icons can come from:
  - `routes.json` (static)
  - Route metadata
  - Core API menu definitions
  - Default icons based on route path/type

**Icon Resolution:**
- Check `item.icon` or `item.Icon` first
- Fall back to route metadata
- Fall back to default icons based on route type/path
- Support FontAwesome classes (`fa-solid`, `fa-regular`, `fa-brands`)

## File Changes Summary

### New Files
- `src/Monolith.FireWall.WebUI/wwwroot/js/core/menu.js` - Complete menu management

### Modified Files
- `src/Monolith.FireWall.WebUI/Controllers/CmsController.cs` - Add `GetMenuJson()` endpoint
- `src/Monolith.FireWall.WebUI/Services/UiManifestBuilder.cs` - Extract menu building, ensure icons
- `src/Monolith.FireWall.WebUI/wwwroot/js/app.js` - Remove menu rendering code
- `src/Monolith.FireWall.WebUI/wwwroot/js/core/cms-router.js` - Remove menu rendering code
- `src/Monolith.FireWall.WebUI/Pages/App.cshtml` - Remove hardcoded menu items

### No Backwards Compatibility
- Remove all old menu code
- Remove `routes.json` menu definitions (or keep only as fallback)
- Remove old menu rendering functions
- Clean slate approach

## Implementation Order

1. Create `/api/cms/menu.json` endpoint with complete menu structure
2. Create `menu.js` module with all menu rendering logic
3. Update `App.cshtml` to remove hardcoded items
4. Clean up `app.js` and `cms-router.js`
5. Test and verify all menus work correctly
6. Remove old/unused menu code

## Testing Checklist

- [ ] System menu renders correctly
- [ ] Interfaces menu renders correctly (including dynamic interfaces)
- [ ] Firewall menu renders correctly
- [ ] Status menu renders correctly
- [ ] Packages menu renders correctly (with nested dropdowns)
- [ ] Icons display at all levels
- [ ] Nested menus (dropend) work correctly
- [ ] Menu navigation works (data-route attributes)
- [ ] Monitoring status menu works
- [ ] Notifications menu works
- [ ] Menu updates when packages are installed/uninstalled
