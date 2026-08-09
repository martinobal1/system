using IsSuas.Application.Abstractions;
using IsSuas.Application.Common;
using IsSuas.Application.Fleet;
using IsSuas.Application.Policies;
using IsSuas.Domain.Enums;
using IsSuas.Domain.Fleet;
using IsSuas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IsSuas.Infrastructure.Services;

public interface IFleetService
{
    Task<PagedResult<UasDto>> ListUasAsync(int page, int pageSize, string? search, CancellationToken cancellationToken = default);
    Task<UasDetailDto> GetUasAsync(Guid id, CancellationToken cancellationToken = default);
    Task<UasDto> CreateUasAsync(CreateUasRequest request, CancellationToken cancellationToken = default);
    Task<UasDto> SubmitUasAsync(Guid id, CancellationToken cancellationToken = default);
    Task<UasDto> ApproveUasAsync(Guid id, CancellationToken cancellationToken = default);
    Task<UasDto> TransitionUasAsync(Guid id, TransitionUasRequest request, CancellationToken cancellationToken = default);
    Task<UasAssignmentDto> AssignUasAsync(Guid id, AssignUasRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ComponentDto>> ListComponentsAsync(CancellationToken cancellationToken = default);
    Task<ComponentDto> CreateComponentAsync(CreateComponentRequest request, CancellationToken cancellationToken = default);
    Task<InstallationDto> InstallComponentAsync(Guid componentId, InstallComponentRequest request, CancellationToken cancellationToken = default);
    Task<InstallationDto> RemoveInstallationAsync(Guid componentId, Guid installationId, RemoveInstallationRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CompatibilityRuleDto>> ListComponentCompatAsync(Guid componentId, CancellationToken cancellationToken = default);
    Task<CompatibilityRuleDto> AddComponentCompatAsync(Guid componentId, CreateCompatibilityRuleRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BatteryDto>> ListBatteriesAsync(CancellationToken cancellationToken = default);
    Task<BatteryDto> CreateBatteryAsync(CreateBatteryRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CompatibilityRuleDto>> ListBatteryCompatAsync(Guid batteryId, CancellationToken cancellationToken = default);
    Task<CompatibilityRuleDto> AddBatteryCompatAsync(Guid batteryId, CreateCompatibilityRuleRequest request, CancellationToken cancellationToken = default);
    Task EnsureBatteryCompatibleAsync(Guid batteryId, Guid uasId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BatteryTechnicalRecordDto>> ListBatteryTechnicalAsync(Guid batteryId, CancellationToken cancellationToken = default);
    Task<BatteryTechnicalRecordDto> AddBatteryTechnicalAsync(Guid batteryId, CreateBatteryTechnicalRecordRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BatteryOperationRecordDto>> ListBatteryOperationsAsync(Guid batteryId, CancellationToken cancellationToken = default);
    Task<BatteryOperationRecordDto> AddBatteryOperationAsync(Guid batteryId, CreateBatteryOperationRecordRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BatteryStatusHistoryDto>> ListBatteryStatusHistoryAsync(Guid batteryId, CancellationToken cancellationToken = default);
    Task<BatteryDto> TransitionBatteryStatusAsync(Guid batteryId, TransitionBatteryStatusRequest request, CancellationToken cancellationToken = default);
}

public sealed class FleetService(
    IsSuasDbContext db,
    IAuthorizationPolicyService policy,
    ICurrentUser currentUser,
    IAuditWriter audit,
    IClock clock) : IFleetService
{
    public async Task<PagedResult<UasDto>> ListUasAsync(int page, int pageSize, string? search, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.UasRead);
        if (!policy.CanAccessBusinessData())
            throw new ForbiddenException("Systémový administrátor nemá automatický prístup k vecnému obsahu.");

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.UasAircraft.AsNoTracking().Include(x => x.OwningOrgUnit).Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(x =>
                x.SerialNumber.Contains(s) ||
                (x.RegistrationNumber != null && x.RegistrationNumber.Contains(s)) ||
                x.Model.Contains(s));
        }

        var all = await query.OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
        var filtered = all.Where(x => x.OwningOrgUnit is null || policy.CanAccessOrgUnit(x.OwningOrgUnit.Path)).ToList();
        var total = filtered.Count;
        var items = filtered.Skip((page - 1) * pageSize).Take(pageSize).Select(MapUas).ToList();
        return new PagedResult<UasDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<UasDetailDto> GetUasAsync(Guid id, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.UasRead);
        if (!policy.CanAccessBusinessData())
            throw new ForbiddenException("Systémový administrátor nemá automatický prístup k vecnému obsahu.");

        var uas = await db.UasAircraft
            .Include(x => x.OwningOrgUnit)
            .Include(x => x.Assignments).ThenInclude(a => a.OrgUnit)
            .Include(x => x.Installations).ThenInclude(i => i.Component)
            .Include(x => x.StatusHistory)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("UAS nebol nájdený.");

        EnsureUasOrgAccess(uas);

        return new UasDetailDto(
            MapUas(uas),
            uas.Assignments.OrderByDescending(a => a.ValidFrom).Select(a => new UasAssignmentDto(
                a.Id, a.OrgUnitId, a.OrgUnit.Name, a.ValidFrom, a.ValidTo, a.Reason, a.IsActive)).ToList(),
            uas.Installations.OrderByDescending(i => i.InstalledAt).Select(i => new InstallationDto(
                i.Id, i.ComponentId, i.Component.Name, i.UasId, uas.RegistrationNumber, i.InstalledAt, i.RemovedAt, i.IsActive)).ToList(),
            uas.StatusHistory.OrderByDescending(h => h.ChangedAt).Select(h => new UasStatusHistoryDto(
                h.Id, h.FromStatus, h.ToStatus, h.Reason, h.ChangedAt)).ToList());
    }

    public async Task<UasDto> CreateUasAsync(CreateUasRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.UasWrite);
        if (await db.UasAircraft.AnyAsync(x => x.SerialNumber == request.SerialNumber, cancellationToken))
            throw new ConflictException("Výrobné číslo UAS už existuje.");

        if (request.OwningOrgUnitId is not null)
        {
            var org = await db.OrgUnits.FirstOrDefaultAsync(x => x.Id == request.OwningOrgUnitId, cancellationToken)
                ?? throw new NotFoundException("Útvar nebol nájdený.");
            policy.EnsureOrgAccess(org.Path);
        }

        var entity = new Uas
        {
            SerialNumber = request.SerialNumber.Trim(),
            Manufacturer = request.Manufacturer.Trim(),
            Model = request.Model.Trim(),
            UasTypeCode = request.UasTypeCode,
            OwningOrgUnitId = request.OwningOrgUnitId,
            Notes = request.Notes,
            Status = UasLifecycleStatus.Draft,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.UasAircraft.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("Uas.Create", nameof(Uas), entity.Id, null, MapUas(entity), cancellationToken: cancellationToken);
        return MapUas(entity);
    }

    public async Task<UasDto> SubmitUasAsync(Guid id, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.UasWrite);
        var uas = await LoadUasAsync(id, cancellationToken);
        EnsureUasOrgAccess(uas);
        if (uas.Status != UasLifecycleStatus.Draft)
            throw new ValidationException("Predložiť možno iba koncept.");

        uas.SubmittedByAccountId = currentUser.AccountId;
        uas.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("Uas.Submit", nameof(Uas), uas.Id, null, MapUas(uas), cancellationToken: cancellationToken);
        return MapUas(uas);
    }

    public async Task<UasDto> ApproveUasAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var uas = await LoadUasAsync(id, cancellationToken);
        EnsureUasOrgAccess(uas);
        policy.EnsureCanApprove(uas.SubmittedByAccountId, Permissions.UasApprove);

        if (uas.Status != UasLifecycleStatus.Draft)
            throw new ValidationException("Schváliť možno iba koncept.");
        if (uas.SubmittedByAccountId is null)
            throw new ValidationException("UAS musí byť najprv predložený.");

        var before = MapUas(uas);
        var year = clock.UtcNow.Year;
        var seq = await db.UasAircraft.CountAsync(x => x.RegistrationNumber != null && x.RegistrationNumber.StartsWith($"SUAS-{year}-"), cancellationToken) + 1;
        uas.RegistrationNumber = $"SUAS-{year}-{seq:D5}";
        uas.Status = UasLifecycleStatus.Registered;
        uas.RegisteredAt = clock.UtcNow;
        uas.ApprovedByAccountId = currentUser.AccountId;
        uas.UpdatedBy = currentUser.AccountId;
        db.UasStatusHistories.Add(new UasStatusHistory
        {
            UasId = uas.Id,
            FromStatus = UasLifecycleStatus.Draft,
            ToStatus = UasLifecycleStatus.Registered,
            Reason = "Schválenie registrácie",
            ChangedAt = clock.UtcNow,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        });
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("Uas.Approve", nameof(Uas), uas.Id, before, MapUas(uas), cancellationToken: cancellationToken);
        return MapUas(uas);
    }

    public async Task<UasDto> TransitionUasAsync(Guid id, TransitionUasRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.UasWrite);
        var uas = await LoadUasAsync(id, cancellationToken);
        EnsureUasOrgAccess(uas);
        if (uas.RowVersion != request.RowVersion)
            throw new ConflictException("Záznam bol medzitým zmenený iným používateľom.");
        if (!UasStateMachine.CanTransition(uas.Status, request.TargetStatus))
            throw new ValidationException($"Nepovolený prechod {uas.Status} → {request.TargetStatus}.");

        var before = MapUas(uas);
        var from = uas.Status;
        uas.Status = request.TargetStatus;
        uas.UpdatedBy = currentUser.AccountId;
        db.UasStatusHistories.Add(new UasStatusHistory
        {
            UasId = uas.Id,
            FromStatus = from,
            ToStatus = request.TargetStatus,
            Reason = request.Reason,
            ChangedAt = clock.UtcNow,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        });
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("Uas.Transition", nameof(Uas), uas.Id, before, MapUas(uas), cancellationToken: cancellationToken);
        return MapUas(uas);
    }

    public async Task<UasAssignmentDto> AssignUasAsync(Guid id, AssignUasRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.UasWrite);
        var uas = await LoadUasAsync(id, cancellationToken);
        EnsureUasOrgAccess(uas);
        var org = await db.OrgUnits.FirstOrDefaultAsync(x => x.Id == request.OrgUnitId && x.IsActive, cancellationToken)
            ?? throw new NotFoundException("Útvar nebol nájdený.");
        policy.EnsureOrgAccess(org.Path);

        var now = request.ValidFrom;
        var hasActive = await db.UasAssignments.AnyAsync(a =>
            a.UasId == id && a.IsActive && a.ValidTo == null, cancellationToken);
        if (hasActive)
            throw new ConflictException("UAS už má aktívne pridelenie útvaru. Najprv ukončite existujúce pridelenie.");

        var overlapping = await db.UasAssignments.AnyAsync(a =>
            a.UasId == id && a.IsActive && a.ValidFrom < (a.ValidTo ?? DateTimeOffset.MaxValue)
            && a.ValidFrom <= now && (a.ValidTo == null || a.ValidTo > now), cancellationToken);
        if (overlapping)
            throw new ConflictException("UAS už má aktívne pridelenie útvaru v danom čase.");

        var assignment = new UasAssignment
        {
            UasId = id,
            OrgUnitId = org.Id,
            ValidFrom = now,
            Reason = request.Reason,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.UasAssignments.Add(assignment);
        uas.OwningOrgUnitId = org.Id;
        uas.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        assignment.OrgUnit = org;
        await audit.WriteAsync("Uas.Assign", nameof(UasAssignment), assignment.Id, null,
            new UasAssignmentDto(assignment.Id, org.Id, org.Name, assignment.ValidFrom, assignment.ValidTo, assignment.Reason, assignment.IsActive),
            cancellationToken: cancellationToken);
        return new UasAssignmentDto(assignment.Id, org.Id, org.Name, assignment.ValidFrom, assignment.ValidTo, assignment.Reason, assignment.IsActive);
    }

    public async Task<IReadOnlyList<ComponentDto>> ListComponentsAsync(CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.ComponentsRead);
        var items = await db.Components.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.InternalNumber).ToListAsync(cancellationToken);
        return items.Select(MapComponent).ToList();
    }

