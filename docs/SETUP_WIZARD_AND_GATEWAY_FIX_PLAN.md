# Setup Wizard and Gateway Fix Plan

## Overview
This plan addresses:
1. Setup Wizard UI issues (buttons disappearing, weird placement, styling)
2. Gateway page issues (showing "Kernel", interface not displayed)
3. Gateway import functionality in setup wizard

## Current Issues Analysis

### Setup Wizard Issues
1. **Duplicate Setup Wizard Files**: 
   - `setup-wizard.js` (standalone controller)
   - `pages/setup.js` (main controller)
   - Both try to manage the same functionality, causing conflicts

2. **Button Disappearing Issues**:
   - Navigation buttons are added dynamically via `setupStepPageNavigation()`
   - Buttons may be removed/hidden by conflicting code
   - CSS may be hiding buttons with `display: none` or `visibility: hidden`

3. **Weird Placement**:
   - Navigation buttons are appended to `.card-body` which may not exist on all pages
   - Layout inconsistencies between different step pages
   - Progress bar and navigation not properly aligned

4. **Styling Issues**:
   - Mix of Bootstrap classes and custom setup-wizard.css
   - Inconsistent spacing and alignment
   - Buttons may not be properly styled

### Gateway Issues
1. **"Kernel" Display**:
   - The `Source` field shows "Kernel" for static routes from the routing table
   - Should show "Static" for manually configured gateways
   - Dynamic gateways should show "Dynamic"

2. **Interface Not Shown**:
   - Interface column shows "-" when interface exists
   - GatewayView has `Interface` property but it's not being displayed correctly
   - May be null/undefined handling issue

3. **Gateway Import in Setup Wizard**:
   - Network setup page doesn't import existing gateway from system
   - Should detect default gateway from routing table
   - Should pre-fill gateway field with current default gateway

## Phase 1: Fix Gateway Display Issues

### 1.1 Fix Gateway Source Display
**File**: `src/Monolith.FireWall.WebUI/wwwroot/js/pages/routing.js`
- Fix source badge logic to properly handle "static" vs "dynamic"
- Remove "Kernel" display - use "Static" for non-dynamic gateways
- Ensure Source field is correctly set from GatewayView

### 1.2 Fix Interface Display
**File**: `src/Monolith.FireWall.WebUI/wwwroot/js/pages/routing.js`
- Fix interface column to properly display interface name
- Handle null/undefined interface gracefully
- Show interface name from `gw.Interface` or `gw.interface`

### 1.3 Fix Gateway Import in Setup Wizard
**File**: `src/Monolith.FireWall.WebUI/Pages/Setup/Network.cshtml`
- Add function to detect and import current default gateway
- Query `/api/routing/status` to get default gateway
- Pre-fill gateway field with current default gateway address
- Pre-select WAN interface if gateway interface matches

## Phase 2: Refactor Setup Wizard

### 2.1 Consolidate Setup Wizard Files
**Decision**: Keep `setup-wizard.js` as the main controller, remove duplicate logic from `pages/setup.js`
- `setup-wizard.js` will be the single source of truth
- `pages/setup.js` will be removed or simplified to only handle step-specific logic
- Ensure all setup pages use the same navigation system

### 2.2 Fix Navigation Button Issues
**Files**: 
- `src/Monolith.FireWall.WebUI/wwwroot/js/setup-wizard.js`
- `src/Monolith.FireWall.WebUI/Pages/Shared/_SetupLayout.cshtml`

**Changes**:
- Add navigation buttons directly in `_SetupLayout.cshtml` instead of dynamically
- Use consistent button IDs across all pages
- Ensure buttons are always visible (remove conflicting CSS)
- Fix button positioning with proper flexbox/grid layout

### 2.3 Improve Setup Wizard Styling
**File**: `src/Monolith.FireWall.WebUI/wwwroot/css/setup-wizard.css`
- Fix button visibility issues
- Ensure consistent spacing
- Fix navigation button container layout
- Add proper responsive design
- Fix progress bar alignment

### 2.4 Standardize Setup Page Structure
**Files**: All setup pages (Router.cshtml, Network.cshtml, PackageStep.cshtml)
- Ensure all pages use the same structure
- Navigation buttons should be in a consistent location
- Remove duplicate navigation button code
- Use shared layout for navigation

## Phase 3: Gateway Import Enhancement

