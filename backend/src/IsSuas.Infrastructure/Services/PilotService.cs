using IsSuas.Application.Abstractions;
using IsSuas.Application.Pilots;
using IsSuas.Application.Policies;
using IsSuas.Domain.Enums;
using IsSuas.Domain.Identity;
using IsSuas.Domain.Pilots;
using IsSuas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IsSuas.Infrastructure.Services;

public interface IPilotService
{
    Task<IReadOnlyList<PilotDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<PilotDetailDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PilotDto> CreateAsync(CreatePilotRequest request, CancellationToken cancellationToken = default);
    Task<PilotDto> ChangeStatusAsync(Guid id, ChangePilotStatusRequest request, CancellationToken cancellationToken = default);
    Task<PilotQualificationDto> AddQualificationAsync(Guid pilotId, CreatePilotQualificationRequest request, CancellationToken cancellationToken = default);
    Task<QualificationVerificationDto> AddQualificationVerificationAsync(Guid pilotId, Guid qualificationId, CreateQualificationVerificationRequest request, CancellationToken cancellationToken = default);
    Task<PilotCertificateDto> AddCertificateAsync(Guid pilotId, CreatePilotCertificateRequest request, CancellationToken cancellationToken = default);
    Task<PilotCertificateDto> IssueCertificateAsync(Guid pilotId, Guid certificateId, IssuePilotCertificateRequest request, CancellationToken cancellationToken = default);
    Task<CertificateScopeDto> AddCertificateScopeAsync(Guid pilotId, Guid certificateId, CreateCertificateScopeRequest request, CancellationToken cancellationToken = default);
    Task<PilotTrainingDto> AddTrainingAsync(Guid pilotId, CreatePilotTrainingRequest request, CancellationToken cancellationToken = default);
    Task<PersonFunctionAssignmentDto> AssignFunctionAsync(Guid pilotId, CreatePersonFunctionAssignmentRequest request, CancellationToken cancellationToken = default);
    Task<PilotRestrictionDto> AddRestrictionAsync(Guid pilotId, CreatePilotRestrictionRequest request, CancellationToken cancellationToken = default);
    Task<PilotRestrictionDto> EndRestrictionAsync(Guid pilotId, Guid restrictionId, EndPilotRestrictionRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrainingCatalogItemDto>> ListCatalogAsync(CancellationToken cancellationToken = default);
    Task<TrainingCatalogItemDto> CreateCatalogItemAsync(CreateTrainingCatalogItemRequest request, CancellationToken cancellationToken = default);
    Task<TrainingActivityDto> CreateActivityAsync(CreateTrainingActivityRequest request, CancellationToken cancellationToken = default);
    Task<TrainingActivityDto> GetActivityAsync(Guid activityId, CancellationToken cancellationToken = default);
    Task<TrainingParticipationDto> RegisterParticipationAsync(Guid activityId, CreateTrainingParticipationRequest request, CancellationToken cancellationToken = default);
    Task<TrainingActivityResultDto> RecordActivityResultAsync(Guid activityId, Guid participationId, RecordTrainingActivityResultRequest request, CancellationToken cancellationToken = default);
    Task<PilotEligibilityCheckDto> CheckEligibilityAsync(Guid pilotId, CancellationToken cancellationToken = default);
}

public sealed class PilotService(
    IsSuasDbContext db,
    IAuthorizationPolicyService policy,
    ICurrentUser currentUser,
    IAuditWriter audit,
    IClock clock) : IPilotService
{
    public async Task<IReadOnlyList<PilotDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.PilotsRead);
        if (!policy.CanAccessBusinessData())
            throw new ForbiddenException("Systémový administrátor nemá automatický prístup k vecnému obsahu.");

        var pilots = await db.Pilots.AsNoTracking()
            .Include(p => p.Person)
            .Include(p => p.OrgUnit)
            .Include(p => p.Certificates)
            .Include(p => p.Restrictions)
            .Where(p => p.IsActive)
            .OrderBy(p => p.Person.LastName)
            .ThenBy(p => p.Person.FirstName)
            .ToListAsync(cancellationToken);

