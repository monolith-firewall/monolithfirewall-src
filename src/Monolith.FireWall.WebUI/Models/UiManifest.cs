using System.Text.Json;

namespace Monolith.FireWall.WebUI.Models;

public sealed class UiManifest
{
    public string DefaultRouteId { get; set; } = "dashboard";
    public string LoginRouteId { get; set; } = "login";
    public List<UiRoute> Routes { get; set; } = new();
    public List<UiMenuItem> Menu { get; set; } = new();
    public Dictionary<string, object?> Metadata { get; set; } = new();

    public void Materialize()
    {
        Metadata = MaterializeDict(Metadata);
        foreach (var route in Routes)
        {
            route.Meta = MaterializeDict(route.Meta);
        }
    }

    private static Dictionary<string, object?> MaterializeDict(Dictionary<string, object?> dict)
    {
        var result = new Dictionary<string, object?>();
        foreach (var kvp in dict)
        {
            result[kvp.Key] = MaterializeValue(kvp.Value);
        }
        return result;
    }

    private static object? MaterializeValue(object? value)
    {
        if (value is JsonElement elem)
        {
            // DEBUG LOGGING - help identify which part of manifest is causing trouble
            Console.WriteLine($"[MANIFEST-DEBUG] Materializing JsonElement: {elem.ValueKind}");
            if (elem.ValueKind == JsonValueKind.Object || elem.ValueKind == JsonValueKind.Array)
            {
                // Console.WriteLine($"[MANIFEST-DEBUG] Raw: {elem.GetRawText()}");
            }

            return elem.ValueKind switch
            {
                JsonValueKind.String => elem.GetString(),
                JsonValueKind.Number => elem.TryGetInt64(out var i) ? i : elem.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Array => elem.EnumerateArray().Select(e => MaterializeValue(e)).ToList(),
                JsonValueKind.Object => elem.EnumerateObject().ToDictionary(p => p.Name, p => MaterializeValue(p.Value)),
                JsonValueKind.Null => null,
                _ => null
            };
        }
        
        if (value is IDictionary<string, object?> dict)
        {
            return dict.ToDictionary(kvp => kvp.Key, kvp => MaterializeValue(kvp.Value));
        }
        
        if (value is System.Collections.IEnumerable list && value is not string)
        {
            var materializedList = new List<object?>();
            foreach (var item in list)
            {
                materializedList.Add(MaterializeValue(item));
            }
            return materializedList;
        }

        return value;
    }
}

public sealed class UiRoute
{
    public string Id { get; set; } = "";
    public string Path { get; set; } = "";
    public string Title { get; set; } = "";
    /// <summary>
    /// internal|package|firewall|login
    /// </summary>
    public string Kind { get; set; } = "internal";
    public bool RequiresAuth { get; set; } = true;
    public UiRouteAssets? Assets { get; set; }
    /// <summary>
    /// HTML shell/container for internal JS pages (only for kind="internal")
    /// </summary>
    public string? Shell { get; set; }
    public Dictionary<string, object?> Meta { get; set; } = new();
}

public sealed class UiRouteAssets
{
    /// <summary>
    /// Asset keys for /assets/pages/{module}/{asset}.js|.css or package assets, depending on Kind.
    /// </summary>
    public List<string> Js { get; set; } = new();
    public List<string> Css { get; set; } = new();
    /// <summary>
    /// Additional assets (used for settings sub-tabs etc).
    /// </summary>
    public List<string> ExtraJs { get; set; } = new();
    public List<string> ExtraCss { get; set; } = new();
}

public sealed class UiMenuItem
{
    public string Label { get; set; } = "";
    public string? RouteId { get; set; }
    public string? Path { get; set; }
    public string? Icon { get; set; }
    public List<UiMenuItem>? Children { get; set; }
}
