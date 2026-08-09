using IsSuas.Application.Pilots;
using IsSuas.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsSuas.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/pilots")]
public sealed class PilotsController(IPilotService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PilotDto>>> List(CancellationToken cancellationToken)
        => Ok(await service.ListAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PilotDetailDto>> Get(Guid id, CancellationToken cancellationToken)
        => Ok(await service.GetAsync(id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<PilotDto>> Create([FromBody] CreatePilotRequest request, CancellationToken cancellationToken)
    {
        var created = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<PilotDto>> ChangeStatus(Guid id, [FromBody] ChangePilotStatusRequest request, CancellationToken cancellationToken)
        => Ok(await service.ChangeStatusAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/qualifications")]
    public async Task<ActionResult<PilotQualificationDto>> AddQualification(Guid id, [FromBody] CreatePilotQualificationRequest request, CancellationToken cancellationToken)
        => Ok(await service.AddQualificationAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/qualifications/{qualificationId:guid}/verifications")]
    public async Task<ActionResult<QualificationVerificationDto>> AddVerification(
        Guid id, Guid qualificationId, [FromBody] CreateQualificationVerificationRequest request, CancellationToken cancellationToken)
        => Ok(await service.AddQualificationVerificationAsync(id, qualificationId, request, cancellationToken));

    [HttpPost("{id:guid}/certificates")]
    public async Task<ActionResult<PilotCertificateDto>> AddCertificate(Guid id, [FromBody] CreatePilotCertificateRequest request, CancellationToken cancellationToken)
        => Ok(await service.AddCertificateAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/certificates/{certificateId:guid}/issue")]
    public async Task<ActionResult<PilotCertificateDto>> IssueCertificate(Guid id, Guid certificateId, [FromBody] IssuePilotCertificateRequest request, CancellationToken cancellationToken)
        => Ok(await service.IssueCertificateAsync(id, certificateId, request, cancellationToken));

    [HttpPost("{id:guid}/certificates/{certificateId:guid}/scopes")]
    public async Task<ActionResult<CertificateScopeDto>> AddScope(
        Guid id, Guid certificateId, [FromBody] CreateCertificateScopeRequest request, CancellationToken cancellationToken)
        => Ok(await service.AddCertificateScopeAsync(id, certificateId, request, cancellationToken));

    [HttpPost("{id:guid}/trainings")]
    public async Task<ActionResult<PilotTrainingDto>> AddTraining(Guid id, [FromBody] CreatePilotTrainingRequest request, CancellationToken cancellationToken)
        => Ok(await service.AddTrainingAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/functions")]
    public async Task<ActionResult<PersonFunctionAssignmentDto>> AssignFunction(
        Guid id, [FromBody] CreatePersonFunctionAssignmentRequest request, CancellationToken cancellationToken)
        => Ok(await service.AssignFunctionAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/restrictions")]
    public async Task<ActionResult<PilotRestrictionDto>> AddRestriction(
        Guid id, [FromBody] CreatePilotRestrictionRequest request, CancellationToken cancellationToken)
        => Ok(await service.AddRestrictionAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/restrictions/{restrictionId:guid}/end")]
    public async Task<ActionResult<PilotRestrictionDto>> EndRestriction(
        Guid id, Guid restrictionId, [FromBody] EndPilotRestrictionRequest request, CancellationToken cancellationToken)
        => Ok(await service.EndRestrictionAsync(id, restrictionId, request, cancellationToken));

    [HttpGet("{id:guid}/eligibility")]
    public async Task<ActionResult<PilotEligibilityCheckDto>> Eligibility(Guid id, CancellationToken cancellationToken)
        => Ok(await service.CheckEligibilityAsync(id, cancellationToken));
}

[ApiController]
[Authorize]
[Route("api/v1/training-catalog")]
public sealed class TrainingCatalogController(IPilotService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TrainingCatalogItemDto>>> List(CancellationToken cancellationToken)
        => Ok(await service.ListCatalogAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<TrainingCatalogItemDto>> Create([FromBody] CreateTrainingCatalogItemRequest request, CancellationToken cancellationToken)
        => Ok(await service.CreateCatalogItemAsync(request, cancellationToken));
}

[ApiController]
[Authorize]
[Route("api/v1/training-activities")]
public sealed class TrainingActivitiesController(IPilotService service) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TrainingActivityDto>> Get(Guid id, CancellationToken cancellationToken)
        => Ok(await service.GetActivityAsync(id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<TrainingActivityDto>> Create([FromBody] CreateTrainingActivityRequest request, CancellationToken cancellationToken)
        => Ok(await service.CreateActivityAsync(request, cancellationToken));

    [HttpPost("{id:guid}/participations")]
    public async Task<ActionResult<TrainingParticipationDto>> Register(
        Guid id, [FromBody] CreateTrainingParticipationRequest request, CancellationToken cancellationToken)
        => Ok(await service.RegisterParticipationAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/participations/{participationId:guid}/result")]
    public async Task<ActionResult<TrainingActivityResultDto>> RecordResult(
        Guid id, Guid participationId, [FromBody] RecordTrainingActivityResultRequest request, CancellationToken cancellationToken)
        => Ok(await service.RecordActivityResultAsync(id, participationId, request, cancellationToken));
}
