# CSHTML/Razor Pages Fix Plan

## Problem Analysis

### Current State
The WebUI has a hybrid SPA system where:

1. **Static HTML Loading**: The system currently loads CSHTML files as **static HTML** files directly via `File.ReadAllTextAsync()` in `Program.cs` (lines 607-714)
2. **No Razor Processing**: CSHTML files are NOT being processed by the Razor view engine - they're just read as plain text
3. **No Code-Behind Execution**: The `.cshtml.cs` PageModel files (like `Config.cshtml.cs`) are **never executed**
4. **Manual JS/CSS Injection**: The current system manually injects `<script>` and `<link>` tags at the bottom of CSHTML files and loads them dynamically via the SPA router

### Why It Doesn't Work

1. **No Razor Compilation**:
   ```csharp
   // Program.cs line 636-641 - This just reads the file as text!
   var content = await System.IO.File.ReadAllTextAsync(filePath);
   context.Response.ContentType = "text/html";
   await context.Response.WriteAsync(content);
   ```
   - Razor directives like `@page`, `@model`, `@{ }` are sent as-is to the browser
   - C# code in CSHTML is not executed
   - PageModel properties are never bound

2. **ASP.NET Core Razor Pages Setup**:
   - The project has `builder.Services.AddRazorPages()` (line 109)
   - But routes are manually handled, bypassing the Razor Pages middleware
   - The system uses `app.MapGet()` instead of `app.MapRazorPages()`

3. **Package CSHTML Files**:
   - External package CSHTML files in `/opt/monolith-firewall/packages/` are even worse off
   - They're not compiled into the WebUI assembly
   - No Razor Class Library (RCL) compilation happens
   - Just raw file reads

### What We Need

We want ALL pages to be:
- ✅ Proper Razor Pages with full server-side processing
- ✅ Support for `@page`, `@model`, `@{}` directives
- ✅ Code-behind execution (`.cshtml.cs` PageModel classes)
- ✅ Dynamic JS/CSS injection from page content
- ✅ SPA-style navigation (no full page reloads)
- ✅ Work for both WebUI internal pages AND external package pages

---

## Solution Architecture

### Approach: Hybrid SPA with Server-Side Razor Rendering

We'll transform the system into a **true SPA with server-rendered Razor partials**:

```
┌─────────────────────────────────────────────────────────────┐
│  Browser (SPA)                                              │
│  - Single page app (index.html)                             │
│  - Hash-based routing (#/page)                              │
│  - jQuery + Bootstrap                                       │
└─────────────────────────────────────────────────────────────┘
                           │
                           │ AJAX request for partial
                           ▼
┌─────────────────────────────────────────────────────────────┐
│  ASP.NET Core WebUI                                         │
│  - Razor Pages middleware                                   │
│  - Renders CSHTML → HTML (server-side)                      │
│  - Executes PageModel code-behind                           │
│  - Returns HTML + CSS/JS references                         │
└─────────────────────────────────────────────────────────────┘
                           │
                           │ Returns rendered HTML
                           ▼
┌─────────────────────────────────────────────────────────────┐
│  Browser (SPA)                                              │
│  - Injects HTML into #page-content                          │
│  - Extracts and loads <script> tags                         │
│  - Extracts and loads <link> tags                           │
│  - Initializes page JS                                      │
└─────────────────────────────────────────────────────────────┘
```

---

## Implementation Plan

### Phase 1: Enable Razor Pages Rendering

#### 1.1 Add Razor Pages Middleware
**File**: `Program.cs`

Replace the current manual file reading with proper Razor Pages routing:

```csharp
// REMOVE these manual routes (lines 607-714):
// app.MapGet("/p/{package}/{module}", async (HttpContext context, string package, string module) => { ... })
// app.MapGet("/p/{package}/{module}/{page}", async (HttpContext context, string package, string module, string page) => { ... })

// ADD Razor Pages middleware:
app.MapRazorPages(); // This enables all Razor Pages

// ADD a special SPA partial rendering endpoint
app.MapGet("/partial/{**path}", async (HttpContext context, string path) =>
{
    // Render Razor Page as partial (without layout)
    // Return just the HTML content
});
```

