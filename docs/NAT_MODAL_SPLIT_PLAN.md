# NAT Tabbed Page with Separate Modals - Implementation Plan

## Overview
Refactor the NAT page to keep the tabbed interface but provide **separate, type-specific modals** for each NAT type (Port Forwarding, 1:1 NAT, Outbound NAT). Remove the TYPE selector from modals since each modal is dedicated to a specific type.

## Current State Analysis

### Current Implementation
- **Page**: `/firewall/nat` (Config.cshtml) - Tabbed interface with 3 tabs
- **Modal**: Single `showRuleModal()` function with TYPE dropdown selector
- **JavaScript**: `nat.js` - One modal function that shows/hides fields based on type
- **Backend**: `NatController` - Handles all types via `type` field in request
- **Database**: Single `firewall_nat_rules` table with `Type` column
- **Integration**: 
  - ✅ nftables: `FirewallApplyManager` generates nftables rules from NAT rules
  - ✅ Interfaces: Loaded from `/interfaces/assignments` API
  - ✅ Aliases: Loaded from `/firewall/aliases` API
  - ✅ Schedules: Loaded from `/firewall/schedules` API

### Current Modal Structure
- Single modal (`#natRuleModal`) with:
  - Type selector dropdown (lines 486-493 in nat.js)
  - Dynamic field visibility based on type
  - `toggleNatFieldsByType()` function to show/hide fields
  - Type change handler that updates fields

### Current Data Flow
1. User clicks "Add Rule" → `showRuleModal(null, activeTab)` 
2. Modal shows with type pre-selected from active tab
3. User can change type → fields update dynamically
4. Save → `saveRule()` sends `type` field to backend
5. Backend stores with `Type` field in database
6. nftables generation reads `Type` field and generates appropriate rules

## Requirements

### Functional Requirements
1. **Keep tabbed page structure** - Three tabs remain (Port Forward, 1:1, Outbound)
2. **Separate modals per type** - Each tab has its own dedicated modal
3. **Remove TYPE field** - No type selector in any modal (type is implicit from active tab)
4. **Type-specific fields** - Each modal shows only relevant fields for its type
5. **Integration maintained** - All existing integrations must continue to work:
   - nftables rule generation
   - Interface selection
   - Alias selection
   - Schedule selection
   - Apply/Discard workflow

### Technical Requirements
1. **Backend**: No changes needed (already supports all types)
2. **Frontend**: 
   - Create three separate modal functions
   - Remove type selector from all modals
   - Ensure type is set based on active tab
   - Maintain all existing API calls
3. **Validation**: Type-specific validation per modal
4. **UX**: Clear, focused modals without unnecessary fields

## Implementation Plan

### Phase 1: JavaScript Refactoring

#### 1.1 Create Three Separate Modal Functions
**File**: `src/Monolith.FireWall.WebUI/wwwroot/js/nat.js`

Replace single `showRuleModal()` with three functions:
- `showPortForwardModal(rule)` - Port Forwarding modal
- `showOneToOneModal(rule)` - 1:1 NAT modal  
- `showOutboundModal(rule)` - Outbound NAT modal

**Key Changes**:
- Remove `defaultType` parameter (type is implicit)
- Remove TYPE selector dropdown
- Remove `toggleNatFieldsByType()` calls
- Remove type change handler
- Hard-code type in each modal's save function
- Show only relevant fields for each type

#### 1.2 Update Event Handlers
**File**: `src/Monolith.FireWall.WebUI/wwwroot/js/nat.js`

Modify `attachEventHandlers()`:
- Update `#btnAddRule` click handler to call type-specific modal based on active tab
- Update edit handlers to call appropriate modal based on rule type
- Track active tab to determine which modal to show

#### 1.3 Update Save Functions
**File**: `src/Monolith.FireWall.WebUI/wwwroot/js/nat.js`

Create three save functions (or one with type parameter):
- `savePortForwardRule(id)` - Hard-codes `type: 'port_forward'`
- `saveOneToOneRule(id)` - Hard-codes `type: 'one_to_one'`
- `saveOutboundRule(id)` - Hard-codes `type: 'outbound'`

Or keep single `saveRule(id, type)` but ensure type is passed correctly.

### Phase 2: Modal HTML Structure

