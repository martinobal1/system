using IsSuas.Application.Abstractions;
using IsSuas.Application.Accounts;
using IsSuas.Application.Policies;
using IsSuas.Domain.Enums;
using IsSuas.Domain.Identity;
using IsSuas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IsSuas.Infrastructure.Services;

public interface IAccountService
{
    Task<IReadOnlyList<AccountDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<AccountDto> CreateAsync(CreateAccountRequest request, CancellationToken cancellationToken = default);
    Task<AccountDto> ChangeStatusAsync(Guid id, ChangeAccountStatusRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoleAssignmentDto>> ListRoleAssignmentsAsync(Guid accountId, CancellationToken cancellationToken = default);
    Task<RoleAssignmentDto> AssignRoleAsync(Guid accountId, CreateRoleAssignmentRequest request, CancellationToken cancellationToken = default);
    Task DeactivateRoleAssignmentAsync(Guid accountId, Guid assignmentId, DeactivateRoleAssignmentRequest request, CancellationToken cancellationToken = default);
}

public sealed class AccountService(
    IsSuasDbContext db,
    IAuthorizationPolicyService policy,
    ICurrentUser currentUser,
    IAuditWriter audit,
    IClock clock) : IAccountService
{
    public async Task<IReadOnlyList<AccountDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.AccountsRead);
        var accounts = await db.Accounts.AsNoTracking()
            .Include(a => a.Person)
            .OrderBy(a => a.UserName)
            .ToListAsync(cancellationToken);

        return accounts.Select(Map).ToList();
    }

    public async Task<AccountDto> CreateAsync(CreateAccountRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.AccountsWrite);

        if (await db.Accounts.AnyAsync(a => a.UserName == request.UserName, cancellationToken))
            throw new ConflictException("Používateľské meno už existuje.");

        if (request.HomeOrgUnitId is not null)
        {
            var org = await db.OrgUnits.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.HomeOrgUnitId, cancellationToken)
                ?? throw new NotFoundException("Útvar nebol nájdený.");
            policy.EnsureOrgAccess(org.Path);
        }

        var person = new Person
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = request.Email,
            HomeOrgUnitId = request.HomeOrgUnitId,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.Persons.Add(person);

        var account = new Account
        {
            Person = person,
            UserName = request.UserName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Status = AccountStatus.Active,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("Account.Create", nameof(Account), account.Id, null, Map(account), cancellationToken: cancellationToken);
        return Map(account);
    }

    public async Task<AccountDto> ChangeStatusAsync(Guid id, ChangeAccountStatusRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.AccountsWrite);
        var account = await db.Accounts.Include(a => a.Person).FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new NotFoundException("Účet nebol nájdený.");

        if (account.RowVersion != request.RowVersion)
            throw new ConflictException("Záznam bol medzitým zmenený iným používateľom.");

        var before = Map(account);
        account.Status = request.Status;
        if (request.Status == AccountStatus.Deactivated)
            account.IsActive = false;
        account.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("Account.ChangeStatus", nameof(Account), account.Id, before, Map(account), cancellationToken: cancellationToken);
        return Map(account);
    }

    public async Task<IReadOnlyList<RoleAssignmentDto>> ListRoleAssignmentsAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.AccountsRead);
        var items = await db.RoleAssignments.AsNoTracking()
            .Include(x => x.Role)
            .Include(x => x.OrgUnit)
            .Where(x => x.AccountId == accountId)
            .OrderByDescending(x => x.ValidFrom)
            .ToListAsync(cancellationToken);
        return items.Select(MapAssignment).ToList();
    }

    public async Task<RoleAssignmentDto> AssignRoleAsync(Guid accountId, CreateRoleAssignmentRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.RolesAssign);
        _ = await db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken)
            ?? throw new NotFoundException("Účet nebol nájdený.");
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == request.RoleId && r.IsActive, cancellationToken)
            ?? throw new NotFoundException("Rola nebola nájdená.");
        var org = await db.OrgUnits.FirstOrDefaultAsync(o => o.Id == request.OrgUnitId && o.IsActive, cancellationToken)
            ?? throw new NotFoundException("Útvar nebol nájdený.");
        policy.EnsureOrgAccess(org.Path);

        var assignment = new RoleAssignment
        {
            AccountId = accountId,
            RoleId = role.Id,
            OrgUnitId = org.Id,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            Reason = request.Reason,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.RoleAssignments.Add(assignment);
        await db.SaveChangesAsync(cancellationToken);
        assignment.Role = role;
        assignment.OrgUnit = org;
        await audit.WriteAsync("RoleAssignment.Create", nameof(RoleAssignment), assignment.Id, null, MapAssignment(assignment), cancellationToken: cancellationToken);
        return MapAssignment(assignment);
    }

    public async Task DeactivateRoleAssignmentAsync(Guid accountId, Guid assignmentId, DeactivateRoleAssignmentRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.RolesAssign);
        var assignment = await db.RoleAssignments
            .Include(x => x.Role)
            .Include(x => x.OrgUnit)
            .FirstOrDefaultAsync(x => x.Id == assignmentId && x.AccountId == accountId, cancellationToken)
            ?? throw new NotFoundException("Pridelenie roly nebolo nájdené.");

        if (assignment.RowVersion != request.RowVersion)
            throw new ConflictException("Záznam bol medzitým zmenený iným používateľom.");

        var before = MapAssignment(assignment);
        assignment.IsActive = false;
        assignment.ValidTo ??= clock.UtcNow;
        assignment.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("RoleAssignment.Deactivate", nameof(RoleAssignment), assignment.Id, before, MapAssignment(assignment), cancellationToken: cancellationToken);
    }

    private static AccountDto Map(Account a) =>
        new(a.Id, a.PersonId, a.UserName, a.Person.FirstName, a.Person.LastName, a.Person.Email,
            a.Status, a.Person.HomeOrgUnitId, a.IsActive, a.RowVersion);

    private static RoleAssignmentDto MapAssignment(RoleAssignment x) =>
        new(x.Id, x.RoleId, x.Role.Code.ToString(), x.Role.NameSk, x.OrgUnitId, x.OrgUnit.Name,
            x.ValidFrom, x.ValidTo, x.Reason, x.IsActive);
}
