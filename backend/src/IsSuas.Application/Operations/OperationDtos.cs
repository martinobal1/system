using IsSuas.Domain.Enums;

namespace IsSuas.Application.Operations;

public sealed record OperationDto(
    Guid Id,
    string Title,
    string? OperationTypeCode,
    OperationStatus Status,
    Guid OrgUnitId,
    string OrgUnitName,
    Guid ProposedByAccountId,
    DateTimeOffset PlannedStart,
    DateTimeOffset PlannedEnd,
    string? LocationAddress,
    double? LocationLat,
    double? LocationLon,
    bool IsUrgent,
    string? UrgentReason,
    int Version,
    bool IsSubmittedVersionLocked,
    bool IsActive,
    uint RowVersion);

public sealed record OperationDetailDto(
    OperationDto Operation,
    string? Description,
    string? LocationGeoJson,
    string? DecisionReason,
    IReadOnlyList<OperationResourceDto> Resources,
    IReadOnlyList<OperationStatusHistoryDto> StatusHistory,
    IReadOnlyList<OperationApprovalStepDto> ApprovalSteps,
    IReadOnlyList<OperationApprovalConditionDto> Conditions,
    OperationRecurrenceRuleDto? Recurrence,
    IReadOnlyList<OperationMapCheckDto> MapChecks);

public sealed record OperationMapCheckDto(
    Guid Id,
    string ProviderCode,
    string ResultCode,
    bool IsBlocking,
    string Summary,
    string? DetailsJson,
    DateTimeOffset CheckedAt,
    Guid? CheckedByAccountId,
    long ElapsedMs,
    bool IsActive);

public sealed record CreateOperationRequest(
    string Title,
    string? OperationTypeCode,
    Guid OrgUnitId,
    DateTimeOffset PlannedStart,
    DateTimeOffset PlannedEnd,
    string? LocationAddress,
    double? LocationLat,
    double? LocationLon,
    string? LocationGeoJson,
    string? Description,
    bool IsUrgent,
    string? UrgentReason);

public sealed record UpdateOperationRequest(
    string Title,
    string? OperationTypeCode,
    DateTimeOffset PlannedStart,
    DateTimeOffset PlannedEnd,
    string? LocationAddress,
    double? LocationLat,
    double? LocationLon,
    string? LocationGeoJson,
    string? Description,
    uint RowVersion);

public sealed record AssignOperationResourceRequest(
    OperationResourceType ResourceType,
    Guid ResourceId,
    string? RoleCode);

public sealed record CreateApprovalConditionRequest(
    string ConditionTypeCode,
    string Description,
    bool MustBeMetBeforeFlight = true,
    DateTimeOffset? DueAt = null);

public sealed record DecisionRequest(
    string Reason,
    uint RowVersion,
    OperationDecisionCode DecisionCode = OperationDecisionCode.Approved,
    IReadOnlyList<CreateApprovalConditionRequest>? Conditions = null);

public sealed record AddApprovalStepRequest(
    int SequenceNumber,
    string StepTypeCode,
    string? AssignedRoleCode,
    Guid? AssignedAccountId,
    bool IsMandatory = true,
    DateTimeOffset? DueAt = null);

public sealed record OperationApprovalStepDto(
    Guid Id,
    int SequenceNumber,
    string StepTypeCode,
    string? AssignedRoleCode,
    Guid? AssignedAccountId,
    bool IsMandatory,
    ApprovalStepStatus Status,
    DateTimeOffset? CompletedAt,
    OperationDecisionDto? Decision);

public sealed record OperationDecisionDto(
    Guid Id,
    OperationDecisionCode DecisionCode,
    DateTimeOffset DecidedAt,
    Guid DecidedByAccountId,
    string Justification);

public sealed record OperationApprovalConditionDto(
    Guid Id,
    Guid DecisionId,
    string ConditionTypeCode,
    string Description,
    bool MustBeMetBeforeFlight,
    ApprovalConditionStatus Status,
    DateTimeOffset? DueAt,
    DateTimeOffset? VerifiedAt,
    string? VerificationNote,
    uint RowVersion);

public sealed record VerifyApprovalConditionRequest(string VerificationNote, uint RowVersion);

public sealed record SetRecurrenceRequest(
    RecurrenceType RecurrenceType,
    int Interval,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    int? MaximumOccurrences,
    string? DaysOfWeek);

public sealed record OperationRecurrenceRuleDto(
    Guid Id,
    Guid OperationId,
    RecurrenceType RecurrenceType,
    int Interval,
    string? DaysOfWeek,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    int? MaximumOccurrences,
    DateTimeOffset? NextOccurrenceAt,
    int GeneratedCount,
    bool IsRuleActive);

public sealed record OperationResourceDto(
    Guid Id,
    OperationResourceType ResourceType,
    Guid ResourceId,
    string? RoleCode,
    string? DisplayName);

public sealed record OperationStatusHistoryDto(
    Guid Id,
    OperationStatus FromStatus,
    OperationStatus ToStatus,
    string Reason,
    DateTimeOffset ChangedAt);

public sealed record WorkTaskDto(
    Guid Id,
    string Title,
    string? Description,
    WorkTaskStatus Status,
    Guid? RelatedOperationId,
    DateTimeOffset? DueAt,
    string TaskTypeCode,
    bool IsActive,
    uint RowVersion);
