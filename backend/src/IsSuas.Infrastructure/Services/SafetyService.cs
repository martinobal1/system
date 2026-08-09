using IsSuas.Application.Abstractions;
using IsSuas.Application.Policies;
using IsSuas.Application.Safety;
using IsSuas.Domain.Enums;
using IsSuas.Domain.Fleet;
using IsSuas.Domain.Safety;
using IsSuas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IsSuas.Infrastructure.Services;

public interface ISafetyService
{
    Task<IReadOnlyList<SafetyEventDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<SafetyEventDetailDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SafetyEventDto> CreateAsync(CreateSafetyEventRequest request, CancellationToken cancellationToken = default);
    Task<SafetyEventDto> SubmitAsync(Guid id, SubmitSafetyEventRequest request, CancellationToken cancellationToken = default);
    Task<SafetyEventDetailDto> UpdateInvestigationAsync(Guid id, UpdateInvestigationRequest request, CancellationToken cancellationToken = default);
    Task<SafetyEventDto> ChangeSeverityAsync(Guid id, ChangeSafetySeverityRequest request, CancellationToken cancellationToken = default);
    Task<SafetyEvidenceDto> AddEvidenceAsync(Guid id, AddEvidenceRequest request, CancellationToken cancellationToken = default);
    Task<CorrectiveActionDto> CreateActionAsync(Guid id, CreateCorrectiveActionRequest request, CancellationToken cancellationToken = default);
    Task<CorrectiveActionDto> CompleteActionAsync(Guid actionId, CompleteCorrectiveActionRequest request, CancellationToken cancellationToken = default);
    Task<CorrectiveActionDto> VerifyActionAsync(Guid actionId, VerifyCorrectiveActionRequest request, CancellationToken cancellationToken = default);
    Task<CorrectiveActionDto> WaiveActionAsync(Guid actionId, WaiveCorrectiveActionRequest request, CancellationToken cancellationToken = default);
    Task<SafetyEventParticipantDto> AddParticipantAsync(Guid id, CreateSafetyEventParticipantRequest request, CancellationToken cancellationToken = default);
    Task<SafetyEventAssetDto> AddAssetAsync(Guid id, CreateSafetyEventAssetRequest request, CancellationToken cancellationToken = default);
    Task<SafetyImmediateActionDto> AddImmediateActionAsync(Guid id, CreateSafetyImmediateActionRequest request, CancellationToken cancellationToken = default);
    Task<SafetyInvestigationDto> CreateInvestigationAsync(Guid id, CreateSafetyInvestigationRequest request, CancellationToken cancellationToken = default);
    Task<SafetyCauseDto> AddCauseAsync(Guid id, Guid investigationId, CreateSafetyCauseRequest request, CancellationToken cancellationToken = default);
    Task<SafetyReportingObligationDto> AddReportingObligationAsync(Guid id, CreateSafetyReportingObligationRequest request, CancellationToken cancellationToken = default);
    Task<SafetyReportingObligationDto> FulfillReportingObligationAsync(Guid id, Guid obligationId, FulfillSafetyReportingObligationRequest request, CancellationToken cancellationToken = default);
    Task<FleetSafetyAlertDto> CreateFleetAlertAsync(Guid id, CreateFleetSafetyAlertRequest request, CancellationToken cancellationToken = default);
    Task<SafetyEventDto> CloseAsync(Guid id, CloseSafetyEventRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AppNotificationDto>> ListNotificationsAsync(CancellationToken cancellationToken = default);
}

public sealed class SafetyService(
    IsSuasDbContext db,
    ICurrentUser currentUser,
    IAuthorizationPolicyService policy,
    IAuditWriter audit,
    IClock clock,
    INotificationPublisher notifications) : ISafetyService
{
    public async Task<IReadOnlyList<SafetyEventDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.SafetyRead);
        if (!policy.CanAccessBusinessData())
            throw new ForbiddenException("Systémový administrátor nemá automatický prístup k vecnému obsahu.");

