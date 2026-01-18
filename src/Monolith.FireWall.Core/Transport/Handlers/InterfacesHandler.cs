using System.Text.Json;
using Monolith.FireWall.Common.Models;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Transport.Handlers;

public sealed class InterfacesHandler : ICoreRequestHandler
{
    private static readonly HashSet<string> Actions = new(StringComparer.OrdinalIgnoreCase)
    {
        "interfaces.assignments.list",
        "interfaces.vlans.list",
        "interfaces.bridges.list",
        "interfaces.available.list",
        "interfaces.assignments.save",
        "interfaces.assignments.delete",
        "interfaces.unmanaged.delete",
        "interfaces.unmanaged.assign",
        "interfaces.config.check",
        "interfaces.config.apply",
        "interfaces.config.apply-now",
        "interfaces.config.fix"
    };

    public bool CanHandle(string action) => Actions.Contains(action);

    public async Task<ApiResponse> HandleAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        var action = request.GetProperty("action").GetString() ?? string.Empty;

        switch (action)
        {
            case "interfaces.assignments.list":
                var snapshot = await context.InterfaceAssignments.GetSnapshotAsync(cancellationToken);
                return new ApiResponse(true, snapshot, null);

            case "interfaces.vlans.list":
                var vlanSnapshot = await context.InterfaceAssignments.GetSnapshotAsync(cancellationToken);
                return new ApiResponse(true, vlanSnapshot.Vlans, null);

            case "interfaces.bridges.list":
                var bridgeSnapshot = await context.InterfaceAssignments.GetSnapshotAsync(cancellationToken);
                return new ApiResponse(true, bridgeSnapshot.Bridges, null);

            case "interfaces.available.list":
                var availableSnapshot = await context.InterfaceAssignments.GetSnapshotAsync(cancellationToken);
                return new ApiResponse(true, availableSnapshot.Unassigned, null);

            case "interfaces.assignments.save":
                if (!CoreRequestParsing.TryGetPayload(request, out InterfaceAssignmentRequest assignmentRequest, out var assignmentError))
                {
                    return new ApiResponse(false, null, assignmentError);
                }

                var saveResult = await context.InterfaceAssignments.SaveAssignmentAsync(assignmentRequest, cancellationToken);
                return saveResult.Success
                    ? new ApiResponse(true, saveResult.Assignment, null)
                    : new ApiResponse(false, null, saveResult.Error);

            case "interfaces.assignments.delete":
                if (!CoreRequestParsing.TryGetPayload(request, out InterfaceAssignmentDeleteRequest deleteRequest, out var deleteError))
                {
                    return new ApiResponse(false, null, deleteError);
                }

                var deleteResult = await context.InterfaceAssignments.DeleteAssignmentAsync(deleteRequest.Interface, cancellationToken);
                return deleteResult.Success
                    ? new ApiResponse(true, new { interfaceName = deleteRequest.Interface }, null)
                    : new ApiResponse(false, null, deleteResult.Error ?? "Failed to delete assignment");

            case "interfaces.config.check":
                var checkResult = await context.InterfaceAssignments.CheckConfigAsync(cancellationToken);
                return new ApiResponse(true, checkResult, null);

            case "interfaces.config.apply":
                var applyResult = await context.InterfaceAssignments.ApplyConfigAsync(cancellationToken);
                return new ApiResponse(applyResult.Success, applyResult, applyResult.Success ? null : applyResult.Message);

            case "interfaces.config.apply-now":
                var applyNowResult = await context.InterfaceAssignments.ApplyNowAsync(cancellationToken);
                return new ApiResponse(applyNowResult.Success, applyNowResult, applyNowResult.Success ? null : applyNowResult.Message);

            case "interfaces.config.fix":
                var fixResult = await context.InterfaceAssignments.FixConfigAsync(cancellationToken);
                return new ApiResponse(fixResult.Success, fixResult, fixResult.Success ? null : fixResult.Message);

            case "interfaces.unmanaged.delete":
                if (!CoreRequestParsing.TryGetPayload(request, out InterfaceAssignmentDeleteRequest deleteUnmanagedRequest, out var deleteUnmanagedError))
                {
                    return new ApiResponse(false, null, deleteUnmanagedError);
                }

                var deleteUnmanagedResult = await context.InterfaceAssignments.DeleteUnmanagedInterfaceAsync(deleteUnmanagedRequest.Interface, cancellationToken);
                return deleteUnmanagedResult.Success
                    ? new ApiResponse(true, new { interfaceName = deleteUnmanagedRequest.Interface }, null)
                    : new ApiResponse(false, null, deleteUnmanagedResult.Error ?? "Failed to delete unmanaged interface");

            case "interfaces.unmanaged.assign":
                if (!CoreRequestParsing.TryGetPayload(request, out InterfaceAssignmentDeleteRequest assignUnmanagedRequest, out var assignUnmanagedError))
                {
                    return new ApiResponse(false, null, assignUnmanagedError);
                }

                var assignUnmanagedResult = await context.InterfaceAssignments.AssignUnmanagedInterfaceAsync(assignUnmanagedRequest.Interface, cancellationToken);
                return assignUnmanagedResult.Success
                    ? new ApiResponse(true, assignUnmanagedResult.Assignment, null)
                    : new ApiResponse(false, null, assignUnmanagedResult.Error ?? "Failed to assign unmanaged interface");
        }

        return new ApiResponse(false, null, $"Unhandled action: {action}");
    }
}
