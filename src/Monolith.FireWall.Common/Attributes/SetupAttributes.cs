namespace Monolith.FireWall.Common.Attributes;

/// <summary>
/// Defines a setup wizard page for this module.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class SetupWizardPageAttribute : Attribute
{
    public string Id { get; }
    public string Title { get; }
    public string Description { get; }
    public string Route { get; }
    public int Order { get; set; } = 100;
    public bool IsRequired { get; set; } = false;

    public SetupWizardPageAttribute(string id, string title, string description, string route)
    {
        Id = id;
        Title = title;
        Description = description;
        Route = route;
    }
}

/// <summary>
/// Defines a scheduled cron job for this module.
/// Applied to methods that should be executed on a schedule.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class CronJobAttribute : Attribute
{
    public string Id { get; }
    public string Name { get; }
    public string CronExpression { get; }
    public bool Enabled { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 600;
    public int MaxFailuresBeforeDisable { get; set; } = 5;

    public CronJobAttribute(string id, string name, string cronExpression)
    {
        Id = id;
        Name = name;
        CronExpression = cronExpression;
    }
}
