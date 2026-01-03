namespace Monolith.FireWall.Common.Models;

public record PermissionDefinition(
    string Id,
    string Name,
    string Category,
    string Description
);

public record RoleDefinition(
    string Id,
    string Name,
    string[] Permissions
);
