using Monolith.FireWall.Common.Controllers;
using Monolith.FireWall.Common.Interfaces;
using Monolith.FireWall.Common.Models;

namespace Monolith.FireWall.Common.Modules;

/// <summary>
/// Container for all metadata extracted from module attributes.
/// </summary>
public sealed class ModuleMetadata
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string PackageId { get; init; } = "";
    public string? Description { get; init; }

    public List<RouteDefinition> Routes { get; init; } = new();
    public List<MenuDefinition> MenuItems { get; init; } = new();
    public List<PageDefinition> Pages { get; init; } = new();
    public List<WidgetDefinition> Widgets { get; init; } = new();
    public List<TemplateDefinition> Templates { get; init; } = new();
    public List<ServiceDefinition> Services { get; init; } = new();
    public List<ServiceBindingDefinition> ServiceBindings { get; init; } = new();
    public List<AptDependency> AptDependencies { get; init; } = new();
    public List<PermissionDefinition> Permissions { get; init; } = new();
    public List<SystemPermissionDefinition> SystemPermissions { get; init; } = new();
    public List<CronJobDefinition> CronJobs { get; init; } = new();
    public List<SetupWizardPage> SetupPages { get; init; } = new();

    /// <summary>
    /// Controller types discovered for this module.
    /// </summary>
    public List<Type> ControllerTypes { get; init; } = new();
}
