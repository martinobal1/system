using IsSuas.Domain.Common;
using IsSuas.Domain.Enums;
using IsSuas.Domain.Identity;

namespace IsSuas.Domain.Operations;

public class Operation : AuditableEntity
{
    public string Title { get; set; } = string.Empty;
    public string? OperationTypeCode { get; set; }
    public OperationStatus Status { get; set; } = OperationStatus.Draft;
    public Guid OrgUnitId { get; set; }
    public OrgUnit OrgUnit { get; set; } = null!;
    public Guid ProposedByAccountId { get; set; }
    public DateTimeOffset PlannedStart { get; set; }
    public DateTimeOffset PlannedEnd { get; set; }
    public string? LocationAddress { get; set; }
    public double? LocationLat { get; set; }
    public double? LocationLon { get; set; }
    public string? LocationGeoJson { get; set; }
    public string? Description { get; set; }
    public bool IsUrgent { get; set; }
    public string? UrgentReason { get; set; }
    public int Version { get; set; } = 1;
    public bool IsSubmittedVersionLocked { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public Guid? DecidedByAccountId { get; set; }
    public string? DecisionReason { get; set; }
    public Guid? RecurrenceSourceOperationId { get; set; }
    public ICollection<OperationResource> Resources { get; set; } = new List<OperationResource>();
    public ICollection<OperationStatusHistory> StatusHistory { get; set; } = new List<OperationStatusHistory>();
    public ICollection<OperationApprovalStep> ApprovalSteps { get; set; } = new List<OperationApprovalStep>();
    public ICollection<OperationMapCheck> MapChecks { get; set; } = new List<OperationMapCheck>();
    public OperationRecurrenceRule? RecurrenceRule { get; set; }
}

/// <summary>ENT-124 — mapová kontrola letovej operácie.</summary>
public class OperationMapCheck : AuditableEntity
{
    public Guid OperationId { get; set; }
    public Operation Operation { get; set; } = null!;
    public string ProviderCode { get; set; } = "Offline";
    public string ResultCode { get; set; } = "CLEAR";
    public bool IsBlocking { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string? DetailsJson { get; set; }
    public DateTimeOffset CheckedAt { get; set; }
    public Guid? CheckedByAccountId { get; set; }
    public string? RequestGeoJson { get; set; }
    public long ElapsedMs { get; set; }
}

/// <summary>ENT-038 — pravidlo opakovania operácie.</summary>
public class OperationRecurrenceRule : AuditableEntity
{
    public Guid OperationId { get; set; }
    public Operation Operation { get; set; } = null!;
    public RecurrenceType RecurrenceType { get; set; } = RecurrenceType.Weekly;
    public int Interval { get; set; } = 1;
    public string? DaysOfWeek { get; set; }
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
    public int? MaximumOccurrences { get; set; }
    public DateTimeOffset? NextOccurrenceAt { get; set; }
    public int GeneratedCount { get; set; }
    public bool IsRuleActive { get; set; } = true;
}

/// <summary>ENT-043 — schvaľovací krok operácie.</summary>
public class OperationApprovalStep : AuditableEntity
{
    public Guid OperationId { get; set; }
    public Operation Operation { get; set; } = null!;
    public int SequenceNumber { get; set; }
    public string StepTypeCode { get; set; } = "APPROVAL";
    public Guid? AssignedAccountId { get; set; }
    public string? AssignedRoleCode { get; set; }
    public bool IsMandatory { get; set; } = true;
    public ApprovalStepStatus Status { get; set; } = ApprovalStepStatus.Pending;
    public DateTimeOffset? AssignedAt { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public OperationDecision? Decision { get; set; }
}

/// <summary>ENT-044 — rozhodnutie o operácii (viazané na krok).</summary>
public class OperationDecision : AuditableEntity
{
    public Guid ApprovalStepId { get; set; }
    public OperationApprovalStep ApprovalStep { get; set; } = null!;
    public OperationDecisionCode DecisionCode { get; set; }
    public DateTimeOffset DecidedAt { get; set; }
    public Guid DecidedByAccountId { get; set; }
    public string Justification { get; set; } = string.Empty;
    public DateTimeOffset? ValidUntil { get; set; }
    public ICollection<OperationApprovalCondition> Conditions { get; set; } = new List<OperationApprovalCondition>();
}

/// <summary>ENT-045 — podmienka schválenia.</summary>
public class OperationApprovalCondition : AuditableEntity
{
    public Guid DecisionId { get; set; }
    public OperationDecision Decision { get; set; } = null!;
    public string ConditionTypeCode { get; set; } = "GENERAL";
    public string Description { get; set; } = string.Empty;
    public bool MustBeMetBeforeFlight { get; set; } = true;
    public ApprovalConditionStatus Status { get; set; } = ApprovalConditionStatus.Unmet;
    public DateTimeOffset? DueAt { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
    public Guid? VerifiedByAccountId { get; set; }
    public string? VerificationNote { get; set; }
}

public class OperationResource : AuditableEntity
{
    public Guid OperationId { get; set; }
    public Operation Operation { get; set; } = null!;
    public OperationResourceType ResourceType { get; set; }
    public Guid ResourceId { get; set; }
    public string? RoleCode { get; set; }
    public string? DisplayName { get; set; }
}

public class OperationStatusHistory : AuditableEntity
{
    public Guid OperationId { get; set; }
    public Operation Operation { get; set; } = null!;
    public OperationStatus FromStatus { get; set; }
    public OperationStatus ToStatus { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset ChangedAt { get; set; }
}

public class WorkTask : AuditableEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public WorkTaskStatus Status { get; set; } = WorkTaskStatus.Open;
    public Guid? OrgUnitId { get; set; }
    public Guid? AssigneeAccountId { get; set; }
    public Guid? RelatedOperationId { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public string TaskTypeCode { get; set; } = "GENERAL";
}

public static class OperationStateMachine
{
    private static readonly Dictionary<OperationStatus, HashSet<OperationStatus>> Transitions = new()
    {
        [OperationStatus.Draft] = [OperationStatus.Submitted, OperationStatus.Cancelled],
        [OperationStatus.Returned] = [OperationStatus.Submitted, OperationStatus.Cancelled],
        [OperationStatus.Submitted] = [OperationStatus.Approved, OperationStatus.Returned, OperationStatus.Rejected],
        [OperationStatus.Approved] = [OperationStatus.Cancelled],
        [OperationStatus.Rejected] = [],
        [OperationStatus.Cancelled] = []
    };

    public static bool CanTransition(OperationStatus from, OperationStatus to) =>
        Transitions.TryGetValue(from, out var allowed) && allowed.Contains(to);
}
