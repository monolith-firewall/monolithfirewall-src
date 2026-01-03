using System.Text.Json;

namespace Monolith.FireWall.Core.Transport.Handlers;

public static class CoreRequestParsing
{
    public static bool TryGetPayload<T>(JsonElement request, out T payload, out string? error)
    {
        payload = default!;
        error = null;

        if (!request.TryGetProperty("payload", out var payloadEl))
        {
            error = "Payload is required";
            return false;
        }

        try
        {
            var value = JsonSerializer.Deserialize<T>(payloadEl.GetRawText(), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            if (value == null)
            {
                error = "Invalid payload";
                return false;
            }

            payload = value;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
