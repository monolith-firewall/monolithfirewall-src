namespace Monolith.FireWall.Common.Attributes;

/// <summary>
/// Marks a method as a route handler for the specified action.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public sealed class RouteActionAttribute : Attribute
{
    public string Action { get; }
    public string[]? RequiredPermissions { get; set; }

    public RouteActionAttribute(string action)
    {
        Action = action;
    }
}

/// <summary>
/// Marks a class as a controller for a module.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ModuleControllerAttribute : Attribute
{
    /// <summary>
    /// The module ID this controller belongs to.
    /// If not specified, inferred from naming convention (e.g., DhcpController -> dhcp).
    /// </summary>
    public string? ModuleId { get; set; }
}
