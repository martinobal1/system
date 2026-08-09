using IsSuas.Application.Reporting;
using IsSuas.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsSuas.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/dashboard")]
public sealed class DashboardController(IReportingService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get(
        [FromQuery] string? scope,
        [FromQuery] Guid? orgUnitId,
        CancellationToken cancellationToken)
        => Ok(await service.GetDashboardAsync(scope, orgUnitId, cancellationToken));
}

[ApiController]
[Authorize]
[Route("api/v1/calendar")]
public sealed class CalendarController(IReportingService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CalendarItemDto>>> Get(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var start = from ?? DateTimeOffset.UtcNow.AddDays(-7);
        var end = to ?? DateTimeOffset.UtcNow.AddDays(30);
        return Ok(await service.GetCalendarAsync(start, end, cancellationToken));
    }
}

[ApiController]
[Authorize]
[Route("api/v1/reports")]
public sealed class ReportsController(IReportingService service) : ControllerBase
{
    [HttpPost("generate")]
    public async Task<ActionResult<ReportDto>> Generate([FromBody] ReportRequest request, CancellationToken cancellationToken)
        => Ok(await service.GenerateReportAsync(request, cancellationToken));

    [HttpPost("export.csv")]
    public async Task<IActionResult> ExportCsv([FromBody] ReportRequest request, CancellationToken cancellationToken)
    {
        var csv = await service.ExportReportCsvAsync(request, cancellationToken);
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8",
            $"{request.ReportTypeCode}-{DateTime.UtcNow:yyyyMMddHHmm}.csv");
    }
}

[ApiController]
[Authorize]
[Route("api/v1/official-reports")]
public sealed class OfficialReportsController(IReportingService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OfficialReportDto>>> List(CancellationToken cancellationToken)
        => Ok(await service.ListOfficialAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OfficialReportDto>> Get(Guid id, CancellationToken cancellationToken)
        => Ok(await service.GetOfficialAsync(id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<OfficialReportDto>> Create([FromBody] CreateOfficialReportRequest request, CancellationToken cancellationToken)
        => Ok(await service.CreateOfficialAsync(request, cancellationToken));

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<OfficialReportDto>> Approve(Guid id, [FromBody] ApproveOfficialReportRequest request, CancellationToken cancellationToken)
        => Ok(await service.ApproveOfficialAsync(id, request, cancellationToken));
}
