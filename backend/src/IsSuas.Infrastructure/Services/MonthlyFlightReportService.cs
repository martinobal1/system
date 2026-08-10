using System.Text.Json;
using IsSuas.Application.Abstractions;
using IsSuas.Application.Policies;
using IsSuas.Application.Reporting;
using IsSuas.Domain.Enums;
using IsSuas.Domain.Reporting;
using IsSuas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IsSuas.Infrastructure.Services;

public interface IMonthlyFlightReportService
{
    Task<MonthlyFlightReportDto> GetOrCreateDraftAsync(Guid? orgUnitId, int year, int month, CancellationToken cancellationToken = default);
    Task<MonthlyFlightReportDto?> GetAsync(Guid? orgUnitId, int year, int month, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MonthlyFlightReportDto>> ListAsync(int? year, int? month, Guid? orgUnitId, CancellationToken cancellationToken = default);
    Task<MonthlyFlightReportDto> SaveDraftAsync(Guid id, SaveMonthlyFlightReportRequest request, CancellationToken cancellationToken = default);
    Task<MonthlyFlightReportDto> ConfirmAsync(Guid id, ConfirmMonthlyFlightReportRequest request, CancellationToken cancellationToken = default);
    Task<MonthlyFlightReportDto> MarkNoActivityAsync(Guid id, MarkNoActivityMonthlyFlightReportRequest request, CancellationToken cancellationToken = default);
    Task<MonthlyFlightReportDto> AmendAsync(Guid id, AmendMonthlyFlightReportRequest request, CancellationToken cancellationToken = default);
}

public sealed class MonthlyFlightReportService(
    IsSuasDbContext db,
    IAuthorizationPolicyService policy,
    ICurrentUser currentUser,
    IAuditWriter audit,
    IClock clock) : IMonthlyFlightReportService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<MonthlyFlightReportDto> GetOrCreateDraftAsync(Guid? orgUnitId, int year, int month, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.ReportsWrite);
        ValidateYearMonth(year, month);
        var org = await ResolveWritableOrgAsync(orgUnitId, cancellationToken);

        var existing = await LoadByOrgMonthAsync(org.Id, year, month, cancellationToken);
        if (existing is not null)
            return Map(existing);

        var report = new MonthlyFlightReport
        {
            OrgUnitId = org.Id,
            Year = year,
            Month = month,
            Status = MonthlyFlightReportStatus.Draft,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.MonthlyFlightReports.Add(report);
        await db.SaveChangesAsync(cancellationToken);
        report.OrgUnit = org;
        await audit.WriteAsync("MonthlyFlightReport.Create", nameof(MonthlyFlightReport), report.Id, null, Map(report), cancellationToken: cancellationToken);
        return Map(report);
    }

