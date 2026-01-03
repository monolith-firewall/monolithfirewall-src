using Monolith.FireWall.Common.Enums;
using Monolith.FireWall.Common.Models;
using Monolith.FireWall.Platform.Models;

namespace Monolith.FireWall.Core.Services.Platform;

public static class PlatformCapabilityMapper
{
    public static PlatformCapability FromSystemPermissions(IEnumerable<SystemPermissionDefinition> permissions)
    {
        if (permissions == null)
        {
            return PlatformCapability.None;
        }

        var capabilities = PlatformCapability.None;
        foreach (var permission in permissions)
        {
            var resource = permission.Resource?.Trim() ?? string.Empty;
            var canRead = HasReadAccess(resource);
            var canWrite = HasWriteAccess(resource);

            switch (permission.Type)
            {
                case SystemPermissionType.NetworkControl:
                    if (canRead)
                    {
                        capabilities |= PlatformCapability.NetworkRead;
                    }
                    if (canWrite)
                    {
                        capabilities |= PlatformCapability.NetworkWrite;
                    }
                    break;

                case SystemPermissionType.FileRead:
                    capabilities |= PlatformCapability.FilesystemRead;
                    break;

                case SystemPermissionType.FileWrite:
                    capabilities |= PlatformCapability.FilesystemWrite;
                    break;

                case SystemPermissionType.CommandExec:
                    if (canRead)
                    {
                        capabilities |= PlatformCapability.SystemRead;
                    }
                    if (canWrite)
                    {
                        capabilities |= PlatformCapability.SystemWrite;
                    }
                    break;

                case SystemPermissionType.ServiceControl:
                    capabilities |= PlatformCapability.SystemWrite;
                    break;
            }
        }

        return capabilities;
    }

    private static bool HasReadAccess(string resource)
    {
        if (string.IsNullOrWhiteSpace(resource))
        {
            return false;
        }

        var normalized = resource.ToLowerInvariant();
        return normalized == "read"
            || normalized == "r"
            || normalized == "rw"
            || normalized == "read-write"
            || normalized == "*";
    }

    private static bool HasWriteAccess(string resource)
    {
        if (string.IsNullOrWhiteSpace(resource))
        {
            return false;
        }

        var normalized = resource.ToLowerInvariant();
        return normalized == "write"
            || normalized == "w"
            || normalized == "rw"
            || normalized == "read-write"
            || normalized == "*";
    }
}
