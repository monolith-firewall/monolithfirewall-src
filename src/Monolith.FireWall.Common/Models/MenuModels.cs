namespace Monolith.FireWall.Common.Models;

public record MenuDefinition(
    string Id,
    string Label,
    string Icon,
    int Order,
    string[] RequiredPermissions,
    MenuDefinition[]? Children = null
);

public record PageDefinition(
    string Route,
    string RazorPath,
    string[] RequiredPermissions
);
