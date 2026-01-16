# NAT Tabbed Page Implementation Plan

## Overview
Convert the single NAT rules page into a tabbed interface with three tabs:
1. **Port Forward** - Port forwarding rules (DNAT in PREROUTING)
2. **1:1** - One-to-one NAT rules (DNAT in PREROUTING)
3. **Outbound** - Outbound NAT rules (SNAT/Masquerade in POSTROUTING)

## Current State Analysis

### Backend (Core)
- ✅ NAT rules already support `Type` field: `port_forward`, `one_to_one`, `outbound`
- ✅ `FirewallNatManager` handles all three types
- ✅ `FirewallApplyManager.BuildNatRule()` currently handles DNAT (port_forward, one_to_one)
- ⚠️ **Note**: Outbound NAT rules may need additional implementation in POSTROUTING chain

### Frontend (WebUI)
- Current page: `/firewall/nat` (Config.cshtml)
- JavaScript: `nat.js` handles all rule types in one table
- Models: `NatRule` model already has `Type` field
- API: `NatController` already supports type filtering

## Implementation Plan

### Phase 1: Frontend UI Changes

#### 1.1 Update Config.cshtml
**File**: `src/Monolith.FireWall.WebUI/Pages/Firewall/Nat/Config.cshtml`

**Changes**:
- Replace single table with Bootstrap tabs component
- Create three tab panes:
  - Tab 1: Port Forward (`data-tab="port_forward"`)
  - Tab 2: 1:1 (`data-tab="one_to_one"`)
  - Tab 3: Outbound (`data-tab="outbound"`)
- Each tab has its own table with appropriate columns
- Shared "Add Rule" button that opens modal with type pre-selected based on active tab
- Keep pending changes banner and status messages at page level (not per-tab)

**Tab Structure**:
```html
<ul class="nav nav-tabs" id="natTabs" role="tablist">
  <li class="nav-item" role="presentation">
    <button class="nav-link active" data-bs-toggle="tab" data-bs-target="#portForwardTab" type="button" role="tab">
      Port Forward
    </button>
  </li>
  <li class="nav-item" role="presentation">
    <button class="nav-link" data-bs-toggle="tab" data-bs-target="#oneToOneTab" type="button" role="tab">
      1:1
    </button>
  </li>
  <li class="nav-item" role="presentation">
    <button class="nav-link" data-bs-toggle="tab" data-bs-target="#outboundTab" type="button" role="tab">
      Outbound
    </button>
  </li>
</ul>

<div class="tab-content" id="natTabContent">
  <!-- Port Forward Tab -->
  <div class="tab-pane fade show active" id="portForwardTab" role="tabpanel">
    <!-- Table for port_forward rules -->
  </div>
  <!-- 1:1 Tab -->
  <div class="tab-pane fade" id="oneToOneTab" role="tabpanel">
    <!-- Table for one_to_one rules -->
  </div>
  <!-- Outbound Tab -->
  <div class="tab-pane fade" id="outboundTab" role="tabpanel">
    <!-- Table for outbound rules -->
  </div>
</div>
```

#### 1.2 Update nat.js
**File**: `src/Monolith.FireWall.WebUI/wwwroot/js/nat.js`

**Changes**:
1. **Add active tab tracking**:
   - Store current active tab in `Nat.activeTab`
   - Listen for tab change events
   - Filter rules by type based on active tab

2. **Update renderRules()**:
   - Rename to `renderRulesByType(type)` or keep `renderRules()` but filter by active tab
   - Filter `this.rules` by `rule.type === activeTabType`
   - Render to appropriate table body based on tab

3. **Update table columns per tab**:
   - **Port Forward**: Interface, Protocol, Source, Destination, Redirect Target IP:Port, Description, Status, Actions
   - **1:1**: Interface, Source IP, Destination IP, Redirect Target IP, Description, Status, Actions
   - **Outbound**: Interface, Source, Destination, NAT Target (SNAT), Description, Status, Actions

4. **Update showRuleModal()**:
   - Pre-select type based on active tab
   - Show/hide fields based on type:
     - Port Forward: Show destination port, redirect target port
     - 1:1: Hide ports (IP-to-IP mapping)
     - Outbound: Show SNAT target instead of DNAT target

5. **Update loadRules()**:
   - Load all rules (unchanged)
   - Call render for each tab after loading

6. **Add tab change handler**:
   ```javascript
   $(document).on('shown.bs.tab', '#natTabs button[data-bs-toggle="tab"]', (e) => {
     const target = $(e.target).data('bs-target');
     const tabType = target === '#portForwardTab' ? 'port_forward' :
                     target === '#oneToOneTab' ? 'one_to_one' : 'outbound';
     Nat.activeTab = tabType;
     Nat.renderRulesByType(tabType);
   });
   ```

#### 1.3 Update CSS (if needed)
**File**: `src/Monolith.FireWall.WebUI/wwwroot/css/firewall.css` (or create nat.css)

