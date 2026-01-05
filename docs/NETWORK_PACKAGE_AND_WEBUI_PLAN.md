# Network Package & WebUI Configuration - Implementation Plan

## Overview

This plan covers:
1. Network Package (DHCP & DNS) - Make it work with defaults
2. WebUI Multi-IP Binding - Configure in WebUI
3. Settings Menu Restructure - Tabbed interface with Web UI tab

## Part 1: Network Package (DHCP & DNS)

### Current State
- ✅ Network package exists at `/opt/monolith-firewall/packages/monolith-network`
- ✅ Has DHCP module (Pages/Dhcp/Config.cshtml)
- ✅ Has DNS module (Pages/Dns/Config.cshtml)
- ✅ Has firewall intents for DHCP (port 67) and DNS (port 53)
- ❌ Modules likely don't implement `IModuleConfigGenerator` yet
- ❌ No default configurations
- ❌ Configs may not be generated on startup

### Tasks

#### 1.1 Explore Network Package Structure
- [x] Find monolith-network package location: `/opt/monolith-firewall/packages/monolith-network`
- [x] Check for DHCP module: ✅ Exists
- [x] Check for DNS module: ✅ Exists
- [ ] Review current implementation (need source code)

#### 1.2 DHCP Server Implementation
- [x] DHCP module exists (need to check source)
- [ ] Implement `IModuleConfigGenerator` for DHCP module
- [ ] Generate `/etc/dhcp/dhcpd.conf` from database
- [ ] Generate `/etc/default/isc-dhcp-server` 
- [ ] Default configuration (if no user config):
  - Detect LAN interface automatically
  - Default subnet: 192.168.1.0/24 (or based on LAN interface IP)
  - Range: 192.168.1.100 - 192.168.1.200
  - Gateway: LAN interface IP (or 192.168.1.1)
  - DNS: From system settings or default 8.8.8.8, 8.8.4.4
  - Lease time: 3600 seconds (1 hour)
  - Max lease time: 7200 seconds (2 hours)
- [ ] Database schema for DHCP settings:
  - `DhcpSettingsEntity` table
  - Subnets, ranges, static leases
- [ ] WebUI pages already exist at `/p/monolith-network/dhcp/config`
- [ ] Make DHCP actually work (start service, verify config)

#### 1.3 DNS Server Implementation
- [x] DNS module exists (need to check source)
- [ ] Implement `IModuleConfigGenerator` for DNS module
- [ ] Choose DNS server: dnsmasq (simpler) or unbound (more features)
- [ ] Generate DNS config from database
- [ ] Default configuration (if no user config):
  - Listen on LAN interfaces
  - Forward to system DNS servers (or 8.8.8.8, 8.8.4.4)
  - Local domain: `local` (or from system domain setting)
  - Enable DHCP hostname resolution
- [ ] Database schema for DNS settings:
  - `DnsSettingsEntity` table
  - Forwarders, local domains, static hosts
- [ ] WebUI pages already exist at `/p/monolith-network/dns/config`
- [ ] Make DNS actually work (start service, verify resolution)

#### 1.4 Default Values System
- [ ] Create `DefaultValuesManager` service
- [ ] Store defaults in database on first run
- [ ] Apply defaults when no user config exists
- [ ] Defaults for:
  - DHCP: Subnet, range, gateway, DNS
  - DNS: Forwarders, local domain
  - System: Hostname, timezone, DNS servers

## Part 2: WebUI Multi-IP Binding

### Current State
- WebUI already supports binding via `/etc/monolith-firewall/webui-bindings.json`
- Currently only configurable via file
- Need to make it configurable via WebUI

### Tasks

#### 2.1 Database Schema
- [ ] Create `WebUiSettingsEntity` table:
  ```sql
  - Id (PK)
  - HttpPort (default: 80)
  - HttpsPort (default: 443)
  - BindingAddresses (JSON array of IPs, empty = all interfaces)
  - UpdatedAt
  ```
