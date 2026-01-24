# User Groups and Permissions Verification & Logging Fix Plan

## Overview
This plan covers:
1. Verification that User Groups and Permissions work correctly
2. Confirmation that permissions integrate properly with monolith packages
3. Fixing authentication and permission logging in System Logs

## Current State Analysis

### User Groups & Permissions System
- **UserGroupService**: Manages groups, permissions, and user-group associations
- **UserGroupRepository**: Database operations for groups
- **PermissionSyncService**: Background service that syncs package permissions to Admin group
- **PackageViewRouter**: Checks permissions before allowing page access
- **AuthenticationMiddleware**: Handles session management but doesn't log auth events

### Logging System
- **LoggingManager**: Centralized logging service with three log types:
  - Monolith: Auth, Changes, Package, Module, User, Permission
  - System: Service, Configuration, Network, Storage, Update
  - Security: Firewall, Intrusion, Access, Threat, Audit
- **SystemLogsManager**: Wrapper for querying logs
- **System Logs UI**: Has tabs for Monolith, System, and Security logs with category filters

### Issues Identified
1. **No authentication logging**: Login/logout events are not logged
2. **No permission check logging**: Permission denials are not logged
3. **Missing IP address capture**: Auth events should capture IP addresses
4. **Permission verification needed**: Need to verify packages properly integrate with permission system

## Phase 1: Fix Authentication & Permission Logging

### 1.1 Add Authentication Logging
**File**: `src/Monolith.FireWall.WebUI/Middleware/AuthenticationMiddleware.cs`
- Add logging for successful logins
- Add logging for failed login attempts
- Add logging for logout events
- Capture IP address from HttpContext
- Use LoggingManager.LogMonolithAsync with category "Auth"

**Implementation**:
- Inject LoggingManager (or use static instance)
- Log successful authentication after session is set
- Log failed authentication attempts
- Log logout events

### 1.2 Add Permission Check Logging
**File**: `src/Monolith.FireWall.WebUI/Services/PackageViewRouter.cs`
- Log when permission check fails
- Include user ID, requested permission, and page/route
- Use LoggingManager.LogMonolithAsync with category "Permission"

**Additional locations**:
- Check other permission check points in the codebase
- Log permission denials in API endpoints that check permissions

### 1.3 Update Login Endpoint
**File**: `src/Monolith.FireWall.WebUI/Program.cs` (login endpoint ~line 695)
- Add logging for successful logins
- Add logging for failed login attempts
- Capture IP address from HttpContext.Connection.RemoteIpAddress

### 1.4 Update Logout Endpoint
**File**: `src/Monolith.FireWall.WebUI/Program.cs` (logout endpoint ~line 742)
- Add logging for logout events
- Include user ID and IP address

## Phase 2: Verify User Groups Functionality

### 2.1 Test User Group CRUD Operations
**Test Cases**:
1. Create a new user group
2. Update group name, description, permissions
3. Delete a user group
4. Verify group appears in UI
5. Verify permissions are correctly stored and retrieved

**Verification Points**:
- UserGroupService.CreateGroupAsync
- UserGroupService.UpdateGroupAsync
- UserGroupService.DeleteGroupAsync
- UserGroupRepository operations

### 2.2 Test Permission Assignment
**Test Cases**:
1. Assign permissions to a group
2. Verify permissions are correctly stored (check wildcard support)
3. Test "All Permissions" (*) wildcard
4. Test specific permissions (e.g., "network.dhcp.read")
5. Test package permissions (e.g., "monolith-network.*")

**Verification Points**:
- UserGroupEntity.SetPermissions()
- UserGroupEntity.GetPermissions()
- Permission storage format

### 2.3 Test User-Group Association
**Test Cases**:
1. Add user to group
2. Remove user from group
3. Verify user inherits group permissions
4. Test multiple groups per user
5. Test disabled groups don't grant permissions

**Verification Points**:
- UserGroupService.AddUserToGroupAsync
- UserGroupService.RemoveUserFromGroupAsync
- UserGroupService.GetUserEffectivePermissionsAsync
- UserGroupService.GetUserGroupsAsync

## Phase 3: Verify Package Integration

### 3.1 Verify Package Permission Definitions
**Test Packages**:
- monolith-network (DHCP, DNS modules)
- monolith-vpn (IPsec, OpenVPN, WireGuard modules)
- monolith-diagnostics (Diagnostics module)

**Verification Points**:
1. Check each module's `GetRequiredPermissions()` method
2. Verify permissions are in format: `{package}.{module}.{action}`
3. Verify permissions are defined in manifest.json (if applicable)
4. Check PermissionDefinition objects are correctly structured

**Expected Permissions**:
- monolith-network: `network.dhcp.read`, `network.dhcp.write`, `network.dns.read`, `network.dns.write`
- monolith-vpn: (check actual definitions)
- monolith-diagnostics: (check actual definitions)

### 3.2 Verify Permission Discovery
**Test Cases**:
1. Verify PackageDiscoveryService.GetAllPermissionsAsync() returns all package permissions
2. Verify PermissionsController returns permissions correctly
3. Verify permissions appear in UI (Permissions page)
4. Verify permissions appear in User Groups editor

**Verification Points**:
- PackageDiscoveryService.GetAllPermissionsAsync()
- PermissionsController.GetAllPermissions()
- Frontend: `Monolith.Packages.getAllPermissions()`
- Frontend: `pages/permissions.js`
- Frontend: `pages/groups.js`

