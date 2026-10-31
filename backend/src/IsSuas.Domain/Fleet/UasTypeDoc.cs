using IsSuas.Domain.Common;

namespace IsSuas.Domain.Fleet;

/// <summary>
/// Technická dokumentácia typu UAS (katalóg „Technická dokumentácia“): vlastné typy, skrytie vstavaných typov
/// a priložený PDF manuál výrobcu. Údaje (sekcie a tabuľky špecifikácií) sú uložené ako JSON.
/// </summary>
public class UasTypeDoc : AuditableEntity
{
    /// <summary>Identifikátor typu (napr. „dji-mavic-3t“), rovnaký ako v katalógu na webe.</summary>
    public string Slug { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    /// <summary>JSON: { manufacturer, model, classEu, tagline, matchModels[], sections[{ title, rows[[param, hodnota]] }] }.</summary>
    public string DataJson { get; set; } = "{}";
    /// <summary>Typ je v katalógu skrytý (odobratý gestorom).</summary>
    public bool IsHidden { get; set; }
    public string? ManualStorageKey { get; set; }
    public string? ManualFileName { get; set; }
    public long? ManualSizeBytes { get; set; }
    public DateTimeOffset? ManualUploadedAt { get; set; }
}
