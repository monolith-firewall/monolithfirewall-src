# Static Assets Fix - Package JS/CSS Files

## Issue ✅ RESOLVED

Package static assets (JS and CSS files) are now being served correctly!

## Problem

The WebUI was trying to load package assets at `/assets/package/{packageId}/{moduleId}/{filename}`, but the route handler was looking in the wrong directory.

## Root Cause

The `packagesRoot` variable was set to `/opt/monolith-firewall/packages`, but packages are actually installed at `/var/lib/monolith-firewall/packages`.

## Fix Applied

**File**: `src/Monolith.FireWall.WebUI/Program.cs`

**Change**: Updated the default packages root path:
```csharp
// Before:
var packagesRoot = Environment.GetEnvironmentVariable("MONOLITH_PACKAGES_ROOT") ?? "/opt/monolith-firewall/packages";

// After:
var packagesRoot = Environment.GetEnvironmentVariable("MONOLITH_PACKAGES_ROOT") ?? "/var/lib/monolith-firewall/packages";
```

## How It Works

1. **Route Handler**: `/assets/package/{package}/{module}/{**filePath}`
   - Located at line 505 in `Program.cs`
   - Handles requests like `/assets/package/monolith-diagnostics/diagnostics/diagnostics.js`

2. **Path Resolution**: `ResolvePackageAsset()` function
   - Converts package ID to folder name (e.g., `monolith-diagnostics`)
   - Looks in `{packagesRoot}/{package}/wwwroot/js/{file}` or `{packagesRoot}/{package}/wwwroot/css/{file}`
   - Falls back to `{packagesRoot}/{package}/wwwroot/{file}`

3. **File Structure**:
   ```
   /var/lib/monolith-firewall/packages/
   ├── monolith-diagnostics/
   │   └── wwwroot/
   │       ├── js/
   │       │   └── diagnostics.js
   │       └── css/
   │           └── diagnostics.css
   ├── monolith-network/
   │   └── wwwroot/
   │       ├── js/
   │       │   ├── dhcp.js
   │       │   └── dns.js
   │       └── css/
   │           ├── dhcp.css
   │           └── dns.css
   └── monolith-vpn/
       └── wwwroot/
           ├── js/
           │   ├── ipsec.js
           │   ├── openvpn.js
           │   └── wireguard.js
           └── css/
               └── wireguard.css
   ```

## Verification ✅

All package assets are now accessible:

- ✅ `/assets/package/monolith-diagnostics/diagnostics/diagnostics.js` - Working
- ✅ `/assets/package/monolith-diagnostics/diagnostics/diagnostics.css` - Working
- ✅ `/assets/package/monolith-network/dhcp/dhcp.js` - Working
- ✅ `/assets/package/monolith-network/dhcp/dhcp.css` - Working
- ✅ `/assets/package/monolith-vpn/ipsec/ipsec.js` - Working

## Content-Type Headers

The route handler automatically sets the correct Content-Type:
- `.js` files → `application/javascript`
- `.css` files → `text/css`
- Other files → Based on extension

## No-Cache Headers

All package assets are served with no-cache headers:
- `Cache-Control: no-cache, no-store, must-revalidate`
- `Pragma: no-cache`
- `Expires: 0`

This ensures that when packages are updated, browsers will fetch the new versions immediately.

## Testing

Test any package asset:
```bash
# Test JS file
curl http://localhost/assets/package/monolith-diagnostics/diagnostics/diagnostics.js

# Test CSS file
curl http://localhost/assets/package/monolith-diagnostics/diagnostics/diagnostics.css
```

## Related Routes

There's also a legacy route handler at `/_content/{packageName}/{**filePath}` that maps:
- `/_content/Monolith.Network/js/file.js` → `/var/lib/monolith-firewall/packages/monolith-network/wwwroot/js/file.js`

This is kept for backwards compatibility but the primary route is `/assets/package/`.
