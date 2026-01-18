# Startup Manager - Current Status Report

## Executive Summary

The startup manager infrastructure is **largely implemented** but has a **critical gap**: it doesn't remove existing network interface configurations before applying new ones. This can cause conflicts and prevent proper network configuration on boot.

## ✅ What Currently Works

### 1. StartupManager Service (`StartupManager.cs`)
- ✅ Fully implemented with `InitializeSystemAsync()` method
- ✅ Orchestrates all startup initialization steps in correct order:
  1. System settings (hostname, timezone, DNS, NTP)
  2. System tuneables (IPv4 forwarding, etc.)
  3. Interface configurations
  4. Module configurations
  5. Module services
  6. Firewall rules
- ✅ Comprehensive error handling and logging
- ✅ Returns detailed `StartupResult` with status for each step

### 2. System Settings Application
- ✅ `SystemSettingsManager.ApplyStoredSettingsAsync()` implemented
- ✅ Applies hostname via `hostnamectl`
- ✅ Applies timezone via `timedatectl`
- ✅ Applies DNS servers (systemd-resolved or /etc/resolv.conf)
- ✅ Applies NTP servers (systemd-timesyncd)
- ✅ Reads from database (`SystemSettingsEntity`)

### 3. System Tuneables Application
- ✅ `StartupManager.ApplySystemTuneablesAsync()` implemented
- ✅ Applies stored tuneables from database
- ✅ Includes IPv4 forwarding (`net.ipv4.ip_forward`)
- ✅ Tracks applied vs. total count

### 4. Interface Configuration Generation
- ✅ `InterfaceConfigApplier.ApplyStoredConfigsAsync()` implemented
- ✅ Reads interface assignments from database
- ✅ Generates `/etc/network/interfaces.d/monolith` file
- ✅ Includes DNS servers from system settings
- ✅ Ensures include line in `/etc/network/interfaces`
- ✅ **Partially cleans old configs** (only managed file)

### 5. Module Configuration Generation
- ✅ `ModuleConfigGenerator` service exists
- ✅ Generates configs for modules implementing `IModuleConfigGenerator`
- ✅ Tracks which modules require service restart

### 6. Module Service Management
- ✅ `ModuleServiceManager` service exists
- ✅ Starts/restarts module services
- ✅ Handles services that require restart after config changes

### 7. Firewall Rules Application
- ✅ `FirewallApplyManager.ApplyAsync()` implemented
- ✅ Applies all firewall rules from database
- ✅ Includes NAT rules, aliases, etc.

### 8. Systemd Integration
- ✅ `monolith-startup.service` exists
- ✅ `monolith-startup.sh` script exists
- ✅ Waits for Core service socket
- ✅ Calls Core API via Unix socket
- ✅ Proper service dependencies (After=monolith-firewall-core.service)

### 9. API Handler
- ✅ `StartupHandler` exists
- ✅ Handles `startup.initialize` action
- ✅ Integrated into Unix socket listener

## ❌ What Needs to Be Fixed

### 1. **CRITICAL: Network Interface Conflict Removal**

**Problem**: The startup manager doesn't remove existing interface configurations from `/etc/network/interfaces` or other files in `/etc/network/interfaces.d/` before applying new ones.

**Current Behavior**:
- `InterfaceConfigApplier.CleanOldInterfaceConfigAsync()` only removes the managed file (`/etc/network/interfaces.d/monolith`)
- Does NOT remove interface stanzas from `/etc/network/interfaces` (main file)
- Does NOT remove interface stanzas from other files in `interfaces.d/`
- This can cause conflicts where the same interface is defined in multiple places

**Expected Behavior**:
- Before generating new configs, remove ALL existing definitions of managed interfaces from:
  - `/etc/network/interfaces` (main file)
  - All files in `/etc/network/interfaces.d/` (except the managed file)
- Backup removed configurations
- Then generate fresh configs

**Solution**:
- `InterfaceConfigManager.RemoveConflictsAsync()` already exists and does this!
- **But it's not being called during startup**
- Need to call it in `InterfaceConfigApplier.ApplyStoredConfigsAsync()` before generating new configs

**Location**: `src/Monolith.FireWall.Core/Services/InterfaceConfigApplier.cs`

### 2. **Network Interface Application to Running System**

**Problem**: The startup manager generates interface config files but doesn't apply them to the running system.

**Current Behavior**:
- Config files are written to `/etc/network/interfaces.d/monolith`
- But interfaces are not brought up/down to apply the config
- On boot, the system reads the files, but if interfaces are already up with old configs, conflicts can occur

**Expected Behavior**:
- After generating configs, apply them to the running system using:
  - `ifreload` (ifupdown2) - preferred
  - `ifdown`/`ifup` (traditional ifupdown) - fallback
- This ensures interfaces are reconfigured immediately

**Solution**:
- `InterfaceConfigManager.ApplyToSystemAsync()` already exists!
- **But it's not being called during startup**
- Need to call it in `InterfaceConfigApplier.ApplyStoredConfigsAsync()` after generating configs

**Location**: `src/Monolith.FireWall.Core/Services/InterfaceConfigApplier.cs`

### 3. **Service Startup Order**

**Problem**: The startup service runs `Before=network-online.target`, which means it runs before the network is fully up. This might cause issues if network interfaces need to be configured.

**Current Behavior**:
- `monolith-startup.service` has `Before=network-online.target`
- This means it runs early, potentially before network interfaces are available

**Expected Behavior**:
- Should run after Core service is ready
- Should run before network services that depend on configured interfaces
- May need to adjust timing or add network interface availability checks

**Solution**:
- Consider changing to `After=network-pre.target` or `After=network.target`
- Or add interface availability checks in the startup script

