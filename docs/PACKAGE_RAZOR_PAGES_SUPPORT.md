# Package Razor Pages Support

## Overview

Package pages (from `monolithfirewall-packages/`) now have full Razor support! All package CSHTML files have been updated to work with the new Razor rendering system.

---

## What Was Fixed

### 1. Package CSHTML Files Updated

All package pages now have:
- ✅ `@page` directive with full route path
- ✅ `Layout = null` for SPA partials
- ✅ `@section Scripts` wrapping asset tags

**Updated Packages**:
- **monolith-network**: DHCP, DNS pages
- **monolith-vpn**: IPsec, OpenVPN, WireGuard pages
- **monolith-diagnostics**: Diagnostics page

### 2. RazorPartialRenderer Enhanced

The `RazorPartialRenderer` service now:
- ✅ Converts package IDs to assembly names (e.g., `monolith-network` → `Monolith.Network`)
- ✅ Tries multiple view path formats for RCL views
- ✅ Uses proper `/_content/{AssemblyName}/Pages/{Module}/{Page}` format
- ✅ Falls back to route-based rendering if view paths don't work

### 3. Package Assembly Registration

The `PackageViewsRegistry` service (already existed) registers package Views assemblies with the Razor engine:
- ✅ Loads Views assemblies from packages
- ✅ Registers them with `ApplicationPartManager`
- ✅ Enables Razor engine to find package pages

---

## How Package Pages Work

### Request Flow

```
1. User navigates to: #/p/monolith-network/dhcp/config
   ↓
2. SPA Router fetches: GET /partial/p/monolith-network/dhcp/config
   ↓
3. Server (RazorPartialRenderer):
   - Converts: monolith-network → Monolith.Network
   - Tries view paths:
     * /_content/Monolith.Network/Pages/Dhcp/Config
     * /_content/Monolith.Network/Pages/Dhcp/Config (with .cshtml)
     * /Pages/Dhcp/Config (fallback)
   - Finds view in registered RCL assembly
   - Executes code-behind (Config.cshtml.cs)
   - Renders Razor syntax
   - Returns HTML
   ↓
4. Browser receives rendered HTML
   ↓
5. SPA Router extracts assets and injects content
```

### Package Page Structure

**monolith-network/Pages/Dhcp/Config.cshtml**:
```razor
@page "/p/monolith-network/dhcp/config"
@model Monolith.Network.Pages.Dhcp.ConfigModel
@{
    Layout = null;
}

<div class="package-page dhcp-page">
    <!-- Page content -->
    <h2>DHCP Configuration</h2>
    
    @if (Model.IsServiceRunning)
    {
        <p>Service is running with @Model.ActiveLeases leases</p>
    }
</div>

@section Scripts {
    <link rel="stylesheet" href="/_content/Monolith.Network/css/dhcp.css" data-module-css="dhcp" />
    <script src="/_content/Monolith.Network/js/dhcp.js" data-module-js="dhcp"></script>
}
```

**monolith-network/Pages/Dhcp/Config.cshtml.cs**:
```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;
using Monolith.Network.Modules.Dhcp;

namespace Monolith.Network.Pages.Dhcp;

public class ConfigModel : PageModel
{
    private readonly DhcpManager _dhcpManager;
    
    public ConfigModel(DhcpManager dhcpManager)
    {
        _dhcpManager = dhcpManager;  // Dependency injection works!
    }
    
    public bool IsServiceRunning { get; set; }
    public int ActiveLeases { get; set; }
    
    public async Task OnGetAsync()
    {
        // This code executes on page load!
        IsServiceRunning = await _dhcpManager.IsServiceRunningAsync();
        ActiveLeases = await _dhcpManager.GetActiveLeasesCountAsync();
    }
}
```

---

## Package Project Configuration

### RCL (Razor Class Library) Setup

Package projects must use `Microsoft.NET.Sdk.Razor`:

**monolith-network/Monolith.Network.csproj**:
```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <AddRazorSupportForMvc>true</AddRazorSupportForMvc>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
</Project>
```

### View Location Formats

The WebUI is configured to find package views:

**Program.cs**:
```csharp
builder.Services.Configure<RazorViewEngineOptions>(options =>
{
    // Package views in RCLs
    options.ViewLocationFormats.Add("/_content/{0}/Pages/{1}" + RazorViewEngine.ViewExtension);
    options.ViewLocationFormats.Add("/_content/{0}/Views/{1}" + RazorViewEngine.ViewExtension);
});
```

Where `{0}` is the assembly name (e.g., `Monolith.Network`) and `{1}` is the view path.

---

## Package Assembly Name Mapping

The system converts package IDs to assembly names:

| Package ID | Assembly Name |
|------------|---------------|
| `monolith-network` | `Monolith.Network` |
| `monolith-vpn` | `Monolith.Vpn` |
| `monolith-diagnostics` | `Monolith.Diagnostics` |

**Conversion Logic**:
- Split by hyphen: `monolith-network` → `["monolith", "network"]`
- Capitalize each part: `["Monolith", "Network"]`
- Join with dot: `Monolith.Network`

---

