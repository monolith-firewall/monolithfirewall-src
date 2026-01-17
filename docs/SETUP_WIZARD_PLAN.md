# Setup Wizard Integration Plan

## Overview
Create a comprehensive, modular setup wizard that integrates with monolith packages to provide a guided initial configuration experience. The wizard should handle basic system setup, network configuration, and package-specific setup steps.

## Current State

### Existing Infrastructure
- ✅ SetupManager service exists in Core
- ✅ SetupHandler API endpoints exist
- ✅ Setup wizard pages exist (Router, Network)
- ✅ Module interface supports `GetSetupWizardPages()`
- ✅ Setup progress tracking (completed steps)
- ✅ Setup status API

### What's Missing
- ❌ Basic setup step (hostname, timezone, date/time) not fully integrated
- ❌ Package setup pages not being discovered/rendered properly
- ❌ Setup wizard UI not fully functional
- ❌ Package modules not providing setup pages yet
- ❌ Network setup not saving to actual configuration
- ❌ Router setup not saving to system settings properly

## Architecture

### Setup Flow
```
1. Basic System Setup (Core)
   ├── Hostname
   ├── Domain
   ├── Timezone
   ├── Date/Time
   └── NTP Servers

2. Network Setup (Core)
   ├── WAN Interface Assignment
   ├── LAN Interface Assignment
   ├── LAN IP Configuration (DHCP/Static)
   ├── Gateway
   └── DNS Servers

3. Package Setup Steps (Dynamic from packages)
   ├── monolith-network
   │   ├── DHCP Configuration (if DHCP module exists)
   │   └── DNS Configuration (if DNS module exists)
   ├── monolith-vpn (if installed)
   │   └── VPN Initial Setup
   └── Other packages...
```

## Implementation Plan

### Phase 1: Core Setup Steps (Basic & Network)

#### 1.1 Fix Router Setup Integration
**Files to modify:**
- `src/Monolith.FireWall.WebUI/Pages/Setup/Router.cshtml.cs`
- `src/Monolith.FireWall.WebUI/Program.cs` (add system settings API endpoints)
- `src/Monolith.FireWall.Core/Services/SystemSettingsManager.cs`

**Tasks:**
- [ ] Add API endpoint `/api/system/settings` (GET/POST) to update system settings
- [ ] Implement hostname update via `hostnamectl` or `/etc/hostname`
- [ ] Implement timezone update via `timedatectl set-timezone`
- [ ] Implement date/time update via `timedatectl set-time`
- [ ] Implement NTP server configuration (update `/etc/systemd/timesyncd.conf` or chrony)
- [ ] Save settings to SystemSettingsEntity in database
- [ ] Test router setup step saves correctly

#### 1.2 Fix Network Setup Integration
**Files to modify:**
- `src/Monolith.FireWall.WebUI/Pages/Setup/Network.cshtml`
- `src/Monolith.FireWall.Core/Services/InterfaceAssignmentManager.cs`
- `src/Monolith.FireWall.Core/Transport/Handlers/InterfacesHandler.cs`

**Tasks:**
- [ ] Add API endpoint to save interface assignments from setup
- [ ] Implement WAN/LAN interface role assignment
- [ ] Implement LAN IP configuration (static IP assignment)
- [ ] Implement gateway configuration (save to routing)
- [ ] Implement DNS server configuration (save to system settings)
- [ ] Test network setup step saves correctly

#### 1.3 Fix Setup Wizard Navigation
**Files to modify:**
- `src/Monolith.FireWall.WebUI/wwwroot/js/pages/setup.js`
- `src/Monolith.FireWall.WebUI/Pages/Setup/Index.cshtml`

**Tasks:**
- [ ] Fix step navigation to properly track current step
- [ ] Fix progress bar calculation
- [ ] Fix step completion tracking
- [ ] Add step validation before proceeding
- [ ] Add step data persistence
- [ ] Test wizard navigation flow

### Phase 2: Package Integration

#### 2.1 Create Base Setup Page Template
**Files to create:**
- `src/Monolith.FireWall.WebUI/Pages/Setup/PackageStep.cshtml`
- `src/Monolith.FireWall.WebUI/Pages/Setup/PackageStep.cshtml.cs`

**Purpose:**
- Generic page that can render any package setup step
- Loads package setup page content dynamically
- Handles navigation and validation

#### 2.2 Update SetupManager
**Files to modify:**
- `src/Monolith.FireWall.Core/Services/SetupManager.cs`

**Tasks:**
- [ ] Ensure package setup pages are discovered correctly
- [ ] Sort steps by Order property
- [ ] Track completion status per package step
- [ ] Handle package step completion callbacks

#### 2.3 Package Setup Page Discovery
**Files to modify:**
- `src/Monolith.FireWall.WebUI/wwwroot/js/pages/setup.js`

**Tasks:**
- [ ] Load package setup pages from API
- [ ] Add package steps to wizard flow
- [ ] Handle package step routing
- [ ] Render package setup pages dynamically

### Phase 3: Package Module Implementation

#### 3.1 monolith-network Package Setup Pages
**For DHCP Module:**
- Create setup page: "DHCP Server Configuration"
- Route: `/setup/package/monolith-network/dhcp`
- Order: 10 (after network setup)
- Fields:
  - Enable DHCP server
  - DHCP pool range
  - Default gateway for clients
  - DNS servers for clients
  - Lease time

**For DNS Module:**
- Create setup page: "DNS Server Configuration"
- Route: `/setup/package/monolith-network/dns`
- Order: 11
- Fields:
  - Enable DNS server
  - Forward DNS servers
  - Local domain resolution

