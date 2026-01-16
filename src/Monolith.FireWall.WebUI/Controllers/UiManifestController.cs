using Microsoft.AspNetCore.Mvc;
using Monolith.FireWall.WebUI.Services;

namespace Monolith.FireWall.WebUI.Controllers;

[ApiController]
[Route("api/ui")]
public sealed class UiManifestController : ControllerBase
{
    private readonly UiManifestBuilder _builder;

    public UiManifestController(UiManifestBuilder builder)
    {
        _builder = builder;
    }

    [HttpGet("manifest")]
    public async Task<IActionResult> GetManifest(CancellationToken ct)
    {
        var manifest = await _builder.BuildAsync(ct);
        return Ok(new { success = true, data = manifest });
    }
}

