namespace Monolith.FireWall.Common.Models;

/// <summary>
/// Defines a dashboard widget provided by a module
/// </summary>
public class WidgetDefinition
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string Package { get; set; }
    public string Module { get; set; }
    public string Description { get; set; }
    public string Icon { get; set; }
    public int DefaultWidth { get; set; }
    public int DefaultHeight { get; set; }
    public int RefreshInterval { get; set; }
    public string[] RequiredPermissions { get; set; }

    public WidgetDefinition(
        string id,
        string title,
        string package,
        string module,
        string description,
        string icon,
        int defaultWidth,
        int defaultHeight,
        int refreshInterval,
        string[] requiredPermissions)
    {
        Id = id;
        Title = title;
        Package = package;
        Module = module;
        Description = description;
        Icon = icon;
        DefaultWidth = defaultWidth;
        DefaultHeight = defaultHeight;
        RefreshInterval = refreshInterval;
        RequiredPermissions = requiredPermissions;
    }
}
