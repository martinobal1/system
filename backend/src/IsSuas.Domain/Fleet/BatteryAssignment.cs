using IsSuas.Domain.Common;

namespace IsSuas.Domain.Fleet;

public class BatteryAssignment : AuditableEntity
{
    public Guid BatteryId { get; set; }
    public Battery Battery { get; set; } = null!;
    public Guid? UasId { get; set; }
    public Uas? Uas { get; set; }
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
    public string? Reason { get; set; }
}
