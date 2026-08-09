using IsSuas.Domain.Common;
using IsSuas.Domain.Identity;

namespace IsSuas.Domain.Fleet;

public class UasAssignment : AuditableEntity
{
    public Guid UasId { get; set; }
    public Uas Uas { get; set; } = null!;
    public Guid OrgUnitId { get; set; }
    public OrgUnit OrgUnit { get; set; } = null!;
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
    public string Reason { get; set; } = string.Empty;
}
