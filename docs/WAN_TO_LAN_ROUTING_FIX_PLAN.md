# WAN to LAN Routing Fix Plan

## Problem Summary
Routing from WAN type interfaces to LAN type interfaces is not working, even though IPv4 forwarding appears to be enabled in the webui.

## Root Causes Identified

### 1. IPv4 Forwarding Not Applied at Startup
**Issue**: The `SystemTuneablesManager` can enable IPv4 forwarding via the webui, but it's not being applied automatically at system startup.

**Evidence**:
- `StartupManager.InitializeSystemAsync()` calls `ApplyStoredSettingsAsync()` which only applies hostname, timezone, DNS, and NTP
- System tuneables (including `net.ipv4.ip_forward`) are NOT applied at startup
- The tuneable is saved to the database but not applied to the kernel at boot

**Location**: `src/Monolith.FireWall.Core/Services/StartupManager.cs`

### 2. Missing NAT Masquerade Rules
**Issue**: The firewall system only creates DNAT (port forwarding) rules, but there's no automatic masquerade/SNAT rule for WAN interfaces. Without masquerade, outbound traffic from LAN won't be properly NAT'd when going through WAN.

**Evidence**:
- `FirewallApplyManager.AppendNatTable()` only creates `prerouting` and `output` chains
- No `postrouting` chain is created for masquerade/SNAT
- NAT rules are only for port forwarding (DNAT), not outbound NAT (masquerade)

**Location**: `src/Monolith.FireWall.Core/Services/Firewall/FirewallApplyManager.cs`

### 3. Missing Forward Chain Rules
**Issue**: The forward chain has a default policy of "drop" and only jumps to interface-specific forward chains. There's no automatic rule to allow forwarding from WAN to LAN interfaces.

**Evidence**:
- `FirewallApplyManager.AppendFilterTable()` creates forward chains with default "drop" policy
- Forward rules are only applied per-interface, but there's no automatic rule allowing WAN→LAN forwarding
- The forward chain structure doesn't include automatic rules for inter-interface routing

**Location**: `src/Monolith.FireWall.Core/Services/Firewall/FirewallApplyManager.cs`

## Fix Plan

### Fix 1: Apply System Tuneables at Startup
**File**: `src/Monolith.FireWall.Core/Services/StartupManager.cs`

**Changes**:
1. Add `SystemTuneablesManager` as a dependency
2. Add a new step in `InitializeSystemAsync()` to apply stored tuneables
3. Create `ApplySystemTuneablesAsync()` method that:
   - Gets all stored tuneables from database
   - Applies each one that has a stored value
   - Logs results

**Implementation**:
```csharp
// In StartupManager constructor, add:
private readonly SystemTuneablesManager _tuneablesManager;

// In InitializeSystemAsync, add after Step 1 (system settings):
// Step 1.5: Apply system tuneables
_logger.LogInformation("Applying system tuneables...");
var tuneablesResult = await ApplySystemTuneablesAsync(cancellationToken);
result.Tuneables = tuneablesResult;
if (tuneablesResult.Success)
{
    _logger.LogInformation($"✓ System tuneables applied ({tuneablesResult.AppliedCount} tuneable(s))");
}
else
{
    _logger.LogWarning($"⚠ System tuneables partially applied: {tuneablesResult.Error}");
}

// Add new method:
public async Task<TuneablesStartupResult> ApplySystemTuneablesAsync(CancellationToken cancellationToken = default)
{
    try
    {
        var stored = await _tuneablesManager.GetTuneablesAsync(cancellationToken);
        var toApply = stored
            .Where(t => t.DesiredValue != null && t.DesiredValue != t.CurrentValue)
            .ToList();
        
        if (toApply.Count == 0)
        {
            return new TuneablesStartupResult { Success = true, AppliedCount = 0 };
        }
        
        var request = new TuneableApplyRequest
        {
            Items = toApply.Select(t => new TuneableUpdate
            {
                Key = t.Key,
                Value = t.DesiredValue
            }).ToList()
        };
        
        var result = await _tuneablesManager.ApplyAsync(request, cancellationToken);
        return new TuneablesStartupResult
        {
            Success = result.Success,
            AppliedCount = result.Results?.Count(r => r.Success) ?? 0,
            Error = result.Error
        };
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to apply system tuneables");
        return new TuneablesStartupResult
        {
            Success = false,
            Error = ex.Message
        };
    }
}
```

### Fix 2: Add Automatic NAT Masquerade for WAN Interfaces
**File**: `src/Monolith.FireWall.Core/Services/Firewall/FirewallApplyManager.cs`

**Changes**:
1. Modify `AppendNatTable()` to add a `postrouting` chain
2. Automatically add masquerade rules for all WAN interfaces
3. Masquerade should apply to traffic going out WAN interfaces

