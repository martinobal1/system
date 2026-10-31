using IsSuas.Application.Abstractions;
using IsSuas.Application.Policies;
using IsSuas.Application.Trash;
using IsSuas.Domain.Common;
using IsSuas.Domain.Enums;
using IsSuas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IsSuas.Infrastructure.Services;

/// <summary>
/// Kôš: presun do koša (všetci s oprávnením na zápis), obnovenie a trvalé vymazanie (iba gestor).
/// Pravidlo „možnosť A“: záznam, na ktorý sa viažu iné aktívne záznamy, nejde vymazať samostatne —
/// GetDependents vráti zoznam väzieb a dávka (MoveBatch/PurgeBatch) zmaže vybrané väzby aj záznam naraz.
/// </summary>
public interface ITrashService
{
    Task<TrashActionResultDto> MoveAsync(MoveToTrashRequest request, CancellationToken cancellationToken = default);
    Task<TrashActionResultDto> MoveBatchAsync(MoveToTrashBatchRequest request, CancellationToken cancellationToken = default);
    Task<TrashDependentsDto> GetDependentsForMoveAsync(string category, Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrashCategorySummaryDto>> SummaryAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrashEntryDto>> ListAsync(string? category, CancellationToken cancellationToken = default);
    Task<TrashActionResultDto> RestoreAsync(Guid entryId, CancellationToken cancellationToken = default);
    Task<TrashActionResultDto> RestoreBatchAsync(RestoreTrashBatchRequest request, CancellationToken cancellationToken = default);
    Task<TrashDependentsDto> GetDependentsForPurgeAsync(Guid entryId, CancellationToken cancellationToken = default);
    Task<TrashActionResultDto> PurgeAsync(Guid entryId, PurgeTrashRequest request, CancellationToken cancellationToken = default);
    Task<TrashActionResultDto> PurgeBatchAsync(PurgeTrashBatchRequest request, CancellationToken cancellationToken = default);
}

public sealed class TrashService(
    IsSuasDbContext db,
    IAuthorizationPolicyService policy,
    ICurrentUser currentUser,
    IAuditWriter audit,
    IClock clock) : ITrashService
{
    public const string PurgeConfirmText = "VYMAZAŤ";
    private const int MaxBatchSize = 1000;
    private const int MaxDependentDepth = 4;

    /// <summary>Čo sa maže: kategória, typ, id, názov, útvar, kto to vytvoril, potrebné oprávnenie.</summary>
    private sealed record TrashTarget(
        string Category,
        string EntityType,
        Guid Id,
        string Title,
        string? OrgUnitPath,
        Guid? OwnerAccountId,
        string WritePermission,
        AccessLevel? AccountLevel = null);

    /// <summary>Väzba brániaca vymazaniu. Category == null = väzba, ktorá sa nemaže (len počet).</summary>
    private sealed record Blocker(string Label, string? Category, IReadOnlyList<Guid> Ids, int Count);

    // ------------------------------------------------------------------ presun do koša

    public Task<TrashActionResultDto> MoveAsync(MoveToTrashRequest request, CancellationToken cancellationToken = default) =>
        MoveBatchAsync(new MoveToTrashBatchRequest([new TrashItemRef(request.Category, request.Id)], request.Reason), cancellationToken);

    public async Task<TrashActionResultDto> MoveBatchAsync(MoveToTrashBatchRequest request, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        var refs = NormalizeRefs(request.Items);

        var targets = new List<TrashTarget>();
        foreach (var r in refs)
        {
            var target = await LoadTargetAsync(r.Category, r.Id, cancellationToken)
                ?? throw new NotFoundException("Niektorý zo záznamov už neexistuje alebo je v koši. Obnovte stránku.");
            EnsureCanMove(target);
            targets.Add(target);
        }

        await EnsureNoBlockersAsync(targets, forPurge: false, cancellationToken);

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : Truncate(request.Reason.Trim(), 1000);
        var now = clock.UtcNow;
        var entries = targets.Select(t => new TrashEntry
        {
            Category = t.Category,
            EntityType = t.EntityType,
            EntityId = t.Id,
            Title = Truncate(t.Title, 300),
            OrgUnitPath = t.OrgUnitPath,
            Reason = reason,
            DeletedAt = now,
            DeletedByAccountId = currentUser.AccountId,
            DeletedByUserName = currentUser.UserName ?? "neznámy"
        }).ToList();
        db.TrashEntries.AddRange(entries);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var entry in entries)
            await audit.WriteAsync("Trash.Move", entry.EntityType, entry.EntityId, null,
                new { entry.Category, entry.Title, reason, batch = entries.Count > 1 }, cancellationToken: cancellationToken);

        return new TrashActionResultDto(entries.Count == 1
            ? $"„{entries[0].Title}“ bol presunutý do koša."
            : $"Do koša sa presunulo {entries.Count} záznamov.");
    }

