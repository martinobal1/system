using IsSuas.Application.Abstractions;
using IsSuas.Domain.Enums;

namespace IsSuas.Application.Policies;

public interface IAuthorizationPolicyService
{
    bool HasPermission(string permission);
    bool CanAccessOrgUnit(string orgUnitPath);
    bool CanAccessBusinessData();
    void EnsurePermission(string permission);
    void EnsureOrgAccess(string orgUnitPath);
    void EnsureCanApprove(Guid? submitterAccountId, string approvePermission);
}

public sealed class AuthorizationPolicyService(
    ICurrentUser currentUser,
    IDelegatedPermissionChecker delegatedPermissions) : IAuthorizationPolicyService
{
    public bool HasPermission(string permission)
    {
        if (!currentUser.IsAuthenticated)
            return false;

        if (!PermissionCatalog.IsKnown(permission))
            return false;

        if (PermissionCatalog.IsGrantedByRoles(permission, currentUser.Roles.Select(r => r.RoleCode)))
            return true;

        if (currentUser.AccountId is Guid accountId
            && delegatedPermissions.HasActiveDelegation(accountId, permission))
            return true;

        return false;
    }

    public bool CanAccessBusinessData()
    {
        if (!currentUser.IsAuthenticated)
            return false;

        // System administrator must not automatically access business content.
        if (currentUser.IsSystemAdministrator && currentUser.Roles.All(r => !r.GrantsBusinessDataAccess))
            return false;

        return currentUser.Roles.Any(r => r.GrantsBusinessDataAccess);
    }

    public bool CanAccessOrgUnit(string orgUnitPath)
    {
        // System administrator without business-data role must not access org business content.
        if (!CanAccessBusinessData())
            return false;

        // Central gestor with root-level assignment can see all via path prefix.
        foreach (var role in currentUser.Roles)
        {
            if (string.IsNullOrEmpty(role.OrgUnitPath))
                continue;
            // Access to own unit and descendants: path starts with assignment path.
            if (orgUnitPath.StartsWith(role.OrgUnitPath, StringComparison.Ordinal))
                return true;
        }

        // Also allow if any role is CentralGestor on a root that covers the path.
        foreach (var role in currentUser.Roles)
        {
            if (role.RoleCode == nameof(SystemRole.CentralGestor)
                && orgUnitPath.StartsWith(role.OrgUnitPath, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public void EnsurePermission(string permission)
    {
        if (!HasPermission(permission))
            throw new ForbiddenException($"Chýba oprávnenie: {permission}");
    }

    public void EnsureOrgAccess(string orgUnitPath)
    {
        if (!CanAccessOrgUnit(orgUnitPath))
            throw new ForbiddenException("Prístup k organizačnému útvaru bol zamietnutý.");
    }

    public void EnsureCanApprove(Guid? submitterAccountId, string approvePermission)
    {
        EnsurePermission(approvePermission);
        if (submitterAccountId.HasValue
            && currentUser.AccountId.HasValue
            && submitterAccountId.Value == currentUser.AccountId.Value)
        {
            throw new ForbiddenException("Oddelenie zodpovedností: nie je možné schváliť vlastný návrh.");
        }
    }
}
public sealed class ForbiddenException(string message) : Exception(message);
public sealed class ConflictException(string message) : Exception(message);
public sealed class NotFoundException(string message) : Exception(message);
public sealed class ValidationException(string message) : Exception(message);
