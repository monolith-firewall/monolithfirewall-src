namespace Monolith.FireWall.Common.Models;

public record ServiceDefinition(
    string Name,
    string SystemdUnit,
    string[] RequiredCapabilities
);

public record ServiceBindingDefinition(
    int Port,
    string Protocol,
    string InterfaceRole,
    string AddressFamily,
    string? Description = null
);

public record AptDependency(
    string PackageName,
    string? MinVersion = null
);
