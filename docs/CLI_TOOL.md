# Monolith FireWall CLI Tool

## Overview

The `monolith` CLI tool provides command-line access to all Monolith FireWall functionality, including package management, firewall configuration, interface management, and system status.

## Installation

The CLI tool is automatically installed with the `monolith-firewall` Debian package and is available as:
- `monolith` - Main CLI tool
- `monolith-pkgmgr` - Alias to `monolith package` (for backward compatibility)

## Commands

### Package Management

```bash
# List installed packages
monolith package list

# Install a package
monolith package install <file.mfwpkg> [--overwrite]

# Remove a package
monolith package remove <package-id>

# Show package information
monolith package info <package-id>
```

### Firewall Management

```bash
# List firewall rules
monolith firewall rules list

# Show firewall status
monolith firewall status

# Apply firewall configuration
monolith firewall apply
```

### Interface Management

```bash
# List network interfaces
monolith interface list

# Assign interface role
monolith interface assign <interface> <role>
# Roles: lan, wan, opt
```

### Routing

```bash
# List static routes
monolith route list
```

### System Management

```bash
# Show system status (shortcut)
monolith status

# Show system status (full)
monolith system status

# Manage services
monolith system service <action> <service>
# Actions: start, stop, restart, status
# Services: core, webui, both
```

## Examples

```bash
# Install a package
monolith package install /var/lib/monolith-firewall/packages/monolith-network.mfwpkg

# List all packages
monolith package list

# Check system status
monolith status

# List firewall rules
monolith firewall rules list

# Apply firewall configuration
monolith firewall apply

# List network interfaces
monolith interface list

# Assign interface to LAN
monolith interface assign eth0 lan
```

## First Boot Integration

The CLI tool is used by the first boot script (`monolith-firstboot.sh`) to automatically install packages from the ISO. The script:

1. Checks if `monolith` CLI is available
2. Uses it to install packages: `monolith package install <file.mfwpkg> --overwrite`
3. Falls back to `socat` if CLI is not available

## Architecture

- **Project**: `src/Monolith.FireWall.CLI/`
- **Uses**: System.CommandLine for argument parsing
- **Communication**: Unix socket API (`/var/lib/monolith-firewall/run/monolith-core.sock`)
- **Same API**: Uses identical API as WebUI for consistency

## Error Handling

- Checks if Core service is running before making requests
- Provides clear error messages
- Returns appropriate exit codes (0 = success, 1 = error)

## Future Enhancements

- Add more firewall commands (add rule, remove rule, etc.)
- Add NAT management commands
- Add monitoring commands
- Add log viewing commands
- Add configuration management commands