- [ ] Store in SQLite via CL.SQLite
- [ ] Default: Empty addresses = bind to all (0.0.0.0)

#### 2.2 Core API
- [ ] Create `WebUiSettingsManager` service
- [ ] Add `WebUiSettingsHandler` for Unix socket API
- [ ] Actions:
  - `webui.settings.get`
  - `webui.settings.update`
  - `webui.settings.apply` (restart WebUI service)

#### 2.3 WebUI Service Restart
- [ ] Add method to restart WebUI service from Core
- [ ] Use `systemctl restart monolith-firewall-webui.service`
- [ ] Handle service restart after binding changes

#### 2.4 WebUI Configuration
- [ ] Add `/api/webui/settings` endpoints in WebUI `Program.cs`
- [ ] Proxy to Core API via Unix socket
- [ ] Handle service restart (call Core API to restart WebUI service)
- [ ] Update `Program.cs` to read from database instead of file
- [ ] Write binding config file for compatibility (or remove file-based approach)

## Part 3: Settings Menu Restructure

### Current State
- Settings page exists at `/settings`
- Single page with all settings
- Need to make it tabbed

### Tasks

#### 3.1 Tabbed Interface
- [ ] Create tabbed layout in settings page
- [ ] Tabs:
  - **System** - Hostname, domain, timezone, DNS, NTP
  - **Web UI** - Ports, binding addresses
  - **Network** - Interface preferences (if needed)
  - **Advanced** - System tuneables, etc.

#### 3.2 Web UI Tab
- [ ] Create Web UI settings form
- [ ] Fields:
  - HTTP Port (default: 80, range: 1-65535)
  - HTTPS Port (default: 443, range: 1-65535)
  - Binding Addresses:
    - Checkbox: "Bind to all interfaces" (0.0.0.0)
    - If unchecked: Multi-select list of available IPs from interfaces
    - Show current binding status
  - Apply button (saves and restarts WebUI service)
  - Warning if changing ports (may lose connection)

#### 3.3 Settings JavaScript Refactor
- [ ] Split `settings.js` into tab modules:
  - `settings-system.js`
  - `settings-webui.js`
  - `settings-advanced.js`
- [ ] Update main `settings.js` to handle tabs
- [ ] Load tab content dynamically

## Implementation Order

### Phase 1: Network Package (DHCP & DNS)
1. Explore and document current network package
2. Implement DHCP with defaults
3. Implement DNS with defaults
4. Test DHCP and DNS functionality

### Phase 2: WebUI Binding Configuration
1. Create database schema
2. Create Core API handlers
3. Create WebUI API endpoints
4. Create WebUI settings tab
5. Test binding changes and service restart

### Phase 3: Settings Menu Restructure
1. Refactor settings page to tabs
2. Move existing settings to System tab
3. Create Web UI tab
4. Test all tabs

## Files to Create/Modify

### Network Package
- `monolith-network/Modules/Dhcp/Module.cs` (or update existing)
- `monolith-network/Modules/Dns/Module.cs` (or update existing)
- Database entities for DHCP/DNS settings
- WebUI pages for DHCP/DNS configuration

### WebUI Binding
- `src/Monolith.FireWall.Core/Models/WebUiSettingsModels.cs` (new)
- `src/Monolith.FireWall.Core/Services/WebUiSettingsManager.cs` (new)
- `src/Monolith.FireWall.Core/Services/WebUiServiceManager.cs` (new - for restarting service)
- `src/Monolith.FireWall.Core/Transport/Handlers/WebUiSettingsHandler.cs` (new)
- `src/Monolith.FireWall.WebUI/wwwroot/js/pages/settings-webui.js` (new)
- Update `src/Monolith.FireWall.WebUI/Program.cs` to read from database instead of file
- Update `src/Monolith.FireWall.Core/Program.cs` to sync WebUiSettingsEntity table

