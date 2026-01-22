# WebUI Package-Agnostic Refactoring Plan

## Overview
Remove all hardcoded references to `monolith-network` package from the main WebUI codebase, making it fully package-agnostic. The WebUI should dynamically discover and interact with packages through generic APIs.

## Current Issues

### 1. Setup Wizard (`setup.js`)
- **Lines 440-469**: Hardcoded `monolith-network` DHCP/DNS configuration logic
- **Problem**: Direct API calls to specific package endpoints instead of generic package module APIs
- **Impact**: Setup wizard won't work for other packages with similar functionality

### 2. Setup Network Page (`Network.cshtml`)
- **Lines 46-57**: Hardcoded check for `monolith-network` package
- **Problem**: Shows warning if network package missing, but should work generically
- **Impact**: Setup flow is tied to specific package

### 3. Dashboard Widgets (`DashboardController.cs`)
- **Line 365**: Hardcoded `monolith-network` package ID mapping (`"network" -> "monolith-network"`)
- **Lines 429-432**: Hardcoded fallback for `network.dhcp.status` widget
- **Line 647**: Another hardcoded `monolith-network` reference
- **Problem**: Widget system should discover packages dynamically
- **Impact**: Widgets only work for hardcoded packages

### 4. Permissions (`permissions.js`)
- **Lines 54-57**: Hardcoded network permissions (`network.dhcp.read`, `network.dns.write`, etc.)
- **Problem**: Permissions should be discovered from installed packages
- **Impact**: Permissions UI doesn't reflect actual package permissions

### 5. Groups (`groups.js`)
- **Line 222**: Hardcoded network permission categories
- **Problem**: Permission categories should be dynamic
- **Impact**: Group permission management is incomplete

### 6. About Page (`about.js`)
- **Line 59**: Hardcoded mention of "DHCP and DNS server management"
- **Problem**: Features should be discovered from installed packages
- **Impact**: About page shows incorrect/outdated feature list

## Refactoring Strategy

### Phase 1: Dynamic Package Discovery
**Goal**: Create utilities to discover installed packages and their capabilities dynamically.

#### 1.1 Create Package Discovery Service
- **File**: `src/Monolith.FireWall.WebUI/Services/PackageDiscoveryService.cs`
- **Purpose**: Centralized service to query Core for installed packages, modules, permissions, and widgets
- **Methods**:
  ```csharp
  Task<List<PackageInfo>> GetInstalledPackagesAsync()
  Task<List<ModuleInfo>> GetPackageModulesAsync(string packageId)
  Task<List<PermissionDefinition>> GetPackagePermissionsAsync(string packageId)
  Task<List<WidgetDefinition>> GetPackageWidgetsAsync(string packageId)
  Task<bool> IsPackageInstalledAsync(string packageId)
  ```

#### 1.2 Create JavaScript Package Discovery Utility
- **File**: `src/Monolith.FireWall.WebUI/wwwroot/js/core/package-discovery.js`
- **Purpose**: Client-side utility to query package information
- **Methods**:
  ```javascript
  async getInstalledPackages()
  async getPackageModules(packageId)
  async isPackageInstalled(packageId)
  async findPackageByModule(moduleId) // e.g., find package that provides "dhcp" module
  ```

### Phase 2: Generic Package Module API
**Goal**: Replace hardcoded package-specific API calls with generic module APIs.

#### 2.1 Refactor Setup Wizard (`setup.js`)
- **Current**: Hardcoded `if (packageId === 'monolith-network' && pageId === 'dhcp')`
- **New**: Generic module API call pattern
  ```javascript
  // Instead of:
  if (packageId === 'monolith-network' && pageId === 'dhcp') { ... }
  
  // Use:
  const packageId = await Monolith.Packages.findPackageByModule('dhcp');
  await Monolith.API.post(`/api/packages/${packageId}/modules/dhcp/update-settings`, data);
  ```
- **Changes**:
  - Remove hardcoded `monolith-network` checks
  - Use `Monolith.Packages.findPackageByModule()` to discover which package provides a module
  - Use generic `/api/packages/{packageId}/modules/{moduleId}/{action}` pattern

#### 2.2 Refactor Setup Network Page (`Network.cshtml`)
- **Current**: Checks if `monolith-network` is installed
- **New**: Check if any package provides network interface management
  ```javascript
  // Instead of:
  const hasNetworkPackage = packages.some(p => p.packageId === 'monolith-network');
  
  // Use:
  const hasNetworkPackage = await Monolith.Packages.findPackageByModule('interfaces') !== null;
  ```
- **Changes**:
  - Remove hardcoded package ID check
  - Use module-based discovery (check for `interfaces` module)
  - Show generic message if no package provides network functionality

