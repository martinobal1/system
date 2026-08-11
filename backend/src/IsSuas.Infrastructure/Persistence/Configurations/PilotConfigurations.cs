using IsSuas.Domain.Pilots;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IsSuas.Infrastructure.Persistence.Configurations;

internal sealed class PilotConfiguration : IEntityTypeConfiguration<Pilot>
{
    public void Configure(EntityTypeBuilder<Pilot> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.LicenseNumber).HasMaxLength(64);
        builder.Property(x => x.PersonalBadgeNumber).HasMaxLength(64);
        builder.Property(x => x.GestorCertificateNumber).HasMaxLength(64);
        builder.Property(x => x.LogbookNumber).HasMaxLength(64);
        builder.HasOne(x => x.Person).WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.OrgUnit).WithMany().HasForeignKey(x => x.OrgUnitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.PersonId);
        builder.HasIndex(x => new { x.PersonId, x.IsActive });
    }
}

internal sealed class PilotQualificationConfiguration : IEntityTypeConfiguration<PilotQualification>
{
    public void Configure(EntityTypeBuilder<PilotQualification> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.QualificationCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.NameSk).HasMaxLength(256).IsRequired();
        builder.HasOne(x => x.Pilot).WithMany(x => x.Qualifications).HasForeignKey(x => x.PilotId);
    }
}

internal sealed class PilotCertificateConfiguration : IEntityTypeConfiguration<PilotCertificate>
{
    public void Configure(EntityTypeBuilder<PilotCertificate> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.CertificateTypeCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.TitleSk).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ExternalNumber).HasMaxLength(128);
        builder.Property(x => x.MtowOrValidityText).HasMaxLength(128);
        builder.HasOne(x => x.Pilot).WithMany(x => x.Certificates).HasForeignKey(x => x.PilotId);
        builder.HasIndex(x => new { x.PilotId, x.CertificateTypeCode, x.Version }).IsUnique();
    }
}

internal sealed class PilotTrainingConfiguration : IEntityTypeConfiguration<PilotTraining>
{
    public void Configure(EntityTypeBuilder<PilotTraining> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.TrainingCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.TitleSk).HasMaxLength(256).IsRequired();
        builder.HasOne(x => x.Pilot).WithMany(x => x.Trainings).HasForeignKey(x => x.PilotId);
        builder.HasOne(x => x.TrainingParticipation).WithMany().HasForeignKey(x => x.TrainingParticipationId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class PilotStatusHistoryConfiguration : IEntityTypeConfiguration<PilotStatusHistory>
{
    public void Configure(EntityTypeBuilder<PilotStatusHistory> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.Reason).HasMaxLength(512).IsRequired();
        builder.HasOne(x => x.Pilot).WithMany(x => x.StatusHistory).HasForeignKey(x => x.PilotId);
    }
}

internal sealed class PersonFunctionAssignmentConfiguration : IEntityTypeConfiguration<PersonFunctionAssignment>
{
    public void Configure(EntityTypeBuilder<PersonFunctionAssignment> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.FunctionTypeCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.AssignmentScopeTypeCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.TerminationReason).HasMaxLength(1000);
        builder.HasOne(x => x.Person).WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.OrganizationOrgUnit).WithMany().HasForeignKey(x => x.OrganizationOrgUnitId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.OrgUnit).WithMany().HasForeignKey(x => x.OrgUnitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Uas).WithMany().HasForeignKey(x => x.UasId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.PersonId, x.FunctionTypeCode, x.Status });
    }
}

internal sealed class PilotRestrictionConfiguration : IEntityTypeConfiguration<PilotRestriction>
{
    public void Configure(EntityTypeBuilder<PilotRestriction> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.RestrictionTypeCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.SeverityCode).HasMaxLength(32).IsRequired();
        builder.Property(x => x.TerminationReason).HasMaxLength(1000);
        builder.HasOne(x => x.Pilot).WithMany(x => x.Restrictions).HasForeignKey(x => x.PilotId);
        builder.HasOne(x => x.ImposedByPerson).WithMany().HasForeignKey(x => x.ImposedByPersonId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.PilotId, x.Status });
    }
}

internal sealed class QualificationDocumentVerificationConfiguration : IEntityTypeConfiguration<QualificationDocumentVerification>
{
    public void Configure(EntityTypeBuilder<QualificationDocumentVerification> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.VerificationMethodCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.VerifiedSource).HasMaxLength(512);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.HasOne(x => x.Qualification).WithMany(x => x.Verifications).HasForeignKey(x => x.QualificationId);
    }
}

internal sealed class CertificateScopeConfiguration : IEntityTypeConfiguration<CertificateScope>
{
    public void Configure(EntityTypeBuilder<CertificateScope> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.ScopeTypeCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.UasTypeCode).HasMaxLength(64);
        builder.Property(x => x.OperationTypeCode).HasMaxLength(64);
        builder.Property(x => x.FunctionTypeCode).HasMaxLength(64);
        builder.Property(x => x.RestrictionText).HasMaxLength(2000);
        builder.HasOne(x => x.Certificate).WithMany(x => x.Scopes).HasForeignKey(x => x.CertificateId);
    }
}

internal sealed class TrainingCatalogItemConfiguration : IEntityTypeConfiguration<TrainingCatalogItem>
{
    public void Configure(EntityTypeBuilder<TrainingCatalogItem> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.ActivityCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ActivityName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ActivityTypeCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(4000).IsRequired();
        builder.HasIndex(x => new { x.ActivityCode, x.VersionNumber }).IsUnique();
    }
}

internal sealed class TrainingActivityConfiguration : IEntityTypeConfiguration<TrainingActivity>
{
    public void Configure(EntityTypeBuilder<TrainingActivity> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.ActivityNumber).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Location).HasMaxLength(512);
        builder.Property(x => x.DeliveryFormCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.CancellationReason).HasMaxLength(1000);
        builder.HasOne(x => x.CatalogItem).WithMany(x => x.Activities).HasForeignKey(x => x.CatalogItemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.OrganizationOrgUnit).WithMany().HasForeignKey(x => x.OrganizationOrgUnitId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.OrgUnit).WithMany().HasForeignKey(x => x.OrgUnitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.InstructorPerson).WithMany().HasForeignKey(x => x.InstructorPersonId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ExaminerPerson).WithMany().HasForeignKey(x => x.ExaminerPersonId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.ActivityNumber).IsUnique();
    }
}

internal sealed class TrainingParticipationConfiguration : IEntityTypeConfiguration<TrainingParticipation>
{
    public void Configure(EntityTypeBuilder<TrainingParticipation> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.ParticipationRoleCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.AbsenceReason).HasMaxLength(1000);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.HasOne(x => x.Activity).WithMany(x => x.Participations).HasForeignKey(x => x.ActivityId);
        builder.HasOne(x => x.Person).WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ActivityId, x.PersonId }).IsUnique();
    }
}

internal sealed class TrainingActivityResultConfiguration : IEntityTypeConfiguration<TrainingActivityResult>
{
    public void Configure(EntityTypeBuilder<TrainingActivityResult> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.HasOne(x => x.Participation).WithOne(x => x.Result).HasForeignKey<TrainingActivityResult>(x => x.ParticipationId);
        builder.HasOne(x => x.EvaluatedByPerson).WithMany().HasForeignKey(x => x.EvaluatedByPersonId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
