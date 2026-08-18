using IsSuas.Domain.Enums;

namespace IsSuas.Application.Pilots;

public sealed record PilotDto(
    Guid Id,
    Guid PersonId,
    string FirstName,
    string LastName,
    string? Email,
    Guid OrgUnitId,
    string OrgUnitName,
    string? LicenseNumber,
    string? PersonalBadgeNumber,
    string? GestorCertificateNumber,
    string? LogbookNumber,
    PilotStatus Status,
    DateTimeOffset? CurrencyValidUntil,
    DateTimeOffset? LastFlightAt,
    bool IsEligibleForOperations,
    bool IsActive,
    uint RowVersion,
    IReadOnlyList<string> CertificateCodes,
    IReadOnlyList<PilotCertListItemDto> Certificates);

public sealed record PilotCertListItemDto(
    string CertificateTypeCode,
    string? ExternalNumber,
    DateTimeOffset? ValidTo,
    string? MtowOrValidityText,
    Guid Id = default,
    CertificateStatus Status = CertificateStatus.Issued,
    DateTimeOffset? ValidFrom = null,
    DateTimeOffset? SuspendedUntil = null);

public sealed record PilotDetailDto(
    PilotDto Pilot,
    IReadOnlyList<PilotQualificationDto> Qualifications,
    IReadOnlyList<PilotCertificateDto> Certificates,
    IReadOnlyList<PilotTrainingDto> Trainings,
    IReadOnlyList<PilotStatusHistoryDto> StatusHistory,
    IReadOnlyList<PersonFunctionAssignmentDto> Functions,
    IReadOnlyList<PilotRestrictionDto> Restrictions);

public sealed record CreatePilotRequest(
    string FirstName,
    string LastName,
    string? Email,
    Guid OrgUnitId,
    string? LicenseNumber,
    string? Notes);

public sealed record ChangePilotStatusRequest(PilotStatus Status, string Reason, uint RowVersion);

public sealed record PilotQualificationDto(
    Guid Id,
    string QualificationCode,
    string NameSk,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    string? Issuer,
    bool IsActive,
    IReadOnlyList<QualificationVerificationDto> Verifications);

public sealed record CreatePilotQualificationRequest(
    string QualificationCode,
    string NameSk,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    string? Issuer);

public sealed record QualificationVerificationDto(
    Guid Id,
    DateTimeOffset VerificationDate,
    string VerificationMethodCode,
    QualificationVerificationResult Result,
    Guid VerifiedByAccountId,
    string? VerifiedSource,
    DateTimeOffset? NextVerificationDate,
    string? Notes,
    bool IsActive);

public sealed record CreateQualificationVerificationRequest(
    string VerificationMethodCode,
    QualificationVerificationResult Result,
    string? VerifiedSource,
    DateTimeOffset? NextVerificationDate,
    string? Notes);

public sealed record PilotCertificateDto(
    Guid Id,
    string CertificateTypeCode,
    string TitleSk,
    string? ExternalNumber,
    int Version,
    CertificateStatus Status,
    DateTimeOffset? IssuedAt,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo,
    bool IsActive,
    IReadOnlyList<CertificateScopeDto> Scopes,
    string? MtowOrValidityText = null,
    DateTimeOffset? SuspendedUntil = null);

public sealed record CreatePilotCertificateRequest(
    string CertificateTypeCode,
    string TitleSk,
    string? ExternalNumber,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo,
    string? Notes);

public sealed record IssuePilotCertificateRequest(string? Reason);

public sealed record GrantPilotCertificateRequest(
    string CertificateTypeCode,
    string? ExternalNumber = null,
    DateTimeOffset? ValidFrom = null,
    DateTimeOffset? ValidTo = null,
    string? Notes = null,
    string? MtowOrValidityText = null);

public sealed record RevokePilotCertificateRequest(string? Reason);

public sealed record SuspendPilotCertificateRequest(
    DateTimeOffset? Until = null,
    int? DurationDays = null,
    string? Reason = null);

public sealed record CertificateScopeDto(
    Guid Id,
    string ScopeTypeCode,
    string? UasTypeCode,
    string? OperationTypeCode,
    string? FunctionTypeCode,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    string? RestrictionText,
    CertificateScopeStatus Status,
    bool IsActive);

public sealed record CreateCertificateScopeRequest(
    string ScopeTypeCode,
    string? UasTypeCode,
    string? OperationTypeCode,
    string? FunctionTypeCode,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    string? RestrictionText);

public sealed record PilotTrainingDto(
    Guid Id,
    string TrainingCode,
    string TitleSk,
    DateTimeOffset CompletedAt,
    TrainingResult Result,
    string? InstructorName,
    DateTimeOffset? ValidUntil,
    bool IsActive,
    Guid? TrainingParticipationId);

public sealed record CreatePilotTrainingRequest(
    string TrainingCode,
    string TitleSk,
    DateTimeOffset CompletedAt,
    TrainingResult Result,
    string? InstructorName,
    DateTimeOffset? ValidUntil,
    string? Notes);

