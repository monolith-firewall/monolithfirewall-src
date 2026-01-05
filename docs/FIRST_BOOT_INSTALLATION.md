# First Boot Package Installation

## Overview

Monolith FireWall automatically installs packages from the ISO on first boot, ensuring all included packages are ready to use immediately after installation.

## How It Works

### 1. During ISO Installation (Preseed)

The preseed configuration (`iso-build/preseed.cfg`) copies all `.mfwpkg` files from the ISO to the target system:

```bash
in-target cp /media/cdrom/monolith-packages/*.mfwpkg /var/lib/monolith-firewall/packages/
```

### 2. Package Installation (Debian Postinst)

The `debian/postinst` script:
- Creates the first boot flag: `/var/lib/monolith-firewall/.firstboot`
- Enables the `monolith-firstboot.service` systemd service
- Sets up all necessary directories

### 3. First Boot Service

The `monolith-firstboot.service` systemd service:
- Runs **once** on first boot (after Core service is ready)
- Waits for Core service socket to be available
- Installs all packages from `/var/lib/monolith-firewall/packages/`
- Also checks `/media/cdrom/monolith-packages/` as fallback
- Removes the first boot flag after completion
- Restarts Core service to load newly installed packages

### 4. Package Installation Method

The first boot script (`debian/monolith-firstboot.sh`) uses the Unix socket API to install packages:

1. **Primary method**: Uses `monolith` CLI tool (if available)
2. **Fallback method**: Uses `socat` to communicate directly with Core via Unix socket
3. **API call**: Sends JSON request to `packages.install` action

## Files Created

- `debian/monolith-firstboot.service` - Systemd service file
- `debian/monolith-firstboot.sh` - Installation script
- Updated `debian/postinst` - Creates first boot flag
- Updated `debian/rules` - Installs first boot service and script
- Updated `iso-build/preseed.cfg` - Copies packages during installation

## Dependencies

- `socat` - Added to debian/control for Unix socket communication
- Core service must be running (service waits for socket)

## Installation Flow

```
ISO Installation
    ↓
Preseed copies .mfwpkg files to /var/lib/monolith-firewall/packages/
    ↓
Debian package installs (postinst)
    ↓
Creates /var/lib/monolith-firewall/.firstboot flag
    ↓
Enables monolith-firstboot.service
    ↓
System boots for first time
    ↓
monolith-firstboot.service starts
    ↓
Waits for Core service socket
    ↓
Installs all packages from /var/lib/monolith-firewall/packages/
    ↓
Removes .firstboot flag
    ↓
Restarts Core service
    ↓
Packages are loaded and ready!
```

## Manual Installation

If automatic installation fails, packages can be installed manually:

```bash
# Using CLI tool (once implemented)
monolith package install /var/lib/monolith-firewall/packages/monolith-network.mfwpkg

# Or via WebUI
# Navigate to Packages section and upload .mfwpkg file
```

## Troubleshooting

### Check if first boot ran
```bash
# If this file exists, first boot hasn't run yet
ls -la /var/lib/monolith-firewall/.firstboot

# Check service status
systemctl status monolith-firstboot.service

# View logs
journalctl -u monolith-firstboot.service
```

### Manually trigger first boot
```bash
# Create flag and run service
touch /var/lib/monolith-firewall/.firstboot
systemctl start monolith-firstboot.service
```

### Check installed packages
```bash
# List packages directory
ls -la /var/lib/monolith-firewall/packages/

# Check Core service logs
journalctl -u monolith-firewall-core.service
```

## Benefits

1. **Automatic**: No manual intervention needed
2. **Offline**: Works completely offline (packages on ISO)
3. **Reliable**: Uses same API as WebUI/CLI
4. **Recoverable**: Can be manually triggered if needed
5. **One-time**: Only runs on first boot (flag prevents re-running)
