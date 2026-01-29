using CodeLogic.Localization;

namespace Monolith.FireWall.Common.Interfaces;

/// <summary>
/// Package lifecycle hooks.
/// </summary>
public interface IMonolithPackage
{
    /// <summary>Register localization models for this package</summary>
    void RegisterLocalizations(ILocalizationManager localization);

    /// <summary>Called when package is loaded by Core</summary>
    Task OnLoadAsync(IPackageContext context);

    /// <summary>Called when package is unloaded</summary>
    Task OnUnloadAsync(IPackageContext context);

    /// <summary>Called when package is installed (first time or update). Can check existing config and initialize database.</summary>
    Task OnInstallAsync(IPackageContext context, bool isUpdate, CancellationToken cancellationToken);
}
