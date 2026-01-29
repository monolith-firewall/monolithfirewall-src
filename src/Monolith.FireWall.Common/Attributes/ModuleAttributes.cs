namespace Monolith.FireWall.Common.Attributes;

/// <summary>
/// Marks a class as a Monolith module and defines its core metadata.
/// Replaces the need for explicit Id, Name properties.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ModuleAttribute : Attribute
{
    public string Id { get; }
    public string Name { get; }
    public string? Description { get; set; }

    public ModuleAttribute(string id, string name)
    {
        Id = id;
        Name = name;
    }
}

/// <summary>
/// Specifies the package that contains this module.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class PackageAttribute : Attribute
{
    public string PackageId { get; }

    public PackageAttribute(string packageId)
    {
        PackageId = packageId;
    }
}
