namespace IsSuas.Domain.Enums;

public enum SafetyEventStatus
{
    Draft = 0,
    Submitted = 1,
    UnderInvestigation = 2,
    ActionsInProgress = 3,
    Closed = 4,
    Cancelled = 5
}

public enum SafetySeverity
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public enum CorrectiveActionStatus
{
    Open = 0,
    Done = 1,
    Verified = 2,
    Waived = 3
}

public enum NotificationStatus
{
    Unread = 0,
    Read = 1,
    Resolved = 2
}

public enum SafetyInvestigationStatus
{
    Planned = 0,
    InProgress = 1,
    Interrupted = 2,
    Completed = 3,
    Cancelled = 4
}

public enum SafetyCauseStatus
{
    Suspected = 0,
    Probable = 1,
    Confirmed = 2,
    Excluded = 3
}

public enum SafetyReportingObligationStatus
{
    Pending = 0,
    Preparing = 1,
    Fulfilled = 2,
    Delayed = 3,
    Cancelled = 4
}

public enum FleetSafetyAlertStatus
{
    Draft = 0,
    Published = 1,
    Withdrawn = 2
}