#### 2.1 Port Forwarding Modal
**Fields to Include**:
- Interface (required) - Dropdown from interfaces API
- Address Family (IPv4/IPv6/Dual) - Default IPv4
- Protocol (TCP/UDP/TCP+UDP/ICMP) - Required
- Source Type/Value/Port (optional filtering)
- Destination IP/Port (required) - External destination
- Redirect Target IP/Port (required) - Internal target
- Reflection Mode (default/proxy/nat/disabled)
- Schedule (optional)
- Description (optional)
- Enabled (checkbox)

**Fields to Exclude**:
- TYPE selector (removed)
- Fields not relevant to port forwarding

#### 2.2 1:1 NAT Modal
**Fields to Include**:
- Interface (required) - Dropdown from interfaces API
- Address Family (IPv4/IPv6/Dual) - Default IPv4, show IPv6 warning
- Source IP (External) (required) - Single IP only
- Destination IP (Internal) (required) - Single IP only
- Redirect Target IP (required)
- Reflection Mode (default/proxy/nat/disabled)
- Description (optional)
- Enabled (checkbox)

**Fields to Exclude**:
- TYPE selector (removed)
- Protocol (not applicable for 1:1)
- Port fields (not applicable for 1:1)
- Source/Destination Type selectors (always "single" for 1:1)

#### 2.3 Outbound NAT Modal
**Fields to Include**:
- Interface (required) - Dropdown from interfaces API
- Address Family (IPv4/IPv6/Dual) - Supports all
- Protocol (TCP/UDP/TCP+UDP/ICMP/Any) - Default Any
- Source Type/Value/Port (optional filtering)
- Destination Type/Value/Port (optional filtering)
- NAT Target IP (SNAT) (required) - Can be interface IP or specific IP
- NAT Target Port (optional) - For port translation
- Reflection Mode (default/proxy/nat/disabled)
- Schedule (optional)
- Description (optional)
- Enabled (checkbox)

**Fields to Exclude**:
- TYPE selector (removed)

### Phase 3: Integration Verification

#### 3.1 Backend Integration
**Verification Points**:
- ✅ `NatController` accepts `type` field (no changes needed)
- ✅ `FirewallNatManager` stores `Type` in database (no changes needed)
- ✅ `FirewallApplyManager` reads `Type` field for nftables generation (no changes needed)

**Test Cases**:
1. Create port forward rule → Verify `type: "port_forward"` in database
2. Create 1:1 NAT rule → Verify `type: "one_to_one"` in database
3. Create outbound rule → Verify `type: "outbound"` in database
4. Apply rules → Verify nftables config generated correctly

#### 3.2 Interface Integration
**Verification Points**:
- ✅ Interface dropdown populated from `/interfaces/assignments` API
- ✅ Interface names displayed correctly
- ✅ Interface validation works

**Test Cases**:
1. Load page → Verify interfaces load in all three modals
2. Select interface → Verify interface saved correctly
3. Apply rules → Verify nftables uses correct interface names

#### 3.3 Alias Integration
**Verification Points**:
- ✅ Alias datalist populated from `/firewall/aliases` API
- ✅ Alias autocomplete works in source/destination fields
- ✅ Alias resolution works in nftables generation

**Test Cases**:
1. Load page → Verify aliases load in datalist
2. Use alias in rule → Verify alias resolved in nftables config
3. Edit rule with alias → Verify alias displayed correctly

#### 3.4 Schedule Integration
**Verification Points**:
- ✅ Schedule dropdown populated from `/firewall/schedules` API
- ✅ Schedule filtering works in rule application
- ✅ Schedule stored correctly in database

**Test Cases**:
1. Load page → Verify schedules load in dropdown
2. Assign schedule to rule → Verify schedule saved
3. Apply rules → Verify scheduled rules filtered correctly

#### 3.5 nftables Integration
**Verification Points**:
- ✅ Port Forward rules → Generate DNAT rules in PREROUTING chain
- ✅ 1:1 NAT rules → Generate DNAT rules in PREROUTING chain (full IP mapping)
- ⚠️ Outbound rules → **NEEDS VERIFICATION**: Currently `BuildNatRule()` generates DNAT rules only
  - May need backend update to generate SNAT rules in POSTROUTING chain for outbound type
  - Or outbound rules may be handled via masquerade rules
- ✅ Rule ordering preserved
- ✅ Interface names resolved correctly
- ✅ Alias resolution works

