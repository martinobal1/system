using System.Globalization;
using System.Reflection;
using System.Text;
using IsSuas.Application.Abstractions;
using IsSuas.Application.Lookups;
using IsSuas.Application.Policies;
using IsSuas.Domain.Common;
using IsSuas.Domain.Enums;
using IsSuas.Domain.Integrations;
using IsSuas.Infrastructure.Integrations;
using IsSuas.Infrastructure.Messaging;
using IsSuas.Infrastructure.Persistence;
using IsSuas.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace IsSuas.Infrastructure.Services;

public interface ILookupService
{
    Task<IReadOnlyList<LookupCodeSummaryDto>> ListCodesAsync(CancellationToken cancellationToken = default);
    Task<LookupCodeDto> GetAsync(string code, CancellationToken cancellationToken = default);
    Task<LookupCodeDto> CreateCodeAsync(CreateLookupCodeRequest request, CancellationToken cancellationToken = default);
    Task<LookupItemDto> AddItemAsync(string code, CreateLookupItemRequest request, CancellationToken cancellationToken = default);
    Task<LookupItemDto> DeactivateItemAsync(Guid itemId, DeactivateLookupItemRequest request, CancellationToken cancellationToken = default);
}

public interface IConfigRuleService
{
    Task<IReadOnlyList<ConfigRuleDto>> ListAsync(string? key, CancellationToken cancellationToken = default);
    Task<ConfigRuleDto?> GetEffectiveAsync(string key, DateTimeOffset? at, CancellationToken cancellationToken = default);
    Task<ConfigRuleDto> CreateVersionAsync(CreateConfigRuleRequest request, CancellationToken cancellationToken = default);
}

public interface IIntegrationConnectorService
{
    Task<IReadOnlyList<IntegrationConnectorDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<IntegrationConnectorDto> UpsertAsync(UpsertIntegrationConnectorRequest request, CancellationToken cancellationToken = default);
    Task<IntegrationConnectorDto> RecordRunAsync(Guid id, RecordIntegrationRunRequest request, CancellationToken cancellationToken = default);
}

public interface IAuditQueryService
{
    Task<IReadOnlyList<AuditEventDto>> SearchAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? entityType,
        Guid? entityId,
        string? action,
        string? actorUserName,
        string? result,
        CancellationToken cancellationToken = default);

    Task<string> ExportCsvAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? entityType,
        Guid? entityId,
        string? action,
        string? actorUserName,
        string? result,
        CancellationToken cancellationToken = default);
}

public interface ISystemInfoService
{
    Task<SystemInfoDto> GetAsync(CancellationToken cancellationToken = default);
}

