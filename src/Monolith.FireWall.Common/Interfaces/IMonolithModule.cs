using Monolith.FireWall.Common.Models;

namespace Monolith.FireWall.Common.Interfaces;

/// <summary>
/// Module definition and provider methods.
/// </summary>
public interface IMonolithModule
{
    /// <summary>Module ID (e.g., "system.users")</summary>
    string Id { get; }

    /// <summary>Display name</summary>
    string Name { get; }

    /// <summary>Parent package ID</summary>
    string PackageId { get; }

    // Provider methods
    IEnumerable<RouteDefinition> GetRoutes();
    IEnumerable<MenuDefinition> GetMenuItems();
    IEnumerable<PageDefinition> GetPages();
    IEnumerable<WidgetDefinition> GetWidgets();
    IEnumerable<TemplateDefinition> GetTemplates();
    IEnumerable<ServiceDefinition> GetServices();
    IEnumerable<AptDependency> GetAptDependencies();
    IEnumerable<PermissionDefinition> GetRequiredPermissions();
    IEnumerable<SystemPermissionDefinition> GetSystemPermissions();
    IEnumerable<CronJobDefinition> GetCronJobs();
    
    /// <summary>
    /// Get setup wizard pages provided by this module.
    /// Returns empty enumerable if module doesn't provide setup pages.
    /// </summary>
    IEnumerable<ISetupWizardPage> GetSetupWizardPages();
}