**Location**: `debian/monolith-startup.service`

### 4. **Error Recovery**

**Problem**: If startup initialization fails, there's no automatic retry or fallback mechanism.

**Current Behavior**:
- If startup fails, it logs errors but doesn't retry
- System may boot with incomplete configuration

**Expected Behavior**:
- Non-critical errors should be logged but not block boot
- Critical errors should be logged prominently
- Consider retry mechanism for transient failures

**Solution**:
- Already implemented in `StartupManager` - errors are logged but don't block
- May want to add retry logic for specific operations

## 📋 Implementation Plan

### Priority 1: Fix Network Interface Conflict Removal (CRITICAL)

**File**: `src/Monolith.FireWall.Core/Services/InterfaceConfigApplier.cs`

**Changes Needed**:
1. Call `_configManager.RemoveConflictsAsync()` before generating configs
2. Log removed stanzas and backup files
3. Handle errors gracefully

**Code Change**:
```csharp
// In ApplyStoredConfigsAsync(), before generating configs:
// Remove existing interface configurations from other files
var (removedStanzas, backupFiles) = await _configManager.RemoveConflictsAsync(assignments, cancellationToken);
if (removedStanzas > 0)
{
    _logger.LogInformation($"Removed {removedStanzas} conflicting interface stanza(s) from other config files");
    foreach (var backup in backupFiles)
    {
        _logger.LogInformation($"  → Backed up to: {backup}");
    }
}
```

### Priority 2: Apply Network Configs to Running System

**File**: `src/Monolith.FireWall.Core/Services/InterfaceConfigApplier.cs`

**Changes Needed**:
1. Call `_configManager.ApplyToSystemAsync()` after generating configs
2. Log results
3. Handle errors gracefully (don't fail startup if interface apply fails)

**Code Change**:
```csharp
// In ApplyStoredConfigsAsync(), after generating configs:
// Apply configuration to running system
var applyNowResult = await _configManager.ApplyToSystemAsync(assignments, cancellationToken);
if (applyNowResult.Success)
{
    _logger.LogInformation($"Applied interface configuration to running system: {applyNowResult.Message}");
}
else
{
    _logger.LogWarning($"Failed to apply interface configuration to running system: {applyNowResult.Message}");
    // Don't fail startup - config files are still written, will apply on next boot
}
```

### Priority 3: Review Service Startup Timing

**File**: `debian/monolith-startup.service`

**Consider**:
- Current timing may be correct (before network-online.target)
- But need to ensure network interfaces are available
- May need to add interface availability checks in startup script

### Priority 4: Testing

**Test Scenarios**:
1. Fresh install with no existing network configs
2. System with existing interface configs in `/etc/network/interfaces`
3. System with existing interface configs in `interfaces.d/`
4. Reboot after configuration changes
5. Verify interfaces are properly configured after boot

## 🔍 Code Locations

### Key Files

1. **StartupManager**: `src/Monolith.FireWall.Core/Services/StartupManager.cs`
   - Main orchestration logic
   - ✅ Working correctly

2. **InterfaceConfigApplier**: `src/Monolith.FireWall.Core/Services/InterfaceConfigApplier.cs`
   - Applies interface configs on startup
   - ❌ Missing conflict removal
   - ❌ Missing apply to running system

3. **InterfaceConfigManager**: `src/Monolith.FireWall.Core/Services/InterfaceConfigManager.cs`
   - Has `RemoveConflictsAsync()` - ✅ exists
   - Has `ApplyToSystemAsync()` - ✅ exists
   - Just needs to be called!

4. **SystemSettingsManager**: `src/Monolith.FireWall.Core/Services/SystemSettingsManager.cs`
   - ✅ Working correctly

5. **Startup Service**: `debian/monolith-startup.service`
   - ✅ Service file exists
   - ⚠️ May need timing adjustment

6. **Startup Script**: `debian/monolith-startup.sh`
   - ✅ Script exists
   - ✅ Calls Core API correctly

## 📊 Summary

| Component | Status | Notes |
|-----------|--------|-------|
| StartupManager orchestration | ✅ Working | All steps execute in correct order |
| System settings application | ✅ Working | Hostname, timezone, DNS, NTP |
| System tuneables application | ✅ Working | IPv4 forwarding, etc. |
| Interface config generation | ✅ Working | Generates config files |
| Interface conflict removal | ❌ **MISSING** | Not called during startup |
| Interface apply to system | ❌ **MISSING** | Not called during startup |
| Module config generation | ✅ Working | Generates module configs |
| Module service management | ✅ Working | Starts/restarts services |
| Firewall rules application | ✅ Working | Applies firewall rules |
| Systemd service integration | ✅ Working | Service exists and runs |
| API handler | ✅ Working | Handles startup.initialize |

## 🎯 Next Steps

1. **Fix InterfaceConfigApplier** (Priority 1 & 2)
   - Add conflict removal call
   - Add apply to system call
   - Test thoroughly

2. **Test on fresh install**
   - Verify all configs apply correctly
   - Verify no conflicts

3. **Test on system with existing configs**
   - Verify old configs are removed
   - Verify new configs are applied

4. **Test reboot scenarios**
   - Verify configs persist
   - Verify interfaces are configured correctly

5. **Review service timing**
   - Verify network interfaces are available when needed
   - Adjust if necessary

## 🐛 Known Issues

1. **Network interface conflicts**: Existing interface configs are not removed before applying new ones
2. **Configs not applied to running system**: Config files are generated but not applied immediately
3. **Service timing**: May run before network interfaces are fully available

## ✅ What's Working Well

1. Comprehensive startup orchestration
2. Good error handling and logging
3. Proper service dependencies
4. Database-driven configuration
5. Modular design with clear separation of concerns