### 3.1 Add Gateway Detection to Network Setup
**File**: `src/Monolith.FireWall.WebUI/Pages/Setup/Network.cshtml`
- On page load, query routing status API
- Extract default gateway from response
- Pre-fill gateway input field
- If gateway has an interface, try to match it to WAN interface selector

### 3.2 Improve Gateway Display Logic
**File**: `src/Monolith.FireWall.Core/Services/GatewayManager.cs`
- Ensure `Source` field is properly set in `ToView()` method
- For static gateways, set Source = "static"
- For dynamic gateways, set Source = "dynamic"
- Remove any "Kernel" references

## Implementation Details

### Gateway Source Fix
```javascript
// In routing.js renderGateways()
const source = gw.IsDynamic || gw.isDynamic 
    ? 'dynamic' 
    : (gw.Source === 'static' || !gw.Source ? 'static' : 'static'); // Always static if not dynamic
```

### Gateway Interface Fix
```javascript
// In routing.js renderGateways()
const interface = gw.Interface || gw.interface || 'N/A';
// Display interface name, not "-"
```

### Setup Wizard Navigation Fix
- Add navigation buttons to `_SetupLayout.cshtml` as a consistent footer
- Remove dynamic button injection from JavaScript
- Use CSS to ensure buttons are always visible
- Fix button state management

### Gateway Import in Setup
```javascript
// In Network.cshtml
async function loadCurrentGateway() {
    try {
        const response = await Monolith.API.get('/api/routing/status');
        const data = response.Data || response.data || response;
        const defaultGateway = data.DefaultGateway || data.defaultGateway;
        if (defaultGateway && defaultGateway.Address) {
            $('#gateway').val(defaultGateway.Address || defaultGateway.address);
            // Try to match interface
            if (defaultGateway.Interface) {
                const iface = defaultGateway.Interface || defaultGateway.interface;
                $('#wan-interface').val(iface);
            }
        }
    } catch (err) {
        console.warn('Could not load current gateway:', err);
    }
}
```

## Files to Modify

### Gateway Fixes
1. `src/Monolith.FireWall.WebUI/wwwroot/js/pages/routing.js` - Fix source and interface display
2. `src/Monolith.FireWall.Core/Services/GatewayManager.cs` - Ensure Source is set correctly
3. `src/Monolith.FireWall.WebUI/Pages/Setup/Network.cshtml` - Add gateway import

### Setup Wizard Fixes
1. `src/Monolith.FireWall.WebUI/wwwroot/js/setup-wizard.js` - Consolidate and fix logic
2. `src/Monolith.FireWall.WebUI/wwwroot/js/pages/setup.js` - Remove or simplify
3. `src/Monolith.FireWall.WebUI/Pages/Shared/_SetupLayout.cshtml` - Add consistent navigation
4. `src/Monolith.FireWall.WebUI/wwwroot/css/setup-wizard.css` - Fix styling issues
5. `src/Monolith.FireWall.WebUI/Pages/Setup/Router.cshtml` - Remove duplicate navigation
6. `src/Monolith.FireWall.WebUI/Pages/Setup/Network.cshtml` - Remove duplicate navigation

## Testing Checklist

### Gateway Page
- [ ] Gateway source shows "Static" or "Dynamic" (not "Kernel")
- [ ] Interface column shows interface name (not "-")
- [ ] All gateway fields display correctly
- [ ] Dynamic gateways are properly identified
- [ ] Static gateways are properly identified

### Setup Wizard
- [ ] Navigation buttons are always visible
- [ ] Buttons are properly positioned
- [ ] Progress bar updates correctly
- [ ] Step navigation works (Next/Back/Skip)
- [ ] Gateway is imported in Network setup step
- [ ] WAN interface is pre-selected if gateway interface matches
- [ ] All setup pages have consistent layout
- [ ] No duplicate navigation buttons
- [ ] Styling is consistent and modern

### Gateway Import
- [ ] Default gateway is detected from system
- [ ] Gateway field is pre-filled in Network setup
- [ ] Interface is matched if available
- [ ] Works even if no gateway is configured

## Success Criteria

1. ✅ Setup wizard has consistent, visible navigation buttons
2. ✅ Setup wizard has modern, clean styling
3. ✅ No duplicate or conflicting setup wizard code
4. ✅ Gateway page shows correct source ("Static" or "Dynamic")
5. ✅ Gateway page shows interface names correctly
6. ✅ Network setup imports current gateway automatically
7. ✅ All setup pages have consistent layout and behavior
