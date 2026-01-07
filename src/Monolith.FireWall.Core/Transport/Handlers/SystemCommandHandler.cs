using System.Text.Json;
using Monolith.FireWall.Common.Models;
using Monolith.FireWall.Core.Services.Platform;
using Monolith.FireWall.Platform.Models;

namespace Monolith.FireWall.Core.Transport.Handlers;

public sealed class SystemCommandHandler : ICoreRequestHandler
{
    private static readonly HashSet<string> AllowedCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "ping",
        "traceroute",
        "tracepath"
    };

    private static readonly HashSet<string> Actions = new(StringComparer.OrdinalIgnoreCase)
    {
        "system.command.run"
    };

    public bool CanHandle(string action) => Actions.Contains(action);

    public async Task<ApiResponse> HandleAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        var action = request.GetProperty("action").GetString() ?? string.Empty;

        switch (action)
        {
            case "system.command.run":
                if (!request.TryGetProperty("payload", out var payloadEl))
                {
                    return new ApiResponse(false, null, "Payload is required");
                }

                var commandName = payloadEl.TryGetProperty("command", out var cmdEl) ? cmdEl.GetString() : null;
                var argsArray = payloadEl.TryGetProperty("args", out var argsEl) && argsEl.ValueKind == JsonValueKind.Array
                    ? argsEl.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToArray()
                    : Array.Empty<string>();

                if (string.IsNullOrWhiteSpace(commandName))
                {
                    return new ApiResponse(false, null, "Command is required");
                }

                if (!AllowedCommands.Contains(commandName))
                {
                    return new ApiResponse(false, null, $"Command '{commandName}' is not allowed. Allowed commands: {string.Join(", ", AllowedCommands)}");
                }

                // Build arguments string - escape spaces and special characters
                var argsString = string.Join(" ", argsArray.Select(arg => 
                {
                    // If argument contains spaces or special chars, quote it
                    if (arg.Contains(' ') || arg.Contains('"') || arg.Contains('\\'))
                    {
                        return $"\"{arg.Replace("\"", "\\\"")}\"";
                    }
                    return arg;
                }));

                var command = new PlatformCommand
                {
                    FileName = commandName,
                    Arguments = argsString,
                    UseSudo = false,
                    TimeoutMs = 30000
                };

                var result = await context.CommandRunner.RunAsync(command, cancellationToken);

                return new ApiResponse(
                    result.ExitCode == 0,
                    new
                    {
                        ExitCode = result.ExitCode,
                        StdOut = result.StdOut ?? string.Empty,
                        StdErr = result.StdErr ?? string.Empty,
                        TimedOut = result.TimedOut
                    },
                    result.ExitCode != 0 ? (result.StdErr ?? $"Command exited with code {result.ExitCode}") : null);
        }

        return new ApiResponse(false, null, $"Unhandled action: {action}");
    }
}
