using IsSuas.Application.Abstractions;
using IsSuas.Application.Operations;
using IsSuas.Application.Policies;
using IsSuas.Domain.Enums;
using IsSuas.Domain.Fleet;
using IsSuas.Domain.Notifications;
using IsSuas.Domain.Operations;
using IsSuas.Domain.Pilots;
using IsSuas.Infrastructure.Integrations;
using IsSuas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace IsSuas.Infrastructure.Services;

public interface IOperationService
{
    Task<IReadOnlyList<OperationDto>> ListAsync(DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken = default);
    Task<OperationDetailDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OperationDto> CreateAsync(CreateOperationRequest request, CancellationToken cancellationToken = default);
    Task<OperationDto> UpdateAsync(Guid id, UpdateOperationRequest request, CancellationToken cancellationToken = default);
    Task<OperationResourceDto> AssignResourceAsync(Guid id, AssignOperationResourceRequest request, CancellationToken cancellationToken = default);
    Task<OperationDto> SubmitAsync(Guid id, uint rowVersion, CancellationToken cancellationToken = default);
    Task<OperationDto> ApproveAsync(Guid id, DecisionRequest request, CancellationToken cancellationToken = default);
    Task<OperationDto> ReturnAsync(Guid id, DecisionRequest request, CancellationToken cancellationToken = default);
    Task<OperationDto> RejectAsync(Guid id, DecisionRequest request, CancellationToken cancellationToken = default);
    Task<OperationApprovalStepDto> AddApprovalStepAsync(Guid id, AddApprovalStepRequest request, CancellationToken cancellationToken = default);
    Task<OperationApprovalConditionDto> VerifyConditionAsync(Guid conditionId, VerifyApprovalConditionRequest request, CancellationToken cancellationToken = default);
    Task<OperationRecurrenceRuleDto> SetRecurrenceAsync(Guid id, SetRecurrenceRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OperationDto>> GenerateRecurrenceOccurrencesAsync(Guid id, int count, CancellationToken cancellationToken = default);
    Task EnsureBeforeFlightConditionsMetAsync(Guid operationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkTaskDto>> ListTasksAsync(CancellationToken cancellationToken = default);
    Task<OperationMapCheckDto> RunMapCheckAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed class OperationService(
    IsSuasDbContext db,
    IAuthorizationPolicyService policy,
    ICurrentUser currentUser,
    IAuditWriter audit,
    IClock clock,
    IMaintenanceService maintenance,
    IDocumentService documents,
    IAirspaceMapAdapter airspace,
    IConfiguration configuration) : IOperationService
{
    public async Task<IReadOnlyList<OperationDto>> ListAsync(DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.OperationsRead);
        if (!policy.CanAccessBusinessData())
            throw new ForbiddenException("Systémový administrátor nemá automatický prístup k vecnému obsahu.");

        var query = db.Operations.AsNoTracking().Include(o => o.OrgUnit).Where(o => o.IsActive);
        if (from is not null) query = query.Where(o => o.PlannedEnd >= from);
        if (to is not null) query = query.Where(o => o.PlannedStart <= to);

        var items = await query.OrderBy(o => o.PlannedStart).ToListAsync(cancellationToken);
        return items.Where(o => policy.CanAccessOrgUnit(o.OrgUnit.Path)).Select(Map).ToList();
    }

    public async Task<OperationDetailDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.OperationsRead);
        var op = await LoadAsync(id, cancellationToken);
        policy.EnsureOrgAccess(op.OrgUnit.Path);
        return ToDetail(op);
    }

    public async Task<OperationDto> CreateAsync(CreateOperationRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.OperationsWrite);
        if (currentUser.AccountId is null)
            throw new ForbiddenException("Chýba identita používateľa.");

        if (request.IsUrgent && string.IsNullOrWhiteSpace(request.UrgentReason))
            throw new ValidationException("Neodkladná operácia vyžaduje povinný dôvod.");

        if (request.PlannedEnd <= request.PlannedStart)
            throw new ValidationException("Plánovaný koniec musí byť po začiatku.");

        var org = await db.OrgUnits.FirstOrDefaultAsync(o => o.Id == request.OrgUnitId && o.IsActive, cancellationToken)
            ?? throw new NotFoundException("Útvar nebol nájdený.");
        policy.EnsureOrgAccess(org.Path);

