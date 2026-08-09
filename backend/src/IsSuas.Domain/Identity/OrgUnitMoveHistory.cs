using IsSuas.Domain.Common;

namespace IsSuas.Domain.Identity;

public class OrgUnitMoveHistory : AuditableEntity
{
    public Guid OrgUnitId { get; set; }
    public OrgUnit OrgUnit { get; set; } = null!;
    public Guid? PreviousParentId { get; set; }
    public Guid? NewParentId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset MovedAt { get; set; }
}
