# CL.SQLite Implementation Guide

**Complete guide for using CL.SQLite with Repository<T> and QueryBuilder<T>**

---

## Overview

CL.SQLite provides:
- **Repository<T>** - CRUD operations
- **QueryBuilder<T>** - Complex queries
- **Database Models** - SQLite attributes
- **Table Sync** - Automatic schema management

---

## 1. Database Model

### File: `Features/Users/Models/UserEntity.cs`

```csharp
using CL.SQLite.Models;

namespace Monolith.FireWall.WebUI.Features.Users.Models;

/// <summary>
/// User database model with CL.SQLite attributes
/// </summary>
[SQLiteTable("users")]
[SQLiteIndex(new[] { "username" }, IsUnique = true, Name = "idx_users_username")]
[SQLiteIndex(new[] { "email" }, Name = "idx_users_email")]
public class UserEntity
{
    [SQLiteColumn(IsPrimaryKey = true, IsAutoIncrement = true)]
    public int Id { get; set; }

    [SQLiteColumn(IsNotNull = true, IsUnique = true, Size = 100)]
    public string Username { get; set; } = string.Empty;

    [SQLiteColumn(IsNotNull = true, Size = 255)]
    public string Email { get; set; } = string.Empty;

    [SQLiteColumn(IsNotNull = true, ColumnName = "password_hash", Size = 255)]
    public string PasswordHash { get; set; } = string.Empty;

    [SQLiteColumn(DataType = SQLiteDataType.TEXT, ColumnName = "roles")]
    public string RolesJson { get; set; } = "[]";

    [SQLiteColumn(DataType = SQLiteDataType.BOOLEAN, DefaultValue = "1")]
    public bool Enabled { get; set; } = true;

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME, ColumnName = "created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [SQLiteColumn(DataType = SQLiteDataType.DATETIME, ColumnName = "updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

---

## 2. Get CL.SQLite Service

### In Core Service

```csharp
// After CodeLogic.StartAsync()
var sqlite = CodeLogic.Libs.Get<CL.SQLite.SQLiteLibrary>();
if (sqlite == null)
{
    throw new Exception("CL.SQLite library not found");
}
```

### In Package Module

```csharp
// In module OnStartAsync
var sqlite = context.GetService<CL.SQLite.SQLiteLibrary>();
if (sqlite == null)
{
    throw new Exception("CL.SQLite not available");
}
```

---

## 3. Create Repository

```csharp
var sqlite = CodeLogic.Libs.Get<CL.SQLite.SQLiteLibrary>();
var repository = sqlite.CreateRepository<UserEntity>();
```

---

## 4. Create QueryBuilder

```csharp
var sqlite = CodeLogic.Libs.Get<CL.SQLite.SQLiteLibrary>();
var queryBuilder = sqlite.CreateQueryBuilder<UserEntity>();
```

---

## 5. Repository Operations

### Create

```csharp
var user = new UserEntity
{
    Username = "testuser",
    Email = "test@example.com",
    PasswordHash = BCrypt.Net.BCrypt.HashPassword("password"),
    RolesJson = "[\"user\"]",
    Enabled = true,
    CreatedAt = DateTime.UtcNow,
    UpdatedAt = DateTime.UtcNow
};

var result = await repository.InsertAsync(user);
if (result.IsSuccess)
{
    var insertedId = result.Data; // Returns inserted ID
}
```

### Read All

```csharp
var result = await repository.GetAllAsync();
if (result.IsSuccess)
{
    var users = result.Data; // List<UserEntity>
}
```

### Read By ID

```csharp
var result = await repository.GetByIdAsync(1);
if (result.IsSuccess)
{
    var user = result.Data; // UserEntity
}
```

### Update

```csharp
var user = await repository.GetByIdAsync(1);
if (user.IsSuccess && user.Data != null)
{
    user.Data.Email = "newemail@example.com";
    user.Data.UpdatedAt = DateTime.UtcNow;
    
    var updateResult = await repository.UpdateAsync(user.Data);
}
```

### Delete

```csharp
var result = await repository.DeleteAsync(1);
if (result.IsSuccess)
{
    // Deleted
}
```

---

## 6. QueryBuilder Operations

### Where Clause

```csharp
var result = await queryBuilder
    .Where(u => u.Username == "admin")
    .FirstOrDefaultAsync();

if (result.IsSuccess && result.Data != null)
{
    var user = result.Data;
}
```

### Multiple Conditions

```csharp
var result = await queryBuilder
    .Where(u => u.Enabled == true)
    .Where(u => u.Email.Contains("@example.com"))
    .ToListAsync();
```

### Order By

```csharp
var result = await queryBuilder
    .OrderBy(u => u.Username)
    .ToListAsync();
