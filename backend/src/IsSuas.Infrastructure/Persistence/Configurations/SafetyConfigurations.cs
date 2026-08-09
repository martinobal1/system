using IsSuas.Domain.Safety;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IsSuas.Infrastructure.Persistence.Configurations;

internal sealed class SafetyEventConfiguration : IEntityTypeConfiguration<SafetyEvent>
{
    public void Configure(EntityTypeBuilder<SafetyEvent> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.Title).HasMaxLength(256).IsRequired();
        builder.Property(x => x.InitialReport).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.LocationDescription).HasMaxLength(512);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.Severity);
        builder.HasIndex(x => x.RelatedUasId);
    }
}

internal sealed class SafetyEvidenceConfiguration : IEntityTypeConfiguration<SafetyEvidence>
{
    public void Configure(EntityTypeBuilder<SafetyEvidence> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.FileName).HasMaxLength(512).IsRequired();
        builder.Property(x => x.ContentHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(128).IsRequired();
        builder.HasOne(x => x.SafetyEvent).WithMany(x => x.Evidences).HasForeignKey(x => x.SafetyEventId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.SafetyEventId, x.IsCurrent });
    }
}

internal sealed class CorrectiveActionConfiguration : IEntityTypeConfiguration<CorrectiveAction>
{
    public void Configure(EntityTypeBuilder<CorrectiveAction> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.Title).HasMaxLength(256).IsRequired();
        builder.Property(x => x.WaiverReason).HasMaxLength(2000);
        builder.HasOne(x => x.SafetyEvent).WithMany(x => x.CorrectiveActions).HasForeignKey(x => x.SafetyEventId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.Status);
    }
}

