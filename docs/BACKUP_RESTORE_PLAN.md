# Backup & Restore System Plan

## Overview
Create a comprehensive backup and restore system for Monolith Firewall that allows administrators to backup and restore the system database, with support for both local and cloud storage (cloud to be implemented later).

## Current System Architecture

### Database
- **Location**: SQLite database managed by CL.SQLite library
- **Path**: `/var/lib/monolith-firewall/codelogic/CodeLogic.db` (typical location)
- **Entities**: Multiple entities stored including:
  - Interface assignments
  - Firewall rules, aliases, NAT rules
  - System settings
  - DHCP/DNS configurations
  - WebUI settings
  - Package installations
  - Module states
  - Monitoring definitions
  - Log entries
  - System tuneables

### Services
- **Core Service**: `monolith-firewall-core.service` - Manages database and system state
- **WebUI Service**: `monolith-firewall-webui.service` - Provides web interface

## Requirements

### Functional Requirements
1. **Backup Functionality**
   - Create full database backup
   - Include metadata (timestamp, version, description)
   - Compress backups (gzip)
   - Store backups in configurable location
   - List available backups
   - Delete old backups

2. **Restore Functionality**
   - Restore from backup file
   - Validate backup before restore
   - Stop services during restore
   - Replace database
   - Restart services
   - Verify restore success

3. **UI Requirements**
   - Tabbed interface: "Local" and "Cloud" tabs
   - Local tab: List backups, create backup, restore, delete
   - Cloud tab: Placeholder for future S3/cloud storage integration
   - Show backup metadata (date, size, description)
   - Progress indicators for backup/restore operations

### Technical Requirements
1. **Backup Format**
   - SQLite database dump or file copy
   - JSON metadata file alongside backup
   - Compressed with gzip (.db.gz or .sqlite.gz)
   - Naming: `monolith-backup-YYYYMMDD-HHMMSS.db.gz`

2. **Backup Location**
   - Default: `/var/lib/monolith-firewall/backups/`
   - Configurable via settings
   - Ensure directory exists and has proper permissions

3. **Restore Process**
   - Validate backup file exists and is readable
   - Stop Core service (to release database lock)
   - Backup current database (safety measure)
   - Copy backup file to database location
   - Restart Core service
   - Verify database integrity
   - Restart WebUI service

4. **API Endpoints**
   - `backup.create` - Create new backup
   - `backup.list` - List available backups
   - `backup.restore` - Restore from backup
   - `backup.delete` - Delete backup file
   - `backup.download` - Download backup file (for manual backup)

## Implementation Plan

### Phase 1: Core Backup/Restore Service
1. **Create BackupManager Service**
   - Location: `src/Monolith.FireWall.Core/Services/BackupManager.cs`
   - Methods:
     - `CreateBackupAsync(string? description = null)` - Create backup
     - `ListBackupsAsync()` - List all backups
     - `RestoreBackupAsync(string backupFileName)` - Restore from backup
     - `DeleteBackupAsync(string backupFileName)` - Delete backup
     - `GetBackupInfoAsync(string backupFileName)` - Get backup metadata
     - `GetDatabasePathAsync()` - Get current database path

2. **Database Entity (Optional)**
   - `BackupEntity` - Track backup metadata in database
   - Fields: Id, FileName, CreatedAt, Size, Description, Type (local/cloud)

3. **Backup Metadata Format**
   ```json
   {
     "version": "1.0.0",
     "createdAt": "2026-01-05T08:00:00Z",
     "description": "Manual backup before update",
     "databaseVersion": "3.x",
     "fileSize": 1234567,
     "type": "local"
   }
   ```

### Phase 2: Core API Handler
1. **Create BackupHandler**
   - Location: `src/Monolith.FireWall.Core/Transport/Handlers/BackupHandler.cs`
   - Actions:
     - `backup.create` - Create backup
     - `backup.list` - List backups
     - `backup.restore` - Restore backup
     - `backup.delete` - Delete backup
     - `backup.info` - Get backup information

2. **Register Handler**
   - Add to `UnixSocketListener` constructor
   - Add to `CoreRequestContext`

### Phase 3: WebUI Implementation
1. **Add Menu Item**
   - Add "Backup & Restore" under System menu in `index.html`

2. **Create Page Route**
   - Route: `/system/backup`
   - Add to router and page loader

3. **Create Page Files**
   - `js/pages/backup.js` - Main backup page logic
   - Tabbed interface with Local and Cloud tabs
   - Local tab: Backup list, create, restore, delete
   - Cloud tab: Placeholder message

4. **Page Features**
   - Backup list table with metadata
   - Create backup button with description input
   - Restore button with confirmation
   - Delete button with confirmation
   - Download button for manual backup
   - Progress indicators
   - Success/error messages

### Phase 4: Cloud Backup (Future)
1. **Cloud Storage Interface**
   - `ICloudBackupProvider` interface
   - Implementations: S3, Azure Blob, Google Cloud Storage

2. **Cloud Tab Implementation**
   - List cloud backups
   - Upload to cloud
   - Download from cloud
   - Restore from cloud

## Database Schema

### BackupEntity (Optional - for tracking)
```csharp
public class BackupEntity
{
    public long Id { get; set; }
    public string FileName { get; set; }
    public DateTime CreatedAt { get; set; }
    public long FileSize { get; set; }
    public string? Description { get; set; }
    public string Type { get; set; } // "local" or "cloud"
    public string? CloudProvider { get; set; } // null for local
    public string? CloudPath { get; set; } // null for local
}
```

## File Structure

```
src/Monolith.FireWall.Core/
  Services/
    BackupManager.cs
  Transport/Handlers/
    BackupHandler.cs
  Models/
    BackupModels.cs

src/Monolith.FireWall.WebUI/
  wwwroot/js/pages/
    backup.js
```

## API Request/Response Formats

### Create Backup
**Request:**
```json
{
  "action": "backup.create",
  "body": {
    "description": "Manual backup before update"
  }
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "fileName": "monolith-backup-20260105-080000.db.gz",
    "createdAt": "2026-01-05T08:00:00Z",
    "size": 1234567,
    "description": "Manual backup before update"
  }
}
```

### List Backups
**Request:**
```json
{
  "action": "backup.list"
}
```

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "fileName": "monolith-backup-20260105-080000.db.gz",
      "createdAt": "2026-01-05T08:00:00Z",
      "size": 1234567,
      "description": "Manual backup before update"
    }
  ]
}
```

### Restore Backup
**Request:**
```json
{
  "action": "backup.restore",
  "body": {
    "fileName": "monolith-backup-20260105-080000.db.gz"
  }
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "message": "Backup restored successfully. Services restarted."
  }
}
```

## Security Considerations
1. Backup files should be readable only by root/monolith-firewall user
2. Restore operations require service restart (privileged)
3. Validate backup file integrity before restore
4. Create safety backup before restore
5. Limit backup file size to prevent DoS

## Error Handling
1. Database lock errors during backup
2. Insufficient disk space
3. Invalid backup file format
4. Service restart failures
5. Permission errors

## Testing Considerations
1. Test backup creation with active services
2. Test restore with various backup files
3. Test restore with corrupted backup files
4. Test concurrent backup/restore operations
5. Test disk space limits
6. Test service restart after restore
