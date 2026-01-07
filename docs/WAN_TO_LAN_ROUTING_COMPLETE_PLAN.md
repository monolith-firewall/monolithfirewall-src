# Complete Integrated Plan: WAN to LAN Routing Fix

## Overview
This plan integrates three critical fixes to enable proper routing between WAN and LAN interfaces. The fixes must be implemented in a specific order and work together to ensure routing functions correctly.

## System Flow Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                    System Startup Sequence                      │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
        ┌─────────────────────────────────────┐
        │  1. Apply System Settings           │
        │     (hostname, timezone, DNS, NTP)  │
        └─────────────────────────────────────┘
                              │
                              ▼
        ┌─────────────────────────────────────┐
        │  1.5. Apply System Tuneables ⭐ NEW  │
        │     - net.ipv4.ip_forward = 1       │
        │     - net.ipv6.conf.all.forwarding   │
        │     - Other stored tuneables         │
        └─────────────────────────────────────┘
                              │
                              ▼
        ┌─────────────────────────────────────┐
        │  2. Generate Interface Configs      │
        │     (WAN/LAN assignments)           │
        └─────────────────────────────────────┘
                              │
                              ▼
        ┌─────────────────────────────────────┐
        │  3. Generate Module Configs         │
        └─────────────────────────────────────┘
                              │
                              ▼
        ┌─────────────────────────────────────┐
        │  4. Start Module Services            │
        └─────────────────────────────────────┘
                              │
                              ▼
        ┌─────────────────────────────────────┐
        │  5. Apply Firewall Rules ⭐ UPDATED │
        │     - Forward rules (WAN↔LAN)       │
        │     - NAT masquerade (WAN)          │
        └─────────────────────────────────────┘
```

## Implementation Order

### Phase 1: Model Updates (Foundation)
**Why first**: Other changes depend on these models.

### Phase 2: StartupManager Integration (Enable Forwarding)
**Why second**: Must apply IPv4 forwarding before firewall rules are applied.

### Phase 3: FirewallApplyManager Updates (Routing Rules)
**Why third**: Depends on forwarding being enabled and interface assignments being known.

## Detailed Implementation

---

## Phase 1: Model Updates

### File: `src/Monolith.FireWall.Core/Models/SystemSettingsModels.cs`

**Add new result class for tuneables startup:**

```csharp
/// <summary>
/// Result of system tuneables application during startup.
/// </summary>
public sealed class TuneablesStartupResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int AppliedCount { get; set; }
    public int TotalCount { get; set; }
    public List<string> Warnings { get; set; } = new();
}
```

**Update StartupResult to include tuneables:**

```csharp
public sealed class StartupResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? Duration { get; set; }
    public SystemSettingsResult SystemSettings { get; set; } = new();
    public TuneablesStartupResult Tuneables { get; set; } = new();  // ⭐ ADD THIS
    public InterfaceConfigResult Interfaces { get; set; } = new();
    public Services.ModuleConfigGenerationSummary Modules { get; set; } = new();
    public Services.ServiceManagementResult Services { get; set; } = new();
    public FirewallStartupResult Firewall { get; set; } = new();
}
```

---

## Phase 2: StartupManager Integration

### File: `src/Monolith.FireWall.Core/Services/StartupManager.cs`

**Step 2.1: Add SystemTuneablesManager dependency**

```csharp
public sealed class StartupManager
{
    private readonly ILogger _logger;
    private readonly SystemSettingsManager _systemSettingsManager;
    private readonly SystemTuneablesManager _tuneablesManager;  // ⭐ ADD THIS
    private readonly InterfaceConfigApplier _interfaceConfigApplier;
    private readonly FirewallApplyManager _firewallApplyManager;
    private readonly ModuleConfigGenerator _moduleConfigGenerator;
    private readonly ModuleServiceManager _moduleServiceManager;

