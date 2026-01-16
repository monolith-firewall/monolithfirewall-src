# Razor Fix Implementation Reference

## Quick Summary

**Key Insight**: When using `Microsoft.NET.Sdk.Razor`, Razor views are **embedded in the main DLL**, not a separate Views.dll. We should use the main assembly for views, not look for a separate Views assembly.

---

## Critical Changes

### 1. Package Structure (NO CHANGE NEEDED)

Packages are already correctly structured:
- Use `Microsoft.NET.Sdk.Razor` SDK ✅
- Razor pages in `Pages/` directory ✅
- Views are compiled into main DLL ✅

**What's wrong**: Code is looking for `Monolith.Network.Views.dll` which doesn't exist. Views are in `Monolith.Network.dll`.

---

### 2. Core Changes

#### `PackageScanner.cs`
**Change**: Don't look for separate Views DLL. Views are in main DLL.

```csharp
// REMOVE this method entirely:
private string? FindViewsDll(string backendDir, string packageId) { ... }

// CHANGE PackageDiscoveryInfo:
public class PackageDiscoveryInfo
{
    // REMOVE: public string? ViewsDllPath { get; set; }
    // REMOVE: public bool HasRazorViews => ViewsDllPath != null;
    
    // ADD:
    public bool HasRazorViews => MainDllPath != null; // Views are in main DLL
}
```

#### `PackageLoader.cs`
**Change**: Use main assembly for views, not separate Views assembly.

```csharp
public async Task<PackageInfo> LoadPackageAsync(PackageDiscoveryInfo discoveryInfo)
{
    // Load main assembly
    var mainAssembly = Assembly.LoadFrom(discoveryInfo.MainDllPath);
    
    // REMOVE: Views assembly loading (lines 49-55)
    // Views are in main assembly when using Microsoft.NET.Sdk.Razor
    
    // Discover Razor views from MAIN assembly
    var viewDiscovery = new RazorViewDiscovery(_logger);
    var discoveredViews = viewDiscovery.DiscoverViews(
        mainAssembly,  // Use main assembly, not viewsAssembly
        discoveryInfo.Manifest.Id, 
        definition.Name);
    
    // CHANGE PackageInfo constructor call:
    return new PackageInfo(
        definition, 
        package, 
        mainAssembly, 
        mainAssembly,  // Use mainAssembly for ViewsAssembly too
        null, 
        discoveredViews, 
        discoveryInfo.Directory);
}
```

#### `SystemMetadataHandler.cs`
**Change**: Return main DLL path as views assembly path.

```csharp
private static string? GetViewsAssemblyPath(PackageInfo package)
{
    // Views are in main assembly, so return main assembly path
    if (package.MainAssembly == null)
        return null;
    
    try
    {
        return package.MainAssembly.Location;
    }
    catch
    {
        return null;
    }
}
```

---

### 3. WebUI Changes

#### `PackageViewsRegistry.cs`
**Change**: Register main assembly, not separate Views assembly.

```csharp
// Since views are in main DLL, we need to register the main DLL
// But wait - main DLLs are already loaded by Core for backend logic
// We need to get the assembly from Core's loaded packages

// Actually, we should get the assembly path from Core API response
// Core already returns viewsAssemblyPath - we just need to use main DLL path
```

**Simplified version**:
```csharp
public async Task RegisterViewsAssembliesAsync(ApplicationPartManager partManager)
{
    var request = JsonSerializer.Serialize(new { action = "get-packages" });
    var responseJson = await _coreClient.SendRequestAsync(request);
    var response = JsonSerializer.Deserialize<JsonElement>(responseJson);
    
    if (!response.TryGetProperty("success", out var success) || !success.GetBoolean())
        return;
    
    if (!response.TryGetProperty("data", out var data))
        return;
    
    var packages = JsonSerializer.Deserialize<List<JsonElement>>(data.GetRawText()) ?? new();
    
    foreach (var package in packages)
    {
        // Get views assembly path (which is now main DLL path)
        if (!package.TryGetProperty("viewsAssemblyPath", out var pathEl))
            continue;
            
        var assemblyPath = pathEl.GetString();
        if (string.IsNullOrEmpty(assemblyPath) || !File.Exists(assemblyPath))
            continue;
        
        // Skip if already registered
        if (IsRegistered(assemblyPath))
            continue;
        
        try
        {
            var assembly = Assembly.LoadFrom(assemblyPath);
            var assemblyPart = new AssemblyPart(assembly);
            partManager.ApplicationParts.Add(assemblyPart);
            _registeredAssemblies.Add(assemblyPath);
            _logger.LogInformation($"Registered Views assembly: {assembly.FullName} from {assemblyPath}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to register Views assembly {assemblyPath}: {ex.Message}");
        }
    }
}
```