    public async Task<ComponentDto> CreateComponentAsync(CreateComponentRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.ComponentsWrite);
        if (await db.Components.AnyAsync(x => x.InternalNumber == request.InternalNumber, cancellationToken))
            throw new ConflictException("Interné číslo komponentu už existuje.");

        var entity = new Component
        {
            InternalNumber = request.InternalNumber.Trim(),
            SerialNumber = request.SerialNumber,
            Name = request.Name.Trim(),
            ComponentTypeCode = request.ComponentTypeCode,
            OwningOrgUnitId = request.OwningOrgUnitId,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.Components.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("Component.Create", nameof(Component), entity.Id, null, MapComponent(entity), cancellationToken: cancellationToken);
        return MapComponent(entity);
    }

    public async Task<InstallationDto> InstallComponentAsync(Guid componentId, InstallComponentRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.ComponentsWrite);
        var component = await db.Components.FirstOrDefaultAsync(x => x.Id == componentId && x.IsActive, cancellationToken)
            ?? throw new NotFoundException("Komponent nebol nájdený.");
        var uas = await LoadUasAsync(request.UasId, cancellationToken);
        EnsureUasOrgAccess(uas);

        var activeInstall = await db.Installations.AnyAsync(i =>
            i.ComponentId == componentId && i.IsActive && i.RemovedAt == null, cancellationToken);
        if (activeInstall)
            throw new ConflictException("Komponent už má aktívnu montáž.");

