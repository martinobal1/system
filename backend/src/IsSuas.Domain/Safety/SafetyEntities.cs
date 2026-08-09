using IsSuas.Domain.Common;
using IsSuas.Domain.Enums;
using IsSuas.Domain.Fleet;
using IsSuas.Domain.Identity;

namespace IsSuas.Domain.Safety;

public class SafetyEvent : AuditableEntity
{
    public string Title { get; set; } = string.Empty;
    /// <summary>Immutable initial report after submit.</summary>
    public string InitialReport { get; set; } = string.Empty;
    public bool InitialReportLocked { get; set; }
    public SafetySeverity Severity { get; set; } = SafetySeverity.Medium;
    public SafetyEventStatus Status { get; set; } = SafetyEventStatus.Draft;
    public Guid ReportedByAccountId { get; set; }
    public Guid? OrgUnitId { get; set; }
    public OrgUnit? OrgUnit { get; set; }
    public Guid? RelatedFlightId { get; set; }
    public Guid? RelatedOperationId { get; set; }
    public Guid? RelatedUasId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string? LocationDescription { get; set; }
    public string? Findings { get; set; }
    public string? Causes { get; set; }
    public string? ContributingFactors { get; set; }
    public string? Recommendations { get; set; }
    public string? ClosureNotes { get; set; }
    public string? ClosureExceptionReason { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public Guid? ClosedByAccountId { get; set; }
    public bool AutoSuspendApplied { get; set; }
    public ICollection<SafetyEvidence> Evidences { get; set; } = new List<SafetyEvidence>();
    public ICollection<CorrectiveAction> CorrectiveActions { get; set; } = new List<CorrectiveAction>();
    public ICollection<SafetyEventParticipant> Participants { get; set; } = new List<SafetyEventParticipant>();
    public ICollection<SafetyEventAsset> Assets { get; set; } = new List<SafetyEventAsset>();
    public ICollection<SafetyImmediateAction> ImmediateActions { get; set; } = new List<SafetyImmediateAction>();
    public ICollection<SafetyInvestigation> Investigations { get; set; } = new List<SafetyInvestigation>();
    public ICollection<SafetyReportingObligation> ReportingObligations { get; set; } = new List<SafetyReportingObligation>();
    public ICollection<FleetSafetyAlert> FleetAlerts { get; set; } = new List<FleetSafetyAlert>();
    public ICollection<SafetyStatusHistory> StatusHistory { get; set; } = new List<SafetyStatusHistory>();
    public ICollection<SafetyClassificationHistory> ClassificationHistory { get; set; } = new List<SafetyClassificationHistory>();
}

public class SafetyEvidence : AuditableEntity
{
    public Guid SafetyEventId { get; set; }
    public SafetyEvent SafetyEvent { get; set; } = null!;
    public string FileName { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public int Version { get; set; } = 1;
    public Guid? SupersedesEvidenceId { get; set; }
    public bool IsCurrent { get; set; } = true;
    public string? Description { get; set; }
    public bool IsSensitive { get; set; }
}

public class CorrectiveAction : AuditableEntity
{
    public Guid SafetyEventId { get; set; }
    public SafetyEvent SafetyEvent { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid OwnerAccountId { get; set; }
    public DateTimeOffset DueAt { get; set; }
    public CorrectiveActionStatus Status { get; set; } = CorrectiveActionStatus.Open;
    public string? CompletionEvidence { get; set; }
    public Guid? VerifiedByAccountId { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
    public string? WaiverReason { get; set; }
    public bool IsMandatory { get; set; } = true;
}

/// <summary>ENT-069 — účastník bezpečnostnej udalosti.</summary>
public class SafetyEventParticipant : AuditableEntity
{
    public Guid SafetyEventId { get; set; }
    public SafetyEvent SafetyEvent { get; set; } = null!;
    public Guid PersonId { get; set; }
    public Person Person { get; set; } = null!;
    public string ParticipantRoleCode { get; set; } = "WITNESS";
    public string? InvolvementDescription { get; set; }
    public string? InjuryStatusCode { get; set; }
    public bool StatementObtained { get; set; }
    public DateTimeOffset? NotifiedAt { get; set; }
    public string? Notes { get; set; }
}

/// <summary>ENT-070 — technický prostriedok udalosti.</summary>
public class SafetyEventAsset : AuditableEntity
{
    public Guid SafetyEventId { get; set; }
    public SafetyEvent SafetyEvent { get; set; } = null!;
    public Guid? UasId { get; set; }
    public Uas? Uas { get; set; }
    public Guid? ComponentId { get; set; }
    public Component? Component { get; set; }
    public Guid? BatteryId { get; set; }
    public Battery? Battery { get; set; }
    public string AssetRoleCode { get; set; } = "PRIMARY";
    public string DamageStatusCode { get; set; } = "UNKNOWN";
    public string? DamageDescription { get; set; }
    public string TechnicalStatusAfterEvent { get; set; } = "UNKNOWN";
    public bool SecuredAsEvidence { get; set; }
    public string? Notes { get; set; }
}

/// <summary>ENT-071 — bezprostredné opatrenie.</summary>
public class SafetyImmediateAction : AuditableEntity
{
    public Guid SafetyEventId { get; set; }
    public SafetyEvent SafetyEvent { get; set; } = null!;
    public string ActionTypeCode { get; set; } = "SECURE";
    public string ActionDescription { get; set; } = string.Empty;
    public DateTimeOffset PerformedAt { get; set; }
    public Guid PerformedByPersonId { get; set; }
    public Person PerformedByPerson { get; set; } = null!;
    public string? ResultDescription { get; set; }
    public bool FollowUpRequired { get; set; }
}

/// <summary>ENT-073 — preverovanie/vyšetrovanie.</summary>
public class SafetyInvestigation : AuditableEntity
{
    public Guid SafetyEventId { get; set; }
    public SafetyEvent SafetyEvent { get; set; } = null!;
    public string InvestigationNumber { get; set; } = string.Empty;
    public string InvestigationTypeCode { get; set; } = "INTERNAL";
    public Guid ResponsiblePersonId { get; set; }
    public Person ResponsiblePerson { get; set; } = null!;
    public DateTimeOffset OpenedAt { get; set; }
    public DateTimeOffset? PlannedCompletionDate { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public string ScopeDescription { get; set; } = string.Empty;
    public SafetyInvestigationStatus Status { get; set; } = SafetyInvestigationStatus.InProgress;
    public string? FindingsSummary { get; set; }
    public string? Conclusion { get; set; }
    public string? InterruptionReason { get; set; }
    public ICollection<SafetyCause> Causes { get; set; } = new List<SafetyCause>();
}

/// <summary>ENT-074 — príčina udalosti.</summary>
public class SafetyCause : AuditableEntity
{
    public Guid InvestigationId { get; set; }
    public SafetyInvestigation Investigation { get; set; } = null!;
    public string CauseCategoryCode { get; set; } = "TECHNICAL";
    public string CauseDescription { get; set; } = string.Empty;
    public bool IsRootCause { get; set; }
    public SafetyCauseStatus Status { get; set; } = SafetyCauseStatus.Suspected;
    public string? ConfidenceLevelCode { get; set; }
    public string? SupportingEvidence { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public Guid? ConfirmedByPersonId { get; set; }
    public Person? ConfirmedByPerson { get; set; }
}

/// <summary>ENT-075 — oznamovacia povinnosť.</summary>
public class SafetyReportingObligation : AuditableEntity
{
    public Guid SafetyEventId { get; set; }
    public SafetyEvent SafetyEvent { get; set; } = null!;
    public string ObligationTypeCode { get; set; } = "EXTERNAL";
    public string RecipientAuthority { get; set; } = string.Empty;
    public string? LegalBasis { get; set; }
    public DateTimeOffset ReportingDeadline { get; set; }
    public SafetyReportingObligationStatus Status { get; set; } = SafetyReportingObligationStatus.Pending;
    public DateTimeOffset? ReportedAt { get; set; }
    public Guid? ReportedByPersonId { get; set; }
    public Person? ReportedByPerson { get; set; }
    public string? ExternalReferenceNumber { get; set; }
    public string? DelayReason { get; set; }
    public string? CancellationReason { get; set; }
}

/// <summary>ENT-077 — história klasifikácie.</summary>
public class SafetyClassificationHistory : AuditableEntity
{
    public Guid SafetyEventId { get; set; }
    public SafetyEvent SafetyEvent { get; set; } = null!;
    public string ClassificationTypeCode { get; set; } = "SEVERITY";
    public string? PreviousValueCode { get; set; }
    public string NewValueCode { get; set; } = string.Empty;
    public DateTimeOffset EffectiveAt { get; set; }
    public string ChangeReason { get; set; } = string.Empty;
    public Guid ChangedByAccountId { get; set; }
}

/// <summary>ENT-078 — história stavu.</summary>
public class SafetyStatusHistory : AuditableEntity
{
    public Guid SafetyEventId { get; set; }
    public SafetyEvent SafetyEvent { get; set; } = null!;
    public SafetyEventStatus? PreviousStatus { get; set; }
    public SafetyEventStatus NewStatus { get; set; }
    public DateTimeOffset EffectiveAt { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid ChangedByAccountId { get; set; }
}

/// <summary>ENT-079 — bezpečnostné upozornenie flotily.</summary>
public class FleetSafetyAlert : AuditableEntity
{
    public Guid SafetyEventId { get; set; }
    public SafetyEvent SafetyEvent { get; set; } = null!;
    public string AlertNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string SeverityCode { get; set; } = "HIGH";
    public string ScopeCode { get; set; } = "FLEET";
    public string? AffectedUasTypeCode { get; set; }
    public FleetSafetyAlertStatus Status { get; set; } = FleetSafetyAlertStatus.Draft;
    public DateTimeOffset? PublishedAt { get; set; }
    public Guid? ApprovedByAccountId { get; set; }
    public string? WithdrawalReason { get; set; }
}

public class AppNotification : AuditableEntity
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string TypeCode { get; set; } = "GENERAL";
    public string Priority { get; set; } = "Normal";
    public Guid? RecipientAccountId { get; set; }
    public string? RecipientRoleCode { get; set; }
    public Guid? RelatedSafetyEventId { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
    public NotificationStatus Status { get; set; } = NotificationStatus.Unread;
}
