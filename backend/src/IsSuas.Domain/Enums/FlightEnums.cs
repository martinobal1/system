namespace IsSuas.Domain.Enums;

public enum FlightStatus
{
    Draft = 0,
    Ready = 1,
    Closed = 2,
    Cancelled = 3
}

public enum TelemetryImportStatus
{
    Received = 0,
    Processed = 1,
    NeedsPairing = 2,
    Failed = 3,
    DuplicateIgnored = 4
}
