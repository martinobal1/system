using IsSuas.Domain.Common;
using IsSuas.Domain.Fleet;
using IsSuas.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IsSuas.Infrastructure.Persistence.Configurations;

internal static class AuditableConfig
{
    public static void Configure<T>(EntityTypeBuilder<T> builder) where T : AuditableEntity
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RowVersion).IsConcurrencyToken();
        builder.HasIndex(x => x.IsActive);
    }
}

internal sealed class OrgUnitConfiguration : IEntityTypeConfiguration<OrgUnit>
{
    public void Configure(EntityTypeBuilder<OrgUnit> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.Code).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Path).HasMaxLength(1024).IsRequired();
        builder.Property(x => x.Kind).HasConversion<int>();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => x.Kind);
        builder.HasOne(x => x.Parent).WithMany(x => x.Children).HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.FirstName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.LastName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.HasOne(x => x.HomeOrgUnit).WithMany().HasForeignKey(x => x.HomeOrgUnitId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.UserName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
        builder.Property(x => x.AccessLevel).HasConversion<int>();
        builder.Property(x => x.LicenseStatus).HasConversion<int>();
        builder.HasIndex(x => x.UserName).IsUnique();
        builder.HasOne(x => x.Person).WithMany().HasForeignKey(x => x.PersonId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.NameSk).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
    }
}

internal sealed class RoleAssignmentConfiguration : IEntityTypeConfiguration<RoleAssignment>
{
    public void Configure(EntityTypeBuilder<RoleAssignment> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.Reason).HasMaxLength(512).IsRequired();
        builder.HasOne(x => x.Account).WithMany(x => x.RoleAssignments).HasForeignKey(x => x.AccountId);
        builder.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId);
        builder.HasOne(x => x.OrgUnit).WithMany().HasForeignKey(x => x.OrgUnitId);
        builder.HasIndex(x => new { x.AccountId, x.RoleId, x.OrgUnitId, x.ValidFrom });
    }
}

internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).HasMaxLength(128).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ActorUserName).HasMaxLength(128);
        builder.Property(x => x.Result).HasMaxLength(64);
        builder.HasIndex(x => x.OccurredAt);
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
    }
}

internal sealed class LookupCodeConfiguration : IEntityTypeConfiguration<LookupCode>
{
    public void Configure(EntityTypeBuilder<LookupCode> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.Code).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasMany(x => x.Items).WithOne(x => x.LookupCode).HasForeignKey(x => x.LookupCodeId);
    }
}

internal sealed class LookupItemConfiguration : IEntityTypeConfiguration<LookupItem>
{
    public void Configure(EntityTypeBuilder<LookupItem> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.ItemCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.LabelSk).HasMaxLength(256).IsRequired();
        builder.HasIndex(x => new { x.LookupCodeId, x.ItemCode }).IsUnique();
    }
}

internal sealed class ConfigRuleConfiguration : IEntityTypeConfiguration<ConfigRule>
{
    public void Configure(EntityTypeBuilder<ConfigRule> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.Key).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => new { x.Key, x.Version }).IsUnique();
    }
}

internal sealed class UasTypeDocConfiguration : IEntityTypeConfiguration<UasTypeDoc>
{
    public void Configure(EntityTypeBuilder<UasTypeDoc> builder)
    {
        AuditableConfig.Configure(builder);
        builder.ToTable("UasTypeDocs");
        builder.Property(x => x.Slug).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Manufacturer).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Model).HasMaxLength(128).IsRequired();
        builder.Property(x => x.DataJson).HasColumnType("text").IsRequired();
        builder.Property(x => x.ManualStorageKey).HasMaxLength(512);
        builder.Property(x => x.ManualFileName).HasMaxLength(260);
        builder.HasIndex(x => x.Slug).IsUnique();
    }
}

