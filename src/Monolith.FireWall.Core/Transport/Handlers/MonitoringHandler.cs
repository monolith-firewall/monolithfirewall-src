using System.Text.Json;
using Monolith.FireWall.Common.Models;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Transport.Handlers;

public sealed class MonitoringHandler : ICoreRequestHandler
{
    private static readonly HashSet<string> Actions = new(StringComparer.OrdinalIgnoreCase)
    {
        "monitoring.status.list",
        "monitoring.monitor.update",
        "monitoring.notifications.list",
        "monitoring.notifications.read"
    };

    public bool CanHandle(string action) => Actions.Contains(action);

    public async Task<ApiResponse> HandleAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        var action = request.GetProperty("action").GetString() ?? string.Empty;

        switch (action)
        {
            case "monitoring.status.list":
                var monitorStatus = await context.MonitoringManager.GetMonitorStatusAsync();
                return new ApiResponse(true, monitorStatus, null);

            case "monitoring.monitor.update":
                if (!CoreRequestParsing.TryGetPayload(request, out MonitorUpdateRequest updateRequest, out var updateError))
                {
                    return new ApiResponse(false, null, updateError);
                }

                var updateResult = await context.MonitoringManager.UpdateMonitorAsync(updateRequest);
                return updateResult.Success
                    ? new ApiResponse(true, new { key = updateRequest.Key }, null)
                    : new ApiResponse(false, null, updateResult.Error ?? "Failed to update monitor");

            case "monitoring.notifications.list":
                NotificationQuery notificationQuery;
                if (request.TryGetProperty("payload", out var notificationPayload))
                {
                    try
                    {
                        notificationQuery = JsonSerializer.Deserialize<NotificationQuery>(notificationPayload.GetRawText(), new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        }) ?? new NotificationQuery();
                    }
                    catch (Exception ex)
                    {
                        return new ApiResponse(false, null, ex.Message);
                    }
                }
                else
                {
                    notificationQuery = new NotificationQuery();
                }

                var notificationSummary = await context.MonitoringManager.GetNotificationsAsync(notificationQuery);
                return new ApiResponse(true, notificationSummary, null);

            case "monitoring.notifications.read":
                if (!CoreRequestParsing.TryGetPayload(request, out NotificationReadRequest readRequest, out var readError))
                {
                    return new ApiResponse(false, null, readError);
                }

                var readResult = await context.MonitoringManager.MarkNotificationsReadAsync(readRequest);
                return readResult.Success
                    ? new ApiResponse(true, new { read = true }, null)
                    : new ApiResponse(false, null, readResult.Error ?? "Failed to mark notifications read");
        }

        return new ApiResponse(false, null, $"Unhandled action: {action}");
    }
}
