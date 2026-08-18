namespace IsSuas.Domain.Enums;

public enum OperationStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Returned = 3,
    Rejected = 4,
    Cancelled = 5,
    /// <summary>L1/L2 potvrdili, že schválený let sa naozaj uskutočnil (záznam v denníku).</summary>
    Completed = 6
}

public enum OperationResourceType
{
    Uas = 0,
    Pilot = 1,
    Battery = 2,
    Component = 3,
    Personnel = 4
}

public enum WorkTaskStatus
{
    Open = 0,
    InProgress = 1,
    Done = 2,
    Cancelled = 3
}

public enum ApprovalStepStatus
{
    Pending = 0,
    InProgress = 1,
    Approved = 2,
    Rejected = 3,
    Skipped = 4,
    Cancelled = 5
}

public enum OperationDecisionCode
{
    Approved = 0,
    ConditionallyApproved = 1,
    Rejected = 2,
    Returned = 3
}

public enum ApprovalConditionStatus
{
    Unmet = 0,
    Met = 1,
    Unmettable = 2,
    Cancelled = 3
}

public enum RecurrenceType
{
    Daily = 0,
    Weekly = 1,
    Monthly = 2
}
