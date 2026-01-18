# Setup Wizard Refactoring Plan

## Overview
Separate the setup wizard into a standalone, guided Razor page experience that runs on first-time installation, completely independent from the main firewall application.

## Goals
1. **Isolation**: Setup wizard runs in its own context, separate from main app
2. **First-Run Detection**: Automatically launch on fresh installation
3. **Skip Option**: Allow users to skip setup and configure later
4. **Guided Experience**: Step-by-step wizard with clear navigation
5. **Persistence**: Remember setup completion state

## Architecture Changes

### 1. Setup Detection & Routing

#### Middleware: `SetupRedirectMiddleware`
- **Location**: `src/Monolith.FireWall.WebUI/Middleware/SetupRedirectMiddleware.cs`
- **Purpose**: Intercept all requests and redirect to setup if needed
- **Logic**:
  - Check if setup is needed via Core API
  - If needed AND not already on setup page → redirect to `/setup`
  - Skip for: `/setup/*`, `/login`, `/api/*`, static files
  - Allow bypass with query param `?skip-setup=true` (admin only)

#### Implementation:
```csharp
public class SetupRedirectMiddleware
{
    private readonly RequestDelegate _next;
    private readonly CoreApiClient _coreClient;
    
    public async Task InvokeAsync(HttpContext context)
    {
        // Skip for setup pages, login, API, static files
        if (ShouldSkipRedirect(context.Request.Path))
        {
            await _next(context);
            return;
        }
        
        // Check if setup is needed
        var needsSetup = await CheckSetupNeededAsync();
        if (needsSetup)
        {
            context.Response.Redirect("/setup");
            return;
        }
        
        await _next(context);
    }
}
```

### 2. Separate Setup Layout

#### New Layout: `_SetupLayout.cshtml`
- **Location**: `src/Monolith.FireWall.WebUI/Pages/Shared/_SetupLayout.cshtml`
- **Features**:
  - Minimal header (just logo/branding)
  - No main navigation menu
  - No user menu/notifications
  - Clean, focused design
  - Progress indicator at top
  - Step navigation (Back/Next/Skip/Finish)

#### Design:
```html
<!DOCTYPE html>
<html>
<head>
    <!-- Minimal head - only setup wizard CSS -->
</head>
<body class="setup-wizard">
    <div class="setup-header">
        <div class="container">
            <h1>Monolith FireWall Setup</h1>
            <div class="setup-progress">
                <!-- Progress bar -->
            </div>
        </div>
    </div>
    
    <main class="setup-content">
        @RenderBody()
    </main>
    
    <footer class="setup-footer">
        <!-- Skip setup link -->
    </footer>
</body>
</html>
```

### 3. Setup Pages Structure

#### Main Setup Page: `Setup/Index.cshtml`
- **Route**: `/setup`
- **Purpose**: Landing page / step router
- **Features**:
  - Welcome message
  - Overview of setup steps
  - "Start Setup" button
  - "Skip Setup" button (with confirmation)
  - Shows completion status if already done

#### Step Pages:
- `Setup/Router.cshtml` - Router & System configuration
- `Setup/Network.cshtml` - Network interface setup
- `Setup/Package/{packageId}/{pageId}.cshtml` - Package-specific setup (dynamic)

#### Completion Page: `Setup/Complete.cshtml`
- **Route**: `/setup/complete`
- **Purpose**: Final step showing completion
- **Features**:
  - Summary of configured items
  - "Go to Dashboard" button
  - Option to restart setup

### 4. Setup State Management

#### Backend: `SetupManager` (already exists)
- **Enhancements**:
  - Add `SkipSetup()` method
  - Add `IsSetupComplete()` method
  - Add `CanSkipStep(stepId)` method
  - Track skipped steps separately

#### Frontend: `setup-wizard.js`
- **Location**: `wwwroot/js/setup-wizard.js`
- **Purpose**: Standalone setup wizard controller
- **Features**:
  - Step navigation
  - Data persistence
  - Validation
  - Progress tracking
  - Skip functionality

### 5. First-Run Detection

#### Detection Methods:
1. **File-based**: Check for `/var/lib/monolith-firewall/.setup-complete`
2. **Database**: Check `SetupStatusResponse.IsFirstRun`
3. **Core API**: Query `setup.status` → `NeedsSetup = true`

#### Implementation:
- Core service checks on startup
- WebUI middleware checks on each request
- Setup wizard checks before rendering

### 6. Skip Setup Functionality

#### Skip Button Locations:
1. **Setup Index Page**: "Skip Setup" button (prominent)
2. **Each Step Page**: "Skip This Step" button (if step is optional)
3. **Footer**: "Skip Setup" link (always visible)

#### Skip Behavior:
- **Skip Individual Step**: Mark step as skipped, continue to next
- **Skip Entire Setup**: 
  - Mark all optional steps as skipped
  - Mark setup as complete
  - Redirect to dashboard
  - Show toast: "Setup skipped. You can configure settings later."

#### Skip Confirmation:
```javascript
Monolith.UI.confirm(
    'Skip setup? You can configure these settings later in the dashboard.',
    () => {
        // Call API to skip setup
        Monolith.API.post('/api/setup/skip', {})
            .then(() => {
                window.location.href = '/';
            });
    }
);
```

