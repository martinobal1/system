namespace IsSuas.Application.Fleet;

public sealed record PilotUasAuthorizationDto(
    Guid Id,
    Guid PilotId,
    string PilotName,
    Guid UasId,
    string UasLabel,
    Guid OrgUnitId,
    string OrgUnitName,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    bool IsActive,
    string? Note,
    uint RowVersion);

public sealed record CreatePilotUasAuthorizationRequest(
    Guid PilotId,
    Guid UasId,
    Guid? OrgUnitId,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    string? Note);

public sealed record EndPilotUasAuthorizationRequest(DateTimeOffset? ValidTo, uint RowVersion);
