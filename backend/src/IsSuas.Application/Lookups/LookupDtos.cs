namespace IsSuas.Application.Lookups;

public sealed record LookupItemDto(Guid Id, string ItemCode, string LabelSk, int SortOrder, bool IsActive, uint RowVersion);
public sealed record LookupCodeDto(string Code, string NameSk, string? DescriptionSk, IReadOnlyList<LookupItemDto> Items, bool IsActive);
public sealed record LookupCodeSummaryDto(Guid Id, string Code, string NameSk, int ItemCount, bool IsActive);

public sealed record CreateLookupCodeRequest(string Code, string NameSk, string? DescriptionSk);
public sealed record CreateLookupItemRequest(string ItemCode, string LabelSk, int SortOrder, string? MetadataJson);
public sealed record DeactivateLookupItemRequest(uint RowVersion);

public sealed record AuditEventDto(
    Guid Id,
    DateTimeOffset OccurredAt,
    Guid? ActorAccountId,
    string ActorUserName,
    string Action,
    string EntityType,
    Guid? EntityId,
    string Result,
    string? BeforeJson,
    string? AfterJson);

public sealed record ConfigRuleDto(
    Guid Id,
    string Key,
    string ValueJson,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    int Version,
    string? DescriptionSk,
    bool IsActive,
    uint RowVersion);

public sealed record CreateConfigRuleRequest(
    string Key,
    string ValueJson,
    DateTimeOffset EffectiveFrom,
    string? DescriptionSk);

public sealed record IntegrationConnectorDto(
    Guid Id,
    string Code,
    string NameSk,
    string ConnectorType,
    string EndpointUrl,
    string Format,
    string? ScheduleCron,
    string Status,
    DateTimeOffset? LastRunAt,
    string? LastRunResult,
    string? LastError,
    string? SecretRef,
    bool IsActive,
    uint RowVersion);

public sealed record UpsertIntegrationConnectorRequest(
    string Code,
    string NameSk,
    string ConnectorType,
    string EndpointUrl,
    string Format,
    string? ScheduleCron,
    string? SecretRef,
    bool Activate);

public sealed record RecordIntegrationRunRequest(bool Success, string? ResultMessage, string? ErrorMessage, uint RowVersion);

public sealed record SystemInfoDto(
    string AppVersion,
    string EnvironmentName,
    string DatabaseMode,
    DateTimeOffset ServerTimeUtc,
    int ActiveConfigRules,
    int LookupCodes,
    int IntegrationConnectors,
    IReadOnlyList<string> ActiveConfigKeys,
    string StorageProvider,
    string EmailProvider,
    string AirspaceProvider,
    bool OidcConfigured);