    public async Task<TrashDependentsDto> GetDependentsForMoveAsync(string category, Guid id, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        var key = NormalizeCategory(category);
        var target = await LoadTargetAsync(key, id, cancellationToken)
            ?? throw new NotFoundException("Záznam nebol nájdený alebo už je v koši.");
        return await BuildDependentsAsync(target, forPurge: false, cancellationToken);
    }

    private void EnsureCanMove(TrashTarget target)
    {
        if (target.Category == TrashCategories.Accounts && target.Id == currentUser.AccountId)
            throw new ForbiddenException("Vlastný účet nemôžete vymazať.");

        if (IsGestor)
            return;

        if (target.Category == TrashCategories.Uas)
            throw new ForbiddenException("UAS môže vymazať iba gestor (úroveň 1). Správca útvaru ho môže vyradiť alebo vrátiť na MV SR.");

        policy.EnsurePermission(target.WritePermission);

        if (target.Category == TrashCategories.Accounts && target.AccountLevel == AccessLevel.Level1)
            throw new ForbiddenException("Účet na úrovni gestora môže vymazať iba gestor.");

        // Prevádzkovateľ (správca útvaru): všetko vo svojom útvare.
        if (IsUnitAdministrator && target.OrgUnitPath is not null)
        {
            policy.EnsureOrgAccess(target.OrgUnitPath);
            return;
        }

        // Ostatní (napr. pilot): iba záznamy, ktoré sami vytvorili.
        if (target.OwnerAccountId is null || target.OwnerAccountId != currentUser.AccountId)
            throw new ForbiddenException($"„{target.Title}“: vymazať môžete iba záznamy, ktoré ste sami vytvorili.");
    }

    // ------------------------------------------------------------------ kôš (iba gestor)

