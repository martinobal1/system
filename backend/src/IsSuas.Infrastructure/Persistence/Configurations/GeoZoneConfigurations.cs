using IsSuas.Domain.GeoZones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IsSuas.Infrastructure.Persistence.Configurations;

internal sealed class GeoZoneImportConfiguration : IEntityTypeConfiguration<GeoZoneImport>
{
    public void Configure(EntityTypeBuilder<GeoZoneImport> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.FileName).HasMaxLength(512).IsRequired();
        builder.Property(x => x.ContentHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Format).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.ResultMessage).HasMaxLength(2000);
        builder.HasOne(x => x.ImportedByAccount).WithMany().HasForeignKey(x => x.ImportedByAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.StoredFile).WithMany().HasForeignKey(x => x.StoredFileId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Zones).WithOne(x => x.Import).HasForeignKey(x => x.ImportId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.ImportedAt);
        builder.HasIndex(x => x.ContentHash);
    }
}

internal sealed class GeoZoneConfiguration : IEntityTypeConfiguration<GeoZone>
{
    public void Configure(EntityTypeBuilder<GeoZone> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.ExternalKey).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ZoneType).HasMaxLength(128);
        builder.Property(x => x.RestrictionDescription).HasMaxLength(4000);
        builder.Property(x => x.FlightConditions).HasMaxLength(4000);
        builder.Property(x => x.HorizontalLimits).HasMaxLength(1000);
        builder.Property(x => x.VerticalLimits).HasMaxLength(1000);
        builder.Property(x => x.AuthorityContact).HasMaxLength(512);
        builder.Property(x => x.SourceLabel).HasMaxLength(256);
        builder.Property(x => x.GeometryGeoJson).IsRequired();
        builder.Property(x => x.PropertiesJson).IsRequired();
        builder.HasIndex(x => new { x.ImportId, x.Sequence });
        builder.HasIndex(x => new { x.ImportId, x.Name });
    }
}
