using System.Text.Json;
using Monolith.FireWall.Common.Models;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Transport.Handlers;

public sealed class PackagesHandler : ICoreRequestHandler
{
    private static readonly HashSet<string> Actions = new(StringComparer.OrdinalIgnoreCase)
    {
        "packages.install",
        "packages.uninstall",
        "packages.list"
    };

    public bool CanHandle(string action) => Actions.Contains(action);

    public async Task<ApiResponse> HandleAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        var action = request.GetProperty("action").GetString() ?? string.Empty;

        switch (action)
        {
            case "packages.install":
                if (!CoreRequestParsing.TryGetPayload(request, out PackageInstallRequest installRequest, out var installError))
                {
                    return new ApiResponse(false, null, installError);
                }

                var installResult = await context.PackageInstaller.InstallAsync(
                    installRequest.SourcePath,
                    installRequest.Overwrite,
                    installRequest.PackageId,
                    cancellationToken);

                if (installResult.Success)
                {
                    context.PackageInstaller.ScheduleRestartIfNeeded(
                        installRequest.RestartServices,
                        installResult.RequiresRestart,
                        installRequest.PackageId);
                }

                return installResult.Success
                    ? new ApiResponse(true, new
                    {
                        packageId = installRequest.PackageId,
                        version = installResult.Manifest?.Version,
                        requiresRestart = installResult.RequiresRestart,
                        isUpdate = installResult.IsUpdate
                    }, null)
                    : new ApiResponse(false, null, installResult.Error);

            case "packages.uninstall":
                if (!CoreRequestParsing.TryGetPayload(request, out PackageUninstallRequest uninstallRequest, out var uninstallError))
                {
                    return new ApiResponse(false, null, uninstallError);
                }

                var uninstallResult = await context.PackageInstaller.RemoveAsync(
                    uninstallRequest.PackageId,
                    cancellationToken);
                return uninstallResult.Success
                    ? new ApiResponse(true, new { packageId = uninstallRequest.PackageId }, null)
                    : new ApiResponse(false, null, uninstallResult.Error);

            case "packages.list":
                var packages = await context.PackageStateStore.GetPackagesAsync();
                return new ApiResponse(true, packages, null);
        }

        return new ApiResponse(false, null, $"Unhandled action: {action}");
    }
}