#### 3.2 Example Module Implementation
```csharp
public class NetworkDhcpModule : IMonolithModule
{
    public IEnumerable<ISetupWizardPage> GetSetupWizardPages()
    {
        return new[]
        {
            new SetupWizardPage
            {
                Id = "dhcp",
                Title = "DHCP Server",
                Description = "Configure DHCP server for your local network",
                Route = "/setup/package/monolith-network/dhcp",
                Order = 10,
                IsRequired = false,
                IsComplete = false, // Check if DHCP is already configured
                PackageId = "monolith-network",
                ModuleId = "dhcp"
            }
        };
    }
}
```

### Phase 4: UI/UX Improvements

#### 4.1 Setup Wizard UI
**Files to modify:**
- `src/Monolith.FireWall.WebUI/Pages/Setup/Index.cshtml`
- `src/Monolith.FireWall.WebUI/wwwroot/css/setup.css` (create if needed)

**Tasks:**
- [ ] Improve progress indicator (show step names)
- [ ] Add step list sidebar showing completed/pending steps
- [ ] Add step icons
- [ ] Improve navigation buttons styling
- [ ] Add step validation feedback
- [ ] Add loading states
- [ ] Add error handling UI

#### 4.2 Step Pages UI
**Files to modify:**
- All setup step pages (Router, Network, PackageStep)

**Tasks:**
- [ ] Consistent styling across all steps
- [ ] Add step number indicator
- [ ] Add help text/descriptions
- [ ] Add form validation feedback
- [ ] Add save indicators

## Technical Details

### Setup Step Order
1. **Router & System** (Order: 0, Required: true)
   - Hostname, domain, timezone, date/time, NTP

2. **Network** (Order: 1, Required: false)
   - Interface assignments, IP configuration, gateway, DNS

3. **Package Steps** (Order: 10+, Required: varies)
   - Dynamically loaded from packages
   - Sorted by Order property

### Data Flow

```
User fills form → Validate → Save to Core → Mark step complete → Next step
```

**API Endpoints:**
- `GET /api/setup/status` - Get setup status and progress
- `GET /api/setup/packages` - Get package setup pages
- `POST /api/setup/complete-step` - Mark step as complete
- `POST /api/setup/finish` - Finish setup wizard
- `GET /api/system/settings` - Get system settings
- `POST /api/system/settings` - Update system settings
- `POST /api/interfaces/assign` - Assign interface roles/IPs

### Package Setup Page Requirements

**For a module to provide setup pages:**
1. Implement `IMonolithModule.GetSetupWizardPages()`
2. Return `ISetupWizardPage` instances with:
   - Unique ID
   - Title and description
   - Route path (e.g., `/setup/package/{packageId}/{pageId}`)
   - Order (for sorting)
   - Required flag
   - Completion status

3. Create Razor page at the route path
4. Implement validation function: `window.validateStep()`
5. Implement data getter function: `window.getStepData()`

## Testing Checklist

- [ ] Setup wizard appears on first run
- [ ] Router setup saves hostname, timezone, NTP correctly
- [ ] Network setup saves interface assignments correctly
- [ ] Package setup pages are discovered and shown
- [ ] Step navigation works (back/next/skip)
- [ ] Progress tracking works correctly
- [ ] Step validation prevents invalid data
- [ ] Setup completion flag is created
- [ ] Setup wizard doesn't appear after completion
- [ ] Can skip optional steps
- [ ] Package steps can be completed independently

## Future Enhancements

1. **Setup Wizard Resumption**
   - Allow users to resume incomplete setup
   - Show which steps are pending

2. **Setup Wizard Reset**
   - Admin option to reset setup and run again
   - Useful for reconfiguration

3. **Setup Templates**
   - Pre-configured setup templates (Home, Office, Datacenter)
   - Quick setup for common scenarios

4. **Setup Validation**
   - Validate network connectivity after network setup
   - Test DNS resolution after DNS setup
   - Verify DHCP is working after DHCP setup

5. **Setup Export/Import**
   - Export setup configuration
   - Import for similar deployments

## File Structure

```
src/Monolith.FireWall.WebUI/
├── Pages/Setup/
│   ├── Index.cshtml (Main wizard)
│   ├── Router.cshtml (Basic system setup)
│   ├── Network.cshtml (Network configuration)
│   └── PackageStep.cshtml (Generic package step renderer)
├── wwwroot/
│   ├── js/pages/setup.js (Wizard controller)
│   └── css/setup.css (Wizard styles)

src/Monolith.FireWall.Core/
├── Services/
│   ├── SetupManager.cs (Setup state management)
│   └── SystemSettingsManager.cs (System settings)
└── Transport/Handlers/
    └── SetupHandler.cs (Setup API)

src/Monolith.FireWall.Common/
├── Interfaces/
│   ├── IMonolithModule.cs (GetSetupWizardPages method)
│   └── ISetupWizardPage.cs (Setup page interface)
└── Models/
    ├── SetupModels.cs (Setup data models)
    └── SetupWizardPage.cs (Implementation)
```

## Dependencies

- SystemSettingsManager for hostname, timezone, NTP
- InterfaceAssignmentManager for network configuration
- ModuleRegistry for discovering package setup pages
- PlatformCommandRunner for executing system commands

## Notes

- Setup wizard should be accessible without authentication
- Setup completion should be checked on app startup
- Package setup pages are optional and can be skipped
- Setup data should be validated before saving
- System commands should be executed with proper error handling