### 7. API Endpoints

#### New Endpoints:
- `POST /api/setup/skip` - Skip entire setup
- `POST /api/setup/skip-step` - Skip individual step
- `GET /api/setup/can-skip/{stepId}` - Check if step can be skipped

#### Existing Endpoints (enhance):
- `GET /api/setup/status` - Already exists, add `CanSkip` info
- `POST /api/setup/complete-step` - Already exists
- `POST /api/setup/finish` - Already exists

### 8. CSS Styling

#### New Stylesheet: `setup-wizard.css`
- **Location**: `wwwroot/css/setup-wizard.css`
- **Features**:
  - Clean, minimal design
  - Step indicator/progress bar
  - Card-based step content
  - Responsive layout
  - Focused on setup flow

### 9. File Structure

```
src/Monolith.FireWall.WebUI/
├── Middleware/
│   └── SetupRedirectMiddleware.cs          [NEW]
├── Pages/
│   ├── Setup/
│   │   ├── Index.cshtml                    [MODIFY]
│   │   ├── Index.cshtml.cs                 [MODIFY]
│   │   ├── Router.cshtml                   [MODIFY - use _SetupLayout]
│   │   ├── Network.cshtml                  [MODIFY - use _SetupLayout]
│   │   ├── Complete.cshtml                 [NEW]
│   │   ├── Complete.cshtml.cs              [NEW]
│   │   └── PackageStep.cshtml              [MODIFY - use _SetupLayout]
│   └── Shared/
│       └── _SetupLayout.cshtml              [NEW]
├── wwwroot/
│   ├── css/
│   │   └── setup-wizard.css                 [NEW]
│   └── js/
│       └── setup-wizard.js                  [NEW - standalone]
└── Program.cs                               [MODIFY - register middleware]
```

## Implementation Phases

### Phase 1: Core Infrastructure
1. Create `SetupRedirectMiddleware`
2. Register middleware in `Program.cs`
3. Create `_SetupLayout.cshtml`
4. Update existing setup pages to use new layout
5. Test first-run detection

### Phase 2: Setup Wizard UI
1. Create `setup-wizard.css`
2. Create `setup-wizard.js` (standalone)
3. Update `Setup/Index.cshtml` with welcome/skip
4. Create `Setup/Complete.cshtml`
5. Add progress indicator to all setup pages

### Phase 3: Skip Functionality
1. Add `POST /api/setup/skip` endpoint
2. Add `POST /api/setup/skip-step` endpoint
3. Implement skip buttons in UI
4. Add skip confirmation dialogs
5. Test skip flow

### Phase 4: Integration & Polish
1. Remove setup checks from `app.js`
2. Update routing logic
3. Test complete flow (first-run → setup → dashboard)
4. Test skip flow
5. Test returning to setup after completion

## Technical Details

### Middleware Registration Order
```csharp
// In Program.cs
app.UseMiddleware<SetupRedirectMiddleware>();  // Early - before routing
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
```

### Setup Status Check
```csharp
private async Task<bool> CheckSetupNeededAsync()
{
    try
    {
        var request = JsonSerializer.Serialize(new { action = "setup.status" });
        var responseJson = await _coreClient.SendRequestAsync(request);
        var response = JsonSerializer.Deserialize<JsonElement>(responseJson);
        
        if (response.TryGetProperty("Success", out var success) && success.GetBoolean())
        {
            if (response.TryGetProperty("Data", out var data))
            {
                return data.TryGetProperty("NeedsSetup", out var needsSetup) && 
                       needsSetup.GetBoolean();
            }
        }
    }
    catch
    {
        // On error, assume setup is needed (safer)
        return true;
    }
    
    return false;
}
```

### Skip Setup API
```csharp
app.MapPost("/api/setup/skip", async (HttpContext context, CoreApiClient coreClient) =>
{
    try
    {
        var coreRequest = new { action = "setup.skip" };
        var requestJson = JsonSerializer.Serialize(coreRequest);
        var responseJson = await coreClient.SendRequestAsync(requestJson);
        return Results.Content(responseJson, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { Success = false, Error = ex.Message }, statusCode: 500);
    }
});
```

## Testing Checklist

- [ ] Fresh install redirects to `/setup`
- [ ] Setup pages use `_SetupLayout` (no main nav)
- [ ] Skip setup button works and redirects to dashboard
- [ ] Skip individual step works
- [ ] After completion, accessing `/setup` shows completion page
- [ ] Middleware doesn't interfere with API calls
- [ ] Middleware doesn't interfere with login
- [ ] Static files load correctly
- [ ] Setup can be restarted after completion
- [ ] Progress indicator shows correct step
- [ ] All setup steps are accessible
- [ ] Package setup steps work correctly

## Migration Notes

- Existing setup pages will be updated to use `_SetupLayout`
- `setup.js` will be replaced with `setup-wizard.js` (standalone)
- Setup checks removed from `app.js`
- No breaking changes to Core API
- Database schema unchanged (uses existing setup progress)

## Future Enhancements

- Setup wizard theme customization
- Multi-language support for setup
- Setup wizard analytics/tracking
- Export/import setup configuration
- Setup wizard tutorials/help tooltips
