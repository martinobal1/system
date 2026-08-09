namespace IsSuas.Domain.Enums;

public enum PilotStatus
{
    Draft = 0,
    Active = 1,
    Fit = 2,
    Expiring = 3,
    Unfit = 4,
    Suspended = 5,
    Inactive = 6
}

public enum CertificateStatus
{
    Draft = 0,
    Issued = 1,
    Suspended = 2,
    Expired = 3,
    Revoked = 4,
    Superseded = 5
}

public enum TrainingResult
{
    Passed = 0,
    Failed = 1,
    Incomplete = 2
}

public enum PersonFunctionAssignmentStatus
{
    Active = 0,
    Suspended = 1,
    Ended = 2
}

public enum PilotRestrictionStatus
{
    Active = 0,
    Ended = 1,
    Cancelled = 2
}

public enum QualificationVerificationResult
{
    Verified = 0,
    Rejected = 1,
    Inconclusive = 2
}

public enum CertificateScopeStatus
{
    Active = 0,
    Suspended = 1,
    Ended = 2
}

public enum TrainingCatalogStatus
{
    Draft = 0,
    Active = 1,
    Cancelled = 2
}

public enum TrainingActivityStatus
{
    Planned = 0,
    Open = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4
}

public enum TrainingRegistrationStatus
{
    Registered = 0,
    Confirmed = 1,
    Rejected = 2,
    Withdrawn = 3
}

public enum TrainingAttendanceStatus
{
    Present = 0,
    Partial = 1,
    Absent = 2
}

public enum TrainingCompletionStatus
{
    NotStarted = 0,
    InProgress = 1,
    Completed = 2,
    Incomplete = 3
}
