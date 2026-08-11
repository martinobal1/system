using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IsSuas.Application.Abstractions;
using IsSuas.Application.Flights;
using IsSuas.Application.Policies;
using IsSuas.Domain.Enums;
using IsSuas.Domain.Flights;
using IsSuas.Domain.Pilots;
using IsSuas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IsSuas.Infrastructure.Services;

public interface IFlightService
{
    Task<IReadOnlyList<FlightDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<FlightDetailDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<FlightDto> CreateFromOperationAsync(CreateFlightFromOperationRequest request, CancellationToken cancellationToken = default);
    Task<FlightDto> CreateStandaloneAsync(CreateStandaloneFlightRequest request, CancellationToken cancellationToken = default);
    Task<FlightDto> MarkReadyAsync(Guid id, uint rowVersion, CancellationToken cancellationToken = default);
    Task<FlightDto> CloseAsync(Guid id, CloseFlightRequest request, CancellationToken cancellationToken = default);
    Task<TelemetryImportDto> ImportTelemetryAsync(ImportTelemetryRequest request, CancellationToken cancellationToken = default);
    Task<TelemetryImportDto> PairTelemetryAsync(Guid importId, PairTelemetryRequest request, CancellationToken cancellationToken = default);
    Task<(string ContentType, string FileName, string Body)> ExportTrackAsync(Guid flightId, string format, CancellationToken cancellationToken = default);
}