#### `RazorPartialRenderer.cs`
**Massive simplification**: Remove 200+ lines of fallback code.

**New simplified `RenderPackagePageAsync`**:
```csharp
public async Task<string> RenderPackagePageAsync(
    HttpContext httpContext, 
    string packageId, 
    string moduleId, 
    string pageId)
{
    // Convert package ID to assembly name: monolith-network -> Monolith.Network
    var assemblyName = ToAssemblyName(packageId);
    var modulePascal = ToPascalCase(moduleId);
    var pagePascal = ToPascalCase(pageId);
    
    // Single view location format: Module/Page
    var viewName = $"{modulePascal}/{pagePascal}";
    
    var actionContext = new ActionContext(
        httpContext,
        httpContext.GetRouteData(),
        new ActionDescriptor())
    {
        RouteData = new RouteData(httpContext.GetRouteData())
    };
    
    // Set controller route value to assembly name for view location
    actionContext.RouteData.Values["controller"] = assemblyName;
    
    // Find view in registered RCL assembly
    var viewResult = _viewEngine.FindView(actionContext, viewName, isMainPage: false);
    
    if (!viewResult.Success)
    {
        // Try Config as fallback (only for pageId != "config")
        if (!string.Equals(pageId, "config", StringComparison.OrdinalIgnoreCase))
        {
            var configViewName = $"{modulePascal}/Config";
            viewResult = _viewEngine.FindView(actionContext, configViewName, isMainPage: false);
        }
        
        if (!viewResult.Success)
        {
            var searched = viewResult.SearchedLocations != null 
                ? string.Join(", ", viewResult.SearchedLocations) 
                : "none";
            throw new FileNotFoundException(
                $"Package page not found: {packageId}/{moduleId}/{pageId}. " +
                $"View '{viewName}' not found in assembly '{assemblyName}'. " +
                $"Searched locations: {searched}");
        }
    }
    
    // Render view
    using var sw = new StringWriter();
    var viewData = new ViewDataDictionary(
        new EmptyModelMetadataProvider(),
        new ModelStateDictionary());
    var tempData = new TempDataDictionary(httpContext, _tempDataProvider);
    
    var viewContext = new ViewContext(
        actionContext,
        viewResult.View,
        viewData,
        tempData,
        sw,
        new HtmlHelperOptions());
    
    await viewResult.View.RenderAsync(viewContext);
    return sw.ToString();
}
```

**Remove**:
- All file system path checking (lines 234-260)
- Multiple view location attempts (lines 150-163)
- Backward compatibility code (lines 62-78)
- Complex fallback logic

#### `Program.cs` (WebUI)
**Remove**:
- File system view locations (line 162)
- Legacy index.html redirect (lines 171-181)
- File system package page routes (lines 697-804)
- WebUI settings file fallback (lines 1898-1922)
- Helper functions: `ResolvePackageAsset`, `FindModuleFolder`, etc.

**Keep**:
- RCL view location formats (lines 156-157)
- Package page route via Razor Pages (line 697 - but simplify)

---

### 4. Package Page Updates

**All package pages need**:
```razor
@page "/p/{package}/{module}/{page}"
@{
    Layout = null;  // CRITICAL for SPA partials
}
```

**Files to update**:
- `monolith-network/Pages/Dhcp/Config.cshtml`
- `monolith-network/Pages/Dhcp/Leases.cshtml`
- `monolith-network/Pages/Dns/Config.cshtml`
- `monolith-vpn/Pages/Ipsec/Config.cshtml`
- `monolith-vpn/Pages/OpenVpn/Config.cshtml`
- `monolith-vpn/Pages/WireGuard/Config.cshtml`
- `monolith-diagnostics/Pages/Diagnostics/Config.cshtml`

---

### 5. Build Scripts

#### `package-mfwpkg.sh`
**Simplify**: Remove all fallback directory checks.

