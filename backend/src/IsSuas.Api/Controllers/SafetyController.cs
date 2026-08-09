using IsSuas.Application.Safety;
using IsSuas.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsSuas.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/safety-events")]
public sealed class SafetyEventsController(ISafetyService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SafetyEventDto>>> List(CancellationToken cancellationToken)
        => Ok(await service.ListAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SafetyEventDetailDto>> Get(Guid id, CancellationToken cancellationToken)
        => Ok(await service.GetAsync(id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<SafetyEventDto>> Create([FromBody] CreateSafetyEventRequest request, CancellationToken cancellationToken)
        => Ok(await service.CreateAsync(request, cancellationToken));

    [HttpPost("{id:guid}/submit")]
    public async Task<ActionResult<SafetyEventDto>> Submit(Guid id, [FromBody] SubmitSafetyEventRequest request, CancellationToken cancellationToken)
        => Ok(await service.SubmitAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/investigation")]
    public async Task<ActionResult<SafetyEventDetailDto>> Investigation(Guid id, [FromBody] UpdateInvestigationRequest request, CancellationToken cancellationToken)
        => Ok(await service.UpdateInvestigationAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/severity")]
    public async Task<ActionResult<SafetyEventDto>> ChangeSeverity(Guid id, [FromBody] ChangeSafetySeverityRequest request, CancellationToken cancellationToken)
        => Ok(await service.ChangeSeverityAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/evidences")]
    public async Task<ActionResult<SafetyEvidenceDto>> AddEvidence(Guid id, [FromBody] AddEvidenceRequest request, CancellationToken cancellationToken)
        => Ok(await service.AddEvidenceAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/actions")]
    public async Task<ActionResult<CorrectiveActionDto>> CreateAction(Guid id, [FromBody] CreateCorrectiveActionRequest request, CancellationToken cancellationToken)
        => Ok(await service.CreateActionAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/participants")]
    public async Task<ActionResult<SafetyEventParticipantDto>> AddParticipant(Guid id, [FromBody] CreateSafetyEventParticipantRequest request, CancellationToken cancellationToken)
        => Ok(await service.AddParticipantAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/assets")]
    public async Task<ActionResult<SafetyEventAssetDto>> AddAsset(Guid id, [FromBody] CreateSafetyEventAssetRequest request, CancellationToken cancellationToken)
        => Ok(await service.AddAssetAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/immediate-actions")]
    public async Task<ActionResult<SafetyImmediateActionDto>> AddImmediateAction(Guid id, [FromBody] CreateSafetyImmediateActionRequest request, CancellationToken cancellationToken)
        => Ok(await service.AddImmediateActionAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/investigations")]
    public async Task<ActionResult<SafetyInvestigationDto>> CreateInvestigation(Guid id, [FromBody] CreateSafetyInvestigationRequest request, CancellationToken cancellationToken)
        => Ok(await service.CreateInvestigationAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/investigations/{investigationId:guid}/causes")]
    public async Task<ActionResult<SafetyCauseDto>> AddCause(Guid id, Guid investigationId, [FromBody] CreateSafetyCauseRequest request, CancellationToken cancellationToken)
        => Ok(await service.AddCauseAsync(id, investigationId, request, cancellationToken));

    [HttpPost("{id:guid}/reporting-obligations")]
    public async Task<ActionResult<SafetyReportingObligationDto>> AddObligation(Guid id, [FromBody] CreateSafetyReportingObligationRequest request, CancellationToken cancellationToken)
        => Ok(await service.AddReportingObligationAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/reporting-obligations/{obligationId:guid}/fulfill")]
    public async Task<ActionResult<SafetyReportingObligationDto>> FulfillObligation(
        Guid id, Guid obligationId, [FromBody] FulfillSafetyReportingObligationRequest request, CancellationToken cancellationToken)
        => Ok(await service.FulfillReportingObligationAsync(id, obligationId, request, cancellationToken));

    [HttpPost("{id:guid}/fleet-alerts")]
    public async Task<ActionResult<FleetSafetyAlertDto>> CreateFleetAlert(Guid id, [FromBody] CreateFleetSafetyAlertRequest request, CancellationToken cancellationToken)
        => Ok(await service.CreateFleetAlertAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<SafetyEventDto>> Close(Guid id, [FromBody] CloseSafetyEventRequest request, CancellationToken cancellationToken)
        => Ok(await service.CloseAsync(id, request, cancellationToken));
}

[ApiController]
[Authorize]
[Route("api/v1/corrective-actions")]
public sealed class CorrectiveActionsController(ISafetyService service) : ControllerBase
{
    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<CorrectiveActionDto>> Complete(Guid id, [FromBody] CompleteCorrectiveActionRequest request, CancellationToken cancellationToken)
        => Ok(await service.CompleteActionAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/verify")]
    public async Task<ActionResult<CorrectiveActionDto>> Verify(Guid id, [FromBody] VerifyCorrectiveActionRequest request, CancellationToken cancellationToken)
        => Ok(await service.VerifyActionAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/waive")]
    public async Task<ActionResult<CorrectiveActionDto>> Waive(Guid id, [FromBody] WaiveCorrectiveActionRequest request, CancellationToken cancellationToken)
        => Ok(await service.WaiveActionAsync(id, request, cancellationToken));
}
