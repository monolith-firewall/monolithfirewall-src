# Razor Pages Usage Guide

Now that all CSHTML pages are properly processed by the Razor engine, you can use the full power of Razor syntax!

---

## Basic Page Structure

### Minimal Page Template

```razor
@page "/your/route/here"
@{
    Layout = null;  // Required for SPA partials
}

<div class="container-fluid p-4">
    <h2>Your Page Title</h2>
    <p>Your content here</p>
</div>

@section Scripts {
    <link rel="stylesheet" href="/css/your-styles.css" data-module-css="your-module" />
    <script src="/js/your-script.js" data-module-js="your-module"></script>
}
```

### Page with Code-Behind

**YourPage.cshtml**:
```razor
@page "/your/route"
@model YourNamespace.YourPageModel
@{
    Layout = null;
}

<div class="container-fluid p-4">
    <h2>@Model.Title</h2>
    
    <ul>
        @foreach (var item in Model.Items)
        {
            <li>@item.Name - @item.Value</li>
        }
    </ul>
</div>

@section Scripts {
    <script src="/js/your-page.js" data-module-js="your-page"></script>
}
```

**YourPage.cshtml.cs**:
```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace YourNamespace;

public class YourPageModel : PageModel
{
    private readonly YourService _service;
    
    public YourPageModel(YourService service)
    {
        _service = service;  // Dependency injection works!
    }
    
    public string Title { get; set; } = "My Page";
    public List<Item> Items { get; set; } = new();
    
    public async Task OnGetAsync()
    {
        // This code executes on page load!
        Items = await _service.GetItemsAsync();
    }
}
```

---

## Razor Syntax Examples

### Variables and Expressions

```razor
@{
    var message = "Hello, World!";
    var count = 42;
    var isActive = true;
}

<p>@message</p>
<p>Count: @count</p>
<p>Active: @(isActive ? "Yes" : "No")</p>
```

### Conditionals

```razor
@if (Model.IsAdmin)
{
    <button class="btn btn-danger">Delete All</button>
}
else
{
    <p class="text-muted">Admin access required</p>
}

@if (Model.Items.Count > 0)
{
    <p>Found @Model.Items.Count items</p>
}
else if (Model.IsLoading)
{
    <p>Loading...</p>
}
else
{
    <p>No items found</p>
}
```

### Loops

```razor
@* Simple foreach *@
<ul>
    @foreach (var item in Model.Items)
    {
        <li>@item.Name</li>
    }
</ul>

@* For loop *@
<table>
    @for (int i = 0; i < Model.Items.Count; i++)
    {
        <tr>
            <td>@(i + 1)</td>
            <td>@Model.Items[i].Name</td>
        </tr>
    }
</table>

@* While loop *@
@{
    int counter = 0;
}
@while (counter < 5)
{
    <p>Item @counter</p>
    counter++;
}
```

### Switch Statements

```razor
@switch (Model.Status)
{
    case "active":
        <span class="badge bg-success">Active</span>
        break;
    case "pending":
        <span class="badge bg-warning">Pending</span>
        break;
    case "disabled":
        <span class="badge bg-danger">Disabled</span>
        break;
    default:
        <span class="badge bg-secondary">Unknown</span>
        break;
}
```

### String Formatting

```razor
@{
    var price = 19.99m;
    var date = DateTime.Now;
}

<p>Price: @price.ToString("C")</p>
<p>Date: @date.ToString("yyyy-MM-dd")</p>
<p>Time: @date.ToString("HH:mm:ss")</p>
```

---

## Working with Models

### Accessing Model Properties

```razor
@model Monolith.FireWall.WebUI.Pages.Firewall.Aliases.ConfigModel

<h2>@Model.PageTitle</h2>
<p>Total Aliases: @Model.Aliases.Count</p>

@foreach (var alias in Model.Aliases)
{
    <div class="card mb-2">
        <div class="card-body">
            <h5>@alias.Name</h5>
            <p>@alias.Description</p>
            <span class="badge bg-@(alias.IsActive ? "success" : "secondary")">
                @(alias.IsActive ? "Active" : "Inactive")
            </span>
        </div>
    </div>
}
```

