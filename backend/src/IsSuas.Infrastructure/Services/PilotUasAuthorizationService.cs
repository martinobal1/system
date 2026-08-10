using IsSuas.Application.Abstractions;
using IsSuas.Application.Fleet;
using IsSuas.Application.Policies;
using IsSuas.Domain.Reporting;
using IsSuas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IsSuas.Infrastructure.Services;

public interface IPilotUasAuthorizationService
{
    Task<IReadOnlyList<PilotUasAuthorizationDto>> ListAsync(Guid? orgUnitId, Guid? uasId, Guid? pilotId, bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<PilotUasAuthorizationDto> CreateAsync(CreatePilotUasAuthorizationRequest request, CancellationToken cancellationToken = default);
    Task EndAsync(Guid id, EndPilotUasAuthorizationRequest request, CancellationToken cancellationToken = default);
}

public sealed class PilotUasAuthorizationService(
    IsSuasDbContext db,
    IAuthorizationPolicyService policy,
    ICurrentUser currentUser,
    IAuditWriter audit,
    IClock clock) : IPilotUasAuthorizationService
{
    public async Task<IReadOnlyList<PilotUasAuthorizationDto>> ListAsync(
        Guid? orgUnitId, Guid? uasId, Guid? pilotId, bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.PilotsRead);
        if (!policy.CanAccessBusinessData())
            throw new ForbiddenException("Systémový administrátor nemá automatický prístup k vecnému obsahu.");

        var query = db.PilotUasAuthorizations.AsNoTracking()
            .Include(x => x.Pilot).ThenInclude(p => p.Person)
            .Include(x => x.Uas)
            .Include(x => x.OrgUnit)
            .AsQueryable();

        if (activeOnly)
            query = query.Where(x => x.IsActive && (x.ValidTo == null || x.ValidTo > clock.UtcNow));
        if (orgUnitId is Guid oid)
            query = query.Where(x => x.OrgUnitId == oid);
        if (uasId is Guid uid)
            query = query.Where(x => x.UasId == uid);
        if (pilotId is Guid pid)
            query = query.Where(x => x.PilotId == pid);

        var items = await query.OrderByDescending(x => x.ValidFrom).ToListAsync(cancellationToken);
        return items.Where(x => policy.CanAccessOrgUnit(x.OrgUnit.Path)).Select(Map).ToList();
    }

    public async Task<PilotUasAuthorizationDto> CreateAsync(CreatePilotUasAuthorizationRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.PilotsWrite);
        var pilot = await db.Pilots.Include(p => p.Person).Include(p => p.OrgUnit)
            .FirstOrDefaultAsync(p => p.Id == request.PilotId && p.IsActive, cancellationToken)
            ?? throw new NotFoundException("Pilot nebol nájdený.");
        policy.EnsureOrgAccess(pilot.OrgUnit.Path);

        var uas = await db.UasAircraft.Include(u => u.OwningOrgUnit)
            .FirstOrDefaultAsync(u => u.Id == request.UasId && u.IsActive, cancellationToken)
            ?? throw new NotFoundException("UAS nebol nájdený.");
        if (uas.OwningOrgUnit is null)
            throw new ValidationException("UAS nemá pridelený útvar.");
        policy.EnsureOrgAccess(uas.OwningOrgUnit.Path);

        var orgUnitId = request.OrgUnitId ?? uas.OwningOrgUnitId ?? pilot.OrgUnitId;
        if (orgUnitId != pilot.OrgUnitId)
            throw new ValidationException("Pilot a oprávnenie musia patriť rovnakému útvaru.");
        if (uas.OwningOrgUnitId != orgUnitId)
            throw new ValidationException("UAS musí byť pridelený rovnakému útvaru ako pilot.");

        var org = await db.OrgUnits.FirstOrDefaultAsync(o => o.Id == orgUnitId && o.IsActive, cancellationToken)
            ?? throw new NotFoundException("Útvar nebol nájdený.");

        var overlapping = await db.PilotUasAuthorizations.AnyAsync(a =>
            a.IsActive && a.PilotId == request.PilotId && a.UasId == request.UasId
            && (a.ValidTo == null || a.ValidTo > request.ValidFrom)
            && a.ValidFrom < (request.ValidTo ?? DateTimeOffset.MaxValue), cancellationToken);
        if (overlapping)
            throw new ConflictException("Pilotívne oprávnenie pre tohto pilota a UAS už existuje.");

        var entity = new PilotUasAuthorization
        {
            PilotId = pilot.Id,
            UasId = uas.Id,
            OrgUnitId = org.Id,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            Note = request.Note,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.PilotUasAuthorizations.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        entity.Pilot = pilot;
        entity.Uas = uas;
        entity.OrgUnit = org;
        await audit.WriteAsync("PilotUasAuthorization.Create", nameof(PilotUasAuthorization), entity.Id, null, Map(entity), cancellationToken: cancellationToken);
        return Map(entity);
    }

    public async Task EndAsync(Guid id, EndPilotUasAuthorizationRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.PilotsWrite);
        var entity = await db.PilotUasAuthorizations
            .Include(x => x.OrgUnit)
            .Include(x => x.Pilot).ThenInclude(p => p.Person)
            .Include(x => x.Uas)
            .FirstOrDefaultAsync(x => x.Id == id && x.IsActive, cancellationToken)
            ?? throw new NotFoundException("Oprávnenie nebolo nájdené.");
        policy.EnsureOrgAccess(entity.OrgUnit.Path);
        if (entity.RowVersion != request.RowVersion)
            throw new ConflictException("Záznam bol medzitým zmenený iným používateľom.");

        var before = Map(entity);
        entity.ValidTo = request.ValidTo ?? clock.UtcNow;
        entity.IsActive = false;
        entity.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("PilotUasAuthorization.End", nameof(PilotUasAuthorization), entity.Id, before, Map(entity), cancellationToken: cancellationToken);
    }

    private static PilotUasAuthorizationDto Map(PilotUasAuthorization x) =>
        new(
            x.Id,
            x.PilotId,
            $"{x.Pilot.Person.FirstName} {x.Pilot.Person.LastName}".Trim(),
            x.UasId,
            x.Uas.RegistrationNumber ?? x.Uas.SerialNumber,
            x.OrgUnitId,
            x.OrgUnit.Name,
            x.ValidFrom,
            x.ValidTo,
            x.IsActive,
            x.Note,
            x.RowVersion);
}
