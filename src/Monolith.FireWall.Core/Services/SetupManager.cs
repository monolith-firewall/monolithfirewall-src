using System.Text.Json;
using Monolith.FireWall.Common.Interfaces;
using Monolith.FireWall.Common.Models;
using Monolith.FireWall.Core.Services;

namespace Monolith.FireWall.Core.Services;

/// <summary>
/// Manages setup state and progress
/// </summary>
public class SetupManager
{
    private const string SetupCompleteFlag = "/var/lib/monolith-firewall/.setup-complete";
    private const string SetupProgressFile = "/var/lib/monolith-firewall/.setup-progress.json";
    private readonly ILogger _logger;
    private readonly ModuleRegistry _moduleRegistry;
    private readonly SetupStateStore _setupStateStore;

    public SetupManager(ILogger logger, ModuleRegistry moduleRegistry)
    {
        _logger = logger;
        _moduleRegistry = moduleRegistry;
        _setupStateStore = new SetupStateStore();
    }

    /// <summary>
    /// Check if setup is needed
    /// </summary>
    public bool NeedsSetup()
    {
        // First check database marker - most reliable indicator
        try
        {
            var setupState = _setupStateStore.GetSetupStateAsync().GetAwaiter().GetResult();
            if (setupState != null)
            {
                // If setup is marked as completed in database, no setup needed
                if (setupState.SetupCompleted)
                {
                    return false;
                }
                
                // If marked as fresh install, definitely needs setup
                if (setupState.IsFreshInstall)
                {
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to check setup state from database: {ex.Message}");
            // Fall through to file-based checks
        }

        // Fallback: Check if setup completion flag exists
        if (File.Exists(SetupCompleteFlag))
        {
            return false;
        }

        // Check if this is a first run (CodeLogic first run)
        var codeLogicFirstRun = !File.Exists("/var/lib/monolith-firewall/codelogic/.codelogic");
        
        // Check if network package is installed (should be on fresh install)
        var hasNetworkPackage = _moduleRegistry.GetAllPackages()
            .Any(p => p.Definition.Id == "monolith-network");

        // If first run or no network package, needs setup
        return codeLogicFirstRun || !hasNetworkPackage;
    }

    /// <summary>
    /// Get setup status
    /// </summary>
    public SetupStatusResponse GetSetupStatus()
    {
        var progress = LoadProgress();
        var completedSteps = progress.CompletedSteps ?? new List<string>();
        var allSteps = GetAllSteps();
        var pendingSteps = allSteps.Except(completedSteps).ToList();

        var totalSteps = allSteps.Count;
        var progressPercent = totalSteps > 0 ? (completedSteps.Count * 100) / totalSteps : 0;

        return new SetupStatusResponse
        {
            NeedsSetup = NeedsSetup(),
            IsFirstRun = !File.Exists("/var/lib/monolith-firewall/codelogic/.codelogic"),
            CompletedSteps = completedSteps,
            PendingSteps = pendingSteps,
            TotalSteps = totalSteps,
            Progress = progressPercent
        };
    }

    /// <summary>
    /// Get all setup steps
    /// </summary>
    private List<string> GetAllSteps()
    {
        var steps = new List<string> { "router", "network" };

        // Add package setup steps
        var packageSteps = GetPackageSetupPages()
            .SelectMany(p => p.SetupPages)
            .Select(p => $"package:{p.PackageId}:{p.Id}")
            .ToList();

        steps.AddRange(packageSteps);
        return steps;
    }

    /// <summary>
    /// Complete a setup step
    /// </summary>
    public void CompleteStep(string stepId, Dictionary<string, object>? data = null)
    {
        var progress = LoadProgress();
        if (!progress.CompletedSteps.Contains(stepId))
        {
            progress.CompletedSteps.Add(stepId);
            SaveProgress(progress);
            _logger.LogInformation($"Setup step completed: {stepId}");
        }
    }

    /// <summary>
    /// Finish setup wizard (legacy sync method for compatibility)
    /// </summary>
    public void FinishSetup(bool skipRemaining = false)
    {
        FinishSetupAsync(skipRemaining).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Finish setup wizard
    /// </summary>
    public async Task FinishSetupAsync(bool skipRemaining = false)
    {
        if (skipRemaining)
        {
            // Mark all steps as complete
            var progress = LoadProgress();
            progress.CompletedSteps = GetAllSteps();
            SaveProgress(progress);
        }

        // Create setup complete flag
        File.WriteAllText(SetupCompleteFlag, DateTime.UtcNow.ToString("O"));
        
        // Mark as completed in database
        await _setupStateStore.MarkSetupCompletedAsync();
        
        _logger.LogInformation("Setup wizard completed");
    }

    /// <summary>
    /// Skip setup wizard
    /// </summary>
    public async Task SkipSetupAsync()
    {
        // Mark all optional steps as skipped
        var progress = LoadProgress();
        var allSteps = GetAllSteps();
        var optionalSteps = allSteps.Except(new[] { "router" }); // Router is required, but we'll skip it anyway
        progress.CompletedSteps = optionalSteps.ToList();
        SaveProgress(progress);

        // Create setup complete flag
        File.WriteAllText(SetupCompleteFlag, DateTime.UtcNow.ToString("O"));
        
        // Mark as skipped in database
        await _setupStateStore.MarkSetupSkippedAsync();
        
        _logger.LogInformation("Setup wizard skipped");
    }

    /// <summary>
    /// Get packages with setup wizard pages
    /// </summary>
    public List<PackageSetupInfo> GetPackageSetupPages()
    {
        var packages = new Dictionary<string, PackageSetupInfo>();

        // Get all registered packages
        var registeredPackages = _moduleRegistry.GetAllPackages();
        foreach (var packageInfo in registeredPackages)
        {
            try
            {
                // Get modules from the package via ModuleRegistry
                var modules = _moduleRegistry.GetAllModules()
                    .Where(m => m.Package.Definition.Id == packageInfo.Definition.Id)
                    .Select(m => m.Module);

                foreach (var module in modules)
                {
                    try
                    {
                        var setupPages = module.GetSetupWizardPages();
                        foreach (var page in setupPages)
                        {
                            if (!packages.ContainsKey(packageInfo.Definition.Id))
                            {
                                packages[packageInfo.Definition.Id] = new PackageSetupInfo
                                {
                                    PackageId = packageInfo.Definition.Id,
                                    PackageName = packageInfo.Definition.Name
                                };
                            }

                            var progress = LoadProgress();
                            var stepId = $"package:{packageInfo.Definition.Id}:{page.Id}";
                            var isComplete = progress.CompletedSteps.Contains(stepId);

                            packages[packageInfo.Definition.Id].SetupPages.Add(new SetupWizardPageInfo
                            {
                                Id = page.Id,
                                Title = page.Title,
                                Description = page.Description,
                                Route = page.Route,
                                Order = page.Order,
                                IsRequired = page.IsRequired,
                                IsComplete = isComplete || page.IsComplete,
                                PackageId = packageInfo.Definition.Id,
                                ModuleId = module.Id
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error getting setup pages from module {module.Id}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting setup pages from package {packageInfo.Definition.Id}");
            }
        }

        // Sort pages by order
        foreach (var package in packages.Values)
        {
            package.SetupPages = package.SetupPages.OrderBy(p => p.Order).ToList();
        }

        return packages.Values.OrderBy(p => p.PackageId).ToList();
    }

    /// <summary>
    /// Load setup progress
    /// </summary>
    private SetupProgress LoadProgress()
    {
        if (File.Exists(SetupProgressFile))
        {
            try
            {
                var json = File.ReadAllText(SetupProgressFile);
                return JsonSerializer.Deserialize<SetupProgress>(json) ?? new SetupProgress();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading setup progress");
            }
        }

        return new SetupProgress();
    }

    /// <summary>
    /// Save setup progress
    /// </summary>
    private void SaveProgress(SetupProgress progress)
    {
        try
        {
            var json = JsonSerializer.Serialize(progress, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SetupProgressFile, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving setup progress");
        }
    }

    private class SetupProgress
    {
        public List<string> CompletedSteps { get; set; } = new();
    }
}
