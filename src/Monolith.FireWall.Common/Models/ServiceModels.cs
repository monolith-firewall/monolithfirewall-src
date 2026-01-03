namespace Monolith.FireWall.Common.Models;

public record ServiceDefinition(
    string Name,
    string SystemdUnit,
    string[] RequiredCapabilities
);

public record AptDependency(
    string PackageName,
    string? MinVersion = null
);
