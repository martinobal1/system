using IsSuas.Domain.Fleet;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IsSuas.Infrastructure.Persistence.Configurations;

internal sealed class ComponentCompatibilityConfiguration : IEntityTypeConfiguration<ComponentCompatibility>
{
    public void Configure(EntityTypeBuilder<ComponentCompatibility> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.UasTypeCode).HasMaxLength(64);
        builder.Property(x => x.RestrictionText).HasMaxLength(2000);
        builder.HasOne(x => x.Component).WithMany().HasForeignKey(x => x.ComponentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Uas).WithMany().HasForeignKey(x => x.UasId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => new { x.ComponentId, x.UasId, x.UasTypeCode, x.ValidFrom });
    }
}

internal sealed class BatteryCompatibilityConfiguration : IEntityTypeConfiguration<BatteryCompatibility>
{
    public void Configure(EntityTypeBuilder<BatteryCompatibility> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.UasTypeCode).HasMaxLength(64);
        builder.Property(x => x.RestrictionText).HasMaxLength(2000);
        builder.HasOne(x => x.Battery).WithMany().HasForeignKey(x => x.BatteryId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Uas).WithMany().HasForeignKey(x => x.UasId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => new { x.BatteryId, x.UasId, x.UasTypeCode, x.ValidFrom });
    }
}

internal sealed class BatteryTechnicalRecordConfiguration : IEntityTypeConfiguration<BatteryTechnicalRecord>
{
    public void Configure(EntityTypeBuilder<BatteryTechnicalRecord> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.UnitCode).HasMaxLength(32);
        builder.Property(x => x.TextValue).HasMaxLength(1000);
        builder.Property(x => x.CorrectionReason).HasMaxLength(1000);
        builder.Property(x => x.MeasuredValue).HasPrecision(18, 4);
        builder.HasOne(x => x.Battery).WithMany().HasForeignKey(x => x.BatteryId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.SourceFlight).WithMany().HasForeignKey(x => x.SourceFlightId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.MeasuredByPerson).WithMany().HasForeignKey(x => x.MeasuredByPersonId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => new { x.BatteryId, x.RecordedAt });
    }
}

internal sealed class BatteryOperationRecordConfiguration : IEntityTypeConfiguration<BatteryOperationRecord>
{
    public void Configure(EntityTypeBuilder<BatteryOperationRecord> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.ResultCode).HasMaxLength(64);
        builder.Property(x => x.Note).HasMaxLength(2000);
        builder.Property(x => x.TemperatureC).HasPrecision(18, 4);
        builder.HasOne(x => x.Battery).WithMany().HasForeignKey(x => x.BatteryId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.RelatedFlight).WithMany().HasForeignKey(x => x.RelatedFlightId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.PerformedByPerson).WithMany().HasForeignKey(x => x.PerformedByPersonId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => new { x.BatteryId, x.OccurredAt });
    }
}

internal sealed class BatteryStatusHistoryConfiguration : IEntityTypeConfiguration<BatteryStatusHistory>
{
    public void Configure(EntityTypeBuilder<BatteryStatusHistory> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.Reason).HasMaxLength(2000);
        builder.HasOne(x => x.Battery).WithMany().HasForeignKey(x => x.BatteryId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.BatteryId, x.EffectiveAt });
    }
}
