namespace IsSuas.Application.Abstractions;

/// <summary>Runtime lookup of ENT-010 delegated permissions for the current account.</summary>
public interface IDelegatedPermissionChecker
{
    bool HasActiveDelegation(Guid accountId, string permissionCode);
}

public sealed class NullDelegatedPermissionChecker : IDelegatedPermissionChecker
{
    public bool HasActiveDelegation(Guid accountId, string permissionCode) => false;
}
