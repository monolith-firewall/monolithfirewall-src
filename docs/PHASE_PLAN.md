# MonolithFireWall - Phase Implementation Plan

**Version**: 1.0.0  
**Date**: December 28, 2025  
**Goal**: Build foundation and first monolith package with Razor Class Libraries

**Key Requirements**:
- ✅ CodeLogic 3.0 complete app cycle (Initialize → Configure → Start)
- ✅ CodeLogic logging system
- ✅ CodeLogic localization
- ✅ CL.SQLite with Repository<T> and QueryBuilder<T>
- ✅ Bootstrap 5.3.8 + jQuery (latest) SPA
- ✅ No-caching headers

---

## Phase Breakdown

### Phase 1: Project Foundation (2-3 days)
**Goal**: Set up project structure, Core service with CodeLogic, and basic WebUI with Bootstrap/jQuery SPA

### Phase 2: Package Scanner & Loader (1-2 days)
**Goal**: Implement dynamic package scanning and RCL loading

### Phase 3: Razor View Discovery (1-2 days)
**Goal**: Discover and register Razor views from packages

### Phase 4: WebUI Package Integration (1-2 days)
**Goal**: Integrate package Razor views into WebUI

### Phase 5: First Package - monolith-network (2-3 days)
**Goal**: Create working network package with DHCP module

### Phase 6: Testing & Validation (1-2 days)
**Goal**: End-to-end testing of complete system

### Phase 7: Packaging & Distribution (1-2 days)
**Goal**: Create Debian package and ISO installer

**Total Estimated Time**: 9-16 days

---

## Phase 1: Project Foundation

### 1.1 Create Project Structure

**Tasks**:
- [x] Create directory structure
- [x] Copy CodeLogic libraries to `src/Libs/`
- [ ] Initialize .NET solution
- [ ] Create Core project
- [ ] Create WebUI project
- [ ] Create Common library
- [ ] Set up CodeLogic references

**Directory Structure**:
```
/home/mlf/monolith-firewall/
├── src/
│   ├── Monolith.FireWall.Common/
│   ├── Monolith.FireWall.Core/
│   ├── Monolith.FireWall.WebUI/
│   └── Libs/
│       ├── CodeLogic3/              ✅ COPIED
│       └── CodeLogic3.Libs/          ✅ COPIED
│           └── CL.SQLite/           ✅ COPIED
├── packages/
│   └── monolith-network/  (created in Phase 5)
├── build-scripts/
├── tests/
└── docs/
```

### 1.2 Core Service - CodeLogic Integration

**Files to Create**:
- `src/Monolith.FireWall.Core/Program.cs` - **Use CodeLogic app cycle**
- `src/Monolith.FireWall.Core/Services/PackageLoader.cs` (basic)
- `src/Monolith.FireWall.Core/Services/ModuleRegistry.cs`
- `src/Monolith.FireWall.Core/Transport/NamedPipeListener.cs`
- `src/Monolith.FireWall.Core/Configuration/CoreConfiguration.cs`

**CodeLogic App Cycle**:
```csharp
// Phase 1: Initialize
var initResult = await CodeLogic.CodeLogic.InitializeAsync(opts => {
    opts.RootDirectory = "/var/lib/monolith-firewall/codelogic";
    opts.PluginsDirectory = "/var/lib/monolith-firewall/plugins";
});

// Phase 2: Configure
await CodeLogic.CodeLogic.ConfigureAsync();

// Phase 3: Start
await CodeLogic.CodeLogic.StartAsync();

// Get CL.SQLite
var sqlite = CodeLogic.Libs.Get<CL.SQLite.SQLiteLibrary>();
```

**CodeLogic Logging**:
```csharp
var logger = new Logger("CORE", logPath, LogLevel.Info, options);
var adapter = new CodeLogicLoggerAdapter(logger);
```

**Features**:
- ✅ CodeLogic initialization
- ✅ CodeLogic logging
- ✅ CL.SQLite integration
- ✅ Basic package loading (without RCL support yet)
- ✅ Module registry
- ✅ Named pipe listener
- ✅ Configuration management

### 1.3 WebUI - Bootstrap 5.3.8 + jQuery SPA

