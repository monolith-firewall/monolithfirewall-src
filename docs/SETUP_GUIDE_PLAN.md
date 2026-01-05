# Setup Guide/Wizard - Implementation Plan

## Overview
Create a comprehensive setup wizard in the WebUI that guides users through initial configuration after installation. The wizard should be modular, allowing packages to contribute their own setup steps.

## Goals
1. Detect if system needs setup (first run)
2. Guide users through essential configuration
3. Allow packages to contribute setup steps
4. Make it skippable but accessible later

---

## Architecture

### 1. Setup Detection

**Location**: Core service + WebUI

**Mechanism**:
- Check for setup completion flag: `/var/lib/monolith-firewall/.setup-complete`
- Check if essential packages are installed (network package)
- Check if basic network configuration exists
- Check if hostname/timezone are configured

**API Endpoint**: `GET /api/setup/status`
```json
{
  "needsSetup": true,
  "completedSteps": ["router", "network"],
  "pendingSteps": ["packages"],
  "isFirstRun": true
}
```

---

### 2. Setup Guide Structure

#### Phase 1: Router/System Basics
- **Hostname** - Set system hostname (✓ verified - uses SystemSettingsManager, applies via hostnamectl)
- **Timezone** - Configure timezone (✓ added - gets list from `timedatectl list-timezones` or `/usr/share/zoneinfo`, applies via timedatectl)
- **Date/Time** - Set current date and time (✓ added - applies via timedatectl set-time)
- **NTP Servers** - Configure time synchronization (✓ added - updates `/etc/systemd/timesyncd.conf`)
- **Admin Password** - Set/change admin password (if not set) - TODO: Add user management API

#### Phase 2: Basic Network Settings
- **WAN Interface** - Select/configure WAN interface
- **LAN Interface** - Select/configure LAN interface
- **IP Configuration** - Static IP or DHCP for LAN
- **Gateway** - Set default gateway
- **DNS Servers** - Configure DNS

#### Phase 3: Package-Specific Setup
- **Scan installed packages** for setup wizard pages
- **Display package-specific setup** for each installed package
- **Examples**:
  - Network Package: DHCP server setup
  - VPN Package: VPN server configuration
  - Diagnostics Package: Monitoring setup

---

## Implementation Details

### 1. Core Service Changes

#### A. Setup Status API Handler
**File**: `src/Monolith.FireWall.Core/Transport/Handlers/SetupHandler.cs`

**Methods**:
- `GET /api/setup/status` - Get setup status
- `POST /api/setup/complete-step` - Mark step as complete
- `GET /api/setup/packages` - Get packages with setup wizards
- `POST /api/setup/finish` - Mark entire setup as complete

#### B. Setup State Management
**File**: `src/Monolith.FireWall.Core/Services/SetupManager.cs`

**Responsibilities**:
- Track setup completion state
- Detect what needs to be configured
- Validate setup prerequisites
- Store setup progress

**Storage**: 
- Flag file: `/var/lib/monolith-firewall/.setup-complete`
- Progress file: `/var/lib/monolith-firewall/.setup-progress.json`

---

### 2. Common Interface Extensions

#### A. Setup Wizard Page Interface
**File**: `src/Monolith.FireWall.Common/Interfaces/ISetupWizardPage.cs`

```csharp
public interface ISetupWizardPage
{
    string Id { get; }
    string Title { get; }
    string Description { get; }
    int Order { get; }
    string Route { get; } // WebUI route for this setup page
    bool IsRequired { get; }
    bool IsComplete { get; }
    Task<bool> ValidateAsync();
}
```

#### B. Module Setup Extension
**File**: `src/Monolith.FireWall.Common/Interfaces/IMonolithModule.cs`

**Add method**:
```csharp
IEnumerable<ISetupWizardPage> GetSetupWizardPages();
```

---

### 3. WebUI Implementation

#### A. Setup Guide Component
**File**: `src/Monolith.FireWall.WebUI/wwwroot/js/pages/setup.js`

**Features**:
- Multi-step wizard UI
- Progress indicator
- Step validation
- Navigation (next/back/skip)
- Save progress

#### B. Setup Guide Pages

**1. Router Setup Page**
- File: `src/Monolith.FireWall.WebUI/Pages/Setup/Router.cshtml`
- Fields: Hostname, Timezone, NTP servers

**2. Network Setup Page**
- File: `src/Monolith.FireWall.WebUI/Pages/Setup/Network.cshtml`
- Fields: WAN interface, LAN interface, IP config, Gateway, DNS

**3. Package Setup Pages**
- Dynamically loaded based on installed packages
- Each package can provide its own setup page
- Route: `/setup/package/{packageId}`

#### C. Setup API Client
**File**: `src/Monolith.FireWall.WebUI/Services/SetupApiClient.cs`

**Methods**:
- `GetSetupStatusAsync()`
- `CompleteStepAsync(string stepId)`
- `GetPackageSetupPagesAsync()`
- `FinishSetupAsync()`

---

### 4. Package Integration

#### A. Network Package Example
**File**: `tmp/monolithfirewall-packages/monolith-network/Modules/Dhcp/Module.cs`

**Implementation**:
```csharp
public class DhcpModule : IMonolithModule
{
    public IEnumerable<ISetupWizardPage> GetSetupWizardPages()
    {
        return new[]
        {
            new SetupWizardPage
            {
                Id = "dhcp-server",
                Title = "DHCP Server Configuration",
                Description = "Configure DHCP server for your network",
                Order = 10,
                Route = "/setup/package/monolith-network/dhcp",
                IsRequired = false,
                IsComplete = CheckDhcpConfigured()
            }
        };
    }
}
```

