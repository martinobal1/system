namespace IsSuas.Domain.Enums;

public enum NotificationChannel
{
    InApp = 0,
    Email = 1,
    Sms = 2
}

public enum DeliveryAttemptStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2,
    Skipped = 3
}

public enum EscalationTrigger
{
    UnreadAfterHours = 0,
    TaskOverdueHours = 1,
    DeliveryFailed = 2
}
