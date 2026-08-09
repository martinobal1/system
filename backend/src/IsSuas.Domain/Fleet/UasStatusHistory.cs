using IsSuas.Domain.Common;
using IsSuas.Domain.Enums;

namespace IsSuas.Domain.Fleet;

public class UasStatusHistory : AuditableEntity
{
    public Guid UasId { get; set; }
    public Uas Uas { get; set; } = null!;
    public UasLifecycleStatus FromStatus { get; set; }
    public UasLifecycleStatus ToStatus { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset ChangedAt { get; set; }
}
