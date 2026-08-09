using IsSuas.Domain.Enums;

namespace IsSuas.Application.Maintenance;

public sealed record MaintenancePlanDto(
    Guid Id,
    string NameSk,
    MaintenanceTargetType TargetType,
    Guid? TargetId,
    string? TargetTypeCode,
    MaintenanceTriggerType TriggerType,
    int? IntervalValue,
    DateTimeOffset? NextDueAt,
    int? NextDueCounter,
    int CurrentCounter,
    int WarningThresholdPercent,
    bool BlockWhenOverdue,
    MaintenancePlanStatus Status,
    bool IsOverdue,
    bool IsWarning,
    bool IsActive,
    uint RowVersion);

public sealed record CreateMaintenancePlanRequest(
    string NameSk,
    MaintenanceTargetType TargetType,
    Guid? TargetId,
    string? TargetTypeCode,
    MaintenanceTriggerType TriggerType,
    int? IntervalValue,
    DateTimeOffset? NextDueAt,
    int? NextDueCounter,
    int WarningThresholdPercent,
    bool BlockWhenOverdue);

public sealed record WorkOrderDto(
    Guid Id,
    Guid? MaintenancePlanId,
    Guid? ServiceRequestId,
    MaintenanceTargetType TargetType,
    Guid TargetId,
    string Title,
    string? Description,
    WorkOrderStatus Status,
    Guid? AssignedTechnicianAccountId,
    DateTimeOffset? DueAt,
    bool IsActive,
    uint RowVersion);

public sealed record CreateWorkOrderRequest(
    Guid? MaintenancePlanId,
    MaintenanceTargetType TargetType,
    Guid TargetId,
    string Title,
    string? Description,
    Guid? AssignedTechnicianAccountId,
    DateTimeOffset? DueAt,
    Guid? ServiceRequestId = null);

public sealed record CompleteWorkOrderRequest(
    string WorkScope,
    string Result,
    DateTimeOffset PerformedAt,
    string? PartsUsed,
    string? DocumentationNotes,
    bool AirworthinessRestored,
    uint RowVersion);

public sealed record MaintenanceRecordDto(
    Guid Id,
    Guid WorkOrderId,
    string WorkScope,
    string Result,
    DateTimeOffset PerformedAt,
    Guid PerformedByAccountId,
    string? PartsUsed,
    bool AirworthinessRestored);

public sealed record DefectDto(
    Guid Id,
    MaintenanceTargetType TargetType,
    Guid TargetId,
    string Title,
    string OriginalReport,
    bool OriginalReportLocked,
    DefectSeverity Severity,
    DefectStatus Status,
    bool BlocksOperations,
    string? TemporaryMeasure,
    Guid ReportedByAccountId,
    string DefectTypeCode,
    string? ResolutionNotes,
    bool IsActive,
    uint RowVersion);

public sealed record ReportDefectRequest(
    MaintenanceTargetType TargetType,
    Guid TargetId,
    string Title,
    string OriginalReport,
    DefectSeverity Severity,
    bool BlocksOperations,
    string? TemporaryMeasure,
    Guid? RelatedFlightId,
    Guid? RelatedUasId,
    string DefectTypeCode);

public sealed record ResolveDefectRequest(
    string ResolutionNotes,
    string TechnicalAssessment,
    bool TestPerformed,
    Guid VerifiedByAccountId,
    uint RowVersion);

public sealed record AssetBlockCheckDto(bool IsBlocked, string Reason);

public sealed record ServiceRequestDto(
    Guid Id,
    string RequestNumber,
    string RequestTypeCode,
    string SourceTypeCode,
    Guid? DefectId,
    MaintenanceTargetType TargetType,
    Guid TargetId,
    string Description,
    ServiceRequestPriority Priority,
    DateTimeOffset RequestedAt,
    ServiceRequestStatus Status,
    DateTimeOffset? RequiredCompletionAt,
    bool IsActive,
    uint RowVersion);

public sealed record CreateServiceRequestRequest(
    string RequestTypeCode,
    string SourceTypeCode,
    Guid? DefectId,
    MaintenanceTargetType TargetType,
    Guid TargetId,
    string Description,
    ServiceRequestPriority Priority,
    DateTimeOffset? RequiredCompletionAt,
    Guid? SourceFlightId);

public sealed record TechnicalRestrictionDto(
    Guid Id,
    Guid? DefectId,
    MaintenanceTargetType TargetType,
    Guid TargetId,
    string RestrictionTypeCode,
    string Description,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    bool BlocksOperation,
    TechnicalRestrictionStatus Status,
    bool IsActive,
    uint RowVersion);

public sealed record CreateTechnicalRestrictionRequest(
    Guid? DefectId,
    MaintenanceTargetType TargetType,
    Guid TargetId,
    string RestrictionTypeCode,
    string Description,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    bool BlocksOperation);

public sealed record EndTechnicalRestrictionRequest(string TerminationReason, uint RowVersion);

public sealed record ReleaseToServiceDto(
    Guid Id,
    Guid? WorkOrderId,
    Guid? DefectId,
    MaintenanceTargetType TargetType,
    Guid TargetId,
    ReleaseToServiceDecision Decision,
    string ReleaseScopeCode,
    string? RestrictionDescription,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? ValidTo,
    Guid ReleasedByAccountId,
    string DecisionReason,
    ReleaseToServiceStatus Status,
    bool IsActive,
    uint RowVersion);

public sealed record CreateReleaseToServiceRequest(
    Guid? WorkOrderId,
    Guid? DefectId,
    MaintenanceTargetType TargetType,
    Guid TargetId,
    ReleaseToServiceDecision Decision,
    string ReleaseScopeCode,
    string? RestrictionDescription,
    string DecisionReason,
    DateTimeOffset? ValidTo);

public sealed record TechnicalStatusHistoryDto(
    Guid Id,
    MaintenanceTargetType TargetType,
    Guid TargetId,
    string? PreviousStatusCode,
    string NewStatusCode,
    DateTimeOffset EffectiveAt,
    string Reason);