#### 1.2 Create Partial Rendering Helper
**New File**: `Services/RazorPartialRenderer.cs`

```csharp
public class RazorPartialRenderer
{
    private readonly IRazorViewEngine _viewEngine;
    private readonly ITempDataProvider _tempDataProvider;

    public async Task<string> RenderPageAsync(HttpContext context, string pagePath)
    {
        // Find the Razor Page
        var actionContext = new ActionContext(context, ...);
        var page = _viewEngine.FindPage(actionContext, pagePath);
        
        // Render without layout
        var viewContext = new ViewContext(...) { Layout = null };
        
        // Execute PageModel code-behind
        await page.ExecuteAsync(viewContext);
        
        // Return rendered HTML
        return renderedHtml;
    }
}
```

#### 1.3 Update Page Directive
**All CSHTML files**:

```razor
@page "/p/package/module/page"
@model Monolith.Network.Pages.Dhcp.ConfigModel
@{
    Layout = null; // Critical: No layout for SPA partials!
}

<!-- Page content here -->
```

---

### Phase 2: Dynamic Asset Extraction

The SPA router needs to extract `<script>` and `<link>` tags from the rendered HTML and load them dynamically.

#### 2.1 Update CSHTML Asset References
**Pattern for all CSHTML files**:

```razor
@page "/p/monolith-network/dhcp/config"
@model Monolith.Network.Pages.Dhcp.ConfigModel
@{
    Layout = null;
}

<div class="package-page dhcp-page">
    <!-- Page content -->
</div>

@section Scripts {
    <link rel="stylesheet" href="/_content/Monolith.Network/css/dhcp.css" data-module-css="dhcp" />
    <script src="/_content/Monolith.Network/js/dhcp.js" data-module-js="dhcp"></script>
}
```

#### 2.2 Update SPA Router to Extract Assets
**File**: `wwwroot/js/core/monolith.router.js`

```javascript
loadPackagePage: async function(pageDef) {
    try {
        // Fetch rendered HTML from server
        const response = await fetch(`/partial${pageDef.route}`);
        const html = await response.text();
        
        // Parse HTML
        const parser = new DOMParser();
        const doc = parser.parseFromString(html, 'text/html');
        
        // Extract and remove asset tags
        const cssLinks = doc.querySelectorAll('link[data-module-css]');
        const jsScripts = doc.querySelectorAll('script[data-module-js]');
        
        // Inject content
        $('#page-content').html(doc.body.innerHTML);
        
        // Load CSS
        cssLinks.forEach(link => {
            if (!document.getElementById(link.id)) {
                document.head.appendChild(link.cloneNode(true));
            }
        });
        
        // Load JS
        for (const script of jsScripts) {
            await this.loadScript(script.src);
        }
        
        // Initialize page
        if (Monolith.PageLoader) {
            await Monolith.PageLoader.load(pageDef);
        }
    } catch (error) {
        console.error('Error loading page:', error);
    }
}
```

---

### Phase 3: Package Razor Pages Support

#### 3.1 Create Razor Class Library (RCL) for Packages
Each package needs to be configured as an RCL.

**File**: `monolith-network/Monolith.Network.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <AddRazorSupportForMvc>true</AddRazorSupportForMvc>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../../src/Monolith.FireWall.Common/Monolith.FireWall.Common.csproj" />
  </ItemGroup>
</Project>
```

#### 3.2 Update Package Loading
**File**: `Monolith.FireWall.Core` - PackageLoader

When loading a package:
1. Load main assembly
2. Load Views RCL assembly (if exists)
3. Register RCL with WebUI's Razor engine via Core API

