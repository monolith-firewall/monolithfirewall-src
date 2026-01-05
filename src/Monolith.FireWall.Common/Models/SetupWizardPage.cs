using Monolith.FireWall.Common.Interfaces;

namespace Monolith.FireWall.Common.Models;

/// <summary>
/// Simple implementation of ISetupWizardPage for modules to use
/// </summary>
public class SetupWizardPage : ISetupWizardPage
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int Order { get; init; }
    public string Route { get; init; } = string.Empty;
    public bool IsRequired { get; init; }
    public bool IsComplete { get; init; }
    public string PackageId { get; init; } = string.Empty;
    public string ModuleId { get; init; } = string.Empty;
}
