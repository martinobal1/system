using IsSuas.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IsSuas.Infrastructure.Persistence.Configurations;

internal sealed class TrashEntryConfiguration : IEntityTypeConfiguration<TrashEntry>
{
    public void Configure(EntityTypeBuilder<TrashEntry> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Category).HasMaxLength(64).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(300).IsRequired();
        builder.Property(x => x.OrgUnitPath).HasMaxLength(512);
        builder.Property(x => x.Reason).HasMaxLength(1000);
        builder.Property(x => x.DeletedByUserName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.RestoredByUserName).HasMaxLength(128);
        builder.Property(x => x.PurgedByUserName).HasMaxLength(128);
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
        builder.HasIndex(x => x.Category);
    }
}
