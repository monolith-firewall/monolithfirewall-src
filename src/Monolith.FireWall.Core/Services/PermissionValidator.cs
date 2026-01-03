using Monolith.FireWall.Common.Enums;
using Monolith.FireWall.Common.Extensions;
using Monolith.FireWall.Common.Interfaces;
using Monolith.FireWall.Common.Models;

namespace Monolith.FireWall.Core.Services;

public class PermissionValidator
{
    private readonly ILogger _logger;
    private readonly ModuleRegistry _registry;

    public PermissionValidator(ILogger logger, ModuleRegistry registry)
    {
        _logger = logger;
        _registry = registry;
    }

    public bool ValidateUserPermission(UserContext user, string[] required)
    {
        // Admin bypass
        if (user.Roles.Contains("admin"))
        {
            return true;
        }

        // Check if user has all required permissions
        var hasPermission = user.HasPermission(required);

        if (!hasPermission)
        {
            _logger.LogWarning($"User {user.Username} denied: missing permissions {string.Join(", ", required)}");
        }

        return hasPermission;
    }

    public bool ValidateSystemPermission(
        string moduleId,
        SystemPermissionType type,
        string resource)
    {
        var module = _registry.GetModule(moduleId);
        if (module == null)
        {
            _logger.LogWarning($"Module not found: {moduleId}");
            return false;
        }

        var permissions = module.Module.GetSystemPermissions();
        var hasPermission = permissions.Any(p =>
            p.Type == type &&
            p.Resource == resource
        );

        if (!hasPermission)
        {
            _logger.LogWarning($"Module {moduleId} denied: missing system permission {type}:{resource}");
        }

        return hasPermission;
    }
}
