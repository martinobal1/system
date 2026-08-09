using IsSuas.Domain.Enums;

namespace IsSuas.Application.Notifications;

public sealed record MessageTemplateDto(
    Guid Id,
    string Code,
    string NameSk,
    string SubjectTemplate,
    string BodyTemplate,
    NotificationChannel Channel,
    string? Language);

public sealed record NotificationRecipientDto(
    Guid Id,
    Guid? AccountId,
    string? RoleCode,
    NotificationStatus Status,
    DateTimeOffset? ReadAt);

public sealed record DeliveryAttemptDto(
    Guid Id,
    NotificationChannel Channel,
    DeliveryAttemptStatus Status,
    DateTimeOffset AttemptedAt,
    string? ErrorMessage,
    int AttemptNumber);

public sealed record NotificationDetailDto(
    Guid Id,
    string Title,
    string Message,
    string TypeCode,
    string Priority,
    Guid? RelatedSafetyEventId,
    string? RelatedEntityType,
    Guid? RelatedEntityId,
    NotificationStatus Status,
    DateTimeOffset CreatedAt,
    uint RowVersion,
    IReadOnlyList<NotificationRecipientDto> Recipients,
    IReadOnlyList<DeliveryAttemptDto> DeliveryAttempts);

public sealed record PublishNotificationRequest(
    string? TemplateCode,
    string Title,
    string Message,
    string TypeCode,
    string Priority,
    Guid? RecipientAccountId,
    string? RecipientRoleCode,
    Guid? RelatedSafetyEventId,
    string? RelatedEntityType,
    Guid? RelatedEntityId,
    NotificationChannel Channel = NotificationChannel.InApp);

public sealed record MarkNotificationReadRequest(uint RowVersion);

public sealed record EscalationRuleDto(
    Guid Id,
    string Code,
    string NameSk,
    EscalationTrigger Trigger,
    int ThresholdHours,
    string TargetRoleCode,
    string? SourceTypeCode);

public sealed record WorkTaskHistoryDto(
    Guid Id,
    Guid WorkTaskId,
    WorkTaskStatus? FromStatus,
    WorkTaskStatus ToStatus,
    Guid? ChangedByAccountId,
    string? Note,
    DateTimeOffset ChangedAt);

public sealed record UpdateWorkTaskStatusRequest(WorkTaskStatus Status, string? Note, uint RowVersion);
