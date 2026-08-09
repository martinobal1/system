using IsSuas.Application.Abstractions;
using IsSuas.Application.Accounts;
using IsSuas.Application.Policies;
using IsSuas.Domain.Identity;
using IsSuas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IsSuas.Infrastructure.Services;

public interface IDelegationService
{
    Task<IReadOnlyList<DelegatedPermissionDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<DelegatedPermissionDto> CreateAsync(CreateDelegatedPermissionRequest request, CancellationToken cancellationToken = default);
    Task<DelegatedPermissionDto> RevokeAsync(Guid id, RevokeDelegatedPermissionRequest request, CancellationToken cancellationToken = default);
}

public sealed class DelegationService(
    IsSuasDbContext db,
    ICurrentUser currentUser,
    IAuthorizationPolicyService policy,
    IAuditWriter audit,
    IClock clock) : IDelegationService
{
    public async Task<IReadOnlyList<DelegatedPermissionDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.AccountsRead);
        if (currentUser.AccountId is null)
            throw new ForbiddenException("Chýba identita.");

        var canSeeAll = policy.HasPermission(Permissions.AccountsWrite)
            || currentUser.Roles.Any(r => r.RoleCode is "CentralGestor" or "SystemAdministrator");

        var query = db.DelegatedPermissions.AsNoTracking()
            .Include(d => d.FromAccount).ThenInclude(a => a.Person)
            .Include(d => d.ToAccount).ThenInclude(a => a.Person)
            .Where(d => d.IsActive);

        if (!canSeeAll)
            query = query.Where(d => d.FromAccountId == currentUser.AccountId || d.ToAccountId == currentUser.AccountId);

        var items = await query.OrderByDescending(d => d.ValidFrom).ToListAsync(cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task<DelegatedPermissionDto> CreateAsync(CreateDelegatedPermissionRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.RolesAssign);
        if (currentUser.AccountId is null)
            throw new ForbiddenException("Chýba identita.");
        if (string.IsNullOrWhiteSpace(request.PermissionCode))
            throw new ValidationException("Kód oprávnenia je povinný.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ValidationException("Dôvod delegovania je povinný.");
        if (!PermissionCatalog.IsKnown(request.PermissionCode.Trim()))
            throw new ValidationException($"Neznáme oprávnenie: {request.PermissionCode}");
        // Only own role-granted permissions may be delegated (not rights received via another delegation).
        if (!PermissionCatalog.IsGrantedByRoles(
                request.PermissionCode.Trim(),
                currentUser.Roles.Select(r => r.RoleCode)))
            throw new ForbiddenException("Nie je možné delegovať oprávnenie, ktoré sami nemáte.");
        if (request.ToAccountId == currentUser.AccountId.Value)
            throw new ValidationException("Nie je možné delegovať oprávnenie sebe samému.");

        var to = await db.Accounts.Include(a => a.Person)
            .FirstOrDefaultAsync(a => a.Id == request.ToAccountId && a.IsActive, cancellationToken)
            ?? throw new NotFoundException("Cieľový účet nebol nájdený.");

        if (request.OrgUnitId is Guid orgId)
        {
            var org = await db.OrgUnits.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orgId && o.IsActive, cancellationToken)
                ?? throw new NotFoundException("Útvar nebol nájdený.");
            policy.EnsureOrgAccess(org.Path);
        }

        var validFrom = request.ValidFrom == default ? clock.UtcNow : request.ValidFrom;
        if (request.ValidTo is not null && request.ValidTo <= validFrom)
            throw new ValidationException("ValidTo musí byť po ValidFrom.");

        var exists = await db.DelegatedPermissions.AnyAsync(d =>
            d.IsActive
            && d.FromAccountId == currentUser.AccountId
            && d.ToAccountId == request.ToAccountId
            && d.PermissionCode == request.PermissionCode.Trim()
            && d.ValidFrom <= clock.UtcNow
            && (d.ValidTo == null || d.ValidTo > clock.UtcNow), cancellationToken);
        if (exists)
            throw new ConflictException("Aktívna delegácia tohto oprávnenia už existuje.");

        var entity = new DelegatedPermission
        {
            FromAccountId = currentUser.AccountId.Value,
            ToAccountId = to.Id,
            PermissionCode = request.PermissionCode.Trim(),
            OrgUnitId = request.OrgUnitId,
            ValidFrom = validFrom,
            ValidTo = request.ValidTo,
            Reason = request.Reason.Trim(),
            ApprovedByAccountId = currentUser.AccountId,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.DelegatedPermissions.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        entity.FromAccount = await db.Accounts.Include(a => a.Person)
            .FirstAsync(a => a.Id == entity.FromAccountId, cancellationToken);
        entity.ToAccount = to;

        await audit.WriteAsync("DelegatedPermission.Create", nameof(DelegatedPermission), entity.Id, null, Map(entity),
            cancellationToken: cancellationToken);
        return Map(entity);
    }

    public async Task<DelegatedPermissionDto> RevokeAsync(Guid id, RevokeDelegatedPermissionRequest request, CancellationToken cancellationToken = default)
    {
        if (currentUser.AccountId is null)
            throw new ForbiddenException("Chýba identita.");

        var entity = await db.DelegatedPermissions
            .Include(d => d.FromAccount).ThenInclude(a => a.Person)
            .Include(d => d.ToAccount).ThenInclude(a => a.Person)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new NotFoundException("Delegácia nebola nájdená.");

        var canRevoke = entity.FromAccountId == currentUser.AccountId.Value
            || policy.HasPermission(Permissions.AccountsWrite);
        if (!canRevoke)
            throw new ForbiddenException("Delegáciu môže zrušiť len odosielateľ alebo správca účtov.");
        if (!entity.IsActive)
            throw new ValidationException("Delegácia je už neaktívna.");
        if (entity.RowVersion != request.RowVersion)
            throw new ConflictException("Záznam bol medzitým zmenený iným používateľom.");

        var before = Map(entity);
        entity.IsActive = false;
        entity.ValidTo = clock.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.Reason))
            entity.Reason = $"{entity.Reason} | Revoke: {request.Reason.Trim()}";
        entity.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("DelegatedPermission.Revoke", nameof(DelegatedPermission), entity.Id, before, Map(entity),
            cancellationToken: cancellationToken);
        return Map(entity);
    }

    private static DelegatedPermissionDto Map(DelegatedPermission d) =>
        new(d.Id, d.FromAccountId, d.FromAccount.UserName, d.ToAccountId, d.ToAccount.UserName,
            d.PermissionCode, d.OrgUnitId, d.ValidFrom, d.ValidTo, d.Reason, d.ApprovedByAccountId,
            d.IsActive, d.RowVersion);
}

public sealed class DbDelegatedPermissionChecker(IsSuasDbContext db, IClock clock) : IDelegatedPermissionChecker
{
    public bool HasActiveDelegation(Guid accountId, string permissionCode)
    {
        var now = clock.UtcNow;
        return db.DelegatedPermissions.AsNoTracking().Any(d =>
            d.IsActive
            && d.ToAccountId == accountId
            && d.PermissionCode == permissionCode
            && d.ValidFrom <= now
            && (d.ValidTo == null || d.ValidTo > now));
    }
}