        var items = await db.SafetyEvents.AsNoTracking()
            .Where(e => e.IsActive)
            .OrderByDescending(e => e.OccurredAt)
            .ToListAsync(cancellationToken);
        var orgPaths = await db.OrgUnits.AsNoTracking().ToDictionaryAsync(o => o.Id, o => o.Path, cancellationToken);
        return items
            .Where(e => e.OrgUnitId is null
                || (orgPaths.TryGetValue(e.OrgUnitId.Value, out var path) && policy.CanAccessOrgUnit(path)))
            .Select(MapEvent)
            .ToList();
    }

    public async Task<SafetyEventDetailDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.SafetyRead);
        var entity = await LoadDetailAsync(id, cancellationToken);
        await EnsureSafetyOrgAccessAsync(entity, cancellationToken);
        return MapDetail(entity);
    }

    public async Task<SafetyEventDto> CreateAsync(CreateSafetyEventRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.SafetyWrite);
        if (currentUser.AccountId is null)
            throw new ForbiddenException("Chýba identita.");
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ValidationException("Názov je povinný.");
        if (string.IsNullOrWhiteSpace(request.InitialReport))
            throw new ValidationException("Prvotné hlásenie je povinné (minimálne údaje).");
        if (request.OrgUnitId is Guid orgId)
        {
            var org = await db.OrgUnits.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orgId, cancellationToken)
                ?? throw new NotFoundException("Útvar nebol nájdený.");
            policy.EnsureOrgAccess(org.Path);
        }

        var entity = new SafetyEvent
        {
            Title = request.Title.Trim(),
            InitialReport = request.InitialReport.Trim(),
            InitialReportLocked = false,
            Severity = request.Severity,
            Status = SafetyEventStatus.Draft,
            ReportedByAccountId = currentUser.AccountId.Value,
            OrgUnitId = request.OrgUnitId,
            RelatedFlightId = request.RelatedFlightId,
            RelatedOperationId = request.RelatedOperationId,
            RelatedUasId = request.RelatedUasId,
            OccurredAt = request.OccurredAt == default ? clock.UtcNow : request.OccurredAt,
            LocationDescription = string.IsNullOrWhiteSpace(request.LocationDescription) ? null : request.LocationDescription.Trim(),
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.SafetyEvents.Add(entity);
        AddStatusHistory(entity, null, SafetyEventStatus.Draft, "Vytvorenie udalosti");
        AddClassificationHistory(entity, "SEVERITY", null, entity.Severity.ToString(), "Počiatočná klasifikácia");
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("SafetyEvent.Create", nameof(SafetyEvent), entity.Id, null, MapEvent(entity), cancellationToken: cancellationToken);
        return MapEvent(entity);
    }

    public async Task<SafetyEventDto> SubmitAsync(Guid id, SubmitSafetyEventRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.SafetyWrite);
        var entity = await db.SafetyEvents
            .Include(e => e.Evidences)
            .Include(e => e.CorrectiveActions)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new NotFoundException("Bezpečnostná udalosť nebola nájdená.");
        await EnsureSafetyOrgAccessAsync(entity, cancellationToken);
        EnsureRowVersion(entity, request.RowVersion);
        if (entity.Status != SafetyEventStatus.Draft)
            throw new ValidationException("Odoslať je možné iba koncept.");

        var before = MapEvent(entity);
        var from = entity.Status;
        entity.Status = SafetyEventStatus.Submitted;
        entity.InitialReportLocked = true;
        entity.UpdatedBy = currentUser.AccountId;
        AddStatusHistory(entity, from, entity.Status, "Odoslanie hlásenia");

        if (entity.Severity == SafetySeverity.Critical)
            await ApplyCriticalEffectsAsync(entity, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("SafetyEvent.Submit", nameof(SafetyEvent), entity.Id, before, MapEvent(entity), cancellationToken: cancellationToken);
        return MapEvent(entity);
    }

    public async Task<SafetyEventDetailDto> UpdateInvestigationAsync(Guid id, UpdateInvestigationRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.SafetyWrite);
        var entity = await LoadDetailAsync(id, cancellationToken, tracking: true);
        await EnsureSafetyOrgAccessAsync(entity, cancellationToken);
        EnsureRowVersion(entity, request.RowVersion);
        if (entity.Status is SafetyEventStatus.Draft or SafetyEventStatus.Closed or SafetyEventStatus.Cancelled)
            throw new ValidationException("Vyšetrovanie nie je možné v tomto stave.");
        if (!entity.InitialReportLocked)
            throw new ValidationException("Najprv odoslite hlásenie.");

        entity.Findings = request.Findings?.Trim();
        entity.Causes = request.Causes?.Trim();
        entity.ContributingFactors = request.ContributingFactors?.Trim();
        entity.Recommendations = request.Recommendations?.Trim();
        var from = entity.Status;
        entity.Status = entity.CorrectiveActions.Any(a => a.IsActive && a.Status != CorrectiveActionStatus.Verified && a.Status != CorrectiveActionStatus.Waived)
            ? SafetyEventStatus.ActionsInProgress
            : SafetyEventStatus.UnderInvestigation;
        entity.UpdatedBy = currentUser.AccountId;
        if (from != entity.Status)
            AddStatusHistory(entity, from, entity.Status, "Aktualizácia vyšetrovania");

        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("SafetyEvent.Investigate", nameof(SafetyEvent), entity.Id, null, MapEvent(entity), cancellationToken: cancellationToken);
        return MapDetail(entity);
    }

    public async Task<SafetyEventDto> ChangeSeverityAsync(Guid id, ChangeSafetySeverityRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.SafetyWrite);
        if (string.IsNullOrWhiteSpace(request.ChangeReason))
            throw new ValidationException("Dôvod zmeny klasifikácie je povinný.");

        var entity = await LoadDetailAsync(id, cancellationToken, tracking: true);
        await EnsureSafetyOrgAccessAsync(entity, cancellationToken);
        EnsureRowVersion(entity, request.RowVersion);
        if (entity.Status is SafetyEventStatus.Closed or SafetyEventStatus.Cancelled)
            throw new ValidationException("Klasifikáciu uzavretej udalosti nie je možné meniť.");

        var before = MapEvent(entity);
        var previous = entity.Severity;
        if (previous == request.Severity)
            return MapEvent(entity);

        entity.Severity = request.Severity;
        entity.UpdatedBy = currentUser.AccountId;
        AddClassificationHistory(entity, "SEVERITY", previous.ToString(), request.Severity.ToString(), request.ChangeReason.Trim());

        if (request.Severity == SafetySeverity.Critical && entity.Status != SafetyEventStatus.Draft)
            await ApplyCriticalEffectsAsync(entity, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("SafetyEvent.ChangeSeverity", nameof(SafetyEvent), entity.Id, before, MapEvent(entity), cancellationToken: cancellationToken);
        return MapEvent(entity);
    }

    public async Task<SafetyEvidenceDto> AddEvidenceAsync(Guid id, AddEvidenceRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.SafetyWrite);
        if (string.IsNullOrWhiteSpace(request.FileName) || string.IsNullOrWhiteSpace(request.ContentHash))
            throw new ValidationException("Súbor a kontrolný súčet sú povinné.");

        var entity = await db.SafetyEvents.Include(e => e.Evidences)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new NotFoundException("Bezpečnostná udalosť nebola nájdená.");
        if (entity.Status is SafetyEventStatus.Closed or SafetyEventStatus.Cancelled)
            throw new ValidationException("K uzavretej udalosti nie je možné pridať dôkaz.");
        if (request.IsSensitive)
            policy.EnsurePermission(Permissions.SafetySensitiveRead);

        var version = 1;
        if (request.SupersedesEvidenceId is Guid supersedesId)
        {
            var previous = entity.Evidences.FirstOrDefault(e => e.Id == supersedesId)
                ?? throw new NotFoundException("Nahrádzaný dôkaz nebol nájdený.");
            if (!previous.IsCurrent)
                throw new ValidationException("Nahradiť je možné iba aktuálnu verziu dôkazu.");
            previous.IsCurrent = false;
            previous.UpdatedBy = currentUser.AccountId;
            version = previous.Version + 1;
        }

        var evidence = new SafetyEvidence
        {
            SafetyEventId = entity.Id,
            FileName = request.FileName.Trim(),
            ContentHash = request.ContentHash.Trim().ToLowerInvariant(),
            ContentType = string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType.Trim(),
            Version = version,
            SupersedesEvidenceId = request.SupersedesEvidenceId,
            IsCurrent = true,
            Description = request.Description?.Trim(),
            IsSensitive = request.IsSensitive,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.SafetyEvidences.Add(evidence);
        entity.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("SafetyEvidence.Add", nameof(SafetyEvidence), evidence.Id, null, MapEvidence(evidence), cancellationToken: cancellationToken);
        return MapEvidence(evidence);
    }

    public async Task<CorrectiveActionDto> CreateActionAsync(Guid id, CreateCorrectiveActionRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.SafetyWrite);
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ValidationException("Názov opatrenia je povinný.");

        var entity = await db.SafetyEvents.Include(e => e.CorrectiveActions)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new NotFoundException("Bezpečnostná udalosť nebola nájdená.");
        if (entity.Status is SafetyEventStatus.Draft or SafetyEventStatus.Closed or SafetyEventStatus.Cancelled)
            throw new ValidationException("Opatrenie nie je možné vytvoriť v tomto stave.");

        var action = new CorrectiveAction
        {
            SafetyEventId = entity.Id,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            OwnerAccountId = request.OwnerAccountId,
            DueAt = request.DueAt,
            IsMandatory = request.IsMandatory,
            Status = CorrectiveActionStatus.Open,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.CorrectiveActions.Add(action);
        var from = entity.Status;
        entity.Status = SafetyEventStatus.ActionsInProgress;
        entity.UpdatedBy = currentUser.AccountId;
        if (from != entity.Status)
            AddStatusHistory(entity, from, entity.Status, "Vytvorenie nápravného opatrenia");
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("CorrectiveAction.Create", nameof(CorrectiveAction), action.Id, null, MapAction(action), cancellationToken: cancellationToken);
        return MapAction(action);
    }

    public async Task<CorrectiveActionDto> CompleteActionAsync(Guid actionId, CompleteCorrectiveActionRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.SafetyWrite);
        if (string.IsNullOrWhiteSpace(request.CompletionEvidence))
            throw new ValidationException("Dôkaz splnenia je povinný.");

        var action = await db.CorrectiveActions.FirstOrDefaultAsync(a => a.Id == actionId, cancellationToken)
            ?? throw new NotFoundException("Nápravné opatrenie nebolo nájdené.");
        EnsureActionRowVersion(action, request.RowVersion);
        if (action.Status != CorrectiveActionStatus.Open)
            throw new ValidationException("Splniť je možné iba otvorené opatrenie.");

        action.CompletionEvidence = request.CompletionEvidence.Trim();
        action.Status = CorrectiveActionStatus.Done;
        action.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("CorrectiveAction.Complete", nameof(CorrectiveAction), action.Id, null, MapAction(action), cancellationToken: cancellationToken);
        return MapAction(action);
    }

    public async Task<CorrectiveActionDto> VerifyActionAsync(Guid actionId, VerifyCorrectiveActionRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.SafetyWrite);
        if (currentUser.AccountId is null)
            throw new ForbiddenException("Chýba identita.");

        var action = await db.CorrectiveActions.FirstOrDefaultAsync(a => a.Id == actionId, cancellationToken)
            ?? throw new NotFoundException("Nápravné opatrenie nebolo nájdené.");
        EnsureActionRowVersion(action, request.RowVersion);
        if (action.Status != CorrectiveActionStatus.Done)
            throw new ValidationException("Overiť je možné iba splnené opatrenie.");
        if (action.OwnerAccountId == currentUser.AccountId.Value)
            throw new ValidationException("Overenie musí vykonať iná osoba než vlastník opatrenia.");

        action.Status = CorrectiveActionStatus.Verified;
        action.VerifiedByAccountId = currentUser.AccountId;
        action.VerifiedAt = clock.UtcNow;
        action.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("CorrectiveAction.Verify", nameof(CorrectiveAction), action.Id, null, MapAction(action), cancellationToken: cancellationToken);
        return MapAction(action);
    }

    public async Task<CorrectiveActionDto> WaiveActionAsync(Guid actionId, WaiveCorrectiveActionRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.SafetyWrite);
        if (string.IsNullOrWhiteSpace(request.WaiverReason))
            throw new ValidationException("Zdôvodnenie výnimky je povinné.");

        var action = await db.CorrectiveActions.FirstOrDefaultAsync(a => a.Id == actionId, cancellationToken)
            ?? throw new NotFoundException("Nápravné opatrenie nebolo nájdené.");
        EnsureActionRowVersion(action, request.RowVersion);
        if (action.Status is CorrectiveActionStatus.Verified or CorrectiveActionStatus.Waived)
            throw new ValidationException("Opatrenie je už uzavreté.");

        action.Status = CorrectiveActionStatus.Waived;
        action.WaiverReason = request.WaiverReason.Trim();
        action.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("CorrectiveAction.Waive", nameof(CorrectiveAction), action.Id, null, MapAction(action), cancellationToken: cancellationToken);
        return MapAction(action);
    }

    public async Task<SafetyEventParticipantDto> AddParticipantAsync(Guid id, CreateSafetyEventParticipantRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.SafetyWrite);
        var entity = await RequireOpenEventAsync(id, cancellationToken);
        _ = await db.Persons.FirstOrDefaultAsync(p => p.Id == request.PersonId && p.IsActive, cancellationToken)
            ?? throw new NotFoundException("Osoba nebola nájdená.");

        var participant = new SafetyEventParticipant
        {
            SafetyEventId = id,
            PersonId = request.PersonId,
            ParticipantRoleCode = request.ParticipantRoleCode.Trim(),
            InvolvementDescription = request.InvolvementDescription,
            InjuryStatusCode = request.InjuryStatusCode,
            StatementObtained = request.StatementObtained,
            NotifiedAt = request.NotifiedAt,
            Notes = request.Notes,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.SafetyEventParticipants.Add(participant);
        entity.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        return MapParticipant(participant);
    }

    public async Task<SafetyEventAssetDto> AddAssetAsync(Guid id, CreateSafetyEventAssetRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.SafetyWrite);
        var entity = await RequireOpenEventAsync(id, cancellationToken);

        var filled = (request.UasId is null ? 0 : 1) + (request.ComponentId is null ? 0 : 1) + (request.BatteryId is null ? 0 : 1);
        if (filled != 1)
            throw new ValidationException("Pri prostriedku musí byť vyplnený práve jeden z: UAS, komponent alebo batéria.");

        var asset = new SafetyEventAsset
        {
            SafetyEventId = id,
            UasId = request.UasId,
            ComponentId = request.ComponentId,
            BatteryId = request.BatteryId,
            AssetRoleCode = request.AssetRoleCode.Trim(),
            DamageStatusCode = request.DamageStatusCode.Trim(),
            DamageDescription = request.DamageDescription,
            TechnicalStatusAfterEvent = request.TechnicalStatusAfterEvent.Trim(),
            SecuredAsEvidence = request.SecuredAsEvidence,
            Notes = request.Notes,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.SafetyEventAssets.Add(asset);
        entity.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        return MapAsset(asset);
    }

    public async Task<SafetyImmediateActionDto> AddImmediateActionAsync(Guid id, CreateSafetyImmediateActionRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.SafetyWrite);
        var entity = await RequireOpenEventAsync(id, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.ActionDescription))
            throw new ValidationException("Opis opatrenia je povinný.");
        _ = await db.Persons.FirstOrDefaultAsync(p => p.Id == request.PerformedByPersonId && p.IsActive, cancellationToken)
            ?? throw new NotFoundException("Osoba vykonávateľa nebola nájdená.");

        var action = new SafetyImmediateAction
        {
            SafetyEventId = id,
            ActionTypeCode = request.ActionTypeCode.Trim(),
            ActionDescription = request.ActionDescription.Trim(),
            PerformedAt = request.PerformedAt == default ? clock.UtcNow : request.PerformedAt,
            PerformedByPersonId = request.PerformedByPersonId,
            ResultDescription = request.ResultDescription,
            FollowUpRequired = request.FollowUpRequired,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.SafetyImmediateActions.Add(action);
        entity.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        return MapImmediate(action);
    }

    public async Task<SafetyInvestigationDto> CreateInvestigationAsync(Guid id, CreateSafetyInvestigationRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.SafetyWrite);
        var entity = await RequireOpenEventAsync(id, cancellationToken);
        if (entity.Status == SafetyEventStatus.Draft)
            throw new ValidationException("Vyšetrovanie je možné až po odoslaní hlásenia.");
        if (string.IsNullOrWhiteSpace(request.InvestigationNumber) || string.IsNullOrWhiteSpace(request.ScopeDescription))
            throw new ValidationException("Číslo a rozsah preverovania sú povinné.");
        _ = await db.Persons.FirstOrDefaultAsync(p => p.Id == request.ResponsiblePersonId && p.IsActive, cancellationToken)
            ?? throw new NotFoundException("Zodpovedná osoba nebola nájdená.");
        if (await db.SafetyInvestigations.AnyAsync(i => i.InvestigationNumber == request.InvestigationNumber.Trim(), cancellationToken))
            throw new ConflictException("Číslo preverovania už existuje.");

        var investigation = new SafetyInvestigation
        {
            SafetyEventId = id,
            InvestigationNumber = request.InvestigationNumber.Trim(),
            InvestigationTypeCode = request.InvestigationTypeCode.Trim(),
            ResponsiblePersonId = request.ResponsiblePersonId,
            OpenedAt = clock.UtcNow,
            PlannedCompletionDate = request.PlannedCompletionDate,
            ScopeDescription = request.ScopeDescription.Trim(),
            Status = SafetyInvestigationStatus.InProgress,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.SafetyInvestigations.Add(investigation);

        var from = entity.Status;
        if (entity.Status is SafetyEventStatus.Submitted)
        {
            entity.Status = SafetyEventStatus.UnderInvestigation;
            AddStatusHistory(entity, from, entity.Status, "Otvorenie preverovania");
        }
        entity.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        return MapInvestigation(investigation);
    }

    public async Task<SafetyCauseDto> AddCauseAsync(Guid id, Guid investigationId, CreateSafetyCauseRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.SafetyWrite);
        var entity = await RequireOpenEventAsync(id, cancellationToken);
        var investigation = await db.SafetyInvestigations.Include(i => i.Causes)
            .FirstOrDefaultAsync(i => i.Id == investigationId && i.SafetyEventId == id, cancellationToken)
            ?? throw new NotFoundException("Preverovanie nebolo nájdené.");
        if (string.IsNullOrWhiteSpace(request.CauseDescription))
            throw new ValidationException("Opis príčiny je povinný.");

        var cause = new SafetyCause
        {
            InvestigationId = investigationId,
            CauseCategoryCode = request.CauseCategoryCode.Trim(),
            CauseDescription = request.CauseDescription.Trim(),
            IsRootCause = request.IsRootCause,
            Status = request.Status,
            ConfidenceLevelCode = request.ConfidenceLevelCode,
            SupportingEvidence = request.SupportingEvidence,
            ConfirmedAt = request.Status == SafetyCauseStatus.Confirmed ? clock.UtcNow : null,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.SafetyCauses.Add(cause);
        entity.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        return MapCause(cause);
    }

    public async Task<SafetyReportingObligationDto> AddReportingObligationAsync(
        Guid id, CreateSafetyReportingObligationRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.SafetyWrite);
        var entity = await RequireOpenEventAsync(id, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.RecipientAuthority))
            throw new ValidationException("Prijímajúci orgán je povinný.");

        var obligation = new SafetyReportingObligation
        {
            SafetyEventId = id,
            ObligationTypeCode = request.ObligationTypeCode.Trim(),
            RecipientAuthority = request.RecipientAuthority.Trim(),
            LegalBasis = request.LegalBasis,
            ReportingDeadline = request.ReportingDeadline,
            Status = SafetyReportingObligationStatus.Pending,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.SafetyReportingObligations.Add(obligation);
        entity.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        return MapObligation(obligation);
    }

    public async Task<SafetyReportingObligationDto> FulfillReportingObligationAsync(
        Guid id, Guid obligationId, FulfillSafetyReportingObligationRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.SafetyWrite);
        var entity = await RequireOpenEventAsync(id, cancellationToken);
        var obligation = await db.SafetyReportingObligations
            .FirstOrDefaultAsync(o => o.Id == obligationId && o.SafetyEventId == id, cancellationToken)
            ?? throw new NotFoundException("Oznamovacia povinnosť nebola nájdená.");
        if (obligation.Status is SafetyReportingObligationStatus.Fulfilled or SafetyReportingObligationStatus.Cancelled)
            throw new ValidationException("Povinnosť je už uzavretá.");

        var delayed = clock.UtcNow > obligation.ReportingDeadline;
        if (delayed && string.IsNullOrWhiteSpace(request.DelayReason))
            throw new ValidationException("Pri oneskorenom splnení je povinný dôvod oneskorenia.");

        obligation.ReportedAt = clock.UtcNow;
        obligation.ReportedByPersonId = request.ReportedByPersonId;
        obligation.ExternalReferenceNumber = request.ExternalReferenceNumber;
        obligation.DelayReason = string.IsNullOrWhiteSpace(request.DelayReason) ? null : request.DelayReason.Trim();
        // Po reportovaní je povinnosť splnená; oneskorenie ostáva v DelayReason.
        obligation.Status = SafetyReportingObligationStatus.Fulfilled;

        obligation.UpdatedBy = currentUser.AccountId;
        entity.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        return MapObligation(obligation);
    }

    public async Task<FleetSafetyAlertDto> CreateFleetAlertAsync(Guid id, CreateFleetSafetyAlertRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.SafetyWrite);
        if (currentUser.AccountId is null)
            throw new ForbiddenException("Chýba identita.");
        var entity = await RequireOpenEventAsync(id, cancellationToken);
        if (entity.Status == SafetyEventStatus.Draft)
            throw new ValidationException("Fleet alert je možné až po odoslaní hlásenia.");
        if (string.IsNullOrWhiteSpace(request.AlertNumber) || string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Message))
            throw new ValidationException("Číslo, názov a text upozornenia sú povinné.");
        if (await db.FleetSafetyAlerts.AnyAsync(a => a.AlertNumber == request.AlertNumber.Trim(), cancellationToken))
            throw new ConflictException("Číslo upozornenia už existuje.");

        var alert = new FleetSafetyAlert
        {
            SafetyEventId = id,
            AlertNumber = request.AlertNumber.Trim(),
            Title = request.Title.Trim(),
            Message = request.Message.Trim(),
            SeverityCode = request.SeverityCode.Trim(),
            ScopeCode = request.ScopeCode.Trim(),
            AffectedUasTypeCode = request.AffectedUasTypeCode,
            Status = FleetSafetyAlertStatus.Draft,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };

        if (request.PublishImmediately)
        {
            alert.Status = FleetSafetyAlertStatus.Published;
            alert.PublishedAt = clock.UtcNow;
            alert.ApprovedByAccountId = currentUser.AccountId;
        }

        db.FleetSafetyAlerts.Add(alert);
        entity.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);

        if (request.PublishImmediately)
        {
            foreach (var role in new[] { nameof(SystemRole.UnitAdministrator), nameof(SystemRole.CentralGestor) })
            {
                await notifications.PublishAsync(new Application.Notifications.PublishNotificationRequest(
                    null,
                    $"Fleet alert: {alert.Title}",
                    alert.Message,
                    "FLEET_SAFETY_ALERT",
                    alert.SeverityCode,
                    null,
                    role,
                    entity.Id,
                    nameof(FleetSafetyAlert),
                    alert.Id), cancellationToken);
            }
        }

        return MapFleetAlert(alert);
    }

    public async Task<SafetyEventDto> CloseAsync(Guid id, CloseSafetyEventRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.SafetyClose);
        if (currentUser.AccountId is null)
            throw new ForbiddenException("Chýba identita.");
        if (string.IsNullOrWhiteSpace(request.ClosureNotes))
            throw new ValidationException("Poznámka k uzatvoreniu je povinná.");

        var entity = await db.SafetyEvents
            .Include(e => e.CorrectiveActions)
            .Include(e => e.ReportingObligations)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new NotFoundException("Bezpečnostná udalosť nebola nájdená.");
        EnsureRowVersion(entity, request.RowVersion);
        if (entity.Status is SafetyEventStatus.Draft or SafetyEventStatus.Closed or SafetyEventStatus.Cancelled)
            throw new ValidationException("Udalosť nie je možné uzatvoriť v tomto stave.");

        var openMandatory = entity.CorrectiveActions
            .Where(a => a.IsActive && a.IsMandatory
                && a.Status is not CorrectiveActionStatus.Verified and not CorrectiveActionStatus.Waived)
            .ToList();
        var openObligations = entity.ReportingObligations
            .Where(o => o.IsActive && o.Status is SafetyReportingObligationStatus.Pending
                or SafetyReportingObligationStatus.Preparing)
            .ToList();
        if (openMandatory.Count > 0 || openObligations.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(request.ClosureExceptionReason))
                throw new ValidationException(
                    "Uzatvorenie vyžaduje splnené/overené povinné opatrenia a oznamovacie povinnosti, alebo zdôvodnenú výnimku.");
            entity.ClosureExceptionReason = request.ClosureExceptionReason.Trim();
        }

        var before = MapEvent(entity);
        var from = entity.Status;
        entity.Status = SafetyEventStatus.Closed;
        entity.ClosureNotes = request.ClosureNotes.Trim();
        entity.ClosedAt = clock.UtcNow;
        entity.ClosedByAccountId = currentUser.AccountId;
        entity.UpdatedBy = currentUser.AccountId;
        AddStatusHistory(entity, from, entity.Status, "Uzatvorenie udalosti");

        if (entity.AutoSuspendApplied && entity.RelatedUasId is Guid uasId)
            await TryRestoreUasAsync(uasId, entity.Id, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("SafetyEvent.Close", nameof(SafetyEvent), entity.Id, before, MapEvent(entity), cancellationToken: cancellationToken);
        return MapEvent(entity);
    }

    public async Task<IReadOnlyList<AppNotificationDto>> ListNotificationsAsync(CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.SafetyRead);
        var items = await db.AppNotifications.AsNoTracking()
            .Where(n => n.IsActive)
            .OrderByDescending(n => n.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);
        return items.Select(MapNotification).ToList();
    }

    private async Task ApplyCriticalEffectsAsync(SafetyEvent entity, CancellationToken cancellationToken)
    {
        const string typeCode = "SAFETY_CRITICAL";
        var roles = new[] { nameof(SystemRole.SafetyOfficer), nameof(SystemRole.CentralGestor), nameof(SystemRole.UnitAdministrator) };
        foreach (var role in roles)
        {
            var exists = await db.AppNotifications.AnyAsync(n =>
                n.TypeCode == typeCode && n.RelatedSafetyEventId == entity.Id && n.RecipientRoleCode == role, cancellationToken);
            if (exists)
                continue;

            await notifications.PublishAsync(new Application.Notifications.PublishNotificationRequest(
                null,
                "Kritická bezpečnostná udalosť",
                $"Kritická udalosť: {entity.Title}. Vyžaduje okamžité opatrenia.",
                typeCode,
                "Critical",
                null,
                role,
                entity.Id,
                nameof(SafetyEvent),
                entity.Id), cancellationToken);
        }

        if (entity.RelatedUasId is Guid uasId)
        {
            var uas = await db.UasAircraft.FirstOrDefaultAsync(u => u.Id == uasId, cancellationToken);
            if (uas is not null && uas.Status is UasLifecycleStatus.InService or UasLifecycleStatus.Registered
                or UasLifecycleStatus.InStorage or UasLifecycleStatus.InMaintenance)
            {
                var from = uas.Status;
                uas.Status = UasLifecycleStatus.Suspended;
                uas.UpdatedBy = currentUser.AccountId;
                db.UasStatusHistories.Add(new UasStatusHistory
                {
                    UasId = uas.Id,
                    FromStatus = from,
                    ToStatus = UasLifecycleStatus.Suspended,
                    ChangedAt = clock.UtcNow,
                    Reason = $"Automatické pozastavenie po kritickej udalosti {entity.Id}",
                    CreatedBy = currentUser.AccountId,
                    UpdatedBy = currentUser.AccountId
                });
                entity.AutoSuspendApplied = true;
            }
        }
    }

    private async Task TryRestoreUasAsync(Guid uasId, Guid closingEventId, CancellationToken cancellationToken)
    {
        var otherCritical = await db.SafetyEvents.AnyAsync(e =>
            e.Id != closingEventId && e.RelatedUasId == uasId && e.IsActive
            && e.Severity == SafetySeverity.Critical
            && e.Status != SafetyEventStatus.Closed && e.Status != SafetyEventStatus.Cancelled
            && e.AutoSuspendApplied, cancellationToken);
        if (otherCritical)
            return;

        var blockingDefect = await db.Defects.AnyAsync(d =>
            d.RelatedUasId == uasId && d.IsActive && d.BlocksOperations
            && d.Status != DefectStatus.Resolved && d.Status != DefectStatus.Closed, cancellationToken);
        if (blockingDefect)
            return;

        var uas = await db.UasAircraft.FirstOrDefaultAsync(u => u.Id == uasId, cancellationToken);
        if (uas is null || uas.Status != UasLifecycleStatus.Suspended)
            return;

        var from = uas.Status;
        uas.Status = UasLifecycleStatus.InService;
        uas.UpdatedBy = currentUser.AccountId;
        db.UasStatusHistories.Add(new UasStatusHistory
        {
            UasId = uas.Id,
            FromStatus = from,
            ToStatus = UasLifecycleStatus.InService,
            ChangedAt = clock.UtcNow,
            Reason = "Obnova po uzatvorení kritickej bezpečnostnej udalosti",
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        });
    }

    private async Task<SafetyEvent> RequireOpenEventAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await db.SafetyEvents.FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new NotFoundException("Bezpečnostná udalosť nebola nájdená.");
        await EnsureSafetyOrgAccessAsync(entity, cancellationToken);
        if (entity.Status is SafetyEventStatus.Closed or SafetyEventStatus.Cancelled)
            throw new ValidationException("Udalosť je uzavretá.");
        return entity;
    }

    private void AddStatusHistory(SafetyEvent entity, SafetyEventStatus? from, SafetyEventStatus to, string reason)
    {
        if (currentUser.AccountId is null)
            return;
        db.SafetyStatusHistories.Add(new SafetyStatusHistory
        {
            SafetyEventId = entity.Id,
            PreviousStatus = from,
            NewStatus = to,
            EffectiveAt = clock.UtcNow,
            Reason = reason,
            ChangedByAccountId = currentUser.AccountId.Value,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        });
    }

    private void AddClassificationHistory(SafetyEvent entity, string type, string? previous, string next, string reason)
    {
        if (currentUser.AccountId is null)
            return;
        db.SafetyClassificationHistories.Add(new SafetyClassificationHistory
        {
            SafetyEventId = entity.Id,
            ClassificationTypeCode = type,
            PreviousValueCode = previous,
            NewValueCode = next,
            EffectiveAt = clock.UtcNow,
            ChangeReason = reason,
            ChangedByAccountId = currentUser.AccountId.Value,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        });
    }

    private async Task EnsureSafetyOrgAccessAsync(SafetyEvent entity, CancellationToken cancellationToken)
    {
        if (entity.OrgUnitId is null)
            return;
        var org = await db.OrgUnits.AsNoTracking().FirstOrDefaultAsync(o => o.Id == entity.OrgUnitId, cancellationToken)
            ?? throw new NotFoundException("Útvar nebol nájdený.");
        policy.EnsureOrgAccess(org.Path);
    }

    private async Task<SafetyEvent> LoadDetailAsync(Guid id, CancellationToken cancellationToken, bool tracking = false)
    {
        IQueryable<SafetyEvent> query = db.SafetyEvents
            .Include(e => e.Evidences)
            .Include(e => e.CorrectiveActions)
            .Include(e => e.Participants)
            .Include(e => e.Assets)
            .Include(e => e.ImmediateActions)
            .Include(e => e.Investigations).ThenInclude(i => i.Causes)
            .Include(e => e.ReportingObligations)
            .Include(e => e.FleetAlerts)
            .Include(e => e.StatusHistory)
            .Include(e => e.ClassificationHistory);
        if (!tracking)
            query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new NotFoundException("Bezpečnostná udalosť nebola nájdená.");
    }

    private static void EnsureRowVersion(SafetyEvent entity, uint rowVersion)
    {
        if (entity.RowVersion != rowVersion)
            throw new ConflictException("Záznam bol medzitým zmenený iným používateľom.");
    }

    private static void EnsureActionRowVersion(CorrectiveAction action, uint rowVersion)
    {
        if (action.RowVersion != rowVersion)
            throw new ConflictException("Záznam bol medzitým zmenený iným používateľom.");
    }

    private SafetyEventDetailDto MapDetail(SafetyEvent e)
    {
        var evidences = e.Evidences
            .Where(x => x.IsActive)
            .Where(x => !x.IsSensitive || policy.HasPermission(Permissions.SafetySensitiveRead))
            .OrderByDescending(x => x.Version)
            .Select(MapEvidence)
            .ToList();
        var actions = e.CorrectiveActions.Where(x => x.IsActive).OrderBy(x => x.DueAt).Select(MapAction).ToList();
        return new SafetyEventDetailDto(
            MapEvent(e), e.LocationDescription, e.Findings, e.Causes, e.ContributingFactors, e.Recommendations,
            e.ClosureNotes, e.ClosureExceptionReason, evidences, actions,
            e.Participants.Where(x => x.IsActive).Select(MapParticipant).ToList(),
            e.Assets.Where(x => x.IsActive).Select(MapAsset).ToList(),
            e.ImmediateActions.Where(x => x.IsActive).OrderByDescending(x => x.PerformedAt).Select(MapImmediate).ToList(),
            e.Investigations.Where(x => x.IsActive).OrderByDescending(x => x.OpenedAt).Select(MapInvestigation).ToList(),
            e.ReportingObligations.Where(x => x.IsActive).Select(MapObligation).ToList(),
            e.FleetAlerts.Where(x => x.IsActive).OrderByDescending(x => x.CreatedAt).Select(MapFleetAlert).ToList(),
            e.StatusHistory.OrderByDescending(x => x.EffectiveAt).Select(MapStatusHistory).ToList(),
            e.ClassificationHistory.OrderByDescending(x => x.EffectiveAt).Select(MapClassification).ToList());
    }

    private static SafetyEventDto MapEvent(SafetyEvent e) =>
        new(e.Id, e.Title, e.InitialReport, e.InitialReportLocked, e.Severity, e.Status, e.ReportedByAccountId,
            e.RelatedUasId, e.RelatedFlightId, e.RelatedOperationId, e.OccurredAt, e.AutoSuspendApplied, e.IsActive, e.RowVersion);

    private static SafetyEvidenceDto MapEvidence(SafetyEvidence e) =>
        new(e.Id, e.FileName, e.ContentHash, e.ContentType, e.Version, e.IsCurrent, e.Description, e.IsSensitive);

    private static CorrectiveActionDto MapAction(CorrectiveAction a) =>
        new(a.Id, a.Title, a.Description, a.OwnerAccountId, a.DueAt, a.Status, a.CompletionEvidence,
            a.VerifiedByAccountId, a.IsMandatory, a.RowVersion);

    private static SafetyEventParticipantDto MapParticipant(SafetyEventParticipant p) =>
        new(p.Id, p.PersonId, p.ParticipantRoleCode, p.InvolvementDescription, p.InjuryStatusCode,
            p.StatementObtained, p.NotifiedAt, p.Notes, p.IsActive);

    private static SafetyEventAssetDto MapAsset(SafetyEventAsset a) =>
        new(a.Id, a.UasId, a.ComponentId, a.BatteryId, a.AssetRoleCode, a.DamageStatusCode, a.DamageDescription,
            a.TechnicalStatusAfterEvent, a.SecuredAsEvidence, a.Notes, a.IsActive);

    private static SafetyImmediateActionDto MapImmediate(SafetyImmediateAction a) =>
        new(a.Id, a.ActionTypeCode, a.ActionDescription, a.PerformedAt, a.PerformedByPersonId,
            a.ResultDescription, a.FollowUpRequired, a.IsActive);

    private static SafetyInvestigationDto MapInvestigation(SafetyInvestigation i) =>
        new(i.Id, i.InvestigationNumber, i.InvestigationTypeCode, i.ResponsiblePersonId, i.OpenedAt,
            i.PlannedCompletionDate, i.ClosedAt, i.ScopeDescription, i.Status, i.FindingsSummary, i.Conclusion,
            i.InterruptionReason, i.IsActive,
            i.Causes.Where(c => c.IsActive).Select(MapCause).ToList());

    private static SafetyCauseDto MapCause(SafetyCause c) =>
        new(c.Id, c.InvestigationId, c.CauseCategoryCode, c.CauseDescription, c.IsRootCause, c.Status,
            c.ConfidenceLevelCode, c.SupportingEvidence, c.ConfirmedAt, c.ConfirmedByPersonId, c.IsActive);

    private static SafetyReportingObligationDto MapObligation(SafetyReportingObligation o) =>
        new(o.Id, o.ObligationTypeCode, o.RecipientAuthority, o.LegalBasis, o.ReportingDeadline, o.Status,
            o.ReportedAt, o.ReportedByPersonId, o.ExternalReferenceNumber, o.DelayReason, o.CancellationReason, o.IsActive);

    private static FleetSafetyAlertDto MapFleetAlert(FleetSafetyAlert a) =>
        new(a.Id, a.AlertNumber, a.Title, a.Message, a.SeverityCode, a.ScopeCode, a.AffectedUasTypeCode,
            a.Status, a.PublishedAt, a.ApprovedByAccountId, a.WithdrawalReason, a.IsActive);

    private static SafetyStatusHistoryDto MapStatusHistory(SafetyStatusHistory h) =>
        new(h.Id, h.PreviousStatus, h.NewStatus, h.EffectiveAt, h.Reason);

    private static SafetyClassificationHistoryDto MapClassification(SafetyClassificationHistory h) =>
        new(h.Id, h.ClassificationTypeCode, h.PreviousValueCode, h.NewValueCode, h.EffectiveAt, h.ChangeReason,
            h.ChangedByAccountId);

    private static AppNotificationDto MapNotification(AppNotification n) =>
        new(n.Id, n.Title, n.Message, n.TypeCode, n.Priority, n.RelatedSafetyEventId, n.Status, n.CreatedAt);
}
