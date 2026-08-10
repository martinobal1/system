using IsSuas.Application.Fleet;
using IsSuas.Application.Reporting;
using IsSuas.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsSuas.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/pilot-uas-authorizations")]
public sealed class PilotUasAuthorizationsController(IPilotUasAuthorizationService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PilotUasAuthorizationDto>>> List(
        [FromQuery] Guid? orgUnitId,
        [FromQuery] Guid? uasId,
        [FromQuery] Guid? pilotId,
        [FromQuery] bool activeOnly = true,
        CancellationToken cancellationToken = default)
        => Ok(await service.ListAsync(orgUnitId, uasId, pilotId, activeOnly, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<PilotUasAuthorizationDto>> Create(
        [FromBody] CreatePilotUasAuthorizationRequest request,
        CancellationToken cancellationToken)
    {
        var created = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(List), new { id = created.Id }, created);
    }

    [HttpPost("{id:guid}/end")]
    public async Task<IActionResult> End(Guid id, [FromBody] EndPilotUasAuthorizationRequest request, CancellationToken cancellationToken)
    {
        await service.EndAsync(id, request, cancellationToken);
        return NoContent();
    }
}

[ApiController]
[Authorize]
[Route("api/v1/monthly-flight-reports")]
public sealed class MonthlyFlightReportsController(IMonthlyFlightReportService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MonthlyFlightReportDto>>> List(
        [FromQuery] int? year,
        [FromQuery] int? month,
        [FromQuery] Guid? orgUnitId,
        CancellationToken cancellationToken)
        => Ok(await service.ListAsync(year, month, orgUnitId, cancellationToken));

    [HttpGet("current")]
    public async Task<ActionResult<MonthlyFlightReportDto>> GetCurrent(
        [FromQuery] Guid? orgUnitId,
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken cancellationToken)
    {
        var report = await service.GetAsync(orgUnitId, year, month, cancellationToken);
        return report is null ? NotFound() : Ok(report);
    }

    [HttpPost("draft")]
    public async Task<ActionResult<MonthlyFlightReportDto>> GetOrCreateDraft(
        [FromQuery] Guid? orgUnitId,
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken cancellationToken)
        => Ok(await service.GetOrCreateDraftAsync(orgUnitId, year, month, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MonthlyFlightReportDto>> Save(Guid id, [FromBody] SaveMonthlyFlightReportRequest request, CancellationToken cancellationToken)
        => Ok(await service.SaveDraftAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/confirm")]
    public async Task<ActionResult<MonthlyFlightReportDto>> Confirm(Guid id, [FromBody] ConfirmMonthlyFlightReportRequest request, CancellationToken cancellationToken)
        => Ok(await service.ConfirmAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/no-activity")]
    public async Task<ActionResult<MonthlyFlightReportDto>> NoActivity(Guid id, [FromBody] MarkNoActivityMonthlyFlightReportRequest request, CancellationToken cancellationToken)
        => Ok(await service.MarkNoActivityAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/amend")]
    public async Task<ActionResult<MonthlyFlightReportDto>> Amend(Guid id, [FromBody] AmendMonthlyFlightReportRequest request, CancellationToken cancellationToken)
        => Ok(await service.AmendAsync(id, request, cancellationToken));
}