    public async Task<MonthlyFlightReportDto?> GetAsync(Guid? orgUnitId, int year, int month, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.ReportsRead);
        ValidateYearMonth(year, month);
        var org = await ResolveReadableOrgAsync(orgUnitId, cancellationToken);
        var existing = await LoadByOrgMonthAsync(org.Id, year, month, cancellationToken);
        return existing is null ? null : Map(existing);
    }

    public async Task<IReadOnlyList<MonthlyFlightReportDto>> ListAsync(int? year, int? month, Guid? orgUnitId, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.ReportsRead);
        var query = db.MonthlyFlightReports.AsNoTracking()
            .Include(r => r.OrgUnit)
            .Include(r => r.Lines).ThenInclude(l => l.Pilot).ThenInclude(p => p.Person)
            .Include(r => r.Lines).ThenInclude(l => l.Uas)
            .Where(r => r.IsActive);

        if (year is int y) query = query.Where(r => r.Year == y);
        if (month is int m) query = query.Where(r => r.Month == m);
        if (orgUnitId is Guid oid) query = query.Where(r => r.OrgUnitId == oid);

        var items = await query.OrderByDescending(r => r.Year).ThenByDescending(r => r.Month).ToListAsync(cancellationToken);
        return items.Where(r => policy.CanAccessOrgUnit(r.OrgUnit.Path)).Select(Map).ToList();
    }

    public async Task<MonthlyFlightReportDto> SaveDraftAsync(Guid id, SaveMonthlyFlightReportRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.ReportsWrite);
        var report = await LoadTrackedAsync(id, cancellationToken);
        EnsureWritable(report);
        if (report.Status is not MonthlyFlightReportStatus.Draft)
            throw new ValidationException("Upraviť možno iba koncept výkazu.");

        await ReplaceLinesAsync(report, request.Lines, cancellationToken);
        report.Note = request.Note;
        report.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        return Map(await LoadTrackedAsync(id, cancellationToken));
    }

    public async Task<MonthlyFlightReportDto> ConfirmAsync(Guid id, ConfirmMonthlyFlightReportRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.ReportsWrite);
        var report = await LoadTrackedAsync(id, cancellationToken);
        EnsureWritable(report);
        EnsureRowVersion(report, request.RowVersion);
        if (report.Status is not MonthlyFlightReportStatus.Draft)
            throw new ValidationException("Potvrdiť možno iba koncept.");
        if (report.Lines.Count == 0)
            throw new ValidationException("Výkaz nemá riadky. Použite „Bez letovej činnosti“ alebo doplňte údaje.");

        var before = Map(report);
        report.Status = MonthlyFlightReportStatus.Confirmed;
        report.ConfirmedAt = clock.UtcNow;
        report.ConfirmedByAccountId = currentUser.AccountId;
        report.UpdatedBy = currentUser.AccountId;
        AddChangeLog(report, "Confirm", before, null);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("MonthlyFlightReport.Confirm", nameof(MonthlyFlightReport), report.Id, before, Map(report), cancellationToken: cancellationToken);
        return Map(await LoadTrackedAsync(id, cancellationToken));
    }

    public async Task<MonthlyFlightReportDto> MarkNoActivityAsync(Guid id, MarkNoActivityMonthlyFlightReportRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.ReportsWrite);
        var report = await LoadTrackedAsync(id, cancellationToken);
        EnsureWritable(report);
        EnsureRowVersion(report, request.RowVersion);
        if (report.Status is not MonthlyFlightReportStatus.Draft)
            throw new ValidationException("Označiť bez činnosti možno iba koncept.");

        var before = Map(report);
        foreach (var line in report.Lines.Where(l => l.IsActive).ToList())
            line.IsActive = false;
        report.Status = MonthlyFlightReportStatus.NoActivity;
        report.Note = request.Note ?? report.Note;
        report.ConfirmedAt = clock.UtcNow;
        report.ConfirmedByAccountId = currentUser.AccountId;
        report.UpdatedBy = currentUser.AccountId;
        AddChangeLog(report, "NoActivity", before, null);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("MonthlyFlightReport.NoActivity", nameof(MonthlyFlightReport), report.Id, before, Map(report), cancellationToken: cancellationToken);
        return Map(await LoadTrackedAsync(id, cancellationToken));
    }

    public async Task<MonthlyFlightReportDto> AmendAsync(Guid id, AmendMonthlyFlightReportRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.ReportsWrite);
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ValidationException("Dôvod opravy je povinný.");

        var report = await LoadTrackedAsync(id, cancellationToken);
        EnsureWritable(report);
        EnsureRowVersion(report, request.RowVersion);
        if (report.Status is MonthlyFlightReportStatus.Draft)
            throw new ValidationException("Na koncept použite uloženie draftu, nie amend.");

        var before = Map(report);
        report.Status = MonthlyFlightReportStatus.Draft;
        report.ConfirmedAt = null;
        report.ConfirmedByAccountId = null;
        report.Note = request.Payload.Note;
        report.UpdatedBy = currentUser.AccountId;
        await ReplaceLinesAsync(report, request.Payload.Lines, cancellationToken);
        AddChangeLog(report, "Amend", before, request.Reason);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("MonthlyFlightReport.Amend", nameof(MonthlyFlightReport), report.Id, before, Map(report), cancellationToken: cancellationToken);
        return Map(await LoadTrackedAsync(id, cancellationToken));
    }

    private async Task ReplaceLinesAsync(MonthlyFlightReport report, IReadOnlyList<UpsertMonthlyFlightReportLineRequest> lines, CancellationToken cancellationToken)
    {
        foreach (var existing in report.Lines.Where(l => l.IsActive).ToList())
            existing.IsActive = false;

        foreach (var line in lines)
        {
            ValidateLine(line);
            var pilot = await db.Pilots.Include(p => p.OrgUnit)
                .FirstOrDefaultAsync(p => p.Id == line.PilotId && p.IsActive, cancellationToken)
                ?? throw new NotFoundException("Pilot nebol nájdený.");
            if (pilot.OrgUnitId != report.OrgUnitId)
                throw new ValidationException("Pilot nepatrí do útvaru výkazu.");

            var uas = await db.UasAircraft.FirstOrDefaultAsync(u => u.Id == line.UasId && u.IsActive, cancellationToken)
                ?? throw new NotFoundException("UAS nebol nájdený.");
            if (uas.OwningOrgUnitId != report.OrgUnitId)
                throw new ValidationException("UAS nepatrí do útvaru výkazu.");

            var authOk = await db.PilotUasAuthorizations.AnyAsync(a =>
                a.IsActive && a.PilotId == line.PilotId && a.UasId == line.UasId
                && a.OrgUnitId == report.OrgUnitId
                && a.ValidFrom <= clock.UtcNow
                && (a.ValidTo == null || a.ValidTo > clock.UtcNow), cancellationToken);
            if (!authOk)
                throw new ValidationException($"Pilot nemá platné oprávnenie na UAS {uas.RegistrationNumber ?? uas.SerialNumber}.");

            db.MonthlyFlightReportLines.Add(new MonthlyFlightReportLine
            {
                ReportId = report.Id,
                PilotId = line.PilotId,
                UasId = line.UasId,
                Hours = line.Hours,
                Minutes = line.Minutes,
                Takeoffs = line.Takeoffs,
                Landings = line.Landings,
                OperationalFlights = line.OperationalFlights,
                TrainingFlights = line.TrainingFlights,
                OtherFlights = line.OtherFlights,
                Note = line.Note,
                CreatedBy = currentUser.AccountId,
                UpdatedBy = currentUser.AccountId
            });
        }
    }

    private void AddChangeLog(MonthlyFlightReport report, string changeType, MonthlyFlightReportDto before, string? reason)
    {
        db.MonthlyFlightReportChangeLogs.Add(new MonthlyFlightReportChangeLog
        {
            ReportId = report.Id,
            ChangedByAccountId = currentUser.AccountId ?? Guid.Empty,
            ChangedAt = clock.UtcNow,
            ChangeType = changeType,
            PreviousJson = JsonSerializer.Serialize(before, JsonOpts),
            NewJson = reason,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        });
    }

    private async Task<MonthlyFlightReport> LoadTrackedAsync(Guid id, CancellationToken cancellationToken)
    {
        return await db.MonthlyFlightReports
            .Include(r => r.OrgUnit)
            .Include(r => r.Lines.Where(l => l.IsActive)).ThenInclude(l => l.Pilot).ThenInclude(p => p.Person)
            .Include(r => r.Lines.Where(l => l.IsActive)).ThenInclude(l => l.Uas)
            .FirstOrDefaultAsync(r => r.Id == id && r.IsActive, cancellationToken)
            ?? throw new NotFoundException("Mesačný výkaz nebol nájdený.");
    }

    private async Task<MonthlyFlightReport?> LoadByOrgMonthAsync(Guid orgUnitId, int year, int month, CancellationToken cancellationToken)
    {
        return await db.MonthlyFlightReports
            .Include(r => r.OrgUnit)
            .Include(r => r.Lines.Where(l => l.IsActive)).ThenInclude(l => l.Pilot).ThenInclude(p => p.Person)
            .Include(r => r.Lines.Where(l => l.IsActive)).ThenInclude(l => l.Uas)
            .FirstOrDefaultAsync(r => r.OrgUnitId == orgUnitId && r.Year == year && r.Month == month && r.IsActive, cancellationToken);
    }

    private async Task<Domain.Identity.OrgUnit> ResolveWritableOrgAsync(Guid? orgUnitId, CancellationToken cancellationToken)
    {
        var org = await ResolveOrgAsync(orgUnitId, cancellationToken);
        policy.EnsureOrgAccess(org.Path);
        if (!IsCentralScope())
        {
            var homeId = currentUser.Roles.Select(r => r.OrgUnitId).FirstOrDefault();
            if (homeId != Guid.Empty && org.Id != homeId && !org.Path.StartsWith(
                    currentUser.Roles.Select(r => r.OrgUnitPath).FirstOrDefault() ?? "\0", StringComparison.Ordinal))
            {
                // Unit users may only write their own unit (exact), not descendants unless path allows.
                if (currentUser.Roles.All(r => r.RoleCode != nameof(SystemRole.CentralGestor))
                    && currentUser.Roles.All(r => org.Id != r.OrgUnitId))
                    throw new ForbiddenException("Výkaz možno editovať len pre vlastný útvar.");
            }
        }
        return org;
    }

    private async Task<Domain.Identity.OrgUnit> ResolveReadableOrgAsync(Guid? orgUnitId, CancellationToken cancellationToken)
    {
        var org = await ResolveOrgAsync(orgUnitId, cancellationToken);
        policy.EnsureOrgAccess(org.Path);
        return org;
    }

    private async Task<Domain.Identity.OrgUnit> ResolveOrgAsync(Guid? orgUnitId, CancellationToken cancellationToken)
    {
        if (orgUnitId is Guid oid)
        {
            return await db.OrgUnits.FirstOrDefaultAsync(o => o.Id == oid && o.IsActive, cancellationToken)
                ?? throw new NotFoundException("Útvar nebol nájdený.");
        }

        var role = currentUser.Roles.FirstOrDefault(r => r.GrantsBusinessDataAccess)
            ?? throw new ValidationException("Účet nemá priradený útvar.");
        return await db.OrgUnits.FirstOrDefaultAsync(o => o.Id == role.OrgUnitId && o.IsActive, cancellationToken)
            ?? throw new NotFoundException("Útvar nebol nájdený.");
    }

    private void EnsureWritable(MonthlyFlightReport report) => policy.EnsureOrgAccess(report.OrgUnit.Path);

    private bool IsCentralScope() =>
        currentUser.Roles.Any(r => r.RoleCode is nameof(SystemRole.CentralGestor) or nameof(SystemRole.ControllerAuditor));

    private static void EnsureRowVersion(MonthlyFlightReport report, uint rowVersion)
    {
        if (report.RowVersion != rowVersion)
            throw new ConflictException("Záznam bol medzitým zmenený iným používateľom.");
    }

    private static void ValidateYearMonth(int year, int month)
    {
        if (year < 2000 || year > 2100 || month is < 1 or > 12)
            throw new ValidationException("Neplatný rok alebo mesiac.");
    }

    private static void ValidateLine(UpsertMonthlyFlightReportLineRequest line)
    {
        if (line.Hours < 0 || line.Minutes is < 0 or > 59)
            throw new ValidationException("Neplatný čas (hodiny/minúty).");
        if (line.Takeoffs < 0 || line.Landings < 0 || line.OperationalFlights < 0 || line.TrainingFlights < 0 || line.OtherFlights < 0)
            throw new ValidationException("Počty letov nemôžu byť záporné.");
    }

    private static MonthlyFlightReportDto Map(MonthlyFlightReport r) =>
        new(
            r.Id,
            r.OrgUnitId,
            r.OrgUnit.Name,
            r.Year,
            r.Month,
            r.Status,
            r.ConfirmedAt,
            r.ConfirmedByAccountId,
            r.Note,
            r.Lines.Where(l => l.IsActive).Select(l => new MonthlyFlightReportLineDto(
                l.Id,
                l.PilotId,
                $"{l.Pilot.Person.FirstName} {l.Pilot.Person.LastName}".Trim(),
                l.UasId,
                l.Uas.RegistrationNumber ?? l.Uas.SerialNumber,
                l.Hours,
                l.Minutes,
                l.Takeoffs,
                l.Landings,
                l.OperationalFlights,
                l.TrainingFlights,
                l.OtherFlights,
                l.Note)).ToList(),
            r.RowVersion);
}
