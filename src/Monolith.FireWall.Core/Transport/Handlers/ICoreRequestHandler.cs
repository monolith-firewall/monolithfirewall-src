using System.Text.Json;
using Monolith.FireWall.Common.Models;

namespace Monolith.FireWall.Core.Transport.Handlers;

public interface ICoreRequestHandler
{
    bool CanHandle(string action);
    Task<ApiResponse> HandleAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken);
}
