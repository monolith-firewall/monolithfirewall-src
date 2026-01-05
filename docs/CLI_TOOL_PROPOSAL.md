# Monolith FireWall CLI Tool Proposal

## Overview

Create a unified CLI tool (`monolith`) that provides command-line access to all Monolith FireWall functionality, including package management, firewall configuration, and system management.

## Current State

- ✅ Unix socket API exists at `/var/lib/monolith-firewall/run/monolith-core.sock`
- ✅ WebUI already uses this API via `CoreApiClient`
- ✅ API handlers exist for: packages, firewall, interfaces, routing, monitoring, system settings
- ❌ No CLI tool exists yet (mentioned in debian/control but not implemented)

## Proposed CLI Tool: `monolith`

### Command Structure

```bash
monolith [command] [subcommand] [options]
```

### Commands

#### Package Management
```bash
monolith package list                    # List installed packages
monolith package install <file.mfwpkg>  # Install a package
monolith package remove <package-id>    # Remove a package
monolith package info <package-id>       # Show package information
monolith package update <package-id>     # Update a package
```

#### Firewall Management
```bash
monolith firewall rules list             # List all firewall rules
monolith firewall rules add <rule>       # Add a firewall rule
monolith firewall rules remove <id>     # Remove a firewall rule
monolith firewall nat list               # List NAT rules
monolith firewall nat add <rule>         # Add NAT rule
monolith firewall aliases list           # List firewall aliases
monolith firewall apply                  # Apply firewall configuration
monolith firewall status                 # Show firewall status
```

#### Interface Management
```bash
monolith interface list                  # List network interfaces
monolith interface assign <iface> <role> # Assign interface role (lan/wan/opt)
monolith interface config <iface>         # Show interface configuration
```

#### Routing
```bash
monolith route list                      # List static routes
monolith route add <route>               # Add static route
monolith route remove <id>               # Remove static route
```

#### System Management
```bash
monolith status                          # Show system status
monolith service start                   # Start Core/WebUI services
monolith service stop                    # Stop services
monolith service restart                 # Restart services
monolith service status                  # Show service status
monolith config show                     # Show configuration
monolith config set <key> <value>        # Set configuration value
```

#### Monitoring
```bash
monolith monitor list                    # List monitoring definitions
monolith monitor status                  # Show monitoring status
monolith logs [component]                # Show logs (optionally filtered)
```

## Implementation Approach

### Option 1: Single `monolith` CLI Tool (Recommended)
- **Location**: `src/Monolith.FireWall.CLI/`
- **Project Type**: Console Application
- **Uses**: Same Unix socket API as WebUI
- **Benefits**: 
  - Single entry point
  - Consistent API usage
  - Easy to extend

### Option 2: Separate `monolith-pkgmgr` Tool
- **Location**: `src/Monolith.FireWall.PackageManager/` (already referenced in debian/rules)
- **Focus**: Package management only
- **Benefits**: 
  - Simpler, focused tool
  - Can be extended later

### Recommended: Option 1 + Option 2
- Create `monolith` as the main CLI tool
- Keep `monolith-pkgmgr` as an alias/symlink to `monolith package` for backward compatibility

## Technical Details

### API Communication
- Use the same `CoreApiClient` pattern as WebUI
- Connect to Unix socket: `/var/lib/monolith-firewall/run/monolith-core.sock`
- Send JSON requests, receive JSON responses
- Handle errors gracefully

### Example Implementation Structure
```
src/Monolith.FireWall.CLI/
├── Program.cs                    # Main entry point, argument parsing
├── Commands/
│   ├── PackageCommand.cs         # Package management commands
│   ├── FirewallCommand.cs        # Firewall commands
│   ├── InterfaceCommand.cs       # Interface commands
│   ├── RouteCommand.cs           # Routing commands
│   ├── SystemCommand.cs          # System commands
│   └── MonitorCommand.cs         # Monitoring commands
├── Services/
│   └── CoreApiClient.cs          # Unix socket client (reuse from WebUI or create shared)
└── Monolith.FireWall.CLI.csproj
```

### Dependencies
- `Monolith.FireWall.Common` (for models/interfaces)
- System.CommandLine (for CLI parsing) OR custom argument parser
- Unix socket support (already in .NET)

## Benefits

1. **Offline Management**: Can manage firewall without web UI
2. **Scripting**: Easy to automate tasks
3. **Troubleshooting**: Quick access to status/logs from command line
4. **Package Installation**: Easy package management from CLI
5. **Consistency**: Uses same API as WebUI, ensuring consistency

## Usage Examples

```bash
# Install a package
monolith package install /path/to/monolith-network.mfwpkg

# List firewall rules
monolith firewall rules list

# Add a firewall rule
monolith firewall rules add --interface wan --direction in --action pass --protocol tcp --port 80

# Check system status
monolith status

# View logs
monolith logs core
```

## Next Steps

1. Create `Monolith.FireWall.CLI` project
2. Implement argument parsing (System.CommandLine recommended)
3. Create CoreApiClient for CLI (or extract shared client)
4. Implement package management commands first
5. Add firewall commands
6. Add remaining commands incrementally
7. Update debian package to include CLI tool
8. Create man pages/documentation
