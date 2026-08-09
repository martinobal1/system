namespace IsSuas.Application.Accounts;

public sealed record DelegatedPermissionDto(
    Guid Id,
    Guid FromAccountId,
    string FromUserName,
    Guid ToAccountId,
    string ToUserName,
    string PermissionCode,
    Guid? OrgUnitId,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    string Reason,
    Guid? ApprovedByAccountId,
    bool IsActive,
    uint RowVersion);

public sealed record CreateDelegatedPermissionRequest(
    Guid ToAccountId,
    string PermissionCode,
    Guid? OrgUnitId,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    string Reason);

public sealed record RevokeDelegatedPermissionRequest(string? Reason, uint RowVersion);
