using IsSuas.Domain.Common;
using IsSuas.Domain.Enums;
using IsSuas.Domain.Fleet;
using IsSuas.Domain.Identity;

namespace IsSuas.Domain.Pilots;

public class Pilot : AuditableEntity
{
    public Guid PersonId { get; set; }
    public Person Person { get; set; } = null!;
    public Guid OrgUnitId { get; set; }
    public OrgUnit OrgUnit { get; set; } = null!;
    public string? LicenseNumber { get; set; }
    public PilotStatus Status { get; set; } = PilotStatus.Draft;
    public DateTimeOffset? CurrencyValidUntil { get; set; }
    public DateTimeOffset? LastFlightAt { get; set; }
    public string? Notes { get; set; }
    public ICollection<PilotQualification> Qualifications { get; set; } = new List<PilotQualification>();
    public ICollection<PilotCertificate> Certificates { get; set; } = new List<PilotCertificate>();
    public ICollection<PilotTraining> Trainings { get; set; } = new List<PilotTraining>();
    public ICollection<PilotStatusHistory> StatusHistory { get; set; } = new List<PilotStatusHistory>();
    public ICollection<PilotRestriction> Restrictions { get; set; } = new List<PilotRestriction>();
}

public class PilotStatusHistory : AuditableEntity
{
    public Guid PilotId { get; set; }
    public Pilot Pilot { get; set; } = null!;
    public PilotStatus FromStatus { get; set; }
    public PilotStatus ToStatus { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset ChangedAt { get; set; }
}

public class PilotQualification : AuditableEntity
{
    public Guid PilotId { get; set; }
    public Pilot Pilot { get; set; } = null!;
    public string QualificationCode { get; set; } = string.Empty;
    public string NameSk { get; set; } = string.Empty;
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
    public string? Issuer { get; set; }
    public ICollection<QualificationDocumentVerification> Verifications { get; set; } = new List<QualificationDocumentVerification>();
}

public class PilotCertificate : AuditableEntity
{
    public Guid PilotId { get; set; }
    public Pilot Pilot { get; set; } = null!;
    public string CertificateTypeCode { get; set; } = string.Empty;
    public string TitleSk { get; set; } = string.Empty;
    public string? ExternalNumber { get; set; }
    public int Version { get; set; } = 1;
    public CertificateStatus Status { get; set; } = CertificateStatus.Draft;
    public DateTimeOffset? IssuedAt { get; set; }
    public DateTimeOffset? ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
    public Guid? SupersedesCertificateId { get; set; }
    public string? Notes { get; set; }
    public ICollection<CertificateScope> Scopes { get; set; } = new List<CertificateScope>();
}

public class PilotTraining : AuditableEntity
{
    public Guid PilotId { get; set; }
    public Pilot Pilot { get; set; } = null!;
    public string TrainingCode { get; set; } = string.Empty;
    public string TitleSk { get; set; } = string.Empty;
    public DateTimeOffset CompletedAt { get; set; }
    public TrainingResult Result { get; set; }
    public string? InstructorName { get; set; }
    public DateTimeOffset? ValidUntil { get; set; }
    public string? Notes { get; set; }
    public Guid? TrainingParticipationId { get; set; }
    public TrainingParticipation? TrainingParticipation { get; set; }
}

/// <summary>ENT-027 — prevádzková/odborná funkcia osoby.</summary>
public class PersonFunctionAssignment : AuditableEntity
{
    public Guid PersonId { get; set; }
    public Person Person { get; set; } = null!;
    public string FunctionTypeCode { get; set; } = string.Empty;
    public Guid OrganizationOrgUnitId { get; set; }
    public OrgUnit OrganizationOrgUnit { get; set; } = null!;
    public Guid? OrgUnitId { get; set; }
    public OrgUnit? OrgUnit { get; set; }
    public string AssignmentScopeTypeCode { get; set; } = "ORG_UNIT";
    public Guid? UasId { get; set; }
    public Uas? Uas { get; set; }
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
    public PersonFunctionAssignmentStatus Status { get; set; } = PersonFunctionAssignmentStatus.Active;
    public Guid AssignedByAccountId { get; set; }
    public string? TerminationReason { get; set; }
}

/// <summary>ENT-028 — obmedzenie pilota.</summary>
public class PilotRestriction : AuditableEntity
{
    public Guid PilotId { get; set; }
    public Pilot Pilot { get; set; } = null!;
    public string RestrictionTypeCode { get; set; } = "OPERATIONAL";
    public string Description { get; set; } = string.Empty;
    public string SeverityCode { get; set; } = "MEDIUM";
    public bool BlocksOperation { get; set; }
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
    public Guid? ImposedByPersonId { get; set; }
    public Person? ImposedByPerson { get; set; }
    public PilotRestrictionStatus Status { get; set; } = PilotRestrictionStatus.Active;
    public string? TerminationReason { get; set; }
}

/// <summary>ENT-030 — overenie externého dokladu (via PilotQualification).</summary>
public class QualificationDocumentVerification : AuditableEntity
{
    public Guid QualificationId { get; set; }
    public PilotQualification Qualification { get; set; } = null!;
    public DateTimeOffset VerificationDate { get; set; }
    public string VerificationMethodCode { get; set; } = "MANUAL";
    public QualificationVerificationResult Result { get; set; }
    public Guid VerifiedByAccountId { get; set; }
    public string? VerifiedSource { get; set; }
    public DateTimeOffset? NextVerificationDate { get; set; }
    public string? Notes { get; set; }
}

/// <summary>ENT-032 — rozsah osvedčenia.</summary>
public class CertificateScope : AuditableEntity
{
    public Guid CertificateId { get; set; }
    public PilotCertificate Certificate { get; set; } = null!;
    public string ScopeTypeCode { get; set; } = "UAS_TYPE";
    public string? UasTypeCode { get; set; }
    public string? OperationTypeCode { get; set; }
    public string? FunctionTypeCode { get; set; }
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
    public string? RestrictionText { get; set; }
    public CertificateScopeStatus Status { get; set; } = CertificateScopeStatus.Active;
}

/// <summary>ENT-033 — katalóg vzdelávacích aktivít.</summary>
public class TrainingCatalogItem : AuditableEntity
{
    public string ActivityCode { get; set; } = string.Empty;
    public string ActivityName { get; set; } = string.Empty;
    public string ActivityTypeCode { get; set; } = "TRAINING";
    public int VersionNumber { get; set; } = 1;
    public string Description { get; set; } = string.Empty;
    public decimal? MinimumDurationHours { get; set; }
    public int? RecurrencePeriodMonths { get; set; }
    public bool ExaminationRequired { get; set; }
    public int? PassingScorePercent { get; set; }
    public bool PracticalPartRequired { get; set; }
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
    public TrainingCatalogStatus Status { get; set; } = TrainingCatalogStatus.Active;
    public ICollection<TrainingActivity> Activities { get; set; } = new List<TrainingActivity>();
}

/// <summary>ENT-034 — konkrétna výcviková aktivita (termín).</summary>
public class TrainingActivity : AuditableEntity
{
    public Guid CatalogItemId { get; set; }
    public TrainingCatalogItem CatalogItem { get; set; } = null!;
    public string ActivityNumber { get; set; } = string.Empty;
    public Guid OrganizationOrgUnitId { get; set; }
    public OrgUnit OrganizationOrgUnit { get; set; } = null!;
    public Guid? OrgUnitId { get; set; }
    public OrgUnit? OrgUnit { get; set; }
    public DateTimeOffset PlannedStartAt { get; set; }
    public DateTimeOffset PlannedEndAt { get; set; }
    public DateTimeOffset? ActualStartAt { get; set; }
    public DateTimeOffset? ActualEndAt { get; set; }
    public string? Location { get; set; }
    public string DeliveryFormCode { get; set; } = "IN_PERSON";
    public Guid? InstructorPersonId { get; set; }
    public Person? InstructorPerson { get; set; }
    public Guid? ExaminerPersonId { get; set; }
    public Person? ExaminerPerson { get; set; }
    public int? MaximumParticipants { get; set; }
    public TrainingActivityStatus Status { get; set; } = TrainingActivityStatus.Planned;
    public string? CancellationReason { get; set; }
    public ICollection<TrainingParticipation> Participations { get; set; } = new List<TrainingParticipation>();
}

/// <summary>ENT-035 — účasť na aktivite.</summary>
public class TrainingParticipation : AuditableEntity
{
    public Guid ActivityId { get; set; }
    public TrainingActivity Activity { get; set; } = null!;
    public Guid PersonId { get; set; }
    public Person Person { get; set; } = null!;
    public string ParticipationRoleCode { get; set; } = "PARTICIPANT";
    public DateTimeOffset RegistrationDate { get; set; }
    public TrainingRegistrationStatus RegistrationStatus { get; set; } = TrainingRegistrationStatus.Registered;
    public TrainingAttendanceStatus? AttendanceStatus { get; set; }
    public int? AttendancePercentage { get; set; }
    public TrainingCompletionStatus? CompletionStatus { get; set; }
    public string? AbsenceReason { get; set; }
    public string? Notes { get; set; }
    public TrainingActivityResult? Result { get; set; }
}

/// <summary>ENT-036 — výsledok aktivity.</summary>
public class TrainingActivityResult : AuditableEntity
{
    public Guid ParticipationId { get; set; }
    public TrainingParticipation Participation { get; set; } = null!;
    public TrainingResult Result { get; set; }
    public int AttemptNumber { get; set; } = 1;
    public int? TheoreticalScorePercent { get; set; }
    public int? PracticalScorePercent { get; set; }
    public int? OverallScorePercent { get; set; }
    public DateTimeOffset? EvaluatedAt { get; set; }
    public Guid? EvaluatedByPersonId { get; set; }
    public Person? EvaluatedByPerson { get; set; }
    public DateTimeOffset? ValidUntil { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Domain rules for assigning a pilot into an operation role.</summary>
public static class PilotEligibility
{
    public static bool CanAssignToOperation(Pilot pilot, DateTimeOffset at)
    {
        if (!pilot.IsActive)
            return false;

        if (pilot.Status is PilotStatus.Draft or PilotStatus.Unfit or PilotStatus.Suspended or PilotStatus.Inactive)
            return false;

        if (HasBlockingRestriction(pilot, at))
            return false;

        var hasValidCertificate = pilot.Certificates.Any(c =>
            c.IsActive
            && c.Status == CertificateStatus.Issued
            && (c.ValidFrom is null || c.ValidFrom <= at)
            && (c.ValidTo is null || c.ValidTo >= at));

        if (!hasValidCertificate)
            return false;

        if (pilot.CurrencyValidUntil is not null && pilot.CurrencyValidUntil < at)
            return false;

        return true;
    }

    public static bool HasBlockingRestriction(Pilot pilot, DateTimeOffset at) =>
        pilot.Restrictions.Any(r =>
            r.IsActive
            && r.Status == PilotRestrictionStatus.Active
            && r.BlocksOperation
            && r.ValidFrom <= at
            && (r.ValidTo is null || r.ValidTo >= at));
}
