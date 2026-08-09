namespace IsSuas.Application.Abstractions;

public interface ICurrentUser
{
    Guid? AccountId { get; }
    string? UserName { get; }
    bool IsAuthenticated { get; }
    bool IsSystemAdministrator { get; }
    IReadOnlyList<EffectiveRole> Roles { get; }
}

public sealed record EffectiveRole(
    Guid RoleId,
    string RoleCode,
    Guid OrgUnitId,
    string OrgUnitPath,
    bool GrantsBusinessDataAccess);
