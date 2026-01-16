# Razor Pages Rendering Fix

## Problem

Package pages were not loading with error:
```
Package page not found: monolith-diagnostics/diagnostics/config. 
View 'Diagnostics/Config' not found in assembly 'Monolith.Diagnostics'.
```

## Root Cause

Package pages are **Razor Pages** (with `@page` directives), not MVC views. The code was trying to find them using the view engine (`IRazorViewEngine.FindView()`), which only works for MVC views, not Razor Pages.

## Solution

Updated `RazorPartialRenderer.RenderPackagePageAsync()` to:
1. **First try rendering as a Razor Page** via the route (since package pages have `@page` directives)
2. **Fall back to view engine lookup** only if Razor Page rendering fails

## Changes Made

### `RazorPartialRenderer.cs`

Changed `RenderPackagePageAsync()` to:
- Try rendering via `RenderPageByRouteAsync()` first (handles Razor Pages)
- Only fall back to view engine if route rendering fails
- Better error messages showing both attempts

## How It Works

1. Package pages have `@page "/p/{package}/{module}/{page}"` directives
2. When registered as ApplicationParts, Razor Pages are automatically discoverable
3. `RenderPageByRouteAsync()` uses ASP.NET Core's endpoint routing to find and render the Razor Page
4. If that fails, we fall back to view engine lookup (for backwards compatibility)

## Testing

After this fix:
1. Restart WebUI service: `sudo systemctl restart monolith-firewall-webui`
2. Access package pages:
   - `/p/monolith-diagnostics/diagnostics/config`
   - `/p/monolith-network/dhcp/config`
   - `/p/monolith-vpn/ipsec/config`

## Verification

Check WebUI logs to see if packages are registered:
```bash
journalctl -u monolith-firewall-webui -f | grep "Registered Views assembly"
```

You should see:
```
Registered Views assembly: Monolith.Diagnostics, Version=... from /path/to/dll
Registered Views assembly: Monolith.Network, Version=... from /path/to/dll
Registered Views assembly: Monolith.Vpn, Version=... from /path/to/dll
```

## Related Files

- `src/Monolith.FireWall.WebUI/Services/RazorPartialRenderer.cs`
- `src/Monolith.FireWall.WebUI/Services/PackageViewsRegistry.cs`
- `src/Monolith.FireWall.WebUI/Program.cs`
