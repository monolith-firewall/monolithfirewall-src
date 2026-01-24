# Notification Management Page - Implementation Plan

## Overview
Create a dedicated notification management page where users can view, mark as read, and delete notifications. Also fix issues with the notification dropdown menu.

## Issues to Fix

### 1. Notification Dropdown Menu Issues
- **Problem**: Notification title is on the same line and behind the message
- **Solution**: Restructure HTML/CSS to put title on a new line above the message
- **Location**: `src/Monolith.FireWall.WebUI/wwwroot/js/app.js` (notification rendering)

### 2. "Mark All Read" Not Removing from Menu
- **Problem**: After marking all as read, notifications still appear in dropdown
- **Solution**: Refresh notification list after marking all as read, or filter out read notifications
- **Location**: `src/Monolith.FireWall.WebUI/wwwroot/js/app.js` (mark all read handler)

### 3. Add Link to Notification Page
- **Problem**: No way to access full notification management page
- **Solution**: Add "View All Notifications" link in dropdown menu footer
- **Location**: `src/Monolith.FireWall.WebUI/wwwroot/js/app.js` (notification menu rendering)

## New Features to Implement

### 1. Notification Management Page
- **Route**: `/notifications`
- **Page File**: `src/Monolith.FireWall.WebUI/Pages/Notifications.cshtml`
- **JavaScript**: `src/Monolith.FireWall.WebUI/wwwroot/js/pages/notifications.js`
- **CSS**: Add styles to `src/Monolith.FireWall.WebUI/wwwroot/css/app.css` or create `notifications.css`

### 2. API Endpoints Needed
**Existing endpoints** (via Core Unix socket):
- `monitoring.notifications.list` - List notifications (with limit and unreadOnly filter)
- `monitoring.notifications.read` - Mark notifications as read (supports `all: true` or `ids: [1,2,3]`)

**Endpoints to add**:
- `monitoring.notifications.delete` - Delete notification(s) (supports `id: 1` or `ids: [1,2,3]` or `all: true`)
- `monitoring.notifications.delete-read` - Delete all read notifications (optional convenience endpoint)

**Note**: These will need to be added to:
- `MonitoringHandler.cs` - Add action to handler
- `MonitoringManager.cs` - Add delete methods
- Notification store (check if delete methods exist in store)

### 3. Page Features
- **List View**: Table or card view showing all notifications
- **Filtering**: Filter by read/unread, type (error/warning/info), date range
- **Actions**:
  - Mark as read/unread (individual)
  - Delete (individual)
  - Mark all as read
  - Delete all (with confirmation)
  - Delete all read notifications
- **Pagination**: If many notifications, add pagination
- **Sorting**: Sort by date (newest/oldest), type, read status

## Implementation Steps

### Step 1: Fix Notification Dropdown Menu
1. **Fix HTML Structure** (`app.js`):
   - Change notification item structure to have title on separate line
   - Update CSS to ensure proper layout
   - Example structure:
     ```html
     <div class="notification-title">Title</div>
     <div class="notification-message">Message</div>
     <div class="notification-date">Date</div>
     ```

2. **Update CSS** (`app.css`):
   - Ensure `.notification-item` uses flexbox column layout
   - Add proper spacing between title, message, and date
   - Fix any z-index or positioning issues

3. **Fix "Mark All Read"**:
   - After successful API call, reload notifications
   - Filter out read notifications from dropdown (or show them differently)
   - Update badge count

4. **Add "View All" Link**:
   - Add link in dropdown footer: "View All Notifications" → `/notifications`
   - Style consistently with "Mark all read" button

### Step 2: Create Notification Management Page
1. **Create Razor Page**:
   - `src/Monolith.FireWall.WebUI/Pages/Notifications.cshtml`
   - Basic structure with container, header, and content area
   - Include action buttons (Mark all read, Delete all, etc.)