**Test Cases**:
1. Create port forward rule → Verify DNAT rule in nftables config (PREROUTING)
2. Create 1:1 NAT rule → Verify DNAT rule in nftables config (PREROUTING)
3. Create outbound rule → **VERIFY**: Check if SNAT rule generated in POSTROUTING chain
4. Apply rules → Verify nftables config applied successfully
5. Verify rules → Run `nft list ruleset` to confirm rules exist

**Note**: If outbound NAT rules are not generating SNAT rules, backend changes may be needed in `FirewallApplyManager.BuildNatRule()` to check `rule.Type == "outbound"` and generate SNAT instead of DNAT.

### Phase 4: Implementation Details

#### 4.1 JavaScript Function Signatures

```javascript
// Port Forwarding Modal
showPortForwardModal: function(rule) {
    // rule can be null (new) or existing rule object
    // Type is always 'port_forward'
    // Show: Interface, Protocol, Source/Destination with ports, Redirect Target with port
}

// 1:1 NAT Modal
showOneToOneModal: function(rule) {
    // rule can be null (new) or existing rule object
    // Type is always 'one_to_one'
    // Show: Interface, Source IP, Destination IP, Redirect Target IP (no ports)
}

// Outbound NAT Modal
showOutboundModal: function(rule) {
    // rule can be null (new) or existing rule object
    // Type is always 'outbound'
    // Show: Interface, Protocol, Source/Destination, NAT Target IP/Port
}
```

#### 4.2 Active Tab Detection

```javascript
attachEventHandlers: function() {
    // Track active tab
    this.activeTab = 'port_forward'; // Default
    
    // Tab change handler
    $(document).on('shown.bs.tab', '#natTabs button[data-bs-toggle="tab"]', (e) => {
        const tabButton = $(e.target);
        const natType = tabButton.data('nat-type');
        if (natType) {
            this.activeTab = natType;
        }
    });
    
    // Add Rule button - use active tab
    $(document).on('click', '#btnAddRule', () => {
        if (this.activeTab === 'port_forward') {
            this.showPortForwardModal(null);
        } else if (this.activeTab === 'one_to_one') {
            this.showOneToOneModal(null);
        } else if (this.activeTab === 'outbound') {
            this.showOutboundModal(null);
        }
    });
}
```

#### 4.3 Edit Handler Updates

```javascript
// In renderRulesByType, update edit buttons
$(document).on('click', '[data-action="edit-nat"]', (e) => {
    const id = $(e.currentTarget).data('id');
    const rule = this.rules.find(r => r.id === id);
    if (rule) {
        if (rule.type === 'port_forward') {
            this.showPortForwardModal(rule);
        } else if (rule.type === 'one_to_one') {
            this.showOneToOneModal(rule);
        } else if (rule.type === 'outbound') {
            this.showOutboundModal(rule);
        }
    }
});
```

#### 4.4 Save Function Updates

```javascript
savePortForwardRule: async function(id) {
    const rule = {
        type: 'port_forward', // Hard-coded
        interface: $('#pfInterface').val(),
        // ... other fields
    };
    // ... rest of save logic
}

saveOneToOneRule: async function(id) {
    const rule = {
        type: 'one_to_one', // Hard-coded
        interface: $('#otoInterface').val(),
        protocol: 'any', // Default for 1:1
        sourcePort: null, // Clear ports
        destinationPort: null,
        redirectTargetPort: null,
        // ... other fields
    };
    // ... rest of save logic
}

saveOutboundRule: async function(id) {
    const rule = {
        type: 'outbound', // Hard-coded
        interface: $('#obInterface').val(),
        // ... other fields
    };
    // ... rest of save logic
}
```

### Phase 5: Field-Specific Details

#### 5.1 Port Forwarding Fields
- **Interface**: Required, dropdown from interfaces API
- **Address Family**: Default IPv4, show warning for IPv6
- **Protocol**: Required, TCP/UDP/TCP+UDP/ICMP
- **Source**: Optional filtering (Type/Value/Port)
- **Destination IP**: Required (external IP)
- **Destination Port**: Required (external port)
- **Redirect Target IP**: Required (internal IP)
- **Redirect Target Port**: Required (internal port)
- **Reflection Mode**: Default/proxy/nat/disabled
- **Schedule**: Optional
- **Description**: Optional
- **Enabled**: Checkbox, default true

