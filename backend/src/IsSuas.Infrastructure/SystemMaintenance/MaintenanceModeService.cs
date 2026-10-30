using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IsSuas.Application.Abstractions;
using IsSuas.Application.Policies;
using IsSuas.Application.SystemMaintenance;
using IsSuas.Domain.Common;
using IsSuas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IsSuas.Infrastructure.SystemMaintenance;

/// <summary>
/// Zdieľaná (singleton) pamäť posledného stavu, aby middleware nečítal DB pri každej požiadavke.
/// Je to len cache — zdrojom pravdy je vždy databáza.
/// </summary>
public sealed class MaintenanceModeStateCache
{
    private readonly object _gate = new();
    private MaintenanceModeState? _value;
    private DateTimeOffset _loadedAt;

    public MaintenanceModeState? LastKnown
    {
        get { lock (_gate) return _value; }
    }

    public bool TryGet(DateTimeOffset now, TimeSpan ttl, out MaintenanceModeState state)
    {
        lock (_gate)
        {
            if (_value is not null && now - _loadedAt < ttl)
            {
                state = _value;
                return true;
            }
        }

        state = MaintenanceModeState.Inactive;
        return false;
    }

    public void Set(MaintenanceModeState state, DateTimeOffset now)
    {
        lock (_gate)
        {
            _value = state;
            _loadedAt = now;
        }
    }

    public void Invalidate()
    {
        lock (_gate)
            _loadedAt = DateTimeOffset.MinValue;
    }
}