public sealed class LookupService(
    IsSuasDbContext db,
    IAuthorizationPolicyService policy,
    ICurrentUser currentUser,
    IAuditWriter audit) : ILookupService
{
    public async Task<IReadOnlyList<LookupCodeSummaryDto>> ListCodesAsync(CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.LookupsRead);
        var items = await db.LookupCodes.AsNoTracking()
            .Include(c => c.Items)
            .OrderBy(c => c.Code)
            .ToListAsync(cancellationToken);
        return items.Select(c => new LookupCodeSummaryDto(
            c.Id, c.Code, c.NameSk, c.Items.Count(i => i.IsActive), c.IsActive)).ToList();
    }

    public async Task<LookupCodeDto> GetAsync(string code, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.LookupsRead);
        var lookup = await db.LookupCodes.AsNoTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Code == code, cancellationToken)
            ?? throw new NotFoundException($"Číselník '{code}' nebol nájdený.");

        return MapCode(lookup, includeInactiveItems: true);
    }

    public async Task<LookupCodeDto> CreateCodeAsync(CreateLookupCodeRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.LookupsWrite);
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.NameSk))
            throw new ValidationException("Kód a názov sú povinné.");
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.LookupCodes.AnyAsync(c => c.Code == code, cancellationToken))
            throw new ConflictException("Číselník s týmto kódom už existuje.");

        var entity = new LookupCode
        {
            Code = code,
            NameSk = request.NameSk.Trim(),
            DescriptionSk = request.DescriptionSk?.Trim(),
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.LookupCodes.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("LookupCode.Create", nameof(LookupCode), entity.Id, null, MapCode(entity, false), cancellationToken: cancellationToken);
        return MapCode(entity, false);
    }

    public async Task<LookupItemDto> AddItemAsync(string code, CreateLookupItemRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.LookupsWrite);
        var lookup = await db.LookupCodes.Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Code == code, cancellationToken)
            ?? throw new NotFoundException($"Číselník '{code}' nebol nájdený.");
        if (!lookup.IsActive)
            throw new ValidationException("Číselník je deaktivovaný.");
        if (string.IsNullOrWhiteSpace(request.ItemCode) || string.IsNullOrWhiteSpace(request.LabelSk))
            throw new ValidationException("Kód a označenie položky sú povinné.");

        var itemCode = request.ItemCode.Trim().ToUpperInvariant();
        if (lookup.Items.Any(i => i.ItemCode == itemCode && i.IsActive))
            throw new ConflictException("Aktívna položka s týmto kódom už existuje.");

        var item = new LookupItem
        {
            LookupCodeId = lookup.Id,
            ItemCode = itemCode,
            LabelSk = request.LabelSk.Trim(),
            SortOrder = request.SortOrder,
            MetadataJson = request.MetadataJson,
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.LookupItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("LookupItem.Create", nameof(LookupItem), item.Id, null, MapItem(item), cancellationToken: cancellationToken);
        return MapItem(item);
    }

    public async Task<LookupItemDto> DeactivateItemAsync(Guid itemId, DeactivateLookupItemRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.LookupsWrite);
        var item = await db.LookupItems.FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken)
            ?? throw new NotFoundException("Položka číselníka nebola nájdená.");
        if (item.RowVersion != request.RowVersion)
            throw new ConflictException("Záznam bol medzitým zmenený iným používateľom.");
        if (!item.IsActive)
            return MapItem(item);

        // Never delete — only deactivate.
        var before = MapItem(item);
        item.IsActive = false;
        item.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("LookupItem.Deactivate", nameof(LookupItem), item.Id, before, MapItem(item), cancellationToken: cancellationToken);
        return MapItem(item);
    }

    private static LookupCodeDto MapCode(LookupCode lookup, bool includeInactiveItems) =>
        new(lookup.Code, lookup.NameSk, lookup.DescriptionSk,
            lookup.Items
                .Where(i => includeInactiveItems || i.IsActive)
                .OrderBy(i => i.SortOrder)
                .Select(MapItem)
                .ToList(),
            lookup.IsActive);

    private static LookupItemDto MapItem(LookupItem i) =>
        new(i.Id, i.ItemCode, i.LabelSk, i.SortOrder, i.IsActive, i.RowVersion);
}

public sealed class ConfigRuleService(
    IsSuasDbContext db,
    IAuthorizationPolicyService policy,
    ICurrentUser currentUser,
    IAuditWriter audit,
    IClock clock) : IConfigRuleService
{
    public async Task<IReadOnlyList<ConfigRuleDto>> ListAsync(string? key, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.LookupsRead);
        var query = db.ConfigRules.AsNoTracking().Where(r => r.IsActive);
        if (!string.IsNullOrWhiteSpace(key))
            query = query.Where(r => r.Key == key);
        var items = await query.OrderBy(r => r.Key).ThenByDescending(r => r.Version).ToListAsync(cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task<ConfigRuleDto?> GetEffectiveAsync(string key, DateTimeOffset? at, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.LookupsRead);
        var moment = at ?? clock.UtcNow;
        var items = await db.ConfigRules.AsNoTracking()
            .Where(r => r.IsActive && r.Key == key && r.EffectiveFrom <= moment
                && (r.EffectiveTo == null || r.EffectiveTo > moment))
            .OrderByDescending(r => r.Version)
            .ToListAsync(cancellationToken);
        var best = items.FirstOrDefault();
        return best is null ? null : Map(best);
    }

    public async Task<ConfigRuleDto> CreateVersionAsync(CreateConfigRuleRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.LookupsWrite);
        if (string.IsNullOrWhiteSpace(request.Key) || string.IsNullOrWhiteSpace(request.ValueJson))
            throw new ValidationException("Kľúč a ValueJson sú povinné.");

        var key = request.Key.Trim();
        var existing = await db.ConfigRules.Where(r => r.Key == key).OrderByDescending(r => r.Version).ToListAsync(cancellationToken);
        var version = existing.Count == 0 ? 1 : existing[0].Version + 1;

        // Close previous open-ended version at EffectiveFrom of the new one — does not alter past closed records.
        foreach (var prev in existing.Where(r => r.EffectiveTo is null && r.EffectiveFrom < request.EffectiveFrom))
        {
            prev.EffectiveTo = request.EffectiveFrom;
            prev.UpdatedBy = currentUser.AccountId;
        }

        var entity = new ConfigRule
        {
            Key = key,
            ValueJson = request.ValueJson.Trim(),
            EffectiveFrom = request.EffectiveFrom,
            Version = version,
            DescriptionSk = request.DescriptionSk?.Trim(),
            CreatedBy = currentUser.AccountId,
            UpdatedBy = currentUser.AccountId
        };
        db.ConfigRules.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("ConfigRule.CreateVersion", nameof(ConfigRule), entity.Id, null, Map(entity), cancellationToken: cancellationToken);
        return Map(entity);
    }

    private static ConfigRuleDto Map(ConfigRule r) =>
        new(r.Id, r.Key, r.ValueJson, r.EffectiveFrom, r.EffectiveTo, r.Version, r.DescriptionSk, r.IsActive, r.RowVersion);
}

