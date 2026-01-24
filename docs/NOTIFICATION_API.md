# Notification API

The Monolith Firewall Notification API allows services, packages, and modules to send notifications to users through the WebUI.

## Overview

Notifications are displayed in the notification dropdown in the navigation bar and can be viewed/managed on the `/notifications` page.

## Database

Notifications are stored in the `system_notifications` table with the following fields:

- **Id**: Auto-increment primary key
- **Type**: Source/category of the notification (e.g., "vpn", "dhcp", "system")
- **Severity**: Level of importance ("info", "warning", "error")
- **Title**: Main notification text (max 160 chars, required)
- **Message**: Optional detailed message
- **MonitorKey**: Optional reference to a monitoring key
- **DetailsJson**: Optional JSON data for custom information
- **CreatedAt**: Timestamp when notification was created
- **ReadAt**: Timestamp when notification was marked as read (null = unread)

## Creating Notifications

### From Core Services (C#)

Use the `MonitoringManager.CreateNotificationAsync` method:

```csharp
using Monolith.FireWall.Core.Models;
using Monolith.FireWall.Core.Services;

// Inject MonitoringManager in your service constructor
var monitoringManager = context.MonitoringManager;

// Create a notification
var request = new NotificationCreateRequest
{
    Title = "VPN Connection Established",
    Message = "Successfully connected to remote VPN server 10.0.1.1",
    Severity = "info",        // "info", "warning", or "error"
    Type = "vpn",             // Type/source of notification
    DetailsJson = "{\"server\":\"10.0.1.1\",\"protocol\":\"OpenVPN\"}"  // Optional
};

var (success, notificationId, error) = await monitoringManager.CreateNotificationAsync(request);
if (success)
{
    // Notification created with ID: notificationId
}
else
{
    // Handle error
}
```

### From WebUI/API (JavaScript)

Use the `Monolith.API.post` method with the `monitoring/notifications/create` endpoint:

```javascript
// Create a notification from JavaScript
const response = await Monolith.API.post('/monitoring/notifications/create', {
    title: 'DHCP Lease Assigned',
    message: 'New DHCP lease assigned to device 00:11:22:33:44:55',
    severity: 'info',
    type: 'dhcp'
});

if (response.Success || response.success) {
    console.log('Notification created with ID:', response.Data.id || response.data.id);
}
```

### Via Core API (JSON Request)

Send a JSON request through the Core API socket:

```json
{
    "action": "monitoring.notifications.create",
    "payload": {
        "title": "Firewall Rule Updated",
        "message": "Rule #5 was modified to allow port 8080",
        "severity": "info",
        "type": "firewall"
    }
}
```

Response:
```json
{
    "Success": true,
    "Data": {
        "id": 42
    }
}
```

## Severity Levels

Notifications support three severity levels:

- **info** (default): General information, success messages
  - Badge color: Blue
  - Use for: Successful operations, status updates, informational messages

- **warning**: Important information that requires attention
  - Badge color: Yellow/Orange
  - Use for: Configuration issues, degraded performance, non-critical errors

- **error**: Critical issues requiring immediate attention
  - Badge color: Red
  - Use for: Service failures, connection errors, critical system issues

## Notification Types

The `Type` field categorizes the source of the notification. Common types include:

- **system**: General system notifications
- **monitor**: Automatic monitoring system notifications
- **vpn**: VPN-related notifications
- **dhcp**: DHCP server notifications
- **firewall**: Firewall rule notifications
- **backup**: Backup/restore notifications
- **update**: System/package update notifications

You can use any custom type that makes sense for your service or package.

## Best Practices

1. **Keep titles concise**: Titles should be under 160 characters and describe the issue clearly
2. **Use appropriate severity**: Don't overuse "error" severity - reserve it for critical issues
3. **Provide context in messages**: Include relevant details (IP addresses, usernames, etc.)
4. **Use consistent types**: Use standardized type names for your service/package
5. **Avoid notification spam**: Don't create notifications for every minor event
6. **Clean up old notifications**: Encourage users to delete read notifications periodically

## Managing Notifications

Users can manage notifications through:

- **Notification dropdown**: Quick view of recent notifications (up to 5)
- **Notifications page** (`/notifications`): Full notification management interface
  - Filter by status (all, unread, read)
  - Filter by severity (all, info, warning, error)
  - Mark individual/all as read
  - Delete individual/all notifications
  - Delete all read notifications

## Examples

### VPN Connection Example
```csharp
await monitoringManager.CreateNotificationAsync(new NotificationCreateRequest
{
    Title = "VPN Client Connected",
    Message = $"User 'john@example.com' connected from 203.0.113.50",
    Severity = "info",
    Type = "vpn"
});
```

### DHCP Lease Example
```javascript
await Monolith.API.post('/monitoring/notifications/create', {
    title: 'DHCP Pool Exhausted',
    message: 'LAN pool has no available IP addresses (0/254 free)',
    severity: 'error',
    type: 'dhcp'
});
```

### Firewall Rule Example
```csharp
await monitoringManager.CreateNotificationAsync(new NotificationCreateRequest
{
    Title = "Firewall Rule Disabled",
    Message = "Rule #12 (Block External SSH) was disabled by admin",
    Severity = "warning",
    Type = "firewall"
});
```

### System Update Example
```javascript
await Monolith.API.post('/monitoring/notifications/create', {
    title: 'System Update Available',
    message: 'Monolith Firewall v2.1.0 is available for download',
    severity = 'info',
    type: 'update'
});
```

## API Reference

### Create Notification

**Action**: `monitoring.notifications.create`
**HTTP**: `POST /api/monitoring/notifications/create`
**Method**: `MonitoringManager.CreateNotificationAsync(request)`

**Request Parameters**:
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| title | string | Yes | Notification title (max 160 chars) |
| message | string | No | Detailed message |
| severity | string | No | "info", "warning", or "error" (default: "info") |
| type | string | No | Notification source/category (default: "system") |
| monitorKey | string | No | Reference to a monitor |
| detailsJson | string | No | Custom JSON data |

**Response**:
```json
{
    "Success": true,
    "Data": {
        "id": 123
    }
}
```

### List Notifications

**Action**: `monitoring.notifications.list`
**HTTP**: `GET /api/monitoring/notifications?limit=20&unreadOnly=false`

**Query Parameters**:
| Field | Type | Default | Description |
|-------|------|---------|-------------|
| limit | int | 20 | Max notifications to return (max: 100) |
| unreadOnly | bool | false | Only return unread notifications |

**Response**:
```json
{
    "Success": true,
    "Data": {
        "Notifications": [...],
        "UnreadCount": 5
    }
}
```

### Mark as Read

**Action**: `monitoring.notifications.read`
**HTTP**: `POST /api/monitoring/notifications/read`

**Request**:
```json
{
    "ids": [1, 2, 3],  // Specific notification IDs
    "all": false        // Or set to true to mark all as read
}
```

### Delete Notifications

**Action**: `monitoring.notifications.delete`
**HTTP**: `POST /api/monitoring/notifications/delete`

**Request**:
```json
{
    "ids": [1, 2, 3],   // Specific notification IDs
    "all": false,       // Or set to true to delete all
    "readOnly": false   // Or set to true to delete only read notifications
}
```