```

### Limit/Offset

```csharp
var result = await queryBuilder
    .OrderBy(u => u.CreatedAt)
    .Limit(10)
    .Offset(0)
    .ToListAsync();
```

### Count

```csharp
var result = await queryBuilder
    .Where(u => u.Enabled == true)
    .CountAsync();

if (result.IsSuccess)
{
    var count = result.Data;
}
```

---

## 7. Table Sync

### Sync Table Schema

```csharp
var sqlite = CodeLogic.Libs.Get<CL.SQLite.SQLiteLibrary>();
await sqlite.TableSyncService!.SyncTableAsync<UserEntity>();
```

This will:
- Create table if it doesn't exist
- Add missing columns
- Create indexes
- **Does NOT** drop columns or modify existing data

---

## 8. Complete Example - UserService

### File: `Features/Users/Services/UserService.cs`

```csharp
using CL.SQLite.Services;
using Monolith.FireWall.WebUI.Features.Users.Models;

namespace Monolith.FireWall.WebUI.Features.Users.Services;

public class UserService
{
    private readonly Repository<UserEntity> _repository;
    private readonly QueryBuilder<UserEntity> _queryBuilder;
    private readonly ILogger<UserService> _logger;

    public UserService(CL.SQLite.SQLiteLibrary sqlite, ILogger<UserService> logger)
    {
        _repository = sqlite.CreateRepository<UserEntity>();
        _queryBuilder = sqlite.CreateQueryBuilder<UserEntity>();
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        // Sync table
        var sqlite = CodeLogic.Libs.Get<CL.SQLite.SQLiteLibrary>();
        await sqlite.TableSyncService!.SyncTableAsync<UserEntity>();

        // Check for admin user
        var adminResult = await _queryBuilder
            .Where(u => u.Username == "admin")
            .FirstOrDefaultAsync();

        if (!adminResult.IsSuccess || adminResult.Data == null)
        {
            // Create default admin
            var admin = new UserEntity
            {
                Username = "admin",
                Email = "admin@monolith.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin"),
                RolesJson = "[\"admin\"]",
                Enabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _repository.InsertAsync(admin);
            _logger.LogInformation("Default admin user created");
        }
    }

    public async Task<List<UserEntity>> GetAllUsersAsync()
    {
        var result = await _repository.GetAllAsync();
        return result.IsSuccess ? result.Data : new List<UserEntity>();
    }

    public async Task<UserEntity?> GetUserByIdAsync(int id)
    {
        var result = await _repository.GetByIdAsync(id);
        return result.IsSuccess ? result.Data : null;
    }

    public async Task<UserEntity?> GetUserByUsernameAsync(string username)
    {
        var result = await _queryBuilder
            .Where(u => u.Username == username)
            .FirstOrDefaultAsync();

        return result.IsSuccess ? result.Data : null;
    }

    public async Task<UserEntity> CreateUserAsync(string username, string email, string password, string[] roles)
    {
        var user = new UserEntity
        {
            Username = username,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            RolesJson = System.Text.Json.JsonSerializer.Serialize(roles),
            Enabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var result = await _repository.InsertAsync(user);
        if (result.IsSuccess)
        {
            user.Id = result.Data;
            return user;
        }

        throw new Exception("Failed to create user");
    }

    public async Task<bool> ValidatePasswordAsync(string username, string password)
    {
        var user = await GetUserByUsernameAsync(username);
        if (user == null || !user.Enabled)
            return false;

        return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
    }
}
```

---

## 9. Package Module Example

### In Package Module

```csharp
public class DhcpManager
{
    private readonly Repository<DhcpLeaseEntity> _repository;
    private readonly QueryBuilder<DhcpLeaseEntity> _queryBuilder;

    public DhcpManager(IModuleContext context)
    {
        var sqlite = context.GetService<CL.SQLite.SQLiteLibrary>();
        _repository = sqlite.CreateRepository<DhcpLeaseEntity>();
        _queryBuilder = sqlite.CreateQueryBuilder<DhcpLeaseEntity>();
    }

    public async Task InitializeAsync()
    {
        var sqlite = _context.GetService<CL.SQLite.SQLiteLibrary>();
        await sqlite.TableSyncService!.SyncTableAsync<DhcpLeaseEntity>();
    }

    public async Task<List<DhcpLeaseEntity>> GetActiveLeasesAsync()
    {
        var result = await _queryBuilder
            .Where(l => l.ExpiresAt > DateTime.UtcNow)
            .OrderBy(l => l.IpAddress)
            .ToListAsync();

        return result.IsSuccess ? result.Data : new List<DhcpLeaseEntity>();
    }
}
```

---

## Summary

✅ **Repository<T>** - CRUD operations  
✅ **QueryBuilder<T>** - Complex queries  
✅ **Table Sync** - Automatic schema  
✅ **Database Models** - SQLite attributes  

**Ready for CL.SQLite!** 🚀
