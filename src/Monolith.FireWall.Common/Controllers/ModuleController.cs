using System.Text.Json;
using Monolith.FireWall.Common.Interfaces;
using Monolith.FireWall.Common.Models;

namespace Monolith.FireWall.Common.Controllers;

/// <summary>
/// Base class for module API controllers.
/// Controllers handle API routes for a module.
/// </summary>
public abstract class ModuleController
{
    private IModuleContext? _context;
    private ApiRequest? _request;

    /// <summary>
    /// The module context providing access to configuration and services.
    /// </summary>
    protected IModuleContext Context => _context ?? throw new InvalidOperationException("Controller not initialized");

    /// <summary>
    /// The current API request.
    /// </summary>
    protected ApiRequest Request => _request ?? throw new InvalidOperationException("Controller not initialized");

    /// <summary>
    /// Logger instance.
    /// </summary>
    protected Interfaces.ILogger Logger => Context.Logger;

    /// <summary>
    /// Current user context.
    /// </summary>
    protected UserContext User => Request.User;

    /// <summary>
    /// Called internally to set the context before action execution.
    /// </summary>
    internal void SetContext(IModuleContext context, ApiRequest request)
    {
        _context = context;
        _request = request;
    }

    /// <summary>
    /// Create a success response with no data.
    /// </summary>
    protected ApiResponse Ok()
    {
        return new ApiResponse(true, null, null);
    }

    /// <summary>
    /// Create a success response with data.
    /// </summary>
    protected ApiResponse Ok(object? data)
    {
        return new ApiResponse(true, data, null);
    }

    /// <summary>
    /// Create an error response.
    /// </summary>
    protected ApiResponse Error(string message)
    {
        return new ApiResponse(false, null, message);
    }

    /// <summary>
    /// Create a response based on a boolean result.
    /// </summary>
    protected ApiResponse Result(bool success, string? errorMessage = null)
    {
        return success ? Ok() : Error(errorMessage ?? "Operation failed");
    }

    /// <summary>
    /// Deserialize the request body to type T.
    /// </summary>
    protected T? GetBody<T>() where T : class
    {
        if (string.IsNullOrEmpty(Request.Body))
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>(Request.Body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
        catch (JsonException)
        {
            Logger.LogWarning($"Failed to deserialize request body to {typeof(T).Name}");
            return null;
        }
    }

    /// <summary>
    /// Get a query parameter value.
    /// </summary>
    protected string? GetQuery(string key)
    {
        return Request.Query.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>
    /// Get a query parameter value with a default.
    /// </summary>
    protected string GetQuery(string key, string defaultValue)
    {
        return Request.Query.TryGetValue(key, out var value) ? value : defaultValue;
    }

    /// <summary>
    /// Get a query parameter as an integer.
    /// </summary>
    protected int GetQueryInt(string key, int defaultValue = 0)
    {
        if (Request.Query.TryGetValue(key, out var value) && int.TryParse(value, out var result))
            return result;
        return defaultValue;
    }

    /// <summary>
    /// Get a query parameter as a boolean.
    /// </summary>
    protected bool GetQueryBool(string key, bool defaultValue = false)
    {
        if (Request.Query.TryGetValue(key, out var value) && bool.TryParse(value, out var result))
            return result;
        return defaultValue;
    }

    /// <summary>
    /// Get a service from the DI container.
    /// </summary>
    protected T GetService<T>() where T : class
    {
        return Context.GetService<T>();
    }

    /// <summary>
    /// Check if the current user has a specific permission.
    /// </summary>
    protected bool HasPermission(string permission)
    {
        return User.Permissions.Contains(permission);
    }

    /// <summary>
    /// Check if the current user has any of the specified permissions.
    /// </summary>
    protected bool HasAnyPermission(params string[] permissions)
    {
        return permissions.Any(p => User.Permissions.Contains(p));
    }
}