### Null Checking

```razor
@* Null-conditional operator *@
<p>@Model.User?.Name</p>
<p>@Model.User?.Email</p>

@* Null-coalescing operator *@
<p>@(Model.Title ?? "Untitled")</p>

@* Traditional null check *@
@if (Model.User != null)
{
    <p>Welcome, @Model.User.Name!</p>
}
```

---

## Advanced Features

### Partial Views

```razor
@* Include a partial view *@
<partial name="_AlertPartial" model="Model.AlertMessage" />

@* Or use HTML helper *@
@await Html.PartialAsync("_AlertPartial", Model.AlertMessage)
```

### Sections

```razor
@* Define a section *@
@section Scripts {
    <script>
        console.log('Page-specific script');
    </script>
}

@section Styles {
    <style>
        .custom-class { color: red; }
    </style>
}

@* Check if section exists (in layout) *@
@if (IsSectionDefined("Scripts"))
{
    @RenderSection("Scripts", required: false)
}
```

### Using Directives

```razor
@using System.Linq
@using Monolith.FireWall.Common.Models
@using Monolith.FireWall.WebUI.Services

@{
    var sortedItems = Model.Items.OrderBy(x => x.Name).ToList();
}
```

### Injecting Services

```razor
@inject UserService UserService
@inject ILogger<YourPage> Logger

@{
    var currentUser = await UserService.GetCurrentUserAsync();
    Logger.LogInformation("Page loaded by {User}", currentUser.Username);
}

<p>Logged in as: @currentUser.Username</p>
```

### HTML Encoding

```razor
@* Automatically encoded (safe) *@
<p>@Model.UserInput</p>

@* Raw HTML (use with caution!) *@
<div>@Html.Raw(Model.HtmlContent)</div>

@* Explicitly encode *@
<p>@Html.Encode(Model.PotentiallyDangerousInput)</p>
```

---

## Firewall Page Example

Here's a complete example showing how to use Razor in a firewall page:

**Pages/Firewall/Aliases/Config.cshtml**:
```razor
@page "/firewall/aliases"
@model Monolith.FireWall.WebUI.Pages.Firewall.Aliases.ConfigModel
@{
    Layout = null;
}

<div class="container-fluid p-4">
    <div class="row mb-4">
        <div class="col-12">
            <h2>Firewall Aliases</h2>
            <p class="text-muted">
                Manage @Model.Aliases.Count alias@(Model.Aliases.Count != 1 ? "es" : "")
            </p>
        </div>
    </div>

    @if (Model.HasPendingChanges)
    {
        <div class="alert alert-warning">
            <strong>⚠ Pending Changes</strong>
            <p>You have @Model.PendingChangesCount unsaved change(s).</p>
        </div>
    }

    <div class="card">
        <div class="card-header d-flex justify-content-between">
            <h5>Aliases</h5>
            <button class="btn btn-primary" id="btnAddAlias">Add Alias</button>
        </div>
        <div class="card-body">
            @if (Model.Aliases.Any())
            {
                <table class="table table-hover">
                    <thead>
                        <tr>
                            <th>Name</th>
                            <th>Type</th>
                            <th>Content</th>
                            <th>Status</th>
                            <th>Actions</th>
                        </tr>
                    </thead>
                    <tbody>
                        @foreach (var alias in Model.Aliases)
                        {
                            <tr>
                                <td>
                                    <strong>@alias.Name</strong>
                                    @if (!string.IsNullOrEmpty(alias.Description))
                                    {
                                        <br />
                                        <small class="text-muted">@alias.Description</small>
                                    }
                                </td>
                                <td>
                                    <span class="badge bg-info">@alias.Type</span>
                                </td>
                                <td>
                                    @switch (alias.Type)
                                    {
                                        case "host":
                                            <code>@alias.Content</code>
                                            break;
                                        case "network":
                                            <code>@alias.Content</code>
                                            break;
                                        case "port":
                                            <code>@alias.Content</code>
                                            break;
                                        default:
                                            <span>@alias.Content</span>
                                            break;
                                    }
                                </td>
                                <td>
                                    @if (alias.IsEnabled)
                                    {
                                        <span class="badge bg-success">Enabled</span>
                                    }
                                    else
                                    {
                                        <span class="badge bg-secondary">Disabled</span>
                                    }
                                </td>
                                <td>
                                    <button class="btn btn-sm btn-primary" 
                                            onclick="editAlias(@alias.Id)">
                                        Edit
                                    </button>
                                    <button class="btn btn-sm btn-danger" 
                                            onclick="deleteAlias(@alias.Id)">
                                        Delete
                                    </button>
                                </td>
                            </tr>
                        }
                    </tbody>
                </table>
            }
            else
            {
                <div class="text-center py-5">
                    <p class="text-muted">No aliases configured</p>
                    <button class="btn btn-primary" id="btnAddFirstAlias">
                        Add Your First Alias
                    </button>
                </div>
            }
        </div>
    </div>
</div>

@section Scripts {
    <link rel="stylesheet" href="/css/firewall.css" data-module-css="firewall-aliases" />
    <script src="/js/aliases.js" data-module-js="aliases"></script>
}
```

