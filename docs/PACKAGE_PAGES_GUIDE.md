# Package Pages Guide - Razor Class Libraries (RCLs)

This guide explains how to create Razor Pages in package modules that work with the Monolith Firewall WebUI.

## Overview

Package pages are Razor Pages located in Razor Class Libraries (RCLs) that are dynamically loaded by the WebUI. They are accessed via routes like `/p/{package}/{module}/{page?}`.

## Two Approaches

### Approach 1: Direct Razor Page (Recommended)

If your package page has its own `@page` directive matching the route pattern, Razor Pages will handle it directly.

**Example: `/p/monolith-network/dhcp/config`**

Create a Razor Page in your RCL:

```cshtml
@page "/p/monolith-network/dhcp/config"
@{
    Layout = "~/Pages/Shared/_Layout.cshtml";
    ViewData["Title"] = "DHCP Configuration";
}

<div class="container-fluid p-4">
    <h2>DHCP Configuration</h2>
    <!-- Your page content here -->
</div>

@section Scripts {
    <script src="/js/pages/dhcp.js" data-module-js="dhcp"></script>
}

@section Styles {
    <link rel="stylesheet" href="/css/dhcp.css" data-module-css="dhcp" />
}
```

**Key Points:**
- Use `@page "/p/{package}/{module}/{page?}"` directive
- Set `Layout = "~/Pages/Shared/_Layout.cshtml"` to use the shared layout
- Use `@section Scripts` and `@section Styles` for page-specific assets
- The route must match exactly: `/p/monolith-network/dhcp/config`

### Approach 2: Fallback Wrapper

If your package page doesn't have a matching `@page` directive, the `PackagePageWrapper.cshtml` will render it via `RazorPartialRenderer`.

**Requirements:**
- Your package Views assembly must be registered via `PackageViewsRegistry`
- The page should be accessible via view paths like `/_content/{AssemblyName}/Pages/{Module}/{Page}.cshtml`
- The wrapper will automatically use the shared layout

## Layout Reference

Package pages should reference the shared layout using:

```cshtml
Layout = "~/Pages/Shared/_Layout.cshtml";
```

This ensures:
- Consistent navigation bar
- Proper JS/CSS injection
- Standard page structure

## Asset Injection

Use Razor sections for page-specific assets:

```cshtml
@section Scripts {
    <script src="/js/pages/your-module.js" data-module-js="your-module"></script>
}

@section Styles {
    <link rel="stylesheet" href="/css/your-module.css" data-module-css="your-module" />
}
```

The `data-module-js` and `data-module-css` attributes help with module initialization.

## Route Patterns

Package pages follow this route pattern:

```
/p/{package}/{module}/{page?}
```

Where:
- `{package}` - Package ID (e.g., `monolith-network`)
- `{module}` - Module ID (e.g., `dhcp`)
- `{page}` - Optional page ID (defaults to `config`)

Examples:
- `/p/monolith-network/dhcp` → renders `config` page
- `/p/monolith-network/dhcp/config` → renders `config` page
- `/p/monolith-network/dhcp/settings` → renders `settings` page

## Registration

Package Views assemblies are automatically registered via `PackageViewsRegistry` during application startup. The registry:

1. Queries Core API for installed packages
2. Checks if packages have Razor Views (`hasRazorViews`)
3. Loads the Views assembly path (`viewsAssemblyPath`)
4. Registers the assembly with ASP.NET Core's `ApplicationPartManager`

## Troubleshooting

### Page Not Found (404)

1. **Check route matching**: Ensure your `@page` directive matches the requested route exactly
2. **Verify assembly registration**: Check logs for "Registered Views assembly" messages
3. **Check view location**: Ensure your page is in the correct path structure

### Layout Not Applied

1. **Verify layout path**: Use `~/Pages/Shared/_Layout.cshtml` (with `~`)
2. **Check Layout directive**: Ensure `Layout = "~/Pages/Shared/_Layout.cshtml";` is set
3. **Verify Layout = null**: If you set `Layout = null`, the page won't use the shared layout

### Assets Not Loading

1. **Check section names**: Use `@section Scripts` and `@section Styles` (case-sensitive)
2. **Verify asset paths**: Ensure paths are correct relative to `wwwroot`
3. **Check data attributes**: Include `data-module-js` or `data-module-css` attributes

## Best Practices

1. **Always use the shared layout** for consistency
2. **Use semantic HTML** with proper Bootstrap classes
3. **Include page titles** via `ViewData["Title"]`
4. **Organize assets** in `wwwroot/js/pages/` and `wwwroot/css/`
5. **Test routes** after package installation to ensure discovery

## Example Package Page Structure

```
YourPackage.Views/
├── Pages/
│   └── Dhcp/
│       └── Config.cshtml
├── wwwroot/
│   ├── js/
│   │   └── pages/
│   │       └── dhcp.js
│   └── css/
│       └── dhcp.css
└── YourPackage.Views.csproj
```

The `Config.cshtml` should have:

```cshtml
@page "/p/monolith-network/dhcp/config"
@{
    Layout = "~/Pages/Shared/_Layout.cshtml";
    ViewData["Title"] = "DHCP Configuration";
}
```