2. **Create JavaScript Module**:
   - `src/Monolith.FireWall.WebUI/wwwroot/js/pages/notifications.js`
   - Functions:
     - `loadNotifications()` - Fetch and render notifications
     - `renderNotificationsTable()` - Render table/cards
     - `markAsRead(id)` - Mark single notification as read
     - `deleteNotification(id)` - Delete single notification
     - `markAllAsRead()` - Mark all as read
     - `deleteAll()` - Delete all notifications
     - `deleteAllRead()` - Delete all read notifications
     - `applyFilters()` - Apply filter criteria
     - `refreshNotifications()` - Reload list

3. **Add Route**:
   - Update `src/Monolith.FireWall.WebUI/wwwroot/page/routes.json`
   - Add route for `/notifications`
   - Include JS and CSS assets

4. **Add to Menu** (Optional):
   - Could add to System menu or Status menu
   - Or keep it accessible only via notification dropdown link

### Step 3: Add Delete API Endpoints (Core)
1. **Add to MonitoringHandler** (`src/Monolith.FireWall.Core/Transport/Handlers/MonitoringHandler.cs`):
   - Add `"monitoring.notifications.delete"` to Actions set
   - Add case handler for delete action
   - Parse `NotificationDeleteRequest` (similar to `NotificationReadRequest`)

2. **Add to MonitoringManager** (`src/Monolith.FireWall.Core/Services/MonitoringManager.cs`):
   - Add `DeleteNotificationsAsync(NotificationDeleteRequest request)` method
   - Support deleting by ID, multiple IDs, or all
   - Return success/error tuple

3. **Add to Notification Store** (if needed):
   - Check if store has delete methods
   - Add `DeleteNotificationAsync(int id)`, `DeleteNotificationsAsync(List<int> ids)`, `DeleteAllAsync()`
   - Add `DeleteAllReadAsync()` for convenience

4. **Create Request Models** (if needed):
   - `NotificationDeleteRequest` in `src/Monolith.FireWall.Core/Models/`
   - Similar structure to `NotificationReadRequest`

### Step 4: API Integration (WebUI)
1. **Update CoreApiClient** (`src/Monolith.FireWall.WebUI/Services/CoreApiClient.cs`):
   - Add `DeleteNotificationAsync(id)` method
   - Add `DeleteNotificationsAsync(ids)` method
   - Add `DeleteAllNotificationsAsync()` method
   - Use Unix socket communication

2. **Update app.js**:
   - Add delete methods to `Monolith.API` namespace if needed
   - Or use `CoreApiClient` directly in notifications.js

### Step 4: Styling
1. **Notification Page Styles**:
   - Table/card layout
   - Badge colors for notification types
   - Action buttons styling
   - Filter controls
   - Empty state (no notifications)

2. **Dropdown Menu Styles**:
   - Fix title/message layout
   - Ensure proper spacing
   - Make sure "View All" link is visible

## File Changes Summary

### Files to Create
1. `src/Monolith.FireWall.WebUI/Pages/Notifications.cshtml`
2. `src/Monolith.FireWall.WebUI/wwwroot/js/pages/notifications.js`

### Files to Modify
1. `src/Monolith.FireWall.WebUI/wwwroot/js/app.js`
   - Fix notification dropdown rendering
   - Fix "mark all read" to refresh list
   - Add "View All" link

2. `src/Monolith.FireWall.WebUI/wwwroot/css/app.css`
   - Fix notification item layout
   - Add notification page styles

3. `src/Monolith.FireWall.WebUI/wwwroot/page/routes.json`
   - Add `/notifications` route

4. `src/Monolith.FireWall.WebUI/Services/CoreApiClient.cs` (if needed)
   - Add notification API methods

## Testing Checklist
- [ ] Notification dropdown shows title on new line
- [ ] "Mark all read" removes notifications from dropdown (or marks them visually)
- [ ] "View All Notifications" link appears in dropdown
- [ ] Notification page loads and displays notifications
- [ ] Can mark individual notifications as read
- [ ] Can delete individual notifications
- [ ] Can mark all as read from page
- [ ] Can delete all notifications
- [ ] Filters work correctly
- [ ] Badge count updates correctly
- [ ] Empty states display properly

## Notes
- Consider pagination if there are many notifications
- May need to check Core API for notification management endpoints
- Ensure proper error handling and user feedback
- Consider adding notification preferences/settings in the future
