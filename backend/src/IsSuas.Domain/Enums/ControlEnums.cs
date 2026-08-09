namespace IsSuas.Domain.Enums;

public enum ControlAuditType
{
    InternalControl = 0,
    InternalAudit = 1,
    ExternalAudit = 2,
    Inspection = 3
}

public enum ControlAuditStatus
{
    Planned = 0,
    InProgress = 1,
    FindingsReview = 2,
    Closed = 3,
    Cancelled = 4
}

public enum ControlCheckpointResultStatus
{
    Pending = 0,
    Compliant = 1,
    NonCompliant = 2,
    NotApplicable = 3
}

public enum ControlFindingSeverity
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public enum ControlActionStatus
{
    Open = 0,
    InProgress = 1,
    Completed = 2,
    Verified = 3,
    Waived = 4
}
