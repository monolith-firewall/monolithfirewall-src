using System.Text.Json;
using Monolith.FireWall.Platform.Models;

namespace Monolith.FireWall.Platform;

public interface IPlatformClient
{
    Task<PlatformResponse> ExecuteAsync(
        string action,
        object? payload,
        PlatformContext context,
        CancellationToken cancellationToken = default);
}

public sealed class PlatformClient : IPlatformClient
{
    private readonly Func<PlatformRequest, CancellationToken, Task<PlatformResponse>> _handler;

    public PlatformClient(Func<PlatformRequest, CancellationToken, Task<PlatformResponse>> handler)
    {
        _handler = handler;
    }

    public Task<PlatformResponse> ExecuteAsync(
        string action,
        object? payload,
        PlatformContext context,
        CancellationToken cancellationToken = default)
    {
        var request = new PlatformRequest
        {
            Action = action,
            Context = context,
            Payload = payload == null ? default : JsonSerializer.SerializeToElement(payload)
        };

        return _handler(request, cancellationToken);
    }
}