    public StartupManager(
        ILogger logger,
        SystemSettingsManager systemSettingsManager,
        SystemTuneablesManager tuneablesManager,  // ⭐ ADD THIS
        InterfaceConfigApplier interfaceConfigApplier,
        FirewallApplyManager firewallApplyManager,
        ModuleConfigGenerator moduleConfigGenerator,
        ModuleServiceManager moduleServiceManager)
    {
        _logger = logger;
        _systemSettingsManager = systemSettingsManager;
        _tuneablesManager = tuneablesManager;  // ⭐ ADD THIS
        _interfaceConfigApplier = interfaceConfigApplier;
        _firewallApplyManager = firewallApplyManager;
        _moduleConfigGenerator = moduleConfigGenerator;
        _moduleServiceManager = moduleServiceManager;
    }
```

**Step 2.2: Add tuneables application step in InitializeSystemAsync**

Insert this **after Step 1** (system settings) and **before Step 2** (interface configs):

```csharp
public async Task<StartupResult> InitializeSystemAsync(CancellationToken cancellationToken = default)
{
    var result = new StartupResult
    {
        StartedAt = DateTime.UtcNow
    };

    _logger.LogInformation("Starting system initialization...");

    try
    {
        // Step 1: Apply system settings (hostname, timezone, DNS, NTP)
        _logger.LogInformation("Applying system settings...");
        var systemResult = await ApplySystemSettingsAsync(cancellationToken);
        result.SystemSettings = systemResult;
        if (systemResult.Success)
        {
            _logger.LogInformation("✓ System settings applied");
        }
        else
        {
            _logger.LogWarning($"⚠ System settings partially applied: {systemResult.Error}");
        }

        // ⭐ STEP 1.5: Apply system tuneables (including IPv4 forwarding)
        _logger.LogInformation("Applying system tuneables...");
        var tuneablesResult = await ApplySystemTuneablesAsync(cancellationToken);
        result.Tuneables = tuneablesResult;
        if (tuneablesResult.Success)
        {
            _logger.LogInformation($"✓ System tuneables applied ({tuneablesResult.AppliedCount}/{tuneablesResult.TotalCount} tuneable(s))");
            if (tuneablesResult.Warnings.Count > 0)
            {
                foreach (var warning in tuneablesResult.Warnings)
                {
                    _logger.LogWarning($"  → {warning}");
                }
            }
        }
        else
        {
            _logger.LogWarning($"⚠ System tuneables partially applied: {tuneablesResult.Error}");
        }

        // Step 2: Generate and apply interface configurations
        _logger.LogInformation("Generating interface configurations...");
        // ... rest of existing code ...
```

**Step 2.3: Add ApplySystemTuneablesAsync method**

Add this new method after `ApplySystemSettingsAsync`:

```csharp
/// <summary>
/// Apply stored system tuneables from database.
/// Used during startup initialization to enable features like IPv4 forwarding.
/// </summary>
public async Task<TuneablesStartupResult> ApplySystemTuneablesAsync(CancellationToken cancellationToken = default)
{
    var result = new TuneablesStartupResult();
    
    try
    {
        // Get all tuneables (includes stored desired values and current system values)
        var allTuneables = await _tuneablesManager.GetTuneablesAsync(cancellationToken);
        result.TotalCount = allTuneables.Count;
        
        // Find tuneables that need to be applied (desired value differs from current)
        var toApply = allTuneables
            .Where(t => 
                !string.IsNullOrWhiteSpace(t.DesiredValue) && 
                t.DesiredValue != t.CurrentValue)
            .ToList();
        
        if (toApply.Count == 0)
        {
            result.Success = true;
            result.AppliedCount = 0;
            return result;
        }
        
        // Build apply request
        var request = new TuneableApplyRequest
        {
            Items = toApply.Select(t => new TuneableUpdate
            {
                Key = t.Key,
                Value = t.DesiredValue
            }).ToList()
        };
        
        // Apply tuneables
        var applyResult = await _tuneablesManager.ApplyAsync(request, cancellationToken);
        
        result.Success = applyResult.Success;
        result.AppliedCount = applyResult.Results?.Count(r => r.Success) ?? 0;
        result.Error = applyResult.Error;
        result.Warnings = applyResult.Results?
            .Where(r => !r.Success && !string.IsNullOrWhiteSpace(r.Error))
            .Select(r => $"{r.Key}: {r.Error}")
            .ToList() ?? new List<string>();
        
        // Log critical tuneables
        var ipForward = toApply.FirstOrDefault(t => t.Key == "net.ipv4.ip_forward");
        if (ipForward != null)
        {
            var ipForwardResult = applyResult.Results?.FirstOrDefault(r => r.Key == "net.ipv4.ip_forward");
            if (ipForwardResult?.Success == true)
            {
                _logger.LogInformation($"  → IPv4 forwarding enabled: {ipForwardResult.AppliedValue}");
            }
            else
            {
                _logger.LogWarning($"  → IPv4 forwarding failed to apply: {ipForwardResult?.Error ?? "unknown error"}");
                result.Warnings.Add("IPv4 forwarding not applied - routing may not work");
            }
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to apply system tuneables");
        result.Success = false;
        result.Error = ex.Message;
    }
    
    return result;
}
```

### File: `src/Monolith.FireWall.Core/Program.cs`

**Update StartupManager instantiation to include SystemTuneablesManager:**

```csharp
// Around line 212, update StartupManager constructor call:
var startupManager = new StartupManager(
    logger,
    settingsManager,
    tuneablesManager,  // ⭐ ADD THIS - already created above
    interfaceConfigApplier,
    firewallManager.ApplyManager,
    moduleConfigGenerator,
    moduleServiceManager);
```

---

## Phase 3: FirewallApplyManager Updates

### File: `src/Monolith.FireWall.Core/Services/Firewall/FirewallApplyManager.cs`

**Step 3.1: Update AppendNatTable to add postrouting chain with masquerade**

Modify the `AppendNatTable` method. Find the section after the `output` chain (around line 260-264) and add the postrouting chain:

```csharp
private void AppendNatTable(
    StringBuilder builder,
    string family,
    List<FirewallNatRuleView> rules,
    List<FirewallAliasView> aliases,
    FirewallNatSettingsView natSettings,
    List<string> warnings)
{
    builder.AppendLine($"table {family} monolith_nat {{");

    // ... existing alias sets code ...

    builder.AppendLine("  chain prerouting {");
    builder.AppendLine("    type nat hook prerouting priority -100; policy accept;");

    foreach (var rule in rules.OrderBy(r => r.RuleNumber))
    {
        var ruleLines = BuildNatRule(rule, usedAliases, family, warnings);
        foreach (var line in ruleLines)
        {
            builder.AppendLine("    " + line);
        }
    }

    builder.AppendLine("  }");
    builder.AppendLine("  chain output {");
    builder.AppendLine("    type nat hook output priority -100; policy accept;");
    builder.AppendLine("  }");
    
    // ⭐ ADD POSTROUTING CHAIN FOR MASQUERADE
    builder.AppendLine("  chain postrouting {");
    builder.AppendLine("    type nat hook postrouting priority 100; policy accept;");
    
    // Get WAN interfaces from assignments (need to pass assignments to this method)
    // For now, we'll need to modify the method signature - see Step 3.2
    
    builder.AppendLine("  }");
    builder.AppendLine("}");

    if (natSettings.ReflectionEnabled && natSettings.ReflectionMode == "nat")
    {
        warnings.Add("NAT reflection is enabled but not fully implemented in nftables output yet");
    }
}
```

**Step 3.2: Update BuildConfigAsync to pass assignments to AppendNatTable**

Modify `BuildConfigAsync` method (around line 168):

```csharp
public async Task<FirewallApplyResult> BuildConfigAsync(CancellationToken cancellationToken)
{
    var aliases = await _aliasManager.ListAliasesAsync();
    var natRules = await _natManager.ListRulesAsync();
    var natSettings = await _natSettingsManager.GetAsync();
    var defaults = await _defaultsManager.GetAsync();
    var effectiveRules = await _rulesManager.GetEffectiveRulesAsync(defaults);
    var assignments = await _interfaceStore.GetAssignmentsAsync();  // ⭐ Already have this

    var warnings = new List<string>();
    var builder = new StringBuilder();

    builder.AppendLine("# Generated by Monolith FireWall");
    builder.AppendLine($"# {DateTime.UtcNow:O}");
    builder.AppendLine("# Managed tables will be replaced by apply step");

    var ipv4Rules = natRules.Where(r => r.Enabled && (r.AddressFamily == "ipv4" || r.AddressFamily == "dual")).ToList();
    var ipv6Rules = natRules.Where(r => r.Enabled && (r.AddressFamily == "ipv6" || r.AddressFamily == "dual")).ToList();

    if (ipv4Rules.Count == 0 && ipv6Rules.Count == 0)
    {
        warnings.Add("No enabled NAT rules found");
    }

    if (ipv4Rules.Count > 0)
    {
        AppendNatTable(builder, "ip", ipv4Rules, aliases, natSettings, assignments, warnings);  // ⭐ ADD assignments
    }

    if (ipv6Rules.Count > 0)
    {
        AppendNatTable(builder, "ip6", ipv6Rules, aliases, natSettings, assignments, warnings);  // ⭐ ADD assignments
    }

    AppendFilterTable(builder, effectiveRules, defaults, assignments, aliases, warnings);

    // ... rest of method ...
}
```

**Step 3.3: Update AppendNatTable signature and add masquerade rules**

```csharp
private void AppendNatTable(
    StringBuilder builder,
    string family,
    List<FirewallNatRuleView> rules,
    List<FirewallAliasView> aliases,
    FirewallNatSettingsView natSettings,
    List<InterfaceAssignmentEntity> assignments,  // ⭐ ADD THIS PARAMETER
    List<string> warnings)
{
    builder.AppendLine($"table {family} monolith_nat {{");

    // ... existing alias sets and prerouting chain code ...

    builder.AppendLine("  chain output {");
    builder.AppendLine("    type nat hook output priority -100; policy accept;");
    builder.AppendLine("  }");
    
    // ⭐ POSTROUTING CHAIN FOR MASQUERADE
    builder.AppendLine("  chain postrouting {");
    builder.AppendLine("    type nat hook postrouting priority 100; policy accept;");
    
    // Get WAN interfaces for this address family
    var wanInterfaces = assignments
        .Where(a => a.Role == InterfaceRole.Wan)
        .Select(a => a.InterfaceName)
        .ToList();
    
    if (wanInterfaces.Count > 0)
    {
        foreach (var wanInterface in wanInterfaces)
        {
            // Masquerade all outbound traffic on WAN interface
            // This allows LAN devices to access internet through WAN
            builder.AppendLine($"    oifname \"{wanInterface}\" masquerade comment \"Auto: WAN masquerade on {wanInterface}\"");
        }
    }
    
    builder.AppendLine("  }");
    builder.AppendLine("}");

    if (natSettings.ReflectionEnabled && natSettings.ReflectionMode == "nat")
    {
        warnings.Add("NAT reflection is enabled but not fully implemented in nftables output yet");
    }
}
```

**Step 3.4: Update AppendFilterTable to add automatic forward rules**

Modify the `forward` chain section in `AppendFilterTable` (around line 317-323):

```csharp
builder.AppendLine("  chain forward {");
builder.AppendLine("    type filter hook forward priority 0; policy drop;");

// ⭐ ADD AUTOMATIC FORWARD RULES FOR WAN↔LAN ROUTING
// Get interface roles
var wanInterfaces = assignments
    .Where(a => a.Role == InterfaceRole.Wan)
    .Select(a => a.InterfaceName)
    .ToList();
var lanInterfaces = assignments
    .Where(a => a.Role == InterfaceRole.Lan)
    .Select(a => a.InterfaceName)
    .ToList();

// Allow forwarding from WAN to LAN (for incoming connections from internet)
// This enables port forwarding and incoming connections
if (wanInterfaces.Count > 0 && lanInterfaces.Count > 0)
{
    foreach (var wan in wanInterfaces)
    {
        foreach (var lan in lanInterfaces)
        {
            builder.AppendLine($"    iifname \"{wan}\" oifname \"{lan}\" accept comment \"Auto: WAN to LAN routing\"");
        }
    }
}

// Allow forwarding from LAN to WAN (for outbound connections to internet)
// This enables LAN devices to access internet through WAN
if (lanInterfaces.Count > 0 && wanInterfaces.Count > 0)
{
    foreach (var lan in lanInterfaces)
    {
        foreach (var wan in wanInterfaces)
        {
            builder.AppendLine($"    iifname \"{lan}\" oifname \"{wan}\" accept comment \"Auto: LAN to WAN routing\"");
        }
    }
}

// Allow forwarding between LAN interfaces (for internal routing)
if (lanInterfaces.Count > 1)
{
    for (int i = 0; i < lanInterfaces.Count; i++)
    {
        for (int j = i + 1; j < lanInterfaces.Count; j++)
        {
            var lan1 = lanInterfaces[i];
            var lan2 = lanInterfaces[j];
            builder.AppendLine($"    iifname \"{lan1}\" oifname \"{lan2}\" accept comment \"Auto: LAN to LAN routing\"");
            builder.AppendLine($"    iifname \"{lan2}\" oifname \"{lan1}\" accept comment \"Auto: LAN to LAN routing\"");
        }
    }
}

// Continue with interface-specific forward chains (existing code)
foreach (var assignment in assignments)
{
    builder.AppendLine($"    iifname \"{assignment.InterfaceName}\" jump forward_{assignment.InterfaceName}");
}
builder.AppendLine("  }");
```

---

## Integration Points Summary

### Dependency Chain

```
1. SystemSettingsModels (Phase 1)
   └─> Defines TuneablesStartupResult
       └─> Used by StartupManager (Phase 2)

2. StartupManager (Phase 2)
   └─> Depends on SystemTuneablesManager (already exists)
   └─> Applies IPv4 forwarding BEFORE firewall rules
       └─> Enables kernel-level forwarding

3. FirewallApplyManager (Phase 3)
   └─> Depends on interface assignments (already available)
   └─> Creates forward rules AFTER forwarding is enabled
   └─> Creates masquerade rules for WAN interfaces
```

### Execution Order (Critical)

1. **System Settings** → Basic system configuration
2. **System Tuneables** → **Enable IPv4 forwarding** ⭐
3. **Interface Configs** → Configure WAN/LAN interfaces
4. **Module Configs** → Module-specific setup
5. **Module Services** → Start services
6. **Firewall Rules** → **Apply forward rules and masquerade** ⭐

**Why this order matters:**
- IPv4 forwarding must be enabled before firewall rules are applied
- Interface assignments must be known before creating forward rules
- Forward rules need forwarding to be enabled in the kernel

---

## Testing Strategy

### Unit Tests
1. Test `ApplySystemTuneablesAsync` applies stored tuneables
2. Test `AppendNatTable` creates postrouting chain with masquerade
3. Test `AppendFilterTable` creates forward rules for WAN↔LAN

### Integration Tests
1. **Startup Sequence Test**
   - Verify tuneables are applied after system settings
   - Verify firewall rules are applied after tuneables
   - Verify IPv4 forwarding is enabled

2. **Firewall Configuration Test**
   - Verify postrouting chain exists with masquerade rules
   - Verify forward chain has WAN↔LAN rules
   - Verify rules are in correct order

### Manual Testing Checklist

1. **Verify IPv4 Forwarding**
   ```bash
   sysctl net.ipv4.ip_forward
   # Should return: net.ipv4.ip_forward = 1
   ```

2. **Verify NAT Masquerade**
   ```bash
   nft list table ip monolith_nat
   # Should show postrouting chain with masquerade rules for WAN interfaces
   ```

3. **Verify Forward Rules**
   ```bash
   nft list table inet monolith_filter
   # Should show forward chain with:
   # - "Auto: WAN to LAN routing" rules
   # - "Auto: LAN to WAN routing" rules
   ```

4. **Test Connectivity**
   - From LAN device: `ping 8.8.8.8` (should work)
   - From LAN device: `curl https://www.google.com` (should work)
   - Check NAT: `nft list table ip monolith_nat -a` (should show masqueraded connections)

5. **Test Port Forwarding**
   - Configure a port forward rule (WAN port → LAN IP)
   - From external network: Connect to WAN IP on forwarded port
   - Should reach LAN device

---

## Rollback Plan

If issues occur:

1. **Disable IPv4 forwarding**: Set `net.ipv4.ip_forward` to `0` in webui
2. **Remove automatic rules**: Comment out the new code sections
3. **Manual firewall rules**: Users can still create manual forward rules if needed

---

## Files Modified Summary

1. ✅ `src/Monolith.FireWall.Core/Models/SystemSettingsModels.cs`
   - Add `TuneablesStartupResult` class
   - Update `StartupResult` to include `Tuneables` property

2. ✅ `src/Monolith.FireWall.Core/Services/StartupManager.cs`
   - Add `SystemTuneablesManager` dependency
   - Add `ApplySystemTuneablesAsync` method
   - Update `InitializeSystemAsync` to call tuneables application

3. ✅ `src/Monolith.FireWall.Core/Program.cs`
   - Update `StartupManager` instantiation to include `tuneablesManager`

4. ✅ `src/Monolith.FireWall.Core/Services/Firewall/FirewallApplyManager.cs`
   - Update `BuildConfigAsync` to pass assignments to `AppendNatTable`
   - Update `AppendNatTable` signature to accept assignments
   - Add postrouting chain with masquerade rules
   - Update `AppendFilterTable` to add automatic forward rules

---

## Success Criteria

✅ IPv4 forwarding is enabled at boot  
✅ NAT masquerade rules exist for all WAN interfaces  
✅ Forward rules allow WAN↔LAN traffic  
✅ LAN devices can access internet through WAN  
✅ Port forwarding works correctly  
✅ System logs show successful tuneables and firewall application  

---

## Notes

- The automatic forward rules are added **before** interface-specific forward chains, so they take precedence
- Masquerade rules are added for **all** WAN interfaces automatically
- The system will work even if no manual forward rules are configured
- Users can still override behavior with custom firewall rules if needed
