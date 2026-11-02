using IsSuas.Domain.Enums;

namespace IsSuas.Application.Accounts;

public sealed record AccountDto(
    Guid Id,
    Guid PersonId,
    string UserName,
    string FirstName,
    string LastName,
    string? Email,
    AccountStatus Status,
    AccessLevel AccessLevel,
    LicenseStatus LicenseStatus,
    Guid? HomeOrgUnitId,
    string? HomeOrgUnitName,
    bool IsActive,
    uint RowVersion);

/// <summary>Návrh loginu: UserName = voľný login, BaseAvailable = zadaný základ bol voľný.</summary>
public sealed record UserNameSuggestionDto(string UserName, bool BaseAvailable, bool IsValid);

public sealed record AccountCreatedDto(
    AccountDto Account,
    string PlainPassword);

public sealed record CreateAccountRequest(
    string UserName,
    string FirstName,
    string LastName,
    string? Email,
    Guid? HomeOrgUnitId,
    AccessLevel AccessLevel = AccessLevel.Level3,
    /// <summary>Optional; when null/empty the server generates a strong password.</summary>
    string? Password = null,
    /// <summary>Pre pilota (úroveň 3): nadriadený prevádzkovateľ.</summary>
    Guid? OperatorAccountId = null);

public sealed record ConfirmLicenseRequest(LicenseStatus Status, string? Reason, uint RowVersion);

public sealed record SetAccountPasswordRequest(
    string Password,
    uint RowVersion,
    string? FirstName = null,
    string? LastName = null,
    string? Email = null);

/// <summary>Úprava údajov používateľa (meno, priezvisko, e-mail, login).</summary>
public sealed record UpdateAccountRequest(
    string FirstName,
    string? LastName,
    string? Email,
    string? UserName,
    uint RowVersion);

public sealed record ChangeAccountStatusRequest(AccountStatus Status, string Reason, uint RowVersion);

public sealed record RoleAssignmentDto(
    Guid Id,
    Guid RoleId,
    string RoleCode,
    string RoleNameSk,
    Guid OrgUnitId,
    string OrgUnitName,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    string Reason,
    bool IsActive);

public sealed record CreateRoleAssignmentRequest(
    Guid RoleId,
    Guid OrgUnitId,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    string Reason);

public sealed record DeactivateRoleAssignmentRequest(string Reason, uint RowVersion);
