namespace Monolith.FireWall.WebUI.Middleware;

/// <summary>
/// Middleware to add no-cache headers to all responses
/// </summary>
public class NoCacheMiddleware
{
    private readonly RequestDelegate _next;

    public NoCacheMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Add no-cache headers
        context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
        context.Response.Headers.Append("Pragma", "no-cache");
        context.Response.Headers.Append("Expires", "0");

        await _next(context);
    }
}

public static class NoCacheMiddlewareExtensions
{
    public static IApplicationBuilder UseNoCache(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<NoCacheMiddleware>();
    }
}
