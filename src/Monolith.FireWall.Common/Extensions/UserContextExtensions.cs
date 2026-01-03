using Monolith.FireWall.Common.Models;

namespace Monolith.FireWall.Common.Extensions;

public static class UserContextExtensions
{
    public static bool HasPermission(this UserContext user, params string[] required)
    {
        if (user.Roles.Contains("admin"))
            return true;

        return required.All(p => user.Permissions.Contains(p));
    }
}
