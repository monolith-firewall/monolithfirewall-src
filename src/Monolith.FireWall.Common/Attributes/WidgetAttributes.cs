namespace Monolith.FireWall.Common.Attributes;

/// <summary>
/// Defines a dashboard widget provided by this module.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class WidgetAttribute : Attribute
{
    public string Id { get; }
    public string Title { get; }
    public string Description { get; }
    public string Icon { get; }
    public int DefaultWidth { get; set; } = 4;
    public int DefaultHeight { get; set; } = 2;
    public int DefaultPosition { get; set; } = 0;
    public int RefreshInterval { get; set; } = 30;
    public string[]? RequiredPermissions { get; set; }

    public WidgetAttribute(string id, string title, string description, string icon)
    {
        Id = id;
        Title = title;
        Description = description;
        Icon = icon;
    }
}

/// <summary>
/// Defines a config template managed by this module.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class TemplateAttribute : Attribute
{
    public string Id { get; }
    public string ResourcePath { get; }
    public string OutputPath { get; }
    public bool RequiresRoot { get; set; } = false;

    public TemplateAttribute(string id, string resourcePath, string outputPath)
    {
        Id = id;
        ResourcePath = resourcePath;
        OutputPath = outputPath;
    }
}
