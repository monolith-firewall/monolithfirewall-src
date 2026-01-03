using System.Text.Json;
using Monolith.FireWall.Platform.Models;

namespace Monolith.FireWall.Core.Services.Platform;

public sealed class PlatformPolicyStore
{
    private readonly string _policyPath;
    private PlatformPolicyDocument? _document;

    public PlatformPolicyStore(string policyPath)
    {
        _policyPath = policyPath;
        Load();
    }

    public void Load()
    {
        _document = null;
        if (!File.Exists(_policyPath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(_policyPath);
            _document = JsonSerializer.Deserialize<PlatformPolicyDocument>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            _document = null;
        }
    }

    public PlatformCapability? GetCapabilityAllowlist(string? packageId, string? moduleId)
    {
        var module = GetModulePolicy(packageId, moduleId);
        if (module == null || module.Capabilities.Count == 0)
        {
            return null;
        }

        PlatformCapability caps = PlatformCapability.None;
        foreach (var entry in module.Capabilities)
        {
            if (Enum.TryParse<PlatformCapability>(entry, true, out var parsed))
            {
                caps |= parsed;
            }
        }

        return caps;
    }

    public IReadOnlyList<string> GetActionAllowlist(string? packageId, string? moduleId)
    {
        var module = GetModulePolicy(packageId, moduleId);
        return module?.Actions ?? new List<string>();
    }

    public IReadOnlyList<string> GetFileAllowlist(string? packageId, string? moduleId, bool write)
    {
        var module = GetModulePolicy(packageId, moduleId);
        if (module?.Files == null)
        {
            return Array.Empty<string>();
        }

        return write ? module.Files.Write : module.Files.Read;
    }

    private PlatformPolicyModule? GetModulePolicy(string? packageId, string? moduleId)
    {
        if (_document == null || string.IsNullOrWhiteSpace(packageId) || string.IsNullOrWhiteSpace(moduleId))
        {
            return null;
        }

        var package = _document.Packages.FirstOrDefault(p =>
            string.Equals(p.Id, packageId, StringComparison.OrdinalIgnoreCase));
        if (package == null)
        {
            return null;
        }

        return package.Modules.FirstOrDefault(m =>
            string.Equals(m.Id, moduleId, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class PlatformPolicyDocument
{
    public List<PlatformPolicyPackage> Packages { get; set; } = new();
}

public sealed class PlatformPolicyPackage
{
    public string Id { get; set; } = string.Empty;
    public List<PlatformPolicyModule> Modules { get; set; } = new();
}

public sealed class PlatformPolicyModule
{
    public string Id { get; set; } = string.Empty;
    public List<string> Capabilities { get; set; } = new();
    public List<string> Actions { get; set; } = new();
    public PlatformPolicyFileAccess? Files { get; set; }
}

public sealed class PlatformPolicyFileAccess
{
    public List<string> Read { get; set; } = new();
    public List<string> Write { get; set; } = new();
}
