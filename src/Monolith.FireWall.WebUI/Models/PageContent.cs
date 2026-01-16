namespace Monolith.FireWall.WebUI.Models;

/// <summary>
/// Represents the content and assets for a rendered page
/// </summary>
public class PageContent
{
    public string Html { get; set; } = string.Empty;
    public List<string> CssAssets { get; set; } = new();
    public List<string> JsAssets { get; set; } = new();
}

/// <summary>
/// API response for page content requests
/// </summary>
public class PageContentResponse
{
    public bool Success { get; set; }
    public string? Html { get; set; }
    public PageAssets? Assets { get; set; }
    public string? Error { get; set; }
}

public class PageAssets
{
    public List<string> Css { get; set; } = new();
    public List<string> Js { get; set; } = new();
}
