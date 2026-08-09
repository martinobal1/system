using IsSuas.Domain.Notifications;
using IsSuas.Domain.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IsSuas.Infrastructure.Persistence.Configurations;

internal sealed class MessageTemplateConfiguration : IEntityTypeConfiguration<MessageTemplate>
{
    public void Configure(EntityTypeBuilder<MessageTemplate> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.Code).HasMaxLength(64).IsRequired();
        builder.Property(x => x.NameSk).HasMaxLength(256).IsRequired();
        builder.Property(x => x.SubjectTemplate).HasMaxLength(512).IsRequired();
        builder.Property(x => x.BodyTemplate).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Language).HasMaxLength(16);
        builder.HasIndex(x => x.Code).IsUnique();
    }
}

internal sealed class NotificationRecipientConfiguration : IEntityTypeConfiguration<NotificationRecipient>
{
    public void Configure(EntityTypeBuilder<NotificationRecipient> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.RoleCode).HasMaxLength(64);
        builder.HasOne(x => x.AppNotification).WithMany().HasForeignKey(x => x.AppNotificationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => new { x.AppNotificationId, x.AccountId, x.RoleCode });
    }
}

internal sealed class DeliveryAttemptConfiguration : IEntityTypeConfiguration<DeliveryAttempt>
{
    public void Configure(EntityTypeBuilder<DeliveryAttempt> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.ErrorMessage).HasMaxLength(2000);
        builder.HasOne(x => x.AppNotification).WithMany().HasForeignKey(x => x.AppNotificationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.NotificationRecipient).WithMany().HasForeignKey(x => x.NotificationRecipientId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => new { x.AppNotificationId, x.AttemptNumber });
    }
}

internal sealed class EscalationRuleConfiguration : IEntityTypeConfiguration<EscalationRule>
{
    public void Configure(EntityTypeBuilder<EscalationRule> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.Code).HasMaxLength(64).IsRequired();
        builder.Property(x => x.NameSk).HasMaxLength(256).IsRequired();
        builder.Property(x => x.TargetRoleCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.SourceTypeCode).HasMaxLength(64);
        builder.Property(x => x.EscalationMessageTemplate).HasMaxLength(2000).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
    }
}

internal sealed class WorkTaskHistoryConfiguration : IEntityTypeConfiguration<WorkTaskHistory>
{
    public void Configure(EntityTypeBuilder<WorkTaskHistory> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.Note).HasMaxLength(2000);
        builder.HasOne(x => x.WorkTask).WithMany().HasForeignKey(x => x.WorkTaskId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.WorkTaskId, x.ChangedAt });
    }
}
