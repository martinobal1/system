using System.Text.Json;
using IsSuas.Application.Abstractions;
using IsSuas.Application.Controls;
using IsSuas.Application.Policies;
using IsSuas.Domain.Controls;
using IsSuas.Domain.Enums;
using IsSuas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IsSuas.Infrastructure.Services;

public interface IControlAuditService
{
    Task<IReadOnlyList<ControlAuditDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<ControlAuditDetailDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ControlAuditDto> CreateAsync(CreateControlAuditRequest request, CancellationToken cancellationToken = default);
    Task<ControlAuditDto> StartAsync(Guid id, StartControlAuditRequest request, CancellationToken cancellationToken = default);
    Task<ControlChecklistItemDto> UpdateChecklistAsync(Guid auditId, Guid itemId, UpdateChecklistResultRequest request, CancellationToken cancellationToken = default);
    Task<ControlFindingDto> AddFindingAsync(Guid auditId, AddControlFindingRequest request, CancellationToken cancellationToken = default);
    Task<ControlCorrectiveActionDto> AddActionAsync(Guid findingId, AddControlActionRequest request, CancellationToken cancellationToken = default);
    Task<ControlCorrectiveActionDto> CompleteActionAsync(Guid actionId, CompleteControlActionRequest request, CancellationToken cancellationToken = default);
    Task<ControlReportDto> IssueReportAsync(Guid auditId, IssueControlReportRequest request, CancellationToken cancellationToken = default);
    Task<ControlAuditDto> CloseAsync(Guid id, CloseControlAuditRequest request, CancellationToken cancellationToken = default);
}