**Files to Create**:
- `src/Monolith.FireWall.WebUI/Program.cs` - **ASP.NET Core with no-caching**
- `src/Monolith.FireWall.WebUI/Services/CoreApiClient.cs`
- `src/Monolith.FireWall.WebUI/Middleware/AuthenticationMiddleware.cs`
- `src/Monolith.FireWall.WebUI/Middleware/NoCacheMiddleware.cs` - **NEW**
- `src/Monolith.FireWall.WebUI/Features/Users/` (with CL.SQLite)

**Bootstrap 5.3.8 + jQuery Setup**:
- Download Bootstrap 5.3.8 CSS/JS
- Download jQuery 3.7.1 (latest)
- Create SPA layout
- Hash-based routing
- No-caching headers

**No-Caching Middleware**:
```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
    context.Response.Headers.Append("Pragma", "no-cache");
    context.Response.Headers.Append("Expires", "0");
    await next();
});
```

**SPA Structure**:
```
wwwroot/
├── css/
│   └── bootstrap.min.css (5.3.8)
├── js/
│   ├── jquery.min.js (3.7.1)
│   ├── bootstrap.bundle.min.js (5.3.8)
│   └── app.js (SPA router)
├── index.html (SPA entry point)
└── app.html (Main SPA layout)
```

**Features**:
- ✅ ASP.NET Core setup
- ✅ Bootstrap 5.3.8
- ✅ jQuery 3.7.1
- ✅ SPA with hash routing
- ✅ No-caching headers
- ✅ Named pipe client
- ✅ Authentication middleware
- ✅ Basic user management (in WebUI with CL.SQLite)

### 1.4 Common Library

**Files to Create**:
- `src/Monolith.FireWall.Common/Interfaces/` (all interfaces)
- `src/Monolith.FireWall.Common/Models/` (all models)

### 1.5 User Management with CL.SQLite

**Files to Create**:
- `src/Monolith.FireWall.WebUI/Features/Users/Models/UserEntity.cs` - **CL.SQLite model**
- `src/Monolith.FireWall.WebUI/Features/Users/Repositories/UserRepository.cs` - **Repository<T>**
- `src/Monolith.FireWall.WebUI/Features/Users/Services/UserService.cs`
- `src/Monolith.FireWall.WebUI/Controllers/UsersController.cs`

**CL.SQLite Model**:
```csharp
using CL.SQLite.Models;

[Table("users")]
public class UserEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    
    [NotNull, Unique]
    public string Username { get; set; } = "";
    
    // ... other fields
}
```

**Repository Pattern**:
```csharp
var sqlite = CodeLogic.Libs.Get<CL.SQLite.SQLiteLibrary>();
var repository = sqlite.CreateRepository<UserEntity>();
var queryBuilder = sqlite.CreateQueryBuilder<UserEntity>();

// Use repository
await repository.CreateAsync(user);
var users = await repository.GetAllAsync();

// Use query builder
var user = await queryBuilder
    .Where(u => u.Username == username)
    .FirstOrDefaultAsync();
```

**Deliverables**:
- ✅ Core service starts with CodeLogic
- ✅ CodeLogic logging works
- ✅ CL.SQLite database operations work
- ✅ WebUI connects to Core
- ✅ Bootstrap/jQuery SPA works
- ✅ No-caching headers set
- ✅ Basic user authentication works
- ✅ Solution builds without errors

---

## Phase 2: Package Scanner & Loader

### 2.1 Package Scanner Implementation

**File**: `src/Monolith.FireWall.Core/Services/PackageScanner.cs`

**Features**:
- Scan `/opt/monolith-firewall/packages/` directory
- Discover packages with `manifest.json`
- Read package metadata
- Return list of discovered packages

**Tasks**:
- [ ] Create PackageScanner class
- [ ] Implement directory scanning
- [ ] Parse manifest.json files
- [ ] Validate package structure
- [ ] Return PackageDiscoveryInfo list

### 2.2 Enhanced Package Loader

**File**: `src/Monolith.FireWall.Core/Services/PackageLoader.cs`

**Features**:
- Load main DLL: `backend/{PackageName}.dll`
- Load Views DLL: `backend/{PackageName}.Views.dll` (if exists)
- Find IMonolithPackageDefinition
- Instantiate package and modules
- Return PackageInfo with view information

