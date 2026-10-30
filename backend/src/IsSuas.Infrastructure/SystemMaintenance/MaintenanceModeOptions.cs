using System.Text.RegularExpressions;

namespace IsSuas.Infrastructure.SystemMaintenance;

/// <summary>
/// Konfigurácia režimu údržby (sekcia "MaintenanceMode" v appsettings / premenné prostredia MaintenanceMode__*).
/// Nič tu nezávisí od domény ani hostingu. Tajné hodnoty patria len do .env / premenných prostredia, nie do gitu.
/// </summary>
public sealed partial class MaintenanceModeOptions
{
    public const string SectionName = "MaintenanceMode";
    public const int MinKillSwitchKeyLength = 32;

    /// <summary>
    /// Núdzový vypínač bez databázy aj bez webu: true = aplikácia je v údržbe bez ohľadu na stav v DB.
    /// Zmena v appsettings.json sa prejaví bez reštartu; premenná prostredia po reštarte kontajnera.
    /// </summary>
    public bool ForceEnabled { get; set; }

    /// <summary>Text pre používateľov, keď je údržba vynútená konfiguráciou.</summary>
    public string? ForceMessage { get; set; }

    /// <summary>Ako dlho (s) si inštancia pamätá stav z DB. Pri viacerých inštanciách = max. oneskorenie prepnutia.</summary>
    public int CacheSeconds { get; set; } = 5;

    /// <summary>Hodnota hlavičky Retry-After, ak nie je zadaný plánovaný koniec.</summary>
    public int RetryAfterSeconds { get; set; } = 300;

    /// <summary>Počas údržby pozastaviť periodické úlohy na pozadí (notifikácie, expirácie, pripomienky).</summary>
    public bool PauseBackgroundJobs { get; set; } = true;

    /// <summary>
    /// Tajná adresa stránky núdzového vypínača, napr. "k7Qm2xP9vL4tR8wZ" → https://domena/k7Qm2xP9vL4tR8wZ.
    /// Len písmená, čísla, '-' a '_', 16–128 znakov. Prázdne/neplatné = vypínač neexistuje.
    /// Zmena sa prejaví po reštarte aplikácie.
    /// </summary>
    public string? KillSwitchPath { get; set; }

    /// <summary>Tajný kľúč núdzového vypínača, min. 32 znakov. Prázdny = vypínač neexistuje.</summary>
    public string? KillSwitchKey { get; set; }

    /// <summary>Platná tajná adresa vo forme "/abc..." alebo null.</summary>
    public string? GetValidKillSwitchPath()
    {
        var path = KillSwitchPath?.Trim().Trim('/');
        return !string.IsNullOrEmpty(path) && PathPattern().IsMatch(path) ? "/" + path : null;
    }

    public bool HasValidKillSwitchKey() => (KillSwitchKey?.Trim().Length ?? 0) >= MinKillSwitchKeyLength;

    [GeneratedRegex("^[A-Za-z0-9_-]{16,128}$")]
    private static partial Regex PathPattern();
}
