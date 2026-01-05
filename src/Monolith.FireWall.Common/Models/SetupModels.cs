namespace Monolith.FireWall.Common.Models;

/// <summary>
/// Setup status response
/// </summary>
public class SetupStatusResponse
{
    public bool NeedsSetup { get; set; }
    public bool IsFirstRun { get; set; }
    public List<string> CompletedSteps { get; set; } = new();
    public List<string> PendingSteps { get; set; } = new();
    public int TotalSteps { get; set; }
    public int Progress { get; set; } // 0-100
}

/// <summary>
/// Package setup information
/// </summary>
public class PackageSetupInfo
{
    public string PackageId { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public List<SetupWizardPageInfo> SetupPages { get; set; } = new();
}

/// <summary>
/// Setup wizard page information
/// </summary>
public class SetupWizardPageInfo
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public int Order { get; set; }
    public bool IsRequired { get; set; }
    public bool IsComplete { get; set; }
    public string PackageId { get; set; } = string.Empty;
    public string ModuleId { get; set; } = string.Empty;
}

/// <summary>
/// Complete step request
/// </summary>
public class CompleteStepRequest
{
    public string StepId { get; set; } = string.Empty;
    public Dictionary<string, object>? Data { get; set; }
}

/// <summary>
/// Finish setup request
/// </summary>
public class FinishSetupRequest
{
    public bool SkipRemaining { get; set; }
}
