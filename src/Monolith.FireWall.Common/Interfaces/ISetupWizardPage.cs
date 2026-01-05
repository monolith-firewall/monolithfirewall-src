namespace Monolith.FireWall.Common.Interfaces;

/// <summary>
/// Represents a setup wizard page that can be provided by a module.
/// </summary>
public interface ISetupWizardPage
{
    /// <summary>Unique identifier for this setup page</summary>
    string Id { get; }

    /// <summary>Display title</summary>
    string Title { get; }

    /// <summary>Description/help text</summary>
    string Description { get; }

    /// <summary>Display order (lower numbers appear first)</summary>
    int Order { get; }

    /// <summary>WebUI route for this setup page (e.g., "/setup/package/monolith-network/dhcp")</summary>
    string Route { get; }

    /// <summary>Whether this step is required to complete setup</summary>
    bool IsRequired { get; }

    /// <summary>Whether this setup page has been completed</summary>
    bool IsComplete { get; }

    /// <summary>Package ID that provides this setup page</summary>
    string PackageId { get; }

    /// <summary>Module ID that provides this setup page</summary>
    string ModuleId { get; }
}