        var op = new Operation
        {
            Title = request.Title.Trim(),
            OperationTypeCode = request.OperationTypeCode,
            OrgUnitId = org.Id,
            ProposedByAccountId = currentUser.AccountId.Value,
            PlannedStart = request.PlannedStart,
            PlannedEnd = request.PlannedEnd,
            LocationAddress = request.LocationAddress,
            LocationLat = request.LocationLat,
            LocationLon = request.LocationLon,
            LocationGeoJson = request.LocationGeoJson,
            Description = request.Description,
            IsUrgent = request.IsUrgent,
            UrgentReason = request.UrgentReason,
            Status = OperationStatus.Draft,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.Operations.Add(op);
        await db.SaveChangesAsync(cancellationToken);
        op.OrgUnit = org;

        if (op.IsUrgent)
            await CreateUrgentFollowUpTaskAsync(op, cancellationToken);

        await audit.WriteAsync("Operation.Create", nameof(Operation), op.Id, null, Map(op), cancellationToken: cancellationToken);
        return Map(op);
    }

    public async Task<OperationDto> UpdateAsync(Guid id, UpdateOperationRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.OperationsWrite);
        var op = await LoadAsync(id, cancellationToken);
        policy.EnsureOrgAccess(op.OrgUnit.Path);
        EnsureEditable(op, request.RowVersion);

        if (request.PlannedEnd <= request.PlannedStart)
            throw new ValidationException("Plánovaný koniec musí byť po začiatku.");

