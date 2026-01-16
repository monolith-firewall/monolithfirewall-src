# Package Razor Page Structure

## Overview

All package Razor pages must follow a consistent structure to work correctly with the SPA architecture and embedded view system.

## Required Structure

### 1. Page Directive

All package pages must have a `@page` directive with the format:
```razor
@page "/p/{package-id}/{module-id}/{page-id}"
```

**Examples:**
- `@page "/p/monolith-network/dhcp/config"`
- `@page "/p/monolith-vpn/ipsec/config"`
- `@page "/p/monolith-diagnostics/diagnostics/config"`

**Note:** Hardcoded paths (as shown above) are preferred over parameterized paths (`@page "/p/{package}/{module}/{page}"`) because they are more specific and take precedence over the `PackagePageWrapper` fallback route.

### 2. Layout Configuration

**CRITICAL:** All package pages must use `Layout = null` to render as partial HTML for SPA injection.

```razor
@{
    Layout = null;  // Required for SPA partial rendering
    ViewData["Title"] = "Page Title";
}
```

**Why:** The SPA architecture loads pages dynamically and injects them into the main app shell. Using a layout would cause double-wrapping or layout conflicts.

### 3. Optional: Page Model

If the page needs a model, declare it after the `@page` directive:

```razor
@page "/p/monolith-network/dhcp/config"
@model Monolith.Network.Pages.Dhcp.ConfigModel
@{
    Layout = null;
    ViewData["Title"] = "DHCP Configuration";
}
```

### 4. Page Content Structure

All pages should:
- Be wrapped in a container div with `package-page` class
- Include `data-module` and `data-package` attributes for JavaScript identification
- Use Bootstrap classes for styling (consistent with WebUI)

**Example:**
```razor
<div class="package-page dhcp-page" data-module="dhcp" data-package="monolith-network">
    <div class="container-fluid p-4">
        <!-- Page content -->
    </div>
</div>
```

### 5. Scripts Section

Use `@section Scripts` to include CSS and JS assets:

```razor
@section Scripts {
    <link rel="stylesheet" href="/_content/Monolith.Network/css/dhcp.css" data-module-css="dhcp" />
    <script src="/_content/Monolith.Network/js/dhcp.js" data-module-js="dhcp"></script>
}
```

**Note:** The `data-module-css` and `data-module-js` attributes are used by the SPA to manage asset loading.

## Complete Example

```razor
@page "/p/monolith-network/dhcp/config"
@model Monolith.Network.Pages.Dhcp.ConfigModel
@{
    Layout = null;
    ViewData["Title"] = "DHCP Configuration";
}

<div class="package-page dhcp-page" data-module="dhcp" data-package="monolith-network">
    <div class="container-fluid p-4">
        <div class="row mb-4">
            <div class="col-12">
                <h2 class="page-title">DHCP Server</h2>
                <p class="text-muted">Dynamic Host Configuration Protocol (DHCP) Server Configuration</p>
            </div>
        </div>
        
        <!-- Page content -->
    </div>
</div>

@section Scripts {
    <link rel="stylesheet" href="/_content/Monolith.Network/css/dhcp.css" data-module-css="dhcp" />
    <script src="/_content/Monolith.Network/js/dhcp.js" data-module-js="dhcp"></script>
}
```

## Current Package Pages Status

### ✅ Verified Pages (All Correct)

1. **monolith-network/Pages/Dhcp/Config.cshtml**
   - `@page "/p/monolith-network/dhcp/config"` ✅
   - `Layout = null` ✅
   - Has `@model` ✅

2. **monolith-network/Pages/Dns/Config.cshtml**
   - `@page "/p/monolith-network/dns/config"` ✅
   - `Layout = null` ✅

3. **monolith-vpn/Pages/Ipsec/Config.cshtml**
   - `@page "/p/monolith-vpn/ipsec/config"` ✅
   - `Layout = null` ✅

4. **monolith-vpn/Pages/OpenVpn/Config.cshtml**
   - `@page "/p/monolith-vpn/openvpn/config"` ✅
   - `Layout = null` ✅

5. **monolith-vpn/Pages/WireGuard/Config.cshtml**
   - `@page "/p/monolith-vpn/wireguard/config"` ✅
   - `Layout = null` ✅

6. **monolith-diagnostics/Pages/Diagnostics/Config.cshtml**
   - `@page "/p/monolith-diagnostics/diagnostics/config"` ✅
   - `Layout = null` ✅

### Special Cases

- **monolith-network/Pages/Dhcp/Leases.cshtml**: This is not a Razor page - it's an HTML redirect page. This is fine and doesn't need to follow the Razor page structure.

## Common Mistakes to Avoid

1. ❌ **Using `Layout = "~/Pages/Shared/_Layout.cshtml"`**
   - This causes double-wrapping in the SPA
   - Always use `Layout = null`

2. ❌ **Missing `@page` directive**
   - Pages without `@page` won't be routable
   - The `PackagePageWrapper` will try to render them, but it's better to have explicit routes

3. ❌ **Using parameterized routes when hardcoded is better**
   - Hardcoded routes like `@page "/p/monolith-network/dhcp/config"` are more specific
   - Parameterized routes like `@page "/p/{package}/{module}/{page}"` are less specific and may conflict

4. ❌ **Forgetting `data-module` and `data-package` attributes**
   - These are used by JavaScript to identify pages
   - Always include them on the root container div

## View Discovery

Package pages are discovered automatically by `RazorViewDiscovery` when:
1. The package DLL is loaded
2. Embedded resources ending in `.cshtml` are found
3. The resource name matches the pattern: `{AssemblyName}.Pages.{Module}.{Page}.cshtml`

**Example:** `Monolith.Network.Pages.Dhcp.Config.cshtml` → Route: `/p/monolith-network/dhcp/config`

## Testing

To verify a package page is correctly configured:

1. Build the package: `dotnet build -c Release`
2. Check the DLL contains the embedded resource:
   ```bash
   strings bin/Release/net10.0/Monolith.Network.dll | grep -i "\.cshtml"
   ```
3. Install the package
4. Navigate to the page route in the WebUI
5. Verify the page renders without layout wrapping

## References

- [ASP.NET Core Razor Pages](https://learn.microsoft.com/en-us/aspnet/core/razor-pages/)
- [Razor Class Libraries](https://learn.microsoft.com/en-us/aspnet/core/razor-pages/ui-class)
- [SPA Architecture in WebUI](../docs/RAZOR_COMPILATION_FIX_PLAN.md)