        await EnsureCompatibleAsync(
            await db.ComponentCompatibilities.AsNoTracking()
                .Where(c => c.ComponentId == componentId && c.IsActive).ToListAsync(cancellationToken),
            uas, "Komponent nie je kompatibilný s cieľovým UAS.", cancellationToken);

        var installation = new Installation
        {
            ComponentId = componentId,
            UasId = uas.Id,
            InstalledAt = request.InstalledAt,
            Reason = request.Reason,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.Installations.Add(installation);
        component.Status = AssetUsabilityStatus.InUse;
        await db.SaveChangesAsync(cancellationToken);
        return new InstallationDto(installation.Id, component.Id, component.Name, uas.Id, uas.RegistrationNumber,
            installation.InstalledAt, installation.RemovedAt, installation.IsActive);
    }

    public async Task<InstallationDto> RemoveInstallationAsync(Guid componentId, Guid installationId, RemoveInstallationRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.ComponentsWrite);
        var installation = await db.Installations
            .Include(i => i.Component)
            .Include(i => i.Uas)
            .FirstOrDefaultAsync(i => i.Id == installationId && i.ComponentId == componentId, cancellationToken)
            ?? throw new NotFoundException("Montáž nebola nájdená.");
        if (installation.RemovedAt is not null)
            throw new ValidationException("Montáž už bola ukončená.");