**Tasks**:
- [ ] Update PackageLoader to handle RCL DLLs
- [ ] Load Views assembly
- [ ] Detect Razor Class Library packages
- [ ] Store view assembly reference

### 2.3 Integration

**Tasks**:
- [ ] Update Core Program.cs to use PackageScanner
- [ ] Integrate scanner with loader
- [ ] Test package discovery
- [ ] Test package loading

**Deliverables**:
- ✅ Core scans for packages automatically
- ✅ Core loads RCL packages (main + Views DLL)
- ✅ Package metadata discovered correctly

---

## Phase 3: Razor View Discovery

### 3.1 Razor View Discovery Service

**File**: `src/Monolith.FireWall.Core/Services/RazorViewDiscovery.cs`

**Features**:
- Discover Razor views in Views assembly
- Extract view paths from compiled Razor
- Map views to routes
- Return view definitions

**Tasks**:
- [ ] Create RazorViewDiscovery class
- [ ] Implement view discovery from Views.dll
- [ ] Extract view paths (e.g., `/Pages/Dhcp/Config.cshtml`)
- [ ] Map to package routes
- [ ] Return PageDefinition list

### 3.2 View Path Mapping

**Features**:
- Map view paths to web routes
- Generate `/_content/{Package}/Pages/...` paths
- Update PageDefinition model

**Tasks**:
- [ ] Update PageDefinition to include Razor view path
- [ ] Implement path mapping logic
- [ ] Generate content paths

### 3.3 Integration

**Tasks**:
- [ ] Integrate discovery into PackageLoader
- [ ] Update ModuleRegistry to track views
- [ ] Test view discovery
- [ ] Verify view paths

**Deliverables**:
- ✅ Razor views discovered from Views.dll
- ✅ View paths mapped correctly
- ✅ PageDefinitions include Razor paths

---

## Phase 4: WebUI Package Integration

### 4.1 Package View Router

**File**: `src/Monolith.FireWall.WebUI/Services/PackageViewRouter.cs`

**Features**:
- Route `/p/{package}/{module}/{page}` to Razor views
- Query Core for page definitions
- Check permissions
- Render Razor views

**Tasks**:
- [ ] Create PackageViewRouter service
- [ ] Implement route mapping
- [ ] Permission checking
- [ ] Razor view rendering

### 4.2 Razor View Registration

**File**: `src/Monolith.FireWall.WebUI/Program.cs`

**Features**:
- Register package Views assemblies with ASP.NET Core
- Configure Razor view engine
- Set up `/_content/{Package}/` static file serving

**Tasks**:
- [ ] Query Core for loaded packages
- [ ] Register Views assemblies
- [ ] Configure static file serving
- [ ] Set up view engine options

### 4.3 Routing Implementation

**Tasks**:
- [ ] Add `/p/{package}/{module}/{page}` route
- [ ] Integrate PackageViewRouter
- [ ] Test view rendering
- [ ] Test static assets (`/_content/{Package}/css/...`)

**Deliverables**:
- ✅ Package Razor views render correctly
- ✅ Static assets served from packages
- ✅ Routing works: `/p/network/dhcp/config`

---

## Phase 5: First Package - monolith-network

### 5.1 Create Package Project

**Tasks**:
- [ ] Create `packages/monolith-network/` directory
- [ ] Create RCL project: `Monolith.Network.csproj`
- [ ] Configure as Razor Class Library
- [ ] Add references (Common, CodeLogic, CL.SQLite)
- [ ] Create manifest.json

### 5.2 DHCP Module - Backend

**Files to Create**:
- `packages/monolith-network/Package.cs`
- `packages/monolith-network/Modules/Dhcp/Module.cs`
- `packages/monolith-network/Modules/Dhcp/DhcpManager.cs` - **Use CL.SQLite**
- `packages/monolith-network/Modules/Dhcp/Models.cs`
- `packages/monolith-network/Modules/Dhcp/Models.Database.cs` - **CL.SQLite models**

**Features**:
- Package definition
- DHCP module definition
- Basic DHCP manager (dnsmasq integration)
- API routes: `get-config`, `update-config`, `list-leases`
- **CL.SQLite for DHCP lease storage**

