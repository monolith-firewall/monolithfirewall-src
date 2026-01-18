using System.Text.Json;
using Monolith.FireWall.WebUI.Services;

namespace Monolith.FireWall.WebUI.Middleware;

/// <summary>
/// Middleware that redirects to setup wizard if setup is needed and user is not already on setup pages
/// </summary>
public class SetupRedirectMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SetupRedirectMiddleware> _logger;

    public SetupRedirectMiddleware(RequestDelegate next, ILogger<SetupRedirectMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, CoreApiClient coreClient)
    {
        // Skip redirect check for certain paths
        if (ShouldSkipRedirect(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Check if setup is needed
        try
        {
            var needsSetup = await CheckSetupNeededAsync(coreClient);
            if (needsSetup)
            {
                _logger.LogInformation("Setup needed, redirecting to /setup");
                context.Response.Redirect("/setup");
                return;
            }
        }
        catch (Exception ex)
        {
            // On error, check if this looks like a first run
            // If Core is unavailable, assume setup is needed (safer for first-run)
            _logger.LogWarning(ex, "Failed to check setup status via Core API");
            
            // Check for first-run indicators
            var isFirstRun = !File.Exists("/var/lib/monolith-firewall/codelogic/.codelogic") ||
                            !File.Exists("/var/lib/monolith-firewall/.setup-complete");
            
            if (isFirstRun)
            {
                _logger.LogInformation("First-run detected, redirecting to /setup");
                context.Response.Redirect("/setup");
                return;
            }
            
            // If not first run and Core is unavailable, allow request to continue
            _logger.LogWarning("Core unavailable but not first-run, allowing request to continue");
        }

        await _next(context);
    }

    private bool ShouldSkipRedirect(PathString path)
    {
        var pathValue = path.Value?.ToLowerInvariant() ?? "";

        // Skip for setup pages
        if (pathValue.StartsWith("/setup"))
            return true;

        // Skip for login page
        if (pathValue.StartsWith("/login"))
            return true;

        // Skip for API endpoints
        if (pathValue.StartsWith("/api"))
            return true;

        // Skip for static files
        if (pathValue.StartsWith("/css") || 
            pathValue.StartsWith("/js") || 
            pathValue.StartsWith("/lib") ||
            pathValue.StartsWith("/images") ||
            pathValue.StartsWith("/fonts") ||
            pathValue.StartsWith("/favicon"))
            return true;

        // Skip for error pages
        if (pathValue.StartsWith("/error"))
            return true;

        return false;
    }

    private async Task<bool> CheckSetupNeededAsync(CoreApiClient coreClient)
    {
        try
        {
            var request = JsonSerializer.Serialize(new { action = "setup.status" });
            var responseJson = await coreClient.SendRequestAsync(request);
            var response = JsonSerializer.Deserialize<JsonElement>(responseJson);

            if (response.TryGetProperty("Success", out var success) && success.GetBoolean())
            {
                if (response.TryGetProperty("Data", out var data))
                {
                    if (data.TryGetProperty("NeedsSetup", out var needsSetup))
                    {
                        return needsSetup.GetBoolean();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking setup status");
            // On error, assume setup is needed (safer default)
            return true;
        }

        // Default to needing setup if we can't determine (safer for first-run)
        return true;
    }
}
