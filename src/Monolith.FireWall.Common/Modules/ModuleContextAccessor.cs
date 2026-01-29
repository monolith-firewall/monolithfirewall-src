using Monolith.FireWall.Common.Interfaces;

namespace Monolith.FireWall.Common.Modules;

/// <summary>
/// Provides ambient access to the current module context.
/// Used by controller-based routes.
/// </summary>
public static class ModuleContextAccessor
{
    private static readonly AsyncLocal<IModuleContext?> _current = new();

    /// <summary>
    /// Gets the current module context.
    /// </summary>
    public static IModuleContext? Current => _current.Value;

    /// <summary>
    /// Sets the current module context for the duration of the scope.
    /// </summary>
    public static IDisposable SetCurrent(IModuleContext context)
    {
        var previous = _current.Value;
        _current.Value = context;
        return new ContextScope(previous);
    }

    private sealed class ContextScope : IDisposable
    {
        private readonly IModuleContext? _previous;

        public ContextScope(IModuleContext? previous)
        {
            _previous = previous;
        }

        public void Dispose()
        {
            _current.Value = _previous;
        }
    }
}
