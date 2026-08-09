namespace IsSuas.Domain.Enums;

public enum MaintenanceTargetType
{
    Uas = 0,
    UasType = 1,
    Component = 2,
    Battery = 3
}

public enum MaintenanceTriggerType
{
    CalendarDate = 0,
    FlightCount = 1,
    FlightHours = 2,
    BatteryCycles = 3
}

public enum MaintenancePlanStatus
{
    Active = 0,
    Suspended = 1,
    Completed = 2,
    Overdue = 3
}

public enum WorkOrderStatus
{
    Open = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3
}

public enum DefectSeverity
{
    Low = 0,
    Medium = 1,
    High = 2,
    Blocking = 3
}

public enum DefectStatus
{
    Reported = 0,
    UnderRepair = 1,
    Resolved = 2,
    Closed = 3
}

public enum ServiceRequestStatus
{
    New = 0,
    Assessed = 1,
    Approved = 2,
    Rejected = 3,
    Realized = 4,
    Cancelled = 5
}

public enum ServiceRequestPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

public enum TechnicalRestrictionStatus
{
    Active = 0,
    Ended = 1,
    Cancelled = 2
}

public enum ReleaseToServiceDecision
{
    Released = 0,
    ReleasedWithRestriction = 1,
    NotReleased = 2
}

public enum ReleaseToServiceStatus
{
    Valid = 0,
    Superseded = 1,
    Cancelled = 2
}
