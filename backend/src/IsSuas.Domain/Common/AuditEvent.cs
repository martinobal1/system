using IsSuas.Domain.Common;

namespace IsSuas.Domain.Common;

public class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; set; }
    public Guid? ActorAccountId { get; set; }
    public string ActorUserName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string Result { get; set; } = "Success";
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public string? CorrelationId { get; set; }
    public string? IpAddress { get; set; }
}
