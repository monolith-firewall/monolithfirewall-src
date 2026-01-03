using System.Text.Json;

namespace Monolith.FireWall.Platform.Models;

[Flags]
public enum PlatformCapability
{
    None = 0,
    SystemRead = 1 << 0,
    SystemWrite = 1 << 1,
    NetworkRead = 1 << 2,
    NetworkWrite = 1 << 3,
    FilesystemRead = 1 << 4,
    FilesystemWrite = 1 << 5
}

public enum PlatformErrorCode
{
    ValidationError,
    PermissionDenied,
    CommandFailed,
    NotSupported,
    Timeout,
    NotFound
}

public sealed class PlatformError
{
    public PlatformErrorCode Code { get; set; }
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, object>? Details { get; set; }
}

public sealed class PlatformDiagnostics
{
    public int DurationMs { get; set; }
    public string? CommandId { get; set; }
}

public sealed class PlatformContext
{
    public string? CorrelationId { get; set; }
    public string? PackageId { get; set; }
    public string? ModuleId { get; set; }
    public int? UserId { get; set; }
    public string[] Permissions { get; set; } = Array.Empty<string>();
    public PlatformCapability Capabilities { get; set; } = PlatformCapability.None;

    public bool HasCapability(PlatformCapability required)
    {
        if (required == PlatformCapability.None)
        {
            return true;
        }

        return (Capabilities & required) == required;
    }

    public static PlatformContext FromJsonElement(JsonElement element)
    {
        var context = new PlatformContext();
        if (element.ValueKind != JsonValueKind.Object)
        {
            return context;
        }

        if (element.TryGetProperty("correlationId", out var correlationId))
        {
            context.CorrelationId = correlationId.GetString();
        }

        if (element.TryGetProperty("packageId", out var packageId))
        {
            context.PackageId = packageId.GetString();
        }

        if (element.TryGetProperty("moduleId", out var moduleId))
        {
            context.ModuleId = moduleId.GetString();
        }

        if (element.TryGetProperty("userId", out var userId) && userId.ValueKind == JsonValueKind.Number)
        {
            context.UserId = userId.GetInt32();
        }

        if (element.TryGetProperty("permissions", out var permissions) && permissions.ValueKind == JsonValueKind.Array)
        {
            var list = new List<string>();
            foreach (var item in permissions.EnumerateArray())
            {
                var value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    list.Add(value);
                }
            }
            context.Permissions = list.ToArray();
        }

        if (element.TryGetProperty("capabilities", out var capabilities) && capabilities.ValueKind == JsonValueKind.Array)
        {
            PlatformCapability caps = PlatformCapability.None;
            foreach (var item in capabilities.EnumerateArray())
            {
                var value = item.GetString();
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (Enum.TryParse<PlatformCapability>(value, true, out var parsed))
                {
                    caps |= parsed;
                }
            }
            context.Capabilities = caps;
        }

        return context;
    }
}

public sealed class PlatformRequest
{
    public string Action { get; set; } = string.Empty;
    public JsonElement Payload { get; set; }
    public PlatformContext Context { get; set; } = new();

    public static PlatformRequest FromJsonElement(JsonElement element)
    {
        var request = new PlatformRequest();
        if (element.ValueKind != JsonValueKind.Object)
        {
            return request;
        }

        if (element.TryGetProperty("action", out var action))
        {
            request.Action = action.GetString() ?? string.Empty;
        }

        if (element.TryGetProperty("payload", out var payload))
        {
            request.Payload = payload;
        }

        if (element.TryGetProperty("context", out var context))
        {
            request.Context = PlatformContext.FromJsonElement(context);
        }
        else
        {
            // Allow fallback context fields at root for internal calls
            request.Context = PlatformContext.FromJsonElement(element);
        }

        return request;
    }
}

public sealed class PlatformResponse
{
    public bool Success { get; set; }
    public object? Data { get; set; }
    public PlatformError? Error { get; set; }
    public PlatformDiagnostics? Diagnostics { get; set; }

    public static PlatformResponse Ok(object? data, PlatformDiagnostics? diagnostics = null)
    {
        return new PlatformResponse { Success = true, Data = data, Diagnostics = diagnostics };
    }

    public static PlatformResponse Fail(PlatformError error, PlatformDiagnostics? diagnostics = null)
    {
        return new PlatformResponse { Success = false, Error = error, Diagnostics = diagnostics };
    }
}

public sealed class PlatformCommandResult
{
    public string Command { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public bool UsedSudo { get; set; }
    public int ExitCode { get; set; }
    public bool TimedOut { get; set; }
    public int DurationMs { get; set; }
    public string StdOut { get; set; } = string.Empty;
    public string StdErr { get; set; } = string.Empty;
}

public sealed class PlatformHandlerResult
{
    public object? Data { get; set; }
    public PlatformCommandResult? CommandResult { get; set; }
}
