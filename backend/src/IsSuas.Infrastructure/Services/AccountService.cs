using System.Security.Cryptography;
using IsSuas.Application.Abstractions;
using IsSuas.Application.Accounts;
using IsSuas.Application.Policies;
using IsSuas.Domain.Enums;
using IsSuas.Domain.Identity;
using IsSuas.Domain.Pilots;
using IsSuas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IsSuas.Infrastructure.Services;

public interface IAccountService
{
    Task<IReadOnlyList<AccountDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<AccountCreatedDto> CreateAsync(CreateAccountRequest request, CancellationToken cancellationToken = default);
    Task<AccountDto> ConfirmLicenseAsync(Guid id, ConfirmLicenseRequest request, CancellationToken cancellationToken = default);
    Task<AccountDto> SetPasswordAsync(Guid id, SetAccountPasswordRequest request, CancellationToken cancellationToken = default);
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
            .Include(a => a.Person).ThenInclude(p => p.HomeOrgUnit)
            .OrderBy(a => a.UserName)
            .ToListAsync(cancellationToken);

        return accounts
            .Where(a => a.Person.HomeOrgUnit is null || policy.CanAccessOrgUnit(a.Person.HomeOrgUnit.Path))
            .Select(Map)
            .ToList();
    }

    public async Task<AccountCreatedDto> CreateAsync(CreateAccountRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.AccountsWrite);

        var creatorLevel = ResolveCallerAccessLevel();
        var targetLevel = request.AccessLevel is AccessLevel.Level1 or AccessLevel.Level2 or AccessLevel.Level3
            ? request.AccessLevel
            : AccessLevel.Level3;

        if (creatorLevel == AccessLevel.Level3)
            throw new ForbiddenException("Pilot nemá oprávnenie vytvárať účty.");
        if (creatorLevel != AccessLevel.Level1)
        {
            if (targetLevel == AccessLevel.Level1)
                throw new ForbiddenException("Nemôžete vytvoriť gestora (úroveň 1).");
            if ((int)targetLevel < (int)creatorLevel)
                throw new ForbiddenException("Môžete vytvoriť len používateľa s rovnakými alebo nižšími právami.");
        }

        if (string.IsNullOrWhiteSpace(request.UserName))
            throw new ValidationException("Login (používateľské meno) je povinný.");
        if (await db.Accounts.AnyAsync(a => a.UserName == request.UserName.Trim(), cancellationToken))
            throw new ConflictException("Používateľské meno už existuje.");

        if (request.HomeOrgUnitId is null)
            throw new ValidationException("HomeOrgUnitId je povinný — účet musí byť viazaný na jeden útvar.");

        var org = await db.OrgUnits.FirstOrDefaultAsync(x => x.Id == request.HomeOrgUnitId && x.IsActive, cancellationToken)
            ?? throw new NotFoundException("Útvar nebol nájdený.");
        policy.EnsureOrgAccess(org.Path);

        // L2 may only create under own org subtree
        if (creatorLevel == AccessLevel.Level2)
        {
            var homePath = currentUser.Roles.Select(r => r.OrgUnitPath).FirstOrDefault(p => !string.IsNullOrEmpty(p));
            if (string.IsNullOrEmpty(homePath) || !org.Path.StartsWith(homePath, StringComparison.Ordinal))
                throw new ForbiddenException("Účet môžete vytvoriť len v rámci svojho organizačného stromu.");
        }

        var plainPassword = string.IsNullOrWhiteSpace(request.Password)
            ? GeneratePassword()
            : request.Password.Trim();

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

        var license = targetLevel == AccessLevel.Level3
            ? LicenseStatus.Pending
            : LicenseStatus.NotApplicable;

        var account = new Account
        {
            Person = person,
            UserName = request.UserName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(plainPassword),
            Status = AccountStatus.Active,
            AccessLevel = targetLevel,
            LicenseStatus = license,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);

        var roleCode = targetLevel switch
        {
            AccessLevel.Level1 => SystemRole.CentralGestor,
            AccessLevel.Level2 => SystemRole.UnitAdministrator,
            _ => SystemRole.RemotePilot
        };
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Code == roleCode && r.IsActive, cancellationToken)
            ?? throw new NotFoundException($"Rola {roleCode} nebola nájdená.");

        db.RoleAssignments.Add(new RoleAssignment
        {
            AccountId = account.Id,
            RoleId = role.Id,
            OrgUnitId = org.Id,
            ValidFrom = clock.UtcNow,
            Reason = "Vytvorenie účtu (ATLAS úrovne)",
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        });

        if (targetLevel == AccessLevel.Level3)
        {
            db.Pilots.Add(new Pilot
            {
                Person = person,
                OrgUnitId = org.Id,
                LicenseNumber = $"PIL-{account.UserName.ToUpperInvariant()}",
                Status = PilotStatus.Fit,
                CurrencyValidUntil = clock.UtcNow.AddYears(1),
                Notes = "Automaticky vytvorený pri účte L3",
                CreatedBy = currentUser.AccountId,
                UpdatedBy = currentUser.AccountId
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        account.Person = person;
        person.HomeOrgUnit = org;
        await audit.WriteAsync("Account.Create", nameof(Account), account.Id, null, Map(account), cancellationToken: cancellationToken);
        return new AccountCreatedDto(Map(account), plainPassword);
    }

    public async Task<AccountDto> ConfirmLicenseAsync(Guid id, ConfirmLicenseRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.AccountsWrite);
        if (ResolveCallerAccessLevel() == AccessLevel.Level3)
            throw new ForbiddenException("Licenciu prideluje gestor alebo prevádzkovateľ (úroveň 1 alebo 2).");

        var account = await db.Accounts.Include(a => a.Person).ThenInclude(p => p.HomeOrgUnit)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new NotFoundException("Účet nebol nájdený.");

        if (account.Person.HomeOrgUnit is not null)
            policy.EnsureOrgAccess(account.Person.HomeOrgUnit.Path);

        if (account.RowVersion != request.RowVersion)
            throw new ConflictException("Záznam bol medzitým zmenený iným používateľom.");
        if (account.AccessLevel != AccessLevel.Level3)
            throw new ValidationException("Licenciu potvrdzujete iba pre pilotov (úroveň 3).");
        if (request.Status is not (LicenseStatus.Confirmed or LicenseStatus.Rejected or LicenseStatus.Pending))
            throw new ValidationException("Neplatný stav licencie.");

        var before = Map(account);
        account.LicenseStatus = request.Status;
        account.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("Account.ConfirmLicense", nameof(Account), account.Id, before, Map(account),
            cancellationToken: cancellationToken);
        return Map(account);
    }

    public async Task<AccountDto> SetPasswordAsync(Guid id, SetAccountPasswordRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.AccountsWrite);
        if (ResolveCallerAccessLevel() != AccessLevel.Level1
            && !currentUser.Roles.Any(r => r.RoleCode == nameof(SystemRole.CentralGestor)))
            throw new ForbiddenException("Heslo môže nastaviť iba gestor (úroveň 1).");

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Trim().Length < 8)
            throw new ValidationException("Heslo musí mať aspoň 8 znakov.");

        var account = await db.Accounts.Include(a => a.Person).ThenInclude(p => p.HomeOrgUnit)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new NotFoundException("Účet nebol nájdený.");

        if (account.RowVersion != request.RowVersion)
            throw new ConflictException("Záznam bol medzitým zmenený iným používateľom.");

        var before = Map(account);
        account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password.Trim());
        if (!string.IsNullOrWhiteSpace(request.FirstName))
            account.Person.FirstName = request.FirstName.Trim();
        if (!string.IsNullOrWhiteSpace(request.LastName))
            account.Person.LastName = request.LastName.Trim();
        if (request.Email is not null)
            account.Person.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        account.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("Account.SetPassword", nameof(Account), account.Id, before, Map(account),
            cancellationToken: cancellationToken);
        return Map(account);
    }

    public async Task<AccountDto> ChangeStatusAsync(Guid id, ChangeAccountStatusRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.AccountsWrite);
        var account = await db.Accounts.Include(a => a.Person).ThenInclude(p => p.HomeOrgUnit)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new NotFoundException("Účet nebol nájdený.");

        if (account.RowVersion != request.RowVersion)
            throw new ConflictException("Záznam bol medzitým zmenený iným používateľom.");

        var before = Map(account);
        account.Status = request.Status;
        if (request.Status == AccountStatus.Deactivated)
            account.IsActive = false;
        else if (request.Status == AccountStatus.Active)
            account.IsActive = true; // obnovenie odstráneného účtu
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
        var account = await db.Accounts.Include(a => a.Person).FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken)
            ?? throw new NotFoundException("Účet nebol nájdený.");
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == request.RoleId && r.IsActive, cancellationToken)
            ?? throw new NotFoundException("Rola nebola nájdená.");
        var org = await db.OrgUnits.FirstOrDefaultAsync(o => o.Id == request.OrgUnitId && o.IsActive, cancellationToken)
            ?? throw new NotFoundException("Útvar nebol nájdený.");
        policy.EnsureOrgAccess(org.Path);

        if (role.Code != SystemRole.SystemAdministrator)
        {
            if (account.Person.HomeOrgUnitId is null)
            {
                account.Person.HomeOrgUnitId = org.Id;
                account.Person.UpdatedBy = currentUser.AccountId;
            }
            else if (account.Person.HomeOrgUnitId != org.Id && role.Code != SystemRole.CentralGestor)
            {
                throw new ValidationException(
                    "Účet je viazaný na iný útvar (HomeOrgUnit). Pre bežné role nie je možné pridať prístup k cudziemu útvaru.");
            }

            var otherActive = await db.RoleAssignments
                .Include(x => x.Role)
                .AnyAsync(x => x.AccountId == accountId && x.IsActive && x.OrgUnitId != org.Id
                    && x.Role.Code != SystemRole.SystemAdministrator && x.Role.Code != SystemRole.CentralGestor, cancellationToken);
            if (otherActive)
                throw new ValidationException("Účet už má aktívne priradenie na inom útvare.");
        }

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

    private AccessLevel ResolveCallerAccessLevel()
    {
        if (currentUser.Roles.Any(r => r.RoleCode == nameof(SystemRole.CentralGestor)))
            return AccessLevel.Level1;
        if (currentUser.Roles.Any(r => r.RoleCode == nameof(SystemRole.UnitAdministrator)))
            return AccessLevel.Level2;
        if (currentUser.Roles.Any(r => r.RoleCode == nameof(SystemRole.RemotePilot)))
            return AccessLevel.Level3;
        // Fallback from account if loaded via roles only
        return AccessLevel.Level2;
    }

    private static string GeneratePassword()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%";
        var bytes = RandomNumberGenerator.GetBytes(16);
        var chars = new char[16];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = alphabet[bytes[i] % alphabet.Length];
        return new string(chars);
    }

    private static AccountDto Map(Account a) =>
        new(a.Id, a.PersonId, a.UserName, a.Person.FirstName, a.Person.LastName, a.Person.Email,
            a.Status, a.AccessLevel, a.LicenseStatus, a.Person.HomeOrgUnitId, a.Person.HomeOrgUnit?.Name,
            a.IsActive, a.RowVersion);

    private static RoleAssignmentDto MapAssignment(RoleAssignment x) =>
        new(x.Id, x.RoleId, x.Role.Code.ToString(), x.Role.NameSk, x.OrgUnitId, x.OrgUnit.Name,
            x.ValidFrom, x.ValidTo, x.Reason, x.IsActive);
}