**Communication Flow**:
```
Core loads package
  → Discovers Views assembly
  → Sends assembly path to WebUI via Unix socket
  → WebUI loads assembly into ApplicationPartManager
  → Razor engine can now find package pages
```

---

### Phase 4: Refactor Existing Pages

#### 4.1 WebUI Internal Pages

**Current**: Firewall pages use raw HTML in `Pages/Firewall/*/Config.cshtml`

**Convert to proper Razor**:

```razor
@page "/firewall/aliases"
@model Monolith.FireWall.WebUI.Pages.Firewall.Aliases.ConfigModel
@{
    Layout = null;
    var aliases = Model.GetAliases(); // Code-behind method
}

<div class="firewall-page aliases-page">
    <h2>Firewall Aliases</h2>
    
    <!-- Now we can use Razor syntax! -->
    @foreach (var alias in aliases)
    {
        <div class="alias-item">
            <strong>@alias.Name</strong>: @alias.Type
        </div>
    }
</div>

@section Scripts {
    <link rel="stylesheet" href="/css/firewall.css" data-module-css="firewall-aliases" />
    <script src="/js/aliases.js" data-module-js="aliases"></script>
}
```

**Code-behind** (ConfigModel.cs):

```csharp
public class ConfigModel : PageModel
{
    private readonly AliasesManager _aliasesManager;
    
    public ConfigModel(AliasesManager aliasesManager)
    {
        _aliasesManager = aliasesManager;
    }
    
    public List<FirewallAlias> Aliases { get; set; } = new();
    
    public async Task OnGetAsync()
    {
        Aliases = await _aliasesManager.GetAllAsync();
    }
}
```

#### 4.2 Package Pages
Same pattern for all package pages.

---

### Phase 5: Testing & Validation

#### 5.1 Test WebUI Pages
- [ ] Dashboard loads and renders
- [ ] Users page loads and executes code-behind
- [ ] Settings page loads with tabs
- [ ] Firewall rules page renders existing data

#### 5.2 Test Package Pages
- [ ] DHCP config page loads
- [ ] PageModel executes and binds data
- [ ] JS/CSS are loaded dynamically
- [ ] Forms work and submit to API

#### 5.3 Test Navigation
- [ ] SPA navigation works (no full page reload)
- [ ] Back/forward browser buttons work
- [ ] Direct URL access works

---

## Key Benefits

### ✅ Full Razor Support
- Use `@model`, `@foreach`, `@if`, etc.
- Server-side C# code execution
- Type-safe view models
- IntelliSense in IDE

### ✅ Code-Behind Execution
- PageModel classes run on page load
- Dependency injection works
- Data binding to view
- Business logic separation

### ✅ SPA Experience Maintained
- No full page reloads
- Fast navigation
- Browser history works
- Progressive enhancement

### ✅ Dynamic Asset Loading
- CSS loaded per-page
- JS loaded per-page
- No conflicts between pages
- Clean unloading on navigation

### ✅ Package System Works
- External packages can use Razor
- RCL compilation
- Same capabilities as internal pages
- Hot-reload support (in dev)

---

## Migration Checklist

### Step 1: Core Infrastructure
- [ ] Add `RazorPartialRenderer` service
- [ ] Update `Program.cs` with `/partial/{**path}` route
- [ ] Remove manual file reading routes
- [ ] Add `app.MapRazorPages()`

### Step 2: Update SPA Router
- [ ] Modify `monolith.router.js` to fetch from `/partial/`
- [ ] Add HTML parsing logic
- [ ] Extract and load `<script>` tags
- [ ] Extract and load `<link>` tags

### Step 3: Refactor WebUI Pages
- [ ] Update all `Pages/Firewall/*.cshtml` with `@page` directive
- [ ] Set `Layout = null` in all pages
- [ ] Add code-behind logic to `.cshtml.cs` files
- [ ] Move inline JS to separate files

### Step 4: Package RCL Setup
- [ ] Convert `Monolith.Network.csproj` to Razor SDK
- [ ] Update all package `.cshtml` files with `@page`
- [ ] Test package page loading

