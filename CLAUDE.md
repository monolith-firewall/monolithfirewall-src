# Monolith Firewall - Development Notes

## Project Structure

- `src/Monolith.FireWall.Core/` - Core service (runs as systemd service, communicates via Unix socket)
- `src/Monolith.FireWall.WebUI/` - ASP.NET Razor Pages web interface
- `src/Monolith.FireWall.Common/` - Shared models and interfaces
- `src/Monolith.FireWall.Platform/` - Platform-specific implementations
- `tmp/monolithfirewall-packages/` - Separate git repo (https://github.com/monolith-firewall/monolithfirewall-packages) cloned into tmp/. Contains add-on packages (monolith-diagnostics, monolith-network, monolith-vpn). This directory is gitignored and not part of the main repo.

## Installation Paths

- **Source**: `/home/mlf/project/monolithfirewall-src/`
- **Installed WebUI**: `/opt/monolith-firewall/webui/`
- **Installed Core**: `/opt/monolith-firewall/core/`
- **Data**: `/var/lib/monolith-firewall/`
- **Socket**: `/var/lib/monolith-firewall/run/monolith-core.sock`

## Services

```bash
sudo systemctl restart monolith-firewall-core
sudo systemctl restart monolith-firewall-webui
```

## WebUI JavaScript API Patterns

### Monolith.Core.call() - Core API Wrapper

The WebUI uses `Monolith.Core.call(action, payload)` to communicate with the Core service via `/api/core`.

**Important**: The `Monolith.Core` namespace is NOT loaded globally. Each page JS file that needs it must include this helper at the top:

```javascript
// Ensure Monolith.Core exists for API calls
if (!window.Monolith) window.Monolith = {};
if (!Monolith.Core) {
    Monolith.Core = {
        call: async function(action, payload) {
            try {
                var requestBody = { action: action };
                if (payload && Object.keys(payload).length > 0) {
                    requestBody.payload = payload;
                }
                var response = await Monolith.API.post('/api/core', requestBody);
                return {
                    success: response.success || response.Success || false,
                    data: response.data || response.Data || null,
                    error: response.error || response.Error || null
                };
            } catch (error) {
                console.error('Core API error:', error);
                return { success: false, data: null, error: error.message };
            }
        }
    };
}
```

### Alternative: Direct API calls

Existing pages use `Monolith.API.post('/api/core', { action: 'action.name', ...params })` directly. The response uses PascalCase (`Success`, `Data`, `Error`).

### Other Monolith namespaces (always available)

- `Monolith.API` - REST API client (get, post, put, delete)
- `Monolith.UI` - UI helpers (toast, showModal, confirm, showLoading)
- `Monolith.Auth` - Authentication helpers
- `Monolith.SignalR` - Real-time event subscriptions

## Core API Handler Pattern

Handlers in `Transport/Handlers/` implement `ICoreRequestHandler`:

```csharp
public sealed class MyHandler : ICoreRequestHandler
{
    private static readonly HashSet<string> Actions = new(StringComparer.OrdinalIgnoreCase)
    {
        "my.action.list",
        "my.action.get"
    };

    public bool CanHandle(string action) => Actions.Contains(action);

    public async Task<ApiResponse> HandleAsync(CoreRequestContext context, JsonElement request, CancellationToken ct)
    {
        // Handle actions
    }
}
```

Register handlers in `UnixSocketListener.cs` constructor `_coreHandlers` list.

## Testing Core Socket

```bash
echo '{"action":"gateway.groups.list"}' | sudo socat - UNIX-CONNECT:/var/lib/monolith-firewall/run/monolith-core.sock
```

## Deploying Changes

After modifying source files, copy to installed location:

```bash
# JS files (no restart needed)
sudo cp src/Monolith.FireWall.WebUI/wwwroot/js/pages/*.js /opt/monolith-firewall/webui/wwwroot/js/pages/

# C# changes require rebuild and restart
dotnet build src/Monolith.FireWall.WebUI/Monolith.FireWall.WebUI.csproj
sudo systemctl restart monolith-firewall-webui
```

## Response Case Sensitivity

- Core returns **PascalCase**: `{ "Success": true, "Data": [...], "Error": null }`
- JavaScript expects **camelCase**: normalize responses when consuming

---

## Creating Pages in Packages/Modules

### Package Structure

```
packages/monolith-mypackage/
├── manifest.json
├── Monolith.MyPackage.dll          # Main assembly with Razor views embedded
├── Package.cs                       # IMonolithPackage implementation
├── PackageDefinition.cs             # IMonolithPackageDefinition
└── Modules/
    └── MyModule/
        ├── MyModule.cs              # Module with attributes
        └── Pages/
            └── Config.cshtml        # Razor pages (embedded in DLL)
```

### manifest.json

```json
{
  "id": "monolith-mypackage",
  "name": "My Package",
  "version": "1.0.0",
  "description": "Package description",
  "author": "Author Name",
  "dependencies": []
}
```

### Package Definition (PackageDefinition.cs)

```csharp
public class PackageDefinition : IMonolithPackageDefinition
{
    public string Id => "monolith-mypackage";
    public string Name => "My Package";
    public string Version => "1.0.0";
    public string Description => "Package description";
    public string Author => "Author Name";
    public string[] Dependencies => Array.Empty<string>();

    public IEnumerable<IMonolithModule> GetModules()
    {
        return new IMonolithModule[]
        {
            new Modules.MyModule.MyModule()
        };
    }
}
```

### Module with Pages (MyModule.cs)

Use attributes to declare pages, menus, and permissions:

```csharp
using Monolith.FireWall.Common.Attributes;
using Monolith.FireWall.Common.Modules;

[Module("mymodule", "My Module", Description = "Module description")]
[Package("monolith-mypackage")]

// Menu Items
[MenuItem("mymodule-config", "Configuration", "gear", Order = 10,
    RequiredPermissions = new[] { "mymodule.read" })]
[MenuItem("mymodule-status", "Status", "activity", Order = 20,
    RequiredPermissions = new[] { "mymodule.read" })]

// Pages - Route and Razor view path
[Page("/p/monolith-mypackage/mymodule/config",
    "/_content/Monolith.MyPackage/Pages/MyModule/Config.cshtml",
    RequiredPermissions = new[] { "mymodule.read" })]
[Page("/p/monolith-mypackage/mymodule/status",
    "/_content/Monolith.MyPackage/Pages/MyModule/Status.cshtml",
    RequiredPermissions = new[] { "mymodule.read" })]

// Permissions
[Permission("mymodule.read", "Read my module configuration")]
[Permission("mymodule.write", "Modify my module configuration")]

public class MyModule : MonolithModuleBase
{
    // Module logic - attributes handle metadata
}
```

### Page Route Convention

```
/p/{package-id}/{module-id}/{page-name}

Examples:
- /p/monolith-vpn/wireguard/config
- /p/monolith-network/dhcp/leases
- /p/monolith-diagnostics/diagnostics/ping
```

### Razor Page Template

```html
@page "/p/monolith-mypackage/mymodule/config"
@{
    Layout = "~/Pages/Shared/_Layout.cshtml";
    ViewData["Title"] = "My Module Config";
}

<div id="mymodule-config-container"></div>

@section Scripts {
    <script src="/_content/Monolith.MyPackage/js/mymodule-config.js"></script>
}
```

### Key Interfaces

| Interface | Purpose |
|-----------|---------|
| `IMonolithPackageDefinition` | Package metadata and module list |
| `IMonolithPackage` | Package lifecycle (OnLoadAsync, OnInstallAsync) |
| `IMonolithModule` | Module interface (routes, menus, pages, widgets) |
| `MonolithModuleBase` | Base class for modules using attributes |

### Attributes Reference

| Attribute | Purpose |
|-----------|---------|
| `[Module(id, name)]` | Declare module ID and name |
| `[Package(packageId)]` | Associate module with package |
| `[Page(route, razorPath)]` | Register a page route |
| `[MenuItem(id, label, icon)]` | Add menu item |
| `[Permission(id, description)]` | Declare required permission |
| `[Widget(id, title)]` | Register dashboard widget |

### Package Loading Flow

1. `PackageScanner` finds `manifest.json` in `packages/` directory
2. `PackageLoader` loads the DLL and finds `IMonolithPackageDefinition`
3. `ModuleMetadataExtractor` reflects on attributes to build metadata
4. `ModuleRegistry.RegisterPackage()` stores modules and pages
5. `RazorViewDiscovery` scans embedded `.cshtml` resources
6. WebUI's `PackageViewRouter` fetches pages from Core API
7. `PackageViewsRegistry` registers assemblies with ASP.NET Core

### Key Files

| File | Purpose |
|------|---------|
| `Common/Interfaces/IMonolithPackageDefinition.cs` | Package interface |
| `Common/Interfaces/IMonolithModule.cs` | Module interface |
| `Common/Attributes/MenuAttributes.cs` | Page, MenuItem, Permission attributes |
| `Common/Modules/MonolithModuleBase.cs` | Base module class |
| `Common/Modules/ModuleMetadataExtractor.cs` | Attribute reflection |
| `Core/Services/PackageScanner.cs` | Package discovery |
| `Core/Services/PackageLoader.cs` | Assembly loading |
| `Core/Services/ModuleRegistry.cs` | Module registration |
| `Core/Services/RazorViewDiscovery.cs` | Embedded view scanning |

---

## Dynamic JS Loading for Complex Tabbed Pages

For pages with multiple tabs and lots of functionality, split the JS into:
- **Main page JS** - Tab navigation, common UI, dynamic loader
- **Tab module JS** - Loaded on-demand when tab is first clicked

### Directory Structure

```
wwwroot/js/pages/my-complex-page/
├── my-complex-page.js      # Main page (loaded with page)
├── tab-overview.js         # Loaded when Overview tab clicked
├── tab-configuration.js    # Loaded when Configuration tab clicked
├── tab-advanced.js         # Loaded when Advanced tab clicked
└── tab-logs.js             # Loaded when Logs tab clicked
```

### Main Page Pattern

```javascript
var MyComplexPage = {
    _loadedTabs: {},
    _activeTab: null,

    init: function() {
        this.render();
        this.attachTabHandlers();
        this.switchTab('overview'); // Load default tab
    },

    switchTab: async function(tabId) {
        // Load tab module if not already loaded
        if (!this._loadedTabs[tabId]) {
            await this._loadTabModule(tabId);
        }

        // Render tab content
        var module = this._loadedTabs[tabId];
        $('#tab-content').html(`<div id="tab-${tabId}-container"></div>`);
        module.render(`#tab-${tabId}-container`);
    },

    _loadTabModule: function(tabId) {
        return new Promise((resolve, reject) => {
            var scriptUrl = `/js/pages/my-complex-page/tab-${tabId}.js`;

            var script = document.createElement('script');
            script.src = scriptUrl + '?v=' + Date.now();
            script.onload = () => {
                // Convention: window.MyComplexPage_Tab{TabName}
                var module = window[`MyComplexPage_Tab${this._capitalize(tabId)}`];
                if (module) {
                    this._loadedTabs[tabId] = module;
                    module.init(this);
                    resolve(module);
                } else {
                    reject(new Error('Module not found'));
                }
            };
            script.onerror = () => reject(new Error('Failed to load'));
            document.head.appendChild(script);
        });
    },

    _capitalize: function(str) {
        return str.charAt(0).toUpperCase() + str.slice(1);
    }
};
```

### Tab Module Pattern

```javascript
// File: tab-overview.js
// Naming: {ParentPage}_Tab{TabName}

var MyComplexPage_TabOverview = {
    _parent: null,

    init: function(parent) {
        this._parent = parent;
    },

    render: function(container) {
        $(container).html('<div id="overview-content">Loading...</div>');
        this.loadData();
    },

    onShow: function() {
        // Called when tab becomes visible again
    },

    onHide: function() {
        // Called when switching away - pause intervals, etc.
    },

    destroy: function() {
        // Full cleanup when page unloads
    },

    loadData: async function() {
        var response = await Monolith.Core.call('my.action', {});
        // Render data...
    }
};
```

### Benefits

- **Faster initial load** - Only main JS loads with page
- **Smaller files** - Each tab is a manageable size
- **Better organization** - Related code stays together
- **Memory efficient** - Unused tabs don't consume resources

### Example Files

See `/wwwroot/js/examples/`:
- `tabbed-page-example.js` - Complete main page example
- `tab-overview-example.js` - Tab module example
