namespace IsSuas.Domain.Enums;

public enum CompatibilityStatus
{
    Compatible = 0,
    Conditional = 1,
    Incompatible = 2
}

public enum BatteryStatusKind
{
    Usability = 0
}

public enum BatteryTechnicalMetric
{
    CapacityPercent = 0,
    HealthPercent = 1,
    CycleCount = 2,
    TemperatureC = 3,
    VoltageV = 4
}

public enum BatteryOperationEventType
{
    Charge = 0,
    Discharge = 1,
    Inspection = 2,
    Storage = 3,
    Damage = 4,
    Quarantine = 5
}

public enum BatteryRecordSource
{
    Manual = 0,
    Flight = 1,
    Telemetry = 2,
    Import = 3
}
