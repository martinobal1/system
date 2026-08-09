using IsSuas.Domain.Common;

namespace IsSuas.Domain.Fleet;

public class Installation : AuditableEntity
{
    public Guid ComponentId { get; set; }
    public Component Component { get; set; } = null!;
    public Guid UasId { get; set; }
    public Uas Uas { get; set; } = null!;
    public DateTimeOffset InstalledAt { get; set; }
    public DateTimeOffset? RemovedAt { get; set; }
    public string? Reason { get; set; }
}
