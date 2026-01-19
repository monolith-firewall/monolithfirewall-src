# ISO Network Configuration Fix

## Issue
After ISO installation, network interfaces don't get IP addresses automatically.

## Root Cause
- During installation, network is configured via Debian installer's netcfg
- After installation, ifupdown2 needs to be configured to bring up interfaces
- DHCP client (isc-dhcp-client) may not be running automatically
- Interfaces need to be explicitly brought up on first boot

## Solution Implemented

### 1. Preseed Configuration
- Added network timeout and DHCP configuration
- Ensures installer properly configures network during installation

### 2. Install Script Enhancement
- `install-packages.sh` now creates basic network configuration
- Automatically brings up interfaces using `ifreload -a`

### 3. First Boot Script Enhancement
- `monolith-firstboot.sh` now ensures interfaces are up after package installation
- Shows network interface status for debugging

## Manual Fix (if needed)

If interfaces still don't get IPs after installation:

1. **Check interface status:**
   ```bash
   ip addr show
   ```

2. **Bring up interfaces manually:**
   ```bash
   ifreload -a
   # Or for specific interface:
   ifup eth0
   ```

3. **Check DHCP client:**
   ```bash
   systemctl status isc-dhcp-client
   systemctl start isc-dhcp-client
   ```

4. **Configure via Monolith:**
   - Once Monolith is running, configure interfaces via web UI
   - Or use CLI: `monolith interface assign <interface> --role lan --ip-mode dhcp`

## Notes
- DHCP server (isc-dhcp-server) and DHCP client (isc-dhcp-client) can coexist
- The server only runs when configured via Monolith
- The client is used to get IPs for management interfaces
