# ISO Package Installation - Complete Guide

## Overview

The ISO builder automatically includes ALL monolith packages and installs them on first boot, providing a complete firewall system out of the box.

## Package Build Process

### Step 1: Build All Packages

Before creating the ISO, all monolith packages are built:

```bash
./build-scripts/build-all-packages.sh
```

This script:
- Scans `tmp/monolithfirewall-packages/` for all packages
- Builds each package using `package-mfwpkg.sh`
- Outputs all `.mfwpkg` files to `build/packages/`

**Default packages included:**
- `monolith-network` - DHCP and DNS management
- `monolith-vpn` - IPsec, OpenVPN, and WireGuard
- `monolith-diagnostics` - System diagnostics

### Step 2: ISO Builder Automatically Includes Packages

The ISO builder (`build-iso.sh`) now:
1. **Automatically builds packages** if they don't exist in `build/packages/`
2. **Includes ALL `.mfwpkg` files** from `build/packages/` in the ISO
3. **Places them** in `/monolith-packages/` on the ISO

```bash
./build-scripts/build-iso.sh
```

The ISO will contain:
- All dependency .deb packages (for offline installation)
- ALL monolith packages (.mfwpkg files) in `monolith-packages/` directory

## First Boot Installation

### Automatic Installation Process

1. **During ISO Installation** (Preseed):
   - Copies ALL `.mfwpkg` files from ISO to `/var/lib/monolith-firewall/packages/`

2. **On First Boot** (`monolith-firstboot.service`):
   - Waits for Core service to be ready
   - Installs **ALL packages** found in `/var/lib/monolith-firewall/packages/`
   - Uses `monolith` CLI tool (or `socat` as fallback)
   - Removes first boot flag after completion
   - Restarts Core service to load packages

### Installation Order

Packages are installed in the order they're found. The first boot script:
- Installs **every** `.mfwpkg` file it finds
- Reports success/failure for each package
- Continues even if one package fails
- Shows summary at the end

## Verification

### Check Packages in ISO

```bash
# Mount ISO
sudo mount -o loop monolith-firewall-1.0.0-amd64.iso /mnt

# List packages
ls -lh /mnt/monolith-packages/

# Unmount
sudo umount /mnt
```

### Check Packages After Installation

```bash
# List installed packages
monolith package list

# Check first boot logs
journalctl -u monolith-firstboot.service

# Check if first boot completed
ls -la /var/lib/monolith-firewall/.firstboot
# (Should not exist if first boot completed)
```

## Adding More Default Packages

To add more default packages:

1. Add package to `tmp/monolithfirewall-packages/` (via `setup-dependencies.sh`)
2. Build packages: `./build-scripts/build-all-packages.sh`
3. Build ISO: `./build-scripts/build-iso.sh`
4. Packages will be automatically included and installed

## Troubleshooting

### Packages Not Installing

```bash
# Check if packages were copied during install
ls -la /var/lib/monolith-firewall/packages/

# Manually trigger first boot
sudo touch /var/lib/monolith-firewall/.firstboot
sudo systemctl start monolith-firstboot.service

# Check Core service
sudo systemctl status monolith-firewall-core.service

# Check socket
ls -la /var/lib/monolith-firewall/run/monolith-core.sock
```

### Manual Package Installation

If automatic installation fails:

```bash
# Install packages manually
for pkg in /var/lib/monolith-firewall/packages/*.mfwpkg; do
    monolith package install "$pkg" --overwrite
done
```

## Summary

✅ **ISO includes ALL packages** from `build/packages/`  
✅ **First boot installs ALL packages** automatically  
✅ **No manual intervention** required  
✅ **Complete firewall system** ready after first boot  