### 3.3 Verify Permission Sync Service
**Test Cases**:
1. Verify PermissionSyncService discovers new package permissions
2. Verify Admin group automatically gets new permissions
3. Test with a new package installation
4. Verify sync happens on startup and periodically

**Verification Points**:
- PermissionSyncService.SyncPermissionsAsync()
- UserGroupService.AddPermissionsToAdminGroupAsync()
- Background service execution

### 3.4 Verify Permission Enforcement
**Test Cases**:
1. Create a user with limited permissions
2. Attempt to access package pages that require permissions
3. Verify access is denied when permission is missing
4. Verify access is granted when permission exists
5. Test wildcard permissions (*, package.*, package.module.*)

**Verification Points**:
- PackageViewRouter.HasPermission()
- Page access control
- API route permission checks (if implemented)

## Phase 4: System Logs UI Verification

### 4.1 Verify Auth Logs Display
**Test Cases**:
1. Perform login
2. Check System Logs > Monolith Logs > Auth category
3. Verify login event appears with correct details
4. Perform logout
5. Verify logout event appears
6. Test failed login attempt
7. Verify failed attempt is logged

**Verification Points**:
- System Logs UI loads Auth category logs
- Log entries show: Timestamp, Category (Auth), Level, Source, Message, User, IP
- Filters work correctly

### 4.2 Verify Permission Logs Display
**Test Cases**:
1. Attempt to access a page without permission
2. Check System Logs > Monolith Logs > Permission category
3. Verify permission denial is logged
4. Verify log includes: user, requested permission, page/route

**Verification Points**:
- System Logs UI loads Permission category logs
- Log entries show permission check failures
- Details include permission ID and context

### 4.3 Test Log Filtering
**Test Cases**:
1. Filter by Auth category
2. Filter by Permission category
3. Filter by date range
4. Filter by level (Info, Warning, Error)
5. Test pagination

**Verification Points**:
- Category filter works
- Date filters work
- Level filters work
- Pagination works correctly

## Implementation Checklist

### Authentication Logging
- [ ] Add LoggingManager to AuthenticationMiddleware
- [ ] Log successful login in login endpoint
- [ ] Log failed login attempts in login endpoint
- [ ] Log logout events in logout endpoint
- [ ] Capture IP address from HttpContext
- [ ] Test login logging
- [ ] Test logout logging
- [ ] Test failed login logging

### Permission Logging
- [ ] Add logging to PackageViewRouter.HasPermission() for failures
- [ ] Add logging to API endpoints that check permissions
- [ ] Include user ID, permission, and context in logs
- [ ] Test permission denial logging
- [ ] Verify logs appear in System Logs UI

### User Groups Verification
- [ ] Test group creation
- [ ] Test group update
- [ ] Test group deletion
- [ ] Test permission assignment
- [ ] Test user-group association
- [ ] Test effective permissions calculation
- [ ] Test disabled groups

### Package Integration Verification
- [ ] Verify all packages define permissions correctly
- [ ] Verify permission discovery works
- [ ] Verify permissions appear in UI
- [ ] Verify PermissionSyncService works
- [ ] Test permission enforcement
- [ ] Test wildcard permissions

### System Logs UI
- [ ] Verify Auth logs display correctly
- [ ] Verify Permission logs display correctly
- [ ] Test filtering by category
- [ ] Test date range filtering
- [ ] Test pagination
- [ ] Verify log details are complete

## Testing Strategy

### Manual Testing
1. Start the application
2. Create test users with different permission levels
3. Create test user groups
4. Assign permissions to groups
5. Test login/logout and verify logs
6. Test permission checks and verify logs
7. Install packages and verify permission sync
8. Test package page access with different permission levels

### Automated Testing (Future)
- Unit tests for UserGroupService
- Unit tests for permission checking
- Integration tests for permission sync
- Integration tests for logging

## Success Criteria

1. ✅ All authentication events (login, logout, failed attempts) are logged
2. ✅ All permission check failures are logged
3. ✅ Logs appear in System Logs > Monolith Logs with correct categories
4. ✅ User groups can be created, updated, and deleted
5. ✅ Permissions can be assigned to groups
6. ✅ Users can be assigned to groups
7. ✅ Users inherit permissions from groups
8. ✅ Package permissions are discovered and available
9. ✅ Admin group automatically gets new package permissions
10. ✅ Permission checks work correctly for package pages/routes
11. ✅ System Logs UI displays Auth and Permission logs correctly
12. ✅ Log filtering works for Auth and Permission categories

## Files to Modify

1. `src/Monolith.FireWall.WebUI/Middleware/AuthenticationMiddleware.cs` - Add auth logging
2. `src/Monolith.FireWall.WebUI/Services/PackageViewRouter.cs` - Add permission logging
3. `src/Monolith.FireWall.WebUI/Program.cs` - Add logging to login/logout endpoints
4. (Potentially) Other files that check permissions

## Notes

- LoggingManager is a singleton, so we can use `LoggingManager.Instance` directly
- Auth logs should use category "Auth" in Monolith log type
- Permission logs should use category "Permission" in Monolith log type
- IP addresses should be captured from `HttpContext.Connection.RemoteIpAddress`
- User IDs should be captured from UserContext when available
