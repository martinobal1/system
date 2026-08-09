using IsSuas.Domain.Enums;

namespace IsSuas.Application.Safety;

public sealed record SafetyEventDto(
    Guid Id,
    string Title,
    string InitialReport,
    bool InitialReportLocked,
    SafetySeverity Severity,
    SafetyEventStatus Status,
    Guid ReportedByAccountId,
    Guid? RelatedUasId,
    Guid? RelatedFlightId,
    Guid? RelatedOperationId,
    DateTimeOffset OccurredAt,
    bool AutoSuspendApplied,
    bool IsActive,
    uint RowVersion);

public sealed record SafetyEventDetailDto(
    SafetyEventDto Event,
    string? LocationDescription,
    string? Findings,
    string? Causes,
    string? ContributingFactors,
    string? Recommendations,
    string? ClosureNotes,
    string? ClosureExceptionReason,
    IReadOnlyList<SafetyEvidenceDto> Evidences,
    IReadOnlyList<CorrectiveActionDto> Actions,
    IReadOnlyList<SafetyEventParticipantDto> Participants,
    IReadOnlyList<SafetyEventAssetDto> Assets,
    IReadOnlyList<SafetyImmediateActionDto> ImmediateActions,
    IReadOnlyList<SafetyInvestigationDto> Investigations,
    IReadOnlyList<SafetyReportingObligationDto> ReportingObligations,
    IReadOnlyList<FleetSafetyAlertDto> FleetAlerts,
    IReadOnlyList<SafetyStatusHistoryDto> StatusHistory,
    IReadOnlyList<SafetyClassificationHistoryDto> ClassificationHistory);

public sealed record CreateSafetyEventRequest(
    string Title,
    string InitialReport,
    SafetySeverity Severity,
    DateTimeOffset OccurredAt,
    Guid? OrgUnitId,
    Guid? RelatedFlightId,
    Guid? RelatedOperationId,
    Guid? RelatedUasId,
    string? LocationDescription);

public sealed record SubmitSafetyEventRequest(uint RowVersion);

public sealed record UpdateInvestigationRequest(
    string? Findings,
    string? Causes,
    string? ContributingFactors,
    string? Recommendations,
    uint RowVersion);

public sealed record ChangeSafetySeverityRequest(SafetySeverity Severity, string ChangeReason, uint RowVersion);

public sealed record AddEvidenceRequest(
    string FileName,
    string ContentHash,
    string ContentType,
    string? Description,
    bool IsSensitive,
    Guid? SupersedesEvidenceId);

public sealed record CreateCorrectiveActionRequest(
    string Title,
    string? Description,
    Guid OwnerAccountId,
    DateTimeOffset DueAt,
    bool IsMandatory);

public sealed record CompleteCorrectiveActionRequest(string CompletionEvidence, uint RowVersion);

public sealed record VerifyCorrectiveActionRequest(uint RowVersion);

public sealed record WaiveCorrectiveActionRequest(string WaiverReason, uint RowVersion);

public sealed record CloseSafetyEventRequest(string ClosureNotes, string? ClosureExceptionReason, uint RowVersion);

public sealed record SafetyEvidenceDto(
    Guid Id,
    string FileName,
    string ContentHash,
    string ContentType,
    int Version,
    bool IsCurrent,
    string? Description,
    bool IsSensitive);

public sealed record CorrectiveActionDto(
    Guid Id,
    string Title,
    string? Description,
    Guid OwnerAccountId,
    DateTimeOffset DueAt,
    CorrectiveActionStatus Status,
    string? CompletionEvidence,
    Guid? VerifiedByAccountId,
    bool IsMandatory,
    uint RowVersion);

public sealed record SafetyEventParticipantDto(
    Guid Id,
    Guid PersonId,
    string ParticipantRoleCode,
    string? InvolvementDescription,
    string? InjuryStatusCode,
    bool StatementObtained,
    DateTimeOffset? NotifiedAt,
    string? Notes,
    bool IsActive);

public sealed record CreateSafetyEventParticipantRequest(
    Guid PersonId,
    string ParticipantRoleCode,
    string? InvolvementDescription,
    string? InjuryStatusCode,
    bool StatementObtained,
    DateTimeOffset? NotifiedAt,
    string? Notes);

public sealed record SafetyEventAssetDto(
    Guid Id,
    Guid? UasId,
    Guid? ComponentId,
    Guid? BatteryId,
    string AssetRoleCode,
    string DamageStatusCode,
    string? DamageDescription,
    string TechnicalStatusAfterEvent,
    bool SecuredAsEvidence,
    string? Notes,
    bool IsActive);

