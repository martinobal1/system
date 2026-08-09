using IsSuas.Domain.Common;
using IsSuas.Domain.Enums;

namespace IsSuas.Domain.Integrations;

public class IntegrationConnector : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string NameSk { get; set; } = string.Empty;
    public string ConnectorType { get; set; } = "REST";
    public string EndpointUrl { get; set; } = string.Empty;
    public string Format { get; set; } = "JSON";
    public string? ScheduleCron { get; set; }
    public IntegrationConnectorStatus Status { get; set; } = IntegrationConnectorStatus.Inactive;
    public DateTimeOffset? LastRunAt { get; set; }
    public string? LastRunResult { get; set; }
    public string? LastError { get; set; }
    /// <summary>Reference to secret store key — never the secret itself.</summary>
    public string? SecretRef { get; set; }
}