#### 5.2 1:1 NAT Fields
- **Interface**: Required, dropdown from interfaces API
- **Address Family**: Default IPv4, show warning for IPv6
- **Source IP**: Required (external IP, single IP only)
- **Destination IP**: Required (internal IP, single IP only)
- **Redirect Target IP**: Required
- **Reflection Mode**: Default/proxy/nat/disabled
- **Description**: Optional
- **Enabled**: Checkbox, default true

**Note**: Protocol, ports, and source/destination type selectors are NOT shown (always "single" for IPs, no ports).

#### 5.3 Outbound NAT Fields
- **Interface**: Required, dropdown from interfaces API
- **Address Family**: IPv4/IPv6/Dual (all supported)
- **Protocol**: Default "any", TCP/UDP/TCP+UDP/ICMP/Any
- **Source**: Optional filtering (Type/Value/Port)
- **Destination**: Optional filtering (Type/Value/Port)
- **NAT Target IP**: Required (SNAT target - can be interface name or IP)
- **NAT Target Port**: Optional (for port translation)
- **Reflection Mode**: Default/proxy/nat/disabled
- **Schedule**: Optional
- **Description**: Optional
- **Enabled**: Checkbox, default true

### Phase 6: Validation

#### 6.1 Port Forwarding Validation
- Interface: Required
- Protocol: Required
- Destination IP: Required, valid IP address
- Destination Port: Required, valid port (1-65535)
- Redirect Target IP: Required, valid IP address
- Redirect Target Port: Required, valid port (1-65535)
- Address Family: If IPv6, show warning (port forward not supported for pure IPv6)

#### 6.2 1:1 NAT Validation
- Interface: Required
- Source IP: Required, valid single IP address (not network)
- Destination IP: Required, valid single IP address (not network)
- Redirect Target IP: Required, valid IP address
- Address Family: If IPv6, show warning (1:1 NAT not supported for pure IPv6)

#### 6.3 Outbound NAT Validation
- Interface: Required
- NAT Target IP: Required (can be interface name or IP address)
- Protocol: Optional (defaults to "any")
- Address Family: All supported (IPv4, IPv6, Dual)

### Phase 7: Testing Checklist

#### 7.1 Functional Testing
- [ ] Port Forward tab → Click "Add Rule" → Port Forward modal opens (no type selector)
- [ ] 1:1 NAT tab → Click "Add Rule" → 1:1 NAT modal opens (no type selector)
- [ ] Outbound tab → Click "Add Rule" → Outbound modal opens (no type selector)
- [ ] Edit port forward rule → Port Forward modal opens with correct fields
- [ ] Edit 1:1 NAT rule → 1:1 NAT modal opens with correct fields
- [ ] Edit outbound rule → Outbound modal opens with correct fields
- [ ] Create port forward rule → Rule saved with `type: "port_forward"`
- [ ] Create 1:1 NAT rule → Rule saved with `type: "one_to_one"`
- [ ] Create outbound rule → Rule saved with `type: "outbound"`
- [ ] Delete rules → Rules deleted correctly
- [ ] Reorder rules → Rules reordered correctly

#### 7.2 Integration Testing
- [ ] Interfaces load in all three modals
- [ ] Aliases available in datalist for all modals
- [ ] Schedules load in modals that support them
- [ ] Apply changes → nftables config generated correctly
- [ ] Verify nftables rules → Rules appear in `nft list ruleset`
- [ ] Port forward rules → DNAT rules in PREROUTING
- [ ] 1:1 NAT rules → DNAT rules in PREROUTING
- [ ] Outbound rules → SNAT rules in POSTROUTING

#### 7.3 Validation Testing
- [ ] Port Forward: Required fields validated
- [ ] 1:1 NAT: Required fields validated
- [ ] Outbound: Required fields validated
- [ ] IPv6 warnings shown for port forward/1:1 NAT
- [ ] Invalid IP addresses rejected
- [ ] Invalid ports rejected

#### 7.4 UX Testing
- [ ] Modals are focused and clear
- [ ] No confusing type selector
- [ ] Fields appropriate for each type
- [ ] Helpful placeholders and labels
- [ ] Error messages clear and helpful

## File Changes Summary

### Files to Modify
1. **`src/Monolith.FireWall.WebUI/wwwroot/js/nat.js`**
   - Replace `showRuleModal()` with three separate functions
   - Update `attachEventHandlers()` to track active tab
   - Update `#btnAddRule` handler to call type-specific modal
   - Update edit handlers to call type-specific modal
   - Create three save functions (or update existing)
   - Remove `toggleNatFieldsByType()` function
   - Remove type change handlers

