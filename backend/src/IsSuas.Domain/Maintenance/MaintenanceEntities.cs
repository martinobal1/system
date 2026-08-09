using IsSuas.Domain.Common;
using IsSuas.Domain.Enums;
using IsSuas.Domain.Fleet;

namespace IsSuas.Domain.Maintenance;

public class MaintenancePlan : AuditableEntity
{
    public string NameSk { get; set; } = string.Empty;
    public MaintenanceTargetType TargetType { get; set; }
    public Guid? TargetId { get; set; }
    public string? TargetTypeCode { get; set; }
    public MaintenanceTriggerType TriggerType { get; set; }
    public int? IntervalValue { get; set; }
    public DateTimeOffset? NextDueAt { get; set; }
    public int? NextDueCounter { get; set; }
    public int WarningThresholdPercent { get; set; } = 80;
    public bool BlockWhenOverdue { get; set; } = true;
    public MaintenancePlanStatus Status { get; set; } = MaintenancePlanStatus.Active;
    public int CurrentCounter { get; set; }
    public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
}

public class WorkOrder : AuditableEntity
{
    public Guid? MaintenancePlanId { get; set; }
    public MaintenancePlan? MaintenancePlan { get; set; }
    public Guid? ServiceRequestId { get; set; }
    public ServiceRequest? ServiceRequest { get; set; }
    public MaintenanceTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public WorkOrderStatus Status { get; set; } = WorkOrderStatus.Open;
    public Guid? AssignedTechnicianAccountId { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public MaintenanceRecord? Record { get; set; }
}

public class MaintenanceRecord : AuditableEntity
{
    public Guid WorkOrderId { get; set; }
    public WorkOrder WorkOrder { get; set; } = null!;
    public string WorkScope { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public DateTimeOffset PerformedAt { get; set; }
    public Guid PerformedByAccountId { get; set; }
    public string? PartsUsed { get; set; }
    public string? DocumentationNotes { get; set; }
    public bool AirworthinessRestored { get; set; }
}

/// <summary>ENT-057 — servisná požiadavka.</summary>
public class ServiceRequest : AuditableEntity
{
    public string RequestNumber { get; set; } = string.Empty;
    public string RequestTypeCode { get; set; } = "REPAIR";
    public string SourceTypeCode { get; set; } = "MANUAL";
    public Guid? DefectId { get; set; }
    public Defect? Defect { get; set; }
    public Guid? SourceFlightId { get; set; }
    public MaintenanceTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public string Description { get; set; } = string.Empty;
    public ServiceRequestPriority Priority { get; set; } = ServiceRequestPriority.Normal;
    public DateTimeOffset RequestedAt { get; set; }
    public Guid RequestedByAccountId { get; set; }
    public DateTimeOffset? RequiredCompletionAt { get; set; }
    public ServiceRequestStatus Status { get; set; } = ServiceRequestStatus.New;
    public string? RejectionReason { get; set; }
    public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
}

/// <summary>ENT-065 — technické obmedzenie.</summary>
public class TechnicalRestriction : AuditableEntity
{
    public Guid? DefectId { get; set; }
    public Defect? Defect { get; set; }
    public MaintenanceTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public string RestrictionTypeCode { get; set; } = "OPERATIONAL";
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
    public bool BlocksOperation { get; set; }
    public Guid ApprovedByAccountId { get; set; }
    public TechnicalRestrictionStatus Status { get; set; } = TechnicalRestrictionStatus.Active;
    public string? TerminationReason { get; set; }
}

/// <summary>ENT-067 — uvoľnenie do prevádzky.</summary>
public class ReleaseToService : AuditableEntity
{
    public Guid? WorkOrderId { get; set; }
    public WorkOrder? WorkOrder { get; set; }
    public Guid? DefectId { get; set; }
    public Defect? Defect { get; set; }
    public MaintenanceTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public ReleaseToServiceDecision Decision { get; set; }
    public string ReleaseScopeCode { get; set; } = "FULL";
    public string? RestrictionDescription { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
    public Guid ReleasedByAccountId { get; set; }
    public string DecisionReason { get; set; } = string.Empty;
    public ReleaseToServiceStatus Status { get; set; } = ReleaseToServiceStatus.Valid;
}

/// <summary>ENT-066 — história technického stavu.</summary>
public class TechnicalStatusHistory : AuditableEntity
{
    public MaintenanceTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public string? PreviousStatusCode { get; set; }
    public string NewStatusCode { get; set; } = string.Empty;
    public DateTimeOffset EffectiveAt { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid? SourceDefectId { get; set; }
    public Guid? SourceWorkOrderId { get; set; }
    public Guid? SourceReleaseId { get; set; }
    public Guid ChangedByAccountId { get; set; }
}

public class Defect : AuditableEntity
{
    public MaintenanceTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public string Title { get; set; } = string.Empty;
    /// <summary>Immutable original report text after submit.</summary>
    public string OriginalReport { get; set; } = string.Empty;
    public bool OriginalReportLocked { get; set; }
    public DefectSeverity Severity { get; set; } = DefectSeverity.Medium;
    public DefectStatus Status { get; set; } = DefectStatus.Reported;
    public bool BlocksOperations { get; set; }
    public string? TemporaryMeasure { get; set; }
    public Guid ReportedByAccountId { get; set; }
    public Guid? ResponsibleAccountId { get; set; }
    public Guid? RelatedFlightId { get; set; }
    public Guid? RelatedUasId { get; set; }
    public string? ResolutionNotes { get; set; }
    public string? TechnicalAssessment { get; set; }
    public bool TestPerformed { get; set; }
    public Guid? ResolvedByAccountId { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string DefectTypeCode { get; set; } = "GENERAL";
}

public static class MaintenanceRules
{
    public static bool IsPlanOverdue(MaintenancePlan plan, DateTimeOffset now) =>
        plan.Status != MaintenancePlanStatus.Completed
        && plan.Status != MaintenancePlanStatus.Suspended
        && (
            (plan.NextDueAt is not null && plan.NextDueAt < now)
            || (plan.NextDueCounter is not null && plan.CurrentCounter >= plan.NextDueCounter)
        );

    public static bool ShouldWarn(MaintenancePlan plan, DateTimeOffset now)
    {
        if (plan.Status is MaintenancePlanStatus.Completed or MaintenancePlanStatus.Suspended)
            return false;
        if (plan.NextDueAt is not null)
        {
            var total = (plan.NextDueAt.Value - plan.CreatedAt).TotalDays;
            if (total <= 0) return IsPlanOverdue(plan, now);
            var remaining = (plan.NextDueAt.Value - now).TotalDays;
            var usedPercent = 100 - (remaining / total * 100);
            return usedPercent >= plan.WarningThresholdPercent;
        }

        if (plan.NextDueCounter is not null && plan.IntervalValue is > 0)
        {
            var used = plan.CurrentCounter - (plan.NextDueCounter.Value - plan.IntervalValue.Value);
            var percent = used * 100.0 / plan.IntervalValue.Value;
            return percent >= plan.WarningThresholdPercent;
        }

        return false;
    }
}
