namespace IsSuas.Application.Trash;

/// <summary>Kategórie koša. Kľúč sa používa v API aj v UI (data-trash-category).</summary>
public static class TrashCategories
{
    public const string Pilots = "pilots";
    public const string Certificates = "certificates";
    public const string Flights = "flights";
    public const string Operations = "operations";
    public const string Uas = "uas";
    public const string Defects = "defects";
    public const string Incidents = "incidents";
    public const string Accounts = "accounts";

    /// <summary>Poradie a slovenské názvy kategórií v koši.</summary>
    public static readonly IReadOnlyList<(string Key, string NameSk)> All =
    [
        (Pilots, "Piloti"),
        (Certificates, "Osvedčenia"),
        (Flights, "Denník letov"),
        (Operations, "Žiadosti o let"),
        (Uas, "Register UAS"),
        (Defects, "Poruchy"),
        (Incidents, "Incidenty"),
        (Accounts, "Prevádzkovatelia a používatelia")
    ];

    public static string NameOf(string key) =>
        All.FirstOrDefault(c => c.Key == key).NameSk ?? key;
}

public sealed record MoveToTrashRequest(string Category, Guid Id, string? Reason);

public sealed record PurgeTrashRequest(string? Confirm);

public sealed record TrashCategorySummaryDto(string Category, string NameSk, int Count);

public sealed record TrashEntryDto(
    Guid Id,
    string Category,
    string CategoryNameSk,
    Guid EntityId,
    string Title,
    string? Reason,
    DateTimeOffset DeletedAt,
    string DeletedBy);

public sealed record TrashActionResultDto(string Message);

/// <summary>Odkaz na záznam, ktorý sa má zmazať (kategória + id).</summary>
public sealed record TrashItemRef(string Category, Guid Id);

/// <summary>Hromadný presun do koša (napr. pilot aj s jeho letmi).</summary>
public sealed record MoveToTrashBatchRequest(IReadOnlyList<TrashItemRef> Items, string? Reason);

public sealed record RestoreTrashBatchRequest(IReadOnlyList<Guid> EntryIds);

/// <summary>Hromadné trvalé vymazanie — záznamy z koša (EntryIds) a/alebo ich väzby (Items).</summary>
public sealed record PurgeTrashBatchRequest(IReadOnlyList<Guid>? EntryIds, IReadOnlyList<TrashItemRef>? Items, string? Confirm);

/// <summary>Záznam, ktorý sa viaže na mazaný záznam a treba ho zmazať skôr.</summary>
public sealed record TrashDependentDto(string Category, string CategoryNameSk, Guid Id, string Title, bool InTrash, int Depth);

/// <summary>
/// Čo bráni vymazaniu: Items sa dajú zmazať spolu s ním (zaškrtávací zoznam),
/// NotDeletable sú väzby, ktoré sa nemažú (napr. riadky mesačných hlásení).
/// </summary>
public sealed record TrashDependentsDto(
    string Category,
    Guid Id,
    string Title,
    IReadOnlyList<TrashDependentDto> Items,
    IReadOnlyList<string> NotDeletable);