public sealed record PilotStatusHistoryDto(
    Guid Id,
    PilotStatus FromStatus,
    PilotStatus ToStatus,
    string Reason,
    DateTimeOffset ChangedAt);

public sealed record PersonFunctionAssignmentDto(
    Guid Id,
    Guid PersonId,
    string FunctionTypeCode,
    Guid OrganizationOrgUnitId,
    Guid? OrgUnitId,
    string AssignmentScopeTypeCode,
    Guid? UasId,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    PersonFunctionAssignmentStatus Status,
    Guid AssignedByAccountId,
    string? TerminationReason,
    bool IsActive);

public sealed record CreatePersonFunctionAssignmentRequest(
    string FunctionTypeCode,
    Guid OrganizationOrgUnitId,
    Guid? OrgUnitId,
    string AssignmentScopeTypeCode,
    Guid? UasId,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo);

public sealed record PilotRestrictionDto(
    Guid Id,
    Guid PilotId,
    string RestrictionTypeCode,
    string Description,
    string SeverityCode,
    bool BlocksOperation,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    Guid? ImposedByPersonId,
    PilotRestrictionStatus Status,
    string? TerminationReason,
    bool IsActive);

public sealed record CreatePilotRestrictionRequest(
    string RestrictionTypeCode,
    string Description,
    string SeverityCode,
    bool BlocksOperation,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    Guid? ImposedByPersonId);

public sealed record EndPilotRestrictionRequest(string TerminationReason);

public sealed record TrainingCatalogItemDto(
    Guid Id,
    string ActivityCode,
    string ActivityName,
    string ActivityTypeCode,
    int VersionNumber,
    string Description,
    decimal? MinimumDurationHours,
    int? RecurrencePeriodMonths,
    bool ExaminationRequired,
    int? PassingScorePercent,
    bool PracticalPartRequired,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    TrainingCatalogStatus Status,
    bool IsActive);

public sealed record CreateTrainingCatalogItemRequest(
    string ActivityCode,
    string ActivityName,
    string ActivityTypeCode,
    string Description,
    decimal? MinimumDurationHours,
    int? RecurrencePeriodMonths,
    bool ExaminationRequired,
    int? PassingScorePercent,
    bool PracticalPartRequired,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo);

public sealed record TrainingActivityDto(
    Guid Id,
    Guid CatalogItemId,
    string ActivityNumber,
    Guid OrganizationOrgUnitId,
    Guid? OrgUnitId,
    DateTimeOffset PlannedStartAt,
    DateTimeOffset PlannedEndAt,
    DateTimeOffset? ActualStartAt,
    DateTimeOffset? ActualEndAt,
    string? Location,
    string DeliveryFormCode,
    Guid? InstructorPersonId,
    Guid? ExaminerPersonId,
    int? MaximumParticipants,
    TrainingActivityStatus Status,
    string? CancellationReason,
    bool IsActive,
    IReadOnlyList<TrainingParticipationDto> Participations);

public sealed record CreateTrainingActivityRequest(
    Guid CatalogItemId,
    string ActivityNumber,
    Guid OrganizationOrgUnitId,
    Guid? OrgUnitId,
    DateTimeOffset PlannedStartAt,
    DateTimeOffset PlannedEndAt,
    string? Location,
    string DeliveryFormCode,
    Guid? InstructorPersonId,
    Guid? ExaminerPersonId,
    int? MaximumParticipants);

public sealed record TrainingParticipationDto(
    Guid Id,
    Guid ActivityId,
    Guid PersonId,
    string ParticipationRoleCode,
    DateTimeOffset RegistrationDate,
    TrainingRegistrationStatus RegistrationStatus,
    TrainingAttendanceStatus? AttendanceStatus,
    int? AttendancePercentage,
    TrainingCompletionStatus? CompletionStatus,
    string? AbsenceReason,
    string? Notes,
    bool IsActive,
    TrainingActivityResultDto? Result);

public sealed record CreateTrainingParticipationRequest(
    Guid PersonId,
    string ParticipationRoleCode);

public sealed record TrainingActivityResultDto(
    Guid Id,
    Guid ParticipationId,
    TrainingResult Result,
    int AttemptNumber,
    int? TheoreticalScorePercent,
    int? PracticalScorePercent,
    int? OverallScorePercent,
    DateTimeOffset? EvaluatedAt,
    Guid? EvaluatedByPersonId,
    DateTimeOffset? ValidUntil,
    string? Notes,
    bool IsActive);

public sealed record RecordTrainingActivityResultRequest(
    TrainingResult Result,
    int AttemptNumber,
    int? TheoreticalScorePercent,
    int? PracticalScorePercent,
    int? OverallScorePercent,
    Guid? EvaluatedByPersonId,
    DateTimeOffset? ValidUntil,
    string? Notes,
    bool SyncToPilotCard);

public sealed record PilotEligibilityCheckDto(Guid PilotId, bool IsEligible, string Reason);
