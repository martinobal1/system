using IsSuas.Domain.Common;
using IsSuas.Domain.Enums;
using IsSuas.Domain.Identity;
using IsSuas.Domain.Operations;
using IsSuas.Domain.Safety;

namespace IsSuas.Domain.Notifications;

/// <summary>ENT-094 — šablóna správy.</summary>
public class MessageTemplate : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string NameSk { get; set; } = string.Empty;
    public string SubjectTemplate { get; set; } = string.Empty;
    public string BodyTemplate { get; set; } = string.Empty;
    public NotificationChannel Channel { get; set; } = NotificationChannel.InApp;
    public string? Language { get; set; } = "sk";
}

/// <summary>ENT-092 — príjemca notifikácie.</summary>
public class NotificationRecipient : AuditableEntity
{
    public Guid AppNotificationId { get; set; }
    public AppNotification AppNotification { get; set; } = null!;
    public Guid? AccountId { get; set; }
    public Account? Account { get; set; }
    public string? RoleCode { get; set; }
    public Guid? OrgUnitId { get; set; }
    public NotificationStatus Status { get; set; } = NotificationStatus.Unread;
    public DateTimeOffset? ReadAt { get; set; }
}

/// <summary>ENT-093 — pokus o doručenie.</summary>
public class DeliveryAttempt : AuditableEntity
{
    public Guid AppNotificationId { get; set; }
    public AppNotification AppNotification { get; set; } = null!;
    public Guid? NotificationRecipientId { get; set; }
    public NotificationRecipient? NotificationRecipient { get; set; }
    public NotificationChannel Channel { get; set; } = NotificationChannel.InApp;
    public DeliveryAttemptStatus Status { get; set; } = DeliveryAttemptStatus.Pending;
    public DateTimeOffset AttemptedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public int AttemptNumber { get; set; } = 1;
}

/// <summary>ENT-098 — pravidlo eskalácie.</summary>
public class EscalationRule : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string NameSk { get; set; } = string.Empty;
    public EscalationTrigger Trigger { get; set; }
    public int ThresholdHours { get; set; }
    public string TargetRoleCode { get; set; } = string.Empty;
    public string? SourceTypeCode { get; set; }
    public string EscalationMessageTemplate { get; set; } = string.Empty;
}

/// <summary>ENT-097 — história pracovnej úlohy.</summary>
public class WorkTaskHistory : AuditableEntity
{
    public Guid WorkTaskId { get; set; }
    public WorkTask WorkTask { get; set; } = null!;
    public WorkTaskStatus? FromStatus { get; set; }
    public WorkTaskStatus ToStatus { get; set; }
    public Guid? ChangedByAccountId { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
}
