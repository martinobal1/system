using IsSuas.Application.Notifications;
using IsSuas.Application.Operations;
using IsSuas.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsSuas.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/operations")]
public sealed class OperationsController(IOperationService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OperationDto>>> List(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
        => Ok(await service.ListAsync(from, to, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OperationDetailDto>> Get(Guid id, CancellationToken cancellationToken)
        => Ok(await service.GetAsync(id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<OperationDto>> Create([FromBody] CreateOperationRequest request, CancellationToken cancellationToken)
    {
        var created = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<OperationDto>> Update(Guid id, [FromBody] UpdateOperationRequest request, CancellationToken cancellationToken)
        => Ok(await service.UpdateAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/resources")]
    public async Task<ActionResult<OperationResourceDto>> AssignResource(Guid id, [FromBody] AssignOperationResourceRequest request, CancellationToken cancellationToken)
        => Ok(await service.AssignResourceAsync(id, request, cancellationToken));

    public sealed record SubmitBody(uint RowVersion);

    [HttpPost("{id:guid}/submit")]
    public async Task<ActionResult<OperationDto>> Submit(Guid id, [FromBody] SubmitBody body, CancellationToken cancellationToken)
        => Ok(await service.SubmitAsync(id, body.RowVersion, cancellationToken));

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<OperationDto>> Approve(Guid id, [FromBody] DecisionRequest request, CancellationToken cancellationToken)
        => Ok(await service.ApproveAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/return")]
    public async Task<ActionResult<OperationDto>> Return(Guid id, [FromBody] DecisionRequest request, CancellationToken cancellationToken)
        => Ok(await service.ReturnAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<OperationDto>> Reject(Guid id, [FromBody] DecisionRequest request, CancellationToken cancellationToken)
        => Ok(await service.RejectAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/approval-steps")]
    public async Task<ActionResult<OperationApprovalStepDto>> AddStep(Guid id, [FromBody] AddApprovalStepRequest request, CancellationToken cancellationToken)
        => Ok(await service.AddApprovalStepAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/recurrence")]
    public async Task<ActionResult<OperationRecurrenceRuleDto>> SetRecurrence(Guid id, [FromBody] SetRecurrenceRequest request, CancellationToken cancellationToken)
        => Ok(await service.SetRecurrenceAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/recurrence/generate")]
    public async Task<ActionResult<IReadOnlyList<OperationDto>>> Generate(Guid id, [FromQuery] int count = 1, CancellationToken cancellationToken = default)
        => Ok(await service.GenerateRecurrenceOccurrencesAsync(id, count, cancellationToken));

    [HttpPost("{id:guid}/map-check")]
    public async Task<ActionResult<OperationMapCheckDto>> MapCheck(Guid id, CancellationToken cancellationToken)
        => Ok(await service.RunMapCheckAsync(id, cancellationToken));
}

[ApiController]
[Authorize]
[Route("api/v1/approval-conditions")]
public sealed class ApprovalConditionsController(IOperationService service) : ControllerBase
{
    [HttpPost("{id:guid}/verify")]
    public async Task<ActionResult<OperationApprovalConditionDto>> Verify(Guid id, [FromBody] VerifyApprovalConditionRequest request, CancellationToken cancellationToken)
        => Ok(await service.VerifyConditionAsync(id, request, cancellationToken));
}

[ApiController]
[Authorize]
[Route("api/v1/work-tasks")]
public sealed class WorkTasksController(IOperationService operations, INotificationService notifications) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkTaskDto>>> List(CancellationToken cancellationToken)
        => Ok(await operations.ListTasksAsync(cancellationToken));

    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<IReadOnlyList<WorkTaskHistoryDto>>> History(Guid id, CancellationToken cancellationToken)
        => Ok(await notifications.ListTaskHistoryAsync(id, cancellationToken));

    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateWorkTaskStatusRequest request, CancellationToken cancellationToken)
    {
        await notifications.UpdateWorkTaskStatusAsync(id, request, cancellationToken);
        return NoContent();
    }
}
