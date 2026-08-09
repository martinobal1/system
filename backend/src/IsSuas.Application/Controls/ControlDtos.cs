using IsSuas.Domain.Enums;

namespace IsSuas.Application.Controls;

public sealed record ControlAuditDto(
    Guid Id,
    string Title,
    ControlAuditType Type,
    ControlAuditStatus Status,
    Guid OrgUnitId,
    string OrgUnitName,
    DateTimeOffset? PlannedFrom,
    DateTimeOffset? PlannedTo,
    string? ScopeDescription,
    string? Summary,
    bool IsActive,
    uint RowVersion);

public sealed record ControlAuditDetailDto(
    ControlAuditDto Audit,
    IReadOnlyList<ControlTeamMemberDto> Team,
    IReadOnlyList<ControlChecklistItemDto> Checklist,
    IReadOnlyList<ControlFindingDto> Findings,
    IReadOnlyList<ControlReportDto> Reports);

public sealed record ControlTeamMemberDto(Guid Id, Guid AccountId, string RoleInTeam);
public sealed record ControlChecklistItemDto(Guid Id, string Code, string QuestionSk, int SortOrder, ControlCheckpointResultStatus ResultStatus, string? ResultNote, uint RowVersion);
public sealed record ControlFindingDto(Guid Id, string Title, string Description, ControlFindingSeverity Severity, string? RelatedEntityType, Guid? RelatedEntityId, IReadOnlyList<ControlCorrectiveActionDto> Actions, uint RowVersion);
public sealed record ControlCorrectiveActionDto(Guid Id, string Title, string? Description, Guid? OwnerAccountId, DateTimeOffset? DueAt, ControlActionStatus Status, string? ProgressNote, uint RowVersion);
public sealed record ControlReportDto(Guid Id, int Version, bool IsFrozen, Guid IssuedByAccountId, DateTimeOffset IssuedAt, string ContentJson);

public sealed record CreateControlAuditRequest(
    string Title,
    ControlAuditType Type,
    Guid OrgUnitId,
    DateTimeOffset? PlannedFrom,
    DateTimeOffset? PlannedTo,
    string? ScopeDescription,
    IReadOnlyList<CreateChecklistItemRequest>? ChecklistItems);

public sealed record CreateChecklistItemRequest(string Code, string QuestionSk, int SortOrder);
public sealed record StartControlAuditRequest(uint RowVersion);
public sealed record AddControlFindingRequest(string Title, string Description, ControlFindingSeverity Severity, string? RelatedEntityType, Guid? RelatedEntityId);
public sealed record AddControlActionRequest(string Title, string? Description, Guid? OwnerAccountId, DateTimeOffset? DueAt);
public sealed record UpdateChecklistResultRequest(ControlCheckpointResultStatus ResultStatus, string? ResultNote, uint RowVersion);
public sealed record CompleteControlActionRequest(string? ProgressNote, uint RowVersion);
public sealed record CloseControlAuditRequest(string? Summary, uint RowVersion);
public sealed record IssueControlReportRequest(string? ContentJson, uint RowVersion);

public sealed record PermissionDto(Guid Id, string Code, string NameSk, string ModuleCode);
public sealed record RolePermissionDto(Guid RoleId, string RoleCode, string PermissionCode);
public sealed record UserSessionDto(Guid Id, Guid AccountId, DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt, DateTimeOffset? RevokedAt, string? IpAddress, string Result);
