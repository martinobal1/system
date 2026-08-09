using IsSuas.Application.Abstractions;
using IsSuas.Application.OrgUnits;
using IsSuas.Application.Policies;
using IsSuas.Domain.Identity;
using IsSuas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IsSuas.Infrastructure.Services;

public interface IOrgUnitService
{
    Task<IReadOnlyList<OrgUnitDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<OrgUnitDto> CreateAsync(CreateOrgUnitRequest request, CancellationToken cancellationToken = default);
    Task<OrgUnitDto> UpdateAsync(Guid id, UpdateOrgUnitRequest request, CancellationToken cancellationToken = default);
    Task<OrgUnitDto> MoveAsync(Guid id, MoveOrgUnitRequest request, CancellationToken cancellationToken = default);
}

public sealed class OrgUnitService(
    IsSuasDbContext db,
    IAuthorizationPolicyService policy,
    ICurrentUser currentUser,
    IAuditWriter audit,
    IClock clock) : IOrgUnitService
{
    public async Task<IReadOnlyList<OrgUnitDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.OrgUnitsRead);

        var units = await db.OrgUnits.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Path)
            .ToListAsync(cancellationToken);

        return units
            .Where(u => policy.CanAccessOrgUnit(u.Path))
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
        if (request.ParentId is null)
        {
            path = "/";
            level = 0;
        }
        else
        {
            var parent = await db.OrgUnits.FirstOrDefaultAsync(x => x.Id == request.ParentId && x.IsActive, cancellationToken)
                ?? throw new NotFoundException("Nadradený útvar nebol nájdený.");
            policy.EnsureOrgAccess(parent.Path);
            path = parent.Path;
            level = parent.Level + 1;
        }

        var entity = new OrgUnit
        {
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
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

    private static void EnsureRowVersion(OrgUnit entity, uint rowVersion)
    {
        if (entity.RowVersion != rowVersion)
            throw new ConflictException("Záznam bol medzitým zmenený iným používateľom.");
    }

    private static OrgUnitDto Map(OrgUnit x) =>
        new(x.Id, x.Code, x.Name, x.ParentId, x.Path, x.Level, x.IsActive);
}