        return pilots
            .Where(p => policy.CanAccessOrgUnit(p.OrgUnit.Path))
            .Select(Map)
            .ToList();
    }

    public async Task<PilotDetailDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.PilotsRead);
        var pilot = await LoadDetailAsync(id, cancellationToken);
        policy.EnsureOrgAccess(pilot.OrgUnit.Path);

        var functions = await db.PersonFunctionAssignments.AsNoTracking()
            .Where(f => f.PersonId == pilot.PersonId && f.IsActive)
            .OrderByDescending(f => f.ValidFrom)
            .ToListAsync(cancellationToken);

        return new PilotDetailDto(
            Map(pilot),
            pilot.Qualifications.OrderByDescending(q => q.ValidFrom).Select(MapQualification).ToList(),
            pilot.Certificates.OrderByDescending(c => c.Version).Select(MapCertificate).ToList(),
            pilot.Trainings.OrderByDescending(t => t.CompletedAt).Select(MapTraining).ToList(),
            pilot.StatusHistory.OrderByDescending(h => h.ChangedAt).Select(h => new PilotStatusHistoryDto(
                h.Id, h.FromStatus, h.ToStatus, h.Reason, h.ChangedAt)).ToList(),
            functions.Select(MapFunction).ToList(),
            pilot.Restrictions.OrderByDescending(r => r.ValidFrom).Select(MapRestriction).ToList());
    }

    public async Task<PilotDto> CreateAsync(CreatePilotRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.PilotsWrite);
        var orgId = request.OrgUnitId;
        if (!currentUser.Roles.Any(r => r.RoleCode == nameof(SystemRole.CentralGestor)))
        {
            var home = currentUser.Roles.FirstOrDefault(r => r.GrantsBusinessDataAccess)
                ?? throw new ValidationException("Účet nemá priradený útvar.");
            orgId = home.OrgUnitId;
        }
        var org = await db.OrgUnits.FirstOrDefaultAsync(o => o.Id == orgId && o.IsActive, cancellationToken)
            ?? throw new NotFoundException("Útvar nebol nájdený.");
        policy.EnsureOrgAccess(org.Path);

        var person = new Person
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = request.Email,
            HomeOrgUnitId = org.Id,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.Persons.Add(person);

        var pilot = new Pilot
        {
            Person = person,
            OrgUnitId = org.Id,
            LicenseNumber = request.LicenseNumber,
            Notes = request.Notes,
            Status = PilotStatus.Draft,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.Pilots.Add(pilot);
        await db.SaveChangesAsync(cancellationToken);

        pilot.OrgUnit = org;
        await audit.WriteAsync("Pilot.Create", nameof(Pilot), pilot.Id, null, Map(pilot), cancellationToken: cancellationToken);
        return Map(pilot);
    }

    public async Task<PilotDto> ChangeStatusAsync(Guid id, ChangePilotStatusRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.PilotsWrite);
        var pilot = await LoadDetailAsync(id, cancellationToken);
        policy.EnsureOrgAccess(pilot.OrgUnit.Path);
        if (pilot.RowVersion != request.RowVersion)
            throw new ConflictException("Záznam bol medzitým zmenený iným používateľom.");

        var before = Map(pilot);
        var from = pilot.Status;
        pilot.Status = request.Status;
        if (request.Status == PilotStatus.Inactive)
            pilot.IsActive = false;
        pilot.UpdatedBy = currentUser.AccountId;
        RecalculateCurrencyFlags(pilot);

        db.PilotStatusHistories.Add(new PilotStatusHistory
        {
            PilotId = pilot.Id,
            FromStatus = from,
            ToStatus = request.Status,
            Reason = request.Reason,
            ChangedAt = clock.UtcNow,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        });

        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("Pilot.ChangeStatus", nameof(Pilot), pilot.Id, before, Map(pilot), cancellationToken: cancellationToken);
        return Map(pilot);
    }

    public async Task<PilotQualificationDto> AddQualificationAsync(Guid pilotId, CreatePilotQualificationRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.PilotsWrite);
        var pilot = await LoadDetailAsync(pilotId, cancellationToken);
        policy.EnsureOrgAccess(pilot.OrgUnit.Path);

        var q = new PilotQualification
        {
            PilotId = pilotId,
            QualificationCode = request.QualificationCode.Trim(),
            NameSk = request.NameSk.Trim(),
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            Issuer = request.Issuer,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.PilotQualifications.Add(q);
        await db.SaveChangesAsync(cancellationToken);
        return MapQualification(q);
    }

    public async Task<QualificationVerificationDto> AddQualificationVerificationAsync(
        Guid pilotId, Guid qualificationId, CreateQualificationVerificationRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.PilotsWrite);
        var pilot = await LoadDetailAsync(pilotId, cancellationToken);
        policy.EnsureOrgAccess(pilot.OrgUnit.Path);

        var qualification = pilot.Qualifications.FirstOrDefault(q => q.Id == qualificationId)
            ?? throw new NotFoundException("Doklad nebol nájdený.");

        if ((request.Result is QualificationVerificationResult.Rejected or QualificationVerificationResult.Inconclusive)
            && string.IsNullOrWhiteSpace(request.Notes))
            throw new ValidationException("Pri neúspešnom alebo nejednoznačnom overení je povinná poznámka.");
        if (currentUser.AccountId is null)
            throw new ForbiddenException("Chýba identifikácia používateľa.");

        var verification = new QualificationDocumentVerification
        {
            QualificationId = qualificationId,
            VerificationDate = clock.UtcNow,
            VerificationMethodCode = request.VerificationMethodCode.Trim(),
            Result = request.Result,
            VerifiedByAccountId = currentUser.AccountId.Value,
            VerifiedSource = request.VerifiedSource,
            NextVerificationDate = request.NextVerificationDate,
            Notes = request.Notes,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.QualificationDocumentVerifications.Add(verification);
        await db.SaveChangesAsync(cancellationToken);
        return MapVerification(verification);
    }

    public async Task<PilotCertificateDto> AddCertificateAsync(Guid pilotId, CreatePilotCertificateRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.PilotsWrite);
        var pilot = await LoadDetailAsync(pilotId, cancellationToken);
        policy.EnsureOrgAccess(pilot.OrgUnit.Path);

        var version = pilot.Certificates.Where(c => c.CertificateTypeCode == request.CertificateTypeCode).Select(c => c.Version).DefaultIfEmpty(0).Max() + 1;
        var cert = new PilotCertificate
        {
            PilotId = pilotId,
            CertificateTypeCode = request.CertificateTypeCode.Trim(),
            TitleSk = request.TitleSk.Trim(),
            ExternalNumber = request.ExternalNumber,
            Version = version,
            Status = CertificateStatus.Draft,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            Notes = request.Notes,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.PilotCertificates.Add(cert);
        await db.SaveChangesAsync(cancellationToken);
        return MapCertificate(cert);
    }

    public async Task<PilotCertificateDto> IssueCertificateAsync(Guid pilotId, Guid certificateId, IssuePilotCertificateRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.PilotsWrite);
        var pilot = await LoadDetailAsync(pilotId, cancellationToken);
        policy.EnsureOrgAccess(pilot.OrgUnit.Path);

        var cert = pilot.Certificates.FirstOrDefault(c => c.Id == certificateId)
            ?? throw new NotFoundException("Osvedčenie nebolo nájdené.");
        if (cert.Status != CertificateStatus.Draft)
            throw new ValidationException("Vydávať možno iba koncept osvedčenia.");

        foreach (var older in pilot.Certificates.Where(c =>
                     c.CertificateTypeCode == cert.CertificateTypeCode
                     && c.Id != cert.Id
                     && c.Status == CertificateStatus.Issued))
        {
            older.Status = CertificateStatus.Superseded;
            older.UpdatedBy = currentUser.AccountId;
            cert.SupersedesCertificateId = older.Id;
        }

        cert.Status = CertificateStatus.Issued;
        cert.IssuedAt = clock.UtcNow;
        cert.ValidFrom ??= clock.UtcNow;
        cert.UpdatedBy = currentUser.AccountId;
        cert.Notes = string.IsNullOrWhiteSpace(request.Reason) ? cert.Notes : request.Reason;

        if (pilot.Status == PilotStatus.Draft)
        {
            var from = pilot.Status;
            pilot.Status = PilotStatus.Fit;
            db.PilotStatusHistories.Add(new PilotStatusHistory
            {
                PilotId = pilot.Id,
                FromStatus = from,
                ToStatus = PilotStatus.Fit,
                Reason = "Vydanie platného osvedčenia",
                ChangedAt = clock.UtcNow,
                CreatedBy = currentUser.AccountId,
                UpdatedBy = currentUser.AccountId
            });
        }

        RecalculateCurrencyFlags(pilot);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("PilotCertificate.Issue", nameof(PilotCertificate), cert.Id, null,
            MapCertificate(cert), cancellationToken: cancellationToken);

        return MapCertificate(cert);
    }

    public async Task<CertificateScopeDto> AddCertificateScopeAsync(
        Guid pilotId, Guid certificateId, CreateCertificateScopeRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.PilotsWrite);
        var pilot = await LoadDetailAsync(pilotId, cancellationToken);
        policy.EnsureOrgAccess(pilot.OrgUnit.Path);

        var cert = pilot.Certificates.FirstOrDefault(c => c.Id == certificateId)
            ?? throw new NotFoundException("Osvedčenie nebolo nájdené.");

        var scope = new CertificateScope
        {
            CertificateId = certificateId,
            ScopeTypeCode = request.ScopeTypeCode.Trim(),
            UasTypeCode = request.UasTypeCode,
            OperationTypeCode = request.OperationTypeCode,
            FunctionTypeCode = request.FunctionTypeCode,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            RestrictionText = request.RestrictionText,
            Status = CertificateScopeStatus.Active,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.CertificateScopes.Add(scope);
        await db.SaveChangesAsync(cancellationToken);
        return MapScope(scope);
    }

    public async Task<PilotTrainingDto> AddTrainingAsync(Guid pilotId, CreatePilotTrainingRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.PilotsWrite);
        var pilot = await LoadDetailAsync(pilotId, cancellationToken);
        policy.EnsureOrgAccess(pilot.OrgUnit.Path);

        var training = new PilotTraining
        {
            PilotId = pilotId,
            TrainingCode = request.TrainingCode.Trim(),
            TitleSk = request.TitleSk.Trim(),
            CompletedAt = request.CompletedAt,
            Result = request.Result,
            InstructorName = request.InstructorName,
            ValidUntil = request.ValidUntil,
            Notes = request.Notes,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.PilotTrainings.Add(training);
        RecalculateCurrencyFlags(pilot);
        await db.SaveChangesAsync(cancellationToken);
        return MapTraining(training);
    }

    public async Task<PersonFunctionAssignmentDto> AssignFunctionAsync(
        Guid pilotId, CreatePersonFunctionAssignmentRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.PilotsWrite);
        var pilot = await LoadDetailAsync(pilotId, cancellationToken);
        policy.EnsureOrgAccess(pilot.OrgUnit.Path);

        var org = await db.OrgUnits.FirstOrDefaultAsync(o => o.Id == request.OrganizationOrgUnitId && o.IsActive, cancellationToken)
            ?? throw new NotFoundException("Organizačný útvar nebol nájdený.");
        policy.EnsureOrgAccess(org.Path);

        if (request.AssignmentScopeTypeCode.Equals("UAS", StringComparison.OrdinalIgnoreCase) && request.UasId is null)
            throw new ValidationException("Pri rozsahu UAS je povinné uasId.");
        if (currentUser.AccountId is null)
            throw new ForbiddenException("Chýba identifikácia používateľa.");

        var assignment = new PersonFunctionAssignment
        {
            PersonId = pilot.PersonId,
            FunctionTypeCode = request.FunctionTypeCode.Trim(),
            OrganizationOrgUnitId = request.OrganizationOrgUnitId,
            OrgUnitId = request.OrgUnitId,
            AssignmentScopeTypeCode = request.AssignmentScopeTypeCode.Trim(),
            UasId = request.UasId,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            Status = PersonFunctionAssignmentStatus.Active,
            AssignedByAccountId = currentUser.AccountId.Value,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.PersonFunctionAssignments.Add(assignment);
        await db.SaveChangesAsync(cancellationToken);
        return MapFunction(assignment);
    }

    public async Task<PilotRestrictionDto> AddRestrictionAsync(
        Guid pilotId, CreatePilotRestrictionRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.PilotsWrite);
        var pilot = await LoadDetailAsync(pilotId, cancellationToken);
        policy.EnsureOrgAccess(pilot.OrgUnit.Path);

        var restriction = new PilotRestriction
        {
            PilotId = pilotId,
            RestrictionTypeCode = request.RestrictionTypeCode.Trim(),
            Description = request.Description.Trim(),
            SeverityCode = request.SeverityCode.Trim(),
            BlocksOperation = request.BlocksOperation,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            ImposedByPersonId = request.ImposedByPersonId,
            Status = PilotRestrictionStatus.Active,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.PilotRestrictions.Add(restriction);
        await db.SaveChangesAsync(cancellationToken);
        return MapRestriction(restriction);
    }

    public async Task<PilotRestrictionDto> EndRestrictionAsync(
        Guid pilotId, Guid restrictionId, EndPilotRestrictionRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.PilotsWrite);
        var pilot = await LoadDetailAsync(pilotId, cancellationToken);
        policy.EnsureOrgAccess(pilot.OrgUnit.Path);

        var restriction = pilot.Restrictions.FirstOrDefault(r => r.Id == restrictionId)
            ?? throw new NotFoundException("Obmedzenie nebolo nájdené.");
        if (restriction.Status != PilotRestrictionStatus.Active)
            throw new ValidationException("Ukončiť možno iba aktívne obmedzenie.");
        if (string.IsNullOrWhiteSpace(request.TerminationReason))
            throw new ValidationException("Dôvod ukončenia je povinný.");

        restriction.Status = PilotRestrictionStatus.Ended;
        restriction.ValidTo ??= clock.UtcNow;
        restriction.TerminationReason = request.TerminationReason.Trim();
        restriction.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        return MapRestriction(restriction);
    }

    public async Task<IReadOnlyList<TrainingCatalogItemDto>> ListCatalogAsync(CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.PilotsRead);
        var items = await db.TrainingCatalogItems.AsNoTracking()
            .Where(i => i.IsActive)
            .OrderBy(i => i.ActivityCode)
            .ThenByDescending(i => i.VersionNumber)
            .ToListAsync(cancellationToken);
        return items.Select(MapCatalog).ToList();
    }

    public async Task<TrainingCatalogItemDto> CreateCatalogItemAsync(
        CreateTrainingCatalogItemRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.PilotsWrite);

        var existingVersions = await db.TrainingCatalogItems
            .Where(i => i.ActivityCode == request.ActivityCode.Trim())
            .Select(i => i.VersionNumber)
            .ToListAsync(cancellationToken);
        var version = existingVersions.DefaultIfEmpty(0).Max() + 1;

        var item = new TrainingCatalogItem
        {
            ActivityCode = request.ActivityCode.Trim(),
            ActivityName = request.ActivityName.Trim(),
            ActivityTypeCode = request.ActivityTypeCode.Trim(),
            VersionNumber = version,
            Description = request.Description.Trim(),
            MinimumDurationHours = request.MinimumDurationHours,
            RecurrencePeriodMonths = request.RecurrencePeriodMonths,
            ExaminationRequired = request.ExaminationRequired,
            PassingScorePercent = request.PassingScorePercent,
            PracticalPartRequired = request.PracticalPartRequired,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            Status = TrainingCatalogStatus.Active,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.TrainingCatalogItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return MapCatalog(item);
    }

    public async Task<TrainingActivityDto> CreateActivityAsync(
        CreateTrainingActivityRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.PilotsWrite);

        var catalog = await db.TrainingCatalogItems.FirstOrDefaultAsync(c => c.Id == request.CatalogItemId && c.IsActive, cancellationToken)
            ?? throw new NotFoundException("Katalógová položka nebola nájdená.");
        if (catalog.Status != TrainingCatalogStatus.Active)
            throw new ValidationException("Aktivitu možno vytvoriť iba z aktívnej katalógovej položky.");

        var org = await db.OrgUnits.FirstOrDefaultAsync(o => o.Id == request.OrganizationOrgUnitId && o.IsActive, cancellationToken)
            ?? throw new NotFoundException("Organizačný útvar nebol nájdený.");
        policy.EnsureOrgAccess(org.Path);

        if (request.PlannedEndAt < request.PlannedStartAt)
            throw new ValidationException("Plánovaný koniec musí byť po začiatku.");

        if (await db.TrainingActivities.AnyAsync(a => a.ActivityNumber == request.ActivityNumber.Trim(), cancellationToken))
            throw new ConflictException("Číslo aktivity už existuje.");

        var activity = new TrainingActivity
        {
            CatalogItemId = catalog.Id,
            ActivityNumber = request.ActivityNumber.Trim(),
            OrganizationOrgUnitId = request.OrganizationOrgUnitId,
            OrgUnitId = request.OrgUnitId,
            PlannedStartAt = request.PlannedStartAt,
            PlannedEndAt = request.PlannedEndAt,
            Location = request.Location,
            DeliveryFormCode = request.DeliveryFormCode.Trim(),
            InstructorPersonId = request.InstructorPersonId,
            ExaminerPersonId = request.ExaminerPersonId,
            MaximumParticipants = request.MaximumParticipants,
            Status = TrainingActivityStatus.Open,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.TrainingActivities.Add(activity);
        await db.SaveChangesAsync(cancellationToken);
        return await GetActivityAsync(activity.Id, cancellationToken);
    }

    public async Task<TrainingActivityDto> GetActivityAsync(Guid activityId, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.PilotsRead);
        var activity = await LoadActivityAsync(activityId, cancellationToken);
        policy.EnsureOrgAccess(activity.OrganizationOrgUnit.Path);
        return MapActivity(activity);
    }

    public async Task<TrainingParticipationDto> RegisterParticipationAsync(
        Guid activityId, CreateTrainingParticipationRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.PilotsWrite);
        var activity = await LoadActivityAsync(activityId, cancellationToken);
        policy.EnsureOrgAccess(activity.OrganizationOrgUnit.Path);

        if (activity.Status is TrainingActivityStatus.Completed or TrainingActivityStatus.Cancelled)
            throw new ValidationException("Na ukončenú alebo zrušenú aktivitu nie je možné prihlásiť účastníka.");

        _ = await db.Persons.FirstOrDefaultAsync(p => p.Id == request.PersonId && p.IsActive, cancellationToken)
            ?? throw new NotFoundException("Osoba nebola nájdená.");

        if (activity.Participations.Any(p => p.PersonId == request.PersonId && p.IsActive))
            throw new ConflictException("Osoba je už prihlásená na túto aktivitu.");

        if (activity.MaximumParticipants is not null
            && activity.Participations.Count(p => p.IsActive
                && p.RegistrationStatus is TrainingRegistrationStatus.Registered or TrainingRegistrationStatus.Confirmed)
                >= activity.MaximumParticipants)
            throw new ValidationException("Bol dosiahnutý maximálny počet účastníkov.");

        var participation = new TrainingParticipation
        {
            ActivityId = activityId,
            PersonId = request.PersonId,
            ParticipationRoleCode = request.ParticipationRoleCode.Trim(),
            RegistrationDate = clock.UtcNow,
            RegistrationStatus = TrainingRegistrationStatus.Confirmed,
            CompletionStatus = TrainingCompletionStatus.NotStarted,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.TrainingParticipations.Add(participation);
        await db.SaveChangesAsync(cancellationToken);
        return MapParticipation(participation);
    }

    public async Task<TrainingActivityResultDto> RecordActivityResultAsync(
        Guid activityId, Guid participationId, RecordTrainingActivityResultRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.PilotsWrite);
        var activity = await LoadActivityAsync(activityId, cancellationToken);
        policy.EnsureOrgAccess(activity.OrganizationOrgUnit.Path);

        var participation = activity.Participations.FirstOrDefault(p => p.Id == participationId)
            ?? throw new NotFoundException("Účasť nebola nájdená.");
        if (participation.Result is not null)
            throw new ConflictException("Výsledok účasti už existuje.");

        if (request.AttemptNumber < 1)
            throw new ValidationException("Číslo pokusu musí byť väčšie ako nula.");

        var result = new TrainingActivityResult
        {
            ParticipationId = participationId,
            Result = request.Result,
            AttemptNumber = request.AttemptNumber,
            TheoreticalScorePercent = request.TheoreticalScorePercent,
            PracticalScorePercent = request.PracticalScorePercent,
            OverallScorePercent = request.OverallScorePercent,
            EvaluatedAt = clock.UtcNow,
            EvaluatedByPersonId = request.EvaluatedByPersonId,
            ValidUntil = request.ValidUntil,
            Notes = request.Notes,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.TrainingActivityResults.Add(result);
        participation.CompletionStatus = TrainingCompletionStatus.Completed;
        participation.AttendanceStatus ??= TrainingAttendanceStatus.Present;
        participation.UpdatedBy = currentUser.AccountId;

        if (request.SyncToPilotCard)
        {
            var pilot = await db.Pilots
                .Include(p => p.Certificates)
                .Include(p => p.Trainings)
                .Include(p => p.Restrictions)
                .FirstOrDefaultAsync(p => p.PersonId == participation.PersonId && p.IsActive, cancellationToken)
                ?? throw new ValidationException("Pre sync výsledku na kartu pilota musí existovať aktívny pilot pre danú osobu.");

            var training = new PilotTraining
            {
                PilotId = pilot.Id,
                TrainingCode = activity.CatalogItem.ActivityCode,
                TitleSk = activity.CatalogItem.ActivityName,
                CompletedAt = result.EvaluatedAt ?? clock.UtcNow,
                Result = result.Result,
                ValidUntil = result.ValidUntil,
                Notes = result.Notes,
                TrainingParticipationId = participation.Id,
                CreatedBy = currentUser.AccountId,
                UpdatedBy = currentUser.AccountId
            };
            db.PilotTrainings.Add(training);
            RecalculateCurrencyFlags(pilot);
        }

        await db.SaveChangesAsync(cancellationToken);
        return MapResult(result);
    }

    public async Task<PilotEligibilityCheckDto> CheckEligibilityAsync(Guid pilotId, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.PilotsRead);
        var pilot = await LoadDetailAsync(pilotId, cancellationToken);
        policy.EnsureOrgAccess(pilot.OrgUnit.Path);

        var eligible = PilotEligibility.CanAssignToOperation(pilot, clock.UtcNow);
        var reason = eligible
            ? "Pilot je spôsobilý na priradenie do operácie."
            : BuildIneligibilityReason(pilot, clock.UtcNow);
        return new PilotEligibilityCheckDto(pilot.Id, eligible, reason);
    }

    private async Task<Pilot> LoadDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        return await db.Pilots
            .Include(p => p.Person)
            .Include(p => p.OrgUnit)
            .Include(p => p.Qualifications).ThenInclude(q => q.Verifications)
            .Include(p => p.Certificates).ThenInclude(c => c.Scopes)
            .Include(p => p.Trainings)
            .Include(p => p.StatusHistory)
            .Include(p => p.Restrictions)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException("Pilot nebol nájdený.");
    }

    private async Task<TrainingActivity> LoadActivityAsync(Guid id, CancellationToken cancellationToken)
    {
        return await db.TrainingActivities
            .Include(a => a.CatalogItem)
            .Include(a => a.OrganizationOrgUnit)
            .Include(a => a.Participations).ThenInclude(p => p.Result)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new NotFoundException("Výcviková aktivita nebola nájdená.");
    }

    private void RecalculateCurrencyFlags(Pilot pilot)
    {
        var now = clock.UtcNow;
        var trainingUntil = pilot.Trainings
            .Where(t => t.IsActive && t.Result == TrainingResult.Passed && t.ValidUntil is not null)
            .Select(t => t.ValidUntil)
            .Max();
        var certUntil = pilot.Certificates
            .Where(c => c.IsActive && c.Status == CertificateStatus.Issued && c.ValidTo is not null)
            .Select(c => c.ValidTo)
            .Min();

        DateTimeOffset? until = null;
        if (trainingUntil is not null && certUntil is not null)
            until = trainingUntil < certUntil ? trainingUntil : certUntil;
        else
            until = trainingUntil ?? certUntil;

        pilot.CurrencyValidUntil = until;

        if (pilot.Status is PilotStatus.Fit or PilotStatus.Active or PilotStatus.Expiring)
        {
            if (until is not null && until < now)
                pilot.Status = PilotStatus.Unfit;
            else if (until is not null && until < now.AddDays(30))
                pilot.Status = PilotStatus.Expiring;
            else if (PilotEligibility.CanAssignToOperation(pilot, now) && pilot.Status != PilotStatus.Suspended)
                pilot.Status = PilotStatus.Fit;
        }
    }

    private static string BuildIneligibilityReason(Pilot pilot, DateTimeOffset at)
    {
        if (!pilot.IsActive || pilot.Status is PilotStatus.Draft or PilotStatus.Unfit or PilotStatus.Suspended or PilotStatus.Inactive)
            return $"Pilot nie je v spôsobilom stave ({pilot.Status}).";
        if (PilotEligibility.HasBlockingRestriction(pilot, at))
            return "Pilot má aktívne blokujúce obmedzenie.";
        if (!pilot.Certificates.Any(c =>
                c.IsActive && c.Status == CertificateStatus.Issued
                && (c.ValidFrom is null || c.ValidFrom <= at)
                && (c.ValidTo is null || c.ValidTo >= at)))
            return "Pilot nemá platné vydané osvedčenie.";
        if (pilot.CurrencyValidUntil is not null && pilot.CurrencyValidUntil < at)
            return "Kondičná spôsobilosť vypršala.";
        return "Pilot nie je spôsobilý.";
    }

    private PilotDto Map(Pilot p)
    {
        var now = clock.UtcNow;
        var activeCerts = p.Certificates
            .Where(c => c.IsActive && c.Status == CertificateStatus.Issued
                && (c.ValidFrom is null || c.ValidFrom <= now)
                && (c.ValidTo is null || c.ValidTo >= now))
            .ToList();

        var certCodes = activeCerts
            .Select(c => NormalizeCertCode(c.CertificateTypeCode))
            .Where(c => c is "A1_A3" or "A2" or "STS")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c switch { "A1_A3" => 0, "A2" => 1, _ => 2 })
            .ToList();

        var certItems = activeCerts
            .Select(c => new PilotCertListItemDto(
                NormalizeCertCode(c.CertificateTypeCode),
                c.ExternalNumber,
                c.ValidTo,
                c.MtowOrValidityText))
            .Where(c => c.CertificateTypeCode is "A1_A3" or "A2" or "STS")
            .GroupBy(c => c.CertificateTypeCode, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(c => c.CertificateTypeCode switch { "A1_A3" => 0, "A2" => 1, _ => 2 })
            .ToList();

        return new(p.Id, p.PersonId, p.Person.FirstName, p.Person.LastName, p.Person.Email, p.OrgUnitId, p.OrgUnit.Name,
            p.LicenseNumber, p.PersonalBadgeNumber, p.GestorCertificateNumber, p.LogbookNumber,
            p.Status, p.CurrencyValidUntil, p.LastFlightAt,
            PilotEligibility.CanAssignToOperation(p, now), p.IsActive, p.RowVersion, certCodes, certItems);
    }

    private static string NormalizeCertCode(string code)
    {
        var c = (code ?? "").Trim().ToUpperInvariant().Replace('-', '_').Replace('/', '_').Replace(' ', '_');
        if (c is "A1A3" or "A1_A3" or "OPEN_A1_A3" or "A1" or "A3") return "A1_A3";
        if (c is "A2" or "OPEN_A2") return "A2";
        if (c is "STS" or "STS01" or "STS_01" or "STS02" or "STS_02" or "STANDARD_SCENARIO") return "STS";
        return c;
    }

    private static PilotQualificationDto MapQualification(PilotQualification q) =>
        new(q.Id, q.QualificationCode, q.NameSk, q.ValidFrom, q.ValidTo, q.Issuer, q.IsActive,
            q.Verifications.OrderByDescending(v => v.VerificationDate).Select(MapVerification).ToList());

    private static QualificationVerificationDto MapVerification(QualificationDocumentVerification v) =>
        new(v.Id, v.VerificationDate, v.VerificationMethodCode, v.Result, v.VerifiedByAccountId,
            v.VerifiedSource, v.NextVerificationDate, v.Notes, v.IsActive);

    private static PilotCertificateDto MapCertificate(PilotCertificate c) =>
        new(c.Id, c.CertificateTypeCode, c.TitleSk, c.ExternalNumber, c.Version, c.Status,
            c.IssuedAt, c.ValidFrom, c.ValidTo, c.IsActive,
            c.Scopes.OrderByDescending(s => s.ValidFrom).Select(MapScope).ToList(),
            c.MtowOrValidityText);

    private static CertificateScopeDto MapScope(CertificateScope s) =>
        new(s.Id, s.ScopeTypeCode, s.UasTypeCode, s.OperationTypeCode, s.FunctionTypeCode,
            s.ValidFrom, s.ValidTo, s.RestrictionText, s.Status, s.IsActive);

    private static PilotTrainingDto MapTraining(PilotTraining t) =>
        new(t.Id, t.TrainingCode, t.TitleSk, t.CompletedAt, t.Result, t.InstructorName, t.ValidUntil, t.IsActive,
            t.TrainingParticipationId);

    private static PersonFunctionAssignmentDto MapFunction(PersonFunctionAssignment f) =>
        new(f.Id, f.PersonId, f.FunctionTypeCode, f.OrganizationOrgUnitId, f.OrgUnitId, f.AssignmentScopeTypeCode,
            f.UasId, f.ValidFrom, f.ValidTo, f.Status, f.AssignedByAccountId, f.TerminationReason, f.IsActive);

    private static PilotRestrictionDto MapRestriction(PilotRestriction r) =>
        new(r.Id, r.PilotId, r.RestrictionTypeCode, r.Description, r.SeverityCode, r.BlocksOperation,
            r.ValidFrom, r.ValidTo, r.ImposedByPersonId, r.Status, r.TerminationReason, r.IsActive);

    private static TrainingCatalogItemDto MapCatalog(TrainingCatalogItem i) =>
        new(i.Id, i.ActivityCode, i.ActivityName, i.ActivityTypeCode, i.VersionNumber, i.Description,
            i.MinimumDurationHours, i.RecurrencePeriodMonths, i.ExaminationRequired, i.PassingScorePercent,
            i.PracticalPartRequired, i.ValidFrom, i.ValidTo, i.Status, i.IsActive);

    private static TrainingActivityDto MapActivity(TrainingActivity a) =>
        new(a.Id, a.CatalogItemId, a.ActivityNumber, a.OrganizationOrgUnitId, a.OrgUnitId,
            a.PlannedStartAt, a.PlannedEndAt, a.ActualStartAt, a.ActualEndAt, a.Location, a.DeliveryFormCode,
            a.InstructorPersonId, a.ExaminerPersonId, a.MaximumParticipants, a.Status, a.CancellationReason, a.IsActive,
            a.Participations.OrderBy(p => p.RegistrationDate).Select(MapParticipation).ToList());

    private static TrainingParticipationDto MapParticipation(TrainingParticipation p) =>
        new(p.Id, p.ActivityId, p.PersonId, p.ParticipationRoleCode, p.RegistrationDate, p.RegistrationStatus,
            p.AttendanceStatus, p.AttendancePercentage, p.CompletionStatus, p.AbsenceReason, p.Notes, p.IsActive,
            p.Result is null ? null : MapResult(p.Result));

    private static TrainingActivityResultDto MapResult(TrainingActivityResult r) =>
        new(r.Id, r.ParticipationId, r.Result, r.AttemptNumber, r.TheoreticalScorePercent, r.PracticalScorePercent,
            r.OverallScorePercent, r.EvaluatedAt, r.EvaluatedByPersonId, r.ValidUntil, r.Notes, r.IsActive);
}
