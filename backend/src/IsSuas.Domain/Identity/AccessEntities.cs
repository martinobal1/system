using IsSuas.Domain.Common;

namespace IsSuas.Domain.Identity;

/// <summary>ENT-007 — funkčné oprávnenie (katalóg).</summary>
public class Permission : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string NameSk { get; set; } = string.Empty;
    public string ModuleCode { get; set; } = string.Empty;
}

/// <summary>ENT-008 — väzba rola ↔ oprávnenie.</summary>
public class RolePermission : AuditableEntity
{
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public Guid PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
}

/// <summary>ENT-010 — dočasné delegovanie oprávnenia.</summary>
public class DelegatedPermission : AuditableEntity
{
    public Guid FromAccountId { get; set; }
    public Account FromAccount { get; set; } = null!;
    public Guid ToAccountId { get; set; }
    public Account ToAccount { get; set; } = null!;
    public string PermissionCode { get; set; } = string.Empty;
    public Guid? OrgUnitId { get; set; }
    public OrgUnit? OrgUnit { get; set; }
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid? ApprovedByAccountId { get; set; }
}

/// <summary>ENT-011 — používateľská relácia.</summary>
public class UserSession : AuditableEntity
{
    public Guid AccountId { get; set; }
    public Account Account { get; set; } = null!;
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string Result { get; set; } = "Success";
}
