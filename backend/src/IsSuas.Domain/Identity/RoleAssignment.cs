using IsSuas.Domain.Common;

namespace IsSuas.Domain.Identity;

public class RoleAssignment : AuditableEntity
{
    public Guid AccountId { get; set; }
    public Account Account { get; set; } = null!;
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public Guid OrgUnitId { get; set; }
    public OrgUnit OrgUnit { get; set; } = null!;
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
    public string Reason { get; set; } = string.Empty;
}
