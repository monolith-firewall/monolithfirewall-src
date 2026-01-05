# Startup Manager - Implementation Plan

## Overview

A startup management service that orchestrates all system initialization on boot:
1. Read all configuration from the database
2. Apply system settings (hostname, timezone, DNS, NTP)
3. Generate and apply network interface configurations
4. Generate and apply module-specific configurations
5. Apply firewall rules
6. Start/restart module services as needed

## Current State Analysis

### ✅ What Exists

1. **SystemSettingsManager** (`SystemSettingsManager.cs`)
   - Stores: hostname, domain, timezone, DNS servers, NTP servers
   - Can apply settings to system (hostnamectl, timedatectl, etc.)
   - **Issue**: Only applies when updated via API, not on boot

2. **InterfaceConfigManager** (`InterfaceConfigManager.cs`)
   - Generates `/etc/network/interfaces.d/monolith` from database
   - Handles VLANs, bridges, static/DHCP configs
   - **Issue**: Config is generated but not automatically applied on boot

3. **FirewallManager** (`FirewallManager.cs`)
   - Manages firewall rules, NAT, aliases
   - **Issue**: Rules are stored but may not be applied on boot

4. **ModuleRegistry** (`ModuleRegistry.cs`)
   - Tracks loaded modules and packages
   - Modules can implement `IMonolithModuleLifecycle` for startup hooks
   - **Issue**: Modules are loaded but config generation is not centralized

5. **First Boot Service** (`monolith-firstboot.service`)
   - Installs packages from ISO on first boot
   - **Works well**: Already handles package installation

6. **Database Storage**
   - `SystemSettingsEntity` - system settings
   - `InterfaceAssignmentEntity` - interface configurations
   - `StaticRouteEntity` - routing
   - `FirewallRuleEntity` - firewall rules
   - `ModuleStateEntity` - module states

### ❌ What's Missing

1. **Startup Manager Service**
   - No centralized service that orchestrates boot initialization
   - No automatic application of stored settings
   - No module config generation on boot

2. **Module Config Generation**
   - Modules can have config files (e.g., DHCP config, VPN configs)
   - No standardized way to generate these from database on boot

3. **Service Management**
   - No automatic start/restart of module services on boot
   - No dependency management between services

## Proposed Solution

### Architecture

```
┌─────────────────────────────────────────────────────────┐
│              Startup Manager Service                     │
│      (monolith-startup.service)                         │
└─────────────────────────────────────────────────────────┘
                         │
         ┌───────────────┼───────────────┐
         │               │               │
         ▼               ▼               ▼
┌──────────────┐ ┌──────────────┐ ┌──────────────┐
│   System     │ │  Interface   │ │   Module    │
│  Settings    │ │    Config     │ │    Config    │
│  Applier     │ │   Generator   │ │  Generator   │
└──────────────┘ └──────────────┘ └──────────────┘
         │               │               │
         ▼               ▼               ▼
┌──────────────┐ ┌──────────────┐ ┌──────────────┐
│  Database    │ │  /etc/network│ │ Module Config│
│  (SQLite)    │ │  /interfaces │ │    Files     │
└──────────────┘ └──────────────┘ └──────────────┘
```

### Components

#### 1. StartupManager Service

**Location**: `src/Monolith.FireWall.Core/Services/StartupManager.cs`

