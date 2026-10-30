namespace IsSuas.Application.SystemMaintenance;

/// <summary>
/// Režim údržby (vypínač celej aplikácie). Nepliesť si s údržbou UAS (namespace IsSuas.Application.Maintenance).
/// Počas údržby nefunguje žiadny účet (ani gestor). Zapína a vypína sa len núdzovým vypínačom
/// na tajnej adrese s tajným kľúčom, alebo na serveri cez MaintenanceMode:ForceEnabled.
/// Stav je v databáze, takže ho zdieľajú všetky inštancie a nezávisí od domény ani hostingu.
/// </summary>
public interface IMaintenanceModeService
{
    /// <summary>Aktuálny stav (s krátkou cache). Nevyžaduje prihlásenie.</summary>
    Task<MaintenanceModeState> GetStateAsync(CancellationToken cancellationToken = default);

    /// <summary>Verejný stav pre UI (bez informácie, kto a kedy prepínal).</summary>
    Task<MaintenanceModeStatusDto> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>True, ak je na serveri nastavená tajná adresa aj tajný kľúč núdzového vypínača.</summary>
    bool IsKillSwitchConfigured { get; }

    /// <summary>Overí tajný kľúč a prepne režim údržby. Vráti null, ak kľúč nesedí (pokus ide do auditu).</summary>
    Task<KillSwitchStatusDto?> SetWithKillSwitchKeyAsync(KillSwitchRequest request, string? clientAddress, CancellationToken cancellationToken = default);
}

public static class MaintenanceModeConstants
{
    /// <summary>Kľúč v tabuľke ConfigRules (každé prepnutie = nová verzia, história zostáva).</summary>
    public const string ConfigKey = "system.maintenance_mode";

    public const string DefaultMessage = "Prebieha plánovaná údržba systému. Skúste to prosím neskôr.";

    public const int MaxMessageLength = 1000;
}

public sealed record MaintenanceModeState(
    bool IsActive,
    string? Message,
    DateTimeOffset? PlannedEndAt,
    bool ForcedByConfiguration,
    DateTimeOffset? ChangedAt,
    string? ChangedBy)
{
    public static readonly MaintenanceModeState Inactive = new(false, null, null, false, null, null);
}

public sealed record MaintenanceModeStatusDto(bool IsActive, string? Message, DateTimeOffset? PlannedEndAt);

public sealed record KillSwitchStatusDto(
    bool IsActive,
    string? Message,
    DateTimeOffset? PlannedEndAt,
    bool ForcedByConfiguration,
    DateTimeOffset? ChangedAt,
    string? ChangedBy);

/// <summary>Enabled = true znamená ZAPNÚŤ údržbu (aplikácia je pre všetkých vypnutá).</summary>
public sealed record KillSwitchRequest(string? Key, bool Enabled, string? Message, DateTimeOffset? PlannedEndAt);
