using IsSuas.Domain.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IsSuas.Infrastructure.Persistence.Configurations;

internal sealed class OperationConfiguration : IEntityTypeConfiguration<Operation>
{
    public void Configure(EntityTypeBuilder<Operation> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.Title).HasMaxLength(256).IsRequired();
        builder.Property(x => x.LocationAddress).HasMaxLength(512);
        builder.Property(x => x.UrgentReason).HasMaxLength(1024);
        builder.Property(x => x.DecisionReason).HasMaxLength(1024);
        builder.HasOne(x => x.OrgUnit).WithMany().HasForeignKey(x => x.OrgUnitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.PlannedStart, x.PlannedEnd });
        builder.HasIndex(x => x.Status);
    }
}

internal sealed class OperationResourceConfiguration : IEntityTypeConfiguration<OperationResource>
{
    public void Configure(EntityTypeBuilder<OperationResource> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.RoleCode).HasMaxLength(64);
        builder.Property(x => x.DisplayName).HasMaxLength(256);
        builder.HasOne(x => x.Operation).WithMany(x => x.Resources).HasForeignKey(x => x.OperationId);
        builder.HasIndex(x => new { x.ResourceType, x.ResourceId });
    }
}

internal sealed class OperationStatusHistoryConfiguration : IEntityTypeConfiguration<OperationStatusHistory>
{
    public void Configure(EntityTypeBuilder<OperationStatusHistory> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.Reason).HasMaxLength(1024).IsRequired();
        builder.HasOne(x => x.Operation).WithMany(x => x.StatusHistory).HasForeignKey(x => x.OperationId);
    }
}

internal sealed class WorkTaskConfiguration : IEntityTypeConfiguration<WorkTask>
{
    public void Configure(EntityTypeBuilder<WorkTask> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.Title).HasMaxLength(256).IsRequired();
        builder.Property(x => x.TaskTypeCode).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.RelatedOperationId);
        builder.HasIndex(x => x.Status);
    }
}

internal sealed class OperationMapCheckConfiguration : IEntityTypeConfiguration<OperationMapCheck>
{
    public void Configure(EntityTypeBuilder<OperationMapCheck> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.ProviderCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ResultCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Summary).HasMaxLength(2000).IsRequired();
        builder.HasOne(x => x.Operation).WithMany(x => x.MapChecks).HasForeignKey(x => x.OperationId);
        builder.HasIndex(x => new { x.OperationId, x.CheckedAt });
    }
}
