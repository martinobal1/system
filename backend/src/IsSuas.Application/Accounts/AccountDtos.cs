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
    string? Password = null);

public sealed record ConfirmLicenseRequest(LicenseStatus Status, string? Reason, uint RowVersion);

public sealed record SetAccountPasswordRequest(
    string Password,
    uint RowVersion,
    string? FirstName = null,
    string? LastName = null,
    string? Email = null);

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