    public async Task<IReadOnlyList<TrashCategorySummaryDto>> SummaryAsync(CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.TrashManage);
        var counts = await OpenEntries()
            .GroupBy(t => t.Category)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        return TrashCategories.All
            .Select(c => new TrashCategorySummaryDto(c.Key, c.NameSk, counts.FirstOrDefault(x => x.Category == c.Key)?.Count ?? 0))
            .ToList();
    }

    public async Task<IReadOnlyList<TrashEntryDto>> ListAsync(string? category, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.TrashManage);
        var query = OpenEntries();
        if (!string.IsNullOrWhiteSpace(category))
        {
            var key = NormalizeCategory(category);
            query = query.Where(t => t.Category == key);
        }

        var items = await query.ToListAsync(cancellationToken);
        return items
            .OrderByDescending(t => t.DeletedAt)
            .Select(t => new TrashEntryDto(t.Id, t.Category, TrashCategories.NameOf(t.Category), t.EntityId,
                t.Title, t.Reason, t.DeletedAt, t.DeletedByUserName))
            .ToList();
    }

    public Task<TrashActionResultDto> RestoreAsync(Guid entryId, CancellationToken cancellationToken = default) =>
        RestoreBatchAsync(new RestoreTrashBatchRequest([entryId]), cancellationToken);

    public async Task<TrashActionResultDto> RestoreBatchAsync(RestoreTrashBatchRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.TrashManage);
        var ids = (request.EntryIds ?? []).Distinct().ToList();
        if (ids.Count == 0)
            throw new ValidationException("Nie je vybraný žiadny záznam.");

        var entries = await OpenEntries().Where(t => ids.Contains(t.Id)).ToListAsync(cancellationToken);
        if (entries.Count != ids.Count)
            throw new NotFoundException("Niektorý zo záznamov v koši už nie je. Obnovte stránku.");

        // Osvedčenie sa nedá obnoviť, kým je jeho pilot v koši (bez pilota by nebolo vidno) — ibaže sa obnovuje aj pilot.
        var restoredPilots = entries.Where(e => e.Category == TrashCategories.Pilots).Select(e => e.EntityId).ToHashSet();
        foreach (var entry in entries.Where(e => e.Category == TrashCategories.Certificates))
        {
            var pilotId = await db.PilotCertificates.IgnoreQueryFilters()
                .Where(c => c.Id == entry.EntityId).Select(c => (Guid?)c.PilotId)
                .FirstOrDefaultAsync(cancellationToken);
            if (pilotId is Guid pid && !restoredPilots.Contains(pid)
                && await IsInTrashAsync(nameof(Domain.Pilots.Pilot), pid, cancellationToken))
                throw new ValidationException($"„{entry.Title}“: najprv obnovte pilota, ku ktorému osvedčenie patrí.");
        }

        var now = clock.UtcNow;
        foreach (var entry in entries)
        {
            entry.RestoredAt = now;
            entry.RestoredByUserName = currentUser.UserName ?? "neznámy";
        }
        await db.SaveChangesAsync(cancellationToken);

        foreach (var entry in entries)
            await audit.WriteAsync("Trash.Restore", entry.EntityType, entry.EntityId, null,
                new { entry.Category, entry.Title }, cancellationToken: cancellationToken);

        return new TrashActionResultDto(entries.Count == 1
            ? $"„{entries[0].Title}“ bol obnovený."
            : $"Obnovilo sa {entries.Count} záznamov.");
    }

    public async Task<TrashDependentsDto> GetDependentsForPurgeAsync(Guid entryId, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.TrashManage);
        var entry = await OpenEntries().AsNoTracking().FirstOrDefaultAsync(t => t.Id == entryId, cancellationToken)
            ?? throw new NotFoundException("Záznam v koši nebol nájdený.");

        db.IgnoreTrashFilter = true;
        try
        {
            var target = await LoadTargetAsync(entry.Category, entry.EntityId, cancellationToken);
            return target is null
                ? new TrashDependentsDto(entry.Category, entry.EntityId, entry.Title, [], [])
                : await BuildDependentsAsync(target, forPurge: true, cancellationToken);
        }
        finally
        {
            db.IgnoreTrashFilter = false;
        }
    }

    public Task<TrashActionResultDto> PurgeAsync(Guid entryId, PurgeTrashRequest request, CancellationToken cancellationToken = default) =>
        PurgeBatchAsync(new PurgeTrashBatchRequest([entryId], null, request.Confirm), cancellationToken);

    public async Task<TrashActionResultDto> PurgeBatchAsync(PurgeTrashBatchRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.TrashManage);
        if (!string.Equals(request.Confirm?.Trim(), PurgeConfirmText, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException($"Na potvrdenie napíšte {PurgeConfirmText}.");

        var entryIds = (request.EntryIds ?? []).Distinct().ToList();
        var entries = entryIds.Count == 0
            ? new List<TrashEntry>()
            : await OpenEntries().Where(t => entryIds.Contains(t.Id)).ToListAsync(cancellationToken);
        if (entries.Count != entryIds.Count)
            throw new NotFoundException("Niektorý zo záznamov v koši už nie je. Obnovte stránku.");

        var refs = (request.Items ?? [])
            .Concat(entries.Select(e => new TrashItemRef(e.Category, e.EntityId)))
            .ToList();
        if (refs.Count == 0)
            throw new ValidationException("Nie je vybraný žiadny záznam.");
        var normalized = NormalizeRefs(refs);

        db.IgnoreTrashFilter = true;
        var purged = new List<(string Category, string EntityType, Guid Id, string Title)>();
        try
        {
            var targets = new List<TrashTarget>();
            foreach (var r in normalized)
            {
                var target = await LoadTargetAsync(r.Category, r.Id, cancellationToken);
                if (target is not null)
                    targets.Add(target);
            }

            await EnsureNoBlockersAsync(targets, forPurge: true, cancellationToken);

            var now = clock.UtcNow;
            var userName = currentUser.UserName ?? "neznámy";
            using (db.AllowPhysicalDelete())
            {
                foreach (var target in targets)
                    await RemoveEntityAsync(target, cancellationToken);

                var targetIds = targets.Select(t => t.Id).ToList();
                var openForTargets = await OpenEntries().Where(t => targetIds.Contains(t.EntityId)).ToListAsync(cancellationToken);
                foreach (var target in targets)
                {
                    var open = openForTargets.Where(t => t.EntityId == target.Id && t.EntityType == target.EntityType).ToList();
                    if (open.Count == 0)
                    {
                        // Väzba, ktorá nebola v koši — zápis do koša kvôli histórii (kto a kedy ju vymazal).
                        db.TrashEntries.Add(new TrashEntry
                        {
                            Category = target.Category,
                            EntityType = target.EntityType,
                            EntityId = target.Id,
                            Title = Truncate(target.Title, 300),
                            OrgUnitPath = target.OrgUnitPath,
                            Reason = "Natrvalo vymazané spolu s nadradeným záznamom",
                            DeletedAt = now,
                            DeletedByAccountId = currentUser.AccountId,
                            DeletedByUserName = userName,
                            PurgedAt = now,
                            PurgedByUserName = userName
                        });
                    }
                    foreach (var entry in open)
                        MarkPurged(entry);
                    purged.Add((target.Category, target.EntityType, target.Id, target.Title));
                }

                // Záznamy v koši, ktorých entita už neexistuje (zanikla s rodičom) — len uzavrieme.
                foreach (var entry in entries.Where(e => targets.All(t => t.Id != e.EntityId)))
                {
                    MarkPurged(entry);
                    purged.Add((entry.Category, entry.EntityType, entry.EntityId, entry.Title));
                }

                await db.SaveChangesAsync(cancellationToken);
            }
        }
        finally
        {
            db.IgnoreTrashFilter = false;
        }

        foreach (var p in purged)
            await audit.WriteAsync("Trash.Purge", p.EntityType, p.Id, null,
                new { p.Category, p.Title, batch = purged.Count > 1 }, cancellationToken: cancellationToken);

        return new TrashActionResultDto(purged.Count == 1
            ? $"„{purged[0].Title}“ bol natrvalo vymazaný."
            : $"Natrvalo sa vymazalo {purged.Count} záznamov.");
    }

    private void MarkPurged(TrashEntry entry)
    {
        entry.PurgedAt = clock.UtcNow;
        entry.PurgedByUserName = currentUser.UserName ?? "neznámy";
    }

    // ------------------------------------------------------------------ načítanie cieľa

    private async Task<TrashTarget?> LoadTargetAsync(string category, Guid id, CancellationToken ct)
    {
        switch (category)
        {
            case TrashCategories.Pilots:
            {
                var p = await db.Pilots.Include(x => x.Person).Include(x => x.OrgUnit)
                    .FirstOrDefaultAsync(x => x.Id == id, ct);
                return p is null ? null : new TrashTarget(category, nameof(Domain.Pilots.Pilot), p.Id,
                    $"{p.Person.FirstName} {p.Person.LastName}".Trim(), p.OrgUnit.Path, p.CreatedBy, Permissions.PilotsWrite);
            }
            case TrashCategories.Certificates:
            {
                var c = await db.PilotCertificates.Include(x => x.Pilot).ThenInclude(x => x.Person)
                    .Include(x => x.Pilot).ThenInclude(x => x.OrgUnit)
                    .FirstOrDefaultAsync(x => x.Id == id, ct);
                return c is null ? null : new TrashTarget(category, nameof(Domain.Pilots.PilotCertificate), c.Id,
                    $"{c.TitleSk} – {c.Pilot.Person.FirstName} {c.Pilot.Person.LastName}", c.Pilot.OrgUnit.Path, c.CreatedBy,
                    Permissions.PilotsWrite);
            }
            case TrashCategories.Flights:
            {
                var f = await db.Flights.Include(x => x.OrgUnit).Include(x => x.Pilot).ThenInclude(x => x!.Person)
                    .FirstOrDefaultAsync(x => x.Id == id, ct);
                if (f is null) return null;
                var when = f.TakeoffAt ?? f.CreatedAt;
                var who = f.Pilot is null ? "" : $" – {f.Pilot.Person.FirstName} {f.Pilot.Person.LastName}";
                var title = $"{(string.IsNullOrWhiteSpace(f.FlightNumber) ? "Let" : f.FlightNumber)} ({when:d.M.yyyy}){who}";
                return new TrashTarget(category, nameof(Domain.Flights.Flight), f.Id, title, f.OrgUnit.Path, f.CreatedBy, Permissions.FlightsWrite);
            }
            case TrashCategories.Operations:
            {
                var o = await db.Operations.Include(x => x.OrgUnit).FirstOrDefaultAsync(x => x.Id == id, ct);
                return o is null ? null : new TrashTarget(category, nameof(Domain.Operations.Operation), o.Id,
                    $"{o.Title} ({o.PlannedStart:d.M.yyyy})", o.OrgUnit.Path, o.ProposedByAccountId, Permissions.OperationsWrite);
            }
            case TrashCategories.Uas:
            {
                var u = await db.UasAircraft.Include(x => x.OwningOrgUnit).FirstOrDefaultAsync(x => x.Id == id, ct);
                return u is null ? null : new TrashTarget(category, nameof(Domain.Fleet.Uas), u.Id,
                    $"{u.RegistrationNumber ?? u.SerialNumber} ({u.Manufacturer} {u.Model})", u.OwningOrgUnit?.Path,
                    u.SubmittedByAccountId ?? u.CreatedBy, Permissions.UasWrite);
            }
            case TrashCategories.Defects:
            {
                var d = await db.Defects.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (d is null) return null;
                var uasId = d.RelatedUasId ?? (d.TargetType == MaintenanceTargetType.Uas ? (Guid?)d.TargetId : null);
                string? path = null;
                if (uasId is Guid uid)
                    path = await db.UasAircraft.IgnoreQueryFilters().Where(u => u.Id == uid)
                        .Select(u => u.OwningOrgUnit != null ? u.OwningOrgUnit.Path : null)
                        .FirstOrDefaultAsync(ct);
                return new TrashTarget(category, nameof(Domain.Maintenance.Defect), d.Id, d.Title, path,
                    d.ReportedByAccountId, Permissions.MaintenanceWrite);
            }
            case TrashCategories.Incidents:
            {
                var e = await db.SafetyEvents.Include(x => x.OrgUnit).FirstOrDefaultAsync(x => x.Id == id, ct);
                return e is null ? null : new TrashTarget(category, nameof(Domain.Safety.SafetyEvent), e.Id,
                    $"{e.Title} ({e.OccurredAt:d.M.yyyy})", e.OrgUnit?.Path, e.ReportedByAccountId, Permissions.SafetyWrite);
            }
            case TrashCategories.Accounts:
            {
                var a = await db.Accounts.Include(x => x.Person).ThenInclude(x => x.HomeOrgUnit)
                    .FirstOrDefaultAsync(x => x.Id == id, ct);
                return a is null ? null : new TrashTarget(category, nameof(Domain.Identity.Account), a.Id,
                    $"{a.UserName} ({a.Person.FirstName} {a.Person.LastName})", a.Person.HomeOrgUnit?.Path, a.CreatedBy,
                    Permissions.AccountsWrite, a.AccessLevel);
            }
            default:
                throw new ValidationException("Neznáma kategória koša.");
        }
    }

    // ------------------------------------------------------------------ väzby (možnosť A)

    /// <summary>Ak sa na niektorý záznam ešte viaže niečo mimo dávky, vyhodí chybu so zoznamom väzieb.</summary>
    private async Task EnsureNoBlockersAsync(IReadOnlyList<TrashTarget> targets, bool forPurge, CancellationToken ct)
    {
        var excluded = targets.Select(t => t.Id).ToHashSet();
        var problems = new List<(string Title, string Blockers)>();
        foreach (var target in targets)
        {
            var blockers = await FindBlockersAsync(target, forPurge, excluded, ct);
            if (blockers.Count > 0)
                problems.Add((target.Title, string.Join("; ", blockers.Select(b => $"{b.Label}: {b.Count}"))));
        }

        if (problems.Count == 0)
            return;

        var verb = forPurge ? "natrvalo vymazať" : "vymazať";
        if (targets.Count == 1)
            throw new ValidationException(
                $"„{problems[0].Title}“ nemožno {verb}, kým sa naň viažu iné záznamy{(forPurge ? " (aj tie v koši)" : "")}. " +
                $"Najprv vymažte: {problems[0].Blockers}.");

        throw new ValidationException(
            $"Niektoré záznamy nemožno {verb}, kým sa na ne viažu iné: " +
            string.Join(" | ", problems.Select(p => $"„{p.Title}“ – {p.Blockers}")) + ".");
    }

    /// <summary>
    /// forPurge=false: blokujú len aktívne záznamy mimo koša.
    /// forPurge=true: blokujú aj záznamy v koši a tabuľky, ktoré databáza nedovolí osirieť.
    /// excluded = id záznamov, ktoré sa mažú v tej istej dávke (tie neblokujú).
    /// </summary>
    private async Task<List<Blocker>> FindBlockersAsync(TrashTarget target, bool forPurge, IReadOnlySet<Guid> excluded, CancellationToken ct)
    {
        var list = new List<Blocker>();
        var id = target.Id;

        // Väzba na inú kategóriu koša — dá sa zmazať spolu (zaškrtávací zoznam).
        async Task Deletable<T>(IQueryable<T> query, string label, string category) where T : AuditableEntity
        {
            if (forPurge) query = query.IgnoreQueryFilters();
            var ids = (await query.Select(x => x.Id).ToListAsync(ct)).Where(x => !excluded.Contains(x)).Distinct().ToList();
            if (ids.Count > 0) list.Add(new Blocker(label, category, ids, ids.Count));
        }

        // Väzba, ktorá sa nemaže (napr. riadky mesačných hlásení) — len počet.
        async Task Fixed<T>(IQueryable<T> query, string label) where T : AuditableEntity
        {
            var count = await query.IgnoreQueryFilters().CountAsync(ct);
            if (count > 0) list.Add(new Blocker(label, null, [], count));
        }

        switch (target.Category)
        {
            case TrashCategories.Pilots:
                await Deletable(db.Flights.Where(f => f.PilotId == id && f.IsActive), "Denník letov (lety pilota)", TrashCategories.Flights);
                if (forPurge)
                    await Fixed(db.MonthlyFlightReportLines.Where(l => l.PilotId == id), "Mesačné hlásenia (riadky s pilotom) — tie sa nemažú");
                break;

            case TrashCategories.Uas:
                await Deletable(db.Flights.Where(f => f.UasId == id && f.IsActive), "Denník letov (lety s UAS)", TrashCategories.Flights);
                await Deletable(db.Defects.Where(d => d.IsActive
                    && (d.RelatedUasId == id || (d.TargetType == MaintenanceTargetType.Uas && d.TargetId == id))), "Poruchy", TrashCategories.Defects);
                await Deletable(db.SafetyEvents.Where(e => e.RelatedUasId == id && e.IsActive), "Incidenty", TrashCategories.Incidents);
                if (forPurge)
                {
                    // Incident, kde je UAS dotknutým prostriedkom — zmaže sa celý incident.
                    var eventIds = (await db.SafetyEventAssets.Where(a => a.UasId == id).Select(a => a.SafetyEventId).ToListAsync(ct))
                        .Where(x => !excluded.Contains(x)).Distinct().ToList();
                    if (eventIds.Count > 0)
                        list.Add(new Blocker("Incidenty (UAS ako dotknutý prostriedok)", TrashCategories.Incidents, eventIds, eventIds.Count));
                    await Fixed(db.MonthlyFlightReportLines.Where(l => l.UasId == id), "Mesačné hlásenia (riadky s UAS) — tie sa nemažú");
                    await Fixed(db.PersonFunctionAssignments.Where(a => a.UasId == id), "Priradenia funkcií k UAS");
                }
                break;

            case TrashCategories.Operations:
                await Deletable(db.Flights.Where(f => f.OperationId == id && f.IsActive), "Denník letov (lety k žiadosti)", TrashCategories.Flights);
                await Deletable(db.SafetyEvents.Where(e => e.RelatedOperationId == id && e.IsActive), "Incidenty", TrashCategories.Incidents);
                break;

            case TrashCategories.Flights:
                await Deletable(db.Defects.Where(d => d.RelatedFlightId == id && d.IsActive), "Poruchy", TrashCategories.Defects);
                await Deletable(db.SafetyEvents.Where(e => e.RelatedFlightId == id && e.IsActive), "Incidenty", TrashCategories.Incidents);
                break;

            case TrashCategories.Accounts:
                if (forPurge)
                {
                    await Fixed(db.GeoZoneImports.Where(i => i.ImportedByAccountId == id), "Importy GEO zón — tie sa nemažú");
                    await Fixed(db.DelegatedPermissions.Where(d => d.FromAccountId == id || d.ToAccountId == id), "Delegácie oprávnení");
                }
                break;
        }

        return list;
    }

    /// <summary>Strom väzieb (aj väzby väzieb) pre zaškrtávací zoznam v UI.</summary>
    private async Task<TrashDependentsDto> BuildDependentsAsync(TrashTarget root, bool forPurge, CancellationToken ct)
    {
        var none = new HashSet<Guid>();
        var visited = new HashSet<(string, Guid)> { (root.Category, root.Id) };
        var items = new List<TrashDependentDto>();
        var notDeletable = new List<string>();
        var queue = new Queue<(TrashTarget Target, int Depth)>();
        queue.Enqueue((root, 0));

        while (queue.Count > 0 && items.Count < MaxBatchSize)
        {
            var (target, depth) = queue.Dequeue();
            foreach (var blocker in await FindBlockersAsync(target, forPurge, none, ct))
            {
                if (blocker.Category is null)
                {
                    var text = target.Id == root.Id ? $"{blocker.Label}: {blocker.Count}" : $"{target.Title} – {blocker.Label}: {blocker.Count}";
                    if (!notDeletable.Contains(text)) notDeletable.Add(text);
                    continue;
                }

                foreach (var childId in blocker.Ids)
                {
                    if (!visited.Add((blocker.Category, childId)))
                        continue;
                    var child = await LoadTargetAsync(blocker.Category, childId, ct);
                    if (child is null)
                        continue;
                    var inTrash = forPurge && await IsInTrashAsync(child.EntityType, child.Id, ct);
                    items.Add(new TrashDependentDto(child.Category, TrashCategories.NameOf(child.Category), child.Id,
                        child.Title, inTrash, depth + 1));
                    if (depth + 1 < MaxDependentDepth)
                        queue.Enqueue((child, depth + 1));
                }
            }
        }

        return new TrashDependentsDto(root.Category, root.Id, root.Title, items, notDeletable);
    }

    // ------------------------------------------------------------------ trvalé vymazanie

    private async Task RemoveEntityAsync(TrashTarget target, CancellationToken ct)
    {
        var id = target.Id;
        switch (target.Category)
        {
            case TrashCategories.Pilots:
            {
                // Oprávnenia pilota na UAS patria pilotovi — zmažú sa s ním.
                db.PilotUasAuthorizations.RemoveRange(
                    await db.PilotUasAuthorizations.Where(a => a.PilotId == id).ToListAsync(ct));
                // Osvedčenia zaniknú s pilotom (kaskáda v DB); ich prípadné záznamy v koši uzavrieme.
                var certIds = await db.PilotCertificates.IgnoreQueryFilters()
                    .Where(c => c.PilotId == id).Select(c => c.Id).ToListAsync(ct);
                foreach (var open in await OpenEntries()
                             .Where(t => t.EntityType == nameof(Domain.Pilots.PilotCertificate) && certIds.Contains(t.EntityId))
                             .ToListAsync(ct))
                    MarkPurged(open);
                db.Remove(await db.Pilots.IgnoreQueryFilters().FirstAsync(x => x.Id == id, ct));
                break;
            }
            case TrashCategories.Certificates:
                db.Remove(await db.PilotCertificates.IgnoreQueryFilters().FirstAsync(x => x.Id == id, ct));
                break;
            case TrashCategories.Flights:
                db.Remove(await db.Flights.IgnoreQueryFilters().FirstAsync(x => x.Id == id, ct));
                break;
            case TrashCategories.Operations:
                db.Remove(await db.Operations.IgnoreQueryFilters().FirstAsync(x => x.Id == id, ct));
                break;
            case TrashCategories.Uas:
            {
                db.PilotUasAuthorizations.RemoveRange(
                    await db.PilotUasAuthorizations.Where(a => a.UasId == id).ToListAsync(ct));
                foreach (var assignment in await db.BatteryAssignments.Where(b => b.UasId == id).ToListAsync(ct))
                    assignment.UasId = null;
                db.Remove(await db.UasAircraft.IgnoreQueryFilters().FirstAsync(x => x.Id == id, ct));
                break;
            }
            case TrashCategories.Defects:
                db.Remove(await db.Defects.IgnoreQueryFilters().FirstAsync(x => x.Id == id, ct));
                break;
            case TrashCategories.Incidents:
                db.Remove(await db.SafetyEvents.IgnoreQueryFilters().FirstAsync(x => x.Id == id, ct));
                break;
            case TrashCategories.Accounts:
                db.Remove(await db.Accounts.IgnoreQueryFilters().FirstAsync(x => x.Id == id, ct));
                break;
            default:
                throw new ValidationException("Neznáma kategória koša.");
        }
    }

    // ------------------------------------------------------------------ pomocné

    private IQueryable<TrashEntry> OpenEntries() =>
        db.TrashEntries.Where(t => t.RestoredAt == null && t.PurgedAt == null);

    private Task<bool> IsInTrashAsync(string entityType, Guid id, CancellationToken ct) =>
        OpenEntries().AnyAsync(t => t.EntityType == entityType && t.EntityId == id, ct);

    private bool IsGestor => currentUser.Roles.Any(r => r.RoleCode == nameof(SystemRole.CentralGestor));

    private bool IsUnitAdministrator => currentUser.Roles.Any(r => r.RoleCode == nameof(SystemRole.UnitAdministrator));

    private void EnsureAuthenticated()
    {
        if (!currentUser.IsAuthenticated)
            throw new UnauthorizedAccessException("Nie ste prihlásený.");
    }

    private static List<TrashItemRef> NormalizeRefs(IEnumerable<TrashItemRef>? items)
    {
        var refs = (items ?? [])
            .Select(i => new TrashItemRef(NormalizeCategory(i.Category), i.Id))
            .Distinct()
            .ToList();
        if (refs.Count == 0)
            throw new ValidationException("Nie je vybraný žiadny záznam.");
        if (refs.Count > MaxBatchSize)
            throw new ValidationException($"Naraz môžete vymazať najviac {MaxBatchSize} záznamov.");
        return refs;
    }

    private static string NormalizeCategory(string? category)
    {
        var key = category?.Trim().ToLowerInvariant() ?? string.Empty;
        if (TrashCategories.All.All(c => c.Key != key))
            throw new ValidationException("Neznáma kategória koša.");
        return key;
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
