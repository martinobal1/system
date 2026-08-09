using IsSuas.Application.Documents;
using IsSuas.Domain.Enums;
using IsSuas.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsSuas.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/documents")]
public sealed class DocumentsController(IDocumentService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ManagedDocumentDto>>> List(
        [FromQuery] LinkedEntityType? entityType,
        [FromQuery] Guid? entityId,
        CancellationToken cancellationToken)
        => Ok(await service.ListDocumentsAsync(entityType, entityId, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<ManagedDocumentDto>> Upload([FromBody] UploadDocumentRequest request, CancellationToken cancellationToken)
        => Ok(await service.UploadAsync(request, cancellationToken));

    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var (content, fileName, contentType) = await service.DownloadContentAsync(id, cancellationToken);
        return File(content, contentType, fileName);
    }

    [HttpPost("refresh-expirations")]
    public async Task<ActionResult<object>> Refresh(CancellationToken cancellationToken)
        => Ok(new { expiredCount = await service.RefreshExpirationsAsync(cancellationToken) });
}

[ApiController]
[Authorize]
[Route("api/v1/retention-rules")]
public sealed class RetentionRulesController(IDocumentService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RetentionRuleDto>>> List(CancellationToken cancellationToken)
        => Ok(await service.ListRetentionRulesAsync(cancellationToken));
}

[ApiController]
[Authorize]
[Route("api/v1/document-dossiers")]
public sealed class DocumentDossiersController(IDocumentService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DocumentDossierDto>>> List(CancellationToken cancellationToken)
        => Ok(await service.ListDossiersAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<DocumentDossierDto>> Create([FromBody] CreateDocumentDossierRequest request, CancellationToken cancellationToken)
        => Ok(await service.CreateDossierAsync(request, cancellationToken));

    [HttpPost("{id:guid}/documents")]
    public async Task<ActionResult<DocumentDossierDto>> Add(Guid id, [FromBody] AddDossierDocumentRequest request, CancellationToken cancellationToken)
        => Ok(await service.AddToDossierAsync(id, request, cancellationToken));
}

[ApiController]
[Authorize]
[Route("api/v1/form-templates")]
public sealed class FormTemplatesController(IDocumentService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FormTemplateDto>>> List(CancellationToken cancellationToken)
        => Ok(await service.ListTemplatesAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<FormTemplateDto>> Create([FromBody] CreateFormTemplateRequest request, CancellationToken cancellationToken)
        => Ok(await service.CreateTemplateAsync(request, cancellationToken));
}

[ApiController]
[Authorize]
[Route("api/v1/form-submissions")]
public sealed class FormSubmissionsController(IDocumentService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FormSubmissionDto>>> List(
        [FromQuery] LinkedEntityType? entityType,
        [FromQuery] Guid? entityId,
        CancellationToken cancellationToken)
        => Ok(await service.ListSubmissionsAsync(entityType, entityId, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<FormSubmissionDto>> Start([FromBody] StartFormSubmissionRequest request, CancellationToken cancellationToken)
        => Ok(await service.StartSubmissionAsync(request, cancellationToken));

    [HttpPost("{id:guid}/answers")]
    public async Task<ActionResult<FormSubmissionDto>> Save(Guid id, [FromBody] SaveFormAnswersRequest request, CancellationToken cancellationToken)
        => Ok(await service.SaveAnswersAsync(id, request, cancellationToken));
}

[ApiController]
[Authorize]
[Route("api/v1/risk-assessments")]
public sealed class RiskAssessmentsController(IDocumentService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RiskAssessmentDto>>> List(
        [FromQuery] LinkedEntityType? entityType,
        [FromQuery] Guid? entityId,
        CancellationToken cancellationToken)
        => Ok(await service.ListRiskAssessmentsAsync(entityType, entityId, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<RiskAssessmentDto>> Create([FromBody] CreateRiskAssessmentRequest request, CancellationToken cancellationToken)
        => Ok(await service.CreateRiskAssessmentAsync(request, cancellationToken));
}

[ApiController]
[Authorize]
[Route("api/v1/operations/{operationId:guid}/document-block")]
public sealed class OperationDocumentBlockController(IDocumentService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DocumentBlockCheckDto>> Get(Guid operationId, CancellationToken cancellationToken)
        => Ok(await service.GetOperationDocumentBlockAsync(operationId, cancellationToken));
}