### Step 5: Asset Management
- [ ] Standardize `@section Scripts` pattern
- [ ] Add `data-module-css` and `data-module-js` attributes
- [ ] Test dynamic loading

### Step 6: Testing
- [ ] Test all internal pages
- [ ] Test all package pages
- [ ] Test navigation
- [ ] Test asset loading/unloading
- [ ] Test code-behind execution

---

## Example: Before vs After

### Before (Current - Broken)
```csharp
// Program.cs - Just reads file as text
app.MapGet("/p/{package}/{module}/{page}", async (context, package, module, page) => {
    var filePath = $"/opt/monolith-firewall/packages/{package}/Pages/{module}/{page}.cshtml";
    var content = await File.ReadAllTextAsync(filePath); // ❌ No Razor processing!
    await context.Response.WriteAsync(content);
});
```

```razor
<!-- Config.cshtml - Razor directives sent to browser as text -->
@page "/p/monolith-network/dhcp/config"  ❌ Never processed
@model Monolith.Network.Pages.Dhcp.ConfigModel  ❌ Never bound

@{  ❌ This code never executes!
    var leaseTime = Model.DefaultLeaseTime;
}
```

### After (Fixed)
```csharp
// Program.cs - Proper Razor rendering
app.MapRazorPages();

app.MapGet("/partial/{**path}", async (context, path, renderer) => {
    var html = await renderer.RenderPageAsync(context, path); // ✅ Full Razor processing
    context.Response.ContentType = "text/html";
    await context.Response.WriteAsync(html);
});
```

```razor
<!-- Config.cshtml - Fully processed Razor Page -->
@page "/p/monolith-network/dhcp/config"  ✅ Registered as route
@model Monolith.Network.Pages.Dhcp.ConfigModel  ✅ Model bound
@{
    Layout = null;  // ✅ SPA partial, no layout
    var leaseTime = Model.DefaultLeaseTime;  ✅ Executes server-side!
}

<div>
    <p>Lease Time: @leaseTime seconds</p>  ✅ Value rendered!
</div>

@section Scripts {
    <script src="/js/dhcp.js" data-module-js="dhcp"></script>
}
```

```javascript
// monolith.router.js - Loads rendered HTML
const response = await fetch(`/partial/p/monolith-network/dhcp/config`);
const html = await response.text(); // ✅ Gets fully rendered HTML
$('#page-content').html(html);  // ✅ Inject into SPA
```

---

## Timeline Estimate

- **Phase 1 (Core)**: 4-6 hours
- **Phase 2 (Router)**: 2-3 hours
- **Phase 3 (Packages)**: 3-4 hours
- **Phase 4 (Refactor)**: 8-12 hours (depends on page count)
- **Phase 5 (Testing)**: 4-6 hours

**Total**: ~21-31 hours

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Breaking existing pages | High | Migrate incrementally, test each page |
| Package RCL compilation issues | Medium | Use well-tested RCL patterns, check docs |
| Dynamic JS loading conflicts | Medium | Use unique module IDs, proper cleanup |
| Performance (server-side rendering) | Low | Cache rendered pages, use async rendering |

---

## Success Criteria

✅ All CSHTML files are processed by Razor engine  
✅ Code-behind (`.cshtml.cs`) executes on page load  
✅ Razor directives (`@page`, `@model`, `@{}`) work  
✅ Package pages render correctly  
✅ SPA navigation works without full page reloads  
✅ JS/CSS load dynamically per page  
✅ No regression in existing functionality  

---

## Notes

- This is a **significant refactoring** but necessary for proper Razor support
- The SPA experience is **preserved** - users won't notice the change
- This enables **much more powerful** page development (loops, conditions, partials, etc.)
- Packages gain **full parity** with internal pages
- This is the **correct ASP.NET Core pattern** for SPA + Razor

---

**Next Step**: Begin Phase 1 implementation with `RazorPartialRenderer` service.
