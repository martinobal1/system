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
    Guid? HomeOrgUnitId,
    bool IsActive,
    uint RowVersion);

public sealed record CreateAccountRequest(
    string UserName,
    string Password,
    string FirstName,
    string LastName,
    string? Email,
    Guid? HomeOrgUnitId);

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