public sealed class ControlAuditService(
    IsSuasDbContext db,
    IAuthorizationPolicyService policy,
    ICurrentUser currentUser,
    IAuditWriter audit,
    IClock clock) : IControlAuditService
{
    public async Task<IReadOnlyList<ControlAuditDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.ControlsRead);
        var items = await db.ControlAudits.AsNoTracking()
            .Include(x => x.OrgUnit)
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        return items.Where(x => policy.CanAccessOrgUnit(x.OrgUnit.Path)).Select(Map).ToList();
    }

    public async Task<ControlAuditDetailDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.ControlsRead);
        var entity = await LoadAsync(id, cancellationToken);
        policy.EnsureOrgAccess(entity.OrgUnit.Path);
        return MapDetail(entity);
    }

    public async Task<ControlAuditDto> CreateAsync(CreateControlAuditRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.ControlsWrite);
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ValidationException("Názov kontroly je povinný.");
        var org = await db.OrgUnits.AsNoTracking().FirstOrDefaultAsync(o => o.Id == request.OrgUnitId && o.IsActive, cancellationToken)
            ?? throw new NotFoundException("Útvar nebol nájdený.");
        policy.EnsureOrgAccess(org.Path);

        var entity = new ControlAudit
        {
            Title = request.Title.Trim(),
            Type = request.Type,
            Status = ControlAuditStatus.Planned,
            OrgUnitId = org.Id,
            PlannedFrom = request.PlannedFrom,
            PlannedTo = request.PlannedTo,
            ScopeDescription = request.ScopeDescription?.Trim(),
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };

        if (request.ChecklistItems is { Count: > 0 })
        {
            foreach (var item in request.ChecklistItems)
            {
                entity.ChecklistItems.Add(new ControlChecklistItem
                {
                    Code = item.Code.Trim(),
                    QuestionSk = item.QuestionSk.Trim(),
                    SortOrder = item.SortOrder,
                    CreatedBy = currentUser.AccountId,
                    UpdatedBy = currentUser.AccountId
                });
            }
        }

        if (currentUser.AccountId is Guid accountId)
        {
            entity.TeamMembers.Add(new ControlTeamMember
            {
                AccountId = accountId,
                RoleInTeam = "Lead",
                CreatedBy = accountId,
                UpdatedBy = accountId
            });
        }

        db.ControlAudits.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        entity.OrgUnit = org;
        await audit.WriteAsync("ControlAudit.Create", nameof(ControlAudit), entity.Id, null, Map(entity), cancellationToken: cancellationToken);
        return Map(entity);
    }

    public async Task<ControlAuditDto> StartAsync(Guid id, StartControlAuditRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.ControlsWrite);
        var entity = await LoadAsync(id, cancellationToken, tracking: true);
        policy.EnsureOrgAccess(entity.OrgUnit.Path);
        EnsureRow(entity.RowVersion, request.RowVersion);
        if (entity.Status is not ControlAuditStatus.Planned)
            throw new ValidationException("Spustiť je možné iba plánovanú kontrolu.");
        entity.Status = ControlAuditStatus.InProgress;
        entity.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("ControlAudit.Start", nameof(ControlAudit), entity.Id, null, Map(entity), cancellationToken: cancellationToken);
        return Map(entity);
    }

    public async Task<ControlChecklistItemDto> UpdateChecklistAsync(
        Guid auditId, Guid itemId, UpdateChecklistResultRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.ControlsWrite);
        var entity = await db.ControlAudits.Include(x => x.OrgUnit)
            .FirstOrDefaultAsync(x => x.Id == auditId, cancellationToken)
            ?? throw new NotFoundException("Kontrola nebola nájdená.");
        policy.EnsureOrgAccess(entity.OrgUnit.Path);
        if (entity.Status is ControlAuditStatus.Closed or ControlAuditStatus.Cancelled)
            throw new ValidationException("Uzavretú kontrolu nie je možné meniť.");
        var item = await db.ControlChecklistItems.FirstOrDefaultAsync(i => i.Id == itemId && i.ControlAuditId == auditId, cancellationToken)
            ?? throw new NotFoundException("Kontrolný bod nebol nájdený.");
        EnsureRow(item.RowVersion, request.RowVersion);
        item.ResultStatus = request.ResultStatus;
        item.ResultNote = request.ResultNote?.Trim();
        item.UpdatedBy = currentUser.AccountId;
        entity.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        return MapItem(item);
    }

    public async Task<ControlFindingDto> AddFindingAsync(Guid auditId, AddControlFindingRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.ControlsWrite);
        var entity = await db.ControlAudits
            .Include(x => x.OrgUnit)
            .FirstOrDefaultAsync(x => x.Id == auditId, cancellationToken)
            ?? throw new NotFoundException("Kontrola nebola nájdená.");
        policy.EnsureOrgAccess(entity.OrgUnit.Path);
        if (entity.Status is ControlAuditStatus.Closed or ControlAuditStatus.Cancelled)
            throw new ValidationException("Uzavretú kontrolu nie je možné meniť.");
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description))
            throw new ValidationException("Názov a opis zistenia sú povinné.");

        var finding = new ControlFinding
        {
            ControlAuditId = entity.Id,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Severity = request.Severity,
            RelatedEntityType = request.RelatedEntityType?.Trim(),
            RelatedEntityId = request.RelatedEntityId,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.ControlFindings.Add(finding);
        entity.Status = ControlAuditStatus.FindingsReview;
        entity.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("ControlFinding.Add", nameof(ControlFinding), finding.Id, null, MapFinding(finding), cancellationToken: cancellationToken);
        return MapFinding(finding);
    }

    public async Task<ControlCorrectiveActionDto> AddActionAsync(Guid findingId, AddControlActionRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.ControlsWrite);
        var finding = await db.ControlFindings
            .Include(f => f.ControlAudit).ThenInclude(a => a.OrgUnit)
            .FirstOrDefaultAsync(f => f.Id == findingId, cancellationToken)
            ?? throw new NotFoundException("Zistenie nebolo nájdené.");
        policy.EnsureOrgAccess(finding.ControlAudit.OrgUnit.Path);
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ValidationException("Názov opatrenia je povinný.");

        var action = new ControlCorrectiveAction
        {
            ControlFindingId = finding.Id,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            OwnerAccountId = request.OwnerAccountId,
            DueAt = request.DueAt,
            Status = ControlActionStatus.Open,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.ControlCorrectiveActions.Add(action);
        await db.SaveChangesAsync(cancellationToken);
        return MapAction(action);
    }

    public async Task<ControlCorrectiveActionDto> CompleteActionAsync(Guid actionId, CompleteControlActionRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.ControlsWrite);
        var action = await db.ControlCorrectiveActions
            .Include(a => a.ControlFinding).ThenInclude(f => f.ControlAudit).ThenInclude(c => c.OrgUnit)
            .FirstOrDefaultAsync(a => a.Id == actionId, cancellationToken)
            ?? throw new NotFoundException("Opatrenie nebolo nájdené.");
        policy.EnsureOrgAccess(action.ControlFinding.ControlAudit.OrgUnit.Path);
        EnsureRow(action.RowVersion, request.RowVersion);
        action.Status = ControlActionStatus.Completed;
        action.ProgressNote = request.ProgressNote?.Trim();
        action.CompletedAt = clock.UtcNow;
        action.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        return MapAction(action);
    }

    public async Task<ControlReportDto> IssueReportAsync(Guid auditId, IssueControlReportRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.ControlsClose);
        if (currentUser.AccountId is null)
            throw new ForbiddenException("Chýba identita.");
        var entity = await db.ControlAudits
            .Include(x => x.OrgUnit)
            .Include(x => x.Findings)
            .Include(x => x.ChecklistItems)
            .Include(x => x.Reports)
            .FirstOrDefaultAsync(x => x.Id == auditId, cancellationToken)
            ?? throw new NotFoundException("Kontrola nebola nájdená.");
        policy.EnsureOrgAccess(entity.OrgUnit.Path);
        EnsureRow(entity.RowVersion, request.RowVersion);

        var version = entity.Reports.Count == 0 ? 1 : entity.Reports.Max(r => r.Version) + 1;
        var payload = request.ContentJson;
        if (string.IsNullOrWhiteSpace(payload))
        {
            payload = JsonSerializer.Serialize(new
            {
                entity.Title,
                entity.Status,
                Findings = entity.Findings.Where(f => f.IsActive).Select(f => new { f.Title, f.Severity, f.Description }),
                Checklist = entity.ChecklistItems.Where(c => c.IsActive).Select(c => new { c.Code, c.ResultStatus })
            });
        }

        var report = new ControlReport
        {
            ControlAuditId = entity.Id,
            Version = version,
            ContentJson = payload,
            IsFrozen = true,
            IssuedByAccountId = currentUser.AccountId.Value,
            IssuedAt = clock.UtcNow,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.ControlReports.Add(report);
        entity.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("ControlReport.Issue", nameof(ControlReport), report.Id, null, MapReport(report), cancellationToken: cancellationToken);
        return MapReport(report);
    }

    public async Task<ControlAuditDto> CloseAsync(Guid id, CloseControlAuditRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.ControlsClose);
        var entity = await db.ControlAudits
            .Include(x => x.OrgUnit)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Kontrola nebola nájdená.");
        policy.EnsureOrgAccess(entity.OrgUnit.Path);
        EnsureRow(entity.RowVersion, request.RowVersion);
        if (entity.Status is ControlAuditStatus.Closed or ControlAuditStatus.Cancelled)
            throw new ValidationException("Kontrola je už uzavretá.");
        entity.Status = ControlAuditStatus.Closed;
        entity.Summary = request.Summary?.Trim();
        entity.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("ControlAudit.Close", nameof(ControlAudit), entity.Id, null, Map(entity), cancellationToken: cancellationToken);
        return Map(entity);
    }

    private async Task<ControlAudit> LoadAsync(Guid id, CancellationToken cancellationToken, bool tracking = false)
    {
        IQueryable<ControlAudit> q = db.ControlAudits
            .Include(x => x.OrgUnit)
            .Include(x => x.TeamMembers)
            .Include(x => x.ChecklistItems)
            .Include(x => x.Findings).ThenInclude(f => f.Actions)
            .Include(x => x.Reports);
        if (!tracking) q = q.AsNoTracking();
        return await q.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Kontrola nebola nájdená.");
    }

    private static void EnsureRow(uint actual, uint expected)
    {
        if (actual != expected)
            throw new ConflictException("Záznam bol medzitým zmenený iným používateľom.");
    }

    private static ControlAuditDto Map(ControlAudit e) =>
        new(e.Id, e.Title, e.Type, e.Status, e.OrgUnitId, e.OrgUnit.Name, e.PlannedFrom, e.PlannedTo,
            e.ScopeDescription, e.Summary, e.IsActive, e.RowVersion);

    private static ControlAuditDetailDto MapDetail(ControlAudit e) =>
        new(Map(e),
            e.TeamMembers.Where(t => t.IsActive).Select(t => new ControlTeamMemberDto(t.Id, t.AccountId, t.RoleInTeam)).ToList(),
            e.ChecklistItems.Where(i => i.IsActive).OrderBy(i => i.SortOrder).Select(MapItem).ToList(),
            e.Findings.Where(f => f.IsActive).Select(MapFinding).ToList(),
            e.Reports.Where(r => r.IsActive).OrderByDescending(r => r.Version).Select(MapReport).ToList());

    private static ControlChecklistItemDto MapItem(ControlChecklistItem i) =>
        new(i.Id, i.Code, i.QuestionSk, i.SortOrder, i.ResultStatus, i.ResultNote, i.RowVersion);

    private static ControlFindingDto MapFinding(ControlFinding f) =>
        new(f.Id, f.Title, f.Description, f.Severity, f.RelatedEntityType, f.RelatedEntityId,
            f.Actions.Where(a => a.IsActive).Select(MapAction).ToList(), f.RowVersion);

    private static ControlCorrectiveActionDto MapAction(ControlCorrectiveAction a) =>
        new(a.Id, a.Title, a.Description, a.OwnerAccountId, a.DueAt, a.Status, a.ProgressNote, a.RowVersion);

    private static ControlReportDto MapReport(ControlReport r) =>
        new(r.Id, r.Version, r.IsFrozen, r.IssuedByAccountId, r.IssuedAt, r.ContentJson);
}
