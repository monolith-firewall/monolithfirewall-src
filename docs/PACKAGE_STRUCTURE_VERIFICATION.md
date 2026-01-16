# Package Structure Verification

## Verified Structure ✅

### Package Build (.mfwpkg)
```
monolith-network.mfwpkg
├── manifest.json
├── backend/
│   ├── Monolith.Network.dll (contains embedded Razor views)
│   └── Monolith.Network.pdb
└── wwwroot/
    ├── css/
    └── js/
```

### Package Installation Location
**Config**: `/var/lib/monolith-firewall/codelogic/core-config.json`
```json
{
  "PackagesDirectory": "/var/lib/monolith-firewall/packages"
}
```

**Installed Structure**:
```
/var/lib/monolith-firewall/packages/
├── monolith-network/
│   ├── manifest.json
│   ├── backend/
│   │   ├── Monolith.Network.dll ✅ (197KB, contains embedded Razor views)
│   │   └── Monolith.Network.pdb
│   └── wwwroot/
├── monolith-diagnostics/
│   ├── manifest.json
│   ├── backend/
│   │   └── Monolith.Diagnostics.dll ✅ (contains embedded Razor views)
│   └── wwwroot/
└── monolith-vpn/
    ├── manifest.json
    ├── backend/
    │   └── Monolith.Vpn.dll ✅ (contains embedded Razor views)
    └── wwwroot/
```

### Embedded Razor Pages Verified ✅

**monolith-network**:
- Embedded resource: `/Pages/Dhcp/Config.cshtml` ✅
- Type: `mvc.1.0.razor-page` ✅
- Compiled class: `AspNetCoreGeneratedDocument.Pages_Dhcp_Config` ✅

**monolith-diagnostics**:
- Embedded resource: `/Pages/Diagnostics/Config.cshtml` ✅
- Type: `mvc.1.0.razor-page` ✅
- Compiled class: `AspNetCoreGeneratedDocument.Pages_Diagnostics_Config` ✅

### Core API Response ✅

Core returns correct paths:
```json
{
  "viewsAssemblyPath": "/var/lib/monolith-firewall/packages/monolith-network/backend/Monolith.Network.dll",
  "viewsAssemblyName": "Monolith.Network, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
  "packageDirectory": "/var/lib/monolith-firewall/packages/monolith-network"
}
```

## The Real Problem

### Issue: Razor Pages vs Views

**Razor Pages are NOT MVC Views!**

1. **Package pages are Razor Pages** with `@page` directives:
   - `@page "/p/monolith-network/dhcp/config"`
   - These are compiled Razor Pages, not MVC views

2. **View Engine can't find them**:
   - `IRazorViewEngine.FindView()` is for MVC views
   - Razor Pages are discovered via Razor Pages infrastructure
   - They're not accessible via view location formats

3. **Razor Pages should be auto-discovered**:
   - When assembly is registered as `ApplicationPart`
   - ASP.NET Core should discover Razor Pages automatically
   - They should be routable via their `@page` directives

### Current Flow (Broken)

1. Package installed → DLL at `/var/lib/monolith-firewall/packages/{id}/backend/{Name}.dll` ✅
2. Core discovers package → Returns `viewsAssemblyPath` ✅
3. WebUI registers assembly → `AssemblyPart` added to `ApplicationPartManager` ✅
4. **Razor Pages should be discovered** → ❌ Not happening
5. View engine tries to find views → ❌ Can't find (they're not views!)

### Solution Needed

Razor Pages embedded in RCLs need to be accessed via:
- **Razor Pages routing** (not view engine)
- **IPageLoader** or **IPageFactoryProvider**
- Or ensure Razor Pages are properly discovered when assembly is registered

## Next Steps

1. Verify Razor Pages are being discovered when assembly is registered
2. Use Razor Pages infrastructure instead of view engine
3. Or ensure package Razor Pages take precedence over PackagePageWrapper route
