using Monolith.FireWall.Platform.Models;

namespace Monolith.FireWall.Core.Services.Platform;

public sealed class PlatformActionDefinition
{
    public PlatformActionDefinition(
        string name,
        PlatformCapability requiredCapability,
        bool isWrite,
        Func<PlatformRequest, CancellationToken, Task<PlatformHandlerResult>> handler)
    {
        Name = name;
        RequiredCapability = requiredCapability;
        IsWrite = isWrite;
        Handler = handler;
    }

    public string Name { get; }
    public PlatformCapability RequiredCapability { get; }
    public bool IsWrite { get; }
    public Func<PlatformRequest, CancellationToken, Task<PlatformHandlerResult>> Handler { get; }
}
