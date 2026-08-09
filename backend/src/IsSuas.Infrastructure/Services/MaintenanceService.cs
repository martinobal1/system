using IsSuas.Application.Abstractions;
using IsSuas.Application.Maintenance;
using IsSuas.Application.Policies;
using IsSuas.Domain.Enums;
using IsSuas.Domain.Fleet;
using IsSuas.Domain.Maintenance;
using IsSuas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IsSuas.Infrastructure.Services;

public interface IMaintenanceService
{
    Task<IReadOnlyList<MaintenancePlanDto>> ListPlansAsync(CancellationToken cancellationToken = default);
    Task<MaintenancePlanDto> CreatePlanAsync(CreateMaintenancePlanRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkOrderDto>> ListWorkOrdersAsync(CancellationToken cancellationToken = default);
    Task<WorkOrderDto> CreateWorkOrderAsync(CreateWorkOrderRequest request, CancellationToken cancellationToken = default);
    Task<MaintenanceRecordDto> CompleteWorkOrderAsync(Guid workOrderId, CompleteWorkOrderRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DefectDto>> ListDefectsAsync(CancellationToken cancellationToken = default);
    Task<DefectDto> ReportDefectAsync(ReportDefectRequest request, CancellationToken cancellationToken = default);
    Task<DefectDto> ResolveDefectAsync(Guid defectId, ResolveDefectRequest request, CancellationToken cancellationToken = default);
    Task RecalculateAfterFlightAsync(Guid? uasId, Guid? batteryId, int flightMinutes, CancellationToken cancellationToken = default);
    Task<AssetBlockCheckDto> GetUasBlockStatusAsync(Guid uasId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ServiceRequestDto>> ListServiceRequestsAsync(CancellationToken cancellationToken = default);
    Task<ServiceRequestDto> CreateServiceRequestAsync(CreateServiceRequestRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TechnicalRestrictionDto>> ListRestrictionsAsync(CancellationToken cancellationToken = default);
    Task<TechnicalRestrictionDto> CreateRestrictionAsync(CreateTechnicalRestrictionRequest request, CancellationToken cancellationToken = default);
    Task<TechnicalRestrictionDto> EndRestrictionAsync(Guid id, EndTechnicalRestrictionRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReleaseToServiceDto>> ListReleasesAsync(CancellationToken cancellationToken = default);
    Task<ReleaseToServiceDto> CreateReleaseAsync(CreateReleaseToServiceRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TechnicalStatusHistoryDto>> ListTechnicalStatusHistoryAsync(MaintenanceTargetType targetType, Guid targetId, CancellationToken cancellationToken = default);
}

public sealed class MaintenanceService(
    IsSuasDbContext db,
    IAuthorizationPolicyService policy,
    ICurrentUser currentUser,
    IAuditWriter audit,
    IClock clock) : IMaintenanceService
{
    public async Task<IReadOnlyList<MaintenancePlanDto>> ListPlansAsync(CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.MaintenanceRead);
        if (!policy.CanAccessBusinessData())
            throw new ForbiddenException("Systémový administrátor nemá automatický prístup k vecnému obsahu.");

        var items = await db.MaintenancePlans.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.NameSk)
            .ToListAsync(cancellationToken);
        var accessible = await AccessibleTargetIdsAsync(cancellationToken);
        return items.Where(p => IsTargetAccessible(p.TargetType, p.TargetId, accessible)).Select(MapPlan).ToList();
    }

    public async Task<MaintenancePlanDto> CreatePlanAsync(CreateMaintenancePlanRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.MaintenanceWrite);
        await EnsureTargetAccessAsync(request.TargetType, request.TargetId, cancellationToken);
        var plan = new MaintenancePlan
        {
            NameSk = request.NameSk.Trim(),
            TargetType = request.TargetType,
            TargetId = request.TargetId,
            TargetTypeCode = request.TargetTypeCode,
            TriggerType = request.TriggerType,
            IntervalValue = request.IntervalValue,
            NextDueAt = request.NextDueAt,
            NextDueCounter = request.NextDueCounter,
            WarningThresholdPercent = request.WarningThresholdPercent <= 0 ? 80 : request.WarningThresholdPercent,
            BlockWhenOverdue = request.BlockWhenOverdue,
            Status = MaintenancePlanStatus.Active,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.MaintenancePlans.Add(plan);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("MaintenancePlan.Create", nameof(MaintenancePlan), plan.Id, null, MapPlan(plan), cancellationToken: cancellationToken);
        return MapPlan(plan);
    }

    public async Task<IReadOnlyList<WorkOrderDto>> ListWorkOrdersAsync(CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.MaintenanceRead);
        if (!policy.CanAccessBusinessData())
            throw new ForbiddenException("Systémový administrátor nemá automatický prístup k vecnému obsahu.");

        var items = await db.WorkOrders.AsNoTracking()
            .Where(w => w.IsActive)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(cancellationToken);
        var accessible = await AccessibleTargetIdsAsync(cancellationToken);
        return items.Where(w => IsTargetAccessible(w.TargetType, w.TargetId, accessible)).Select(MapWorkOrder).ToList();
    }

    public async Task<WorkOrderDto> CreateWorkOrderAsync(CreateWorkOrderRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.MaintenanceWrite);
        await EnsureTargetAccessAsync(request.TargetType, request.TargetId, cancellationToken);
        var wo = new WorkOrder
        {
            MaintenancePlanId = request.MaintenancePlanId,
            ServiceRequestId = request.ServiceRequestId,
            TargetType = request.TargetType,
            TargetId = request.TargetId,
            Title = request.Title.Trim(),
            Description = request.Description,
            AssignedTechnicianAccountId = request.AssignedTechnicianAccountId,
            DueAt = request.DueAt,
            Status = WorkOrderStatus.Open,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        if (request.ServiceRequestId is Guid srId)
        {
            var sr = await db.ServiceRequests.FirstOrDefaultAsync(s => s.Id == srId && s.IsActive, cancellationToken)
                ?? throw new NotFoundException("Servisná požiadavka nebola nájdená.");
            if (sr.Status is ServiceRequestStatus.Rejected or ServiceRequestStatus.Cancelled)
                throw new ValidationException("Na zamietnutú/zrušenú požiadavku nemožno vytvoriť príkaz.");
            if (sr.Status == ServiceRequestStatus.New)
                sr.Status = ServiceRequestStatus.Approved;
        }
        db.WorkOrders.Add(wo);
        await db.SaveChangesAsync(cancellationToken);
        return MapWorkOrder(wo);
    }

    public async Task<MaintenanceRecordDto> CompleteWorkOrderAsync(Guid workOrderId, CompleteWorkOrderRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.MaintenanceWrite);
        if (currentUser.AccountId is null)
            throw new ForbiddenException("Chýba identita.");

        var wo = await db.WorkOrders.Include(w => w.MaintenancePlan)
            .FirstOrDefaultAsync(w => w.Id == workOrderId, cancellationToken)
            ?? throw new NotFoundException("Pracovný príkaz nebol nájdený.");
        if (wo.RowVersion != request.RowVersion)
            throw new ConflictException("Záznam bol medzitým zmenený iným používateľom.");
        if (wo.Status == WorkOrderStatus.Completed)
            throw new ValidationException("Pracovný príkaz je už dokončený.");

        wo.Status = WorkOrderStatus.Completed;
        wo.UpdatedBy = currentUser.AccountId;

        var record = new MaintenanceRecord
        {
            WorkOrderId = wo.Id,
            WorkScope = request.WorkScope.Trim(),
            Result = request.Result.Trim(),
            PerformedAt = request.PerformedAt,
            PerformedByAccountId = currentUser.AccountId.Value,
            PartsUsed = request.PartsUsed,
            DocumentationNotes = request.DocumentationNotes,
            AirworthinessRestored = request.AirworthinessRestored,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.MaintenanceRecords.Add(record);

        if (wo.MaintenancePlan is not null)
        {
            ResetPlanAfterMaintenance(wo.MaintenancePlan);
            wo.MaintenancePlan.UpdatedBy = currentUser.AccountId;
        }

        if (request.AirworthinessRestored && wo.TargetType == MaintenanceTargetType.Uas)
        {
            var uas = await db.UasAircraft.FirstOrDefaultAsync(u => u.Id == wo.TargetId, cancellationToken);
            if (uas is not null && uas.Status == UasLifecycleStatus.InMaintenance)
            {
                uas.Status = UasLifecycleStatus.InService;
                uas.UpdatedBy = currentUser.AccountId;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("WorkOrder.Complete", nameof(WorkOrder), wo.Id, null,
            new MaintenanceRecordDto(record.Id, record.WorkOrderId, record.WorkScope, record.Result, record.PerformedAt,
                record.PerformedByAccountId, record.PartsUsed, record.AirworthinessRestored),
            cancellationToken: cancellationToken);

        return new MaintenanceRecordDto(record.Id, record.WorkOrderId, record.WorkScope, record.Result, record.PerformedAt,
            record.PerformedByAccountId, record.PartsUsed, record.AirworthinessRestored);
    }

    public async Task<IReadOnlyList<DefectDto>> ListDefectsAsync(CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.MaintenanceRead);
        if (!policy.CanAccessBusinessData())
            throw new ForbiddenException("Systémový administrátor nemá automatický prístup k vecnému obsahu.");

        var items = await db.Defects.AsNoTracking()
            .Where(d => d.IsActive)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);
        var accessible = await AccessibleTargetIdsAsync(cancellationToken);
        return items.Where(d =>
        {
            var targetId = d.RelatedUasId ?? (d.TargetType == MaintenanceTargetType.Uas ? d.TargetId : null);
            return targetId is null
                || IsTargetAccessible(MaintenanceTargetType.Uas, targetId, accessible)
                || IsTargetAccessible(d.TargetType, d.TargetId, accessible);
        }).Select(MapDefect).ToList();
    }

    public async Task<DefectDto> ReportDefectAsync(ReportDefectRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.MaintenanceWrite);
        if (currentUser.AccountId is null)
            throw new ForbiddenException("Chýba identita.");
        if (string.IsNullOrWhiteSpace(request.OriginalReport))
            throw new ValidationException("Pôvodné hlásenie je povinné.");
        await EnsureTargetAccessAsync(request.TargetType, request.TargetId, cancellationToken);

        var blocks = request.BlocksOperations || request.Severity == DefectSeverity.Blocking;
        var defect = new Defect
        {
            TargetType = request.TargetType,
            TargetId = request.TargetId,
            Title = request.Title.Trim(),
            OriginalReport = request.OriginalReport.Trim(),
            OriginalReportLocked = true,
            Severity = request.Severity,
            Status = DefectStatus.Reported,
            BlocksOperations = blocks,
            TemporaryMeasure = request.TemporaryMeasure,
            ReportedByAccountId = currentUser.AccountId.Value,
            RelatedFlightId = request.RelatedFlightId,
            RelatedUasId = request.RelatedUasId ?? (request.TargetType == MaintenanceTargetType.Uas ? request.TargetId : null),
            DefectTypeCode = string.IsNullOrWhiteSpace(request.DefectTypeCode) ? "GENERAL" : request.DefectTypeCode.Trim(),
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.Defects.Add(defect);

        if (blocks && defect.RelatedUasId is not null)
        {
            var uas = await db.UasAircraft.FirstOrDefaultAsync(u => u.Id == defect.RelatedUasId, cancellationToken);
            if (uas is not null && uas.Status is UasLifecycleStatus.InService or UasLifecycleStatus.Registered or UasLifecycleStatus.InStorage)
            {
                uas.Status = UasLifecycleStatus.Suspended;
                uas.UpdatedBy = currentUser.AccountId;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("Defect.Report", nameof(Defect), defect.Id, null, MapDefect(defect), cancellationToken: cancellationToken);
        return MapDefect(defect);
    }

    public async Task<DefectDto> ResolveDefectAsync(Guid defectId, ResolveDefectRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.MaintenanceWrite);
        if (currentUser.AccountId is null)
            throw new ForbiddenException("Chýba identita.");

        var defect = await db.Defects.FirstOrDefaultAsync(d => d.Id == defectId, cancellationToken)
            ?? throw new NotFoundException("Porucha nebola nájdená.");
        if (defect.RowVersion != request.RowVersion)
            throw new ConflictException("Záznam bol medzitým zmenený iným používateľom.");
        if (defect.Status is DefectStatus.Resolved or DefectStatus.Closed)
            throw new ValidationException("Porucha je už vyriešená.");
        if (string.IsNullOrWhiteSpace(request.TechnicalAssessment))
            throw new ValidationException("Technické posúdenie je povinné.");
        if (!request.TestPerformed)
            throw new ValidationException("Obnovenie spôsobilosti vyžaduje vykonanú skúšku.");
        if (request.VerifiedByAccountId == defect.ReportedByAccountId)
            throw new ValidationException("Overenie musí vykonať iná osoba než oznamovateľ.");

        // Original report remains immutable.
        var original = defect.OriginalReport;
        defect.ResolutionNotes = request.ResolutionNotes.Trim();
        defect.TechnicalAssessment = request.TechnicalAssessment.Trim();
        defect.TestPerformed = true;
        defect.ResolvedByAccountId = request.VerifiedByAccountId;
        defect.ResolvedAt = clock.UtcNow;
        defect.Status = DefectStatus.Resolved;
        defect.BlocksOperations = false;
        defect.UpdatedBy = currentUser.AccountId;
        if (defect.OriginalReport != original)
            throw new ValidationException("Pôvodné hlásenie poruchy je nemenné.");

        if (defect.RelatedUasId is not null)
        {
            var uas = await db.UasAircraft.FirstOrDefaultAsync(u => u.Id == defect.RelatedUasId, cancellationToken);
            if (uas is not null && uas.Status == UasLifecycleStatus.Suspended)
            {
                var stillBlocked = await db.Defects.AnyAsync(d =>
                    d.Id != defect.Id && d.RelatedUasId == uas.Id && d.IsActive && d.BlocksOperations
                    && d.Status != DefectStatus.Resolved && d.Status != DefectStatus.Closed, cancellationToken);
                var overdue = await HasBlockingOverdueMaintenanceAsync(uas.Id, cancellationToken);
                if (!stillBlocked && !overdue)
                {
                    uas.Status = UasLifecycleStatus.InService;
                    uas.UpdatedBy = currentUser.AccountId;
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("Defect.Resolve", nameof(Defect), defect.Id, null, MapDefect(defect), cancellationToken: cancellationToken);
        return MapDefect(defect);
    }

    public async Task RecalculateAfterFlightAsync(Guid? uasId, Guid? batteryId, int flightMinutes, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        if (uasId is not null)
        {
            var uas = await db.UasAircraft.AsNoTracking().FirstOrDefaultAsync(u => u.Id == uasId, cancellationToken);
            var plans = await db.MaintenancePlans
                .Where(p => p.IsActive && p.Status != MaintenancePlanStatus.Completed && p.Status != MaintenancePlanStatus.Suspended
                    && ((p.TargetType == MaintenanceTargetType.Uas && p.TargetId == uasId)
                        || (p.TargetType == MaintenanceTargetType.UasType && uas != null && p.TargetTypeCode == uas.UasTypeCode)))
                .ToListAsync(cancellationToken);

            foreach (var plan in plans)
            {
                if (plan.TriggerType is MaintenanceTriggerType.FlightCount)
                    plan.CurrentCounter += 1;
                if (plan.TriggerType is MaintenanceTriggerType.FlightHours)
                    plan.CurrentCounter += Math.Max(1, (int)Math.Ceiling(flightMinutes / 60.0));

                if (MaintenanceRules.IsPlanOverdue(plan, now))
                {
                    plan.Status = MaintenancePlanStatus.Overdue;
                    if (plan.BlockWhenOverdue)
                    {
                        var tracked = await db.UasAircraft.FirstAsync(u => u.Id == uasId, cancellationToken);
                        if (tracked.Status is UasLifecycleStatus.InService or UasLifecycleStatus.Registered or UasLifecycleStatus.InStorage)
                            tracked.Status = UasLifecycleStatus.InMaintenance;
                    }
                }

                plan.UpdatedBy = currentUser.AccountId;
            }
        }

        if (batteryId is not null)
        {
            var plans = await db.MaintenancePlans
                .Where(p => p.IsActive && p.TargetType == MaintenanceTargetType.Battery && p.TargetId == batteryId
                    && p.Status != MaintenancePlanStatus.Completed && p.Status != MaintenancePlanStatus.Suspended)
                .ToListAsync(cancellationToken);
            foreach (var plan in plans)
            {
                if (plan.TriggerType == MaintenanceTriggerType.BatteryCycles)
                    plan.CurrentCounter += 1;
                if (MaintenanceRules.IsPlanOverdue(plan, now))
                    plan.Status = MaintenancePlanStatus.Overdue;
                plan.UpdatedBy = currentUser.AccountId;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AssetBlockCheckDto> GetUasBlockStatusAsync(Guid uasId, CancellationToken cancellationToken = default)
    {
        var defect = await db.Defects.AsNoTracking().FirstOrDefaultAsync(d =>
            d.IsActive && d.BlocksOperations && d.RelatedUasId == uasId
            && d.Status != DefectStatus.Resolved && d.Status != DefectStatus.Closed, cancellationToken);
        if (defect is not null)
            return new AssetBlockCheckDto(true, $"Blokujúca porucha: {defect.Title}");

        var now = clock.UtcNow;
        var restriction = await db.TechnicalRestrictions.AsNoTracking().FirstOrDefaultAsync(r =>
            r.IsActive && r.Status == TechnicalRestrictionStatus.Active && r.BlocksOperation
            && r.TargetType == MaintenanceTargetType.Uas && r.TargetId == uasId
            && r.ValidFrom <= now && (r.ValidTo == null || r.ValidTo >= now), cancellationToken);
        if (restriction is not null)
            return new AssetBlockCheckDto(true, $"Technické obmedzenie: {restriction.Description}");

        if (await HasBlockingOverdueMaintenanceAsync(uasId, cancellationToken))
            return new AssetBlockCheckDto(true, "Prekročený limit údržby — UAS je blokované.");

        var uas = await db.UasAircraft.AsNoTracking().FirstOrDefaultAsync(u => u.Id == uasId, cancellationToken);
        if (uas is not null && !UasStateMachine.IsUsableInOperations(uas.Status))
            return new AssetBlockCheckDto(true, $"UAS nie je použiteľné (stav {uas.Status}).");

        return new AssetBlockCheckDto(false, "OK");
    }

    public async Task<IReadOnlyList<ServiceRequestDto>> ListServiceRequestsAsync(CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.MaintenanceRead);
        var items = await db.ServiceRequests.AsNoTracking().Where(s => s.IsActive)
            .OrderByDescending(s => s.RequestedAt).Take(200).ToListAsync(cancellationToken);
        var accessible = await AccessibleTargetIdsAsync(cancellationToken);
        return items.Where(s => IsTargetAccessible(s.TargetType, s.TargetId, accessible)).Select(MapServiceRequest).ToList();
    }

    public async Task<ServiceRequestDto> CreateServiceRequestAsync(CreateServiceRequestRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.MaintenanceWrite);
        if (currentUser.AccountId is null)
            throw new ForbiddenException("Chýba identita.");
        if (string.IsNullOrWhiteSpace(request.Description))
            throw new ValidationException("Opis požiadavky je povinný.");
        await EnsureTargetAccessAsync(request.TargetType, request.TargetId, cancellationToken);

        if (request.DefectId is Guid defectId
            && !await db.Defects.AnyAsync(d => d.Id == defectId && d.IsActive, cancellationToken))
            throw new NotFoundException("Porucha nebola nájdená.");

        var entity = new ServiceRequest
        {
            RequestNumber = $"SR-{clock.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            RequestTypeCode = string.IsNullOrWhiteSpace(request.RequestTypeCode) ? "REPAIR" : request.RequestTypeCode.Trim(),
            SourceTypeCode = string.IsNullOrWhiteSpace(request.SourceTypeCode) ? "MANUAL" : request.SourceTypeCode.Trim(),
            DefectId = request.DefectId,
            SourceFlightId = request.SourceFlightId,
            TargetType = request.TargetType,
            TargetId = request.TargetId,
            Description = request.Description.Trim(),
            Priority = request.Priority,
            RequestedAt = clock.UtcNow,
            RequestedByAccountId = currentUser.AccountId.Value,
            RequiredCompletionAt = request.RequiredCompletionAt,
            Status = ServiceRequestStatus.New,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.ServiceRequests.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("ServiceRequest.Create", nameof(ServiceRequest), entity.Id, null, MapServiceRequest(entity), cancellationToken: cancellationToken);
        return MapServiceRequest(entity);
    }

    public async Task<IReadOnlyList<TechnicalRestrictionDto>> ListRestrictionsAsync(CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.MaintenanceRead);
        var items = await db.TechnicalRestrictions.AsNoTracking().Where(r => r.IsActive)
            .OrderByDescending(r => r.ValidFrom).Take(200).ToListAsync(cancellationToken);
        var accessible = await AccessibleTargetIdsAsync(cancellationToken);
        return items.Where(r => IsTargetAccessible(r.TargetType, r.TargetId, accessible)).Select(MapRestriction).ToList();
    }

    public async Task<TechnicalRestrictionDto> CreateRestrictionAsync(CreateTechnicalRestrictionRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.MaintenanceWrite);
        if (currentUser.AccountId is null)
            throw new ForbiddenException("Chýba identita.");
        if (string.IsNullOrWhiteSpace(request.Description))
            throw new ValidationException("Opis obmedzenia je povinný.");
        await EnsureTargetAccessAsync(request.TargetType, request.TargetId, cancellationToken);

        var entity = new TechnicalRestriction
        {
            DefectId = request.DefectId,
            TargetType = request.TargetType,
            TargetId = request.TargetId,
            RestrictionTypeCode = string.IsNullOrWhiteSpace(request.RestrictionTypeCode) ? "OPERATIONAL" : request.RestrictionTypeCode.Trim(),
            Description = request.Description.Trim(),
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            BlocksOperation = request.BlocksOperation,
            ApprovedByAccountId = currentUser.AccountId.Value,
            Status = TechnicalRestrictionStatus.Active,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.TechnicalRestrictions.Add(entity);
        AddTechHistory(request.TargetType, request.TargetId, null, request.BlocksOperation ? "RESTRICTED_BLOCKING" : "RESTRICTED",
            request.Description, null, null, null);
        await db.SaveChangesAsync(cancellationToken);
        return MapRestriction(entity);
    }

    public async Task<TechnicalRestrictionDto> EndRestrictionAsync(Guid id, EndTechnicalRestrictionRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.MaintenanceWrite);
        var entity = await db.TechnicalRestrictions.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException("Technické obmedzenie nebolo nájdené.");
        if (entity.RowVersion != request.RowVersion)
            throw new ConflictException("Záznam bol medzitým zmenený iným používateľom.");
        if (entity.Status != TechnicalRestrictionStatus.Active)
            throw new ValidationException("Ukončiť možno iba aktívne obmedzenie.");
        if (string.IsNullOrWhiteSpace(request.TerminationReason))
            throw new ValidationException("Dôvod ukončenia je povinný.");

        entity.Status = TechnicalRestrictionStatus.Ended;
        entity.ValidTo = clock.UtcNow;
        entity.TerminationReason = request.TerminationReason.Trim();
        entity.UpdatedBy = currentUser.AccountId;
        AddTechHistory(entity.TargetType, entity.TargetId, "RESTRICTED", "RESTRICTION_ENDED", request.TerminationReason, entity.DefectId, null, null);
        await db.SaveChangesAsync(cancellationToken);
        return MapRestriction(entity);
    }

    public async Task<IReadOnlyList<ReleaseToServiceDto>> ListReleasesAsync(CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.MaintenanceRead);
        var items = await db.ReleasesToService.AsNoTracking().Where(r => r.IsActive)
            .OrderByDescending(r => r.EffectiveFrom).Take(200).ToListAsync(cancellationToken);
        var accessible = await AccessibleTargetIdsAsync(cancellationToken);
        return items.Where(r => IsTargetAccessible(r.TargetType, r.TargetId, accessible)).Select(MapRelease).ToList();
    }

    public async Task<ReleaseToServiceDto> CreateReleaseAsync(CreateReleaseToServiceRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.MaintenanceWrite);
        if (currentUser.AccountId is null)
            throw new ForbiddenException("Chýba identita.");
        if (string.IsNullOrWhiteSpace(request.DecisionReason))
            throw new ValidationException("Odôvodnenie uvoľnenia je povinné.");
        if (request.Decision == ReleaseToServiceDecision.ReleasedWithRestriction
            && string.IsNullOrWhiteSpace(request.RestrictionDescription))
            throw new ValidationException("Pri uvoľnení s obmedzením je opis obmedzenia povinný.");
        await EnsureTargetAccessAsync(request.TargetType, request.TargetId, cancellationToken);

        if (request.WorkOrderId is Guid woId)
        {
            var wo = await db.WorkOrders.FirstOrDefaultAsync(w => w.Id == woId, cancellationToken)
                ?? throw new NotFoundException("Pracovný príkaz nebol nájdený.");
            if (wo.Status != WorkOrderStatus.Completed)
                throw new ValidationException("Uvoľnenie vyžaduje dokončený pracovný príkaz.");
        }

        var release = new ReleaseToService
        {
            WorkOrderId = request.WorkOrderId,
            DefectId = request.DefectId,
            TargetType = request.TargetType,
            TargetId = request.TargetId,
            Decision = request.Decision,
            ReleaseScopeCode = string.IsNullOrWhiteSpace(request.ReleaseScopeCode) ? "FULL" : request.ReleaseScopeCode.Trim(),
            RestrictionDescription = request.RestrictionDescription?.Trim(),
            EffectiveFrom = clock.UtcNow,
            ValidTo = request.ValidTo,
            ReleasedByAccountId = currentUser.AccountId.Value,
            DecisionReason = request.DecisionReason.Trim(),
            Status = ReleaseToServiceStatus.Valid,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.ReleasesToService.Add(release);

        if (request.Decision is ReleaseToServiceDecision.Released or ReleaseToServiceDecision.ReleasedWithRestriction)
        {
            var active = await db.TechnicalRestrictions
                .Where(r => r.IsActive && r.Status == TechnicalRestrictionStatus.Active
                    && r.TargetType == request.TargetType && r.TargetId == request.TargetId)
                .ToListAsync(cancellationToken);
            foreach (var r in active)
            {
                r.Status = TechnicalRestrictionStatus.Ended;
                r.ValidTo = clock.UtcNow;
                r.TerminationReason = "Ukončené uvoľnením do prevádzky";
                r.UpdatedBy = currentUser.AccountId;
            }

            if (request.Decision == ReleaseToServiceDecision.ReleasedWithRestriction)
            {
                db.TechnicalRestrictions.Add(new TechnicalRestriction
                {
                    DefectId = request.DefectId,
                    TargetType = request.TargetType,
                    TargetId = request.TargetId,
                    RestrictionTypeCode = "RELEASE_CONDITION",
                    Description = request.RestrictionDescription!.Trim(),
                    ValidFrom = clock.UtcNow,
                    ValidTo = request.ValidTo,
                    BlocksOperation = false,
                    ApprovedByAccountId = currentUser.AccountId.Value,
                    Status = TechnicalRestrictionStatus.Active,
                    CreatedBy = currentUser.AccountId,
                    UpdatedBy = currentUser.AccountId
                });
            }

            if (request.DefectId is Guid defectId)
            {
                var defect = await db.Defects.FirstOrDefaultAsync(d => d.Id == defectId, cancellationToken);
                if (defect is not null && defect.Status is not (DefectStatus.Resolved or DefectStatus.Closed))
                {
                    defect.Status = DefectStatus.Resolved;
                    defect.ResolutionNotes = "Uzavreté uvoľnením do prevádzky";
                    defect.ResolvedAt = clock.UtcNow;
                    defect.ResolvedByAccountId = currentUser.AccountId;
                    defect.UpdatedBy = currentUser.AccountId;
                }
            }

            if (request.TargetType == MaintenanceTargetType.Uas && request.Decision == ReleaseToServiceDecision.Released)
            {
                var uas = await db.UasAircraft.FirstOrDefaultAsync(u => u.Id == request.TargetId, cancellationToken);
                if (uas is not null && uas.Status == UasLifecycleStatus.Suspended)
                {
                    var stillBlockedDefect = await db.Defects.AnyAsync(d =>
                        d.IsActive && d.BlocksOperations && d.RelatedUasId == uas.Id
                        && d.Status != DefectStatus.Resolved && d.Status != DefectStatus.Closed
                        && (request.DefectId == null || d.Id != request.DefectId), cancellationToken);
                    if (!stillBlockedDefect)
                    {
                        var from = uas.Status;
                        uas.Status = UasLifecycleStatus.InService;
                        uas.UpdatedBy = currentUser.AccountId;
                        db.UasStatusHistories.Add(new UasStatusHistory
                        {
                            UasId = uas.Id,
                            FromStatus = from,
                            ToStatus = UasLifecycleStatus.InService,
                            ChangedAt = clock.UtcNow,
                            Reason = "Uvoľnenie do prevádzky",
                            CreatedBy = currentUser.AccountId,
                            UpdatedBy = currentUser.AccountId
                        });
                    }
                }
            }

            if (request.WorkOrderId is Guid linkedWo)
            {
                var sr = await db.WorkOrders.AsNoTracking()
                    .Where(w => w.Id == linkedWo).Select(w => w.ServiceRequestId).FirstOrDefaultAsync(cancellationToken);
                if (sr is Guid srId)
                {
                    var serviceReq = await db.ServiceRequests.FirstOrDefaultAsync(s => s.Id == srId, cancellationToken);
                    if (serviceReq is not null)
                    {
                        serviceReq.Status = ServiceRequestStatus.Realized;
                        serviceReq.UpdatedBy = currentUser.AccountId;
                    }
                }
            }

            AddTechHistory(request.TargetType, request.TargetId, "RESTRICTED",
                request.Decision == ReleaseToServiceDecision.Released ? "RELEASED" : "RELEASED_WITH_RESTRICTION",
                request.DecisionReason, request.DefectId, request.WorkOrderId, null);
        }
        else
        {
            AddTechHistory(request.TargetType, request.TargetId, null, "NOT_RELEASED", request.DecisionReason,
                request.DefectId, request.WorkOrderId, null);
        }

        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("ReleaseToService.Create", nameof(ReleaseToService), release.Id, null, MapRelease(release), cancellationToken: cancellationToken);
        return MapRelease(release);
    }

    public async Task<IReadOnlyList<TechnicalStatusHistoryDto>> ListTechnicalStatusHistoryAsync(
        MaintenanceTargetType targetType, Guid targetId, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.MaintenanceRead);
        return await db.TechnicalStatusHistories.AsNoTracking()
            .Where(h => h.IsActive && h.TargetType == targetType && h.TargetId == targetId)
            .OrderByDescending(h => h.EffectiveAt)
            .Select(h => new TechnicalStatusHistoryDto(h.Id, h.TargetType, h.TargetId, h.PreviousStatusCode, h.NewStatusCode, h.EffectiveAt, h.Reason))
            .ToListAsync(cancellationToken);
    }

    private void AddTechHistory(
        MaintenanceTargetType targetType,
        Guid targetId,
        string? previous,
        string next,
        string reason,
        Guid? defectId,
        Guid? workOrderId,
        Guid? releaseId)
    {
        db.TechnicalStatusHistories.Add(new TechnicalStatusHistory
        {
            TargetType = targetType,
            TargetId = targetId,
            PreviousStatusCode = previous,
            NewStatusCode = next,
            EffectiveAt = clock.UtcNow,
            Reason = reason.Trim(),
            SourceDefectId = defectId,
            SourceWorkOrderId = workOrderId,
            SourceReleaseId = releaseId,
            ChangedByAccountId = currentUser.AccountId ?? Guid.Empty,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        });
    }

    private async Task<bool> HasBlockingOverdueMaintenanceAsync(Guid uasId, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var plans = await db.MaintenancePlans.AsNoTracking()
            .Where(p => p.IsActive && p.BlockWhenOverdue && p.TargetType == MaintenanceTargetType.Uas && p.TargetId == uasId
                && p.Status != MaintenancePlanStatus.Completed && p.Status != MaintenancePlanStatus.Suspended)
            .ToListAsync(cancellationToken);
        return plans.Any(p => MaintenanceRules.IsPlanOverdue(p, now) || p.Status == MaintenancePlanStatus.Overdue);
    }

    private void ResetPlanAfterMaintenance(MaintenancePlan plan)
    {
        plan.Status = MaintenancePlanStatus.Active;
        if (plan.TriggerType == MaintenanceTriggerType.CalendarDate && plan.IntervalValue is not null)
            plan.NextDueAt = clock.UtcNow.AddDays(plan.IntervalValue.Value);
        if (plan.TriggerType is MaintenanceTriggerType.FlightCount or MaintenanceTriggerType.FlightHours or MaintenanceTriggerType.BatteryCycles
            && plan.IntervalValue is not null)
            plan.NextDueCounter = plan.CurrentCounter + plan.IntervalValue.Value;
    }

    private MaintenancePlanDto MapPlan(MaintenancePlan p)
    {
        var now = clock.UtcNow;
        var overdue = MaintenanceRules.IsPlanOverdue(p, now) || p.Status == MaintenancePlanStatus.Overdue;
        return new MaintenancePlanDto(p.Id, p.NameSk, p.TargetType, p.TargetId, p.TargetTypeCode, p.TriggerType,
            p.IntervalValue, p.NextDueAt, p.NextDueCounter, p.CurrentCounter, p.WarningThresholdPercent, p.BlockWhenOverdue,
            overdue ? MaintenancePlanStatus.Overdue : p.Status, overdue, MaintenanceRules.ShouldWarn(p, now), p.IsActive, p.RowVersion);
    }

    private static WorkOrderDto MapWorkOrder(WorkOrder w) =>
        new(w.Id, w.MaintenancePlanId, w.ServiceRequestId, w.TargetType, w.TargetId, w.Title, w.Description, w.Status,
            w.AssignedTechnicianAccountId, w.DueAt, w.IsActive, w.RowVersion);

    private static ServiceRequestDto MapServiceRequest(ServiceRequest s) =>
        new(s.Id, s.RequestNumber, s.RequestTypeCode, s.SourceTypeCode, s.DefectId, s.TargetType, s.TargetId,
            s.Description, s.Priority, s.RequestedAt, s.Status, s.RequiredCompletionAt, s.IsActive, s.RowVersion);

    private static TechnicalRestrictionDto MapRestriction(TechnicalRestriction r) =>
        new(r.Id, r.DefectId, r.TargetType, r.TargetId, r.RestrictionTypeCode, r.Description, r.ValidFrom, r.ValidTo,
            r.BlocksOperation, r.Status, r.IsActive, r.RowVersion);

    private static ReleaseToServiceDto MapRelease(ReleaseToService r) =>
        new(r.Id, r.WorkOrderId, r.DefectId, r.TargetType, r.TargetId, r.Decision, r.ReleaseScopeCode,
            r.RestrictionDescription, r.EffectiveFrom, r.ValidTo, r.ReleasedByAccountId, r.DecisionReason, r.Status,
            r.IsActive, r.RowVersion);

    private static DefectDto MapDefect(Defect d) =>
        new(d.Id, d.TargetType, d.TargetId, d.Title, d.OriginalReport, d.OriginalReportLocked, d.Severity, d.Status,
            d.BlocksOperations, d.TemporaryMeasure, d.ReportedByAccountId, d.DefectTypeCode, d.ResolutionNotes, d.IsActive, d.RowVersion);

    private sealed record AccessibleTargets(HashSet<Guid> UasIds, HashSet<Guid> BatteryIds, HashSet<Guid> ComponentIds);

    private async Task<AccessibleTargets> AccessibleTargetIdsAsync(CancellationToken cancellationToken)
    {
        var uas = await db.UasAircraft.AsNoTracking().Include(u => u.OwningOrgUnit)
            .Where(u => u.IsActive).ToListAsync(cancellationToken);
        var batteries = await db.Batteries.AsNoTracking().Include(b => b.OwningOrgUnit)
            .Where(b => b.IsActive).ToListAsync(cancellationToken);
        var components = await db.Components.AsNoTracking().Include(c => c.OwningOrgUnit)
            .Where(c => c.IsActive).ToListAsync(cancellationToken);

        return new AccessibleTargets(
            uas.Where(u => u.OwningOrgUnit is null || policy.CanAccessOrgUnit(u.OwningOrgUnit.Path)).Select(u => u.Id).ToHashSet(),
            batteries.Where(b => b.OwningOrgUnit is null || policy.CanAccessOrgUnit(b.OwningOrgUnit.Path)).Select(b => b.Id).ToHashSet(),
            components.Where(c => c.OwningOrgUnit is null || policy.CanAccessOrgUnit(c.OwningOrgUnit.Path)).Select(c => c.Id).ToHashSet());
    }

    private static bool IsTargetAccessible(MaintenanceTargetType type, Guid? targetId, AccessibleTargets accessible)
    {
        if (targetId is null || type == MaintenanceTargetType.UasType)
            return true;
        return type switch
        {
            MaintenanceTargetType.Uas => accessible.UasIds.Contains(targetId.Value),
            MaintenanceTargetType.Battery => accessible.BatteryIds.Contains(targetId.Value),
            MaintenanceTargetType.Component => accessible.ComponentIds.Contains(targetId.Value),
            _ => true
        };
    }

    private async Task EnsureTargetAccessAsync(MaintenanceTargetType type, Guid? targetId, CancellationToken cancellationToken)
    {
        if (targetId is null || type == MaintenanceTargetType.UasType)
            return;
        var accessible = await AccessibleTargetIdsAsync(cancellationToken);
        if (!IsTargetAccessible(type, targetId, accessible))
            throw new ForbiddenException("Prístup k cieľovému majetku bol zamietnutý.");
    }
}
