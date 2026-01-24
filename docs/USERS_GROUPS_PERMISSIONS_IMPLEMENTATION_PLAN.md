# Users, Groups, and Permissions Implementation Plan

## Current State Analysis

### Backend (✅ Complete)
- **Controllers exist:**
  - `UsersController.cs` - `/api/users` (GET, POST, PUT, DELETE)
  - `UserGroupsController.cs` - `/api/usergroups` (GET, POST, PUT, DELETE, user management)
- **Services exist:**
  - `UserService` - User CRUD operations
  - `UserGroupService` - Group CRUD, permissions, user-group relationships
- **All API endpoints are implemented and working**

### Frontend (❌ Issues Found)

#### 1. **API Path Mismatches**
- `users.js` calls `/users` but controller is at `/api/users`
- `groups.js` calls `/usergroups` but controller is at `/api/usergroups`
- `permissions.js` calls `/api/permissions` but **no endpoint exists**

#### 2. **Old Code in app.js**
- `loadUsersTable()`, `renderUsersTable()` - unused, should be removed
- `loadGroupsTable()`, `renderGroupsTable()` - unused, should be removed
- `showAddUserModal()`, `showAddGroupModal()` - unused, should be removed
- `editUser()`, `deleteUser()`, `editGroup()`, `deleteGroup()` - unused, should be removed
- These functions are TODOs that were never implemented

#### 3. **Missing Features**

**Groups Page:**
- `loadPermissionCategories()` - **NOT IMPLEMENTED** (line 11)
- `loadAllPermissions()` - **NOT IMPLEMENTED** (line 51-62, has TODO)
- `renderPermissionsList()` - Uses hardcoded categories (line 221-224)
- Groups table shows "Users" column but always shows "-" (line 107)
- Need to load actual users count for each group

**Permissions Page:**
- Calls `/api/permissions` but **endpoint doesn't exist**
- Falls back to hardcoded core permissions
- Should load permissions dynamically from Core API or packages

## Implementation Plan

### Phase 1: Fix API Paths

**Files to Update:**
- `wwwroot/js/pages/users.js` - Change `/users` → `/api/users`
- `wwwroot/js/pages/groups.js` - Change `/usergroups` → `/api/usergroups`

### Phase 2: Create Permissions API Endpoint

**New Endpoint:** `/api/permissions`

**Implementation:**
- Query Core API for all packages/modules
- Extract permissions from module definitions
- Return structured permissions list with categories
- Include core system permissions

**Location:** `Program.cs` or new `PermissionsController.cs`

### Phase 3: Implement Missing Groups Features

**Groups Page (`groups.js`):**
1. **Implement `loadPermissionCategories()`:**
   - Call `/api/permissions` to get all permissions
   - Group by category/subcategory
   - Cache for use in modal

2. **Implement `loadAllPermissions()`:**
   - Call `/api/permissions` endpoint
   - Store in `this.allPermissions`
   - Use in `renderPermissionsList()`

3. **Fix Users Count in Table:**
   - Call `/api/usergroups/{id}/users` for each group
   - Display actual user count instead of "-"

4. **Fix `renderPermissionsList()`:**
   - Use `this.allPermissions` instead of hardcoded categories
   - Group by category/subcategory dynamically
   - Support nested permissions (e.g., `system.*`, `system.users.*`)

### Phase 4: Fix Permissions Page

**Permissions Page (`permissions.js`):**
1. **Fix API call:**
   - Ensure `/api/permissions` endpoint exists
   - Handle response correctly

2. **Improve rendering:**
   - Better categorization
   - Show package/module source
   - Add descriptions if available

### Phase 5: Clean Up app.js

**Remove unused functions:**
- `loadUsersTable()` (lines 240-250)
- `renderUsersTable()` (lines 252-295)
- `loadGroupsTable()` (lines 297-307)
- `renderGroupsTable()` (lines 309-353)
- `showAddUserModal()` (lines 354-359)
- `showAddGroupModal()` (lines 359-364)
- `editUser()` (lines 364-369)
- `deleteUser()` (lines 369-375)
- `editGroup()` (lines 376-381)
- `deleteGroup()` (lines 381-386)

**Remove event handlers:**
- `#btn-add-user` click handler (line 51-53)
- `#btn-add-group` click handler (line 56-58)

### Phase 6: Verify Integration

**Test:**
- Users page: Create, edit, delete users
- Users page: Assign users to groups
- Groups page: Create, edit, delete groups
- Groups page: Assign permissions to groups
- Groups page: View users in each group
- Permissions page: View all available permissions

## File Changes Summary

### New Files
- None (use existing controllers)

### Modified Files
1. **`wwwroot/js/pages/users.js`**
   - Fix API paths: `/users` → `/api/users`
   - Verify all endpoints work correctly

2. **`wwwroot/js/pages/groups.js`**
   - Fix API paths: `/usergroups` → `/api/usergroups`
   - Implement `loadPermissionCategories()`
   - Implement `loadAllPermissions()`
   - Fix users count in table
   - Fix `renderPermissionsList()` to use dynamic permissions

3. **`wwwroot/js/pages/permissions.js`**
   - Verify `/api/permissions` endpoint works
   - Improve error handling

4. **`wwwroot/js/app.js`**
   - Remove all unused user/group functions (lines 240-386)
   - Remove unused event handlers

5. **`Program.cs`** (if needed)
   - Add `/api/permissions` endpoint if not using Core API directly

## API Endpoints Reference

### Users API (`/api/users`)
- `GET /api/users` - List all users
- `GET /api/users/{id}` - Get user by ID (includes groups)
- `POST /api/users` - Create user
- `PUT /api/users/{id}` - Update user
- `DELETE /api/users/{id}` - Delete user

### User Groups API (`/api/usergroups`)
- `GET /api/usergroups` - List all groups
- `GET /api/usergroups/{id}` - Get group by ID
- `POST /api/usergroups` - Create group
- `PUT /api/usergroups/{id}` - Update group
- `DELETE /api/usergroups/{id}` - Delete group
- `GET /api/usergroups/{id}/users` - Get users in group
- `POST /api/usergroups/{id}/users/{userId}` - Add user to group
- `DELETE /api/usergroups/{id}/users/{userId}` - Remove user from group
- `GET /api/usergroups/user/{userId}/permissions` - Get user's effective permissions

### Permissions API (NEW - `/api/permissions`)
- `GET /api/permissions` - Get all available permissions
  - Query Core API: `/core?action=get-modules` or `/core?action=get-packages`
  - Extract `requiredPermissions` from modules
  - But Core API only returns permission IDs, not full PermissionDefinition objects
  - Need to either:
    a) Query modules directly and call `GetRequiredPermissions()` to get full objects
    b) Create endpoint that queries Core and enriches with PermissionDefinition data
    c) Use Core API and build permission list from module metadata

## Implementation Order

1. Fix API paths in `users.js` and `groups.js`
2. Create `/api/permissions` endpoint
3. Implement missing Groups features
4. Clean up `app.js`
5. Test all functionality
6. Fix any remaining issues
