using Monolith.FireWall.Common.Interfaces;
using Monolith.FireWall.Common.Models;

namespace Monolith.FireWall.Common.Modules;

/// <summary>
/// Abstract base class for Monolith modules.
/// Provides sensible defaults and automatic attribute-based discovery.
/// </summary>
public abstract class MonolithModuleBase : IMonolithModule, IMonolithModuleLifecycle
{
    private ModuleMetadata? _metadata;
    private IModuleContext? _context;

    /// <summary>
    /// Module context, available after OnStartAsync is called.
    /// </summary>
    protected IModuleContext Context => _context ?? throw new InvalidOperationException("Module not started");

    /// <summary>
    /// Whether the module has been started.
    /// </summary>
    protected bool IsStarted => _context != null;

    /// <summary>
    /// Logger instance.
    /// </summary>
    protected Interfaces.ILogger Logger => Context.Logger;

    /// <summary>
    /// Cached metadata extracted from attributes.
    /// </summary>
    public ModuleMetadata Metadata => _metadata ??= ModuleMetadataExtractor.Extract(GetType());

    // IMonolithModule implementation - delegates to metadata
    public virtual string Id => Metadata.Id;
    public virtual string Name => Metadata.Name;
    public virtual string PackageId => Metadata.PackageId;

    public virtual IEnumerable<RouteDefinition> GetRoutes()
    {
        // Routes are built from controllers
        return Metadata.Routes;
    }

    public virtual IEnumerable<MenuDefinition> GetMenuItems()
    {
        return Metadata.MenuItems;
    }

    public virtual IEnumerable<PageDefinition> GetPages()
    {
        return Metadata.Pages;
    }

    public virtual IEnumerable<WidgetDefinition> GetWidgets()
    {
        return Metadata.Widgets;
    }

    public virtual IEnumerable<TemplateDefinition> GetTemplates()
    {
        return Metadata.Templates;
    }

    public virtual IEnumerable<ServiceDefinition> GetServices()
    {
        return Metadata.Services;
    }

    public virtual IEnumerable<ServiceBindingDefinition> GetServiceBindings()
    {
        return Metadata.ServiceBindings;
    }

    public virtual IEnumerable<AptDependency> GetAptDependencies()
    {
        return Metadata.AptDependencies;
    }

    public virtual IEnumerable<PermissionDefinition> GetRequiredPermissions()
    {
        return Metadata.Permissions;
    }

    public virtual IEnumerable<SystemPermissionDefinition> GetSystemPermissions()
    {
        return Metadata.SystemPermissions;
    }

    public virtual IEnumerable<CronJobDefinition> GetCronJobs()
    {
        return Metadata.CronJobs;
    }

    public virtual IEnumerable<ISetupWizardPage> GetSetupWizardPages()
    {
        return Metadata.SetupPages;
    }

    // IMonolithModuleLifecycle implementation
    public virtual Task OnStartAsync(IModuleContext context)
    {
        _context = context;
        return Task.CompletedTask;
    }

    public virtual Task OnStopAsync(IModuleContext context)
    {
        _context = null;
        return Task.CompletedTask;
    }

    public virtual Task OnConfigChangedAsync(string key, string? oldValue, string? newValue)
    {
        return Task.CompletedTask;
    }
}
