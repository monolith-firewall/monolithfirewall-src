# Routing Page Integration Plan

## Current State Analysis

### Existing Routing Page (`routing.js`)
**What's Already There:**
- ✅ Gateways tab - displays current gateways
- ✅ Static Routes tab - displays and manages static routes
- ✅ Add Route functionality
- ✅ Delete Route functionality
- ✅ Basic routing page structure with tabs

**What's Missing:**
- ❌ Routing status (IP forwarding, NAT masquerade)
- ❌ Routing table display
- ❌ IP forwarding toggle/configuration
- ❌ Gateway configuration (set default gateway)
- ❌ Routing tests (ping, traceroute)
- ❌ Routing status API integration

### What We Added to Interfaces (Need to Move)
**From `interfaces.js`:**
1. Routing Status tab with:
   - IP forwarding status display
   - NAT masquerade status
   - Default gateway information
   - Complete routing table display
2. Routing Configuration:
   - IP forwarding toggle switch
   - Apply IP forwarding button
3. Gateway Configuration:
   - Gateway address/interface inputs
   - Set gateway button
   - Test gateway button
4. Routing Tests:
   - Ping test functionality
   - Traceroute functionality
   - Test results display

## Integration Plan

### Phase 1: Add Routing Status Tab to Routing Page
**Goal**: Add new "Routing Status" tab as the first tab

**Tasks**:
1. Add "Routing Status" tab to the tab navigation
2. Create routing status tab content with:
   - IP forwarding status card (with toggle)
   - NAT masquerade status card
   - Default gateway display
   - Complete routing table
   - Refresh button
3. Add `loadRoutingStatus()` method
4. Add `renderRoutingStatus()` method
5. Integrate with `/api/routing/status` endpoint

### Phase 2: Enhance Gateways Tab
**Goal**: Add gateway configuration to existing Gateways tab

**Tasks**:
1. Add gateway configuration section to Gateways tab:
   - Current default gateway display (enhance existing)
   - Set default gateway form
   - Test gateway connectivity button
2. Add `setDefaultGateway()` method
3. Add `testGateway()` method
4. Enhance gateway display with more details

### Phase 3: Add Routing Tests Tab
**Goal**: Add new tab for routing diagnostic tools

**Tasks**:
1. Add "Routing Tests" tab
2. Create routing tests tab content:
   - Ping test section
   - Traceroute test section
   - Test results display area
3. Add `runPingTest()` method
4. Add `runTraceTest()` method
5. Add `isValidIp()` helper method

### Phase 4: Add Routing Configuration Section
**Goal**: Add IP forwarding configuration

**Tasks**:
1. Add routing configuration card to Routing Status tab:
   - IP forwarding toggle
   - Apply button
   - Link to Advanced Settings for more options
2. Add `applyIpForwarding()` method
3. Integrate with `/api/system/tuneables/apply` endpoint

### Phase 5: Remove Routing from Interfaces Page
**Goal**: Clean up interfaces page

**Tasks**:
1. Remove "Routing Status" tab from `interfaces.js`
2. Remove routing tab from tab navigation
3. Remove routing-related event handlers:
   - `#btn-refresh-routing`
   - `#btn-apply-ip-forwarding`
   - `#btn-set-gateway`
   - `#btn-test-gateway`
   - `#btn-ping-test`
   - `#btn-trace-test`
4. Remove routing methods from `interfaces.js`:
   - `loadRoutingStatus()`
   - `renderRoutingStatus()`
   - `applyIpForwarding()`
   - `setDefaultGateway()`
   - `testGateway()`
   - `runPingTest()`
   - `runTraceTest()`
   - `isValidIp()`
5. Remove routing status loading from `loadData()`

### Phase 6: Update Routing Page Load Data
**Goal**: Load routing status along with gateways and routes

**Tasks**:
1. Update `loadData()` to include `loadRoutingStatus()`
2. Ensure all data loads correctly
3. Add error handling

## Implementation Structure

