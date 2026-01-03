using CodeLogic.Localization;

namespace Monolith.FireWall.Common.Interfaces;

/// <summary>
/// Context provided to packages during lifecycle.
/// </summary>
public interface IPackageContext
{
    /// <summary>Package ID</summary>
    string PackageId { get; }

    /// <summary>Logger instance</summary>
    ILogger Logger { get; }

    /// <summary>Localization manager for this package</summary>
    ILocalizationManager Localization { get; }
}
