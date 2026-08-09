using System.Globalization;
using System.Text;
using System.Text.Json;
using IsSuas.Application.Abstractions;
using IsSuas.Application.Policies;
using IsSuas.Application.Reporting;
using IsSuas.Domain.Enums;
using IsSuas.Domain.Reporting;
using IsSuas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IsSuas.Infrastructure.Services;

public interface IReportingService
{
    Task<DashboardDto> GetDashboardAsync(string? scope = null, Guid? orgUnitId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CalendarItemDto>> GetCalendarAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
    Task<ReportDto> GenerateReportAsync(ReportRequest request, CancellationToken cancellationToken = default);
    Task<string> ExportReportCsvAsync(ReportRequest request, CancellationToken cancellationToken = default);
    Task<OfficialReportDto> CreateOfficialAsync(CreateOfficialReportRequest request, CancellationToken cancellationToken = default);
    Task<OfficialReportDto> ApproveOfficialAsync(Guid id, ApproveOfficialReportRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OfficialReportDto>> ListOfficialAsync(CancellationToken cancellationToken = default);
    Task<OfficialReportDto> GetOfficialAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed class ReportingService(
    IsSuasDbContext db,
    ICurrentUser currentUser,
    IAuthorizationPolicyService policy,
    IAuditWriter audit,
    IClock clock) : IReportingService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<DashboardDto> GetDashboardAsync(
        string? scope = null,
        Guid? orgUnitId = null,
        CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.ReportsRead);
        if (!policy.CanAccessBusinessData())
            throw new ForbiddenException("Systémový administrátor nemá automatický prístup k vecnému obsahu.");

        var scopeNorm = (scope ?? "central").Trim().ToLowerInvariant();
        if (scopeNorm is not ("central" or "branch" or "unit"))
            throw new ValidationException("Neplatný scope. Povolené: central, branch, unit.");

        string? scopePath = null;
        Guid? scopeOrgId = null;
        string? scopeOrgName = null;

        if (scopeNorm is "branch" or "unit")
        {
            if (orgUnitId is null)
                throw new ValidationException("Pre scope branch/unit je povinné orgUnitId.");
            var org = await db.OrgUnits.AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == orgUnitId && o.IsActive, cancellationToken)
                ?? throw new NotFoundException("Útvar nebol nájdený.");
            policy.EnsureOrgAccess(org.Path);
            scopePath = org.Path;
            scopeOrgId = org.Id;
            scopeOrgName = org.Name;
        }
        else if (orgUnitId is Guid oid)
        {
            // Backward-compatible: central + orgUnitId still filters like Wave 12.
            var org = await db.OrgUnits.AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == oid && o.IsActive, cancellationToken)
                ?? throw new NotFoundException("Útvar nebol nájdený.");
            policy.EnsureOrgAccess(org.Path);
            scopePath = org.Path;
            scopeOrgId = org.Id;
            scopeOrgName = org.Name;
        }

        bool InScope(string? path) =>
            scopePath is null
                ? path is null || policy.CanAccessOrgUnit(path)
                : path is not null && path.StartsWith(scopePath, StringComparison.Ordinal);

