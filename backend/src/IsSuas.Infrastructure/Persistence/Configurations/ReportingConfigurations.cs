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