### Settings Restructure
- Update `src/Monolith.FireWall.WebUI/wwwroot/js/pages/settings.js`
- Create `src/Monolith.FireWall.WebUI/wwwroot/js/pages/settings-system.js`
- Create `src/Monolith.FireWall.WebUI/wwwroot/js/pages/settings-webui.js`
- Update settings HTML/Razor page

## Default Values Strategy

### Where to Store Defaults
1. **Code defaults** - Hardcoded in services
2. **Database defaults** - Inserted on first run
3. **Config file defaults** - In `appsettings.json` or similar

### Recommendation
- Use **code defaults** with database override
- On first run, insert default values into database
- Services read from database, fallback to code defaults if not found
- User can modify via WebUI

## Testing Plan

1. **DHCP Testing**
   - Configure DHCP via WebUI
   - Verify config files generated
   - Test DHCP server starts
   - Verify clients get IPs

2. **DNS Testing**
   - Configure DNS via WebUI
   - Verify config files generated
   - Test DNS server starts
   - Verify DNS resolution works

3. **WebUI Binding Testing**
   - Change binding addresses via WebUI
   - Verify service restarts
   - Verify WebUI accessible on new addresses
   - Test port changes

4. **Settings Tabs Testing**
   - Navigate between tabs
   - Save settings in each tab
   - Verify settings persist

## Implementation Details

### Network Package Source Location
The network package source code is likely in a separate repository (monolithfirewall-packages). We need to:
1. Find the source repository
2. Update DHCP/DNS modules to implement `IModuleConfigGenerator`
3. Add database entities for settings
4. Rebuild and reinstall the package

### Default Values Approach
**Strategy**: Code defaults + Database storage

1. **First Run Detection**
   - Check if DHCP/DNS settings exist in database
   - If not, create defaults based on:
     - LAN interface IP (from interface assignments)
     - System DNS settings
     - Common defaults (192.168.1.0/24, etc.)

2. **Default Generation Logic**
   ```csharp
   // Pseudo-code
   if (noDhcpSettings) {
       var lanInterface = GetLanInterface();
       var lanIp = lanInterface.IpAddress; // e.g., 192.168.1.1
       var subnet = CalculateSubnet(lanIp); // e.g., 192.168.1.0/24
       CreateDefaultDhcpSettings(subnet, lanIp);
   }
   ```

3. **Service Integration**
   - Defaults created during startup if missing
   - User can override via WebUI
   - Configs regenerated on boot with current settings

### WebUI Binding Implementation

**Current Flow:**
1. WebUI reads `/etc/monolith-firewall/webui-bindings.json` on startup
2. Parses IP addresses
3. Binds Kestrel to those addresses

**New Flow:**
1. WebUI reads from database via Core API on startup
2. Core API reads `WebUiSettingsEntity` from database
3. If empty, defaults to all interfaces (0.0.0.0)
4. WebUI binds Kestrel accordingly
5. Changes via WebUI → Core API → Database → Restart service → New binding

**Service Restart:**
- Core can restart WebUI service via `systemctl restart monolith-firewall-webui.service`
- WebUI calls Core API to update settings and restart
- Core updates database, writes config file (for compatibility), restarts service
- WebUI reconnects after restart

### Settings Tab Structure

**Tab Layout:**
```
┌─────────────────────────────────────┐
│  Settings                           │
├─────────────────────────────────────┤
│ [System] [Web UI] [Advanced]        │
├─────────────────────────────────────┤
│                                     │
│  Tab Content (dynamically loaded)   │
│                                     │
└─────────────────────────────────────┘
```

**Tab Content:**
- **System Tab**: Current settings.js content (hostname, timezone, DNS, NTP)
- **Web UI Tab**: New content (ports, binding addresses)
- **Advanced Tab**: System tuneables (move from advanced-settings.js)

### Port Change Warning
When changing ports, show warning:
- "Changing the port may cause you to lose connection. Make sure you can access the new port before applying."
- Option to test connection before applying
- Auto-redirect to new port after restart (if possible)