        installation.RemovedAt = clock.UtcNow;
        installation.Reason = request.Reason ?? installation.Reason;
        installation.UpdatedBy = currentUser.AccountId;
        installation.Component.Status = AssetUsabilityStatus.Available;
        await db.SaveChangesAsync(cancellationToken);
        return new InstallationDto(installation.Id, installation.ComponentId, installation.Component.Name,
            installation.UasId, installation.Uas.RegistrationNumber, installation.InstalledAt, installation.RemovedAt, installation.IsActive);
    }

    public async Task<IReadOnlyList<CompatibilityRuleDto>> ListComponentCompatAsync(Guid componentId, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.ComponentsRead);
        if (!await db.Components.AnyAsync(c => c.Id == componentId, cancellationToken))
            throw new NotFoundException("Komponent nebol nájdený.");
        var items = await db.ComponentCompatibilities.AsNoTracking()
            .Where(c => c.ComponentId == componentId && c.IsActive)
            .OrderByDescending(c => c.ValidFrom)
            .ToListAsync(cancellationToken);
        return items.Select(c => MapCompat(c.Id, c.ComponentId, c.UasId, c.UasTypeCode, c.Status, c.ValidFrom, c.ValidTo, c.RestrictionText)).ToList();
    }

    public async Task<CompatibilityRuleDto> AddComponentCompatAsync(Guid componentId, CreateCompatibilityRuleRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.ComponentsWrite);
        if (!await db.Components.AnyAsync(c => c.Id == componentId && c.IsActive, cancellationToken))
            throw new NotFoundException("Komponent nebol nájdený.");
        ValidateCompatRequest(request);

        var entity = new ComponentCompatibility
        {
            ComponentId = componentId,
            UasId = request.UasId,
            UasTypeCode = string.IsNullOrWhiteSpace(request.UasTypeCode) ? null : request.UasTypeCode.Trim(),
            Status = request.Status,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            RestrictionText = request.RestrictionText?.Trim(),
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.ComponentCompatibilities.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return MapCompat(entity.Id, entity.ComponentId, entity.UasId, entity.UasTypeCode, entity.Status, entity.ValidFrom, entity.ValidTo, entity.RestrictionText);
    }

    public async Task<IReadOnlyList<BatteryDto>> ListBatteriesAsync(CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.BatteriesRead);
        var items = await db.Batteries.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.InternalNumber).ToListAsync(cancellationToken);
        return items.Select(MapBattery).ToList();
    }

    public async Task<BatteryDto> CreateBatteryAsync(CreateBatteryRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.BatteriesWrite);
        if (await db.Batteries.AnyAsync(x => x.InternalNumber == request.InternalNumber || x.SerialNumber == request.SerialNumber, cancellationToken))
            throw new ConflictException("Interné alebo výrobné číslo batérie už existuje.");

        var entity = new Battery
        {
            InternalNumber = request.InternalNumber.Trim(),
            SerialNumber = request.SerialNumber.Trim(),
            Manufacturer = request.Manufacturer,
            Model = request.Model,
            CompatibleUasTypeCodes = request.CompatibleUasTypeCodes,
            OwningOrgUnitId = request.OwningOrgUnitId,
            CapacityPercent = 100,
            HealthPercent = 100,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.Batteries.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        db.BatteryStatusHistories.Add(new BatteryStatusHistory
        {
            BatteryId = entity.Id,
            StatusKind = BatteryStatusKind.Usability,
            PreviousStatus = null,
            NewStatus = entity.Status,
            EffectiveAt = clock.UtcNow,
            Reason = "Zaradenie batérie do evidencie",
            ChangedByAccountId = currentUser.AccountId ?? Guid.Empty,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        });
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("Battery.Create", nameof(Battery), entity.Id, null, MapBattery(entity), cancellationToken: cancellationToken);
        return MapBattery(entity);
    }

    public async Task<IReadOnlyList<CompatibilityRuleDto>> ListBatteryCompatAsync(Guid batteryId, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.BatteriesRead);
        if (!await db.Batteries.AnyAsync(b => b.Id == batteryId, cancellationToken))
            throw new NotFoundException("Batéria nebola nájdená.");
        var items = await db.BatteryCompatibilities.AsNoTracking()
            .Where(c => c.BatteryId == batteryId && c.IsActive)
            .OrderByDescending(c => c.ValidFrom)
            .ToListAsync(cancellationToken);
        return items.Select(c => MapCompat(c.Id, c.BatteryId, c.UasId, c.UasTypeCode, c.Status, c.ValidFrom, c.ValidTo, c.RestrictionText)).ToList();
    }

    public async Task<CompatibilityRuleDto> AddBatteryCompatAsync(Guid batteryId, CreateCompatibilityRuleRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.BatteriesWrite);
        if (!await db.Batteries.AnyAsync(b => b.Id == batteryId && b.IsActive, cancellationToken))
            throw new NotFoundException("Batéria nebola nájdená.");
        ValidateCompatRequest(request);

        var entity = new BatteryCompatibility
        {
            BatteryId = batteryId,
            UasId = request.UasId,
            UasTypeCode = string.IsNullOrWhiteSpace(request.UasTypeCode) ? null : request.UasTypeCode.Trim(),
            Status = request.Status,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            RestrictionText = request.RestrictionText?.Trim(),
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.BatteryCompatibilities.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return MapCompat(entity.Id, entity.BatteryId, entity.UasId, entity.UasTypeCode, entity.Status, entity.ValidFrom, entity.ValidTo, entity.RestrictionText);
    }

    public async Task EnsureBatteryCompatibleAsync(Guid batteryId, Guid uasId, CancellationToken cancellationToken = default)
    {
        var uas = await LoadUasAsync(uasId, cancellationToken);
        var rules = await db.BatteryCompatibilities.AsNoTracking()
            .Where(c => c.BatteryId == batteryId && c.IsActive).ToListAsync(cancellationToken);
        await EnsureCompatibleAsync(rules.Select(r => (r.UasId, r.UasTypeCode, r.Status, r.ValidFrom, r.ValidTo, r.RestrictionText)).ToList(),
            uas, "Batéria nie je kompatibilná s UAS.", cancellationToken);
    }

    public async Task<IReadOnlyList<BatteryTechnicalRecordDto>> ListBatteryTechnicalAsync(Guid batteryId, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.BatteriesRead);
        return await db.BatteryTechnicalRecords.AsNoTracking()
            .Where(r => r.BatteryId == batteryId && r.IsActive)
            .OrderByDescending(r => r.RecordedAt)
            .Select(r => new BatteryTechnicalRecordDto(r.Id, r.BatteryId, r.RecordedAt, r.MetricType, r.MeasuredValue, r.UnitCode, r.TextValue, r.SourceType, r.SourceFlightId))
            .ToListAsync(cancellationToken);
    }

    public async Task<BatteryTechnicalRecordDto> AddBatteryTechnicalAsync(Guid batteryId, CreateBatteryTechnicalRecordRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.BatteriesWrite);
        var battery = await db.Batteries.FirstOrDefaultAsync(b => b.Id == batteryId && b.IsActive, cancellationToken)
            ?? throw new NotFoundException("Batéria nebola nájdená.");
        if (request.MeasuredValue is null && string.IsNullOrWhiteSpace(request.TextValue))
            throw new ValidationException("Technický záznam vyžaduje nameranú alebo textovú hodnotu.");

        var record = new BatteryTechnicalRecord
        {
            BatteryId = batteryId,
            RecordedAt = request.RecordedAt,
            MetricType = request.MetricType,
            MeasuredValue = request.MeasuredValue,
            UnitCode = request.UnitCode,
            TextValue = request.TextValue,
            SourceType = request.SourceType,
            SourceFlightId = request.SourceFlightId,
            CorrectionReason = request.CorrectionReason,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.BatteryTechnicalRecords.Add(record);

        if (request.ApplyToBattery && request.MeasuredValue is decimal value)
        {
            switch (request.MetricType)
            {
                case BatteryTechnicalMetric.CapacityPercent:
                    battery.CapacityPercent = value;
                    break;
                case BatteryTechnicalMetric.HealthPercent:
                    battery.HealthPercent = value;
                    break;
                case BatteryTechnicalMetric.CycleCount:
                    battery.CycleCount = (int)value;
                    break;
                case BatteryTechnicalMetric.TemperatureC:
                    battery.MaxTemperatureC = battery.MaxTemperatureC is null ? value : Math.Max(battery.MaxTemperatureC.Value, value);
                    battery.MinTemperatureC = battery.MinTemperatureC is null ? value : Math.Min(battery.MinTemperatureC.Value, value);
                    break;
            }
            battery.UpdatedBy = currentUser.AccountId;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new BatteryTechnicalRecordDto(record.Id, record.BatteryId, record.RecordedAt, record.MetricType,
            record.MeasuredValue, record.UnitCode, record.TextValue, record.SourceType, record.SourceFlightId);
    }

    public async Task<IReadOnlyList<BatteryOperationRecordDto>> ListBatteryOperationsAsync(Guid batteryId, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.BatteriesRead);
        return await db.BatteryOperationRecords.AsNoTracking()
            .Where(r => r.BatteryId == batteryId && r.IsActive)
            .OrderByDescending(r => r.OccurredAt)
            .Select(r => new BatteryOperationRecordDto(r.Id, r.BatteryId, r.EventType, r.OccurredAt, r.ChargeLevelPercent, r.CycleCount, r.TemperatureC, r.ResultCode, r.Note, r.SourceType))
            .ToListAsync(cancellationToken);
    }

    public async Task<BatteryOperationRecordDto> AddBatteryOperationAsync(Guid batteryId, CreateBatteryOperationRecordRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.BatteriesWrite);
        var battery = await db.Batteries.FirstOrDefaultAsync(b => b.Id == batteryId && b.IsActive, cancellationToken)
            ?? throw new NotFoundException("Batéria nebola nájdená.");

        var record = new BatteryOperationRecord
        {
            BatteryId = batteryId,
            EventType = request.EventType,
            OccurredAt = request.OccurredAt,
            ChargeLevelPercent = request.ChargeLevelPercent,
            CycleCount = request.CycleCount,
            TemperatureC = request.TemperatureC,
            ResultCode = request.ResultCode,
            Note = request.Note,
            SourceType = request.SourceType,
            RelatedFlightId = request.RelatedFlightId,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.BatteryOperationRecords.Add(record);

        if (request.ApplyToBattery)
        {
            if (request.ChargeLevelPercent is int charge)
                battery.CapacityPercent = charge;
            if (request.CycleCount is int cycles)
                battery.CycleCount = cycles;
            if (request.EventType is BatteryOperationEventType.Damage or BatteryOperationEventType.Quarantine
                && battery.Status is AssetUsabilityStatus.Available or AssetUsabilityStatus.InUse)
            {
                var from = battery.Status;
                battery.Status = request.EventType == BatteryOperationEventType.Damage
                    ? AssetUsabilityStatus.Damaged
                    : AssetUsabilityStatus.Suspended;
                db.BatteryStatusHistories.Add(new BatteryStatusHistory
                {
                    BatteryId = battery.Id,
                    PreviousStatus = from,
                    NewStatus = battery.Status,
                    EffectiveAt = request.OccurredAt,
                    Reason = request.Note ?? request.EventType.ToString(),
                    ChangedByAccountId = currentUser.AccountId ?? Guid.Empty,
                    CreatedBy = currentUser.AccountId,
                    UpdatedBy = currentUser.AccountId
                });
            }
            battery.UpdatedBy = currentUser.AccountId;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new BatteryOperationRecordDto(record.Id, record.BatteryId, record.EventType, record.OccurredAt,
            record.ChargeLevelPercent, record.CycleCount, record.TemperatureC, record.ResultCode, record.Note, record.SourceType);
    }

    public async Task<IReadOnlyList<BatteryStatusHistoryDto>> ListBatteryStatusHistoryAsync(Guid batteryId, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.BatteriesRead);
        return await db.BatteryStatusHistories.AsNoTracking()
            .Where(h => h.BatteryId == batteryId && h.IsActive)
            .OrderByDescending(h => h.EffectiveAt)
            .Select(h => new BatteryStatusHistoryDto(h.Id, h.BatteryId, h.StatusKind, h.PreviousStatus, h.NewStatus, h.EffectiveAt, h.Reason, h.ChangedByAccountId))
            .ToListAsync(cancellationToken);
    }

    public async Task<BatteryDto> TransitionBatteryStatusAsync(Guid batteryId, TransitionBatteryStatusRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.BatteriesWrite);
        var battery = await db.Batteries.FirstOrDefaultAsync(b => b.Id == batteryId && b.IsActive, cancellationToken)
            ?? throw new NotFoundException("Batéria nebola nájdená.");
        if (battery.RowVersion != request.RowVersion)
            throw new ConflictException("Záznam bol medzitým zmenený iným používateľom.");
        if (string.IsNullOrWhiteSpace(request.Reason)
            && request.TargetStatus is AssetUsabilityStatus.Suspended or AssetUsabilityStatus.Damaged or AssetUsabilityStatus.Decommissioned)
            throw new ValidationException("Dôvod je povinný pri pozastavení, poškodení alebo vyradení.");

        var from = battery.Status;
        if (from == request.TargetStatus)
            return MapBattery(battery);

        battery.Status = request.TargetStatus;
        battery.UpdatedBy = currentUser.AccountId;
        db.BatteryStatusHistories.Add(new BatteryStatusHistory
        {
            BatteryId = battery.Id,
            PreviousStatus = from,
            NewStatus = request.TargetStatus,
            EffectiveAt = clock.UtcNow,
            Reason = request.Reason?.Trim(),
            ChangedByAccountId = currentUser.AccountId ?? Guid.Empty,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        });
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("Battery.StatusChange", nameof(Battery), battery.Id,
            new { from }, new { to = request.TargetStatus, request.Reason }, cancellationToken: cancellationToken);
        return MapBattery(battery);
    }

    private async Task EnsureCompatibleAsync(
        IReadOnlyList<ComponentCompatibility> rules,
        Uas uas,
        string failMessage,
        CancellationToken cancellationToken) =>
        await EnsureCompatibleAsync(
            rules.Select(r => (r.UasId, r.UasTypeCode, r.Status, r.ValidFrom, r.ValidTo, r.RestrictionText)).ToList(),
            uas, failMessage, cancellationToken);

    private Task EnsureCompatibleAsync(
        IReadOnlyList<(Guid? UasId, string? UasTypeCode, CompatibilityStatus Status, DateTimeOffset ValidFrom, DateTimeOffset? ValidTo, string? RestrictionText)> rules,
        Uas uas,
        string failMessage,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (rules.Count == 0)
            return Task.CompletedTask;

        var now = clock.UtcNow;
        var active = rules.Where(r => r.ValidFrom <= now && (r.ValidTo is null || r.ValidTo >= now)).ToList();
        if (active.Count == 0)
            throw new ValidationException(failMessage + " (žiadne platné pravidlo).");

        var matching = active.Where(r =>
            (r.UasId is Guid uid && uid == uas.Id)
            || (!string.IsNullOrWhiteSpace(r.UasTypeCode)
                && !string.IsNullOrWhiteSpace(uas.UasTypeCode)
                && string.Equals(r.UasTypeCode, uas.UasTypeCode, StringComparison.OrdinalIgnoreCase))).ToList();

        if (matching.Count == 0)
            throw new ValidationException(failMessage);

        if (matching.Any(r => r.Status == CompatibilityStatus.Incompatible))
            throw new ValidationException(failMessage + " Pravidlo zakazuje použitie.");

        var conditional = matching.Where(r => r.Status == CompatibilityStatus.Conditional).ToList();
        if (conditional.Count > 0 && conditional.All(r => string.IsNullOrWhiteSpace(r.RestrictionText))
            && !matching.Any(r => r.Status == CompatibilityStatus.Compatible))
            throw new ValidationException(failMessage + " Podmienená kompatibilita vyžaduje restriction text.");

        if (!matching.Any(r => r.Status is CompatibilityStatus.Compatible or CompatibilityStatus.Conditional))
            throw new ValidationException(failMessage);

        return Task.CompletedTask;
    }

    private static void ValidateCompatRequest(CreateCompatibilityRuleRequest request)
    {
        if (request.UasId is null && string.IsNullOrWhiteSpace(request.UasTypeCode))
            throw new ValidationException("Kompatibilita musí smerovať na UAS alebo typ UAS.");
        if (request.Status is CompatibilityStatus.Conditional or CompatibilityStatus.Incompatible
            && string.IsNullOrWhiteSpace(request.RestrictionText))
            throw new ValidationException("Restriction text je povinný pri podmienenej alebo nekompatibilnej väzbe.");
    }

    private static CompatibilityRuleDto MapCompat(
        Guid id, Guid assetId, Guid? uasId, string? typeCode, CompatibilityStatus status,
        DateTimeOffset from, DateTimeOffset? to, string? restriction) =>
        new(id, assetId, uasId, typeCode, status, from, to, restriction);

    private async Task<Uas> LoadUasAsync(Guid id, CancellationToken cancellationToken)
    {
        return await db.UasAircraft.Include(x => x.OwningOrgUnit)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("UAS nebol nájdený.");
    }

    private void EnsureUasOrgAccess(Uas uas)
    {
        if (uas.OwningOrgUnit is not null)
            policy.EnsureOrgAccess(uas.OwningOrgUnit.Path);
        else if (!policy.CanAccessBusinessData())
            throw new ForbiddenException("Prístup zamietnutý.");
    }

    private static UasDto MapUas(Uas x) =>
        new(x.Id, x.RegistrationNumber, x.SerialNumber, x.Manufacturer, x.Model, x.UasTypeCode, x.Status,
            x.OwningOrgUnitId, x.OwningOrgUnit?.Name, x.Notes, x.RegisteredAt, x.SubmittedByAccountId,
            x.TotalFlightCount, x.TotalFlightMinutes, x.IsActive, x.RowVersion);

    private static ComponentDto MapComponent(Component x) =>
        new(x.Id, x.InternalNumber, x.SerialNumber, x.Name, x.ComponentTypeCode, x.Status, x.OwningOrgUnitId, x.IsActive, x.RowVersion);

    private static BatteryDto MapBattery(Battery x) =>
        new(x.Id, x.InternalNumber, x.SerialNumber, x.Manufacturer, x.Model, x.CompatibleUasTypeCodes, x.Status,
            x.OwningOrgUnitId, x.CycleCount, x.CapacityPercent, x.HealthPercent, x.IsActive, x.RowVersion);
}
