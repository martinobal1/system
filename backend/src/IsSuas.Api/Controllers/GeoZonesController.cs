using IsSuas.Application.GeoZones;
using IsSuas.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsSuas.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/geo-zones")]
public sealed class GeoZonesController(IGeoZoneService service) : ControllerBase
{
    [HttpGet("map")]
    public async Task<ActionResult<GeoZoneMapPayloadDto>> GetMap(CancellationToken cancellationToken)
        => Ok(await service.GetActiveMapAsync(cancellationToken));

    [HttpGet("imports")]
    public async Task<ActionResult<IReadOnlyList<GeoZoneImportDto>>> ListImports(CancellationToken cancellationToken)
        => Ok(await service.ListImportsAsync(cancellationToken));

    [HttpGet("imports/{id:guid}")]
    public async Task<ActionResult<GeoZoneImportDto>> GetImport(Guid id, CancellationToken cancellationToken)
    {
        var item = await service.GetImportAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("imports/preview")]
    public async Task<ActionResult<GeoZoneImportDto>> Preview([FromBody] PreviewGeoZoneImportRequest request, CancellationToken cancellationToken)
    {
        var created = await service.PreviewImportAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetImport), new { id = created.Id }, created);
    }

    [HttpPost("imports/{id:guid}/activate")]
    public async Task<ActionResult<GeoZoneImportDto>> Activate(Guid id, CancellationToken cancellationToken)
        => Ok(await service.ActivateAsync(id, cancellationToken));

    [HttpPost("imports/{id:guid}/discard")]
    public async Task<ActionResult<GeoZoneImportDto>> Discard(Guid id, CancellationToken cancellationToken)
        => Ok(await service.DiscardPreviewAsync(id, cancellationToken));

    [HttpPost("imports/{id:guid}/remove")]
    public async Task<ActionResult<GeoZoneImportDto>> Remove(Guid id, CancellationToken cancellationToken)
        => Ok(await service.RemoveActiveAsync(id, cancellationToken));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GeoZoneDto>>> ListActive(CancellationToken cancellationToken)
        => Ok(await service.ListActiveZonesAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GeoZoneDto>> GetZone(Guid id, CancellationToken cancellationToken)
    {
        var zone = await service.GetZoneAsync(id, cancellationToken);
        return zone is null ? NotFound() : Ok(zone);
    }
}