### Phase 3: Dynamic Widget System
**Goal**: Make dashboard widgets discoverable from packages, remove hardcoded fallbacks.

#### 3.1 Refactor Dashboard Controller (`DashboardController.cs`)
- **Current**: Hardcoded `monolith-network` mapping and fallback
- **New**: Dynamic widget discovery
  ```csharp
  // Instead of:
  var packageId = $"monolith-{packagePrefix}"; // "network" -> "monolith-network"
  
  // Use:
  var packageId = await _packageDiscovery.FindPackageByModuleAsync(packagePrefix);
  if (packageId == null) return NotFound();
  ```
- **Changes**:
  - Remove hardcoded package ID mapping
  - Use `PackageDiscoveryService` to find package by module
  - Remove hardcoded `GetDhcpStatus()` fallback
  - All widgets must come from packages

#### 3.2 Create Widget Discovery API Endpoint
- **File**: `src/Monolith.FireWall.WebUI/Controllers/WidgetsController.cs` (new)
- **Purpose**: Provide endpoint to discover available widgets from all packages
- **Endpoint**: `GET /api/widgets`
- **Returns**: List of all available widgets from installed packages

### Phase 4: Dynamic Permissions System Integration
**Goal**: Fully integrate permissions system with package discovery - permissions should be discovered from installed packages and properly integrated with user/group management.

#### 4.1 Create Permissions API Endpoint
- **File**: `src/Monolith.FireWall.WebUI/Controllers/PermissionsController.cs` (new)
- **Purpose**: Provide endpoints to get all permissions from installed packages
- **Endpoints**:
  - `GET /api/permissions` - Get all permissions from all packages
  - `GET /api/permissions/categories` - Get permissions grouped by category
  - `GET /api/permissions/by-package` - Get permissions grouped by package
  - `GET /api/permissions/{packageId}` - Get permissions for specific package
- **Returns**: 
  ```json
  {
    "permissions": [
      {
        "id": "network.dhcp.read",
        "name": "View DHCP Configuration",
        "category": "Network",
        "subcategory": "DHCP",
        "packageId": "monolith-network",
        "packageName": "Monolith Network",
        "moduleId": "dhcp",
        "moduleName": "DHCP Server"
      }
    ],
    "categories": {
      "Network": ["network.*", "network.dhcp.*", ...]
    }
  }
  ```

#### 4.2 Integrate with User/Group Permission System
- **Purpose**: Ensure discovered permissions work with existing user/group permission checks
- **Integration Points**:
  - `AuthenticationMiddleware` - Already uses `UserContext.Permissions`
  - `PackageViewRouter.HasPermission()` - Already checks permissions
  - User/Group management - Need to ensure discovered permissions are available for assignment
- **Changes**:
  - Verify `UserContext` includes all package permissions
  - Ensure permission checks use discovered permissions
  - Update user/group assignment UI to show discovered permissions

#### 4.3 Refactor Permissions Page (`permissions.js`)
- **Current**: Hardcoded permission list
- **New**: Load permissions dynamically from packages
  ```javascript
  // Instead of:
  const permissions = [
    { id: 'network.dhcp.read', name: 'View DHCP Configuration', ... },
    // hardcoded list
  ];
  
  // Use:
  const response = await Monolith.API.get('/api/permissions');
  const permissions = response.permissions || response.data || [];
  ```
- **Changes**:
  - Remove hardcoded permission array
  - Load from API endpoint on page init
  - Group by package/category dynamically
  - Show package/module information for each permission
  - Handle case when no packages are installed (show core permissions only)

#### 4.4 Refactor Groups Page (`groups.js`)
- **Current**: Hardcoded permission categories
- **New**: Dynamic categories from packages
  ```javascript
  // Instead of:
  const categories = {
    'Network': ['network.*', 'network.dhcp.*', 'network.dns.*', ...],
    'System': ['system.*', ...],
    // hardcoded
  };
  
  // Use:
  const response = await Monolith.API.get('/api/permissions/categories');
  const categories = response.categories || response.data || {};
  ```
- **Changes**:
  - Remove hardcoded categories
  - Load from API endpoint
  - Build permission tree dynamically
  - Group by package, then by category
  - Support wildcard permissions (e.g., `network.*`)

#### 4.5 Update Permission Validation
- **Purpose**: Ensure permission checks work with discovered permissions
- **Files to Update**:
  - `PackageViewRouter.HasPermission()` - Already generic, verify it works
  - `AuthenticationMiddleware` - Verify it uses discovered permissions
  - Any other permission checks in controllers
- **Changes**:
  - Verify permission IDs match between packages and user assignments
  - Test permission inheritance (wildcards, package-level)
  - Ensure permission checks are case-insensitive and flexible