        var before = Map(op);
        op.Title = request.Title.Trim();
        op.OperationTypeCode = request.OperationTypeCode;
        op.PlannedStart = request.PlannedStart;
        op.PlannedEnd = request.PlannedEnd;
        op.LocationAddress = request.LocationAddress;
        op.LocationLat = request.LocationLat;
        op.LocationLon = request.LocationLon;
        op.LocationGeoJson = request.LocationGeoJson;
        op.Description = request.Description;
        op.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("Operation.Update", nameof(Operation), op.Id, before, Map(op), cancellationToken: cancellationToken);
        return Map(op);
    }

    public async Task<OperationResourceDto> AssignResourceAsync(Guid id, AssignOperationResourceRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.OperationsWrite);
        var op = await LoadAsync(id, cancellationToken);
        policy.EnsureOrgAccess(op.OrgUnit.Path);
        if (op.IsSubmittedVersionLocked || op.Status is OperationStatus.Submitted or OperationStatus.Approved)
            throw new ValidationException("Schválenú/predloženú verziu nie je možné meniť priamo.");

        var (displayName, ok, error) = await ValidateResourceAsync(op, request, cancellationToken);
        if (!ok)
            throw new ValidationException(error!);

        var resource = new OperationResource
        {
            OperationId = op.Id,
            ResourceType = request.ResourceType,
            ResourceId = request.ResourceId,
            RoleCode = request.RoleCode,
            DisplayName = displayName,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.OperationResources.Add(resource);
        await db.SaveChangesAsync(cancellationToken);
        return new OperationResourceDto(resource.Id, resource.ResourceType, resource.ResourceId, resource.RoleCode, resource.DisplayName);
    }

    public async Task<OperationDto> SubmitAsync(Guid id, uint rowVersion, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.OperationsWrite);
        var op = await LoadAsync(id, cancellationToken);
        policy.EnsureOrgAccess(op.OrgUnit.Path);
        EnsureRowVersion(op, rowVersion);
        if (op.Status is not (OperationStatus.Draft or OperationStatus.Returned))
            throw new ValidationException("Predložiť možno iba koncept alebo vrátenú operáciu.");
        if (!op.Resources.Any(r => r.ResourceType == OperationResourceType.Uas && r.IsActive))
            throw new ValidationException("Operácia musí mať priradené UAS.");
        if (!op.Resources.Any(r => r.ResourceType == OperationResourceType.Pilot && r.IsActive))
            throw new ValidationException("Operácia musí mať priradeného pilota.");

        var docBlock = await documents.GetOperationDocumentBlockAsync(op.Id, cancellationToken);
        if (docBlock.IsBlocked)
            throw new ValidationException(docBlock.Reason);

        if (configuration.GetValue("Airspace:RequireMapCheckOnSubmit", false))
        {
            var mapCheck = await RunMapCheckInternalAsync(op, cancellationToken);
            if (mapCheck.IsBlocking)
                throw new ValidationException($"Mapová kontrola blokuje predloženie: {mapCheck.Summary}");
        }

        await EnsureApprovalRouteAsync(op, cancellationToken);
        return await TransitionAsync(op, OperationStatus.Submitted, "Predloženie na schválenie", lockVersion: true, cancellationToken);
    }

    public async Task<OperationMapCheckDto> RunMapCheckAsync(Guid id, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.OperationsWrite);
        var op = await LoadAsync(id, cancellationToken);
        policy.EnsureOrgAccess(op.OrgUnit.Path);
        return await RunMapCheckInternalAsync(op, cancellationToken);
    }

    private async Task<OperationMapCheckDto> RunMapCheckInternalAsync(Operation op, CancellationToken cancellationToken)
    {
        var result = await airspace.CheckAsync(new AirspaceMapCheckRequest(
            op.Id, op.LocationGeoJson, op.LocationLat, op.LocationLon, op.LocationAddress), cancellationToken);

        var entity = new OperationMapCheck
        {
            OperationId = op.Id,
            ProviderCode = result.ProviderCode,
            ResultCode = result.ResultCode switch
            {
                AirspaceMapCheckResultCode.Clear => "CLEAR",
                AirspaceMapCheckResultCode.Conflict => "CONFLICT",
                AirspaceMapCheckResultCode.InsufficientData => "INSUFFICIENT_DATA",
                _ => "ERROR"
            },
            IsBlocking = result.IsBlocking,
            Summary = result.Summary,
            DetailsJson = result.DetailsJson,
            CheckedAt = clock.UtcNow,
            CheckedByAccountId = currentUser.AccountId,
            RequestGeoJson = op.LocationGeoJson,
            ElapsedMs = result.ElapsedMs,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.OperationMapChecks.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("Operation.MapCheck", nameof(OperationMapCheck), entity.Id, null,
            new { entity.ResultCode, entity.IsBlocking, entity.ProviderCode }, cancellationToken: cancellationToken);
        return MapMapCheck(entity);
    }

    public async Task<OperationDto> ApproveAsync(Guid id, DecisionRequest request, CancellationToken cancellationToken = default)
    {
        var op = await LoadAsync(id, cancellationToken);
        policy.EnsureOrgAccess(op.OrgUnit.Path);
        policy.EnsureCanApprove(op.ProposedByAccountId, Permissions.OperationsApprove);
        EnsureRowVersion(op, request.RowVersion);
        if (op.Status != OperationStatus.Submitted)
            throw new ValidationException("Schváliť možno iba predloženú operáciu.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ValidationException("Dôvod rozhodnutia je povinný.");
        if (request.DecisionCode is not (OperationDecisionCode.Approved or OperationDecisionCode.ConditionallyApproved))
            throw new ValidationException("Schválenie vyžaduje kód Approved alebo ConditionallyApproved.");
        if (request.DecisionCode == OperationDecisionCode.ConditionallyApproved
            && (request.Conditions is null || request.Conditions.Count == 0))
            throw new ValidationException("Podmienené schválenie vyžaduje aspoň jednu podmienku.");

        await EnsureApprovalRouteAsync(op, cancellationToken);
        var step = GetCurrentPendingStep(op)
            ?? throw new ValidationException("Nie je k dispozícii žiadny čakajúci schvaľovací krok.");

        CompleteStep(step, request.DecisionCode, request.Reason, request.Conditions);
        op.DecidedAt = clock.UtcNow;
        op.DecidedByAccountId = currentUser.AccountId;
        op.DecisionReason = request.Reason;
        op.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);

        var remaining = op.ApprovalSteps.Where(s => s.IsActive && s.IsMandatory
            && s.Status is ApprovalStepStatus.Pending or ApprovalStepStatus.InProgress).Any();
        if (remaining)
        {
            await audit.WriteAsync("Operation.StepApproved", nameof(Operation), op.Id,
                null, new { step.SequenceNumber, request.DecisionCode }, cancellationToken: cancellationToken);
            return Map(op);
        }

        return await TransitionAsync(op, OperationStatus.Approved, request.Reason, lockVersion: true, cancellationToken);
    }

    public async Task<OperationDto> ReturnAsync(Guid id, DecisionRequest request, CancellationToken cancellationToken = default)
    {
        var op = await LoadAsync(id, cancellationToken);
        policy.EnsureOrgAccess(op.OrgUnit.Path);
        policy.EnsureCanApprove(op.ProposedByAccountId, Permissions.OperationsApprove);
        EnsureRowVersion(op, request.RowVersion);
        if (op.Status != OperationStatus.Submitted)
            throw new ValidationException("Vrátiť možno iba predloženú operáciu.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ValidationException("Dôvod vrátenia je povinný.");

        await EnsureApprovalRouteAsync(op, cancellationToken);
        var step = GetCurrentPendingStep(op);
        if (step is not null)
            CompleteStep(step, OperationDecisionCode.Returned, request.Reason, null);
        CancelAllSteps(op);

        op.IsSubmittedVersionLocked = false;
        op.DecidedAt = clock.UtcNow;
        op.DecidedByAccountId = currentUser.AccountId;
        op.DecisionReason = request.Reason;
        return await TransitionAsync(op, OperationStatus.Returned, request.Reason, lockVersion: false, cancellationToken);
    }

    public async Task<OperationDto> RejectAsync(Guid id, DecisionRequest request, CancellationToken cancellationToken = default)
    {
        var op = await LoadAsync(id, cancellationToken);
        policy.EnsureOrgAccess(op.OrgUnit.Path);
        policy.EnsureCanApprove(op.ProposedByAccountId, Permissions.OperationsApprove);
        EnsureRowVersion(op, request.RowVersion);
        if (op.Status != OperationStatus.Submitted)
            throw new ValidationException("Zamietnuť možno iba predloženú operáciu.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ValidationException("Dôvod zamietnutia je povinný.");

        await EnsureApprovalRouteAsync(op, cancellationToken);
        var step = GetCurrentPendingStep(op);
        if (step is not null)
            CompleteStep(step, OperationDecisionCode.Rejected, request.Reason, null);
        CancelAllSteps(op);

        op.DecidedAt = clock.UtcNow;
        op.DecidedByAccountId = currentUser.AccountId;
        op.DecisionReason = request.Reason;
        return await TransitionAsync(op, OperationStatus.Rejected, request.Reason, lockVersion: true, cancellationToken);
    }

    public async Task<OperationApprovalStepDto> AddApprovalStepAsync(Guid id, AddApprovalStepRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.OperationsWrite);
        var op = await LoadAsync(id, cancellationToken);
        policy.EnsureOrgAccess(op.OrgUnit.Path);
        if (op.Status is not (OperationStatus.Draft or OperationStatus.Returned))
            throw new ValidationException("Schvaľovacie kroky možno pridávať iba v koncepte alebo po vrátení.");
        if (request.SequenceNumber <= 0)
            throw new ValidationException("Poradie kroku musí byť kladné.");
        if (op.ApprovalSteps.Any(s => s.IsActive && s.SequenceNumber == request.SequenceNumber))
            throw new ConflictException("Krok s týmto poradím už existuje.");

        var step = new OperationApprovalStep
        {
            OperationId = op.Id,
            SequenceNumber = request.SequenceNumber,
            StepTypeCode = string.IsNullOrWhiteSpace(request.StepTypeCode) ? "APPROVAL" : request.StepTypeCode.Trim(),
            AssignedRoleCode = request.AssignedRoleCode?.Trim(),
            AssignedAccountId = request.AssignedAccountId,
            IsMandatory = request.IsMandatory,
            Status = ApprovalStepStatus.Pending,
            DueAt = request.DueAt,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.OperationApprovalSteps.Add(step);
        await db.SaveChangesAsync(cancellationToken);
        return MapStep(step);
    }

    public async Task<OperationApprovalConditionDto> VerifyConditionAsync(Guid conditionId, VerifyApprovalConditionRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.OperationsWrite);
        var condition = await db.OperationApprovalConditions
            .Include(c => c.Decision).ThenInclude(d => d.ApprovalStep).ThenInclude(s => s.Operation).ThenInclude(o => o.OrgUnit)
            .FirstOrDefaultAsync(c => c.Id == conditionId, cancellationToken)
            ?? throw new NotFoundException("Podmienka nebola nájdená.");
        policy.EnsureOrgAccess(condition.Decision.ApprovalStep.Operation.OrgUnit.Path);
        if (condition.RowVersion != request.RowVersion)
            throw new ConflictException("Záznam bol medzitým zmenený iným používateľom.");
        if (condition.Status != ApprovalConditionStatus.Unmet)
            throw new ValidationException("Overiť možno iba nesplnenú podmienku.");
        if (string.IsNullOrWhiteSpace(request.VerificationNote))
            throw new ValidationException("Poznámka k overeniu je povinná.");

        condition.Status = ApprovalConditionStatus.Met;
        condition.VerifiedAt = clock.UtcNow;
        condition.VerifiedByAccountId = currentUser.AccountId;
        condition.VerificationNote = request.VerificationNote.Trim();
        condition.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        return MapCondition(condition);
    }

    public async Task<OperationRecurrenceRuleDto> SetRecurrenceAsync(Guid id, SetRecurrenceRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.OperationsWrite);
        var op = await LoadAsync(id, cancellationToken);
        policy.EnsureOrgAccess(op.OrgUnit.Path);
        if (request.Interval <= 0)
            throw new ValidationException("Interval opakovania musí byť väčší ako nula.");

        var rule = await db.OperationRecurrenceRules.FirstOrDefaultAsync(r => r.OperationId == id, cancellationToken);
        if (rule is null)
        {
            rule = new OperationRecurrenceRule
            {
                OperationId = id,
                CreatedBy = currentUser.AccountId,
                UpdatedBy = currentUser.AccountId
            };
            db.OperationRecurrenceRules.Add(rule);
        }

        rule.RecurrenceType = request.RecurrenceType;
        rule.Interval = request.Interval;
        rule.DaysOfWeek = request.DaysOfWeek;
        rule.ValidFrom = request.ValidFrom;
        rule.ValidTo = request.ValidTo;
        rule.MaximumOccurrences = request.MaximumOccurrences;
        rule.NextOccurrenceAt = ComputeNextOccurrence(op.PlannedStart, request.RecurrenceType, request.Interval);
        rule.IsRuleActive = true;
        rule.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        return MapRecurrence(rule);
    }

    public async Task<IReadOnlyList<OperationDto>> GenerateRecurrenceOccurrencesAsync(Guid id, int count, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.OperationsWrite);
        if (count is < 1 or > 52)
            throw new ValidationException("Počet výskytov musí byť 1–52.");

        var source = await LoadAsync(id, cancellationToken);
        policy.EnsureOrgAccess(source.OrgUnit.Path);
        var rule = await db.OperationRecurrenceRules.FirstOrDefaultAsync(r => r.OperationId == id && r.IsActive && r.IsRuleActive, cancellationToken)
            ?? throw new NotFoundException("Pravidlo opakovania nebolo nájdené.");

        var created = new List<OperationDto>();
        var cursorStart = rule.NextOccurrenceAt ?? ComputeNextOccurrence(source.PlannedStart, rule.RecurrenceType, rule.Interval);
        var duration = source.PlannedEnd - source.PlannedStart;

        for (var i = 0; i < count; i++)
        {
            if (rule.MaximumOccurrences is int max && rule.GeneratedCount >= max)
                break;
            if (rule.ValidTo is DateTimeOffset until && cursorStart > until)
                break;

            var child = new Operation
            {
                Title = $"{source.Title} ({cursorStart:yyyy-MM-dd})",
                OperationTypeCode = source.OperationTypeCode,
                OrgUnitId = source.OrgUnitId,
                ProposedByAccountId = currentUser.AccountId ?? source.ProposedByAccountId,
                PlannedStart = cursorStart,
                PlannedEnd = cursorStart + duration,
                LocationAddress = source.LocationAddress,
                LocationLat = source.LocationLat,
                LocationLon = source.LocationLon,
                LocationGeoJson = source.LocationGeoJson,
                Description = source.Description,
                Status = OperationStatus.Draft,
                RecurrenceSourceOperationId = source.Id,
                CreatedBy = currentUser.AccountId,
                UpdatedBy = currentUser.AccountId
            };
            db.Operations.Add(child);
            await db.SaveChangesAsync(cancellationToken);

            foreach (var res in source.Resources.Where(r => r.IsActive))
            {
                db.OperationResources.Add(new OperationResource
                {
                    OperationId = child.Id,
                    ResourceType = res.ResourceType,
                    ResourceId = res.ResourceId,
                    RoleCode = res.RoleCode,
                    DisplayName = res.DisplayName,
                    CreatedBy = currentUser.AccountId,
                    UpdatedBy = currentUser.AccountId
                });
            }

            rule.GeneratedCount++;
            cursorStart = ComputeNextOccurrence(cursorStart, rule.RecurrenceType, rule.Interval);
            rule.NextOccurrenceAt = cursorStart;
            rule.UpdatedBy = currentUser.AccountId;
            await db.SaveChangesAsync(cancellationToken);

            child.OrgUnit = source.OrgUnit;
            created.Add(Map(child));
        }

        return created;
    }

    public async Task EnsureBeforeFlightConditionsMetAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        var unmet = await db.OperationApprovalConditions.AsNoTracking()
            .Include(c => c.Decision).ThenInclude(d => d.ApprovalStep)
            .Where(c => c.IsActive && c.MustBeMetBeforeFlight && c.Status == ApprovalConditionStatus.Unmet
                && c.Decision.ApprovalStep.OperationId == operationId)
            .Select(c => c.Description)
            .ToListAsync(cancellationToken);
        if (unmet.Count > 0)
            throw new ValidationException("Pred letom treba splniť podmienky schválenia: " + string.Join("; ", unmet));
    }

    public async Task<IReadOnlyList<WorkTaskDto>> ListTasksAsync(CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.OperationsRead);
        var items = await db.WorkTasks.AsNoTracking()
            .Where(t => t.IsActive)
            .OrderByDescending(t => t.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);
        return items.Select(t => new WorkTaskDto(t.Id, t.Title, t.Description, t.Status, t.RelatedOperationId, t.DueAt, t.TaskTypeCode, t.IsActive, t.RowVersion)).ToList();
    }

    private async Task CreateUrgentFollowUpTaskAsync(Operation op, CancellationToken cancellationToken)
    {
        var task = new WorkTask
        {
            Title = $"Doplniť údaje neodkladnej operácie: {op.Title}",
            Description = $"Povinné doplnenie po neodkladnej operácii. Dôvod: {op.UrgentReason}",
            Status = WorkTaskStatus.Open,
            OrgUnitId = op.OrgUnitId,
            AssigneeAccountId = op.ProposedByAccountId,
            RelatedOperationId = op.Id,
            DueAt = clock.UtcNow.AddDays(2),
            TaskTypeCode = "URGENT_OPERATION_FOLLOWUP",
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.WorkTasks.Add(task);
        await db.SaveChangesAsync(cancellationToken);
        db.WorkTaskHistories.Add(new Domain.Notifications.WorkTaskHistory
        {
            WorkTaskId = task.Id,
            FromStatus = null,
            ToStatus = WorkTaskStatus.Open,
            ChangedByAccountId = currentUser.AccountId,
            Note = "Vytvorenie úlohy",
            ChangedAt = clock.UtcNow,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<(string DisplayName, bool Ok, string? Error)> ValidateResourceAsync(
        Operation op,
        AssignOperationResourceRequest request,
        CancellationToken cancellationToken)
    {
        switch (request.ResourceType)
        {
            case OperationResourceType.Uas:
            {
                var uas = await db.UasAircraft.Include(u => u.OwningOrgUnit)
                    .FirstOrDefaultAsync(u => u.Id == request.ResourceId && u.IsActive, cancellationToken)
                    ?? throw new NotFoundException("UAS nebol nájdený.");
                if (!UasStateMachine.IsUsableInOperations(uas.Status))
                    return ("", false, $"UAS nie je použiteľné (stav {uas.Status}).");
                var block = await maintenance.GetUasBlockStatusAsync(uas.Id, cancellationToken);
                if (block.IsBlocked)
                    return ("", false, block.Reason);
                if (await HasResourceConflict(OperationResourceType.Uas, uas.Id, op, cancellationToken))
                    return ("", false, "UAS má konflikt v čase s inou operáciou.");
                return ($"{uas.RegistrationNumber ?? uas.SerialNumber}", true, null);
            }
            case OperationResourceType.Pilot:
            {
                var pilot = await db.Pilots.Include(p => p.Person).Include(p => p.Certificates).Include(p => p.Restrictions)
                    .FirstOrDefaultAsync(p => p.Id == request.ResourceId && p.IsActive, cancellationToken)
                    ?? throw new NotFoundException("Pilot nebol nájdený.");
                if (!PilotEligibility.CanAssignToOperation(pilot, op.PlannedStart))
                    return ("", false, "Pilot nie je spôsobilý na priradenie do operácie.");
                if (await HasResourceConflict(OperationResourceType.Pilot, pilot.Id, op, cancellationToken))
                    return ("", false, "Pilot má konflikt v čase s inou operáciou.");
                return ($"{pilot.Person.FirstName} {pilot.Person.LastName}", true, null);
            }
            case OperationResourceType.Battery:
            {
                var battery = await db.Batteries.FirstOrDefaultAsync(b => b.Id == request.ResourceId && b.IsActive, cancellationToken)
                    ?? throw new NotFoundException("Batéria nebola nájdená.");
                if (battery.Status is AssetUsabilityStatus.Damaged or AssetUsabilityStatus.Suspended or AssetUsabilityStatus.Decommissioned)
                    return ("", false, $"Batéria nie je použiteľná (stav {battery.Status}).");
                return (battery.InternalNumber, true, null);
            }
            case OperationResourceType.Component:
            {
                var component = await db.Components.FirstOrDefaultAsync(c => c.Id == request.ResourceId && c.IsActive, cancellationToken)
                    ?? throw new NotFoundException("Komponent nebol nájdený.");
                if (component.Status is AssetUsabilityStatus.Damaged or AssetUsabilityStatus.Suspended or AssetUsabilityStatus.Decommissioned)
                    return ("", false, $"Komponent nie je použiteľný (stav {component.Status}).");
                return (component.Name, true, null);
            }
            default:
                return ($"{request.ResourceType}:{request.ResourceId}", true, null);
        }
    }

    private async Task<bool> HasResourceConflict(
        OperationResourceType type,
        Guid resourceId,
        Operation op,
        CancellationToken cancellationToken)
    {
        var blocking = new[] { OperationStatus.Submitted, OperationStatus.Approved, OperationStatus.Draft, OperationStatus.Returned };
        return await db.OperationResources
            .Include(r => r.Operation)
            .AnyAsync(r =>
                r.IsActive
                && r.ResourceType == type
                && r.ResourceId == resourceId
                && r.OperationId != op.Id
                && r.Operation.IsActive
                && blocking.Contains(r.Operation.Status)
                && r.Operation.PlannedStart < op.PlannedEnd
                && r.Operation.PlannedEnd > op.PlannedStart, cancellationToken);
    }

    private async Task<OperationDto> TransitionAsync(
        Operation op,
        OperationStatus to,
        string reason,
        bool lockVersion,
        CancellationToken cancellationToken)
    {
        if (!OperationStateMachine.CanTransition(op.Status, to))
            throw new ValidationException($"Nepovolený prechod {op.Status} → {to}.");

        var before = Map(op);
        var from = op.Status;
        op.Status = to;
        op.UpdatedBy = currentUser.AccountId;
        if (to == OperationStatus.Submitted)
        {
            op.SubmittedAt = clock.UtcNow;
            op.IsSubmittedVersionLocked = true;
            op.Version++;
        }
        else if (lockVersion)
        {
            op.IsSubmittedVersionLocked = true;
        }

        db.OperationStatusHistories.Add(new OperationStatusHistory
        {
            OperationId = op.Id,
            FromStatus = from,
            ToStatus = to,
            Reason = reason,
            ChangedAt = clock.UtcNow,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        });

        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync($"Operation.{to}", nameof(Operation), op.Id, before, Map(op), cancellationToken: cancellationToken);
        return Map(op);
    }

    private async Task EnsureApprovalRouteAsync(Operation op, CancellationToken cancellationToken)
    {
        if (op.ApprovalSteps.Any(s => s.IsActive && s.Status is ApprovalStepStatus.Pending or ApprovalStepStatus.InProgress))
            return;

        var nextSeq = op.ApprovalSteps.Where(s => s.IsActive).Select(s => s.SequenceNumber).DefaultIfEmpty(0).Max() + 1;
        var step = new OperationApprovalStep
        {
            OperationId = op.Id,
            SequenceNumber = nextSeq <= 0 ? 1 : nextSeq,
            StepTypeCode = "APPROVAL",
            AssignedRoleCode = nameof(SystemRole.Approver),
            IsMandatory = true,
            Status = ApprovalStepStatus.Pending,
            AssignedAt = clock.UtcNow,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.OperationApprovalSteps.Add(step);
        await db.SaveChangesAsync(cancellationToken);
        op.ApprovalSteps.Add(step);
    }

    private static OperationApprovalStep? GetCurrentPendingStep(Operation op) =>
        op.ApprovalSteps
            .Where(s => s.IsActive && s.Status is ApprovalStepStatus.Pending or ApprovalStepStatus.InProgress)
            .OrderBy(s => s.SequenceNumber)
            .FirstOrDefault();

    private void CompleteStep(
        OperationApprovalStep step,
        OperationDecisionCode code,
        string reason,
        IReadOnlyList<CreateApprovalConditionRequest>? conditions)
    {
        step.Status = code is OperationDecisionCode.Approved or OperationDecisionCode.ConditionallyApproved
            ? ApprovalStepStatus.Approved
            : code == OperationDecisionCode.Returned ? ApprovalStepStatus.Cancelled : ApprovalStepStatus.Rejected;
        step.CompletedAt = clock.UtcNow;
        step.UpdatedBy = currentUser.AccountId;

        var decision = new OperationDecision
        {
            ApprovalStepId = step.Id,
            DecisionCode = code,
            DecidedAt = clock.UtcNow,
            DecidedByAccountId = currentUser.AccountId ?? Guid.Empty,
            Justification = reason.Trim(),
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.OperationDecisions.Add(decision);

        if (conditions is not null)
        {
            foreach (var c in conditions)
            {
                if (string.IsNullOrWhiteSpace(c.Description))
                    throw new ValidationException("Opis podmienky je povinný.");
                db.OperationApprovalConditions.Add(new OperationApprovalCondition
                {
                    Decision = decision,
                    ConditionTypeCode = string.IsNullOrWhiteSpace(c.ConditionTypeCode) ? "GENERAL" : c.ConditionTypeCode.Trim(),
                    Description = c.Description.Trim(),
                    MustBeMetBeforeFlight = c.MustBeMetBeforeFlight,
                    Status = ApprovalConditionStatus.Unmet,
                    DueAt = c.DueAt,
                    CreatedBy = currentUser.AccountId,
                    UpdatedBy = currentUser.AccountId
                });
            }
        }
    }

    private void CancelAllSteps(Operation op)
    {
        foreach (var s in op.ApprovalSteps.Where(s => s.IsActive
                     && s.Status is not (ApprovalStepStatus.Cancelled or ApprovalStepStatus.Rejected)))
        {
            if (s.Status is ApprovalStepStatus.Pending or ApprovalStepStatus.InProgress or ApprovalStepStatus.Approved)
            {
                s.Status = ApprovalStepStatus.Cancelled;
                s.UpdatedBy = currentUser.AccountId;
            }
        }
    }

    private static DateTimeOffset ComputeNextOccurrence(DateTimeOffset from, RecurrenceType type, int interval) =>
        type switch
        {
            RecurrenceType.Daily => from.AddDays(interval),
            RecurrenceType.Weekly => from.AddDays(7 * interval),
            RecurrenceType.Monthly => from.AddMonths(interval),
            _ => from.AddDays(interval)
        };

    private async Task<Operation> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Operations
            .Include(o => o.OrgUnit)
            .Include(o => o.Resources)
            .Include(o => o.StatusHistory)
            .Include(o => o.ApprovalSteps).ThenInclude(s => s.Decision)!.ThenInclude(d => d!.Conditions)
            .Include(o => o.RecurrenceRule)
            .Include(o => o.MapChecks)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
            ?? throw new NotFoundException("Operácia nebola nájdená.");

    private static void EnsureEditable(Operation op, uint rowVersion)
    {
        EnsureRowVersion(op, rowVersion);
        if (op.IsSubmittedVersionLocked || op.Status is OperationStatus.Submitted or OperationStatus.Approved or OperationStatus.Rejected)
            throw new ValidationException("Záznam je uzamknutý. Zmena je možná len riadeným procesom.");
    }

    private static void EnsureRowVersion(Operation op, uint rowVersion)
    {
        if (op.RowVersion != rowVersion)
            throw new ConflictException("Záznam bol medzitým zmenený iným používateľom.");
    }

    private static OperationDto Map(Operation o) =>
        new(o.Id, o.Title, o.OperationTypeCode, o.Status, o.OrgUnitId, o.OrgUnit.Name, o.ProposedByAccountId,
            o.PlannedStart, o.PlannedEnd, o.LocationAddress, o.LocationLat, o.LocationLon, o.IsUrgent, o.UrgentReason,
            o.Version, o.IsSubmittedVersionLocked, o.IsActive, o.RowVersion);

    private static OperationApprovalStepDto MapStep(OperationApprovalStep s) =>
        new(s.Id, s.SequenceNumber, s.StepTypeCode, s.AssignedRoleCode, s.AssignedAccountId, s.IsMandatory, s.Status, s.CompletedAt,
            s.Decision is null ? null : new OperationDecisionDto(s.Decision.Id, s.Decision.DecisionCode, s.Decision.DecidedAt, s.Decision.DecidedByAccountId, s.Decision.Justification));

    private static OperationApprovalConditionDto MapCondition(OperationApprovalCondition c) =>
        new(c.Id, c.DecisionId, c.ConditionTypeCode, c.Description, c.MustBeMetBeforeFlight, c.Status, c.DueAt, c.VerifiedAt, c.VerificationNote, c.RowVersion);

    private static OperationRecurrenceRuleDto MapRecurrence(OperationRecurrenceRule r) =>
        new(r.Id, r.OperationId, r.RecurrenceType, r.Interval, r.DaysOfWeek, r.ValidFrom, r.ValidTo, r.MaximumOccurrences,
            r.NextOccurrenceAt, r.GeneratedCount, r.IsRuleActive);

    private static OperationDetailDto ToDetail(Operation o)
    {
        var conditions = o.ApprovalSteps
            .Where(s => s.Decision?.Conditions is not null)
            .SelectMany(s => s.Decision!.Conditions)
            .Where(c => c.IsActive)
            .Select(MapCondition)
            .ToList();
        return new(Map(o), o.Description, o.LocationGeoJson, o.DecisionReason,
            o.Resources.Where(r => r.IsActive).Select(r => new OperationResourceDto(r.Id, r.ResourceType, r.ResourceId, r.RoleCode, r.DisplayName)).ToList(),
            o.StatusHistory.OrderByDescending(h => h.ChangedAt).Select(h => new OperationStatusHistoryDto(h.Id, h.FromStatus, h.ToStatus, h.Reason, h.ChangedAt)).ToList(),
            o.ApprovalSteps.Where(s => s.IsActive).OrderBy(s => s.SequenceNumber).Select(MapStep).ToList(),
            conditions,
            o.RecurrenceRule is null || !o.RecurrenceRule.IsActive ? null : MapRecurrence(o.RecurrenceRule),
            o.MapChecks.Where(m => m.IsActive).OrderByDescending(m => m.CheckedAt).Select(MapMapCheck).ToList());
    }

    private static OperationMapCheckDto MapMapCheck(OperationMapCheck m) =>
        new(m.Id, m.ProviderCode, m.ResultCode, m.IsBlocking, m.Summary, m.DetailsJson, m.CheckedAt,
            m.CheckedByAccountId, m.ElapsedMs, m.IsActive);
}
