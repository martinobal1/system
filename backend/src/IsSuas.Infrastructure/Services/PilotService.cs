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
    Task<PilotCertificateDto> GrantCertificateAsync(Guid pilotId, GrantPilotCertificateRequest request, CancellationToken cancellationToken = default);
    Task<PilotCertificateDto> RevokeCertificateAsync(Guid pilotId, Guid certificateId, RevokePilotCertificateRequest request, CancellationToken cancellationToken = default);
    Task<PilotCertificateDto> SuspendCertificateAsync(Guid pilotId, Guid certificateId, SuspendPilotCertificateRequest request, CancellationToken cancellationToken = default);
    Task<PilotCertificateDto> ResumeCertificateAsync(Guid pilotId, Guid certificateId, CancellationToken cancellationToken = default);
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

        await LiftExpiredCertificateSuspensionsAsync(cancellationToken);

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
        await LiftExpiredCertificateSuspensionsAsync(cancellationToken);
        var pilot = await LoadDetailAsync(id, cancellationToken);
        policy.EnsureOrgAccess(pilot.OrgUnit.Path);

        var functions = await db.PersonFunctionAssignments.AsNoTracking()
            .Where(f => f.PersonId == pilot.PersonId && f.IsActive)
            .OrderByDescending(f => f.ValidFrom)
            .ToListAsync(cancellationToken);

        return new PilotDetailDto(
            Map(pilot),
            pilot.Qualifications.OrderByDescending(q => q.ValidFrom).Select(MapQualification).ToList(),
            CurrentPnDCertificates(pilot).Select(MapCertificate).ToList(),
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

    public async Task<PilotCertificateDto> GrantCertificateAsync(
        Guid pilotId, GrantPilotCertificateRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.PilotsWrite);
        await LiftExpiredCertificateSuspensionsAsync(cancellationToken);
        var pilot = await LoadDetailAsync(pilotId, cancellationToken);
        policy.EnsureOrgAccess(pilot.OrgUnit.Path);

        var typeCode = NormalizeCertCode(request.CertificateTypeCode);
        if (typeCode is not ("A1_A3" or "A2" or "STS1" or "STS2"))
            throw new ValidationException("Povolené typy osvedčenia sú A1/A3, A2, STS1 a STS2.");

        var now = clock.UtcNow;
        var validFrom = request.ValidFrom ?? now;
        var chain = PrerequisiteChain(typeCode);
        PilotCertificate? granted = null;
        foreach (var step in chain)
        {
            var isTop = step == typeCode;
            granted = GrantIssuedStep(
                pilot,
                step,
                now,
                validFrom,
                request.ValidTo,
                isTop ? request.ExternalNumber : null,
                isTop ? request.Notes : $"Doplnené automaticky pri pridelení {CertificateTitle(typeCode)}",
                isTop ? request.MtowOrValidityText : null,
                forceNew: isTop);
        }

        if (pilot.Status == PilotStatus.Draft)
        {
            var from = pilot.Status;
            pilot.Status = PilotStatus.Fit;
            db.PilotStatusHistories.Add(new PilotStatusHistory
            {
                PilotId = pilot.Id,
                FromStatus = from,
                ToStatus = PilotStatus.Fit,
                Reason = "Pridelenie osvedčenia",
                ChangedAt = now,
                CreatedBy = currentUser.AccountId,
                UpdatedBy = currentUser.AccountId
            });
        }

        RecalculateCurrencyFlags(pilot);
        await SyncLevel3LicenseAsync(pilot, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("PilotCertificate.Grant", nameof(PilotCertificate), granted!.Id, null,
            MapCertificate(granted), cancellationToken: cancellationToken);
        return MapCertificate(granted);
    }

    private static IReadOnlyList<string> PrerequisiteChain(string typeCode) => typeCode switch
    {
        "STS2" => ["A1_A3", "A2", "STS1", "STS2"],
        "STS1" => ["A1_A3", "A2", "STS1"],
        "A2" => ["A1_A3", "A2"],
        _ => ["A1_A3"]
    };

    /// <summary>Odobratie nižšieho stupňa stiahne aj všetky vyššie (A1/A3 → A2, STS1, STS2).</summary>
    private static IReadOnlyList<string> DependentChain(string typeCode) => typeCode switch
    {
        "A1_A3" => ["A1_A3", "A2", "STS1", "STS2"],
        "A2" => ["A2", "STS1", "STS2"],
        "STS1" => ["STS1", "STS2"],
        _ => ["STS2"]
    };

    private bool HasIssued(Pilot pilot, string typeCode, DateTimeOffset at) =>
        pilot.Certificates.Any(c =>
            NormalizeCertCode(c.CertificateTypeCode) == typeCode
            && c.IsActive
            && c.Status == CertificateStatus.Issued
            && (c.ValidFrom is null || c.ValidFrom <= at)
            && (c.ValidTo is null || c.ValidTo >= at));

    private PilotCertificate GrantIssuedStep(
        Pilot pilot,
        string typeCode,
        DateTimeOffset now,
        DateTimeOffset validFrom,
        DateTimeOffset? validTo,
        string? externalNumber,
        string? notes,
        string? mtowOrValidityText,
        bool forceNew)
    {
        if (!forceNew && HasIssued(pilot, typeCode, now))
            return pilot.Certificates.First(c =>
                NormalizeCertCode(c.CertificateTypeCode) == typeCode
                && c.IsActive
                && c.Status == CertificateStatus.Issued);

        var sameType = pilot.Certificates
            .Where(c => NormalizeCertCode(c.CertificateTypeCode) == typeCode)
            .ToList();
        var existing = sameType.FirstOrDefault(c => c.IsActive && c.Status == CertificateStatus.Issued)
            ?? sameType.FirstOrDefault(c => c.IsActive && c.Status == CertificateStatus.Suspended)
            ?? sameType.OrderByDescending(c => c.Version).FirstOrDefault();
        if (existing is not null)
        {
            RemoveCertificates(pilot, sameType.Where(c => c.Id != existing.Id));
            existing.IsActive = true;
            existing.Status = CertificateStatus.Issued;
            existing.SuspendedUntil = null;
            if (!string.IsNullOrWhiteSpace(externalNumber))
                existing.ExternalNumber = externalNumber.Trim();
            existing.ValidFrom = validFrom;
            existing.ValidTo = validTo;
            existing.IssuedAt ??= now;
            existing.TitleSk = CertificateTitle(typeCode);
            if (!string.IsNullOrWhiteSpace(mtowOrValidityText))
                existing.MtowOrValidityText = mtowOrValidityText;
            if (!string.IsNullOrWhiteSpace(notes))
                existing.Notes = notes;
            existing.SupersedesCertificateId = null;
            existing.UpdatedBy = currentUser.AccountId;
            return existing;
        }

        var cert = new PilotCertificate
        {
            PilotId = pilot.Id,
            CertificateTypeCode = typeCode,
            TitleSk = CertificateTitle(typeCode),
            ExternalNumber = string.IsNullOrWhiteSpace(externalNumber) ? null : externalNumber.Trim(),
            Version = 1,
            Status = CertificateStatus.Issued,
            IssuedAt = now,
            ValidFrom = validFrom,
            ValidTo = validTo,
            Notes = notes,
            MtowOrValidityText = mtowOrValidityText,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.PilotCertificates.Add(cert);
        pilot.Certificates.Add(cert);
        return cert;
    }

    public async Task<PilotCertificateDto> RevokeCertificateAsync(
        Guid pilotId, Guid certificateId, RevokePilotCertificateRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.PilotsWrite);
        var pilot = await LoadDetailAsync(pilotId, cancellationToken);
        policy.EnsureOrgAccess(pilot.OrgUnit.Path);

        var cert = pilot.Certificates.FirstOrDefault(c => c.Id == certificateId)
            ?? throw new NotFoundException("Osvedčenie nebolo nájdené.");
        if (cert.Status is CertificateStatus.Revoked or CertificateStatus.Superseded)
            throw new ValidationException("Toto osvedčenie už nie je aktívne.");

        var typeCode = NormalizeCertCode(cert.CertificateTypeCode);
        var cascade = new HashSet<string>(DependentChain(typeCode), StringComparer.OrdinalIgnoreCase);
        var toRevoke = pilot.Certificates
            .Where(c => cascade.Contains(NormalizeCertCode(c.CertificateTypeCode)))
            .ToList();
        if (!toRevoke.Any(c => c.Id == cert.Id))
            toRevoke.Add(cert);

        var snapshot = MapCertificate(cert);
        RemoveCertificates(pilot, toRevoke);

        RecalculateCurrencyFlags(pilot);
        await SyncLevel3LicenseAsync(pilot, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("PilotCertificate.Revoke", nameof(PilotCertificate), snapshot.Id, null,
            snapshot, cancellationToken: cancellationToken);
        return snapshot;
    }

    public async Task<PilotCertificateDto> SuspendCertificateAsync(
        Guid pilotId, Guid certificateId, SuspendPilotCertificateRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.PilotsWrite);
        await LiftExpiredCertificateSuspensionsAsync(cancellationToken);
        var pilot = await LoadDetailAsync(pilotId, cancellationToken);
        policy.EnsureOrgAccess(pilot.OrgUnit.Path);

        var cert = pilot.Certificates.FirstOrDefault(c => c.Id == certificateId)
            ?? throw new NotFoundException("Osvedčenie nebolo nájdené.");
        if (cert.Status is not (CertificateStatus.Issued or CertificateStatus.Suspended))
            throw new ValidationException("Pozastaviť možno iba platné alebo už pozastavené osvedčenie.");

        var now = clock.UtcNow;
        DateTimeOffset until;
        if (request.Until is DateTimeOffset requestedUntil)
        {
            until = requestedUntil.ToUniversalTime();
            if (until <= now)
                throw new ValidationException("Dátum pozastavenia musí byť v budúcnosti.");
            if (until > now.AddYears(2))
                throw new ValidationException("Pozastavenie môže byť najviac na 2 roky.");
        }
        else
        {
            var days = request.DurationDays;
            if (days is not (7 or 30 or 90 or 180))
                throw new ValidationException("Vyberte dátum, dokedy má byť osvedčenie pozastavené.");
            until = now.AddDays(days.Value);
        }

        var typeCode = NormalizeCertCode(cert.CertificateTypeCode);
        var cascade = new HashSet<string>(DependentChain(typeCode), StringComparer.OrdinalIgnoreCase);
        var reason = string.IsNullOrWhiteSpace(request.Reason)
            ? $"Pozastavené do {until:dd.MM.yyyy}"
            : request.Reason.Trim();

        foreach (var item in pilot.Certificates.Where(c =>
                     c.IsActive
                     && cascade.Contains(NormalizeCertCode(c.CertificateTypeCode))
                     && c.Status is CertificateStatus.Issued or CertificateStatus.Suspended))
        {
            item.Status = CertificateStatus.Suspended;
            item.SuspendedUntil = until;
            item.Notes = reason;
            item.UpdatedBy = currentUser.AccountId;
        }

        RecalculateCurrencyFlags(pilot);
        await SyncLevel3LicenseAsync(pilot, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("PilotCertificate.Suspend", nameof(PilotCertificate), cert.Id, null,
            MapCertificate(cert), cancellationToken: cancellationToken);
        return MapCertificate(cert);
    }

    public async Task<PilotCertificateDto> ResumeCertificateAsync(
        Guid pilotId, Guid certificateId, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.PilotsWrite);
        await LiftExpiredCertificateSuspensionsAsync(cancellationToken);
        var pilot = await LoadDetailAsync(pilotId, cancellationToken);
        policy.EnsureOrgAccess(pilot.OrgUnit.Path);

        var cert = pilot.Certificates.FirstOrDefault(c => c.Id == certificateId)
            ?? throw new NotFoundException("Osvedčenie nebolo nájdené.");
        if (cert.Status == CertificateStatus.Issued)
            return MapCertificate(cert);
        if (cert.Status != CertificateStatus.Suspended)
            throw new ValidationException("Obnoviť možno iba pozastavené osvedčenie.");

        var typeCode = NormalizeCertCode(cert.CertificateTypeCode);
        var cascade = new HashSet<string>(DependentChain(typeCode), StringComparer.OrdinalIgnoreCase);
        foreach (var item in pilot.Certificates.Where(c =>
                     c.IsActive
                     && cascade.Contains(NormalizeCertCode(c.CertificateTypeCode))
                     && c.Status == CertificateStatus.Suspended))
        {
            item.Status = CertificateStatus.Issued;
            item.SuspendedUntil = null;
            item.UpdatedBy = currentUser.AccountId;
        }

        RecalculateCurrencyFlags(pilot);
        await SyncLevel3LicenseAsync(pilot, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("PilotCertificate.Resume", nameof(PilotCertificate), cert.Id, null,
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

    private static IEnumerable<PilotCertificate> CurrentPnDCertificates(Pilot pilot) =>
        pilot.Certificates
            .Where(c => c.IsActive
                && c.Status is CertificateStatus.Issued or CertificateStatus.Suspended
                && NormalizeCertCode(c.CertificateTypeCode) is "A1_A3" or "A2" or "STS1" or "STS2")
            .GroupBy(c => NormalizeCertCode(c.CertificateTypeCode), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderBy(c => c.Status == CertificateStatus.Issued ? 0 : 1)
                .ThenByDescending(c => c.IssuedAt ?? c.CreatedAt).ThenByDescending(c => c.Version).First())
            .OrderBy(c => NormalizeCertCode(c.CertificateTypeCode) switch
            {
                "A1_A3" => 0,
                "A2" => 1,
                "STS1" => 2,
                _ => 3
            });

    private void RemoveCertificates(Pilot pilot, IEnumerable<PilotCertificate> items)
    {
        foreach (var item in items.Where(c => c is not null).Distinct())
        {
            item.IsActive = false;
            if (item.Status is CertificateStatus.Issued or CertificateStatus.Suspended)
                item.Status = CertificateStatus.Revoked;
            item.SuspendedUntil = null;
            item.SupersedesCertificateId = null;
            item.UpdatedBy = currentUser.AccountId;
        }
    }

    private async Task LiftExpiredCertificateSuspensionsAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var expired = await db.PilotCertificates
            .Where(c => c.IsActive && c.Status == CertificateStatus.Suspended
                && c.SuspendedUntil != null && c.SuspendedUntil <= now)
            .ToListAsync(cancellationToken);
        if (expired.Count == 0) return;
        foreach (var cert in expired)
        {
            cert.Status = CertificateStatus.Issued;
            cert.SuspendedUntil = null;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private PilotDto Map(Pilot p)
    {
        var now = clock.UtcNow;
        var issuedCerts = p.Certificates
            .Where(c => c.IsActive && c.Status == CertificateStatus.Issued
                && (c.ValidFrom is null || c.ValidFrom <= now)
                && (c.ValidTo is null || c.ValidTo >= now))
            .ToList();
        var visibleCerts = p.Certificates
            .Where(c => c.IsActive && c.Status is CertificateStatus.Issued or CertificateStatus.Suspended
                && (c.ValidFrom is null || c.ValidFrom <= now)
                && (c.Status == CertificateStatus.Suspended || c.ValidTo is null || c.ValidTo >= now))
            .ToList();

        var certCodes = issuedCerts
            .Select(c => NormalizeCertCode(c.CertificateTypeCode))
            .Where(c => c is "A1_A3" or "A2" or "STS1" or "STS2")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c switch { "A1_A3" => 0, "A2" => 1, "STS1" => 2, _ => 3 })
            .ToList();

        var certItems = visibleCerts
            .Select(c => new PilotCertListItemDto(
                NormalizeCertCode(c.CertificateTypeCode),
                c.ExternalNumber,
                c.ValidTo,
                c.MtowOrValidityText,
                c.Id,
                c.Status,
                c.ValidFrom,
                c.SuspendedUntil))
            .Where(c => c.CertificateTypeCode is "A1_A3" or "A2" or "STS1" or "STS2")
            .GroupBy(c => c.CertificateTypeCode, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderBy(c => c.Status == CertificateStatus.Issued ? 0 : 1).First())
            .OrderBy(c => c.CertificateTypeCode switch { "A1_A3" => 0, "A2" => 1, "STS1" => 2, _ => 3 })
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
        if (c is "STS2" or "STS_2" or "STS02" or "STS_02") return "STS2";
        if (c is "STS1" or "STS_1" or "STS01" or "STS_01" or "STS" or "STANDARD_SCENARIO") return "STS1";
        if (c.StartsWith("STS2", StringComparison.Ordinal)) return "STS2";
        if (c.StartsWith("STS", StringComparison.Ordinal)) return "STS1";
        return c;
    }

    private static string CertificateTitle(string typeCode) => typeCode switch
    {
        "A1_A3" => "Preukaz pilota na diaľku — kategória A1/A3",
        "A2" => "Preukaz pilota na diaľku — kategória A2",
        "STS1" => "Osvedčenie STS1",
        "STS2" => "Osvedčenie STS2",
        _ => typeCode
    };

    private async Task SyncLevel3LicenseAsync(Pilot pilot, CancellationToken cancellationToken)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.PersonId == pilot.PersonId, cancellationToken);
        if (account is null || account.AccessLevel != AccessLevel.Level3)
            return;

        var now = clock.UtcNow;
        var hasIssued = pilot.Certificates.Any(c =>
            c.IsActive
            && c.Status == CertificateStatus.Issued
            && (c.ValidFrom is null || c.ValidFrom <= now)
            && (c.ValidTo is null || c.ValidTo >= now));
        account.LicenseStatus = hasIssued ? LicenseStatus.Confirmed : LicenseStatus.Pending;
        account.UpdatedBy = currentUser.AccountId;
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
            c.MtowOrValidityText, c.SuspendedUntil);

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
