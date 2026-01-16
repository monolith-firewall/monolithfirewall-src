# Package Installation Guide

## Overview

This guide explains how to install, manage, and verify Monolith FireWall packages (`.mfwpkg` files).

## Prerequisites

Before installing packages, ensure:

1. **Core Service is Running**
   ```bash
   systemctl status monolith-firewall-core
   # Should show: active (running)
   ```

2. **Unix Socket is Available**
   ```bash
   test -S /var/lib/monolith-firewall/run/monolith-core.sock && echo "OK" || echo "Socket missing"
   ```

3. **CLI Tool is Installed**
   ```bash
   which monolith-pkgmgr
   # Should output: /usr/bin/monolith-pkgmgr
   ```

## Installation Methods

### Method 1: Using CLI Tool (Recommended)

The `monolith-pkgmgr` CLI tool is the standard way to install packages.

#### Install a Package

```bash
monolith-pkgmgr package install <package-file.mfwpkg> [--overwrite]
```

**Options:**
- `--overwrite`: Overwrite existing package if already installed

**Example:**
```bash
monolith-pkgmgr package install /path/to/monolith-network.mfwpkg --overwrite
```

#### List Installed Packages

```bash
monolith-pkgmgr package list
```

#### Remove a Package

```bash
monolith-pkgmgr package remove <package-id>
```

**Example:**
```bash
monolith-pkgmgr package remove monolith-network
```

### Method 2: Using Unix Socket API (Advanced)

For programmatic installation, you can use the Unix socket API directly:

```bash
# Using socat
echo '{
  "action": "packages.install",
  "payload": {
    "sourcePath": "/path/to/package.mfwpkg",
    "overwrite": true
  }
}' | socat - UNIX-CONNECT:/var/lib/monolith-firewall/run/monolith-core.sock
```

### Method 3: Using WebUI

1. Navigate to the WebUI: `https://your-server/`
2. Go to **Packages** section
3. Upload or select the `.mfwpkg` file
4. Click **Install**

## Installed Packages

### Package Locations

After installation, packages are stored in:

```
/var/lib/monolith-firewall/codelogic/Packages/
├── monolith-network/
│   ├── manifest.json
│   └── backend/
│       └── Monolith.Network.dll
├── monolith-vpn/
│   ├── manifest.json
│   └── backend/
│       └── Monolith.Vpn.dll
└── monolith-diagnostics/
    ├── manifest.json
    └── backend/
        └── Monolith.Diagnostics.dll
```

### Package Information

Each package contains:
- **`manifest.json`**: Package metadata (ID, name, version, dependencies)
- **`backend/`**: Compiled DLL files (main package DLL with embedded Razor views)
- **`wwwroot/`** (optional): Static assets (CSS, JS, images)

## Verification

### Check Package Installation

1. **List installed packages:**
   ```bash
   monolith-pkgmgr package list
   ```

2. **Check package directory:**
   ```bash
   ls -la /var/lib/monolith-firewall/codelogic/Packages/
   ```

3. **Verify DLL files:**
   ```bash
   find /var/lib/monolith-firewall/codelogic/Packages -name "*.dll"
   ```

4. **Check Core logs:**
   ```bash
   journalctl -u monolith-firewall-core -n 50 | grep -i package
   ```

### Verify Package Functionality

1. **Access WebUI:**
   - Navigate to package pages via routes like:
     - `/p/monolith-network/dhcp/config`
     - `/p/monolith-vpn/ipsec/config`
     - `/p/monolith-diagnostics/diagnostics/config`

2. **Check API metadata:**
   ```bash
   echo '{"action": "get-packages"}' | \
     socat - UNIX-CONNECT:/var/lib/monolith-firewall/run/monolith-core.sock | \
     jq '.data[] | {id, name, version, hasRazorViews}'
   ```

## Currently Installed Packages

Based on the installation session, the following packages are installed:

