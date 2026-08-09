using IsSuas.Domain.Common;
using IsSuas.Domain.Enums;
using IsSuas.Domain.Flights;
using IsSuas.Domain.Identity;

namespace IsSuas.Domain.Fleet;

/// <summary>ENT-017 — kompatibilita komponentu s UAS / typom UAS.</summary>
public class ComponentCompatibility : AuditableEntity
{
    public Guid ComponentId { get; set; }
    public Component Component { get; set; } = null!;
    public Guid? UasId { get; set; }
    public Uas? Uas { get; set; }
    public string? UasTypeCode { get; set; }
    public CompatibilityStatus Status { get; set; } = CompatibilityStatus.Compatible;
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
    public string? RestrictionText { get; set; }
    public Guid? VerificationDocumentId { get; set; }
}

/// <summary>ENT-020 — kompatibilita batérie s UAS / typom UAS.</summary>
public class BatteryCompatibility : AuditableEntity
{
    public Guid BatteryId { get; set; }
    public Battery Battery { get; set; } = null!;
    public Guid? UasId { get; set; }
    public Uas? Uas { get; set; }
    public string? UasTypeCode { get; set; }
    public CompatibilityStatus Status { get; set; } = CompatibilityStatus.Compatible;
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
    public string? RestrictionText { get; set; }
    public Guid? VerificationDocumentId { get; set; }
}

/// <summary>ENT-021 — technický záznam batérie.</summary>
public class BatteryTechnicalRecord : AuditableEntity
{
    public Guid BatteryId { get; set; }
    public Battery Battery { get; set; } = null!;
    public DateTimeOffset RecordedAt { get; set; }
    public BatteryTechnicalMetric MetricType { get; set; }
    public decimal? MeasuredValue { get; set; }
    public string? UnitCode { get; set; }
    public string? TextValue { get; set; }
    public BatteryRecordSource SourceType { get; set; } = BatteryRecordSource.Manual;
    public Guid? SourceFlightId { get; set; }
    public Flight? SourceFlight { get; set; }
    public Guid? MeasuredByPersonId { get; set; }
    public Person? MeasuredByPerson { get; set; }
    public string? CorrectionReason { get; set; }
}

/// <summary>ENT-022 — prevádzkový záznam batérie.</summary>
public class BatteryOperationRecord : AuditableEntity
{
    public Guid BatteryId { get; set; }
    public Battery Battery { get; set; } = null!;
    public BatteryOperationEventType EventType { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public Guid? PerformedByPersonId { get; set; }
    public Person? PerformedByPerson { get; set; }
    public Guid? RelatedFlightId { get; set; }
    public Flight? RelatedFlight { get; set; }
    public int? ChargeLevelPercent { get; set; }
    public int? CycleCount { get; set; }
    public decimal? TemperatureC { get; set; }
    public string? ResultCode { get; set; }
    public string? Note { get; set; }
    public BatteryRecordSource SourceType { get; set; } = BatteryRecordSource.Manual;
}

/// <summary>ENT-023 — história stavu batérie.</summary>
public class BatteryStatusHistory : AuditableEntity
{
    public Guid BatteryId { get; set; }
    public Battery Battery { get; set; } = null!;
    public BatteryStatusKind StatusKind { get; set; } = BatteryStatusKind.Usability;
    public AssetUsabilityStatus? PreviousStatus { get; set; }
    public AssetUsabilityStatus NewStatus { get; set; }
    public DateTimeOffset EffectiveAt { get; set; }
    public string? Reason { get; set; }
    public Guid ChangedByAccountId { get; set; }
    public Guid? ApprovedByAccountId { get; set; }
}
