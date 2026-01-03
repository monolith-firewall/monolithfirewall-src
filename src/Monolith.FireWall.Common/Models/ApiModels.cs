namespace Monolith.FireWall.Common.Models;

public record ApiRequest(
    string PackageId,
    string ModuleId,
    string Action,
    Dictionary<string, string> Query,
    string? Body,
    UserContext User
);

public record ApiResponse(
    bool Success,
    object? Data,
    string? Error
);

public record UserContext(
    int UserId,
    string Username,
    string[] Roles,
    string[] Permissions
);