### 1. monolith-diagnostics (28K)
- **Purpose**: System diagnostics tools (ping, traceroute, MTR)
- **Routes**: `/p/monolith-diagnostics/diagnostics/config`
- **Status**: ✓ Installed

### 2. monolith-network (105K)
- **Purpose**: Network management (DHCP, DNS)
- **Routes**: 
  - `/p/monolith-network/dhcp/config`
  - `/p/monolith-network/dns/config`
- **Status**: ✓ Installed

### 3. monolith-vpn (67K)
- **Purpose**: VPN management (IPsec, OpenVPN, WireGuard)
- **Routes**:
  - `/p/monolith-vpn/ipsec/config`
  - `/p/monolith-vpn/openvpn/config`
  - `/p/monolith-vpn/wireguard/config`
- **Status**: ✓ Installed

## Troubleshooting

### Package Installation Fails

**Error: "Core service is not running"**
```bash
# Start the Core service
sudo systemctl start monolith-firewall-core
sudo systemctl status monolith-firewall-core
```

**Error: "Package already installed"**
```bash
# Use --overwrite flag
monolith-pkgmgr package install package.mfwpkg --overwrite
```

**Error: "Permission denied"**
```bash
# Ensure you have proper permissions
sudo monolith-pkgmgr package install package.mfwpkg
```

### Package Not Loading

1. **Check Core logs:**
   ```bash
   journalctl -u monolith-firewall-core -f
   ```

2. **Verify package structure:**
   ```bash
   ls -la /var/lib/monolith-firewall/codelogic/Packages/<package-id>/
   cat /var/lib/monolith-firewall/codelogic/Packages/<package-id>/manifest.json
   ```

3. **Check DLL exists:**
   ```bash
   find /var/lib/monolith-firewall/codelogic/Packages/<package-id>/backend -name "*.dll"
   ```

### Package Pages Not Rendering

1. **Verify Razor views are embedded:**
   ```bash
   strings /var/lib/monolith-firewall/codelogic/Packages/<package-id>/backend/*.dll | grep -i "\.cshtml"
   ```

2. **Check WebUI logs:**
   ```bash
   journalctl -u monolith-firewall-webui -f
   ```

3. **Verify package is registered:**
   ```bash
   echo '{"action": "get-packages"}' | \
     socat - UNIX-CONNECT:/var/lib/monolith-firewall/run/monolith-core.sock | \
     jq '.data[] | select(.id == "<package-id>")'
   ```

## Package Development

### Building Packages

```bash
# Build all packages
./build-scripts/build-all-packages.sh

# Build specific package
./build-scripts/package-mfwpkg.sh <package-id>
```

### Package Structure

Packages should follow this structure:

```
package-name/
├── manifest.json          # Package metadata
├── Package.cs            # Package implementation
├── Modules/              # Module implementations
│   └── ...
├── Pages/                 # Razor pages (embedded in DLL)
│   └── ...
└── wwwroot/              # Static assets (optional)
    ├── css/
    ├── js/
    └── ...
```

### Package Manifest

Example `manifest.json`:

```json
{
  "id": "monolith-network",
  "name": "Network Management",
  "version": "1.0.0",
  "description": "DHCP and DNS management",
  "author": "Monolith Team",
  "dependencies": [],
  "firewallIntents": []
}
```

## Best Practices

1. **Always use `--overwrite` during development** to ensure latest version is installed
2. **Check Core service status** before installing packages
3. **Verify package installation** using `monolith-pkgmgr package list`
4. **Monitor logs** during installation for any errors
5. **Test package pages** in WebUI after installation

## Related Documentation

- [Package Page Structure](./PACKAGE_PAGE_STRUCTURE.md)
- [Razor Compilation Fix Plan](./RAZOR_COMPILATION_FIX_PLAN.md)
- [Build Scripts](../build-scripts/README.md)

## Support

For issues or questions:
1. Check Core service logs: `journalctl -u monolith-firewall-core`
2. Check WebUI logs: `journalctl -u monolith-firewall-webui`
3. Verify package structure and manifest.json
4. Review package installation directory permissions
