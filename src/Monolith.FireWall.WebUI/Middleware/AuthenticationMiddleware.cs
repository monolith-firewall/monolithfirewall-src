using System.Text.Json;
using Monolith.FireWall.Common.Models;

namespace Monolith.FireWall.WebUI.Middleware;

public class AuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private const string SessionCookieName = "monolith-session";
    private const string SessionKey = "user";

    public AuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip authentication for public endpoints
        var path = context.Request.Path.Value?.ToLower() ?? "";
        
        // Core metadata endpoints are public
        if (path.StartsWith("/api/core"))
        {
            await _next(context);
            return;
        }
        
        // Dashboard widgets endpoints are public (for initial load)
        if (path.StartsWith("/api/dashboard/widgets") || path.StartsWith("/api/dashboard/widget/"))
        {
            await _next(context);
            return;
        }
        
        if (path == "/" ||
            path.StartsWith("/index.html") ||
            path.StartsWith("/login") ||
            path.StartsWith("/setup") ||
            path.StartsWith("/api/auth/login") ||
            path.StartsWith("/api/cms/") ||
            path.StartsWith("/api/ui/") ||
            path.StartsWith("/css/") ||
            path.StartsWith("/js/") ||
            path.StartsWith("/assets/") ||
            path.StartsWith("/_content/") ||
            path.StartsWith("/pages/") ||
            path.StartsWith("/favicon"))
        {
            await _next(context);
            return;
        }

        // Check for session cookie
        if (context.Request.Cookies.TryGetValue(SessionCookieName, out var sessionToken))
        {
            // TODO: Validate session token with Core service
            // For now, we'll check if it's a valid JSON user object
            try
            {
                var userJson = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(sessionToken));
                var user = JsonSerializer.Deserialize<UserContext>(userJson);
                if (user != null)
                {
                    context.Items[SessionKey] = user;
                }
            }
            catch
            {
                // Invalid session, clear cookie
                context.Response.Cookies.Delete(SessionCookieName);
            }
        }

        await _next(context);
    }

    public static void SetUserSession(HttpContext context, UserContext user)
    {
        var userJson = JsonSerializer.Serialize(user);
        var token = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(userJson));
        context.Response.Cookies.Append(SessionCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = false, // Set to true in production with HTTPS
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddHours(24)
        });
    }

    public static void ClearUserSession(HttpContext context)
    {
        context.Response.Cookies.Delete(SessionCookieName);
    }

    public static UserContext? GetUser(HttpContext context)
    {
        if (context.Items.TryGetValue(SessionKey, out var userObj) && userObj is UserContext user)
        {
            return user;
        }
        return null;
    }
}
