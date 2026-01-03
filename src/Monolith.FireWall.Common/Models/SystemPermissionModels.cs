using Monolith.FireWall.Common.Enums;

namespace Monolith.FireWall.Common.Models;

public record SystemPermissionDefinition(
    SystemPermissionType Type,
    string Resource,
    string Justification
);
