namespace Monolith.FireWall.Common.Interfaces;

/// <summary>
/// Defines package metadata and module registration.
/// </summary>
public interface IMonolithPackageDefinition
{
    /// <summary>Package ID (e.g., "monolith-system")</summary>
    string Id { get; }

    /// <summary>Display name</summary>
    string Name { get; }

    /// <summary>Semantic version</summary>
    string Version { get; }

    /// <summary>Package description</summary>
    string Description { get; }

    /// <summary>Author name</summary>
    string Author { get; }

    /// <summary>Package dependencies (other package IDs)</summary>
    string[] Dependencies { get; }

    /// <summary>Get all modules provided by this package</summary>
    IEnumerable<IMonolithModule> GetModules();
}
