# Theme Switching Implementation Plan

## Overview
Implement Bootstrap dark/light theme switching with user profile persistence. Users can select their preferred theme in their profile, and it will be saved and applied across all sessions.

## Current State Analysis

### Bootstrap Version
- Using Bootstrap 5.3.8 (supports `data-bs-theme` attribute)
- Bootstrap dark mode is built-in and can be toggled via `data-bs-theme="dark"` or `data-bs-theme="light"`

### User Profile Structure
- `UserEntity` in `Features/Users/Models/UserEntity.cs`
- Profile page at `/profile` (rendered via `profile.js`)
- User service handles user operations

### Current Theme
- Custom CSS in `monolith-theme.css` and `app.css`
- No theme switching currently implemented

## Implementation Plan

### Phase 1: Database Schema Update
**Goal**: Add theme preference field to user entity

1. **Update UserEntity**
   - Add `Theme` property (string: "light", "dark", or "auto")
   - Default: "dark" (current default)
   - Migration: Add column to existing user table

2. **Database Migration**
   - Use CL.SQLite migration or manual update
   - Add `Theme` column to `users` table
   - Set default value for existing users

**Files to modify:**
- `src/Monolith.FireWall.WebUI/Features/Users/Models/UserEntity.cs`

### Phase 2: Backend API
**Goal**: Add API endpoints to get/set user theme preference

1. **UserService Updates**
   - Add `GetUserThemeAsync(int userId)` method
   - Add `UpdateUserThemeAsync(int userId, string theme)` method
   - Validate theme value ("light", "dark", "auto")

2. **API Endpoints**
   - `GET /api/users/profile/theme` - Get current user's theme
   - `PUT /api/users/profile/theme` - Update current user's theme
   - Include in existing profile update endpoint if available

**Files to modify:**
- `src/Monolith.FireWall.WebUI/Features/Users/Services/UserService.cs`
- `src/Monolith.FireWall.WebUI/Features/Users/Controllers/UsersController.cs`

### Phase 3: Frontend Theme System
**Goal**: Implement theme switching mechanism

1. **Theme Manager JavaScript**
   - Create `js/core/theme-manager.js`
   - Functions:
     - `getTheme()` - Get current theme from localStorage or API
     - `setTheme(theme)` - Apply theme to document
     - `saveTheme(theme)` - Save to API and localStorage
     - `initTheme()` - Initialize theme on page load
   - Listen for system preference changes (if "auto")

2. **Bootstrap Theme Application**
   - Apply `data-bs-theme` attribute to `<html>` or `<body>` element
   - Support values: "light", "dark", "auto" (auto uses system preference)

3. **Theme Persistence**
   - Store in localStorage as fallback
   - Sync with user profile via API
   - Apply on page load before content renders

**Files to create:**
- `src/Monolith.FireWall.WebUI/wwwroot/js/core/theme-manager.js`

**Files to modify:**
- `src/Monolith.FireWall.WebUI/Pages/App.cshtml` - Add theme initialization script

### Phase 4: Profile Page Integration
**Goal**: Add theme selector to profile page

1. **Profile Page UI**
   - Add theme selection section
   - Radio buttons or dropdown: Light / Dark / Auto (System)
   - Show current selection
   - Save button or auto-save on change

2. **Profile JavaScript**
   - Load current theme from API
   - Display in UI
   - Handle theme change
   - Save to API on change
   - Apply theme immediately

**Files to modify:**
- `src/Monolith.FireWall.WebUI/wwwroot/js/pages/profile.js`
- `src/Monolith.FireWall.WebUI/Pages/Profile.cshtml` (if needed)

### Phase 5: Theme Styling Adjustments
**Goal**: Ensure custom CSS works with both themes

1. **CSS Variables**
   - Use CSS custom properties for colors
   - Define light and dark variants
   - Update custom components to use variables

2. **Component Updates**
   - Update `package-header.css` to work with both themes
   - Update `monolith-theme.css` for theme support
   - Ensure page header works in both themes

**Files to modify:**
- `src/Monolith.FireWall.WebUI/wwwroot/css/package-header.css`
- `src/Monolith.FireWall.WebUI/wwwroot/css/monolith-theme.css`
- `src/Monolith.FireWall.WebUI/wwwroot/css/app.css`

### Phase 6: Top Navbar Quick Toggle (Optional)
**Goal**: Add quick theme toggle in top navbar

1. **Navbar Theme Toggle**
   - Add theme toggle button in user dropdown menu
   - Icon: sun/moon icon
   - Quick switch without going to profile
   - Persists to profile

**Files to modify:**
- `src/Monolith.FireWall.WebUI/Pages/App.cshtml`
- `src/Monolith.FireWall.WebUI/wwwroot/js/core/monolith.ui.js` (or create theme toggle handler)

## Technical Details

### Theme Values
- `"light"` - Always use light theme
- `"dark"` - Always use dark theme  
- `"auto"` - Follow system preference (prefers-color-scheme media query)

### Bootstrap Theme Application
```html
<html data-bs-theme="dark">
<!-- or -->
<html data-bs-theme="light">
```

### System Preference Detection
```javascript
const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
```

### API Request Format
```json
// GET /api/users/profile/theme
{
  "success": true,
  "data": {
    "theme": "dark"
  }
}

// PUT /api/users/profile/theme
{
  "theme": "dark"
}
```

## Database Schema

### UserEntity Update
```csharp
public class UserEntity
{
    // ... existing fields ...
    public string Theme { get; set; } = "dark"; // "light", "dark", or "auto"
}
```

### Migration SQL
```sql
ALTER TABLE users ADD COLUMN Theme TEXT DEFAULT 'dark';
```

## Implementation Order

1. ✅ **Phase 1**: Database schema (UserEntity + migration)
2. ✅ **Phase 2**: Backend API (UserService + Controller)
3. ✅ **Phase 3**: Frontend theme system (theme-manager.js)
4. ✅ **Phase 4**: Profile page integration
5. ✅ **Phase 5**: CSS adjustments for both themes
6. ⏭️ **Phase 6**: Optional navbar quick toggle

## Testing Checklist

- [ ] Theme persists after page refresh
- [ ] Theme syncs across browser tabs
- [ ] Theme saved to user profile
- [ ] Theme loads from profile on login
- [ ] "Auto" theme follows system preference
- [ ] Theme changes apply immediately
- [ ] All pages work in both themes
- [ ] Custom components styled correctly
- [ ] Page header works in both themes
- [ ] Profile page theme selector works

## Future Enhancements

- Per-page theme override (if needed)
- Theme preview before saving
- More theme options (if Bootstrap adds more)
- Theme transition animations
