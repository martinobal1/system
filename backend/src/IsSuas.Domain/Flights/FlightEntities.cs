using IsSuas.Domain.Common;
using IsSuas.Domain.Enums;
using IsSuas.Domain.Fleet;
using IsSuas.Domain.Identity;
using IsSuas.Domain.Operations;
using IsSuas.Domain.Pilots;

namespace IsSuas.Domain.Flights;

public class Flight : AuditableEntity
{
    public Guid? OperationId { get; set; }
    public Operation? Operation { get; set; }
    public string? StandaloneReason { get; set; }
    public Guid OrgUnitId { get; set; }
    public OrgUnit OrgUnit { get; set; } = null!;
    public Guid? UasId { get; set; }
    public Uas? Uas { get; set; }
    public Guid? PilotId { get; set; }
    public Pilot? Pilot { get; set; }
    public Guid? BatteryId { get; set; }
    public Battery? Battery { get; set; }
    public string? FlightNumber { get; set; }
    public string? Location { get; set; }
    /// <summary>Stred plánovaného operačného priestoru (WGS84).</summary>
    public double? LocationLat { get; set; }
    public double? LocationLon { get; set; }
    /// <summary>Polomer operačného priestoru v metroch (napr. 1000 = 1 km).</summary>
    public double? LocationRadiusMeters { get; set; }
    public string? Purpose { get; set; }
    public FlightStatus Status { get; set; } = FlightStatus.Draft;
    public DateTimeOffset? TakeoffAt { get; set; }
    public DateTimeOffset? LandingAt { get; set; }
    public int? DurationMinutes { get; set; }
    public double? DistanceMeters { get; set; }
    public double? MaxAltitudeMeters { get; set; }
    public double? MaxSpeedMps { get; set; }
    public string? PostFlightNotes { get; set; }
    public string? ChecklistResult { get; set; }
    public string? DeviationNotes { get; set; }
    public bool CountersApplied { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public Guid? ClosedByAccountId { get; set; }
    public string? TrackGeoJson { get; set; }
    public ICollection<TelemetryImport> TelemetryImports { get; set; } = new List<TelemetryImport>();
    public ICollection<FlightTrackPoint> TrackPoints { get; set; } = new List<FlightTrackPoint>();
}

public class FlightTrackPoint : AuditableEntity
{
    public Guid FlightId { get; set; }
    public Flight Flight { get; set; } = null!;
    public DateTimeOffset RecordedAt { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? AltitudeMeters { get; set; }
    public double? SpeedMps { get; set; }
    public int Sequence { get; set; }
}

public class TelemetryImport : AuditableEntity
{
    public string FileName { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public string Format { get; set; } = "geojson";
    public string Source { get; set; } = "manual-upload";
    public TelemetryImportStatus Status { get; set; } = TelemetryImportStatus.Received;
    public string? ProcessingMessage { get; set; }
    public Guid? FlightId { get; set; }
    public Flight? Flight { get; set; }
    public Guid? MatchedUasId { get; set; }
    public Guid? MatchedOperationId { get; set; }
    public Guid? MatchedPilotId { get; set; }
    public Guid? MatchedBatteryId { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public string? OriginalPayload { get; set; }
}

public static class FlightStateMachine
{
    private static readonly Dictionary<FlightStatus, HashSet<FlightStatus>> Transitions = new()
    {
        [FlightStatus.Draft] = [FlightStatus.Ready, FlightStatus.Cancelled],
        [FlightStatus.Ready] = [FlightStatus.Closed, FlightStatus.Cancelled],
        [FlightStatus.Closed] = [],
        [FlightStatus.Cancelled] = []
    };

    public static bool CanTransition(FlightStatus from, FlightStatus to) =>
        Transitions.TryGetValue(from, out var allowed) && allowed.Contains(to);
}