**Changes**:
- Add styles for tabbed interface
- Ensure tables are responsive within tabs
- Style active tab indicators

### Phase 2: Backend Compatibility

#### 2.1 Verify API Support
**Status**: ✅ Already supported
- `NatController.List()` returns all rules
- Can add optional `?type=port_forward` filter if needed (optional enhancement)
- Create/Update/Delete already support `Type` field

#### 2.2 Core NAT Rule Processing
**File**: `src/Monolith.FireWall.Core/Services/Firewall/FirewallApplyManager.cs`

**Current State**:
- `BuildNatRule()` handles DNAT for port_forward and one_to_one
- POSTROUTING chain has automatic masquerade for WAN interfaces
- **Action Needed**: Verify if outbound rules need special handling

**Potential Changes**:
- If outbound rules need custom SNAT (not just masquerade), add handling in POSTROUTING chain
- Check if `rule.Type == "outbound"` needs special processing
- May need to add SNAT target field if not using masquerade

### Phase 3: Debian 13 Integration

#### 3.1 iptables/nftables Compatibility
**Status**: ✅ Already compatible
- System uses nftables (Debian 13 default)
- NAT rules are generated as nftables rules
- PREROUTING chain for port_forward/one_to_one (DNAT)
- POSTROUTING chain for outbound (SNAT/Masquerade)

#### 3.2 Testing Checklist
- [ ] Port Forward rules apply correctly in PREROUTING
- [ ] 1:1 NAT rules apply correctly in PREROUTING
- [ ] Outbound rules apply correctly in POSTROUTING
- [ ] Rules persist across firewall apply/reload
- [ ] Tab switching maintains rule state
- [ ] Add/Edit/Delete works from each tab

## Implementation Steps

### Step 1: Update HTML Structure
1. Modify `Config.cshtml` to use Bootstrap tabs
2. Create three separate table structures (one per tab)
3. Update card header to show active tab context

### Step 2: Update JavaScript Logic
1. Add tab state management
2. Implement `renderRulesByType(type)` function
3. Update modal to pre-select type based on tab
4. Add field visibility logic based on type
5. Update event handlers for tab-specific actions

### Step 3: Update Table Columns
1. **Port Forward Tab**:
   - #, Interface, Protocol, Source, Destination, Redirect Target, Description, Status, Actions
2. **1:1 Tab**:
   - #, Interface, Source IP, Destination IP, Redirect Target IP, Description, Status, Actions
3. **Outbound Tab**:
   - #, Interface, Source, Destination, NAT Target, Description, Status, Actions

### Step 4: Update Form Fields
1. Port Forward: Full form (all fields)
2. 1:1: Hide port fields, show IP-to-IP mapping
3. Outbound: Show SNAT target field, adjust source/destination logic

### Step 5: Testing
1. Test each tab independently
2. Verify rule filtering works correctly
3. Test add/edit/delete from each tab
4. Verify backend API compatibility
5. Test firewall apply with mixed rule types

## File Changes Summary

### Files to Modify
1. `src/Monolith.FireWall.WebUI/Pages/Firewall/Nat/Config.cshtml` - Add tabs structure
2. `src/Monolith.FireWall.WebUI/wwwroot/js/nat.js` - Add tab filtering and type-specific rendering
3. `src/Monolith.FireWall.WebUI/wwwroot/css/firewall.css` - Add tab styles (if needed)

### Files to Review (No Changes Expected)
1. `src/Monolith.FireWall.WebUI/Features/Firewall/Nat/NatController.cs` - Already supports types
2. `src/Monolith.FireWall.WebUI/Features/Firewall/Nat/Models.cs` - Already has Type field
3. `src/Monolith.FireWall.Core/Services/Firewall/FirewallNatManager.cs` - Already handles all types
4. `src/Monolith.FireWall.Core/Services/Firewall/FirewallApplyManager.cs` - May need outbound handling review

## Notes

### Outbound NAT Considerations
- Current system has automatic masquerade for WAN interfaces
- Outbound rules may need:
  - Custom SNAT target (instead of masquerade)
  - Source interface matching
  - Destination interface matching
- May need to extend `BuildNatRule()` or create `BuildOutboundNatRule()` for POSTROUTING chain

### Type-Specific Field Requirements
- **Port Forward**: Requires destination port and redirect target port
- **1:1**: IP-to-IP mapping, no ports
- **Outbound**: May need SNAT target IP (or use masquerade)

### Backward Compatibility
- Existing rules will continue to work
- Type field is already in database schema
- No migration needed

## Success Criteria
1. ✅ Three tabs display correctly
2. ✅ Rules filter correctly by type
3. ✅ Add/Edit/Delete works from each tab
4. ✅ Form fields show/hide based on type
5. ✅ Rules apply correctly to firewall (iptables/nftables)
6. ✅ UI is responsive and user-friendly
7. ✅ No breaking changes to existing functionality