## Testing Package Pages

### Verify Package Assembly Registration

Check WebUI startup logs:
```
✓ Package Views assemblies registered
Registered Views assembly: Monolith.Network, Version=1.0.0.0 from /opt/monolith-firewall/packages/monolith-network/...
```

### Test Package Page Loading

1. Navigate to: `#/p/monolith-network/dhcp/config`
2. Check browser DevTools → Network tab
3. Look for: `GET /partial/p/monolith-network/dhcp/config`
4. Verify response is rendered HTML (not raw CSHTML)

### Test Code-Behind Execution

1. Add a breakpoint in `Config.cshtml.cs` → `OnGetAsync()`
2. Navigate to the page
3. Breakpoint should hit (code-behind executes!)

### Test Razor Syntax

Add to package CSHTML:
```razor
@{
    var test = "Hello from Razor!";
}
<p>@test</p>
```

Should render: `<p>Hello from Razor!</p>` (not the literal `@test`)

---

## Troubleshooting

### Package Page Not Found

**Error**: `Package page not found: monolith-network/dhcp/config`

**Solutions**:
1. Check if package Views assembly is registered (check startup logs)
2. Verify assembly name matches: `Monolith.Network` (not `monolith-network`)
3. Check view location formats in `Program.cs`
4. Verify package project uses `Microsoft.NET.Sdk.Razor`

### View Not Found in Searched Locations

**Error**: `View not found: /_content/Monolith.Network/Pages/Dhcp/Config`

**Solutions**:
1. Verify package assembly is loaded: Check `PackageViewsRegistry` logs
2. Check assembly name conversion: `monolith-network` → `Monolith.Network`
3. Verify view path: Should be `Pages/Dhcp/Config.cshtml` in package
4. Check RCL compilation: Package must be built as RCL

### Code-Behind Not Executing

**Symptoms**: PageModel properties are null, `OnGetAsync()` never runs

**Solutions**:
1. Verify `@model` directive matches PageModel class name
2. Check namespace matches: `Monolith.Network.Pages.Dhcp.ConfigModel`
3. Verify dependency injection: Services must be registered
4. Check for compilation errors in package project

---

## Package Page Checklist

When creating a new package page:

- [ ] Use `@page` directive with full route: `/p/{package-id}/{module}/{page}`
- [ ] Set `Layout = null` for SPA partials
- [ ] Add `@model` directive if using code-behind
- [ ] Create `.cshtml.cs` file with PageModel class
- [ ] Wrap assets in `@section Scripts`
- [ ] Use `data-module-css` and `data-module-js` attributes
- [ ] Verify package project uses `Microsoft.NET.Sdk.Razor`
- [ ] Test page loads via `/partial/p/{package}/{module}/{page}`
- [ ] Test code-behind executes (add breakpoint)
- [ ] Test Razor syntax works (`@foreach`, `@if`, etc.)

---

## Example: Complete Package Page

**Pages/Dhcp/Config.cshtml**:
```razor
@page "/p/monolith-network/dhcp/config"
@model Monolith.Network.Pages.Dhcp.ConfigModel
@{
    Layout = null;
}

<div class="container-fluid p-4">
    <h2>DHCP Server</h2>
    
    @if (Model.IsRunning)
    {
        <div class="alert alert-success">
            Running with @Model.LeaseCount active lease(s)
        </div>
    }
    
    <div class="card">
        <div class="card-header">Interfaces</div>
        <div class="card-body">
            @foreach (var iface in Model.Interfaces)
            {
                <div class="mb-2">
                    <strong>@iface.Name</strong>
                    @if (iface.DhcpEnabled)
                    {
                        <span class="badge bg-success">DHCP Enabled</span>
                    }
                </div>
            }
        </div>
    </div>
</div>

@section Scripts {
    <link rel="stylesheet" href="/_content/Monolith.Network/css/dhcp.css" data-module-css="dhcp" />
    <script src="/_content/Monolith.Network/js/dhcp.js" data-module-js="dhcp"></script>
}
```

**Pages/Dhcp/Config.cshtml.cs**:
```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;
using Monolith.Network.Modules.Dhcp;

namespace Monolith.Network.Pages.Dhcp;

public class ConfigModel : PageModel
{
    private readonly DhcpManager _manager;
    
    public ConfigModel(DhcpManager manager)
    {
        _manager = manager;
    }
    
    public bool IsRunning { get; set; }
    public int LeaseCount { get; set; }
    public List<DhcpInterface> Interfaces { get; set; } = new();
    
    public async Task OnGetAsync()
    {
        IsRunning = await _manager.IsServiceRunningAsync();
        LeaseCount = await _manager.GetActiveLeasesCountAsync();
        Interfaces = await _manager.GetInterfacesAsync();
    }
}
```

---

## Summary

✅ **All package pages now support full Razor syntax**  
✅ **Code-behind execution works**  
✅ **Dependency injection works in PageModel**  
✅ **Assets load dynamically via SPA router**  
✅ **Package assemblies are automatically registered**  

Package pages work exactly like WebUI internal pages - full Razor support with server-side rendering! 🎉