public sealed class IntegrationConnectorService(
    IsSuasDbContext db,
    IAuthorizationPolicyService policy,
    ICurrentUser currentUser,
    IAuditWriter audit,
    IClock clock) : IIntegrationConnectorService
{
    public async Task<IReadOnlyList<IntegrationConnectorDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.IntegrationsRead);
        var items = await db.IntegrationConnectors.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Code)
            .ToListAsync(cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task<IntegrationConnectorDto> UpsertAsync(UpsertIntegrationConnectorRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.IntegrationsWrite);
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.EndpointUrl))
            throw new ValidationException("Kód a endpoint sú povinné.");

        var code = request.Code.Trim().ToUpperInvariant();
        var entity = await db.IntegrationConnectors.FirstOrDefaultAsync(c => c.Code == code, cancellationToken);
        if (entity is null)
        {
            entity = new IntegrationConnector { Code = code, CreatedBy = currentUser.AccountId };
            db.IntegrationConnectors.Add(entity);
        }

        entity.NameSk = string.IsNullOrWhiteSpace(request.NameSk) ? code : request.NameSk.Trim();
        entity.ConnectorType = string.IsNullOrWhiteSpace(request.ConnectorType) ? "REST" : request.ConnectorType.Trim();
        entity.EndpointUrl = request.EndpointUrl.Trim();
        entity.Format = string.IsNullOrWhiteSpace(request.Format) ? "JSON" : request.Format.Trim();
        entity.ScheduleCron = request.ScheduleCron?.Trim();
        // Store only secret reference, never raw secret.
        entity.SecretRef = string.IsNullOrWhiteSpace(request.SecretRef) ? entity.SecretRef : request.SecretRef.Trim();
        entity.Status = request.Activate ? IntegrationConnectorStatus.Active : IntegrationConnectorStatus.Inactive;
        entity.UpdatedBy = currentUser.AccountId;

        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("IntegrationConnector.Upsert", nameof(IntegrationConnector), entity.Id, null, Map(entity), cancellationToken: cancellationToken);
        return Map(entity);
    }

    public async Task<IntegrationConnectorDto> RecordRunAsync(Guid id, RecordIntegrationRunRequest request, CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.IntegrationsWrite);
        var entity = await db.IntegrationConnectors.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException("Konektor nebol nájdený.");
        if (entity.RowVersion != request.RowVersion)
            throw new ConflictException("Záznam bol medzitým zmenený iným používateľom.");

        entity.LastRunAt = clock.UtcNow;
        entity.LastRunResult = Sanitize(request.ResultMessage);
        entity.LastError = request.Success ? null : Sanitize(request.ErrorMessage);
        entity.Status = request.Success ? IntegrationConnectorStatus.Active : IntegrationConnectorStatus.Error;
        entity.UpdatedBy = currentUser.AccountId;
        await db.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    private static IntegrationConnectorDto Map(IntegrationConnector c) =>
        new(c.Id, c.Code, c.NameSk, c.ConnectorType, c.EndpointUrl, c.Format, c.ScheduleCron,
            c.Status.ToString(), c.LastRunAt, c.LastRunResult, c.LastError, c.SecretRef, c.IsActive, c.RowVersion);

    private static string? Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim();
        // Strip obvious secret patterns from technical messages.
        if (v.Contains("password", StringComparison.OrdinalIgnoreCase)
            || v.Contains("token", StringComparison.OrdinalIgnoreCase)
            || v.Contains("Bearer ", StringComparison.OrdinalIgnoreCase))
            return "[redacted]";
        return v.Length > 500 ? v[..500] : v;
    }
}

