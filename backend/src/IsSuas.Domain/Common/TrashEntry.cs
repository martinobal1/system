namespace IsSuas.Domain.Common;

/// <summary>
/// Záznam v koši. Zmazaná entita zostáva v databáze, len ju globálny filter skryje všade v aplikácii,
/// kým existuje otvorený záznam v koši (RestoredAt aj PurgedAt sú null).
/// Záznam koša sa nikdy nemaže — obnovenie aj trvalé vymazanie sa iba doplnia (história).
/// </summary>
public class TrashEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Kategória v koši (napr. "pilots", "flights") — viď TrashCategories.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Názov typu entity (napr. "Pilot").</summary>
    public string EntityType { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    /// <summary>Čitateľný názov v čase zmazania (meno pilota, číslo letu...).</summary>
    public string Title { get; set; } = string.Empty;

    public string? OrgUnitPath { get; set; }
    public string? Reason { get; set; }

    public DateTimeOffset DeletedAt { get; set; }
    public Guid? DeletedByAccountId { get; set; }
    public string DeletedByUserName { get; set; } = string.Empty;

    public DateTimeOffset? RestoredAt { get; set; }
    public string? RestoredByUserName { get; set; }

    public DateTimeOffset? PurgedAt { get; set; }
    public string? PurgedByUserName { get; set; }
}