```bash
# Build package (single command, single location)
cd "$PACKAGE_DIR"
dotnet build -c Release --no-incremental

# Single build output location
BACKEND_BUILD_DIR="$PACKAGE_DIR/bin/Release/net10.0"

# Copy main DLL (contains Razor views)
# Pattern: Monolith.Network.dll
PACKAGE_DLL_BASE=$(echo "$PACKAGE_ID" | sed 's/-/./g' | awk -F. '{for(i=1;i<=NF;i++) $i=toupper(substr($i,1,1)) substr($i,2)}1' OFS=.)
PACKAGE_DLL="${PACKAGE_DLL_BASE}.dll"

if [ -f "$BACKEND_BUILD_DIR/$PACKAGE_DLL" ]; then
    cp "$BACKEND_BUILD_DIR/$PACKAGE_DLL" "$STAGING_DIR/backend/"
    # Copy PDB if exists
    if [ -f "$BACKEND_BUILD_DIR/${PACKAGE_DLL%.dll}.pdb" ]; then
        cp "$BACKEND_BUILD_DIR/${PACKAGE_DLL%.dll}.pdb" "$STAGING_DIR/backend/"
    fi
else
    echo "ERROR: Package DLL not found: $PACKAGE_DLL"
    exit 1
fi
```

**Remove**:
- Multiple build output location attempts
- Views DLL copying (doesn't exist)
- Pattern matching fallbacks

---

## Testing Checklist

### Build Test
```bash
cd tmp/monolithfirewall-packages/monolith-network
dotnet build -c Release
# Verify: bin/Release/net10.0/Monolith.Network.dll exists
# Check DLL contains embedded resources:
dotnet exec --runtimeconfig bin/Release/net10.0/Monolith.Network.runtimeconfig.json \
  --depsfile bin/Release/net10.0/Monolith.Network.deps.json \
  --additionalprobingpath /usr/share/dotnet/packs \
  /usr/bin/strings bin/Release/net10.0/Monolith.Network.dll | grep -i "\.cshtml"
```

### Package Test
```bash
./build-scripts/package-mfwpkg.sh monolith-network
# Verify: monolith-network.mfwpkg created
# Verify: backend/Monolith.Network.dll exists
# Verify: NO Monolith.Network.Views.dll (should not exist)
```

### Runtime Test
1. Install package
2. Start WebUI
3. Check logs for: "Registered Views assembly: Monolith.Network"
4. Navigate to `/p/monolith-network/dhcp/config`
5. Verify page renders

---

## Key Points

1. **Views are in main DLL**: When using `Microsoft.NET.Sdk.Razor`, Razor views are embedded in the main assembly, not a separate Views.dll

2. **No separate Views assembly needed**: Remove all code that looks for `*.Views.dll`

3. **Use main assembly for views**: In `PackageLoader`, use `mainAssembly` for both backend and views

4. **Register main DLL as RCL**: In `PackageViewsRegistry`, register the main DLL (which contains views)

5. **Simplify everything**: Remove ALL fallback/legacy code - this is brand new, unreleased code

6. **Single code path**: One way to do things, clear errors when it fails

---

## Files Summary

### Core Files to Modify
1. `PackageScanner.cs` - Remove ViewsDllPath logic
2. `PackageLoader.cs` - Use mainAssembly for views
3. `SystemMetadataHandler.cs` - Return main DLL path as views path
4. `RazorViewDiscovery.cs` - Remove fallback method

### WebUI Files to Modify
1. `RazorPartialRenderer.cs` - Massive simplification (remove 200+ lines)
2. `PageContentRenderer.cs` - Remove fallbacks
3. `PackageViewsRegistry.cs` - Simplify
4. `Program.cs` - Remove legacy routes and helpers

### Build Scripts
1. `package-mfwpkg.sh` - Simplify build process

### Package Files
1. All package .csproj - Already correct (no changes needed)
2. All package Razor pages - Change `Layout = null`

---

## Expected Code Reduction

- **RazorPartialRenderer.cs**: ~330 lines → ~100 lines (-230 lines)
- **Program.cs**: ~1955 lines → ~1800 lines (-155 lines)
- **PackageScanner.cs**: ~170 lines → ~120 lines (-50 lines)
- **PackageLoader.cs**: ~107 lines → ~80 lines (-27 lines)
- **PageContentRenderer.cs**: ~412 lines → ~150 lines (-262 lines)

**Total reduction**: ~724 lines of legacy code removed