public sealed class AuditQueryService(
    IsSuasDbContext db,
    IAuthorizationPolicyService policy,
    ICurrentUser currentUser,
    IAuditWriter audit) : IAuditQueryService
{
    public async Task<IReadOnlyList<AuditEventDto>> SearchAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? entityType,
        Guid? entityId,
        string? action,
        string? actorUserName,
        string? result,
        CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.AuditRead);
        var items = await Query(from, to, entityType, entityId, action, actorUserName, result)
            .OrderByDescending(x => x.OccurredAt)
            .Take(500)
            .ToListAsync(cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task<string> ExportCsvAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? entityType,
        Guid? entityId,
        string? action,
        string? actorUserName,
        string? result,
        CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.AuditExport);
        var items = await Query(from, to, entityType, entityId, action, actorUserName, result)
            .OrderByDescending(x => x.OccurredAt)
            .Take(5000)
            .ToListAsync(cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("occurredAt;actor;action;entityType;entityId;result");
        foreach (var x in items)
        {
            sb.Append(x.OccurredAt.ToString("o", CultureInfo.InvariantCulture)).Append(';')
                .Append(Esc(x.ActorUserName)).Append(';')
                .Append(Esc(x.Action)).Append(';')
                .Append(Esc(x.EntityType)).Append(';')
                .Append(x.EntityId?.ToString() ?? "").Append(';')
                .Append(Esc(x.Result)).AppendLine();
        }

        // Export itself is audited.
        await audit.WriteAsync(
            "Audit.Export",
            "AuditEvent",
            null,
            null,
            new { count = items.Count, from, to, entityType, action, actorUserName, exportedBy = currentUser.UserName },
            cancellationToken: cancellationToken);

        return sb.ToString();
    }

    private IQueryable<AuditEvent> Query(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? entityType,
        Guid? entityId,
        string? action,
        string? actorUserName,
        string? result)
    {
        // Append-only: query never updates/deletes audit rows.
        var query = db.AuditEvents.AsNoTracking().AsQueryable();
        if (from is not null) query = query.Where(x => x.OccurredAt >= from);
        if (to is not null) query = query.Where(x => x.OccurredAt <= to);
        if (!string.IsNullOrWhiteSpace(entityType)) query = query.Where(x => x.EntityType == entityType);
        if (entityId is not null) query = query.Where(x => x.EntityId == entityId);
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(x => x.Action == action);
        if (!string.IsNullOrWhiteSpace(actorUserName)) query = query.Where(x => x.ActorUserName == actorUserName);
        if (!string.IsNullOrWhiteSpace(result)) query = query.Where(x => x.Result == result);
        return query;
    }

    private static AuditEventDto Map(AuditEvent x) =>
        new(x.Id, x.OccurredAt, x.ActorAccountId, x.ActorUserName, x.Action, x.EntityType,
            x.EntityId, x.Result, x.BeforeJson, x.AfterJson);

    private static string Esc(string? v)
    {
        if (string.IsNullOrEmpty(v)) return "";
        return v.Contains(';') || v.Contains('"') ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
    }
}

public sealed class SystemInfoService(
    IsSuasDbContext db,
    IAuthorizationPolicyService policy,
    IConfiguration configuration,
    IHostEnvironment env,
    IClock clock,
    IObjectStorage objectStorage,
    IEmailSender emailSender,
    IAirspaceMapAdapter airspace) : ISystemInfoService
{
    public async Task<SystemInfoDto> GetAsync(CancellationToken cancellationToken = default)
    {
        policy.EnsurePermission(Permissions.SystemInfoRead);
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0";
        var provider = (configuration["Database:Provider"] ?? "").Trim();
        var engine = configuration.GetValue("Database:UseInMemory", false) || provider.Equals("InMemory", StringComparison.OrdinalIgnoreCase)
            ? "InMemory"
            : provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase) ? "Sqlite" : "PostgreSQL";
        var now = clock.UtcNow;
        var activeRules = await db.ConfigRules.AsNoTracking()
            .Where(r => r.IsActive && r.EffectiveFrom <= now && (r.EffectiveTo == null || r.EffectiveTo > now))
            .ToListAsync(cancellationToken);
        var lookups = await db.LookupCodes.AsNoTracking().CountAsync(c => c.IsActive, cancellationToken);
        var connectors = await db.IntegrationConnectors.AsNoTracking().CountAsync(c => c.IsActive, cancellationToken);
        var oidcConfigured = !string.IsNullOrWhiteSpace(configuration["Auth:Oidc:Authority"]);

        return new SystemInfoDto(
            version,
            env.EnvironmentName,
            engine,
            now,
            activeRules.Count,
            lookups,
            connectors,
            activeRules.Select(r => r.Key).Distinct().OrderBy(k => k).ToList(),
            objectStorage.ProviderName,
            emailSender.ProviderName,
            airspace.ProviderCode,
            oidcConfigured);
    }
}
