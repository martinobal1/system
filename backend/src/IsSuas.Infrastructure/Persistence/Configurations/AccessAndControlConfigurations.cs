using IsSuas.Domain.Controls;
using IsSuas.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IsSuas.Infrastructure.Persistence.Configurations;

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.Code).HasMaxLength(128).IsRequired();
        builder.Property(x => x.NameSk).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ModuleCode).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
    }
}

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        AuditableConfig.Configure(builder);
        builder.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.RoleId, x.PermissionId }).IsUnique();
    }
}

internal sealed class DelegatedPermissionConfiguration : IEntityTypeConfiguration<DelegatedPermission>
{
    public void Configure(EntityTypeBuilder<DelegatedPermission> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.PermissionCode).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(512).IsRequired();
        builder.HasOne(x => x.FromAccount).WithMany().HasForeignKey(x => x.FromAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ToAccount).WithMany().HasForeignKey(x => x.ToAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.OrgUnit).WithMany().HasForeignKey(x => x.OrgUnitId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.Result).HasMaxLength(64).IsRequired();
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasMaxLength(512);
        builder.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.AccountId, x.IssuedAt });
    }
}

internal sealed class ControlAuditConfiguration : IEntityTypeConfiguration<ControlAudit>
{
    public void Configure(EntityTypeBuilder<ControlAudit> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.Title).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ScopeDescription).HasMaxLength(4000);
        builder.Property(x => x.Summary).HasMaxLength(4000);
        builder.HasOne(x => x.OrgUnit).WithMany().HasForeignKey(x => x.OrgUnitId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ControlTeamMemberConfiguration : IEntityTypeConfiguration<ControlTeamMember>
{
    public void Configure(EntityTypeBuilder<ControlTeamMember> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.RoleInTeam).HasMaxLength(64).IsRequired();
        builder.HasOne(x => x.ControlAudit).WithMany(x => x.TeamMembers).HasForeignKey(x => x.ControlAuditId);
    }
}

internal sealed class ControlChecklistItemConfiguration : IEntityTypeConfiguration<ControlChecklistItem>
{
    public void Configure(EntityTypeBuilder<ControlChecklistItem> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.Code).HasMaxLength(64).IsRequired();
        builder.Property(x => x.QuestionSk).HasMaxLength(512).IsRequired();
        builder.Property(x => x.ResultNote).HasMaxLength(2000);
        builder.HasOne(x => x.ControlAudit).WithMany(x => x.ChecklistItems).HasForeignKey(x => x.ControlAuditId);
    }
}

internal sealed class ControlFindingConfiguration : IEntityTypeConfiguration<ControlFinding>
{
    public void Configure(EntityTypeBuilder<ControlFinding> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.Title).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.RelatedEntityType).HasMaxLength(128);
        builder.HasOne(x => x.ControlAudit).WithMany(x => x.Findings).HasForeignKey(x => x.ControlAuditId);
    }
}

internal sealed class ControlCorrectiveActionConfiguration : IEntityTypeConfiguration<ControlCorrectiveAction>
{
    public void Configure(EntityTypeBuilder<ControlCorrectiveAction> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.Title).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(4000);
        builder.Property(x => x.ProgressNote).HasMaxLength(4000);
        builder.HasOne(x => x.ControlFinding).WithMany(x => x.Actions).HasForeignKey(x => x.ControlFindingId);
    }
}

internal sealed class ControlReportConfiguration : IEntityTypeConfiguration<ControlReport>
{
    public void Configure(EntityTypeBuilder<ControlReport> builder)
    {
        AuditableConfig.Configure(builder);
        builder.Property(x => x.ContentJson).IsRequired();
        builder.HasOne(x => x.ControlAudit).WithMany(x => x.Reports).HasForeignKey(x => x.ControlAuditId);
    }
}
