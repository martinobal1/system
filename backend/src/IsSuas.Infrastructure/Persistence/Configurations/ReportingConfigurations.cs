using IsSuas.Domain.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IsSuas.Infrastructure.Persistence.Configurations;

internal sealed class OfficialReportConfiguration : IEntityTypeConfiguration<OfficialReport>
{
    public void Configure(EntityTypeBuilder<OfficialReport> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.ReportTypeCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.TitleSk).HasMaxLength(256).IsRequired();
        builder.Property(x => x.FiltersJson).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.RulesJson).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.SnapshotJson).IsRequired();
        builder.Property(x => x.ApprovalReason).HasMaxLength(2000);
        builder.HasIndex(x => x.ReportFamilyId);
        builder.HasIndex(x => new { x.ReportTypeCode, x.Status });
    }
}

internal sealed class PilotUasAuthorizationConfiguration : IEntityTypeConfiguration<PilotUasAuthorization>
{
    public void Configure(EntityTypeBuilder<PilotUasAuthorization> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.HasOne(x => x.Pilot).WithMany().HasForeignKey(x => x.PilotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Uas).WithMany().HasForeignKey(x => x.UasId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.OrgUnit).WithMany().HasForeignKey(x => x.OrgUnitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.PilotId, x.UasId, x.ValidFrom });
        builder.HasIndex(x => new { x.OrgUnitId, x.IsActive });
    }
}

internal sealed class MonthlyFlightReportConfiguration : IEntityTypeConfiguration<MonthlyFlightReport>
{
    public void Configure(EntityTypeBuilder<MonthlyFlightReport> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.Note).HasMaxLength(2000);
        builder.HasOne(x => x.OrgUnit).WithMany().HasForeignKey(x => x.OrgUnitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.OrgUnitId, x.Year, x.Month }).IsUnique();
        builder.HasIndex(x => new { x.Year, x.Month, x.Status });
        builder.HasMany(x => x.Lines).WithOne(x => x.Report).HasForeignKey(x => x.ReportId);
        builder.HasMany(x => x.ChangeLogs).WithOne(x => x.Report).HasForeignKey(x => x.ReportId);
    }
}

internal sealed class MonthlyFlightReportLineConfiguration : IEntityTypeConfiguration<MonthlyFlightReportLine>
{
    public void Configure(EntityTypeBuilder<MonthlyFlightReportLine> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.HasOne(x => x.Pilot).WithMany().HasForeignKey(x => x.PilotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Uas).WithMany().HasForeignKey(x => x.UasId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ReportId, x.PilotId, x.UasId });
    }
}

internal sealed class MonthlyFlightReportChangeLogConfiguration : IEntityTypeConfiguration<MonthlyFlightReportChangeLog>
{
    public void Configure(EntityTypeBuilder<MonthlyFlightReportChangeLog> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.ChangeType).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.ReportId, x.ChangedAt });
    }
}
