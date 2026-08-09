using IsSuas.Application.Controls;
using IsSuas.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsSuas.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/control-audits")]
public sealed class ControlAuditsController(IControlAuditService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ControlAuditDto>>> List(CancellationToken cancellationToken)
        => Ok(await service.ListAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ControlAuditDetailDto>> Get(Guid id, CancellationToken cancellationToken)
        => Ok(await service.GetAsync(id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<ControlAuditDto>> Create([FromBody] CreateControlAuditRequest request, CancellationToken cancellationToken)
        => Ok(await service.CreateAsync(request, cancellationToken));

    [HttpPost("{id:guid}/start")]
    public async Task<ActionResult<ControlAuditDto>> Start(Guid id, [FromBody] StartControlAuditRequest request, CancellationToken cancellationToken)
        => Ok(await service.StartAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/checklist/{itemId:guid}")]
    public async Task<ActionResult<ControlChecklistItemDto>> Checklist(Guid id, Guid itemId, [FromBody] UpdateChecklistResultRequest request, CancellationToken cancellationToken)
        => Ok(await service.UpdateChecklistAsync(id, itemId, request, cancellationToken));

    [HttpPost("{id:guid}/findings")]
    public async Task<ActionResult<ControlFindingDto>> AddFinding(Guid id, [FromBody] AddControlFindingRequest request, CancellationToken cancellationToken)
        => Ok(await service.AddFindingAsync(id, request, cancellationToken));

    [HttpPost("findings/{findingId:guid}/actions")]
    public async Task<ActionResult<ControlCorrectiveActionDto>> AddAction(Guid findingId, [FromBody] AddControlActionRequest request, CancellationToken cancellationToken)
        => Ok(await service.AddActionAsync(findingId, request, cancellationToken));

    [HttpPost("actions/{actionId:guid}/complete")]
    public async Task<ActionResult<ControlCorrectiveActionDto>> CompleteAction(Guid actionId, [FromBody] CompleteControlActionRequest request, CancellationToken cancellationToken)
        => Ok(await service.CompleteActionAsync(actionId, request, cancellationToken));

    [HttpPost("{id:guid}/reports")]
    public async Task<ActionResult<ControlReportDto>> IssueReport(Guid id, [FromBody] IssueControlReportRequest request, CancellationToken cancellationToken)
        => Ok(await service.IssueReportAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<ControlAuditDto>> Close(Guid id, [FromBody] CloseControlAuditRequest request, CancellationToken cancellationToken)
        => Ok(await service.CloseAsync(id, request, cancellationToken));
}
