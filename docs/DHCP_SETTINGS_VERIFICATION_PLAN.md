# DHCP Settings Verification and Fix Plan

## Issue
User enabled DHCP mode for ens19 interface, but the configuration file still shows it as static (192.168.0.1). Need to verify:
1. DHCP settings are saved to database
2. DHCP settings are written to `/etc/network/interfaces.d/monolith`
3. Configuration is applied after saving

## Current State
- Interface assignment modal has "Save" button that saves to database
- Configuration file is only written when "Apply Config" is clicked
- No automatic application after saving

## Changes Made

### 1. Added "Save & Apply" Button
- Modified `interfaces.js` to add a "Save & Apply" button in addition to "Save"
- "Save" button: Saves to database only
- "Save & Apply" button: Saves to database AND immediately applies configuration

### 2. DHCP Configuration Flow
When DHCP mode is selected:
1. `ipMode: "dhcp"` is sent in the payload
2. `InterfaceAssignmentManager.SaveAssignmentAsync()` parses it to `InterfaceIpMode.Dhcp`
3. Saved to database in `InterfaceAssignmentEntity.IpMode`
4. When config is applied, `InterfaceConfigManager.BuildStanza()` writes:
   ```
   auto ens19
   iface ens19 inet dhcp
   ```

## Verification Steps

### Check Database
```sql
SELECT InterfaceName, IpMode FROM interface_assignments WHERE InterfaceName = 'ens19';
```
Expected: `IpMode = 1` (which is `InterfaceIpMode.Dhcp`)

### Check Config File
```bash
grep -A 3 "ens19" /etc/network/interfaces.d/monolith
```
Expected:
```
auto ens19
iface ens19 inet dhcp
```

### Check if Applied
After clicking "Save & Apply", the interface should be restarted with DHCP mode.

## DHCP Server Configuration (Separate)
The user mentioned "192.168.0.x" which suggests DHCP server configuration. This is handled by the `monolith-network` package, not the core interface assignment. The interface assignment only configures:
- DHCP client mode (interface gets IP from DHCP server)
- Static IP mode (interface has fixed IP)

DHCP server configuration (serving IPs to clients) is a separate module in the monolith-network package.

## Next Steps
1. Test saving with DHCP mode selected
2. Verify database entry
3. Verify config file is written correctly
4. Test "Save & Apply" button
5. If DHCP server configuration is needed, check monolith-network package