**Pages/Firewall/Aliases/Config.cshtml.cs**:
```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;
using Monolith.FireWall.WebUI.Features.Firewall.Aliases;

namespace Monolith.FireWall.WebUI.Pages.Firewall.Aliases;

public class ConfigModel : PageModel
{
    private readonly AliasesManager _aliasesManager;
    
    public ConfigModel(AliasesManager aliasesManager)
    {
        _aliasesManager = aliasesManager;
    }
    
    public List<FirewallAlias> Aliases { get; set; } = new();
    public bool HasPendingChanges { get; set; }
    public int PendingChangesCount { get; set; }
    
    public async Task OnGetAsync()
    {
        // Load aliases from database
        Aliases = await _aliasesManager.GetAllAsync();
        
        // Check for pending changes
        HasPendingChanges = await _aliasesManager.HasPendingChangesAsync();
        PendingChangesCount = await _aliasesManager.GetPendingChangesCountAsync();
    }
}
```

---

## Package Page Example

**monolith-network/Pages/Dhcp/Config.cshtml**:
```razor
@page "/p/monolith-network/dhcp/config"
@model Monolith.Network.Pages.Dhcp.ConfigModel
@{
    Layout = null;
}

<div class="container-fluid p-4">
    <h2>DHCP Server Configuration</h2>
    
    @if (Model.IsServiceRunning)
    {
        <div class="alert alert-success">
            <strong>✓ Service Running</strong>
            <p>DHCP server is active and serving @Model.ActiveLeaseCount lease(s)</p>
        </div>
    }
    else
    {
        <div class="alert alert-warning">
            <strong>⚠ Service Stopped</strong>
            <p>DHCP server is not running</p>
        </div>
    }
    
    <div class="card">
        <div class="card-header">
            <h5>Interface Configuration</h5>
        </div>
        <div class="card-body">
            @foreach (var iface in Model.Interfaces)
            {
                <div class="mb-3">
                    <h6>@iface.Name (@iface.Description)</h6>
                    
                    @if (iface.DhcpEnabled)
                    {
                        <p>
                            <strong>Range:</strong> @iface.DhcpRangeStart - @iface.DhcpRangeEnd<br />
                            <strong>Subnet:</strong> @iface.Subnet<br />
                            <strong>Gateway:</strong> @iface.Gateway
                        </p>
                        
                        @if (iface.DnsServers.Any())
                        {
                            <p>
                                <strong>DNS Servers:</strong>
                                @string.Join(", ", iface.DnsServers)
                            </p>
                        }
                    }
                    else
                    {
                        <p class="text-muted">DHCP disabled on this interface</p>
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
        _dhcpManager = dhcpManager;
    }
    
    public bool IsServiceRunning { get; set; }
    public int ActiveLeaseCount { get; set; }
    public List<DhcpInterface> Interfaces { get; set; } = new();
    
    public async Task OnGetAsync()
    {
        IsServiceRunning = await _dhcpManager.IsServiceRunningAsync();
        ActiveLeaseCount = await _dhcpManager.GetActiveLeasesCountAsync();
        Interfaces = await _dhcpManager.GetInterfacesAsync();
    }
}
```