        var now = clock.UtcNow;
        var dayStart = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1);

        var uas = await db.UasAircraft.AsNoTracking().Include(u => u.OwningOrgUnit)
            .Where(u => u.IsActive).ToListAsync(cancellationToken);
        uas = uas.Where(u => InScope(u.OwningOrgUnit?.Path)).ToList();

        var fleet = new FleetSummaryDto(
            uas.Count,
            uas.Count(u => u.Status == UasLifecycleStatus.InService),
            uas.Count(u => u.Status == UasLifecycleStatus.Suspended),
            uas.Count(u => u.Status == UasLifecycleStatus.InMaintenance),
            uas.Count(u => u.Status == UasLifecycleStatus.Draft));

        var ops = await db.Operations.AsNoTracking().Include(o => o.OrgUnit)
            .Where(o => o.IsActive && o.PlannedStart < dayEnd && o.PlannedEnd >= dayStart)
            .ToListAsync(cancellationToken);
        ops = ops.Where(o => InScope(o.OrgUnit.Path)).ToList();

        var upcoming = await db.Operations.AsNoTracking().Include(o => o.OrgUnit)
            .Where(o => o.IsActive && o.PlannedStart >= now && o.PlannedStart <= now.AddDays(14))
            .OrderBy(o => o.PlannedStart)
            .Take(50)
            .ToListAsync(cancellationToken);
        upcoming = upcoming.Where(o => InScope(o.OrgUnit.Path)).Take(20).ToList();

        // Personal / inbox metrics stay global for the current user (not org-scoped).
        var openTasks = await db.WorkTasks.AsNoTracking().CountAsync(t => t.IsActive && t.Status == WorkTaskStatus.Open, cancellationToken);
        var openDefects = await db.Defects.AsNoTracking().CountAsync(d =>
            d.IsActive && d.Status != DefectStatus.Resolved && d.Status != DefectStatus.Closed, cancellationToken);
        var openIncidents = await db.SafetyEvents.AsNoTracking().CountAsync(e =>
            e.IsActive && e.Status != SafetyEventStatus.Closed && e.Status != SafetyEventStatus.Cancelled, cancellationToken);
        var unread = await db.AppNotifications.AsNoTracking().CountAsync(n =>
            n.IsActive && n.Status == NotificationStatus.Unread, cancellationToken);
        var overdueMaint = await db.MaintenancePlans.AsNoTracking().CountAsync(p =>
            p.IsActive && (p.Status == MaintenancePlanStatus.Overdue
                || (p.NextDueAt != null && p.NextDueAt < now)), cancellationToken);

        var alerts = await db.AppNotifications.AsNoTracking()
            .Where(n => n.IsActive && n.Status == NotificationStatus.Unread
                && (n.Priority == "Critical" || n.Priority == "High"))
            .OrderByDescending(n => n.CreatedAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        var closedFlights = await db.Flights.AsNoTracking()
            .Include(f => f.OrgUnit)
            .Include(f => f.Uas)
            .Where(f => f.IsActive && f.Status == FlightStatus.Closed && f.DurationMinutes != null && f.DurationMinutes > 0)
            .ToListAsync(cancellationToken);
        closedFlights = closedFlights.Where(f => InScope(f.OrgUnit.Path)).ToList();

        var totalFlightHours = Math.Round((decimal)closedFlights.Sum(f => f.DurationMinutes!.Value) / 60m, 1);

        var byOrg = closedFlights
            .GroupBy(f => new { f.OrgUnitId, f.OrgUnit.Code, f.OrgUnit.Name })
            .Select(g => new FlightHoursSliceDto(
                g.Key.Code,
                g.Key.Name,
                Math.Round(g.Sum(x => x.DurationMinutes!.Value) / 60m, 1),
                g.Count()))
            .OrderByDescending(x => x.Hours)
            .ToList();

        var byUasRaw = closedFlights
            .Where(f => f.UasId != null)
            .GroupBy(f => new
            {
                Id = f.UasId!.Value,
                Label = f.Uas!.RegistrationNumber ?? f.Uas.SerialNumber,
                Serial = f.Uas.SerialNumber
            })
            .Select(g => new FlightHoursSliceDto(
                g.Key.Serial,
                g.Key.Label,
                Math.Round(g.Sum(x => x.DurationMinutes!.Value) / 60m, 1),
                g.Count()))
            .OrderByDescending(x => x.Hours)
            .ToList();

        const int topUas = 10;
        IReadOnlyList<FlightHoursSliceDto> byUas;
        if (byUasRaw.Count <= topUas)
            byUas = byUasRaw;
        else
        {
            var top = byUasRaw.Take(topUas).ToList();
            var rest = byUasRaw.Skip(topUas).ToList();
            top.Add(new FlightHoursSliceDto(
                "OTHER",
                $"Ostatné drony ({rest.Count})",
                Math.Round(rest.Sum(x => x.Hours), 1),
                rest.Sum(x => x.FlightCount)));
            byUas = top;
        }

        var roleHint = currentUser.Roles.Select(r => r.RoleCode).FirstOrDefault() ?? "User";

        return new DashboardDto(
            roleHint,
            fleet,
            ops.Count,
            openTasks,
            openDefects,
            openIncidents,
            unread,
            overdueMaint,
            upcoming
                .Select(o => new CalendarItemDto(o.Id, o.Title, o.Status.ToString(), o.PlannedStart, o.PlannedEnd, o.OrgUnit.Name, o.IsUrgent))
                .ToList(),
            alerts.Select(a => new AlertItemDto(a.Id, a.Title, a.Priority, a.TypeCode, a.RelatedEntityId, a.RelatedEntityType)).ToList(),
            scopeNorm,
            scopeOrgId,
            scopeOrgName,
            totalFlightHours,
            byOrg,
            byUas);
    }

    public async Task<IReadOnlyList<CalendarItemDto>> GetCalendarAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.OperationsRead);
        var items = await db.Operations.AsNoTracking().Include(o => o.OrgUnit)
            .Where(o => o.IsActive && o.PlannedStart <= to && o.PlannedEnd >= from)
            .OrderBy(o => o.PlannedStart)
            .ToListAsync(cancellationToken);
        return items.Where(o => policy.CanAccessOrgUnit(o.OrgUnit.Path))
            .Select(o => new CalendarItemDto(o.Id, o.Title, o.Status.ToString(), o.PlannedStart, o.PlannedEnd, o.OrgUnit.Name, o.IsUrgent))
            .ToList();
    }

    public async Task<ReportDto> GenerateReportAsync(ReportRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.ReportsRead);
        var dataAsOf = clock.UtcNow;
        var code = (request.ReportTypeCode ?? "").Trim().ToUpperInvariant();
        return code switch
        {
            "UAS" => await ReportUasAsync(request, dataAsOf, cancellationToken),
            "PILOTS" => await ReportPilotsAsync(request, dataAsOf, cancellationToken),
            "FLIGHTS" => await ReportFlightsAsync(request, dataAsOf, cancellationToken),
            "BATTERIES" => await ReportBatteriesAsync(request, dataAsOf, cancellationToken),
            "MAINTENANCE" => await ReportMaintenanceAsync(request, dataAsOf, cancellationToken),
            "DEFECTS" => await ReportDefectsAsync(request, dataAsOf, cancellationToken),
            "INCIDENTS" => await ReportIncidentsAsync(request, dataAsOf, cancellationToken),
            "OPERATIONS" => await ReportOperationsAsync(request, dataAsOf, cancellationToken),
            _ => throw new ValidationException("Neznámy typ reportu. Povolené: UAS, PILOTS, FLIGHTS, BATTERIES, MAINTENANCE, DEFECTS, INCIDENTS, OPERATIONS.")
        };
    }

    public async Task<string> ExportReportCsvAsync(ReportRequest request, CancellationToken cancellationToken = default)
    {
        var report = await GenerateReportAsync(request, cancellationToken);
        var sb = new StringBuilder();
        sb.AppendLine("reportType;title;dataAsOf;rulesVersion");
        sb.AppendLine($"{report.ReportTypeCode};{Escape(report.TitleSk)};{report.DataAsOf:o};{report.RulesVersion}");
        sb.AppendLine("key;label;value;unit;group");
        foreach (var row in report.Rows)
            sb.AppendLine($"{Escape(row.Key)};{Escape(row.Label)};{row.Value.ToString(CultureInfo.InvariantCulture)};{Escape(row.Unit)};{Escape(row.Group)}");
        sb.AppendLine("totalKey;totalValue");
        foreach (var t in report.Totals)
            sb.AppendLine($"{Escape(t.Key)};{t.Value.ToString(CultureInfo.InvariantCulture)}");
        return sb.ToString();
    }

    public async Task<OfficialReportDto> CreateOfficialAsync(CreateOfficialReportRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.ReportsWrite);
        if (currentUser.AccountId is null)
            throw new ForbiddenException("Chýba identita.");

        var live = await GenerateReportAsync(new ReportRequest(request.ReportTypeCode, request.From, request.To, request.OrgUnitId), cancellationToken);
        var familyId = Guid.NewGuid();
        var version = 1;
        if (request.SupersedesReportId is Guid prevId)
        {
            var prev = await db.OfficialReports.FirstOrDefaultAsync(r => r.Id == prevId, cancellationToken)
                ?? throw new NotFoundException("Nahrádzaný výkaz nebol nájdený.");
            if (prev.Status != OfficialReportStatus.Approved)
                throw new ValidationException("Nahradiť je možné iba schválený oficiálny výkaz.");
            familyId = prev.ReportFamilyId;
            version = prev.Version + 1;
        }

        var entity = new OfficialReport
        {
            ReportTypeCode = live.ReportTypeCode,
            TitleSk = string.IsNullOrWhiteSpace(request.TitleSk) ? live.TitleSk : request.TitleSk.Trim(),
            Version = version,
            ReportFamilyId = familyId,
            SupersedesReportId = request.SupersedesReportId,
            DataAsOf = live.DataAsOf,
            FiltersJson = JsonSerializer.Serialize(new { request.From, request.To, request.OrgUnitId }, JsonOpts),
            RulesJson = ReportRules.DescriptionJson,
            SnapshotJson = JsonSerializer.Serialize(live, JsonOpts),
            Status = OfficialReportStatus.Draft,
            PreparedByAccountId = currentUser.AccountId.Value,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.OfficialReports.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("OfficialReport.Create", nameof(OfficialReport), entity.Id, null, MapOfficial(entity), cancellationToken: cancellationToken);
        return MapOfficial(entity);
    }

    public async Task<OfficialReportDto> ApproveOfficialAsync(Guid id, ApproveOfficialReportRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.ReportsOfficial);
        if (currentUser.AccountId is null)
            throw new ForbiddenException("Chýba identita.");
        if (string.IsNullOrWhiteSpace(request.ApprovalReason))
            throw new ValidationException("Dôvod schválenia je povinný.");

        var entity = await db.OfficialReports.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException("Oficiálny výkaz nebol nájdený.");
        if (entity.RowVersion != request.RowVersion)
            throw new ConflictException("Záznam bol medzitým zmenený iným používateľom.");
        if (entity.Status != OfficialReportStatus.Draft)
            throw new ValidationException("Schváliť je možné iba koncept výkazu.");
        if (entity.PreparedByAccountId == currentUser.AccountId.Value)
            throw new ForbiddenException("Oddelenie zodpovedností: pripravovateľ nemôže schváliť vlastný výkaz.");

        if (entity.SupersedesReportId is Guid prevId)
        {
            var prev = await db.OfficialReports.FirstOrDefaultAsync(r => r.Id == prevId, cancellationToken);
            if (prev is not null && prev.Status == OfficialReportStatus.Approved)
            {
                prev.Status = OfficialReportStatus.Superseded;
                prev.UpdatedBy = currentUser.AccountId;
            }
        }

        // Snapshot stays frozen — do not regenerate from live data.
        entity.Status = OfficialReportStatus.Approved;
        entity.ApprovedByAccountId = currentUser.AccountId;
        entity.ApprovedAt = clock.UtcNow;
        entity.ApprovalReason = request.ApprovalReason.Trim();
        entity.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("OfficialReport.Approve", nameof(OfficialReport), entity.Id, null, MapOfficial(entity), cancellationToken: cancellationToken);
        return MapOfficial(entity);
    }

    public async Task<IReadOnlyList<OfficialReportDto>> ListOfficialAsync(CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.ReportsRead);
        var items = await db.OfficialReports.AsNoTracking()
            .Where(r => r.IsActive)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
        return items.Select(MapOfficial).ToList();
    }

    public async Task<OfficialReportDto> GetOfficialAsync(Guid id, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.ReportsRead);
        var entity = await db.OfficialReports.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException("Oficiálny výkaz nebol nájdený.");
        return MapOfficial(entity);
    }

    private async Task<ReportDto> ReportUasAsync(ReportRequest request, DateTimeOffset dataAsOf, CancellationToken ct)
    {
        var items = await db.UasAircraft.AsNoTracking().Where(u => u.IsActive).ToListAsync(ct);
        var rows = items.GroupBy(u => u.Status)
            .Select(g => new ReportRowDto(g.Key.ToString(), $"UAS stav {StatusSk(g.Key.ToString())}", g.Count(), "ks", "status"))
            .ToList();
        return new ReportDto("UAS", "Výkaz flotily UAS", dataAsOf, ReportRules.Version, rows,
            new Dictionary<string, decimal> { ["uas_total"] = items.Count });
    }

    private async Task<ReportDto> ReportPilotsAsync(ReportRequest request, DateTimeOffset dataAsOf, CancellationToken ct)
    {
        var items = await db.Pilots.AsNoTracking().Where(p => p.IsActive).ToListAsync(ct);
        var rows = items.GroupBy(p => p.Status)
            .Select(g => new ReportRowDto(g.Key.ToString(), $"Piloti — {StatusSk(g.Key.ToString())}", g.Count(), "osôb", "status"))
            .ToList();
        return new ReportDto("PILOTS", "Výkaz pilotov", dataAsOf, ReportRules.Version, rows,
            new Dictionary<string, decimal> { ["pilots_total"] = items.Count });
    }

    private async Task<ReportDto> ReportFlightsAsync(ReportRequest request, DateTimeOffset dataAsOf, CancellationToken ct)
    {
        var query = db.Flights.AsNoTracking().Where(f => f.IsActive);
        if (request.From is not null) query = query.Where(f => f.TakeoffAt >= request.From);
        if (request.To is not null) query = query.Where(f => f.TakeoffAt <= request.To);
        var items = await query.ToListAsync(ct);
        var closed = items.Where(f => f.Status == FlightStatus.Closed).ToList();
        var duration = closed.Sum(f => f.DurationMinutes ?? 0);
        var rows = new List<ReportRowDto>
        {
            new("flights_count", "Počet letov (nie operácií)", items.Count, "ks", "flights"),
            new("flights_closed", "Uzatvorené lety", closed.Count, "ks", "flights"),
            new("flight_minutes", "Súčet minút uzatvorených letov", duration, "min", "flights")
        };
        return new ReportDto("FLIGHTS", "Výkaz letov", dataAsOf, ReportRules.Version, rows,
            new Dictionary<string, decimal>
            {
                ["flights_count"] = items.Count,
                ["flight_minutes"] = duration
            });
    }

    private async Task<ReportDto> ReportOperationsAsync(ReportRequest request, DateTimeOffset dataAsOf, CancellationToken ct)
    {
        var query = db.Operations.AsNoTracking().Where(o => o.IsActive);
        if (request.From is not null) query = query.Where(o => o.PlannedStart >= request.From);
        if (request.To is not null) query = query.Where(o => o.PlannedStart <= request.To);
        var items = await query.ToListAsync(ct);
        var rows = new List<ReportRowDto>
        {
            new("operations_count", "Počet operácií (nie letov)", items.Count, "ks", "operations"),
            new("operations_approved", "Schválené", items.Count(o => o.Status == OperationStatus.Approved), "ks", "operations"),
            new("operations_urgent", "Neodkladné", items.Count(o => o.IsUrgent), "ks", "operations")
        };
        return new ReportDto("OPERATIONS", "Výkaz operácií", dataAsOf, ReportRules.Version, rows,
            new Dictionary<string, decimal> { ["operations_count"] = items.Count });
    }

    private async Task<ReportDto> ReportBatteriesAsync(ReportRequest request, DateTimeOffset dataAsOf, CancellationToken ct)
    {
        var items = await db.Batteries.AsNoTracking().Where(b => b.IsActive).ToListAsync(ct);
        var rows = new List<ReportRowDto>
        {
            new("batteries_total", "Batérie celkom", items.Count, "ks", "batteries"),
            new("battery_cycles_sum", "Súčet cyklov", items.Sum(b => b.CycleCount), "cykly", "batteries")
        };
        return new ReportDto("BATTERIES", "Výkaz batérií", dataAsOf, ReportRules.Version, rows,
            new Dictionary<string, decimal> { ["batteries_total"] = items.Count });
    }

    private async Task<ReportDto> ReportMaintenanceAsync(ReportRequest request, DateTimeOffset dataAsOf, CancellationToken ct)
    {
        var plans = await db.MaintenancePlans.AsNoTracking().Where(p => p.IsActive).ToListAsync(ct);
        var overdue = plans.Count(p => p.Status == MaintenancePlanStatus.Overdue || (p.NextDueAt != null && p.NextDueAt < dataAsOf));
        var rows = new List<ReportRowDto>
        {
            new("plans_total", "Plány údržby", plans.Count, "ks", "maintenance"),
            new("plans_overdue", "Po termíne", overdue, "ks", "maintenance")
        };
        return new ReportDto("MAINTENANCE", "Výkaz údržby", dataAsOf, ReportRules.Version, rows,
            new Dictionary<string, decimal> { ["plans_total"] = plans.Count, ["plans_overdue"] = overdue });
    }

    private async Task<ReportDto> ReportDefectsAsync(ReportRequest request, DateTimeOffset dataAsOf, CancellationToken ct)
    {
        var items = await db.Defects.AsNoTracking().Where(d => d.IsActive).ToListAsync(ct);
        var open = items.Count(d => d.Status is not (DefectStatus.Resolved or DefectStatus.Closed));
        var rows = new List<ReportRowDto>
        {
            new("defects_total", "Poruchy celkom", items.Count, "ks", "defects"),
            new("defects_open", "Otvorené", open, "ks", "defects")
        };
        return new ReportDto("DEFECTS", "Výkaz porúch", dataAsOf, ReportRules.Version, rows,
            new Dictionary<string, decimal> { ["defects_total"] = items.Count, ["defects_open"] = open });
    }

    private async Task<ReportDto> ReportIncidentsAsync(ReportRequest request, DateTimeOffset dataAsOf, CancellationToken ct)
    {
        var items = await db.SafetyEvents.AsNoTracking().Where(e => e.IsActive).ToListAsync(ct);
        var critical = items.Count(e => e.Severity == SafetySeverity.Critical);
        var rows = new List<ReportRowDto>
        {
            new("incidents_total", "Incidenty celkom", items.Count, "ks", "incidents"),
            new("incidents_critical", "Kritické", critical, "ks", "incidents")
        };
        return new ReportDto("INCIDENTS", "Výkaz incidentov", dataAsOf, ReportRules.Version, rows,
            new Dictionary<string, decimal> { ["incidents_total"] = items.Count, ["incidents_critical"] = critical });
    }

    private static OfficialReportDto MapOfficial(OfficialReport r) =>
        new(r.Id, r.ReportFamilyId, r.ReportTypeCode, r.TitleSk, r.Version, r.DataAsOf, r.Status,
            r.PreparedByAccountId, r.ApprovedByAccountId, r.ApprovedAt, r.ApprovalReason,
            r.SnapshotJson, r.RulesJson, r.RowVersion);

    private static string StatusSk(string code) => code switch
    {
        "Draft" => "Koncept",
        "Submitted" => "Podané",
        "Approved" => "Schválené",
        "Returned" => "Vrátené",
        "Rejected" => "Zamietnuté",
        "Cancelled" => "Zrušené",
        "Active" => "Aktívne",
        "Revoked" => "Odvolané",
        "Open" => "Otvorené",
        "Closed" => "Uzavreté",
        "Resolved" => "Vyriešené",
        "InProgress" => "Prebieha",
        "Pending" => "Čaká",
        "Ready" => "Pripravené",
        "Reported" => "Nahlásené",
        "UnderRepair" => "V oprave",
        "InService" => "V prevádzke",
        "Suspended" => "Pozastavené",
        "InMaintenance" => "V údržbe",
        "Deactivated" => "Deaktivovaný",
        "TemporarilyBlocked" => "Dočasne blokovaný",
        "Prepared" => "Pripravený",
        "Eligible" => "Spôsobilý",
        "NotEligible" => "Nespôsobilý",
        "Registered" => "Registrovaný",
        "Available" => "Dostupný",
        "InUse" => "Používaný",
        "Overdue" => "Po termíne",
        "Planned" => "Plánované",
        "Completed" => "Dokončené",
        "Unread" => "Neprečítané",
        "Read" => "Prečítané",
        "Critical" => "Kritická",
        "High" => "Vysoká",
        "Medium" => "Stredná",
        "Low" => "Nízka",
        "Blocking" => "Blokujúca",
        _ => code
    };

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Replace("\"", "\"\"").Contains(';') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}