### Files to Verify (No Changes Expected)
1. **`src/Monolith.FireWall.WebUI/Pages/Firewall/Nat/Config.cshtml`** - No changes (tabs already exist)
2. **`src/Monolith.FireWall.WebUI/Features/Firewall/Nat/NatController.cs`** - No changes (handles all types)
3. **`src/Monolith.FireWall.Core/Services/Firewall/FirewallNatManager.cs`** - No changes (stores type)
4. **`src/Monolith.FireWall.Core/Services/Firewall/FirewallApplyManager.cs`** - **MAY NEED CHANGES**: 
   - Currently `BuildNatRule()` only generates DNAT rules
   - Outbound NAT rules may need SNAT generation in POSTROUTING chain
   - Verify if outbound rules work correctly, or add SNAT support if needed

## Implementation Steps

### Step 1: Create Port Forward Modal Function
1. Copy `showRuleModal()` to `showPortForwardModal()`
2. Remove TYPE selector dropdown
3. Remove type change handler
4. Hard-code type-specific fields (show ports, redirect port)
5. Update modal title to "Port Forwarding"
6. Update save handler to call `savePortForwardRule()`

### Step 2: Create 1:1 NAT Modal Function
1. Copy `showRuleModal()` to `showOneToOneModal()`
2. Remove TYPE selector dropdown
3. Remove protocol field (not applicable)
4. Remove port fields (not applicable)
5. Simplify source/destination to single IP inputs
6. Update modal title to "1:1 NAT"
7. Update save handler to call `saveOneToOneRule()`

### Step 3: Create Outbound NAT Modal Function
1. Copy `showRuleModal()` to `showOutboundModal()`
2. Remove TYPE selector dropdown
3. Update labels (NAT Target IP instead of Redirect Target IP)
4. Show all fields (ports, protocol, etc.)
5. Update modal title to "Outbound NAT"
6. Update save handler to call `saveOutboundRule()`

### Step 4: Update Event Handlers
1. Track active tab in `attachEventHandlers()`
2. Update `#btnAddRule` to call appropriate modal based on active tab
3. Update edit handlers to call appropriate modal based on rule type
4. Remove old `showRuleModal()` calls

### Step 5: Update Save Functions
1. Create `savePortForwardRule(id)` - hard-code `type: 'port_forward'`
2. Create `saveOneToOneRule(id)` - hard-code `type: 'one_to_one'`, clear ports
3. Create `saveOutboundRule(id)` - hard-code `type: 'outbound'`
4. Or update single `saveRule(id, type)` to accept type parameter

### Step 6: Cleanup
1. Remove `toggleNatFieldsByType()` function
2. Remove type change handlers
3. Remove unused code
4. Test all three modals

### Step 7: Testing
1. Test each modal independently
2. Test create/edit/delete for each type
3. Test apply/discard workflow
4. Verify nftables integration
5. Verify interface/alias/schedule integration

## Benefits

1. **Clearer UX**: Each modal is focused on its specific type
2. **No Confusion**: No type selector to accidentally change
3. **Better Validation**: Type-specific validation per modal
4. **Cleaner Code**: Separate functions are easier to maintain
5. **Type Safety**: Type is hard-coded, reducing errors

## Risk Assessment

**Low Risk**:
- Backend already supports all types
- No database changes needed
- No API changes needed
- Integration points remain the same
- Can test incrementally

**Mitigation**:
- Test each modal independently
- Verify backend integration after each change
- Keep old code commented during transition
- Test apply workflow thoroughly

## Timeline Estimate

- **Step 1-3 (Create Modals)**: 2-3 hours
- **Step 4 (Event Handlers)**: 1 hour
- **Step 5 (Save Functions)**: 1 hour
- **Step 6 (Cleanup)**: 30 minutes
- **Step 7 (Testing)**: 2-3 hours
- **Total**: ~6-8 hours

## Success Criteria

1. ✅ Three separate modals (one per type)
2. ✅ No TYPE selector in any modal
3. ✅ Type-specific fields shown/hidden correctly
4. ✅ All integrations work (nftables, interfaces, aliases, schedules)
5. ✅ Rules save with correct type
6. ✅ nftables config generated correctly
7. ✅ Apply/discard workflow works
8. ✅ Edit/delete operations work correctly
