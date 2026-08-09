using IsSuas.Domain.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IsSuas.Infrastructure.Persistence.Configurations;

internal sealed class MaintenancePlanConfiguration : IEntityTypeConfiguration<MaintenancePlan>
{
    public void Configure(EntityTypeBuilder<MaintenancePlan> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.NameSk).HasMaxLength(256).IsRequired();
        builder.Property(x => x.TargetTypeCode).HasMaxLength(64);
        builder.HasIndex(x => new { x.TargetType, x.TargetId });
    }
}

internal sealed class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.Title).HasMaxLength(256).IsRequired();
        builder.HasOne(x => x.MaintenancePlan).WithMany(x => x.WorkOrders).HasForeignKey(x => x.MaintenancePlanId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.ServiceRequest).WithMany(x => x.WorkOrders).HasForeignKey(x => x.ServiceRequestId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.Record).WithOne(x => x.WorkOrder).HasForeignKey<MaintenanceRecord>(x => x.WorkOrderId);
    }
}

internal sealed class MaintenanceRecordConfiguration : IEntityTypeConfiguration<MaintenanceRecord>
{
    public void Configure(EntityTypeBuilder<MaintenanceRecord> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.WorkScope).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Result).HasMaxLength(2000).IsRequired();
    }
}

internal sealed class ServiceRequestConfiguration : IEntityTypeConfiguration<ServiceRequest>
{
    public void Configure(EntityTypeBuilder<ServiceRequest> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.RequestNumber).HasMaxLength(64).IsRequired();
        builder.Property(x => x.RequestTypeCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.SourceTypeCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.RejectionReason).HasMaxLength(2000);
        builder.HasIndex(x => x.RequestNumber).IsUnique();
        builder.HasOne(x => x.Defect).WithMany().HasForeignKey(x => x.DefectId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => new { x.TargetType, x.TargetId });
    }
}

internal sealed class TechnicalRestrictionConfiguration : IEntityTypeConfiguration<TechnicalRestriction>
{
    public void Configure(EntityTypeBuilder<TechnicalRestriction> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.RestrictionTypeCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.TerminationReason).HasMaxLength(2000);
        builder.HasOne(x => x.Defect).WithMany().HasForeignKey(x => x.DefectId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => new { x.TargetType, x.TargetId, x.Status });
    }
}

internal sealed class ReleaseToServiceConfiguration : IEntityTypeConfiguration<ReleaseToService>
{
    public void Configure(EntityTypeBuilder<ReleaseToService> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.ReleaseScopeCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.RestrictionDescription).HasMaxLength(2000);
        builder.Property(x => x.DecisionReason).HasMaxLength(2000).IsRequired();
        builder.HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.Defect).WithMany().HasForeignKey(x => x.DefectId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => new { x.TargetType, x.TargetId, x.EffectiveFrom });
    }
}

internal sealed class TechnicalStatusHistoryConfiguration : IEntityTypeConfiguration<TechnicalStatusHistory>
{
    public void Configure(EntityTypeBuilder<TechnicalStatusHistory> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.PreviousStatusCode).HasMaxLength(64);
        builder.Property(x => x.NewStatusCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(2000).IsRequired();
        builder.HasIndex(x => new { x.TargetType, x.TargetId, x.EffectiveAt });
    }
}

internal sealed class DefectConfiguration : IEntityTypeConfiguration<Defect>
{
    public void Configure(EntityTypeBuilder<Defect> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.Title).HasMaxLength(256).IsRequired();
        builder.Property(x => x.OriginalReport).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.DefectTypeCode).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.TargetType, x.TargetId });
        builder.HasIndex(x => x.DefectTypeCode);
    }
}
