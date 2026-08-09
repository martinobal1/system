namespace IsSuas.Application.OrgUnits;

public sealed record OrgUnitDto(
    Guid Id,
    string Code,
    string Name,
    Guid? ParentId,
    string Path,
    int Level,
    bool IsActive);

public sealed record CreateOrgUnitRequest(string Code, string Name, Guid? ParentId);
public sealed record UpdateOrgUnitRequest(string Name, uint RowVersion);
public sealed record MoveOrgUnitRequest(Guid? NewParentId, string Reason, uint RowVersion);