internal sealed class UasConfiguration : IEntityTypeConfiguration<Uas>
{
    public void Configure(EntityTypeBuilder<Uas> builder)
    {
        AuditableConfig.Configure(builder);
        builder.ToTable("UasAircraft");
        builder.Property(x => x.SerialNumber).HasMaxLength(128).IsRequired();
        builder.Property(x => x.RegistrationNumber).HasMaxLength(64);
        builder.Property(x => x.CertificateNumber).HasMaxLength(64);
        builder.Property(x => x.LogbookNumber).HasMaxLength(64);
        builder.Property(x => x.InventoryAssetNumber).HasMaxLength(64);
        builder.Property(x => x.Manufacturer).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Model).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => x.SerialNumber).IsUnique();
        builder.HasIndex(x => x.RegistrationNumber).IsUnique().HasFilter("\"RegistrationNumber\" IS NOT NULL");
        builder.HasIndex(x => x.CertificateNumber);
        builder.HasIndex(x => x.InventoryAssetNumber);
        builder.Property(x => x.PendingReason).HasMaxLength(1000);
        builder.HasIndex(x => x.PendingOrgUnitId);
        builder.HasOne(x => x.OwningOrgUnit).WithMany().HasForeignKey(x => x.OwningOrgUnitId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class UasAssignmentConfiguration : IEntityTypeConfiguration<UasAssignment>
{
    public void Configure(EntityTypeBuilder<UasAssignment> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.Reason).HasMaxLength(512).IsRequired();
        builder.HasOne(x => x.Uas).WithMany(x => x.Assignments).HasForeignKey(x => x.UasId);
        builder.HasOne(x => x.OrgUnit).WithMany().HasForeignKey(x => x.OrgUnitId);
        builder.HasIndex(x => new { x.UasId, x.ValidFrom });
    }
}

internal sealed class ComponentConfiguration : IEntityTypeConfiguration<Component>
{
    public void Configure(EntityTypeBuilder<Component> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.InternalNumber).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
        builder.HasIndex(x => x.InternalNumber).IsUnique();
    }
}

internal sealed class InstallationConfiguration : IEntityTypeConfiguration<Installation>
{
    public void Configure(EntityTypeBuilder<Installation> builder)
    {
        AuditableConfig.Configure(builder);
        builder.HasOne(x => x.Component).WithMany(x => x.Installations).HasForeignKey(x => x.ComponentId);
        builder.HasOne(x => x.Uas).WithMany(x => x.Installations).HasForeignKey(x => x.UasId);
        builder.HasIndex(x => new { x.ComponentId, x.RemovedAt });
    }
}

internal sealed class BatteryConfiguration : IEntityTypeConfiguration<Battery>
{
    public void Configure(EntityTypeBuilder<Battery> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.InternalNumber).HasMaxLength(64).IsRequired();
        builder.Property(x => x.SerialNumber).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => x.InternalNumber).IsUnique();
        builder.HasIndex(x => x.SerialNumber).IsUnique();
    }
}

internal sealed class BatteryAssignmentConfiguration : IEntityTypeConfiguration<BatteryAssignment>
{
    public void Configure(EntityTypeBuilder<BatteryAssignment> builder)
    {
        AuditableConfig.Configure(builder);
        builder.HasOne(x => x.Battery).WithMany(x => x.Assignments).HasForeignKey(x => x.BatteryId);
        builder.HasOne(x => x.Uas).WithMany().HasForeignKey(x => x.UasId);
    }
}

internal sealed class UasStatusHistoryConfiguration : IEntityTypeConfiguration<UasStatusHistory>
{
    public void Configure(EntityTypeBuilder<UasStatusHistory> builder)
    {
        AuditableConfig.Configure(builder);
        builder.HasOne(x => x.Uas).WithMany(x => x.StatusHistory).HasForeignKey(x => x.UasId);
    }
}

internal sealed class OrgUnitMoveHistoryConfiguration : IEntityTypeConfiguration<OrgUnitMoveHistory>
{
    public void Configure(EntityTypeBuilder<OrgUnitMoveHistory> builder)
    {
        AuditableConfig.Configure(builder);
        builder.HasOne(x => x.OrgUnit).WithMany(x => x.MoveHistory).HasForeignKey(x => x.OrgUnitId);
    }
}
