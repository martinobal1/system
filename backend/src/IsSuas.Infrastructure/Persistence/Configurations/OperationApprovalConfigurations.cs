using IsSuas.Domain.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IsSuas.Infrastructure.Persistence.Configurations;

internal sealed class OperationRecurrenceRuleConfiguration : IEntityTypeConfiguration<OperationRecurrenceRule>
{
    public void Configure(EntityTypeBuilder<OperationRecurrenceRule> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.DaysOfWeek).HasMaxLength(64);
        builder.HasOne(x => x.Operation).WithOne(x => x.RecurrenceRule).HasForeignKey<OperationRecurrenceRule>(x => x.OperationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.OperationId).IsUnique();
    }
}

internal sealed class OperationApprovalStepConfiguration : IEntityTypeConfiguration<OperationApprovalStep>
{
    public void Configure(EntityTypeBuilder<OperationApprovalStep> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.StepTypeCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.AssignedRoleCode).HasMaxLength(64);
        builder.HasOne(x => x.Operation).WithMany(x => x.ApprovalSteps).HasForeignKey(x => x.OperationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.OperationId, x.SequenceNumber });
    }
}

internal sealed class OperationDecisionConfiguration : IEntityTypeConfiguration<OperationDecision>
{
    public void Configure(EntityTypeBuilder<OperationDecision> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.Justification).HasMaxLength(2000).IsRequired();
        builder.HasOne(x => x.ApprovalStep).WithOne(x => x.Decision).HasForeignKey<OperationDecision>(x => x.ApprovalStepId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.ApprovalStepId).IsUnique();
    }
}

internal sealed class OperationApprovalConditionConfiguration : IEntityTypeConfiguration<OperationApprovalCondition>
{
    public void Configure(EntityTypeBuilder<OperationApprovalCondition> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.ConditionTypeCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.VerificationNote).HasMaxLength(2000);
        builder.HasOne(x => x.Decision).WithMany(x => x.Conditions).HasForeignKey(x => x.DecisionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.DecisionId, x.Status });
    }
}
