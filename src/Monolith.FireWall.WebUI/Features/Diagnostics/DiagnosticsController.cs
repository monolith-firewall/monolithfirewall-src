using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Monolith.FireWall.WebUI.Features.Diagnostics;

[ApiController]
[Route("api/diagnostics")]
public class DiagnosticsController : ControllerBase
{
    private readonly Services.CoreApiClient _coreClient;
    private readonly ILogger<DiagnosticsController> _logger;

    public DiagnosticsController(Services.CoreApiClient coreClient, ILogger<DiagnosticsController> logger)
    {
        _coreClient = coreClient;
        _logger = logger;
    }

    [HttpPost("ping")]
    public Task<ActionResult> Ping([FromBody] JsonElement payload)
        => ForwardAsync("platform.diagnostics.ping", payload);

    [HttpPost("traceroute")]
    public Task<ActionResult> Traceroute([FromBody] JsonElement payload)
        => ForwardAsync("platform.diagnostics.traceroute", payload);

    [HttpPost("mtr")]
    public Task<ActionResult> Mtr([FromBody] JsonElement payload)
        => ForwardAsync("platform.diagnostics.mtr", payload);

    private async Task<ActionResult> ForwardAsync(string action, JsonElement payload)
    {
        try
        {
            var coreRequest = new
            {
                action,
                payload
            };

            var timeoutMs = GetTimeoutMs(action, payload);
            var responseJson = await _coreClient.SendRequestAsync(JsonSerializer.Serialize(coreRequest), timeoutMs);
            return Content(responseJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Diagnostics action failed: {Action}", action);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    private static int GetTimeoutMs(string action, JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            return 15000;
        }

        return action switch
        {
            "platform.diagnostics.ping" => GetPingTimeout(payload),
            "platform.diagnostics.traceroute" => GetTracerouteTimeout(payload),
            "platform.diagnostics.mtr" => GetMtrTimeout(payload),
            _ => 15000
        };
    }

    private static int GetPingTimeout(JsonElement payload)
    {
        var count = GetInt(payload, "count", 4, 1, 20);
        var intervalMs = GetInt(payload, "intervalMs", 1000, 200, 5000);
        var timeoutMs = GetInt(payload, "timeoutMs", 3000, 500, 10000);
        return timeoutMs + (count * intervalMs) + 4000;
    }

    private static int GetTracerouteTimeout(JsonElement payload)
    {
        var isFast = GetBool(payload, "fast", false);
        var maxHops = GetInt(payload, "maxHops", isFast ? 20 : 30, 1, isFast ? 40 : 64);
        var waitMs = GetInt(payload, "waitMs", isFast ? 1000 : 3000, isFast ? 200 : 500, isFast ? 2000 : 10000);
        return (maxHops * waitMs) + 6000;
    }

    private static int GetMtrTimeout(JsonElement payload)
    {
        var count = GetInt(payload, "count", 10, 1, 50);
        var intervalMs = GetInt(payload, "intervalMs", 1000, 200, 5000);
        return (count * intervalMs) + 30000;
    }

    private static int GetInt(JsonElement payload, string name, int defaultValue, int min, int max)
    {
        if (!payload.TryGetProperty(name, out var value))
        {
            return defaultValue;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed))
        {
            return Math.Clamp(parsed, min, max);
        }

        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out parsed))
        {
            return Math.Clamp(parsed, min, max);
        }

        return defaultValue;
    }

    private static bool GetBool(JsonElement payload, string name, bool defaultValue)
    {
        if (!payload.TryGetProperty(name, out var value))
        {
            return defaultValue;
        }

        if (value.ValueKind == JsonValueKind.True)
        {
            return true;
        }

        if (value.ValueKind == JsonValueKind.False)
        {
            return false;
        }

        if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed))
        {
            return parsed;
        }

        return defaultValue;
    }
}
