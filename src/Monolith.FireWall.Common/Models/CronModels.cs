using Monolith.FireWall.Common.Interfaces;

namespace Monolith.FireWall.Common.Models;

public record CronJobDefinition(
    string Id,
    string Name,
    string CronExpression,
    Func<IModuleContext, CancellationToken, Task> Handler,
    bool Enabled = true,
    int TimeoutSeconds = 600,
    int MaxFailuresBeforeDisable = 5
);