**CL.SQLite in Package**:
```csharp
// In DhcpManager
var sqlite = context.GetService<CL.SQLite.SQLiteLibrary>();
_repository = sqlite.CreateRepository<DhcpLeaseEntity>();
_queryBuilder = sqlite.CreateQueryBuilder<DhcpLeaseEntity>();
```

**Tasks**:
- [ ] Implement IMonolithPackageDefinition
- [ ] Implement IMonolithModule
- [ ] Create DHCP manager with CL.SQLite
- [ ] Implement API route handlers
- [ ] Test module registration

### 5.3 DHCP Module - Razor Views

**Files to Create**:
- `packages/monolith-network/Pages/Dhcp/Config.cshtml` - **Bootstrap 5.3.8 styled**
- `packages/monolith-network/wwwroot/css/dhcp.css`
- `packages/monolith-network/wwwroot/js/dhcp.js` - **jQuery**

**Features**:
- DHCP configuration page (Razor with Bootstrap)
- Static CSS/JS assets
- Form for DHCP settings (Bootstrap forms)
- Leases display (Bootstrap tables)
- jQuery for API calls

**Tasks**:
- [ ] Create Razor page with @page directive
- [ ] Create view model
- [ ] Implement Bootstrap-styled form
- [ ] Add CSS styling
- [ ] Add jQuery for API calls

### 5.4 Build & Package

**Tasks**:
- [ ] Build package: `dotnet build`
- [ ] Verify Views.dll generated
- [ ] Copy to `/opt/monolith-firewall/packages/monolith-network/`
- [ ] Test package loading
- [ ] Test view rendering

**Deliverables**:
- ✅ monolith-network package builds as RCL
- ✅ Views compiled into Views.dll
- ✅ Package loads in Core
- ✅ DHCP config page accessible (Bootstrap styled)
- ✅ API routes work
- ✅ CL.SQLite database operations work

---

## Phase 6: Testing & Validation

### 6.1 End-to-End Testing

**Test Scenarios**:
1. Core startup with CodeLogic
2. Package scanning and loading (RCL + Views)
3. Razor view discovery
4. WebUI view registration
5. Page routing: `/p/network/dhcp/config`
6. API calls: `/api/packages/monolith-network/modules/network.dhcp/get-config`
7. Static assets: `/_content/Monolith.Network/css/dhcp.css`
8. Bootstrap/jQuery SPA functionality
9. No-caching headers
10. CL.SQLite database operations

### 6.2 Integration Testing

**Tests**:
- [ ] Package discovery works
- [ ] Package loading works
- [ ] View discovery works
- [ ] View rendering works
- [ ] API routing works
- [ ] Static assets work
- [ ] Permissions work
- [ ] CodeLogic logging works
- [ ] CL.SQLite operations work
- [ ] Bootstrap/jQuery SPA works

### 6.3 Documentation

**Tasks**:
- [ ] Update FirewallPlan.md with implementation details
- [ ] Create package development guide
- [ ] Document API endpoints
- [ ] Create troubleshooting guide

**Deliverables**:
- ✅ All tests passing
- ✅ Documentation complete
- ✅ System ready for next package

---

## Key Implementation Details

### CodeLogic App Cycle

```csharp
// 1. Initialize
var initResult = await CodeLogic.CodeLogic.InitializeAsync(opts => {
    opts.RootDirectory = "/var/lib/monolith-firewall/codelogic";
    opts.PluginsDirectory = "/var/lib/monolith-firewall/plugins";
});

// 2. Configure
await CodeLogic.CodeLogic.ConfigureAsync();

// 3. Start
await CodeLogic.CodeLogic.StartAsync();

// 4. Get services
var sqlite = CodeLogic.Libs.Get<CL.SQLite.SQLiteLibrary>();
```

### CL.SQLite Usage

```csharp
// Model
[Table("users")]
public class UserEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    
    [NotNull, Unique]
    public string Username { get; set; } = "";
}

// Repository
var repository = sqlite.CreateRepository<UserEntity>();
await repository.CreateAsync(user);

// QueryBuilder
var queryBuilder = sqlite.CreateQueryBuilder<UserEntity>();
var user = await queryBuilder
    .Where(u => u.Username == username)
    .FirstOrDefaultAsync();
```

