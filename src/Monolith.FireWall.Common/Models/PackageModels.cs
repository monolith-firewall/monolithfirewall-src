namespace Monolith.FireWall.Common.Models;

public record PackageManifest(
    string Id,
    string Name,
    string Version,
    string Description,
    string Author,
    string? Homepage = null,
    string? License = null,
    string[]? Dependencies = null,
    string[]? AptDependencies = null,
    BundledDebInfo[]? BundledDebs = null,
    string? MinCoreVersion = null,
    string? MaxCoreVersion = null,
    bool RequiresRestart = false,
    FirewallIntentDefinition[]? FirewallIntents = null
);

/// <summary>
/// Information about a bundled .deb package included in the monolith package.
/// </summary>
public record BundledDebInfo(
    string FileName,
    string PackageName,
    string Version,
    string Architecture,
    string[]? Dependencies = null
);

public record FirewallIntentDefinition(
    string? ModuleId = null,
    string? Interface = null,
    string? InterfaceRole = null,
    string? Direction = null,
    string? Action = null,
    string? AddressFamily = null,
    string? Protocol = null,
    string? SourceType = null,
    string? SourceValue = null,
    string? SourcePort = null,
    string? DestinationType = null,
    string? DestinationValue = null,
    string? DestinationPort = null,
    string? Description = null,
    bool Enabled = true
);