---

## Best Practices

### 1. Always Set Layout = null
```razor
@{
    Layout = null;  // Required for SPA partials!
}
```

### 2. Use @section Scripts for Assets
```razor
@section Scripts {
    <link rel="stylesheet" href="/css/your-page.css" data-module-css="your-module" />
    <script src="/js/your-page.js" data-module-js="your-module"></script>
}
```

### 3. Keep Logic in Code-Behind
```razor
@* ❌ Bad - Too much logic in view *@
@{
    var items = await _service.GetItemsAsync();
    var filtered = items.Where(x => x.IsActive).OrderBy(x => x.Name).ToList();
}

@* ✅ Good - Logic in code-behind *@
@model YourPageModel
@foreach (var item in Model.FilteredItems)
{
    <p>@item.Name</p>
}
```

### 4. Use Null-Conditional Operators
```razor
@* ✅ Safe *@
<p>@Model.User?.Name</p>
<p>@(Model.Title ?? "Untitled")</p>
```

### 5. Encode User Input
```razor
@* ✅ Automatically encoded *@
<p>@Model.UserInput</p>

@* ❌ Dangerous - only if you trust the source *@
<div>@Html.Raw(Model.TrustedHtmlContent)</div>
```

---

## Debugging Tips

### 1. Add Breakpoints in Code-Behind
Set breakpoints in your `.cshtml.cs` file's `OnGetAsync()` method to debug server-side logic.

### 2. View Rendered HTML
Check browser DevTools → Network tab → `/partial/your/route` to see the rendered HTML.

### 3. Check Razor Compilation Errors
If a page doesn't load, check the console for Razor compilation errors.

### 4. Use @* Comments *@
```razor
@* This is a Razor comment - won't appear in HTML *@
<!-- This is an HTML comment - will appear in HTML -->
```

---

## Common Patterns

### Loading State
```razor
@if (Model.IsLoading)
{
    <div class="text-center py-5">
        <div class="spinner-border" role="status">
            <span class="visually-hidden">Loading...</span>
        </div>
    </div>
}
else if (Model.Items.Any())
{
    @* Show items *@
}
else
{
    <p class="text-muted">No items found</p>
}
```

### Error Handling
```razor
@if (!string.IsNullOrEmpty(Model.ErrorMessage))
{
    <div class="alert alert-danger">
        <strong>Error:</strong> @Model.ErrorMessage
    </div>
}
```

### Pagination
```razor
<nav>
    <ul class="pagination">
        @for (int i = 1; i <= Model.TotalPages; i++)
        {
            <li class="page-item @(i == Model.CurrentPage ? "active" : "")">
                <a class="page-link" href="#" onclick="loadPage(@i)">@i</a>
            </li>
        }
    </ul>
</nav>
```

---

## Resources

- [ASP.NET Core Razor Pages Documentation](https://learn.microsoft.com/en-us/aspnet/core/razor-pages/)
- [Razor Syntax Reference](https://learn.microsoft.com/en-us/aspnet/core/mvc/views/razor)
- [Tag Helpers](https://learn.microsoft.com/en-us/aspnet/core/mvc/views/tag-helpers/intro)

---

**Happy Razor Coding!** 🎉
