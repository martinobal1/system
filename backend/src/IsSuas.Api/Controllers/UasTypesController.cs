using IsSuas.Application.Fleet;
using IsSuas.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsSuas.Api.Controllers;

/// <summary>Technická dokumentácia typov UAS (katalóg, PDF manuály, AI extrakcia špecifikácií).</summary>
[ApiController]
[Authorize]
[Route("api/v1/uas-types")]
public sealed class UasTypesController(IUasTypeDocService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UasTypeDocDto>>> List(CancellationToken cancellationToken)
        => Ok(await service.ListAsync(cancellationToken));

    [HttpPut("{slug}")]
    [RequestSizeLimit(2_000_000)]
    public async Task<ActionResult<UasTypeDocDto>> Save(string slug, [FromBody] SaveUasTypeDocRequest request, CancellationToken cancellationToken)
        => Ok(await service.SaveAsync(slug, request, cancellationToken));

    [HttpPost("{slug}/hide")]
    public async Task<IActionResult> Hide(string slug, [FromBody] HideUasTypeDocRequest? request, CancellationToken cancellationToken)
    {
        await service.HideAsync(slug, request ?? new HideUasTypeDocRequest(null, null), cancellationToken);
        return NoContent();
    }

    [HttpPost("{slug}/manual")]
    [RequestSizeLimit(80_000_000)]
    public async Task<ActionResult<UasTypeDocDto>> UploadManual(string slug, [FromBody] UploadUasTypeManualRequest request, CancellationToken cancellationToken)
        => Ok(await service.UploadManualAsync(slug, request, cancellationToken));

    [HttpGet("{slug}/manual")]
    public async Task<IActionResult> DownloadManual(string slug, CancellationToken cancellationToken)
    {
        var (content, fileName) = await service.DownloadManualAsync(slug, cancellationToken);
        Response.Headers.CacheControl = "private, no-store";
        return File(content, "application/pdf", fileName);
    }

    [HttpPost("extract")]
    [RequestSizeLimit(4_000_000)]
    public async Task<ActionResult<UasTypeExtractionDto>> Extract([FromBody] ExtractUasTypeSpecsRequest request, CancellationToken cancellationToken)
        => Ok(await service.ExtractSpecsAsync(request, cancellationToken));
}