**Responsibilities**:
- Orchestrate the entire system startup process
- Read all configuration from database
- Call individual generators/appliers
- Handle errors gracefully (log but don't fail boot)
- Track what was applied

**Methods**:
```csharp
public class StartupManager
{
    Task<StartupResult> InitializeSystemAsync(CancellationToken cancellationToken);
    Task ApplySystemSettingsAsync(CancellationToken cancellationToken);
    Task GenerateInterfaceConfigsAsync(CancellationToken cancellationToken);
    Task GenerateModuleConfigsAsync(CancellationToken cancellationToken);
    Task ApplyFirewallRulesAsync(CancellationToken cancellationToken);
    Task StartModuleServicesAsync(CancellationToken cancellationToken);
}
```

#### 2. SystemSettingsApplier

**Location**: `src/Monolith.FireWall.Core/Services/SystemSettingsApplier.cs`

**Responsibilities**:
- Read `SystemSettingsEntity` from database
- Apply hostname, timezone, DNS, NTP to system
- Use existing `SystemSettingsManager` logic

**Implementation**:
- Extend `SystemSettingsManager` or create wrapper
- Add `ApplyStoredSettingsAsync()` method
- Read from database and apply without requiring API call

#### 3. InterfaceConfigApplier

**Location**: `src/Monolith.FireWall.Core/Services/InterfaceConfigApplier.cs`

**Responsibilities**:
- Read `InterfaceAssignmentEntity` from database
- Generate `/etc/network/interfaces.d/monolith` using `InterfaceConfigManager`
- Apply DNS servers from system settings
- Trigger network restart if needed

**Implementation**:
- Use existing `InterfaceConfigManager.BuildManagedConfig()`
- Use existing `InterfaceConfigManager.ApplyAsync()`
- Read assignments from `InterfaceAssignmentStore`
- Read DNS from `SystemSettingsManager`

#### 4. ModuleConfigGenerator

**Location**: `src/Monolith.FireWall.Core/Services/ModuleConfigGenerator.cs`

**Responsibilities**:
- Iterate through all loaded modules
- Call module's config generation method (if exists)
- Generate module-specific config files
- Handle module dependencies

**New Interface**:
```csharp
public interface IModuleConfigGenerator
{
    Task<ModuleConfigResult> GenerateConfigAsync(IModuleContext context, CancellationToken cancellationToken);
    IEnumerable<string> GetConfigFilePaths();
    bool RequiresServiceRestart { get; }
}
```

**Module Implementation**:
- Modules can optionally implement `IModuleConfigGenerator`
- Called during startup to generate configs from database
- Example: `monolith-network` generates DHCP config from database

#### 5. FirewallRuleApplier

**Location**: `src/Monolith.FireWall.Core/Services/Firewall/FirewallApplyManager.cs` (extend existing)

**Responsibilities**:
- Read all firewall rules from database
- Apply via `iptables`/`nftables`
- Apply NAT rules
- Apply aliases

**Implementation**:
- Extend existing `FirewallApplyManager`
- Add `ApplyAllStoredRulesAsync()` method
- Read from `FirewallManager` stores

#### 6. ModuleServiceManager

**Location**: `src/Monolith.FireWall.Core/Services/ModuleServiceManager.cs`

**Responsibilities**:
- Start/restart systemd services for modules
- Handle service dependencies
- Check if services need restart (config changed)

**Implementation**:
- Use `systemctl` via `PlatformCommandRunner`
- Read service definitions from modules
- Track which services were started

### Systemd Service

**File**: `debian/monolith-startup.service`

```ini
[Unit]
Description=Monolith FireWall Startup Manager
After=monolith-firewall-core.service
Wants=monolith-firewall-core.service
Before=network-online.target

[Service]
Type=oneshot
RemainAfterExit=yes
ExecStart=/opt/monolith-firewall/bin/monolith-startup
StandardOutput=journal
StandardError=journal

[Install]
WantedBy=multi-user.target
```

**Script**: `debian/monolith-startup.sh`

```bash
#!/bin/bash
# Monolith FireWall Startup Manager
# Orchestrates all system initialization on boot

SOCKET_PATH="/var/lib/monolith-firewall/run/monolith-core.sock"

# Wait for Core service
MAX_WAIT=60
WAITED=0
while [ ! -S "$SOCKET_PATH" ] && [ $WAITED -lt $MAX_WAIT ]; do
    sleep 1
    WAITED=$((WAITED + 1))
done

# Call Core API to initialize system
if command -v monolith &> /dev/null; then
    monolith startup initialize
elif command -v socat &> /dev/null; then
    echo '{"action":"startup.initialize","payload":{}}' | socat - UNIX-CONNECT:"$SOCKET_PATH"
fi
```

### API Endpoint

**Handler**: `src/Monolith.FireWall.Core/Transport/Handlers/StartupHandler.cs`

**Action**: `startup.initialize`

**Request**:
```json
{
  "action": "startup.initialize",
  "payload": {
    "modules": ["all"] // or specific module IDs
  }
}
```

**Response**:
```json
{
  "Success": true,
  "Data": {
    "systemSettings": { "applied": true },
    "interfaces": { "generated": 3, "applied": true },
    "modules": [
      { "id": "monolith-network", "configs": 2, "services": ["dhcpd"] }
    ],
    "firewall": { "rules": 15, "applied": true }
  }
}
```

## Implementation Steps

### Phase 1: Core Infrastructure

1. **Create StartupConfigGenerator service**
   - Basic structure
   - Database reading
   - Error handling

2. **Extend SystemSettingsManager**
   - Add `ApplyStoredSettingsAsync()` method
   - Read from database and apply

3. **Create InterfaceConfigApplier**
   - Use existing `InterfaceConfigManager`
   - Read from database
   - Apply on boot

### Phase 2: Module Support

4. **Create IModuleConfigGenerator interface**
   - Define interface in `Monolith.FireWall.Common`
   - Document expected behavior

5. **Create ModuleConfigGenerator service**
   - Iterate modules
   - Call config generators
   - Handle errors

6. **Update example module** (monolith-network)
   - Implement `IModuleConfigGenerator`
   - Generate DHCP config from database

### Phase 3: Firewall & Services

7. **Extend FirewallApplyManager**
   - Add `ApplyAllStoredRulesAsync()`
   - Read from database

8. **Create ModuleServiceManager**
   - Start/restart services
   - Handle dependencies

### Phase 4: Integration

9. **Create systemd service**
   - Service file
   - Startup script
   - Integration with Core

10. **Create API handler**
    - `ConfigGeneratorHandler`
    - Unix socket endpoint
    - CLI support

11. **Testing**
    - Test on fresh install
    - Test on reboot
    - Test with various configurations

## Database Schema (Already Exists)

### SystemSettingsEntity
- ✅ Hostname
- ✅ Domain
- ✅ Timezone
- ✅ DnsServers
- ✅ NtpServers

### InterfaceAssignmentEntity
- ✅ InterfaceName
- ✅ Type (Physical, VLAN, Bridge)
- ✅ IpMode (Static, DHCP, None)
- ✅ IpAddress, PrefixLength, Gateway
- ✅ Role (LAN, WAN, OPT)
- ✅ BridgePorts, BridgeStp, etc.

### FirewallRuleEntity
- ✅ Rules stored
- ✅ NAT rules stored
- ✅ Aliases stored

### ModuleStateEntity
- ✅ Module states
- ⚠️ May need to add: ConfigVersion, LastGenerated

## Module Config Storage Strategy

### Option 1: Database Tables (Recommended)
- Create `ModuleConfigEntity` table
- Store module configs as JSON or key-value
- Modules read from database

### Option 2: Config Files
- Modules generate config files
- Store file paths in database
- Files in `/var/lib/monolith-firewall/configs/{module-id}/`

### Option 3: Hybrid
- Simple configs in database
- Complex configs as files
- File paths stored in database

**Recommendation**: Option 1 (Database) for consistency and backup/restore

## Error Handling

- **Non-critical errors**: Log warning, continue
- **Critical errors**: Log error, continue with other configs
- **Boot should never fail** due to config generation issues
- **Fallback**: Use existing system configs if database read fails

## Dependencies

### Service Order
```
1. monolith-firewall-core.service (must be running)
2. monolith-config-generator.service (generates configs)
3. Network services (can use generated configs)
4. Module services (can use generated configs)
```

### Module Dependencies
- Some modules depend on others (e.g., VPN depends on network)
- Config generator should respect dependency order
- Use `IModuleConfigGenerator` order or explicit dependencies

## Testing Plan

1. **Fresh Install**
   - Install system
   - Configure via WebUI
   - Reboot
   - Verify all configs applied

2. **Configuration Changes**
   - Change hostname, timezone
   - Add interface
   - Add firewall rule
   - Reboot
   - Verify changes persisted

3. **Module Configs**
   - Install module with config generator
   - Configure via WebUI
   - Reboot
   - Verify module config generated

4. **Error Scenarios**
   - Corrupt database
   - Missing files
   - Invalid configs
   - Verify graceful degradation

## Future Enhancements

1. **Config Validation**
   - Validate configs before applying
   - Rollback on failure

2. **Config Diff**
   - Track what changed
   - Only regenerate changed configs

3. **Dry Run Mode**
   - Generate configs without applying
   - Preview changes

4. **Config Backup**
   - Backup before applying
   - Restore on failure

5. **Module Config Templates**
   - Standardized config templates
   - Module-specific templates

## Files to Create/Modify

### New Files
- `src/Monolith.FireWall.Core/Services/StartupConfigGenerator.cs`
- `src/Monolith.FireWall.Core/Services/SystemSettingsApplier.cs`
- `src/Monolith.FireWall.Core/Services/InterfaceConfigApplier.cs`
- `src/Monolith.FireWall.Core/Services/ModuleConfigGenerator.cs`
- `src/Monolith.FireWall.Core/Services/ModuleServiceManager.cs`
- `src/Monolith.FireWall.Core/Transport/Handlers/ConfigGeneratorHandler.cs`
- `src/Monolith.FireWall.Common/Interfaces/IModuleConfigGenerator.cs`
- `debian/monolith-config-generator.service`
- `debian/monolith-config-generator.sh`

### Modified Files
- `src/Monolith.FireWall.Core/Services/SystemSettingsManager.cs` (add ApplyStoredSettingsAsync)
- `src/Monolith.FireWall.Core/Services/Firewall/FirewallApplyManager.cs` (add ApplyAllStoredRulesAsync)
- `src/Monolith.FireWall.Core/Program.cs` (register new services)
- `src/Monolith.FireWall.Core/Transport/UnixSocketListener.cs` (register handler)
- `debian/rules` (install service and script)
- `debian/control` (if needed)

## Timeline Estimate

- **Phase 1**: 2-3 days
- **Phase 2**: 2-3 days
- **Phase 3**: 1-2 days
- **Phase 4**: 2-3 days
- **Total**: ~7-11 days

## Success Criteria

1. ✅ System settings (hostname, timezone) applied on boot
2. ✅ Network interfaces configured from database on boot
3. ✅ Firewall rules applied on boot
4. ✅ Module configs generated on boot
5. ✅ Module services started on boot
6. ✅ All configs persist across reboots
7. ✅ Boot doesn't fail on config errors
8. ✅ Configs can be regenerated via API/CLI