#### B. Setup Page Registration
- Packages register setup pages via `GetSetupWizardPages()`
- Core service collects all setup pages from installed packages
- WebUI displays them in the setup wizard

---

## Data Flow

```
1. User opens WebUI
   ↓
2. WebUI checks /api/setup/status
   ↓
3. If needsSetup = true, redirect to /setup
   ↓
4. Setup wizard displays:
   - Router setup (hostname, timezone)
   - Network setup (interfaces, IP, DNS)
   - Package setup pages (from installed packages)
   ↓
5. User completes each step
   ↓
6. WebUI calls /api/setup/complete-step
   ↓
7. After all steps, call /api/setup/finish
   ↓
8. Setup complete, redirect to dashboard
```

---

## File Structure

```
src/
├── Monolith.FireWall.Common/
│   └── Interfaces/
│       ├── ISetupWizardPage.cs (NEW)
│       └── IMonolithModule.cs (EXTEND)
│   └── Models/
│       └── SetupModels.cs (NEW)
│
├── Monolith.FireWall.Core/
│   ├── Services/
│   │   └── SetupManager.cs (NEW)
│   └── Transport/Handlers/
│       └── SetupHandler.cs (NEW)
│
└── Monolith.FireWall.WebUI/
    ├── Pages/
    │   └── Setup/
    │       ├── Index.cshtml (NEW - Wizard container)
    │       ├── Router.cshtml (NEW)
    │       ├── Network.cshtml (NEW)
    │       └── Package.cshtml (NEW - Dynamic package setup)
    ├── Services/
    │   └── SetupApiClient.cs (NEW)
    └── wwwroot/js/pages/
        └── setup.js (NEW)
```

---

## Implementation Phases

### Phase 1: Core Infrastructure
1. Create `ISetupWizardPage` interface
2. Extend `IMonolithModule` with `GetSetupWizardPages()`
3. Create `SetupManager` service
4. Create `SetupHandler` API handler
5. Add setup status detection logic

### Phase 2: Basic Setup Pages
1. Create Router setup page (hostname, timezone)
2. Create Network setup page (interfaces, IP, DNS)
3. Create setup wizard UI component
4. Implement step navigation and validation

### Phase 3: Package Integration
1. Update Network package to provide DHCP setup page
2. Create package setup page loader
3. Display package setup pages in wizard
4. Handle package-specific setup completion

### Phase 4: Polish & Testing
1. Add setup completion detection
2. Add "Skip Setup" option (with warning)
3. Add "Re-run Setup" option in settings
4. Test with fresh install
5. Test with existing installations

---

## API Endpoints

### GET /api/setup/status
Returns setup status and progress.

**Response**:
```json
{
  "needsSetup": true,
  "isFirstRun": true,
  "completedSteps": ["router", "network"],
  "pendingSteps": ["packages"],
  "totalSteps": 5,
  "progress": 40
}
```

### GET /api/system/settings/timezones
Returns list of available timezones from the system.

**Response**:
```json
{
  "timezones": [
    "Africa/Abidjan",
    "Africa/Accra",
    "America/New_York",
    "Europe/London",
    ...
  ]
}
```

**Implementation**: Uses `timedatectl list-timezones` or reads from `/usr/share/zoneinfo/` directory recursively.

### POST /api/setup/complete-step
Mark a setup step as complete.

**Request**:
```json
{
  "stepId": "router",
  "data": { ... }
}
```

### GET /api/setup/packages
Get all packages with setup wizard pages.

**Response**:
```json
{
  "packages": [
    {
      "packageId": "monolith-network",
      "packageName": "Network",
      "setupPages": [
        {
          "id": "dhcp-server",
          "title": "DHCP Server Configuration",
          "route": "/setup/package/monolith-network/dhcp",
          "order": 10,
          "isRequired": false,
          "isComplete": false
        }
      ]
    }
  ]
}
```

### POST /api/setup/finish
Mark entire setup as complete.

**Request**:
```json
{
  "skipRemaining": false
}
```

---

## UI/UX Considerations

1. **Progress Indicator**: Show progress bar at top
2. **Step Navigation**: Next/Back buttons, skip option for optional steps
3. **Validation**: Real-time validation, show errors clearly
4. **Auto-save**: Save progress as user goes through steps
5. **Responsive**: Work on mobile/tablet
6. **Accessibility**: Keyboard navigation, screen reader support

---

## Edge Cases

1. **No Network Package**: Show warning, allow basic setup
2. **Partial Setup**: Allow resuming from where user left off
3. **Package Installation During Setup**: Refresh setup pages list
4. **Network Changes**: Warn if network config changes during setup
5. **Multiple Packages**: Order setup pages by priority/order

---

## Future Enhancements

1. **Setup Templates**: Pre-configured setups (Router, Firewall, VPN server)
2. **Import/Export**: Export setup config, import on new install
3. **Guided Tours**: Interactive tutorials for each step
4. **Validation Rules**: Advanced validation for network configs
5. **Rollback**: Undo setup changes if something goes wrong

---

## Testing Checklist

- [ ] Fresh install shows setup wizard
- [ ] Existing install doesn't show setup wizard
- [ ] Router setup saves hostname/timezone
- [ ] Network setup configures interfaces correctly
- [ ] Package setup pages appear for installed packages
- [ ] Setup can be skipped (with warning)
- [ ] Setup can be re-run from settings
- [ ] Progress is saved and can be resumed
- [ ] Validation works for all fields
- [ ] Works with no network package installed