public sealed record CreateSafetyEventAssetRequest(
    Guid? UasId,
    Guid? ComponentId,
    Guid? BatteryId,
    string AssetRoleCode,
    string DamageStatusCode,
    string? DamageDescription,
    string TechnicalStatusAfterEvent,
    bool SecuredAsEvidence,
    string? Notes);

public sealed record SafetyImmediateActionDto(
    Guid Id,
    string ActionTypeCode,
    string ActionDescription,
    DateTimeOffset PerformedAt,
    Guid PerformedByPersonId,
    string? ResultDescription,
    bool FollowUpRequired,
    bool IsActive);

public sealed record CreateSafetyImmediateActionRequest(
    string ActionTypeCode,
    string ActionDescription,
    DateTimeOffset PerformedAt,
    Guid PerformedByPersonId,
    string? ResultDescription,
    bool FollowUpRequired);

public sealed record SafetyCauseDto(
    Guid Id,
    Guid InvestigationId,
    string CauseCategoryCode,
    string CauseDescription,
    bool IsRootCause,
    SafetyCauseStatus Status,
    string? ConfidenceLevelCode,
    string? SupportingEvidence,
    DateTimeOffset? ConfirmedAt,
    Guid? ConfirmedByPersonId,
    bool IsActive);

public sealed record CreateSafetyCauseRequest(
    string CauseCategoryCode,
    string CauseDescription,
    bool IsRootCause,
    SafetyCauseStatus Status,
    string? ConfidenceLevelCode,
    string? SupportingEvidence);

public sealed record SafetyInvestigationDto(
    Guid Id,
    string InvestigationNumber,
    string InvestigationTypeCode,
    Guid ResponsiblePersonId,
    DateTimeOffset OpenedAt,
    DateTimeOffset? PlannedCompletionDate,
    DateTimeOffset? ClosedAt,
    string ScopeDescription,
    SafetyInvestigationStatus Status,
    string? FindingsSummary,
    string? Conclusion,
    string? InterruptionReason,
    bool IsActive,
    IReadOnlyList<SafetyCauseDto> Causes);

public sealed record CreateSafetyInvestigationRequest(
    string InvestigationNumber,
    string InvestigationTypeCode,
    Guid ResponsiblePersonId,
    string ScopeDescription,
    DateTimeOffset? PlannedCompletionDate);

public sealed record SafetyReportingObligationDto(
    Guid Id,
    string ObligationTypeCode,
    string RecipientAuthority,
    string? LegalBasis,
    DateTimeOffset ReportingDeadline,
    SafetyReportingObligationStatus Status,
    DateTimeOffset? ReportedAt,
    Guid? ReportedByPersonId,
    string? ExternalReferenceNumber,
    string? DelayReason,
    string? CancellationReason,
    bool IsActive);

public sealed record CreateSafetyReportingObligationRequest(
    string ObligationTypeCode,
    string RecipientAuthority,
    string? LegalBasis,
    DateTimeOffset ReportingDeadline);

public sealed record FulfillSafetyReportingObligationRequest(
    Guid? ReportedByPersonId,
    string? ExternalReferenceNumber,
    string? DelayReason);

public sealed record FleetSafetyAlertDto(
    Guid Id,
    string AlertNumber,
    string Title,
    string Message,
    string SeverityCode,
    string ScopeCode,
    string? AffectedUasTypeCode,
    FleetSafetyAlertStatus Status,
    DateTimeOffset? PublishedAt,
    Guid? ApprovedByAccountId,
    string? WithdrawalReason,
    bool IsActive);

public sealed record CreateFleetSafetyAlertRequest(
    string AlertNumber,
    string Title,
    string Message,
    string SeverityCode,
    string ScopeCode,
    string? AffectedUasTypeCode,
    bool PublishImmediately);

public sealed record SafetyStatusHistoryDto(
    Guid Id,
    SafetyEventStatus? PreviousStatus,
    SafetyEventStatus NewStatus,
    DateTimeOffset EffectiveAt,
    string Reason);

public sealed record SafetyClassificationHistoryDto(
    Guid Id,
    string ClassificationTypeCode,
    string? PreviousValueCode,
    string NewValueCode,
    DateTimeOffset EffectiveAt,
    string ChangeReason,
    Guid ChangedByAccountId);

public sealed record AppNotificationDto(
    Guid Id,
    string Title,
    string Message,
    string TypeCode,
    string Priority,
    Guid? RelatedSafetyEventId,
    NotificationStatus Status,
    DateTimeOffset CreatedAt);
