# WAN Interface Auto-Outbound Rule Plan

## Overview
When an interface is assigned as WAN type, automatically create an outbound NAT rule to allow internet traffic through it. This integrates with the existing Firewall > NAT > Outbound page.

## Current State

### Routing Status Page
- Location: `src/Monolith.FireWall.WebUI/wwwroot/js/pages/routing-status.js`
- Contains routing configuration section (lines 165-184) that needs to be removed
- Shows IP forwarding toggle and apply button

### NAT Outbound Page
- Location: `src/Monolith.FireWall.WebUI/wwwroot/js/nat.js`
- Has outbound tab that displays outbound NAT rules
- Supports creating/editing outbound rules via `showOutboundModal()`

### Interface Assignment
- Location: `src/Monolith.FireWall.Core/Services/InterfaceAssignmentManager.cs`
- `CreateOrUpdateAssignmentAsync()` method saves interface assignments
- Currently auto-enables IP forwarding when WAN/LAN interfaces are configured (line 338)

### NAT Manager
- Location: `src/Monolith.FireWall.Core/Services/Firewall/FirewallNatManager.cs`
- `CreateRuleAsync()` method creates NAT rules
- Supports "outbound" type rules
- Outbound rules have: Type="outbound", Interface, AddressFamily, Protocol, Source/Destination

## Implementation Plan

### Phase 1: Remove Routing Configuration from Routing Status Page
1. Remove the "Routing Configuration" section (lines 165-184) from `routing-status.js`
2. Keep only the status display (IP forwarding status, NAT masquerade status, default gateway, routing table)
3. Remove handlers for `#routing-toggle-ip-forwarding` and `#routing-apply-ip-forwarding`

### Phase 2: Auto-Create Outbound Rule on WAN Assignment
1. In `InterfaceAssignmentManager.CreateOrUpdateAssignmentAsync()`:
   - After saving assignment (line 331), check if role is WAN
   - If WAN and assignment is new OR role changed to WAN:
     - Check if outbound rule already exists for this interface
     - If not, create automatic outbound rule:
       - Type: "outbound"
       - Interface: WAN interface name
       - AddressFamily: "ipv4" (and optionally "ipv6" if IPv6 is configured)
       - Protocol: "any"
       - SourceType: "any"
       - DestinationType: "any"
       - Description: "Auto: Outbound NAT for WAN interface {interface}"
       - Enabled: true
   - If role changed FROM WAN to something else:
     - Find and remove auto-created outbound rule for this interface

### Phase 3: Integration with NAT Outbound Page
1. Ensure auto-created rules are visible in the outbound tab
2. Mark auto-created rules (e.g., via description prefix "Auto:")
3. Optionally disable editing/deletion of auto-created rules, or allow with warning
4. Show indicator in UI that rule is auto-managed

### Phase 4: Cleanup Logic
1. When interface assignment is deleted:
   - Remove associated auto-created outbound rule
2. When interface role changes from WAN to non-WAN:
   - Remove auto-created outbound rule
3. When interface role changes from non-WAN to WAN:
   - Create auto-created outbound rule if it doesn't exist

## Technical Details

### Outbound Rule Structure
```csharp
var outboundRule = new FirewallNatRuleRequest
{
    Type = "outbound",
    Interface = wanInterfaceName,
    AddressFamily = "ipv4", // or "ipv6" if needed
    Protocol = "any",
    SourceType = "any",
    SourceValue = null,
    DestinationType = "any",
    DestinationValue = null,
    Description = $"Auto: Outbound NAT for WAN interface {wanInterfaceName}",
    Enabled = true
};
```

### Detection Logic
- Check if outbound rule exists: Query NAT rules where Type="outbound" AND Interface=wanInterfaceName AND Description starts with "Auto:"
- This ensures we don't duplicate auto-created rules

### Dependencies
- `FirewallNatManager` - for creating/deleting NAT rules
- `InterfaceAssignmentManager` - for detecting role changes
- Need to inject `FirewallNatManager` into `InterfaceAssignmentManager` constructor

## Files to Modify

1. `src/Monolith.FireWall.WebUI/wwwroot/js/pages/routing-status.js`
   - Remove routing configuration section

2. `src/Monolith.FireWall.Core/Services/InterfaceAssignmentManager.cs`
   - Add `FirewallNatManager` dependency
   - Add logic to create/remove outbound rules on WAN assignment/removal

3. `src/Monolith.FireWall.Core/Program.cs`
   - Update `InterfaceAssignmentManager` constructor to include `FirewallNatManager`

4. `src/Monolith.FireWall.WebUI/wwwroot/js/nat.js` (optional)
   - Add visual indicator for auto-created rules
   - Optionally prevent editing/deletion of auto-created rules

## Testing Checklist

- [ ] Assign interface as WAN → outbound rule created automatically
- [ ] Change interface from WAN to LAN → outbound rule removed automatically
- [ ] Change interface from LAN to WAN → outbound rule created automatically
- [ ] Delete WAN interface assignment → outbound rule removed automatically
- [ ] Outbound rule appears in Firewall > NAT > Outbound page
- [ ] Routing Status page no longer shows routing configuration section
- [ ] Manual outbound rules are not affected
- [ ] Multiple WAN interfaces each get their own outbound rule
