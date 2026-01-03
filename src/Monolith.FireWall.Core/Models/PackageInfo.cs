using System.Reflection;
using System.Runtime.Loader;
using Monolith.FireWall.Common.Interfaces;
using Monolith.FireWall.Common.Models;

namespace Monolith.FireWall.Core.Models;

public record PackageInfo(
    IMonolithPackageDefinition Definition,
    IMonolithPackage Package,
    Assembly MainAssembly,
    Assembly? ViewsAssembly,
    AssemblyLoadContext? LoadContext,
    List<PageDefinition> DiscoveredViews,
    string? PackageDirectory = null
)
{
    public bool HasRazorViews => ViewsAssembly != null && DiscoveredViews.Count > 0;
}

public record ModuleInfo(
    IMonolithModule Module,
    PackageInfo Package
);