### Phase 5: Dynamic Features List
**Goal**: Discover features from installed packages for About page.

#### 5.1 Refactor About Page (`about.js`)
- **Current**: Hardcoded feature list including "DHCP and DNS server management"
- **New**: Dynamic feature discovery
  ```javascript
  // Instead of:
  <li>DHCP and DNS server management</li>
  
  // Use:
  const features = await Monolith.API.get('/api/packages/features');
  features.forEach(feature => {
    // Render feature from package description/modules
  });
  ```
- **Changes**:
  - Remove hardcoded feature list
  - Query packages for their descriptions/modules
  - Display features dynamically

### Phase 6: Core API Enhancements (if needed)
**Goal**: Ensure Core provides all necessary APIs for package discovery.

#### 6.1 Check Core API Coverage
- Verify `packages.list` returns sufficient information
- Verify `packages.get-widget-data` works generically
- Verify module routes are discoverable
- Add missing APIs if needed:
  - `packages.get-modules` - Get all modules for a package
  - `packages.get-permissions` - Get all permissions for a package
  - `packages.find-by-module` - Find package that provides a module

## Implementation Steps

### Step 1: Create Package Discovery Service
1. Create `PackageDiscoveryService.cs` in WebUI Services
2. Implement methods to query Core for package information
3. Add caching for performance
4. Add error handling

### Step 2: Create JavaScript Package Discovery Utility
1. Create `package-discovery.js` in `wwwroot/js/core/`
2. Implement async methods to query package APIs
3. Add helper methods for common queries
4. Export as `Monolith.Packages` namespace

### Step 3: Refactor Setup Wizard
1. Update `setup.js` to use generic module APIs
2. Remove hardcoded `monolith-network` checks
3. Use `Monolith.Packages.findPackageByModule()` for discovery
4. Test with network package

### Step 4: Refactor Setup Network Page
1. Update `Network.cshtml` to use module-based discovery
2. Remove hardcoded package ID check
3. Update error messages to be generic
4. Test setup flow

### Step 5: Refactor Dashboard Widgets
1. Create `WidgetsController.cs` for widget discovery
2. Update `DashboardController.cs` to use dynamic discovery
3. Remove hardcoded fallbacks
4. Test widget loading

### Step 6: Refactor Permissions
1. Create `PermissionsController.cs` for permission discovery
2. Update `permissions.js` to load from API
3. Update `groups.js` to use dynamic categories
4. Test permission management

### Step 7: Refactor About Page
1. Update `about.js` to load features dynamically
2. Remove hardcoded feature list
3. Test feature display

### Step 8: Testing & Validation
1. Test with network package installed
2. Test with network package uninstalled
3. Test with multiple packages installed
4. Verify no hardcoded references remain
5. Test error handling (package not found, etc.)

## Files to Modify

### New Files
- `src/Monolith.FireWall.WebUI/Services/PackageDiscoveryService.cs`
- `src/Monolith.FireWall.WebUI/wwwroot/js/core/package-discovery.js`
- `src/Monolith.FireWall.WebUI/Controllers/WidgetsController.cs`
- `src/Monolith.FireWall.WebUI/Controllers/PermissionsController.cs`

### Modified Files
- `src/Monolith.FireWall.WebUI/wwwroot/js/pages/setup.js`
- `src/Monolith.FireWall.WebUI/Pages/Setup/Network.cshtml`
- `src/Monolith.FireWall.WebUI/Features/Dashboard/DashboardController.cs`
- `src/Monolith.FireWall.WebUI/wwwroot/js/pages/permissions.js`
- `src/Monolith.FireWall.WebUI/wwwroot/js/pages/groups.js`
- `src/Monolith.FireWall.WebUI/wwwroot/js/pages/about.js`

## Testing Checklist

- [ ] Setup wizard works with network package installed
- [ ] Setup wizard handles missing network package gracefully
- [ ] Dashboard widgets load from packages dynamically
- [ ] Permissions page shows all package permissions
- [ ] Groups page shows dynamic permission categories
- [ ] About page shows features from installed packages
- [ ] No hardcoded `monolith-network` references remain
- [ ] Error handling works when packages are missing
- [ ] System works with multiple packages installed
- [ ] System works with no packages installed (core only)

## Benefits

1. **Package-Agnostic**: WebUI no longer depends on specific packages
2. **Extensible**: New packages automatically integrate with UI
3. **Maintainable**: No need to update WebUI when packages change
4. **Flexible**: Packages can be installed/uninstalled without code changes
5. **Clean Architecture**: Separation of concerns between core, packages, and UI

## Migration Notes

- Existing installations will continue to work
- Network package functionality remains the same
- No breaking changes to user-facing features
- Only internal implementation changes
