using IsSuas.Domain.Enums;

namespace IsSuas.Application.Fleet;

public sealed record UasDto(
    Guid Id,
    string? RegistrationNumber,
    string? CertificateNumber,
    DateTimeOffset? DeregisteredAt,
    string SerialNumber,
    string Manufacturer,
    string Model,
    string? UasTypeCode,
    string? LogbookNumber,
    string? InventoryAssetNumber,
    UasLifecycleStatus Status,
    Guid? OwningOrgUnitId,
    string? OwningOrgUnitName,
    string? Notes,
    DateTimeOffset? RegisteredAt,
    Guid? SubmittedByAccountId,
    int TotalFlightCount,
    int TotalFlightMinutes,
    bool IsActive,
    uint RowVersion);

public sealed record UasDetailDto(
    UasDto Uas,
    IReadOnlyList<UasAssignmentDto> Assignments,
    IReadOnlyList<InstallationDto> Installations,
    IReadOnlyList<UasStatusHistoryDto> StatusHistory,
    IReadOnlyList<PilotUasAuthorizationDto>? AuthorizedPilots = null);

public sealed record CreateUasRequest(
    string SerialNumber,
    string Manufacturer,
    string Model,
    string? UasTypeCode,
    Guid? OwningOrgUnitId,
    string? Notes,
    string? CertificateNumber = null,
    string? LogbookNumber = null,
    string? InventoryAssetNumber = null);

public sealed record UpdateUasRequest(
    string SerialNumber,
    string Manufacturer,
    string Model,
    string? LogbookNumber,
    string? InventoryAssetNumber,
    uint RowVersion);

public sealed record TransitionUasRequest(UasLifecycleStatus TargetStatus, string Reason, uint RowVersion);
public sealed record AssignUasRequest(Guid OrgUnitId, DateTimeOffset ValidFrom, string Reason);
public sealed record TransferUasRequest(Guid NewOrgUnitId, DateTimeOffset EffectiveAt, string Reason);

public sealed record UasAssignmentDto(
    Guid Id,
    Guid OrgUnitId,
    string OrgUnitName,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    string Reason,
    bool IsActive);

public sealed record UasStatusHistoryDto(
    Guid Id,
    UasLifecycleStatus FromStatus,
    UasLifecycleStatus ToStatus,
    string Reason,
    DateTimeOffset ChangedAt);

public sealed record ComponentDto(
    Guid Id,
    string InternalNumber,
    string? SerialNumber,
    string Name,
    string? ComponentTypeCode,
    AssetUsabilityStatus Status,
    Guid? OwningOrgUnitId,
    Guid? UasId,
    string? UasRegistrationNumber,
    bool IsActive,
    uint RowVersion);

public sealed record CreateComponentRequest(
    string Name,
    Guid? UasId = null,
    string? ComponentTypeCode = null,
    string? SerialNumber = null,
    Guid? OwningOrgUnitId = null,
    /// <summary>Optional explicit number; otherwise derived from drone registration when UasId is set.</summary>
    string? InternalNumber = null);

public sealed record InstallComponentRequest(Guid UasId, DateTimeOffset InstalledAt, string? Reason);
public sealed record RemoveInstallationRequest(string? Reason);

public sealed record InstallationDto(
    Guid Id,
    Guid ComponentId,
    string ComponentName,
    Guid UasId,
    string? UasRegistrationNumber,
    DateTimeOffset InstalledAt,
    DateTimeOffset? RemovedAt,
    bool IsActive);

public sealed record BatteryDto(
    Guid Id,
    string InternalNumber,
    string SerialNumber,
    string? Manufacturer,
    string? Model,
    string? CompatibleUasTypeCodes,
    AssetUsabilityStatus Status,
    Guid? OwningOrgUnitId,
    int CycleCount,
    decimal? CapacityPercent,
    decimal? HealthPercent,
    bool IsActive,
    uint RowVersion);

public sealed record CreateBatteryRequest(
    string InternalNumber,
    string SerialNumber,
    string? Manufacturer,
    string? Model,
    string? CompatibleUasTypeCodes,
    Guid? OwningOrgUnitId);

public sealed record CompatibilityRuleDto(
    Guid Id,
    Guid AssetId,
    Guid? UasId,
    string? UasTypeCode,
    CompatibilityStatus Status,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    string? RestrictionText);

public sealed record CreateCompatibilityRuleRequest(
    Guid? UasId,
    string? UasTypeCode,
    CompatibilityStatus Status,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    string? RestrictionText);

public sealed record BatteryTechnicalRecordDto(
    Guid Id,
    Guid BatteryId,
    DateTimeOffset RecordedAt,
    BatteryTechnicalMetric MetricType,
    decimal? MeasuredValue,
    string? UnitCode,
    string? TextValue,
    BatteryRecordSource SourceType,
    Guid? SourceFlightId);

public sealed record CreateBatteryTechnicalRecordRequest(
    DateTimeOffset RecordedAt,
    BatteryTechnicalMetric MetricType,
    decimal? MeasuredValue,
    string? UnitCode,
    string? TextValue,
    BatteryRecordSource SourceType = BatteryRecordSource.Manual,
    Guid? SourceFlightId = null,
    string? CorrectionReason = null,
    bool ApplyToBattery = true);

public sealed record BatteryOperationRecordDto(
    Guid Id,
    Guid BatteryId,
    BatteryOperationEventType EventType,
    DateTimeOffset OccurredAt,
    int? ChargeLevelPercent,
    int? CycleCount,
    decimal? TemperatureC,
    string? ResultCode,
    string? Note,
    BatteryRecordSource SourceType);

public sealed record CreateBatteryOperationRecordRequest(
    BatteryOperationEventType EventType,
    DateTimeOffset OccurredAt,
    int? ChargeLevelPercent,
    int? CycleCount,
    decimal? TemperatureC,
    string? ResultCode,
    string? Note,
    BatteryRecordSource SourceType = BatteryRecordSource.Manual,
    Guid? RelatedFlightId = null,
    bool ApplyToBattery = true);

public sealed record BatteryStatusHistoryDto(
    Guid Id,
    Guid BatteryId,
    BatteryStatusKind StatusKind,
    AssetUsabilityStatus? PreviousStatus,
    AssetUsabilityStatus NewStatus,
    DateTimeOffset EffectiveAt,
    string? Reason,
    Guid ChangedByAccountId);

public sealed record TransitionBatteryStatusRequest(
    AssetUsabilityStatus TargetStatus,
    string Reason,
    uint RowVersion);

