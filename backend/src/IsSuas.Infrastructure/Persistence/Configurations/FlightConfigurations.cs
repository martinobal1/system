using IsSuas.Domain.Flights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IsSuas.Infrastructure.Persistence.Configurations;

internal sealed class FlightConfiguration : IEntityTypeConfiguration<Flight>
{
    public void Configure(EntityTypeBuilder<Flight> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.StandaloneReason).HasMaxLength(1024);
        builder.Property(x => x.FlightNumber).HasMaxLength(64);
        builder.Property(x => x.Location).HasMaxLength(512);
        builder.Property(x => x.Purpose).HasMaxLength(256);
        builder.Property(x => x.ChecklistResult).HasMaxLength(512);
        builder.HasOne(x => x.Operation).WithMany().HasForeignKey(x => x.OperationId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.OrgUnit).WithMany().HasForeignKey(x => x.OrgUnitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Uas).WithMany().HasForeignKey(x => x.UasId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.Pilot).WithMany().HasForeignKey(x => x.PilotId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.Battery).WithMany().HasForeignKey(x => x.BatteryId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.FlightNumber);
        builder.HasIndex(x => x.OperationId);
    }
}

internal sealed class FlightTrackPointConfiguration : IEntityTypeConfiguration<FlightTrackPoint>
{
    public void Configure(EntityTypeBuilder<FlightTrackPoint> builder)
    {
        AuditableConfig.Configure(builder);
        builder.HasOne(x => x.Flight).WithMany(x => x.TrackPoints).HasForeignKey(x => x.FlightId);
        builder.HasIndex(x => new { x.FlightId, x.Sequence });
    }
}

internal sealed class TelemetryImportConfiguration : IEntityTypeConfiguration<TelemetryImport>
{
    public void Configure(EntityTypeBuilder<TelemetryImport> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.FileName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ContentHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Format).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Source).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.ContentHash).IsUnique();
        builder.HasOne(x => x.Flight).WithMany(x => x.TelemetryImports).HasForeignKey(x => x.FlightId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