internal sealed class SafetyEventParticipantConfiguration : IEntityTypeConfiguration<SafetyEventParticipant>
{
    public void Configure(EntityTypeBuilder<SafetyEventParticipant> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.ParticipantRoleCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.InvolvementDescription).HasMaxLength(2000);
        builder.Property(x => x.InjuryStatusCode).HasMaxLength(64);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.HasOne(x => x.SafetyEvent).WithMany(x => x.Participants).HasForeignKey(x => x.SafetyEventId);
        builder.HasOne(x => x.Person).WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SafetyEventAssetConfiguration : IEntityTypeConfiguration<SafetyEventAsset>
{
    public void Configure(EntityTypeBuilder<SafetyEventAsset> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.AssetRoleCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.DamageStatusCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.DamageDescription).HasMaxLength(2000);
        builder.Property(x => x.TechnicalStatusAfterEvent).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.HasOne(x => x.SafetyEvent).WithMany(x => x.Assets).HasForeignKey(x => x.SafetyEventId);
        builder.HasOne(x => x.Uas).WithMany().HasForeignKey(x => x.UasId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Component).WithMany().HasForeignKey(x => x.ComponentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Battery).WithMany().HasForeignKey(x => x.BatteryId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SafetyImmediateActionConfiguration : IEntityTypeConfiguration<SafetyImmediateAction>
{
    public void Configure(EntityTypeBuilder<SafetyImmediateAction> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.ActionTypeCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ActionDescription).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.ResultDescription).HasMaxLength(2000);
        builder.HasOne(x => x.SafetyEvent).WithMany(x => x.ImmediateActions).HasForeignKey(x => x.SafetyEventId);
        builder.HasOne(x => x.PerformedByPerson).WithMany().HasForeignKey(x => x.PerformedByPersonId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SafetyInvestigationConfiguration : IEntityTypeConfiguration<SafetyInvestigation>
{
    public void Configure(EntityTypeBuilder<SafetyInvestigation> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.InvestigationNumber).HasMaxLength(64).IsRequired();
        builder.Property(x => x.InvestigationTypeCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ScopeDescription).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.FindingsSummary).HasMaxLength(4000);
        builder.Property(x => x.Conclusion).HasMaxLength(4000);
        builder.Property(x => x.InterruptionReason).HasMaxLength(2000);
        builder.HasOne(x => x.SafetyEvent).WithMany(x => x.Investigations).HasForeignKey(x => x.SafetyEventId);
        builder.HasOne(x => x.ResponsiblePerson).WithMany().HasForeignKey(x => x.ResponsiblePersonId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.InvestigationNumber).IsUnique();
    }
}

internal sealed class SafetyCauseConfiguration : IEntityTypeConfiguration<SafetyCause>
{
    public void Configure(EntityTypeBuilder<SafetyCause> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.CauseCategoryCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.CauseDescription).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.ConfidenceLevelCode).HasMaxLength(64);
        builder.Property(x => x.SupportingEvidence).HasMaxLength(2000);
        builder.HasOne(x => x.Investigation).WithMany(x => x.Causes).HasForeignKey(x => x.InvestigationId);
        builder.HasOne(x => x.ConfirmedByPerson).WithMany().HasForeignKey(x => x.ConfirmedByPersonId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SafetyReportingObligationConfiguration : IEntityTypeConfiguration<SafetyReportingObligation>
{
    public void Configure(EntityTypeBuilder<SafetyReportingObligation> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.ObligationTypeCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.RecipientAuthority).HasMaxLength(256).IsRequired();
        builder.Property(x => x.LegalBasis).HasMaxLength(1000);
        builder.Property(x => x.ExternalReferenceNumber).HasMaxLength(128);
        builder.Property(x => x.DelayReason).HasMaxLength(2000);
        builder.Property(x => x.CancellationReason).HasMaxLength(2000);
        builder.HasOne(x => x.SafetyEvent).WithMany(x => x.ReportingObligations).HasForeignKey(x => x.SafetyEventId);
        builder.HasOne(x => x.ReportedByPerson).WithMany().HasForeignKey(x => x.ReportedByPersonId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SafetyStatusHistoryConfiguration : IEntityTypeConfiguration<SafetyStatusHistory>
{
    public void Configure(EntityTypeBuilder<SafetyStatusHistory> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        builder.HasOne(x => x.SafetyEvent).WithMany(x => x.StatusHistory).HasForeignKey(x => x.SafetyEventId);
    }
}

internal sealed class SafetyClassificationHistoryConfiguration : IEntityTypeConfiguration<SafetyClassificationHistory>
{
    public void Configure(EntityTypeBuilder<SafetyClassificationHistory> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.ClassificationTypeCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.PreviousValueCode).HasMaxLength(64);
        builder.Property(x => x.NewValueCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ChangeReason).HasMaxLength(1000).IsRequired();
        builder.HasOne(x => x.SafetyEvent).WithMany(x => x.ClassificationHistory).HasForeignKey(x => x.SafetyEventId);
    }
}

internal sealed class FleetSafetyAlertConfiguration : IEntityTypeConfiguration<FleetSafetyAlert>
{
    public void Configure(EntityTypeBuilder<FleetSafetyAlert> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.AlertNumber).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.SeverityCode).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ScopeCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.AffectedUasTypeCode).HasMaxLength(64);
        builder.Property(x => x.WithdrawalReason).HasMaxLength(2000);
        builder.HasOne(x => x.SafetyEvent).WithMany(x => x.FleetAlerts).HasForeignKey(x => x.SafetyEventId);
        builder.HasIndex(x => x.AlertNumber).IsUnique();
    }
}

internal sealed class AppNotificationConfiguration : IEntityTypeConfiguration<AppNotification>
{
    public void Configure(EntityTypeBuilder<AppNotification> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.Title).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.TypeCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Priority).HasMaxLength(32).IsRequired();
        builder.Property(x => x.RecipientRoleCode).HasMaxLength(64);
        builder.Property(x => x.RelatedEntityType).HasMaxLength(64);
        builder.HasIndex(x => new { x.TypeCode, x.RelatedSafetyEventId, x.RecipientRoleCode });
        builder.HasIndex(x => x.Status);
    }
}