**Implementation**:
```csharp
// In AppendNatTable, after the output chain, add:
builder.AppendLine("  chain postrouting {");
builder.AppendLine("    type nat hook postrouting priority 100; policy accept;");

// Get WAN interfaces from assignments
var wanInterfaces = assignments
    .Where(a => a.Role == InterfaceRole.Wan)
    .Select(a => a.InterfaceName)
    .ToList();

foreach (var wanInterface in wanInterfaces)
{
    // Masquerade all outbound traffic on WAN interface
    builder.AppendLine($"    oifname \"{wanInterface}\" masquerade comment \"Auto: WAN masquerade\"");
}

builder.AppendLine("  }");
```

### Fix 3: Add Automatic Forward Rules for WAN→LAN Routing
**File**: `src/Monolith.FireWall.Core/Services/Firewall/FirewallApplyManager.cs`

**Changes**:
1. Modify `AppendFilterTable()` to add automatic forward rules
2. Allow forwarding from WAN to LAN interfaces
3. Allow forwarding from LAN to WAN interfaces (for outbound traffic)
4. Add these rules before the interface-specific forward chains

**Implementation**:
```csharp
// In AppendFilterTable, in the forward chain, add before interface jumps:
builder.AppendLine("  chain forward {");
builder.AppendLine("    type filter hook forward priority 0; policy drop;");

// Get interface roles
var wanInterfaces = assignments
    .Where(a => a.Role == InterfaceRole.Wan)
    .Select(a => a.InterfaceName)
    .ToList();
var lanInterfaces = assignments
    .Where(a => a.Role == InterfaceRole.Lan)
    .Select(a => a.InterfaceName)
    .ToList();

// Allow forwarding from WAN to LAN (for incoming connections)
if (wanInterfaces.Count > 0 && lanInterfaces.Count > 0)
{
    foreach (var wan in wanInterfaces)
    {
        foreach (var lan in lanInterfaces)
        {
            builder.AppendLine($"    iifname \"{wan}\" oifname \"{lan}\" accept comment \"Auto: WAN to LAN\"");
        }
    }
}

// Allow forwarding from LAN to WAN (for outbound connections)
if (lanInterfaces.Count > 0 && wanInterfaces.Count > 0)
{
    foreach (var lan in lanInterfaces)
    {
        foreach (var wan in wanInterfaces)
        {
            builder.AppendLine($"    iifname \"{lan}\" oifname \"{wan}\" accept comment \"Auto: LAN to WAN\"");
        }
    }
}

// Then continue with interface-specific jumps
foreach (var assignment in assignments)
{
    builder.AppendLine($"    iifname \"{assignment.InterfaceName}\" jump forward_{assignment.InterfaceName}");
}
```

## Additional Considerations

### Reverse Path Filtering
The system has `net.ipv4.conf.all.rp_filter` tuneable. For routing to work properly, this should typically be set to:
- `0` (disabled) - if you want asymmetric routing
- `2` (loose) - recommended for routers with multiple interfaces
- `1` (strict) - default, may block valid traffic in some routing scenarios

Consider adding a note or automatic adjustment when IPv4 forwarding is enabled.

### Testing Checklist
1. ✅ Verify IPv4 forwarding is enabled: `sysctl net.ipv4.ip_forward` should return `1`
2. ✅ Verify masquerade rule exists: `nft list table ip monolith_nat` should show postrouting chain with masquerade
3. ✅ Verify forward rules exist: `nft list table inet monolith_filter` should show forward chain with WAN→LAN and LAN→WAN rules
4. ✅ Test connectivity: From a device on LAN, ping/trace to an external IP through WAN
5. ✅ Test reverse: From external network, access a service forwarded from WAN to LAN

## Files to Modify

1. `src/Monolith.FireWall.Core/Services/StartupManager.cs`
   - Add SystemTuneablesManager dependency
   - Add ApplySystemTuneablesAsync method
   - Call it in InitializeSystemAsync

2. `src/Monolith.FireWall.Core/Services/Firewall/FirewallApplyManager.cs`
   - Modify AppendNatTable to add postrouting chain with masquerade
   - Modify AppendFilterTable to add automatic forward rules

3. `src/Monolith.FireWall.Core/Models/StartupModels.cs` (may need to create or update)
   - Add TuneablesStartupResult class

## Priority
**HIGH** - This is a core functionality issue that prevents the firewall from functioning as a router.

## Estimated Impact
- Fix 1: Critical - Without this, IPv4 forwarding won't be enabled at boot
- Fix 2: Critical - Without masquerade, LAN devices can't access internet through WAN
- Fix 3: Critical - Without forward rules, packets won't be allowed to traverse interfaces

All three fixes are required for WAN to LAN routing to work properly.