public sealed class FlightService(
    IsSuasDbContext db,
    IAuthorizationPolicyService policy,
    ICurrentUser currentUser,
    IAuditWriter audit,
    IClock clock,
    IMaintenanceService maintenance,
    IFleetService fleet,
    IOperationService operations) : IFlightService
{
    public async Task<IReadOnlyList<FlightDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.FlightsRead);
        if (!policy.CanAccessBusinessData())
            throw new ForbiddenException("Systémový administrátor nemá automatický prístup k vecnému obsahu.");

        var items = await db.Flights.AsNoTracking()
            .Include(f => f.OrgUnit)
            .Include(f => f.Uas)
            .Include(f => f.Pilot)!.ThenInclude(p => p!.Person)
            .Where(f => f.IsActive)
            .OrderByDescending(f => f.TakeoffAt ?? f.CreatedAt)
            .ToListAsync(cancellationToken);

        var scoped = items.Where(f => policy.CanAccessOrgUnit(f.OrgUnit.Path)).ToList();

        // L3 RemotePilot: only own flights (pilot person matches account person)
        if (currentUser.Roles.Any(r => r.RoleCode == nameof(SystemRole.RemotePilot))
            && !currentUser.Roles.Any(r => r.RoleCode is nameof(SystemRole.CentralGestor) or nameof(SystemRole.UnitAdministrator)))
        {
            var personId = await db.Accounts.AsNoTracking()
                .Where(a => a.Id == currentUser.AccountId)
                .Select(a => a.PersonId)
                .FirstOrDefaultAsync(cancellationToken);
            var myPilotIds = await db.Pilots.AsNoTracking()
                .Where(p => p.PersonId == personId && p.IsActive)
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);
            scoped = scoped.Where(f => f.PilotId != null && myPilotIds.Contains(f.PilotId.Value)).ToList();
        }

        return scoped.Select(Map).ToList();
    }

    public async Task<FlightDetailDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.FlightsRead);
        var flight = await LoadAsync(id, cancellationToken);
        policy.EnsureOrgAccess(flight.OrgUnit.Path);
        return ToDetail(flight);
    }

    public async Task<FlightDto> CreateFromOperationAsync(CreateFlightFromOperationRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.FlightsWrite);
        var op = await db.Operations.Include(o => o.OrgUnit).Include(o => o.Resources)
            .FirstOrDefaultAsync(o => o.Id == request.OperationId && o.IsActive, cancellationToken)
            ?? throw new NotFoundException("Operácia nebola nájdená.");
        policy.EnsureOrgAccess(op.OrgUnit.Path);
        if (op.Status != OperationStatus.Approved)
            throw new ValidationException("Let z operácie je možné vytvoriť iba zo schválenej operácie.");
        await operations.EnsureBeforeFlightConditionsMetAsync(op.Id, cancellationToken);

        var uasId = request.UasId ?? op.Resources.FirstOrDefault(r => r.ResourceType == OperationResourceType.Uas && r.IsActive)?.ResourceId;
        var pilotId = request.PilotId ?? op.Resources.FirstOrDefault(r => r.ResourceType == OperationResourceType.Pilot && r.IsActive)?.ResourceId;
        var batteryId = request.BatteryId ?? op.Resources.FirstOrDefault(r => r.ResourceType == OperationResourceType.Battery && r.IsActive)?.ResourceId;
        if (batteryId is Guid bid && uasId is Guid uid)
            await fleet.EnsureBatteryCompatibleAsync(bid, uid, cancellationToken);

        var flight = new Flight
        {
            OperationId = op.Id,
            OrgUnitId = op.OrgUnitId,
            UasId = uasId,
            PilotId = pilotId,
            BatteryId = batteryId,
            TakeoffAt = request.TakeoffAt ?? op.PlannedStart,
            LandingAt = request.LandingAt ?? op.PlannedEnd,
            FlightNumber = await NextFlightNumberAsync(cancellationToken),
            Status = FlightStatus.Draft,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        RecalcDuration(flight);
        db.Flights.Add(flight);
        await db.SaveChangesAsync(cancellationToken);
        flight = await LoadAsync(flight.Id, cancellationToken);
        await audit.WriteAsync("Flight.CreateFromOperation", nameof(Flight), flight.Id, null, Map(flight), cancellationToken: cancellationToken);
        return Map(flight);
    }

    public async Task<FlightDto> CreateStandaloneAsync(CreateStandaloneFlightRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.FlightsWrite);
        if (string.IsNullOrWhiteSpace(request.StandaloneReason))
            throw new ValidationException("Samostatný let vyžaduje dôvod.");

        var org = await db.OrgUnits.FirstOrDefaultAsync(o => o.Id == request.OrgUnitId && o.IsActive, cancellationToken)
            ?? throw new NotFoundException("Útvar nebol nájdený.");
        policy.EnsureOrgAccess(org.Path);

        if (request.BatteryId is Guid batteryId && request.UasId is Guid uasId)
            await fleet.EnsureBatteryCompatibleAsync(batteryId, uasId, cancellationToken);

        Guid? pilotId = request.PilotId;
        var isL3Only = currentUser.Roles.Any(r => r.RoleCode == nameof(SystemRole.RemotePilot))
            && !currentUser.Roles.Any(r => r.RoleCode is nameof(SystemRole.CentralGestor) or nameof(SystemRole.UnitAdministrator));
        if (isL3Only)
        {
            var personId = await db.Accounts.AsNoTracking()
                .Where(a => a.Id == currentUser.AccountId)
                .Select(a => a.PersonId)
                .FirstOrDefaultAsync(cancellationToken);
            var myPilot = await db.Pilots.AsNoTracking()
                .FirstOrDefaultAsync(p => p.PersonId == personId && p.IsActive, cancellationToken)
                ?? throw new ValidationException("Účet nemá viazaný pilotný profil — kontaktujte správcu.");
            pilotId = myPilot.Id;
        }

        var lat = request.LocationLat;
        var lon = request.LocationLon;
        var radius = request.LocationRadiusMeters;
        if (radius is < 50 or > 20000)
            throw new ValidationException("Polomer operačného priestoru musí byť 50–20000 m.");
        if ((lat is null) != (lon is null))
            throw new ValidationException("Lokalita na mape vyžaduje lat aj lon.");
        if (lat is < -90 or > 90 || lon is < -180 or > 180)
            throw new ValidationException("Neplatné súradnice lokality.");

        var locationText = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim();
        if (locationText is null && lat is not null && lon is not null)
        {
            var rLabel = radius is null ? "" : $" · r={radius:0} m";
            locationText = $"{lat:0.0000}°N, {lon:0.0000}°E{rLabel}";
        }

        var flight = new Flight
        {
            StandaloneReason = request.StandaloneReason.Trim(),
            OrgUnitId = org.Id,
            UasId = request.UasId,
            PilotId = pilotId,
            BatteryId = request.BatteryId,
            TakeoffAt = request.TakeoffAt,
            LandingAt = request.LandingAt,
            FlightNumber = await NextFlightNumberAsync(cancellationToken),
            Location = locationText,
            LocationLat = lat,
            LocationLon = lon,
            LocationRadiusMeters = radius,
            Purpose = string.IsNullOrWhiteSpace(request.Purpose) ? null : request.Purpose.Trim(),
            Status = FlightStatus.Draft,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        RecalcDuration(flight);
        db.Flights.Add(flight);
        await db.SaveChangesAsync(cancellationToken);
        flight = await LoadAsync(flight.Id, cancellationToken);
        await audit.WriteAsync("Flight.CreateStandalone", nameof(Flight), flight.Id, null, Map(flight), cancellationToken: cancellationToken);
        return Map(flight);
    }

    public async Task<FlightDto> MarkReadyAsync(Guid id, uint rowVersion, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.FlightsWrite);
        var flight = await LoadAsync(id, cancellationToken);
        policy.EnsureOrgAccess(flight.OrgUnit.Path);
        if (flight.RowVersion != rowVersion)
            throw new ConflictException("Záznam bol medzitým zmenený iným používateľom.");
        if (!FlightStateMachine.CanTransition(flight.Status, FlightStatus.Ready))
            throw new ValidationException($"Nepovolený prechod {flight.Status} → Ready.");
        if (flight.OperationId is Guid opId)
            await operations.EnsureBeforeFlightConditionsMetAsync(opId, cancellationToken);

        flight.Status = FlightStatus.Ready;
        flight.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        return Map(flight);
    }

    public async Task<FlightDto> CloseAsync(Guid id, CloseFlightRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.FlightsWrite);
        var flight = await LoadAsync(id, cancellationToken);
        policy.EnsureOrgAccess(flight.OrgUnit.Path);
        if (flight.RowVersion != request.RowVersion)
            throw new ConflictException("Záznam bol medzitým zmenený iným používateľom.");
        if (flight.Status == FlightStatus.Closed && flight.CountersApplied)
            throw new ValidationException("Let je už uzatvorený; počítadlá boli aplikované práve raz.");
        if (!FlightStateMachine.CanTransition(flight.Status, FlightStatus.Closed)
            && flight.Status != FlightStatus.Draft)
            throw new ValidationException($"Nepovolený prechod {flight.Status} → Closed.");
        // Allow Draft -> Ready implied then Close for MVP convenience if Ready path used; prefer Ready.
        if (flight.Status == FlightStatus.Draft)
            flight.Status = FlightStatus.Ready;
        if (!FlightStateMachine.CanTransition(flight.Status, FlightStatus.Closed))
            throw new ValidationException($"Nepovolený prechod {flight.Status} → Closed.");

        if (string.IsNullOrWhiteSpace(request.PostFlightNotes))
            throw new ValidationException("Poletové poznámky sú povinné.");
        if (string.IsNullOrWhiteSpace(request.ChecklistResult))
            throw new ValidationException("Vyhodnotenie checklistu je povinné.");
        if (flight.TakeoffAt is null || flight.LandingAt is null)
            throw new ValidationException("Pred uzatvorením musí byť vyplnený čas vzletu a pristátia.");

        var before = Map(flight);
        flight.PostFlightNotes = request.PostFlightNotes.Trim();
        flight.ChecklistResult = request.ChecklistResult.Trim();
        flight.DeviationNotes = request.DeviationNotes;
        flight.Status = FlightStatus.Closed;
        flight.ClosedAt = clock.UtcNow;
        flight.ClosedByAccountId = currentUser.AccountId;
        flight.UpdatedBy = currentUser.AccountId;
        RecalcDuration(flight);

        if (!flight.CountersApplied)
        {
            ApplyCountersOnce(flight);
            flight.CountersApplied = true;
        }

        await db.SaveChangesAsync(cancellationToken);
        await maintenance.RecalculateAfterFlightAsync(flight.UasId, flight.BatteryId, flight.DurationMinutes ?? 0, cancellationToken);
        await audit.WriteAsync("Flight.Close", nameof(Flight), flight.Id, before, Map(flight), cancellationToken: cancellationToken);
        flight = await LoadAsync(flight.Id, cancellationToken);
        return Map(flight);
    }

    public async Task<TelemetryImportDto> ImportTelemetryAsync(ImportTelemetryRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.FlightsWrite);
        if (string.IsNullOrWhiteSpace(request.Payload))
            throw new ValidationException("Telemetrický obsah je povinný.");

        var hash = ComputeSha256(request.Payload);
        var existing = await db.TelemetryImports.FirstOrDefaultAsync(t => t.ContentHash == hash, cancellationToken);
        if (existing is not null)
        {
            existing.Status = TelemetryImportStatus.DuplicateIgnored;
            existing.ProcessingMessage = "Opakovaný import rovnakého súboru (rovnaký checksum) — duplicita ignorovaná.";
            await db.SaveChangesAsync(cancellationToken);
            return MapImport(existing);
        }

        var import = new TelemetryImport
        {
            FileName = string.IsNullOrWhiteSpace(request.FileName) ? "telemetry.json" : request.FileName.Trim(),
            ContentHash = hash,
            Format = string.IsNullOrWhiteSpace(request.Format) ? "geojson" : request.Format.Trim().ToLowerInvariant(),
            Source = "api-upload",
            Status = TelemetryImportStatus.Received,
            FlightId = request.FlightId,
            MatchedUasId = request.UasId,
            MatchedOperationId = request.OperationId,
            MatchedPilotId = request.PilotId,
            MatchedBatteryId = request.BatteryId,
            ReceivedAt = clock.UtcNow,
            OriginalPayload = request.Payload,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.TelemetryImports.Add(import);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            await ProcessImportAsync(import, cancellationToken);
        }
        catch (Exception ex)
        {
            import.Status = TelemetryImportStatus.Failed;
            import.ProcessingMessage = ex.Message;
            await db.SaveChangesAsync(cancellationToken);
        }

        return MapImport(import);
    }

    public async Task<TelemetryImportDto> PairTelemetryAsync(Guid importId, PairTelemetryRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.FlightsWrite);
        var import = await db.TelemetryImports.FirstOrDefaultAsync(t => t.Id == importId, cancellationToken)
            ?? throw new NotFoundException("Import telemetrie nebol nájdený.");

        import.FlightId = request.FlightId ?? import.FlightId;
        import.MatchedUasId = request.UasId ?? import.MatchedUasId;
        import.MatchedOperationId = request.OperationId ?? import.MatchedOperationId;
        import.MatchedPilotId = request.PilotId ?? import.MatchedPilotId;
        import.MatchedBatteryId = request.BatteryId ?? import.MatchedBatteryId;
        import.UpdatedBy = currentUser.AccountId;

        await ProcessImportAsync(import, cancellationToken);
        return MapImport(import);
    }

    private async Task ProcessImportAsync(TelemetryImport import, CancellationToken cancellationToken)
    {
        var points = ParseTrackPoints(import.OriginalPayload ?? "[]", import.Format);
        if (points.Count == 0)
        {
            import.Status = TelemetryImportStatus.NeedsPairing;
            import.ProcessingMessage = "Telemetria neobsahuje rozpoznateľnú GPS stopu.";
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        Flight? flight = null;
        if (import.FlightId is not null)
        {
            flight = await LoadAsync(import.FlightId.Value, cancellationToken);
        }
        else if (import.MatchedOperationId is not null || import.MatchedUasId is not null)
        {
            // Create draft flight from matched resources if none linked.
            var orgId = await ResolveOrgUnitIdAsync(import, cancellationToken);
            flight = new Flight
            {
                OperationId = import.MatchedOperationId,
                OrgUnitId = orgId,
                UasId = import.MatchedUasId,
                PilotId = import.MatchedPilotId,
                BatteryId = import.MatchedBatteryId,
                Status = FlightStatus.Draft,
                TakeoffAt = points.Min(p => p.RecordedAt),
                LandingAt = points.Max(p => p.RecordedAt),
                CreatedBy = currentUser.AccountId,
                UpdatedBy = currentUser.AccountId
            };
            RecalcDuration(flight);
            db.Flights.Add(flight);
            await db.SaveChangesAsync(cancellationToken);
            import.FlightId = flight.Id;
            flight = await LoadAsync(flight.Id, cancellationToken);
        }
        else
        {
            import.Status = TelemetryImportStatus.NeedsPairing;
            import.ProcessingMessage = "Import vyžaduje ručné spárovanie s letom/UAS/operáciou.";
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        policy.EnsureOrgAccess(flight.OrgUnit.Path);

        // Replace track points for this flight from this import (idempotent on re-pair of same import content once linked).
        var oldPoints = await db.FlightTrackPoints.Where(p => p.FlightId == flight.Id).ToListAsync(cancellationToken);
        db.FlightTrackPoints.RemoveRange(oldPoints);

        var seq = 0;
        foreach (var p in points.OrderBy(x => x.RecordedAt))
        {
            db.FlightTrackPoints.Add(new FlightTrackPoint
            {
                FlightId = flight.Id,
                RecordedAt = p.RecordedAt,
                Latitude = p.Latitude,
                Longitude = p.Longitude,
                AltitudeMeters = p.AltitudeMeters,
                SpeedMps = p.SpeedMps,
                Sequence = seq++,
                CreatedBy = currentUser.AccountId,
                UpdatedBy = currentUser.AccountId
            });
        }

        flight.TakeoffAt ??= points.Min(p => p.RecordedAt);
        flight.LandingAt ??= points.Max(p => p.RecordedAt);
        flight.MaxAltitudeMeters = points.Max(p => p.AltitudeMeters);
        flight.MaxSpeedMps = points.Max(p => p.SpeedMps);
        flight.DistanceMeters = ApproximateDistanceMeters(points);
        flight.TrackGeoJson = BuildLineStringGeoJson(points);
        RecalcDuration(flight);
        flight.UpdatedBy = currentUser.AccountId;

        import.Status = TelemetryImportStatus.Processed;
        import.ProcessingMessage = $"Spracovaných bodov: {points.Count}";
        import.FlightId = flight.Id;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Guid> ResolveOrgUnitIdAsync(TelemetryImport import, CancellationToken cancellationToken)
    {
        if (import.MatchedOperationId is not null)
        {
            var op = await db.Operations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == import.MatchedOperationId, cancellationToken)
                ?? throw new NotFoundException("Operácia pre párovanie nebola nájdená.");
            return op.OrgUnitId;
        }

        if (import.MatchedUasId is not null)
        {
            var uas = await db.UasAircraft.AsNoTracking().FirstOrDefaultAsync(u => u.Id == import.MatchedUasId, cancellationToken)
                ?? throw new NotFoundException("UAS pre párovanie nebolo nájdené.");
            if (uas.OwningOrgUnitId is null)
                throw new ValidationException("UAS nemá priradený útvar.");
            return uas.OwningOrgUnitId.Value;
        }

        throw new ValidationException("Nie je možné určiť organizačný útvar.");
    }

    private void ApplyCountersOnce(Flight flight)
    {
        var minutes = flight.DurationMinutes ?? 0;
        if (flight.UasId is not null)
        {
            var uas = db.UasAircraft.First(u => u.Id == flight.UasId);
            uas.TotalFlightCount += 1;
            uas.TotalFlightMinutes += minutes;
            uas.UpdatedBy = currentUser.AccountId;
        }

        if (flight.BatteryId is not null)
        {
            var battery = db.Batteries.First(b => b.Id == flight.BatteryId);
            battery.CycleCount += 1;
            battery.UpdatedBy = currentUser.AccountId;
        }

        if (flight.PilotId is not null)
        {
            var pilot = db.Pilots.First(p => p.Id == flight.PilotId);
            pilot.LastFlightAt = flight.LandingAt ?? clock.UtcNow;
            // Default currency window 90 days after last flight (configurable later).
            pilot.CurrencyValidUntil = (flight.LandingAt ?? clock.UtcNow).AddDays(90);
            if (pilot.Status is PilotStatus.Fit or PilotStatus.Active or PilotStatus.Expiring or PilotStatus.Unfit)
                pilot.Status = PilotStatus.Fit;
            pilot.UpdatedBy = currentUser.AccountId;
        }
    }

    private async Task<string> NextFlightNumberAsync(CancellationToken cancellationToken)
    {
        var year = clock.UtcNow.Year;
        var prefix = $"LET-{year}-";
        var existing = await db.Flights.AsNoTracking()
            .Where(f => f.FlightNumber != null && f.FlightNumber.StartsWith(prefix))
            .Select(f => f.FlightNumber!)
            .ToListAsync(cancellationToken);

        var max = 0;
        foreach (var number in existing)
        {
            var suffix = number.Length > prefix.Length ? number[prefix.Length..] : "";
            if (int.TryParse(suffix, out var seq) && seq > max)
                max = seq;
        }

        return $"{prefix}{(max + 1):0000}";
    }

    private async Task<Flight> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Flights
            .Include(f => f.OrgUnit)
            .Include(f => f.Uas)
            .Include(f => f.Pilot)!.ThenInclude(p => p!.Person)
            .Include(f => f.Battery)
            .Include(f => f.TrackPoints)
            .Include(f => f.TelemetryImports)
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
            ?? throw new NotFoundException("Let nebol nájdený.");

    private static void RecalcDuration(Flight flight)
    {
        if (flight.TakeoffAt is not null && flight.LandingAt is not null && flight.LandingAt >= flight.TakeoffAt)
            flight.DurationMinutes = (int)Math.Round((flight.LandingAt.Value - flight.TakeoffAt.Value).TotalMinutes);
    }

    private static string ComputeSha256(string payload)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes);
    }

    private static List<ParsedPoint> ParseTrackPoints(string payload, string format)
    {
        var points = new List<ParsedPoint>();
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        if (format is "geojson" or "json")
        {
            if (root.TryGetProperty("features", out var features))
            {
                var i = 0;
                foreach (var f in features.EnumerateArray())
                {
                    if (!f.TryGetProperty("geometry", out var geom)) continue;
                    if (!geom.TryGetProperty("coordinates", out var coords)) continue;
                    if (coords.ValueKind != JsonValueKind.Array || coords.GetArrayLength() < 2) continue;
                    var lon = coords[0].GetDouble();
                    var lat = coords[1].GetDouble();
                    double? alt = coords.GetArrayLength() > 2 ? coords[2].GetDouble() : null;
                    var at = clockFallback(i++);
                    if (f.TryGetProperty("properties", out var props) && props.TryGetProperty("time", out var timeProp)
                        && DateTimeOffset.TryParse(timeProp.GetString(), out var parsed))
                        at = parsed;
                    points.Add(new ParsedPoint(at, lat, lon, alt, null));
                }
            }
            else if (root.TryGetProperty("points", out var arr))
            {
                foreach (var p in arr.EnumerateArray())
                {
                    var lat = p.GetProperty("lat").GetDouble();
                    var lon = p.GetProperty("lon").GetDouble();
                    DateTimeOffset at = p.TryGetProperty("time", out var t) && DateTimeOffset.TryParse(t.GetString(), out var parsed)
                        ? parsed
                        : DateTimeOffset.UtcNow;
                    double? alt = p.TryGetProperty("alt", out var a) ? a.GetDouble() : null;
                    double? spd = p.TryGetProperty("speed", out var s) ? s.GetDouble() : null;
                    points.Add(new ParsedPoint(at, lat, lon, alt, spd));
                }
            }
        }

        return points;

        static DateTimeOffset clockFallback(int i) => DateTimeOffset.UtcNow.AddSeconds(i);
    }

    private static double ApproximateDistanceMeters(List<ParsedPoint> points)
    {
        double total = 0;
        for (var i = 1; i < points.Count; i++)
            total += Haversine(points[i - 1].Latitude, points[i - 1].Longitude, points[i].Latitude, points[i].Longitude);
        return total;
    }

    private static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * R * Math.Asin(Math.Sqrt(a));
    }

    private static string BuildLineStringGeoJson(List<ParsedPoint> points)
    {
        var coords = string.Join(",", points.OrderBy(p => p.RecordedAt).Select(p => $"[{p.Longitude},{p.Latitude}]"));
        return $"{{\"type\":\"Feature\",\"geometry\":{{\"type\":\"LineString\",\"coordinates\":[{coords}]}},\"properties\":{{}}}}";
    }

    public async Task<(string ContentType, string FileName, string Body)> ExportTrackAsync(
        Guid flightId, string format, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.FlightsRead);
        var flight = await LoadAsync(flightId, cancellationToken);
        policy.EnsureOrgAccess(flight.OrgUnit.Path);

        var points = flight.TrackPoints.OrderBy(p => p.Sequence).ToList();
        if (points.Count == 0 && string.IsNullOrWhiteSpace(flight.TrackGeoJson))
            throw new ValidationException("Let nemá GPS stopu na export.");

        var fmt = (format ?? "geojson").Trim().ToLowerInvariant();
        if (fmt is "geojson" or "json")
        {
            var geo = !string.IsNullOrWhiteSpace(flight.TrackGeoJson)
                ? flight.TrackGeoJson!
                : BuildLineStringGeoJson(points.Select(p =>
                    new ParsedPoint(p.RecordedAt, p.Latitude, p.Longitude, p.AltitudeMeters, p.SpeedMps)).ToList());
            return ("application/geo+json", $"flight-{flightId:N}-track.geojson", geo);
        }

        if (fmt is "kml" or "kmz")
        {
            var coords = string.Join(" ", points.Select(p => $"{p.Longitude},{p.Latitude},{p.AltitudeMeters ?? 0}"));
            if (points.Count == 0 && !string.IsNullOrWhiteSpace(flight.TrackGeoJson))
                throw new ValidationException("KML export vyžaduje body stopy (nie iba uložené GeoJSON).");
            var kml =
                $"""<?xml version="1.0" encoding="UTF-8"?><kml xmlns="http://www.opengis.net/kml/2.2"><Document><name>Flight {flightId}</name><Placemark><name>Track</name><LineString><tessellate>1</tessellate><coordinates>{coords}</coordinates></LineString></Placemark></Document></kml>""";
            return ("application/vnd.google-earth.kml+xml", $"flight-{flightId:N}-track.kml", kml);
        }

        throw new ValidationException("Podporované formáty: geojson, kml.");
    }

    private static FlightDto Map(Flight f) =>
        new(f.Id, f.OperationId, f.StandaloneReason, f.OrgUnitId, f.OrgUnit.Name, f.UasId,
            f.Uas?.RegistrationNumber ?? f.Uas?.SerialNumber, f.PilotId,
            f.Pilot is null ? null : $"{f.Pilot.Person.FirstName} {f.Pilot.Person.LastName}",
            f.BatteryId, f.Status, f.TakeoffAt, f.LandingAt, f.DurationMinutes,
            f.FlightNumber, f.Location, f.Purpose,
            f.LocationLat, f.LocationLon, f.LocationRadiusMeters,
            f.DistanceMeters, f.MaxAltitudeMeters, f.CountersApplied, f.IsActive, f.RowVersion);

    private static FlightDetailDto ToDetail(Flight f) =>
        new(Map(f), f.PostFlightNotes, f.ChecklistResult, f.DeviationNotes, f.TrackGeoJson,
            f.TrackPoints.OrderBy(p => p.Sequence).Select(p => new FlightTrackPointDto(
                p.Id, p.RecordedAt, p.Latitude, p.Longitude, p.AltitudeMeters, p.SpeedMps, p.Sequence)).ToList(),
            f.TelemetryImports.OrderByDescending(t => t.ReceivedAt).Select(MapImport).ToList());

    private static TelemetryImportDto MapImport(TelemetryImport t) =>
        new(t.Id, t.FileName, t.ContentHash, t.Format, t.Source, t.Status, t.ProcessingMessage, t.FlightId, t.ReceivedAt);

    private sealed record ParsedPoint(DateTimeOffset RecordedAt, double Latitude, double Longitude, double? AltitudeMeters, double? SpeedMps);
}
