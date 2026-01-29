namespace Monolith.FireWall.Common.Attributes;

/// <summary>
/// Defines a menu item for this module.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class MenuItemAttribute : Attribute
{
    public string Id { get; }
    public string Label { get; }
    public string Icon { get; }
    public int Order { get; set; } = 100;
    public string[]? RequiredPermissions { get; set; }
    public string? ParentMenuId { get; set; }

    public MenuItemAttribute(string id, string label, string icon)
    {
        Id = id;
        Label = label;
        Icon = icon;
    }
}

/// <summary>
/// Defines a page route for this module.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class PageAttribute : Attribute
{
    public string Route { get; }
    public string RazorPath { get; }
    public string[]? RequiredPermissions { get; set; }

    public PageAttribute(string route, string razorPath)
    {
        Route = route;
        RazorPath = razorPath;
    }
}