### Bootstrap 5.3.8 + jQuery SPA

```html
<!DOCTYPE html>
<html>
<head>
    <link href="~/css/bootstrap.min.css" rel="stylesheet" />
</head>
<body>
    <div id="app"></div>
    <script src="~/js/jquery.min.js"></script>
    <script src="~/js/bootstrap.bundle.min.js"></script>
    <script src="~/js/app.js"></script>
</body>
</html>
```

### No-Caching Headers

```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
    context.Response.Headers.Append("Pragma", "no-cache");
    context.Response.Headers.Append("Expires", "0");
    await next();
});
```

---

## File Structure

```
/home/mlf/monolith-firewall/
├── src/
│   ├── Monolith.FireWall.Common/
│   ├── Monolith.FireWall.Core/
│   │   ├── Services/
│   │   │   ├── PackageScanner.cs          # NEW
│   │   │   ├── PackageLoader.cs         # Updated
│   │   │   ├── RazorViewDiscovery.cs    # NEW
│   │   │   └── ModuleRegistry.cs
│   │   └── Program.cs                   # CodeLogic app cycle
│   ├── Monolith.FireWall.WebUI/
│   │   ├── Services/
│   │   │   ├── CoreApiClient.cs
│   │   │   └── PackageViewRouter.cs      # NEW
│   │   ├── Middleware/
│   │   │   ├── AuthenticationMiddleware.cs
│   │   │   └── NoCacheMiddleware.cs      # NEW
│   │   ├── Features/
│   │   │   └── Users/
│   │   │       ├── Models/
│   │   │       │   └── UserEntity.cs    # CL.SQLite model
│   │   │       ├── Repositories/
│   │   │       │   └── UserRepository.cs # Repository<T>
│   │   │       └── Services/
│   │   │           └── UserService.cs
│   │   └── wwwroot/
│   │       ├── css/
│   │       │   └── bootstrap.min.css    # 5.3.8
│   │       └── js/
│   │           ├── jquery.min.js        # 3.7.1
│   │           ├── bootstrap.bundle.min.js # 5.3.8
│   │           └── app.js                # SPA router
│   └── Libs/
│       ├── CodeLogic3/                  ✅ COPIED
│       └── CodeLogic3.Libs/
│           └── CL.SQLite/               ✅ COPIED
├── packages/
│   └── monolith-network/
│       ├── Pages/                       # Razor pages
│       │   └── Dhcp/
│       │       └── Config.cshtml        # Bootstrap styled
│       ├── Modules/
│       │   └── Dhcp/
│       │       ├── Module.cs
│       │       ├── DhcpManager.cs       # CL.SQLite
│       │       └── Models.Database.cs   # CL.SQLite models
│       ├── wwwroot/
│       │   ├── css/
│       │   └── js/                      # jQuery
│       └── Monolith.Network.csproj
├── build-scripts/
├── tests/
└── docs/
```

---

## Success Criteria

### Phase 1 Complete
- ✅ Project structure created
- ✅ CodeLogic libraries copied
- ✅ Core service starts with CodeLogic app cycle
- ✅ CodeLogic logging works
- ✅ CL.SQLite database operations work
- ✅ WebUI connects to Core
- ✅ Bootstrap 5.3.8 + jQuery SPA works
- ✅ No-caching headers set
- ✅ User authentication works

### Phase 6 Complete
- ✅ All tests passing
- ✅ End-to-end working
- ✅ Documentation complete
- ✅ Bootstrap/jQuery SPA functional
- ✅ CL.SQLite working in packages

### Phase 7 Complete
- ✅ Debian package builds successfully
- ✅ Package installation works
- ✅ ISO builder creates bootable ISO
- ✅ Preseed configuration works
- ✅ Services start automatically

---

## Next Steps After Phase 7

1. Add more modules to monolith-network (DNS, Firewall, NAT)
2. Create additional packages
3. Implement Template Engine
4. Implement Cron Scheduler
5. Add monitoring package

---

**Ready to start Phase 1 with CodeLogic, CL.SQLite, Bootstrap, and jQuery!** 🚀