/// <summary>
/// Stav je uložený v existujúcej tabuľke ConfigRules (kľúč system.maintenance_mode).
/// Výhody: žiadna nová migrácia, funguje na PostgreSQL aj SQLite, zdieľajú ho všetky inštancie,
/// každé prepnutie je nová verzia (história) + záznam v audite.
/// </summary>
public sealed class MaintenanceModeService(
    IsSuasDbContext db,
    IAuditWriter audit,
    IClock clock,
    IOptionsMonitor<MaintenanceModeOptions> options,
    MaintenanceModeStateCache cache,
    ILogger<MaintenanceModeService> logger) : IMaintenanceModeService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record StoredValue(bool Enabled, string? Message, DateTimeOffset? PlannedEndAt, string? ChangedBy);

    public async Task<MaintenanceModeState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        var opts = options.CurrentValue;
        if (opts.ForceEnabled)
        {
            return new MaintenanceModeState(
                true,
                string.IsNullOrWhiteSpace(opts.ForceMessage) ? MaintenanceModeConstants.DefaultMessage : opts.ForceMessage.Trim(),
                null,
                true,
                null,
                "konfigurácia servera");
        }

        var now = clock.UtcNow;
        var ttl = TimeSpan.FromSeconds(Math.Clamp(opts.CacheSeconds, 0, 300));
        if (cache.TryGet(now, ttl, out var cached))
            return cached;

        try
        {
            var state = await LoadFromDatabaseAsync(cancellationToken);
            cache.Set(state, now);
            return state;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // DB nedostupná: necháme posledný známy stav (alebo vypnuté). Núdzovo sa dá použiť ForceEnabled.
            logger.LogWarning(ex, "Stav režimu údržby sa nepodarilo načítať z databázy.");
            return cache.LastKnown ?? MaintenanceModeState.Inactive;
        }
    }

    public async Task<MaintenanceModeStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var state = await GetStateAsync(cancellationToken);
        return new MaintenanceModeStatusDto(
            state.IsActive,
            state.IsActive ? state.Message : null,
            state.IsActive ? state.PlannedEndAt : null);
    }

    public bool IsKillSwitchConfigured =>
        options.CurrentValue.GetValidKillSwitchPath() is not null && options.CurrentValue.HasValidKillSwitchKey();

    public async Task<KillSwitchStatusDto?> SetWithKillSwitchKeyAsync(
        KillSwitchRequest request,
        string? clientAddress,
        CancellationToken cancellationToken = default)
    {
        if (!IsKillSwitchConfigured)
            return null;

        var client = string.IsNullOrWhiteSpace(clientAddress) ? "neznáma IP" : clientAddress;
        if (!KeyMatches(request.Key, options.CurrentValue.KillSwitchKey!.Trim()))
        {
            logger.LogWarning("Núdzový vypínač: nesprávny kľúč z {Client}.", client);
            await audit.WriteAsync("MaintenanceMode.KillSwitchDenied", nameof(ConfigRule), null, null,
                new { client }, result: "Denied", cancellationToken: cancellationToken);
            return null;
        }

        var message = string.IsNullOrWhiteSpace(request.Message) ? null : request.Message.Trim();
        if (message is { Length: > MaintenanceModeConstants.MaxMessageLength })
            throw new ValidationException($"Správa môže mať najviac {MaintenanceModeConstants.MaxMessageLength} znakov.");

        var now = clock.UtcNow;
        var changedBy = $"núdzový vypínač ({client})";
        var before = await LoadFromDatabaseAsync(cancellationToken);

        var rows = await db.ConfigRules
            .Where(r => r.Key == MaintenanceModeConstants.ConfigKey)
            .ToListAsync(cancellationToken);
        var version = rows.Count == 0 ? 1 : rows.Max(r => r.Version) + 1;
        foreach (var open in rows.Where(r => r.EffectiveTo == null))
            open.EffectiveTo = now;

        var stored = new StoredValue(
            request.Enabled,
            request.Enabled ? message : null,
            request.Enabled ? request.PlannedEndAt : null,
            changedBy);

        var entity = new ConfigRule
        {
            Key = MaintenanceModeConstants.ConfigKey,
            ValueJson = JsonSerializer.Serialize(stored, JsonOptions),
            EffectiveFrom = now,
            Version = version,
            DescriptionSk = request.Enabled ? "Režim údržby zapnutý" : "Režim údržby vypnutý"
        };
        db.ConfigRules.Add(entity);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Dve prepnutia naraz → unikátny index (Key, Version).
            throw new ConflictException("Režim údržby sa práve menil. Skúste to znova.");
        }

        cache.Invalidate();
        var after = await GetStateAsync(cancellationToken);
        await audit.WriteAsync(
            request.Enabled ? "MaintenanceMode.KillSwitch.Enable" : "MaintenanceMode.KillSwitch.Disable",
            nameof(ConfigRule),
            entity.Id,
            before,
            after,
            cancellationToken: cancellationToken);

        logger.LogWarning("Režim údržby {State} cez núdzový vypínač z {Client}.",
            request.Enabled ? "ZAPNUTÝ" : "VYPNUTÝ", client);

        return new KillSwitchStatusDto(after.IsActive, after.Message, after.PlannedEndAt,
            after.ForcedByConfiguration, after.ChangedAt, after.ChangedBy);
    }

    // Porovnanie v konštantnom čase (cez hash), aby sa kľúč nedal uhádnuť meraním času odpovede.
    private static bool KeyMatches(string? provided, string expected)
    {
        if (string.IsNullOrEmpty(provided))
            return false;
        var a = SHA256.HashData(Encoding.UTF8.GetBytes(provided.Trim()));
        var b = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(a, b);
    }

    private async Task<MaintenanceModeState> LoadFromDatabaseAsync(CancellationToken cancellationToken)
    {
        // Zoradenie len podľa int stĺpca Version — funguje aj na SQLite (DateTimeOffset tam nejde triediť).
        var row = await db.ConfigRules.AsNoTracking()
            .Where(r => r.Key == MaintenanceModeConstants.ConfigKey && r.IsActive)
            .OrderByDescending(r => r.Version)
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null || row.EffectiveTo is not null)
            return MaintenanceModeState.Inactive;

        StoredValue? value;
        try
        {
            value = JsonSerializer.Deserialize<StoredValue>(row.ValueJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Neplatná hodnota režimu údržby v ConfigRules (verzia {Version}).", row.Version);
            return MaintenanceModeState.Inactive;
        }

        if (value is null || !value.Enabled)
            return MaintenanceModeState.Inactive with { ChangedAt = row.EffectiveFrom, ChangedBy = value?.ChangedBy };

        return new MaintenanceModeState(
            true,
            value.Message ?? MaintenanceModeConstants.DefaultMessage,
            value.PlannedEndAt,
            false,
            row.EffectiveFrom,
            value.ChangedBy);
    }
}
