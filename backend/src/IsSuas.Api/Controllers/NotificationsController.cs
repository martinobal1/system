using IsSuas.Application.Notifications;
using IsSuas.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsSuas.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/notifications")]
public sealed class NotificationsController(INotificationService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationDetailDto>>> List(CancellationToken cancellationToken)
        => Ok(await service.ListAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NotificationDetailDto>> Get(Guid id, CancellationToken cancellationToken)
        => Ok(await service.GetAsync(id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<NotificationDetailDto>> Publish([FromBody] PublishNotificationRequest request, CancellationToken cancellationToken)
        => Ok(await service.PublishAsync(request, cancellationToken));

    [HttpPost("{id:guid}/mark-read")]
    public async Task<ActionResult<NotificationDetailDto>> MarkRead(Guid id, [FromBody] MarkNotificationReadRequest request, CancellationToken cancellationToken)
        => Ok(await service.MarkReadAsync(id, request, cancellationToken));

    [HttpPost("run-escalations")]
    public async Task<ActionResult<object>> RunEscalations(CancellationToken cancellationToken)
        => Ok(new { created = await service.RunEscalationsAsync(cancellationToken) });
}

[ApiController]
[Authorize]
[Route("api/v1/message-templates")]
public sealed class MessageTemplatesController(INotificationService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MessageTemplateDto>>> List(CancellationToken cancellationToken)
        => Ok(await service.ListTemplatesAsync(cancellationToken));
}

[ApiController]
[Authorize]
[Route("api/v1/escalation-rules")]
public sealed class EscalationRulesController(INotificationService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EscalationRuleDto>>> List(CancellationToken cancellationToken)
        => Ok(await service.ListEscalationRulesAsync(cancellationToken));
}
