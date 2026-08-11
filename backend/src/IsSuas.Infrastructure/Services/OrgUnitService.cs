using IsSuas.Application.Abstractions;
using IsSuas.Application.OrgUnits;
using IsSuas.Application.Policies;
using IsSuas.Domain.Enums;
using IsSuas.Domain.Identity;
using IsSuas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IsSuas.Infrastructure.Services;

public interface IOrgUnitService
{
    Task<IReadOnlyList<OrgUnitDto>> ListAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<OrgUnitDto> CreateAsync(CreateOrgUnitRequest request, CancellationToken cancellationToken = default);
    Task<OrgUnitDto> UpdateAsync(Guid id, UpdateOrgUnitRequest request, CancellationToken cancellationToken = default);
    Task<OrgUnitDto> MoveAsync(Guid id, MoveOrgUnitRequest request, CancellationToken cancellationToken = default);
    Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default);
    Task ReactivateAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed class OrgUnitService(
    IsSuasDbContext db,
    IAuthorizationPolicyService policy,
    ICurrentUser currentUser,
    IAuditWriter audit,
    IClock clock) : IOrgUnitService
{
    public async Task<IReadOnlyList<OrgUnitDto>> ListAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.OrgUnitsRead);

        var query = db.OrgUnits.AsNoTracking().AsQueryable();
        if (!includeInactive)
            query = query.Where(x => x.IsActive);

        var units = await query.OrderBy(x => x.Path).ToListAsync(cancellationToken);
        var byId = units.ToDictionary(u => u.Id);

        // Own unit + descendants (business access), plus ancestors so the tree path is visible (MV → zložka → kraj).
        var visibleIds = new HashSet<Guid>();
        foreach (var u in units.Where(x => policy.CanAccessOrgUnit(x.Path)))
        {
            visibleIds.Add(u.Id);
            var cur = u;
            while (cur.ParentId is Guid pid && byId.TryGetValue(pid, out var parent))
            {
                visibleIds.Add(parent.Id);
                cur = parent;
            }
        }

        return units
            .Where(u => visibleIds.Contains(u.Id))
            .Select(Map)
            .ToList();
    }

    public async Task<OrgUnitDto> CreateAsync(CreateOrgUnitRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.OrgUnitsWrite);

        if (await db.OrgUnits.AnyAsync(x => x.Code == request.Code, cancellationToken))
            throw new ConflictException($"Útvar s kódom '{request.Code}' už existuje.");

        string path;
        int level;
        OrgUnit? parent = null;
        if (request.ParentId is null)
        {
            path = "/";
            level = 0;
        }
        else
        {
            parent = await db.OrgUnits.FirstOrDefaultAsync(x => x.Id == request.ParentId && x.IsActive, cancellationToken)
                ?? throw new NotFoundException("Nadradený útvar nebol nájdený.");
            policy.EnsureOrgAccess(parent.Path);
            path = parent.Path;
            level = parent.Level + 1;
        }

        var kind = request.Kind ?? InferKind(parent, request.Code);

        var entity = new OrgUnit
        {
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            Kind = kind,
            ParentId = request.ParentId,
            Level = level,
            Path = path,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };

        db.OrgUnits.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        entity.Path = $"{(request.ParentId is null ? "" : path.TrimEnd('/'))}/{entity.Id}/";
        await db.SaveChangesAsync(cancellationToken);

        await audit.WriteAsync("OrgUnit.Create", nameof(OrgUnit), entity.Id, null, Map(entity), cancellationToken: cancellationToken);
        return Map(entity);
    }

    public async Task<OrgUnitDto> UpdateAsync(Guid id, UpdateOrgUnitRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.OrgUnitsWrite);
        var entity = await db.OrgUnits.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Útvar nebol nájdený.");
        policy.EnsureOrgAccess(entity.Path);
        EnsureRowVersion(entity, request.RowVersion);

        var before = Map(entity);
        entity.Name = request.Name.Trim();
        entity.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("OrgUnit.Update", nameof(OrgUnit), entity.Id, before, Map(entity), cancellationToken: cancellationToken);
        return Map(entity);
    }

    public async Task<OrgUnitDto> MoveAsync(Guid id, MoveOrgUnitRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.OrgUnitsWrite);
        var entity = await db.OrgUnits.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Útvar nebol nájdený.");
        policy.EnsureOrgAccess(entity.Path);
        EnsureRowVersion(entity, request.RowVersion);

        if (request.NewParentId == entity.Id)
            throw new ValidationException("Útvar nemôže byť vlastným nadradeným.");

        OrgUnit? newParent = null;
        if (request.NewParentId is not null)
        {
            newParent = await db.OrgUnits.FirstOrDefaultAsync(x => x.Id == request.NewParentId && x.IsActive, cancellationToken)
                ?? throw new NotFoundException("Nový nadradený útvar nebol nájdený.");
            if (newParent.Path.Contains($"/{entity.Id}/", StringComparison.Ordinal))
                throw new ValidationException("Presun by vytvoril cyklus v hierarchii.");
            policy.EnsureOrgAccess(newParent.Path);
        }

        var before = Map(entity);
        var previousParentId = entity.ParentId;
        entity.ParentId = request.NewParentId;
        entity.Level = newParent is null ? 0 : newParent.Level + 1;
        var oldPath = entity.Path;
        entity.Path = newParent is null ? $"/{entity.Id}/" : $"{newParent.Path.TrimEnd('/')}/{entity.Id}/";
        entity.UpdatedBy = currentUser.AccountId;

        db.OrgUnitMoveHistories.Add(new OrgUnitMoveHistory
        {
            OrgUnitId = entity.Id,
            PreviousParentId = previousParentId,
            NewParentId = request.NewParentId,
            Reason = request.Reason,
            MovedAt = clock.UtcNow,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        });

        var descendants = await db.OrgUnits.Where(x => x.Path.StartsWith(oldPath) && x.Id != entity.Id).ToListAsync(cancellationToken);
        foreach (var d in descendants)
        {
            d.Path = entity.Path + d.Path[oldPath.Length..];
            d.Level = d.Path.Count(c => c == '/') - 1;
            d.UpdatedBy = currentUser.AccountId;
        }

        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("OrgUnit.Move", nameof(OrgUnit), entity.Id, before, Map(entity), cancellationToken: cancellationToken);
        return Map(entity);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.OrgUnitsWrite);
        var entity = await db.OrgUnits.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, cancellationToken)
            ?? throw new NotFoundException("Útvar nebol nájdený.");
        policy.EnsureOrgAccess(entity.Path);

        if (await db.OrgUnits.AnyAsync(x => x.ParentId == id && x.IsActive, cancellationToken))
            throw new ValidationException("Najprv odoberte (deaktivujte) podradené útvary.");

        var before = Map(entity);
        entity.IsActive = false;
        entity.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("OrgUnit.Deactivate", nameof(OrgUnit), entity.Id, before, Map(entity), cancellationToken: cancellationToken);
    }

    public async Task ReactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.OrgUnitsWrite);
        var entity = await db.OrgUnits.FirstOrDefaultAsync(x => x.Id == id && !x.IsActive, cancellationToken)
            ?? throw new NotFoundException("Neaktívny útvar nebol nájdený.");
        policy.EnsureOrgAccess(entity.Path);

        if (entity.ParentId is Guid parentId)
        {
            var parent = await db.OrgUnits.AsNoTracking().FirstOrDefaultAsync(x => x.Id == parentId, cancellationToken)
                ?? throw new ValidationException("Nadradený útvar neexistuje.");
            if (!parent.IsActive)
                throw new ValidationException("Najprv aktivujte nadradený útvar.");
        }

        var before = Map(entity);
        entity.IsActive = true;
        entity.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("OrgUnit.Reactivate", nameof(OrgUnit), entity.Id, before, Map(entity), cancellationToken: cancellationToken);
    }

    private static OrgUnitKind InferKind(OrgUnit? parent, string code)
    {
        if (parent is null)
            return OrgUnitKind.Root;
        var c = code.Trim().ToUpperInvariant();
        if (c is "ZLOZKA-PZ" or "PZ")
            return OrgUnitKind.BranchPz;
        if (c is "ZLOZKA-HAZZ" or "HAZZ")
            return OrgUnitKind.BranchHazz;
        if (c is "ZLOZKA-CO" or "CO")
            return OrgUnitKind.BranchCo;
        if (parent.Kind is OrgUnitKind.Root && (c.StartsWith("ZLOZKA-", StringComparison.Ordinal) || parent.Level == 0))
        {
            // Child of root without known code → unit unless looks like branch.
        }
        return OrgUnitKind.Unit;
    }

    private static void EnsureRowVersion(OrgUnit entity, uint rowVersion)
    {
        if (entity.RowVersion != rowVersion)
            throw new ConflictException("Záznam bol medzitým zmenený iným používateľom.");
    }

    private static OrgUnitDto Map(OrgUnit x) =>
        new(x.Id, x.Code, x.Name, x.Kind, x.ParentId, x.Path, x.Level, x.IsActive, x.RowVersion);
}
