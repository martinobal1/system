using IsSuas.Application.Flights;
using IsSuas.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsSuas.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/flights")]
public sealed class FlightsController(IFlightService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FlightDto>>> List(CancellationToken cancellationToken)
        => Ok(await service.ListAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FlightDetailDto>> Get(Guid id, CancellationToken cancellationToken)
        => Ok(await service.GetAsync(id, cancellationToken));

    [HttpPost("from-operation")]
    public async Task<ActionResult<FlightDto>> CreateFromOperation([FromBody] CreateFlightFromOperationRequest request, CancellationToken cancellationToken)
    {
        var created = await service.CreateFromOperationAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPost("standalone")]
    public async Task<ActionResult<FlightDto>> CreateStandalone([FromBody] CreateStandaloneFlightRequest request, CancellationToken cancellationToken)
    {
        var created = await service.CreateStandaloneAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    public sealed record ReadyBody(uint RowVersion);

    [HttpPost("{id:guid}/ready")]
    public async Task<ActionResult<FlightDto>> Ready(Guid id, [FromBody] ReadyBody body, CancellationToken cancellationToken)
        => Ok(await service.MarkReadyAsync(id, body.RowVersion, cancellationToken));

    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<FlightDto>> Close(Guid id, [FromBody] CloseFlightRequest request, CancellationToken cancellationToken)
        => Ok(await service.CloseAsync(id, request, cancellationToken));

    [HttpGet("{id:guid}/track")]
    public async Task<IActionResult> ExportTrack(Guid id, [FromQuery] string format = "geojson", CancellationToken cancellationToken = default)
    {
        var (contentType, fileName, body) = await service.ExportTrackAsync(id, format, cancellationToken);
        return File(System.Text.Encoding.UTF8.GetBytes(body), contentType, fileName);
    }
}

[ApiController]
[Authorize]
[Route("api/v1/telemetry-imports")]
public sealed class TelemetryImportsController(IFlightService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<TelemetryImportDto>> Import([FromBody] ImportTelemetryRequest request, CancellationToken cancellationToken)
        => Ok(await service.ImportTelemetryAsync(request, cancellationToken));

    [HttpPost("{id:guid}/pair")]
    public async Task<ActionResult<TelemetryImportDto>> Pair(Guid id, [FromBody] PairTelemetryRequest request, CancellationToken cancellationToken)
        => Ok(await service.PairTelemetryAsync(id, request, cancellationToken));
}
