using IsSuas.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IsSuas.Infrastructure.Persistence.Configurations;

internal sealed class ManagedDocumentConfiguration : IEntityTypeConfiguration<ManagedDocument>
{
    public void Configure(EntityTypeBuilder<ManagedDocument> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.Title).HasMaxLength(256).IsRequired();
        builder.Property(x => x.DocumentTypeCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(512).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ContentHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ScanStatus).HasMaxLength(64);
        builder.HasIndex(x => new { x.LinkedEntityType, x.LinkedEntityId, x.IsCurrent });
        builder.HasIndex(x => x.DocumentFamilyId);
        builder.HasIndex(x => x.DocumentTypeCode);
        builder.HasOne(x => x.StoredFile).WithMany().HasForeignKey(x => x.StoredFileId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.RetentionRule).WithMany().HasForeignKey(x => x.RetentionRuleId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.StorageKey).HasMaxLength(1024).IsRequired();
        builder.Property(x => x.Provider).HasMaxLength(32).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(512).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ContentHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => x.ContentHash);
        builder.HasIndex(x => x.StorageKey).IsUnique();
    }
}

internal sealed class RetentionRuleConfiguration : IEntityTypeConfiguration<RetentionRule>
{
    public void Configure(EntityTypeBuilder<RetentionRule> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.Code).HasMaxLength(64).IsRequired();
        builder.Property(x => x.NameSk).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ArchiveMode).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
    }
}

internal sealed class DocumentDossierConfiguration : IEntityTypeConfiguration<DocumentDossier>
{
    public void Configure(EntityTypeBuilder<DocumentDossier> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.Title).HasMaxLength(256).IsRequired();
    }
}

internal sealed class DocumentDossierItemConfiguration : IEntityTypeConfiguration<DocumentDossierItem>
{
    public void Configure(EntityTypeBuilder<DocumentDossierItem> builder)
    {
        AuditableConfig.Configure(builder);
        builder.HasOne(x => x.DocumentDossier).WithMany(x => x.Items).HasForeignKey(x => x.DocumentDossierId);
        builder.HasOne(x => x.ManagedDocument).WithMany().HasForeignKey(x => x.ManagedDocumentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.DocumentDossierId, x.ManagedDocumentId }).IsUnique();
    }
}

internal sealed class FormTemplateConfiguration : IEntityTypeConfiguration<FormTemplate>
{
    public void Configure(EntityTypeBuilder<FormTemplate> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.Code).HasMaxLength(64).IsRequired();
        builder.Property(x => x.NameSk).HasMaxLength(256).IsRequired();
        builder.HasIndex(x => new { x.Code, x.Version }).IsUnique();
    }
}

internal sealed class FormTemplateItemConfiguration : IEntityTypeConfiguration<FormTemplateItem>
{
    public void Configure(EntityTypeBuilder<FormTemplateItem> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.Code).HasMaxLength(64).IsRequired();
        builder.Property(x => x.LabelSk).HasMaxLength(512).IsRequired();
        builder.HasOne(x => x.FormTemplate).WithMany(x => x.Items).HasForeignKey(x => x.FormTemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class FormSubmissionConfiguration : IEntityTypeConfiguration<FormSubmission>
{
    public void Configure(EntityTypeBuilder<FormSubmission> builder)
    {
        AuditableConfig.Configure(builder);
        builder.HasOne(x => x.FormTemplate).WithMany().HasForeignKey(x => x.FormTemplateId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.LinkedEntityType, x.LinkedEntityId });
    }
}

internal sealed class FormAnswerConfiguration : IEntityTypeConfiguration<FormAnswer>
{
    public void Configure(EntityTypeBuilder<FormAnswer> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.ItemCode).HasMaxLength(64).IsRequired();
        builder.HasOne(x => x.FormSubmission).WithMany(x => x.Answers).HasForeignKey(x => x.FormSubmissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class RiskAssessmentConfiguration : IEntityTypeConfiguration<RiskAssessment>
{
    public void Configure(EntityTypeBuilder<RiskAssessment> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.MethodologyCode).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.LinkedEntityType, x.LinkedEntityId });
    }
}
