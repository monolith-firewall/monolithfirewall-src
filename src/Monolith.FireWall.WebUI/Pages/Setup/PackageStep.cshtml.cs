using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Monolith.FireWall.WebUI.Services;

namespace Monolith.FireWall.WebUI.Pages.Setup;

public class PackageStepModel : PageModel
{
    private readonly CoreApiClient _coreClient;
    private readonly ILogger<PackageStepModel> _logger;

    [FromRoute]
    public string PackageId { get; set; } = string.Empty;

    [FromRoute]
    public string PageId { get; set; } = string.Empty;

    public string? PageTitle { get; set; }
    public string? Description { get; set; }
    public string? Route { get; set; }
    public string? ErrorMessage { get; set; }

    public PackageStepModel(CoreApiClient coreClient, ILogger<PackageStepModel> logger)
    {
        _coreClient = coreClient;
        _logger = logger;
    }

    public async Task OnGetAsync()
    {
        try
        {
            // Load package setup pages to get this page's details
            var request = new { action = "setup.packages" };
            var requestJson = System.Text.Json.JsonSerializer.Serialize(request);
            var responseJson = await _coreClient.SendRequestAsync(requestJson);
            
            using var doc = System.Text.Json.JsonDocument.Parse(responseJson);
            var root = doc.RootElement;
            
            if (root.TryGetProperty("Success", out var successEl) && successEl.GetBoolean() ||
                root.TryGetProperty("success", out var successEl2) && successEl2.GetBoolean())
            {
                var data = root.TryGetProperty("Data", out var dataEl) ? dataEl : 
                          root.TryGetProperty("data", out var dataEl2) ? dataEl2 : root;
                
                var packages = data.TryGetProperty("packages", out var packagesEl) ? packagesEl :
                              data.TryGetProperty("Packages", out var packagesEl2) ? packagesEl2 :
                              data.EnumerateArray();

                foreach (var package in packages)
                {
                    var packageId = package.TryGetProperty("packageId", out var pkgIdEl) ? pkgIdEl.GetString() :
                                   package.TryGetProperty("PackageId", out var pkgIdEl2) ? pkgIdEl2.GetString() : null;

                    if (packageId != PackageId) continue;

                    var setupPages = package.TryGetProperty("setupPages", out var pagesEl) ? pagesEl :
                                    package.TryGetProperty("SetupPages", out var pagesEl2) ? pagesEl2 :
                                    default;

                    if (setupPages.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var page in setupPages.EnumerateArray())
                        {
                            var pageId = page.TryGetProperty("id", out var pageIdEl) ? pageIdEl.GetString() :
                                        page.TryGetProperty("Id", out var pageIdEl2) ? pageIdEl2.GetString() : null;

                            if (pageId == PageId)
                            {
                                PageTitle = page.TryGetProperty("title", out var titleEl) ? titleEl.GetString() :
                                           page.TryGetProperty("Title", out var titleEl2) ? titleEl2.GetString() : null;
                                
                                Description = page.TryGetProperty("description", out var descEl) ? descEl.GetString() :
                                             page.TryGetProperty("Description", out var descEl2) ? descEl2.GetString() : null;
                                
                                Route = page.TryGetProperty("route", out var routeEl) ? routeEl.GetString() :
                                       page.TryGetProperty("Route", out var routeEl2) ? routeEl2.GetString() : null;
                                
                                break;
                            }
                        }
                    }
                    break;
                }
            }
            else
            {
                var error = root.TryGetProperty("Error", out var errEl) ? errEl.GetString() :
                           root.TryGetProperty("error", out var errEl2) ? errEl2.GetString() : "Unknown error";
                ErrorMessage = $"Failed to load package setup page: {error}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading package setup page {PackageId}/{PageId}", PackageId, PageId);
            ErrorMessage = $"Error loading setup page: {ex.Message}";
        }
    }
}
