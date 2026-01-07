# WAN to LAN Routing Fix Plan

## Problem Statement
Routing from ens18 (WAN) to ens19 (LAN 192.168.0.1) is not working. Need to:
1. Fix the routing functionality
2. Make it configurable through the WebUI
3. Ensure it works with gateway settings
4. Verify interface, firewall, and routing configuration

## Current State Analysis

### ✅ What's Working
- IP forwarding is enabled (`net.ipv4.ip_forward = 1`)
- Routing table shows:
  - Default gateway: `10.100.0.1 via ens18` (WAN)
  - LAN network: `192.168.0.0/24 via ens19`
- Firewall forward rules exist:
  - `iifname "ens18" oifname "ens19" accept` (WAN→LAN)
  - `iifname "ens19" oifname "ens18" accept` (LAN→WAN)

### ❌ What's Broken
- **NAT table is missing** - No masquerade rules for outbound traffic
- **No automatic IP forwarding enablement** - Must be manually enabled
- **No routing status display** - Can't see routing state in UI
- **No routing configuration UI** - Can't configure routing settings
- **No gateway management** - Can't view/configure default gateway

## Implementation Plan

### Phase 1: Core Routing Infrastructure (Backend)

#### 1.1 Auto-Enable IP Forwarding
**File**: `src/Monolith.FireWall.Core/Services/InterfaceAssignmentManager.cs`
- When WAN/LAN interfaces are assigned, automatically enable IP forwarding
- Check if WAN and/or LAN interfaces exist
- If both exist, enable `net.ipv4.ip_forward` via `SystemTuneablesManager`
- Also enable `net.ipv6.conf.all.forwarding` for IPv6 support
- Log the action for audit trail

**Implementation**:
```csharp
// In SaveAssignmentAsync or ApplyNowAsync
var hasWan = assignments.Any(a => a.Role == InterfaceRole.Wan);
var hasLan = assignments.Any(a => a.Role == InterfaceRole.Lan);
if (hasWan && hasLan)
{
    // Auto-enable IP forwarding
    await EnableIpForwardingAsync(cancellationToken);
}
```

#### 1.2 Ensure NAT Table Creation
**File**: `src/Monolith.FireWall.Core/Services/Firewall/FirewallApplyManager.cs`
- ✅ Already fixed: NAT table now created when WAN interfaces exist
- **Verify**: Ensure the fix is working correctly
- **Test**: Rebuild firewall config and verify NAT table is created

#### 1.3 Add Routing Status API
**File**: `src/Monolith.FireWall.Core/Transport/Handlers/RoutingHandler.cs` (extend)
- Add endpoint to get routing status:
  - IP forwarding status (IPv4/IPv6)
  - Current routing table
  - Default gateway
  - Interface routing info
  - NAT masquerade status

**New Action**: `routing.status`

#### 1.4 Add Gateway Management
**File**: `src/Monolith.FireWall.Core/Services/RoutingManager.cs` (extend)
- Get current default gateway
- Set default gateway for WAN interface
- View routing table entries
- Test gateway connectivity

### Phase 2: WebUI - Routing Status Display

#### 2.1 Add Routing Status Section to Interfaces Page
**File**: `src/Monolith.FireWall.WebUI/wwwroot/js/pages/interfaces.js`
- Add new tab or section: "Routing Status"
- Display:
  - IP forwarding status (enabled/disabled) with toggle
  - Current default gateway
  - Routing table summary
  - NAT masquerade status
  - Interface routing roles (WAN/LAN)

**UI Elements**:
```html
<div class="card">
  <div class="card-header">
    <h5>Routing Status</h5>
  </div>
  <div class="card-body">
    <div class="row">
      <div class="col-md-6">
        <strong>IP Forwarding:</strong>
        <span id="ip-forwarding-status">Checking...</span>
        <button id="toggle-ip-forwarding" class="btn btn-sm btn-primary">Enable</button>
      </div>
      <div class="col-md-6">
        <strong>Default Gateway:</strong>
        <span id="default-gateway">10.100.0.1 via ens18</span>
      </div>
    </div>
    <div class="mt-3">
      <strong>Routing Table:</strong>
      <table class="table table-sm">
        <!-- Routes -->
      </table>
    </div>
  </div>
</div>
```

#### 2.2 Add Routing Configuration Section
**File**: `src/Monolith.FireWall.WebUI/wwwroot/js/pages/interfaces.js`
- Add routing configuration panel
- Options:
  - Enable/disable routing
  - Configure default gateway
  - View/edit routing table
  - Test routing connectivity

### Phase 3: WebUI - Gateway Configuration

#### 3.1 Gateway Display
**File**: `src/Monolith.FireWall.WebUI/wwwroot/js/pages/interfaces.js`
- Show current default gateway
- Display gateway for each WAN interface
- Show gateway status (reachable/unreachable)

#### 3.2 Gateway Configuration
- Allow setting default gateway for WAN interface
- Validate gateway IP address
- Test gateway connectivity
- Apply gateway changes

### Phase 4: Integration & Testing

#### 4.1 Integration Points
1. **Interface Assignment** → Auto-enable IP forwarding
2. **Firewall Apply** → Ensure NAT table with masquerade
3. **Routing Status** → Display in UI
4. **Gateway Config** → Manage default gateway

#### 4.2 Testing Checklist
- [ ] IP forwarding auto-enabled when WAN+LAN configured
- [ ] NAT table created with masquerade rules
- [ ] Firewall forward rules allow WAN↔LAN traffic
- [ ] Routing status displays correctly
- [ ] Gateway configuration works
- [ ] Routing test functionality works
- [ ] Changes persist across reboots

## File Changes Summary

### Backend Files
1. `src/Monolith.FireWall.Core/Services/InterfaceAssignmentManager.cs`
   - Add auto-enable IP forwarding logic
   
2. `src/Monolith.FireWall.Core/Services/Firewall/FirewallApplyManager.cs`
   - ✅ Already fixed: NAT table creation
   - Verify fix works

3. `src/Monolith.FireWall.Core/Transport/Handlers/RoutingHandler.cs`
   - Add `routing.status` action
   - Add `routing.gateway.get` action
   - Add `routing.gateway.set` action

4. `src/Monolith.FireWall.Core/Services/RoutingManager.cs`
   - Extend with gateway management methods
   - Add routing status methods

### Frontend Files
1. `src/Monolith.FireWall.WebUI/wwwroot/js/pages/interfaces.js`
   - Add routing status display
   - Add routing configuration UI
   - Add gateway management UI

2. `src/Monolith.FireWall.WebUI/wwwroot/css/interfaces.css` (if exists)
   - Add styling for routing sections

## Implementation Order

1. **First**: Fix NAT table creation (verify existing fix works)
2. **Second**: Auto-enable IP forwarding when WAN/LAN configured
3. **Third**: Add routing status API endpoints
4. **Fourth**: Add routing status display to UI
5. **Fifth**: Add gateway configuration
6. **Sixth**: Add routing test functionality
7. **Seventh**: Integration testing

## Success Criteria

- ✅ Routing works from WAN to LAN
- ✅ Routing works from LAN to WAN (with NAT masquerade)
- ✅ IP forwarding auto-enabled when WAN/LAN configured
- ✅ Routing status visible in WebUI
- ✅ Gateway configurable through WebUI
- ✅ All changes persist across reboots
- ✅ Firewall rules allow routing traffic

## Notes

- IP forwarding must be enabled for routing to work
- NAT masquerade is required for LAN→WAN outbound traffic
- Firewall forward rules must allow WAN↔LAN traffic
- Default gateway must be configured on WAN interface
- All routing settings should persist across reboots
