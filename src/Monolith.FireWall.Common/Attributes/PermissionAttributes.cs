using Monolith.FireWall.Common.Enums;

namespace Monolith.FireWall.Common.Attributes;

/// <summary>
/// Defines a permission required by this module.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class PermissionAttribute : Attribute
{
    public string Id { get; }
    public string Name { get; }
    public string Category { get; }
    public string? SubCategory { get; set; }

    public PermissionAttribute(string id, string name, string category)
    {
        Id = id;
        Name = name;
        Category = category;
    }
}

/// <summary>
/// Defines a system-level permission (file access, network control, etc.).
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class SystemPermissionAttribute : Attribute
{
    public SystemPermissionType Type { get; }
    public string Resource { get; }
    public string Justification { get; }

    public SystemPermissionAttribute(SystemPermissionType type, string resource, string justification)
    {
        Type = type;
        Resource = resource;
        Justification = justification;
    }
}
