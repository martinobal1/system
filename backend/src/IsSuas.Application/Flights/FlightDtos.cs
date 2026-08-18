using IsSuas.Domain.Enums;

namespace IsSuas.Application.Flights;

public sealed record FlightDto(
    Guid Id,
    Guid? OperationId,
    string? StandaloneReason,
    Guid OrgUnitId,
    string OrgUnitName,
    Guid? UasId,
    string? UasLabel,
    Guid? PilotId,
    string? PilotName,
    Guid? BatteryId,
    FlightStatus Status,
    DateTimeOffset? TakeoffAt,
    DateTimeOffset? LandingAt,
    int? DurationMinutes,
    string? FlightNumber,
    string? Location,
    string? Purpose,
    double? LocationLat,
    double? LocationLon,
    double? LocationRadiusMeters,
    double? DistanceMeters,
    double? MaxAltitudeMeters,
    bool CountersApplied,
    bool IsActive,
    uint RowVersion);

public sealed record FlightDetailDto(
    FlightDto Flight,
    string? PostFlightNotes,
    string? ChecklistResult,
    string? DeviationNotes,
    string? TrackGeoJson,
    IReadOnlyList<FlightTrackPointDto> TrackPoints,
    IReadOnlyList<TelemetryImportDto> TelemetryImports);

public sealed record FlightTrackPointDto(
    Guid Id,
    DateTimeOffset RecordedAt,
    double Latitude,
    double Longitude,
    double? AltitudeMeters,
    double? SpeedMps,
    int Sequence);

public sealed record CreateFlightFromOperationRequest(
    Guid OperationId,
    Guid? UasId,
    Guid? PilotId,
    Guid? BatteryId,
    DateTimeOffset? TakeoffAt,
    DateTimeOffset? LandingAt);

/// <summary>L1/L2 potvrdia, že schválená žiadosť o let sa naozaj odlietala — vytvorí uzavretý let do denníka.</summary>
public sealed record ConfirmOperationFlownRequest(
    uint RowVersion,
    DateTimeOffset? TakeoffAt = null,
    DateTimeOffset? LandingAt = null,
    string? Notes = null);

public sealed record CreateStandaloneFlightRequest(
    Guid OrgUnitId,
    string StandaloneReason,
    Guid? UasId,
    Guid? PilotId,
    Guid? BatteryId,
    DateTimeOffset? TakeoffAt,
    DateTimeOffset? LandingAt,
    string? Location = null,
    string? Purpose = null,
    double? LocationLat = null,
    double? LocationLon = null,
    double? LocationRadiusMeters = null);

public sealed record CloseFlightRequest(
    string PostFlightNotes,
    string ChecklistResult,
    string? DeviationNotes,
    uint RowVersion);

public sealed record TelemetryImportDto(
    Guid Id,
    string FileName,
    string ContentHash,
    string Format,
    string Source,
    TelemetryImportStatus Status,
    string? ProcessingMessage,
    Guid? FlightId,
    DateTimeOffset ReceivedAt);

public sealed record ImportTelemetryRequest(
    string FileName,
    string Format,
    string Payload,
    Guid? FlightId,
    Guid? UasId,
    Guid? OperationId,
    Guid? PilotId,
    Guid? BatteryId);

public sealed record PairTelemetryRequest(
    Guid? FlightId,
    Guid? UasId,
    Guid? OperationId,
    Guid? PilotId,
    Guid? BatteryId);
