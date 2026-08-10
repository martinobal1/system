namespace IsSuas.Application.OrgUnits;

using IsSuas.Domain.Enums;

public sealed record OrgUnitDto(
    Guid Id,
    string Code,
    string Name,
    OrgUnitKind Kind,
    Guid? ParentId,
    string Path,
    int Level,
    bool IsActive,
    uint RowVersion);

public sealed record CreateOrgUnitRequest(
    string Code,
    string Name,
    Guid? ParentId,
    OrgUnitKind? Kind = null);

public sealed record UpdateOrgUnitRequest(string Name, uint RowVersion);
public sealed record MoveOrgUnitRequest(Guid? NewParentId, string Reason, uint RowVersion);
