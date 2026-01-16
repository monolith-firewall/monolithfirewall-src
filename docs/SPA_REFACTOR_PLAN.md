# SPA Refactor Plan - Simplified & Reliable Implementation

## Current Problems

1. **Complex Route Matching**: Trying to match endpoints manually is unreliable
2. **Endpoint Execution Issues**: Modifying HttpContext causes routing problems
3. **Content Extraction**: Parsing full HTML to extract content is fragile
4. **Package Pages Not Working**: RCL pages aren't being found/rendered correctly
5. **Too Many Layers**: Multiple fallbacks and workarounds make debugging hard

## What We Want to Keep ✅

- **JS/CSS Separation**: Dynamic loading per page (you love this!)
- **SPA Feel**: No full page reloads, smooth navigation
- **Hash-based Routing**: `#/firewall/aliases` style URLs
- **Dynamic Asset Injection**: Load CSS/JS on demand

## Proposed Solution: Simplified Hybrid SPA

### Core Concept

**Use Razor Pages properly + Simple AJAX partial loading**

Instead of fighting Razor Pages, we'll:
1. Let Razor Pages handle routing naturally
2. Use a simple endpoint that returns just the page content
3. Keep the SPA router for navigation and asset loading
4. Simplify everything!

---

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  Browser (SPA)                                              │
│  - index.html (shell)                                       │
│  - Hash routing (#/firewall/aliases)                        │
│  - AJAX fetch for page content                              │
└─────────────────────────────────────────────────────────────┘
                           │
                           │ GET /api/page?route=/firewall/aliases
                           ▼
┌─────────────────────────────────────────────────────────────┐
│  ASP.NET Core WebUI                                         │
│  - Razor Pages (normal routing)                             │
│  - Simple API endpoint: /api/page                           │
│  - Renders page as partial (no layout)                      │
│  - Returns: { html, css, js }                               │
└─────────────────────────────────────────────────────────────┘
                           │
                           │ Returns JSON with HTML + assets
                           ▼
┌─────────────────────────────────────────────────────────────┐
│  Browser (SPA)                                              │
│  - Injects HTML into #page-content                          │
│  - Loads CSS dynamically                                    │
│  - Loads JS dynamically                                     │
│  - Initializes page                                          │
└─────────────────────────────────────────────────────────────┘
```

---

## Implementation Plan

### Phase 1: Create Simple Page API Endpoint

**New Endpoint**: `GET /api/page?route=/firewall/aliases`

**Returns JSON**:
```json
{
  "success": true,
  "html": "<div class=\"package-page\">...</div>",
  "assets": {
    "css": ["/css/firewall.css"],
    "js": ["/js/aliases.js"]
  }
}
```

**Benefits**:
- ✅ No complex route matching
- ✅ No HTML parsing needed
- ✅ Explicit asset list
- ✅ Works for all pages (internal + packages)

### Phase 2: Simplify Razor Page Rendering

**New Service**: `PageContentRenderer.cs`

```csharp
public class PageContentRenderer
{
    public async Task<PageContent> RenderPageAsync(string route)
    {
        // 1. Find Razor Page by route (use Razor Pages API)
        var page = FindPageByRoute(route);
        
        // 2. Render page (with Layout = null)
        var html = await RenderPageAsync(page);
        
        // 3. Extract assets from @section Scripts
        var assets = ExtractAssets(page);
        
        return new PageContent { Html = html, Assets = assets };
    }
}
```

**Key Simplifications**:
- Use Razor Pages' built-in page finder
- Render directly (no endpoint execution)
- Extract assets from page metadata
- No HTML parsing!

### Phase 3: Update SPA Router

**Simplified Router**:
```javascript
loadPage: async function(pageDef) {
    // Fetch page content from simple API
    const response = await fetch(`/api/page?route=${pageDef.route}`);
    const data = await response.json();
    
    if (data.success) {
        // Inject HTML
        $('#page-content').html(data.html);
        
        // Load CSS
        data.assets.css.forEach(css => loadCSS(css));
        
        // Load JS
        for (const js of data.assets.js) {
            await loadJS(js);
        }
        
        // Initialize page
        if (Monolith.PageLoader) {
            await Monolith.PageLoader.load(pageDef);
        }
    }
}
```

**Benefits**:
- ✅ Much simpler code
- ✅ No HTML parsing
- ✅ No script tag extraction
- ✅ Explicit asset management
- ✅ Easier to debug

### Phase 4: Asset Extraction from Razor Pages

**Problem**: How do we know which CSS/JS a page needs?

**Solution**: Extract from `@section Scripts` at compile time or runtime.

**Option A - Runtime Extraction** (Simpler):
```csharp
// After rendering, parse the HTML for @section Scripts
// But we need to do this BEFORE rendering...

// Better: Use Razor Pages metadata
var pageDescriptor = GetPageDescriptor(route);
var sections = pageDescriptor.Sections; // @section Scripts
```

**Option B - Explicit Asset Registration** (Most Reliable):
```csharp
// In PageModel or page metadata
public class AliasesConfigModel : PageModel
{
    public static string[] RequiredCSS => new[] { "/css/firewall.css" };
    public static string[] RequiredJS => new[] { "/js/aliases.js" };
}
```

**Option C - Convention-Based** (Easiest):
```csharp
// Auto-detect based on route:
// /firewall/aliases -> /css/firewall.css, /js/aliases.js
// /p/monolith-network/dhcp -> /_content/Monolith.Network/css/dhcp.css
```

---

## Recommended Approach: Hybrid

### For Internal Pages (WebUI)
- Use **Option C** (convention-based)
- Route `/firewall/aliases` → assets: `firewall.css`, `aliases.js`
- Simple and predictable

### For Package Pages
- Use **Option B** (explicit registration)
- Package pages declare assets in PageModel
- More flexible for packages

### Implementation

**1. Simple API Endpoint**:
```csharp
app.MapGet("/api/page", async (HttpContext context, PageContentRenderer renderer) =>
{
    var route = context.Request.Query["route"].ToString();
    if (string.IsNullOrEmpty(route))
    {
        return Results.BadRequest("route parameter required");
    }
    
    try
    {
        var content = await renderer.RenderPageAsync(route);
        return Results.Json(new
        {
            success = true,
            html = content.Html,
            assets = new
            {
                css = content.CssAssets,
                js = content.JsAssets
            }
        });
    }
    catch (FileNotFoundException)
    {
        return Results.Json(new { success = false, error = "Page not found" }, statusCode: 404);
    }
});
```

**2. PageContentRenderer**:
```csharp
public class PageContentRenderer
{
    private readonly IPageLoader _pageLoader;
    private readonly IRazorViewEngine _viewEngine;
    
    public async Task<PageContent> RenderPageAsync(string route)
    {
        // Find page using Razor Pages API
        var page = _pageLoader.Load(route);
        
        // Render page (Layout = null is already set)
        var html = await RenderPageHtmlAsync(page);
        
        // Get assets (from convention or page metadata)
        var assets = GetPageAssets(route, page);
        
        return new PageContent
        {
            Html = html,
            CssAssets = assets.css,
            JsAssets = assets.js
        };
    }
    
    private (string[] css, string[] js) GetPageAssets(string route, CompiledPageActionDescriptor page)
    {
        // Try to get from page metadata first
        if (page.HandlerMethods?.FirstOrDefault()?.MethodInfo.DeclaringType
            ?.GetProperty("RequiredCSS")?.GetValue(null) is string[] css)
        {
            // Use explicit assets
        }
        else
        {
            // Use convention-based
            css = GetAssetsByConvention(route);
        }
        
        return (css, js);
    }
}
```

**3. Simplified Router**:
```javascript
Monolith.Router = {
    loadPage: async function(pageDef) {
        try {
            // Fetch from simple API
            const response = await fetch(`/api/page?route=${encodeURIComponent(pageDef.route)}`);
            const data = await response.json();
            
            if (!data.success) {
                throw new Error(data.error || 'Failed to load page');
            }
            
            // Inject HTML
            $('#page-content').html(data.html);
            
            // Load CSS (no duplicates)
            const loadedCSS = new Set();
            data.assets.css.forEach(css => {
                if (!loadedCSS.has(css)) {
                    loadCSS(css);
                    loadedCSS.add(css);
                }
            });
            
            // Load JS (no duplicates, sequential)
            const loadedJS = new Set();
            for (const js of data.assets.js) {
                if (!loadedJS.has(js)) {
                    await loadJS(js);
                    loadedJS.add(js);
                }
            }
            
            // Initialize page
            if (Monolith.PageLoader) {
                await Monolith.PageLoader.load(pageDef);
            }
        } catch (error) {
            console.error('Error loading page:', error);
            $('#page-content').html(`<div class="alert alert-danger">Error: ${error.message}</div>`);
        }
    }
};
```

---

## Migration Strategy

### Step 1: Create New API Endpoint
- Add `/api/page` endpoint
- Implement `PageContentRenderer`
- Test with one page (e.g., `/firewall/aliases`)

### Step 2: Update Router
- Change router to use `/api/page`
- Remove complex HTML parsing
- Simplify asset loading

### Step 3: Test All Pages
- Internal pages (firewall, setup, etc.)
- Package pages (network, vpn, diagnostics)

### Step 4: Remove Old Code
- Remove `/partial` endpoint
- Remove `RazorPartialRenderer` (or simplify it)
- Clean up unused code

---

## Benefits of New Approach

### ✅ Reliability
- Uses Razor Pages API properly
- No complex route matching
- No HTML parsing
- Works for all pages

### ✅ Simplicity
- One endpoint: `/api/page`
- Clear JSON response
- Explicit asset lists
- Easy to debug

### ✅ Maintainability
- Less code
- Clear separation of concerns
- Easy to extend
- Better error handling

### ✅ Performance
- No HTML parsing overhead
- Explicit asset loading
- Better caching opportunities

### ✅ Keeps What You Love
- ✅ JS/CSS separation per page
- ✅ SPA feel (no full reloads)
- ✅ Dynamic asset loading
- ✅ Hash-based routing

---

## Alternative: Keep SPA but Fix Current Issues

If you want to keep the current approach but fix it:

### Option 1: Use Razor Pages Compilation API
- Use `IPageLoader` to find pages
- Use `PageActionInvoker` to render
- Bypass endpoint execution entirely

### Option 2: Use View Components
- Convert pages to View Components
- Render via API endpoint
- Simpler than full Razor Pages

### Option 3: Pre-compile Pages
- Compile all pages at startup
- Cache rendered HTML
- Serve from cache

---

## My Recommendation

**Go with the Simplified Hybrid SPA approach** because:

1. **Much Simpler**: One endpoint, clear JSON, no parsing
2. **More Reliable**: Uses Razor Pages properly
3. **Easier to Debug**: Clear error messages, explicit assets
4. **Keeps Your Features**: JS/CSS separation, SPA feel
5. **Future-Proof**: Easy to extend, maintain, optimize

The current approach is fighting against Razor Pages. The new approach works WITH Razor Pages.

---

## Implementation Time Estimate

- **Phase 1** (API Endpoint): 2-3 hours
- **Phase 2** (PageContentRenderer): 3-4 hours
- **Phase 3** (Update Router): 1-2 hours
- **Phase 4** (Asset Extraction): 2-3 hours
- **Testing**: 2-3 hours

**Total**: ~10-15 hours

---

## Questions to Answer

1. **Asset Discovery**: How should we know which assets a page needs?
   - Convention-based (recommended for simplicity)
   - Explicit in PageModel (more flexible)
   - Extract from @section Scripts (complex)

2. **Backward Compatibility**: Keep `/partial` endpoint during migration?
   - Yes (safer, gradual migration)
   - No (cleaner, faster)

3. **Error Handling**: What should happen if a page fails to load?
   - Show error message in page-content
   - Redirect to dashboard
   - Show 404 page

---

## Next Steps

1. **Decide on approach** (I recommend Simplified Hybrid SPA)
2. **Choose asset discovery method** (I recommend convention-based with explicit fallback)
3. **Start implementation** (I can do this!)
4. **Test thoroughly** (all pages, all scenarios)

---

**What do you think? Should we go with the Simplified Hybrid SPA approach?** 🚀
