using System.Text.Json;

namespace Monolith.FireWall.WebUI.Services;

public sealed class PackageUpdatesClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _baseUrl;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);
    private DateTime _lastFetch = DateTime.MinValue;
    private List<AvailablePackage> _cache = new();

    public PackageUpdatesClient(IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _httpClientFactory = httpClientFactory;
        _baseUrl = config["PackageUpdates:BaseUrl"] ?? "https://updates.monolithfirewall.com/api/v1/packages";
    }

    public DateTime LastFetchUtc => _lastFetch;

    public async Task<List<AvailablePackage>> GetAvailablePackagesAsync(string version, CancellationToken cancellationToken)
    {
        if (_cache.Count > 0 && DateTime.UtcNow - _lastFetch < _cacheDuration)
        {
            return _cache;
        }

        var url = $"{_baseUrl}?version={Uri.EscapeDataString(version)}";
        using var client = _httpClientFactory.CreateClient();

        var response = await client.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return _cache;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var packages = ParsePackages(json);

        _cache = packages;
        _lastFetch = DateTime.UtcNow;
        return packages;
    }

    private static List<AvailablePackage> ParsePackages(string json)
    {
        var results = new List<AvailablePackage>();
        if (string.IsNullOrWhiteSpace(json))
        {
            return results;
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var packagesEl = root;

        if (root.TryGetProperty("packages", out var packagesProp))
        {
            packagesEl = packagesProp;
        }
        else if (root.TryGetProperty("data", out var dataProp))
        {
            packagesEl = dataProp;
        }

        if (packagesEl.ValueKind != JsonValueKind.Array)
        {
            return results;
        }

        foreach (var pkg in packagesEl.EnumerateArray())
        {
            var id =
                GetString(pkg, "id") ??
                GetString(pkg, "packageId") ??
                GetString(pkg, "package_id") ??
                "";
            var name = GetString(pkg, "name") ?? id;
            var version = GetString(pkg, "version") ?? "";
            var description = GetString(pkg, "description") ?? "";
            var downloadUrl =
                GetString(pkg, "downloadUrl") ??
                GetString(pkg, "download_url") ??
                GetString(pkg, "url") ??
                "";
            var sha256 =
                GetString(pkg, "sha256") ??
                GetString(pkg, "sha_256") ??
                GetString(pkg, "checksum") ??
                GetString(pkg, "hash") ??
                null;
            var category =
                GetString(pkg, "category") ??
                GetString(pkg, "Category") ??
                GetString(pkg, "group") ??
                GetString(pkg, "Group") ??
                null;

            results.Add(new AvailablePackage
            {
                Id = id,
                Name = name,
                Version = version,
                Description = description,
                DownloadUrl = downloadUrl,
                Sha256 = sha256,
                Category = category,
                Author = GetString(pkg, "author"),
                Homepage = GetString(pkg, "homepage"),
                ReleaseNotes = GetString(pkg, "releaseNotes")
            });
        }

        return results;
    }

    private static string? GetString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) ? value.GetString() : null;
    }
}

public sealed class AvailablePackage
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Author { get; set; }
    public string? Homepage { get; set; }
    public string? ReleaseNotes { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
    public string? Sha256 { get; set; }
    public string? Category { get; set; }
}