### Updated Routing Page Layout
```
Routing Page
├── Tab Navigation
│   ├── Routing Status (NEW - first tab)
│   ├── Gateways (enhanced)
│   ├── Static Routes (existing)
│   └── Routing Tests (NEW)
│
├── Routing Status Tab
│   ├── Status Cards Row
│   │   ├── IP Forwarding Card (toggle + status)
│   │   └── NAT Masquerade Card (status)
│   ├── Default Gateway Section
│   ├── Routing Table Section
│   └── Routing Configuration Card
│
├── Gateways Tab (Enhanced)
│   ├── Gateways Table (existing)
│   └── Gateway Configuration Card (NEW)
│       ├── Current Gateway Display
│       ├── Set Gateway Form
│       └── Test Gateway Button
│
├── Static Routes Tab (Keep as-is)
│   └── Static Routes Table + Add/Delete
│
└── Routing Tests Tab (NEW)
    ├── Ping Test Section
    ├── Traceroute Test Section
    └── Test Results Display
```

## File Changes

### Files to Modify

1. **`src/Monolith.FireWall.WebUI/wwwroot/js/pages/routing.js`**
   - Add Routing Status tab
   - Add Routing Tests tab
   - Enhance Gateways tab
   - Add all routing methods from interfaces.js
   - Update `loadData()` to include routing status
   - Add event handlers for new features

2. **`src/Monolith.FireWall.WebUI/wwwroot/js/pages/interfaces.js`**
   - Remove Routing Status tab
   - Remove all routing-related code
   - Clean up event handlers
   - Remove routing methods

### Files Already Done (Backend)
- ✅ `RoutingManager.cs` - Has `GetRoutingStatusAsync()`
- ✅ `RoutingHandler.cs` - Has `routing.status` action
- ✅ `SystemCommandHandler.cs` - Has command execution
- ✅ API endpoints in `Program.cs`:
  - `GET /api/routing/status`
  - `POST /api/system/command`

## Implementation Order

1. **First**: Add Routing Status tab to routing.js
2. **Second**: Move routing methods from interfaces.js to routing.js
3. **Third**: Add Routing Tests tab to routing.js
4. **Fourth**: Enhance Gateways tab with configuration
5. **Fifth**: Remove routing tab from interfaces.js
6. **Sixth**: Test all functionality
7. **Seventh**: Clean up and verify

## Detailed Tab Structure

### Tab 1: Routing Status (NEW)
```html
<div class="tab-pane fade show active" id="routing-status">
  <!-- Status Cards -->
  <div class="row mb-4">
    <div class="col-md-6">
      IP Forwarding Card (with toggle)
    </div>
    <div class="col-md-6">
      NAT Masquerade Card
    </div>
  </div>
  
  <!-- Default Gateway -->
  <div class="card mb-4">
    Default Gateway Display
  </div>
  
  <!-- Routing Table -->
  <div class="card mb-4">
    Complete Routing Table
  </div>
  
  <!-- Configuration -->
  <div class="card">
    IP Forwarding Configuration
  </div>
</div>
```

### Tab 2: Gateways (Enhanced)
```html
<div class="tab-pane fade" id="routing-gateways">
  <!-- Existing Gateways Table -->
  <div class="card mb-4">
    Gateways Table (existing)
  </div>
  
  <!-- NEW: Gateway Configuration -->
  <div class="card">
    Gateway Configuration Card
    - Current gateway display
    - Set gateway form
    - Test gateway button
  </div>
</div>
```

### Tab 3: Static Routes (Keep as-is)
- No changes needed

### Tab 4: Routing Tests (NEW)
```html
<div class="tab-pane fade" id="routing-tests">
  <div class="card">
    <!-- Ping Test -->
    <div class="mb-4">
      Ping test section
    </div>
    
    <!-- Traceroute Test -->
    <div class="mb-4">
      Traceroute test section
    </div>
    
    <!-- Test Results -->
    <div>
      Test results display
    </div>
  </div>
</div>
```

## Success Criteria

- ✅ Routing page has 4 tabs: Status, Gateways, Static Routes, Tests
- ✅ Routing Status tab shows all status information
- ✅ IP forwarding can be toggled from Routing Status tab
- ✅ Gateway configuration available in Gateways tab
- ✅ Routing tests work in Routing Tests tab
- ✅ Interfaces page no longer has routing tab
- ✅ All routing functionality centralized in routing page
- ✅ All features work correctly
- ✅ Page loads without errors

## Notes

- Keep existing functionality intact (gateways, static routes)
- Add new features as additional tabs/sections
- Maintain consistency with existing routing page style
- All routing features in one place for better UX
- Interfaces page focuses solely on interface management
