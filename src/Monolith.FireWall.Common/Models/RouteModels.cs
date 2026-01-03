namespace Monolith.FireWall.Common.Models;

public record RouteDefinition(
    string Action,
    Func<ApiRequest, Task<ApiResponse>> Handler,
    string[] RequiredPermissions
);
